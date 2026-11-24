using System;
using System.Collections;
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
        }

        protected virtual void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
        }

        protected virtual void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
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
                if (VisualRenderers[i] != null && VisualRenderers[i].material != null)
                {
                    var m = VisualRenderers[i].material;
                    OriginalColors[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
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

            // Knockback leve
            Vector3 kb = hit.KnockbackDir;
            kb.y = 0f;
            if (kb.sqrMagnitude > 0.001f)
                transform.position += kb.normalized * (hit.IsHeavy ? 0.55f : 0.25f);

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
            for (int i = 0; i < VisualRenderers.Length; i++)
            {
                if (VisualRenderers[i] == null) continue;
                var m = VisualRenderers[i].material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else m.color = c;
            }
        }

        protected void RestoreVisualTint()
        {
            if (VisualRenderers == null || OriginalColors == null) return;
            for (int i = 0; i < VisualRenderers.Length; i++)
            {
                if (VisualRenderers[i] == null) continue;
                var m = VisualRenderers[i].material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", OriginalColors[i]);
                else m.color = OriginalColors[i];
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
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.20f, 0.95f, 1f, 1f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.25f, 0.95f, 1f) * 2.0f);
                }
                rend.sharedMaterial = mat;
            }
            var pickup = cellGo.AddComponent<EnergyCellPickup>();
            pickup.cellAmount = 1;
            Debug.Log("[Manifestation] Celula de Energia dropada na morte!");
        }
    }
}
