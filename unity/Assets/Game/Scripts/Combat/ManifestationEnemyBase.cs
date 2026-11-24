using System;
using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Collision;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Powers;
using Studio.Game.Repair;
using Studio.Game.Tool;
using UnityEngine;

namespace Studio.Game.Combat
{
    public enum EnemyFsmState
    {
        IdlePatrol,
        Chase,
        Telegraph,
        Attack,
        Stagger,
        Dead
    }

    /// <summary>
    /// Base compartilhada das Manifestacoes (#94 — Estilhaco, Flutuante e extensivel para futuros inimigos):
    ///   - FSM limpa e honesta: IdlePatrol -> Chase -> Telegraph -> Attack -> Stagger -> Dead
    ///   - Implementa IDamageable (MultiToolController) e ITimeScalable (Estase #93)
    ///   - Bloqueia IA automaticamente durante cutscenes, dialogo, pause e reparo (regressao zero)
    ///   - Feedback de dano: flash branco + stagger + hitstop da ferramenta
    ///   - Morte: dissolve em particulas de glitch + som reverso + chance de dropar EnergyCellPickup
    /// </summary>
    public abstract class ManifestationEnemyBase : MonoBehaviour, IDamageable, ITimeScalable
    {
        [Header("Config & Audio")]
        public CombatBalanceConfig config;
        public AudioClip telegraphClip;
        public AudioClip dissolveReverseClip;

        private CombatBalanceConfig _runtimeCfg;
        protected CombatBalanceConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<CombatBalanceConfig>();
                return _runtimeCfg;
            }
        }

        public EnemyFsmState State { get; protected set; } = EnemyFsmState.IdlePatrol;
        public float CurrentHp { get; protected set; }
        public abstract float MaxHp { get; }
        public abstract float CellDropChance { get; }
        public bool IsAlive => State != EnemyFsmState.Dead && CurrentHp > 0f;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public Transform ScalableTransform => transform;

        public event Action<ManifestationEnemyBase> OnEnemyDied;

        protected Transform PlayerTarget;
        protected Vector3 SpawnOrigin;
        protected float StateTimer;
        protected float AttackCooldownTimer;
        protected AudioSource EnemyAudio;
        protected Renderer[] VisualRenderers;
        protected Color[] OriginalColors;

        private float _hitFlashTimer;
        private CharacterController _enemyCc;
        private MaterialPropertyBlock _tintMpb;
        private static Material _cachedDissolveParticleMat;
        private static Material _cachedCellPickupMat;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId     = Shader.PropertyToID("_Color");

        /// <summary>Flag QA exclusiva para gravacao ANTES (magenta) vs DEPOIS (ciano URP) do FIX 1.</summary>
        public static bool SimulateLegacyMagentaParticleBug { get; set; }

        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;
        private Cutscene04SkyCrackedController _cs04;

        public void SetLocalTimeScale(float scale) => LocalTimeScale = scale;

        protected virtual void Awake()
        {
            gameObject.layer = CollisionMatrixBootstrap.LayerEnemy;
            SpawnOrigin = transform.position;
            CurrentHp = MaxHp;
            _enemyCc = GetComponent<CharacterController>();
            _tintMpb = new MaterialPropertyBlock();

            EnemyAudio = GetComponent<AudioSource>();
            if (EnemyAudio == null) EnemyAudio = gameObject.AddComponent<AudioSource>();
            EnemyAudio.playOnAwake = false;
            EnemyAudio.spatialBlend = 0.65f;
            if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                EnemyAudio.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;

            CacheVisualRenderers();
        }

        protected virtual void OnEnable()
        {
            StasisFieldSystem.Register(this);
            CombatGroupCoordinator.Register(this);
        }

        protected virtual void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
            CombatGroupCoordinator.Unregister(this);
        }

        public virtual void ApplyParryStagger(float durationSec)
        {
            if (!IsAlive) return;
            CombatGroupCoordinator.ReleaseAttackToken(this);
            State = EnemyFsmState.Stagger;
            StateTimer = Mathf.Max(0.8f, durationSec);
            SetVisualTint(new Color(0.32f, 1.0f, 0.88f, 1f));
        }

        protected virtual void Start()
        {
            var dez = PlayerRegistry.Resolve();
            if (dez != null) PlayerTarget = dez.transform;
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            _cs04 = FindAnyObjectByType<Cutscene04SkyCrackedController>();
        }

        protected void CacheVisualRenderers()
        {
            VisualRenderers = GetComponentsInChildren<Renderer>(true);
            OriginalColors = new Color[VisualRenderers.Length];
            for (int i = 0; i < VisualRenderers.Length; i++)
            {
                // FIX 9: usa sharedMaterial (ZERO instanciacao de material) + MaterialPropertyBlock para tint
                if (VisualRenderers[i] != null && VisualRenderers[i].sharedMaterial != null)
                {
                    var m = VisualRenderers[i].sharedMaterial;
                    OriginalColors[i] = m.HasProperty(BaseColorPropId) ? m.GetColor(BaseColorPropId) : (m.HasProperty(ColorPropId) ? m.GetColor(ColorPropId) : Color.white);
                }
            }
        }

        protected bool IsCombatBlocked()
        {
            if (Time.timeScale <= 0.01f) return true;
            if (_cs01 != null && _cs01.IsPlaying) return true;
            if (_cs02 != null && _cs02.IsPlaying) return true;
            if (_cs03 != null && _cs03.IsPlaying) return true;
            if (_cs04 != null && _cs04.IsPlaying) return true;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return true;
            if (RepairRitualController.Instance != null && (RepairRitualController.Instance.IsRepairing || RepairRitualController.Instance.IsPlayingFinalBeat)) return true;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return true;
            return false;
        }

        public virtual void ApplyDamage(ToolHitInfo hit)
        {
            if (!IsAlive) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - hit.Damage);
            _hitFlashTimer = Cfg.hitFlashDurationSec;
            SetVisualTint(Color.white);

            // FIX 8: Knockback seguro contra paredes (raycast curto em LayerEnvironment|LayerDefault + _enemyCc.Move se disponivel)
            Vector3 kb = hit.KnockbackDir;
            kb.y = 0f;
            if (kb.sqrMagnitude > 0.001f)
            {
                Vector3 dir = kb.normalized;
                float dist = hit.IsHeavy ? 0.55f : 0.25f;
                int wallMask = (1 << CollisionMatrixBootstrap.LayerEnvironment) | (1 << CollisionMatrixBootstrap.LayerDefault);
                Vector3 probeOrigin = transform.position + Vector3.up * 0.75f;
                if (!Physics.Raycast(probeOrigin, dir, dist + 0.45f, wallMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 push = dir * dist;
                    if (_enemyCc != null && _enemyCc.enabled) _enemyCc.Move(push);
                    else transform.position += push;
                }
            }

            if (CurrentHp <= 0f)
            {
                BeginDeathDissolve();
                return;
            }

            State = EnemyFsmState.Stagger;
            StateTimer = GetStaggerDuration() * (hit.IsHeavy ? 1.35f : 1.0f);
        }

        protected abstract float GetStaggerDuration();

        protected void SetVisualTint(Color c)
        {
            if (VisualRenderers == null) return;
            if (_tintMpb == null) _tintMpb = new MaterialPropertyBlock();
            for (int i = 0; i < VisualRenderers.Length; i++)
            {
                var r = VisualRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_tintMpb);
                _tintMpb.SetColor(BaseColorPropId, c);
                _tintMpb.SetColor(ColorPropId, c);
                r.SetPropertyBlock(_tintMpb);
            }
        }

        protected void RestoreVisualTint()
        {
            if (VisualRenderers == null) return;
            for (int i = 0; i < VisualRenderers.Length; i++)
            {
                if (VisualRenderers[i] != null)
                    VisualRenderers[i].SetPropertyBlock(null);
            }
        }

        protected void PlayEnemySfx(AudioClip clip, float vol, float pitch = 1.0f)
        {
            if (clip == null || EnemyAudio == null) return;
            EnemyAudio.pitch = pitch;
            float sc = EnemyAudio.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
            EnemyAudio.PlayOneShot(clip, vol * sc);
        }

        protected virtual void Update()
        {
            if (State == EnemyFsmState.Dead) return;

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.unscaledDeltaTime;
                if (_hitFlashTimer <= 0f && State != EnemyFsmState.Telegraph)
                    RestoreVisualTint();
            }

            if (IsCombatBlocked()) return;

            float dt = Time.deltaTime * LocalTimeScale;
            if (AttackCooldownTimer > 0f)
                AttackCooldownTimer = Mathf.Max(0f, AttackCooldownTimer - dt);

            TickFsm(dt);
        }

        protected abstract void TickFsm(float dt);

        protected void BeginDeathDissolve()
        {
            State = EnemyFsmState.Dead;
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            PlayEnemySfx(dissolveReverseClip, 0.90f, 1.0f);
            SpawnGlitchDissolveParticles(transform.position + Vector3.up * 0.8f);
            TrySpawnCellDrop();
            OnEnemyDied?.Invoke(this);
            StartCoroutine(RunDissolveScaleOut());
        }

        private IEnumerator RunDissolveScaleOut()
        {
            Vector3 startScale = transform.localScale;
            float dur = Mathf.Max(0.15f, Cfg.deathDissolveSec);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, u);
                SetVisualTint(Color.Lerp(new Color(0.25f, 0.95f, 1f, 1f), Color.black, u));
                yield return null;
            }
            Destroy(gameObject);
        }

        private void SpawnGlitchDissolveParticles(Vector3 pos)
        {
            var pGo = new GameObject("GlitchDissolve_VFX");
            pGo.transform.position = pos;
            var ps = pGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 0.42f;
            main.startSpeed = 3.2f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.25f, 0.92f, 1f, 1f);
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            // FIX 1 (P1): Material URP/Particles/Unlit cacheado estaticamente (ZERO magenta!)
            var psr = pGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null && !SimulateLegacyMagentaParticleBug)
            {
                if (_cachedDissolveParticleMat == null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
                    if (sh != null)
                    {
                        _cachedDissolveParticleMat = new Material(sh);
                        _cachedDissolveParticleMat.name = "Mat_GlitchDissolve_URP_Cached";
                        if (_cachedDissolveParticleMat.HasProperty(BaseColorPropId))
                            _cachedDissolveParticleMat.SetColor(BaseColorPropId, new Color(0.25f, 0.92f, 1f, 1f));
                        if (_cachedDissolveParticleMat.HasProperty(ColorPropId))
                            _cachedDissolveParticleMat.SetColor(ColorPropId, new Color(0.25f, 0.92f, 1f, 1f));
                    }
                }
                if (_cachedDissolveParticleMat != null)
                    psr.sharedMaterial = _cachedDissolveParticleMat;
            }

            ps.Play();
            Destroy(pGo, 1.2f);
        }

        private void TrySpawnCellDrop()
        {
            if (UnityEngine.Random.value > CellDropChance) return;
            var cellGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cellGo.name = "Dropped_EnergyCell";
            cellGo.transform.position = new Vector3(transform.position.x, Mathf.Max(0.55f, transform.position.y + 0.55f), transform.position.z);
            cellGo.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            cellGo.transform.localScale = Vector3.one * 0.28f;
            var col = cellGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            var rend = cellGo.GetComponent<Renderer>();
            if (rend != null)
            {
                // FIX 1: material URP/Lit cacheado estaticamente (zero shader quebrado, zero leak)
                if (_cachedCellPickupMat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    if (sh != null)
                    {
                        _cachedCellPickupMat = new Material(sh);
                        _cachedCellPickupMat.name = "Mat_DroppedEnergyCell_URP_Cached";
                        if (_cachedCellPickupMat.HasProperty(BaseColorPropId))
                            _cachedCellPickupMat.SetColor(BaseColorPropId, new Color(0.20f, 0.95f, 1f, 1f));
                        if (_cachedCellPickupMat.HasProperty("_EmissionColor"))
                        {
                            _cachedCellPickupMat.EnableKeyword("_EMISSION");
                            _cachedCellPickupMat.SetColor("_EmissionColor", new Color(0.25f, 0.95f, 1f) * 2.0f);
                        }
                    }
                }
                if (_cachedCellPickupMat != null)
                    rend.sharedMaterial = _cachedCellPickupMat;
            }
            var pickup = cellGo.AddComponent<EnergyCellPickup>();
            pickup.cellAmount = 1;
            Debug.Log("[Manifestation] Celula de Energia dropada na morte!");
        }
    }
}
