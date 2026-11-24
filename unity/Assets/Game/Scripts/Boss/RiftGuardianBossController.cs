using System;
using System.Collections;
using System.Collections.Generic;
using Studio.Game.Audio;
using Studio.Game.Collision;
using Studio.Game.Combat;
using Studio.Game.Epicenter;
using Studio.Game.Menu;
using Studio.Game.Powers;
using Studio.Game.Tool;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Boss
{
    public enum BossFsmState
    {
        Dormant,
        Chase,
        ChargeTelegraph,
        Charge,
        SlamTelegraph,
        SlamShockwave,
        SprayTelegraph,
        CoreExposedStagger,
        Dead
    }

    /// <summary>
    /// Mini-boss "Guardiao da Fenda" (#103) — o primeiro boss de CONCERTO:
    ///   - Pluga diretamente em EpicenterArenaBossStub.OnBossArenaTriggered na Arena Final do Epicentro
    ///   - F1: Investida com telegraph + Slam com onda expansiva (desviavel com Eco) -> Nucleo Exposto (stagger ~3s, 1.65x dano)
    ///   - F2 (<=50% HP): Invoca 2 Estilhacos TripoSG + Spray de projeteis lentos (Estase brilha aqui) + Slam com Nucleo Exposto
    ///   - Retry limpo: se Dez sofrer knockdown, respawna no checkpoint da arena e a luta reinicia limpa
    ///   - Morte: dissolve grande em glitch + silencio -> dropa CoreFragmentInteractable (flash holograma 0.5s + Fim do Ato 3)
    /// </summary>
    public sealed class RiftGuardianBossController : MonoBehaviour, IDamageable, ITimeScalable
    {
        [Header("Config & Prefabs")]
        public RiftGuardianConfig config;
        public CombatBalanceConfig minionBalanceConfig;
        public GameObject estilhacoPrefab;
        public Texture2D hologramWorkshopWifeTexture;

        [Header("Audio")]
        public AudioClip bossMusicClip;
        public AudioClip telegraphClip;
        public AudioClip slamClip;
        public AudioClip dissolveClip;
        public AudioClip hologramFlashClip;

        [Header("Conexao Arena")]
        public EpicenterArenaBossStub arenaStub;
        public Vector3 arenaCenter = new Vector3(116f, 0.9f, 0f);
        public Vector3 arenaRetryCheckpoint = new Vector3(104f, 1.05f, 0f);

        private RiftGuardianConfig _runtimeCfg;
        public RiftGuardianConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<RiftGuardianConfig>();
                return _runtimeCfg;
            }
        }

        public BossFsmState State { get; private set; } = BossFsmState.Dormant;
        public float CurrentHp { get; private set; }
        public float MaxHp => Cfg.maxHp;
        public bool IsAlive => State != BossFsmState.Dead && CurrentHp > 0f;
        public bool IsPhase2 { get; private set; }
        public bool IsCoreExposed => State == BossFsmState.CoreExposedStagger;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public Transform ScalableTransform => transform;

        public int RetryResetCount { get; private set; }
        public int Phase2TransitionCount { get; private set; }
        public int ShockwaveCount { get; private set; }
        public int SprayCount { get; private set; }
        public CoreFragmentInteractable SpawnedCoreFragment { get; private set; }

        public event Action OnBossDefeated;

        private Transform _player;
        private AudioSource _sfxSource;
        private AudioSource _bossMusicSource;
        private Renderer[] _renderers;
        private Color[] _origColors;
        private readonly List<GameObject> _summonedAdds = new List<GameObject>(4);

        private float _stateTimer;
        private float _cooldownTimer;
        private int _attackCycle;
        private Vector3 _chargeDir;
        private bool _chargeHitPlayer;
        private float _hitFlashTimer;

        // Boss HUD
        private CanvasGroup _bossHudGroup;
        private Image _hpFill;
        private Text _phaseLabel;

        public void SetLocalTimeScale(float scale) => LocalTimeScale = scale;

        private void Awake()
        {
            gameObject.layer = CollisionMatrixBootstrap.LayerEnemy;
            CurrentHp = MaxHp;
            CacheRenderers();
            EnsureAudioSources();
            BuildBossHud();
        }

        private void OnEnable()
        {
            StasisFieldSystem.Register(this);
        }

        private void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
        }

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null) _player = dez.transform;

            if (arenaStub == null) arenaStub = FindAnyObjectByType<EpicenterArenaBossStub>();
            if (arenaStub != null) arenaStub.OnBossArenaTriggered += StartBossFight;

            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.OnPlayerRespawned += HandlePlayerRespawnedRetry;
        }

        private void OnDestroy()
        {
            if (arenaStub != null) arenaStub.OnBossArenaTriggered -= StartBossFight;
            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.OnPlayerRespawned -= HandlePlayerRespawnedRetry;
        }

        public void StartBossFight()
        {
            if (State != BossFsmState.Dormant) return;
            // Define checkpoint da ARENA para retry limpo (sem repetir o Epicentro inteiro!)
            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.SetCheckpoint(arenaRetryCheckpoint);

            State = BossFsmState.Chase;
            _cooldownTimer = 0.8f;
            if (_bossHudGroup != null) _bossHudGroup.alpha = 1f;
            if (_bossMusicSource != null && bossMusicClip != null && !_bossMusicSource.isPlaying)
            {
                _bossMusicSource.clip = bossMusicClip;
                _bossMusicSource.pitch = 0.86f; // Variacao distorcida do tema (TEMP)
                _bossMusicSource.Play();
            }
            RefreshBossHud();
            Debug.Log("[RiftGuardian] Luta iniciada (FASE 1)! Checkpoint de retry configurado na Arena.");
        }

        /// <summary>
        /// Retry limpo apos knockdown do Dez na arena: restaura boss e limpa adds/projeteis.
        /// </summary>
        public void HandlePlayerRespawnedRetry()
        {
            if (State == BossFsmState.Dormant || State == BossFsmState.Dead) return;
            for (int i = 0; i < _summonedAdds.Count; i++)
                if (_summonedAdds[i] != null) Destroy(_summonedAdds[i]);
            _summonedAdds.Clear();

            var projs = FindObjectsByType<ManifestationProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < projs.Length; i++) Destroy(projs[i].gameObject);

            CurrentHp = MaxHp;
            IsPhase2 = false;
            _attackCycle = 0;
            _cooldownTimer = 1.1f;
            State = BossFsmState.Chase;
            transform.position = arenaCenter + new Vector3(6.5f, 0f, 0f);
            RestoreTint();
            RetryResetCount++;
            RefreshBossHud();
            Debug.Log($"[RiftGuardian] Retry limpo #{RetryResetCount}: Boss restaurado para 100% HP na Fase 1.");
        }

        public void ApplyDamage(ToolHitInfo hit)
        {
            if (!IsAlive || State == BossFsmState.Dormant) return;

            float mult = IsCoreExposed ? Cfg.exposedCoreDamageMultiplier : Cfg.armoredDamageMultiplier;
            float dmg = hit.Damage * mult;
            CurrentHp = Mathf.Max(0f, CurrentHp - dmg);
            _hitFlashTimer = 0.12f;
            SetTint(IsCoreExposed ? Color.yellow : Color.white);
            RefreshBossHud();

            // Transicao para Fase 2 aos 50% de HP
            if (!IsPhase2 && CurrentHp > 0f && (CurrentHp / MaxHp) <= Cfg.phase2Threshold01)
                EnterPhase2();

            if (CurrentHp <= 0f)
                BeginBossDeath();
        }

        private void EnterPhase2()
        {
            IsPhase2 = true;
            Phase2TransitionCount++;
            RefreshBossHud();
            PlaySfx(slamClip, 0.95f, 0.78f);

            // Invoca 2 Estilhacos TripoSG existentes
            if (estilhacoPrefab != null)
            {
                for (int i = 0; i < Cfg.phase2SummonEstilhacosCount; i++)
                {
                    float side = (i % 2 == 0) ? 1f : -1f;
                    Vector3 pos = arenaCenter + new Vector3(2.5f, 0.05f, side * 5.2f);
                    var go = Instantiate(estilhacoPrefab, pos, Quaternion.identity);
                    go.name = $"Guardiao_F2_Estilhaco_{i + 1}";
                    var e = go.GetComponent<ManifestationEnemyBase>();
                    if (e != null && minionBalanceConfig != null) e.config = minionBalanceConfig;
                    _summonedAdds.Add(go);
                }
            }
            Debug.Log("[RiftGuardian] FASE 2 ATIVADA (<=50% HP): 2 Estilhacos invocados + Spray de projeteis liberado!");
        }

        private void Update()
        {
            if (State == BossFsmState.Dormant || State == BossFsmState.Dead) return;
            if (Time.timeScale <= 0.01f) return;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return;

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.unscaledDeltaTime;
                if (_hitFlashTimer <= 0f && State != BossFsmState.ChargeTelegraph && State != BossFsmState.SlamTelegraph && State != BossFsmState.CoreExposedStagger)
                    RestoreTint();
            }

            if (_player == null) return;
            float dt = Time.deltaTime * LocalTimeScale;
            if (_cooldownTimer > 0f) _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            switch (State)
            {
                case BossFsmState.Chase:
                    if (dist > 3.0f)
                    {
                        transform.position += toPlayer.normalized * (Cfg.moveSpeed * dt);
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 6f * dt);
                    }
                    if (_cooldownTimer <= 0f)
                    {
                        _attackCycle++;
                        if (IsPhase2 && (_attackCycle % 3 == 0))
                        {
                            State = BossFsmState.SprayTelegraph;
                            _stateTimer = 0.65f;
                            SetTint(new Color(0.75f, 0.25f, 1.0f, 1f));
                            PlaySfx(telegraphClip, 0.85f, 0.85f);
                        }
                        else if (_attackCycle % 2 == 1)
                        {
                            State = BossFsmState.ChargeTelegraph;
                            _stateTimer = Cfg.chargeTelegraphSec;
                            _chargeDir = dist > 0.05f ? toPlayer.normalized : transform.forward;
                            transform.rotation = Quaternion.LookRotation(_chargeDir, Vector3.up);
                            SetTint(new Color(1.0f, 0.35f, 0.18f, 1f));
                            PlaySfx(telegraphClip, 0.85f, 1.05f);
                        }
                        else
                        {
                            State = BossFsmState.SlamTelegraph;
                            _stateTimer = Cfg.slamTelegraphSec;
                            SetTint(new Color(0.25f, 0.92f, 1.0f, 1f));
                            PlaySfx(telegraphClip, 0.88f, 0.72f);
                        }
                    }
                    break;

                case BossFsmState.ChargeTelegraph:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        RestoreTint();
                        State = BossFsmState.Charge;
                        _stateTimer = Cfg.chargeDurationSec;
                        _chargeHitPlayer = false;
                    }
                    break;

                case BossFsmState.Charge:
                    _stateTimer -= dt;
                    transform.position += _chargeDir * (Cfg.chargeSpeed * dt);
                    if (!_chargeHitPlayer && dist <= 2.3f && PlayerHealthController.Instance != null)
                    {
                        _chargeHitPlayer = true;
                        PlayerHealthController.Instance.TryTakeDamage(Cfg.attackDamageSegments, transform.position);
                    }
                    if (_stateTimer <= 0f)
                    {
                        _cooldownTimer = IsPhase2 ? Cfg.phase2CooldownSec : Cfg.phase1CooldownSec;
                        State = BossFsmState.Chase;
                    }
                    break;

                case BossFsmState.SlamTelegraph:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        RestoreTint();
                        State = BossFsmState.SlamShockwave;
                        PlaySfx(slamClip, 0.95f, 1.0f);
                        ShockwaveCount++;
                        StartCoroutine(RunShockwaveRing(transform.position));
                        // Apos o slam, o NUCLEO fica exposto por ~3s (janela de golpe carregado!)
                        State = BossFsmState.CoreExposedStagger;
                        _stateTimer = Cfg.coreExposedStaggerSec;
                        SetTint(new Color(0.30f, 1.0f, 0.90f, 1f));
                        RefreshBossHud();
                    }
                    break;

                case BossFsmState.SprayTelegraph:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        RestoreTint();
                        FirePhase2ProjectileSpray();
                        SprayCount++;
                        _cooldownTimer = Cfg.phase2CooldownSec;
                        State = BossFsmState.Chase;
                    }
                    break;

                case BossFsmState.CoreExposedStagger:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        RestoreTint();
                        _cooldownTimer = IsPhase2 ? Cfg.phase2CooldownSec : Cfg.phase1CooldownSec;
                        State = BossFsmState.Chase;
                        RefreshBossHud();
                    }
                    break;
            }
        }

        private IEnumerator RunShockwaveRing(Vector3 origin)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Guardiao_ShockwaveRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(origin.x, origin.y + 0.15f, origin.z);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.35f, 0.92f, 1.0f, 0.75f));
            ring.GetComponent<Renderer>().sharedMaterial = mat;

            float r = 1.2f;
            bool hit = false;
            while (r < Cfg.shockwaveMaxRadius && ring != null)
            {
                r += Cfg.shockwaveExpandSpeed * Time.deltaTime * LocalTimeScale;
                ring.transform.localScale = new Vector3(r * 2f, 0.08f, r * 2f);
                if (!hit && _player != null && PlayerHealthController.Instance != null)
                {
                    float pd = Vector3.Distance(new Vector3(_player.position.x, 0f, _player.position.z), new Vector3(origin.x, 0f, origin.z));
                    if (Mathf.Abs(pd - r) <= 1.15f && (_player.position.y - origin.y) < 1.2f)
                    {
                        hit = true;
                        PlayerHealthController.Instance.TryTakeDamage(Cfg.attackDamageSegments, origin);
                    }
                }
                yield return null;
            }
            if (ring != null) Destroy(ring);
            if (mat != null) Destroy(mat);
        }

        private void FirePhase2ProjectileSpray()
        {
            if (_player == null) return;
            Vector3 toPlayer = (_player.position - transform.position);
            toPlayer.y = 0f;
            Vector3 baseDir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
            int count = Mathf.Max(3, Cfg.phase2SprayProjectileCount);
            float half = Cfg.phase2SpraySpreadDeg * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float u = count > 1 ? (i / (float)(count - 1)) : 0.5f;
                float yaw = Mathf.Lerp(-half, half, u);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * baseDir;

                var pGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pGo.name = $"Guardiao_SprayProj_{i}";
                pGo.transform.position = transform.position + Vector3.up * 1.4f + dir * 1.2f;
                pGo.transform.localScale = Vector3.one * 0.42f;
                Destroy(pGo.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.90f, 0.25f, 1.0f, 1f));
                pGo.GetComponent<Renderer>().sharedMaterial = mat;

                var proj = pGo.AddComponent<ManifestationProjectile>();
                proj.direction = dir;
                proj.speed = Cfg.phase2SprayProjectileSpeed;
                proj.lifetimeSec = 4.5f;
                proj.damageSegments = Cfg.attackDamageSegments;
            }
        }

        private void BeginBossDeath()
        {
            State = BossFsmState.Dead;
            if (_bossHudGroup != null) _bossHudGroup.alpha = 0f;
            if (_bossMusicSource != null && _bossMusicSource.isPlaying) _bossMusicSource.Stop();

            for (int i = 0; i < _summonedAdds.Count; i++)
                if (_summonedAdds[i] != null) Destroy(_summonedAdds[i]);
            _summonedAdds.Clear();

            PlaySfx(dissolveClip, 0.95f, 0.72f);
            SpawnCoreFragmentDrop();
            OnBossDefeated?.Invoke();
            StartCoroutine(DissolveBossRoutine());
            Debug.Log("[RiftGuardian] BOSS DERROTADO! Fragmento de Nucleo dropado no centro da arena.");
        }

        private void SpawnCoreFragmentDrop()
        {
            var fragGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragGo.name = "Fragmento_De_Nucleo";
            fragGo.transform.position = new Vector3(arenaCenter.x, arenaCenter.y + 0.95f, arenaCenter.z);
            fragGo.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            fragGo.transform.localScale = Vector3.one * 0.55f;
            var col = fragGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.35f, 0.95f, 1.0f, 1f));
            if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0.45f, 0.96f, 1.0f) * 3.0f); }
            fragGo.GetComponent<Renderer>().sharedMaterial = mat;

            var frag = fragGo.AddComponent<CoreFragmentInteractable>();
            frag.config = Cfg;
            frag.hologramWorkshopWifeTexture = hologramWorkshopWifeTexture;
            frag.hologramFlashClip = hologramFlashClip;
            SpawnedCoreFragment = frag;
        }

        private IEnumerator DissolveBossRoutine()
        {
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            Vector3 s0 = transform.localScale;
            float t = 0f;
            while (t < 0.85f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / 0.85f);
                transform.localScale = Vector3.Lerp(s0, Vector3.zero, u);
                SetTint(Color.Lerp(new Color(0.35f, 0.95f, 1f, 1f), Color.black, u));
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _origColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    var m = _renderers[i].material;
                    _origColors[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                }
            }
        }

        private void SetTint(Color c)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var m = _renderers[i].material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else m.color = c;
            }
        }

        private void RestoreTint()
        {
            if (_renderers == null || _origColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var m = _renderers[i].material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", _origColors[i]);
                else m.color = _origColors[i];
            }
        }

        private void EnsureAudioSources()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0.25f;
            _bossMusicSource = gameObject.AddComponent<AudioSource>();
            _bossMusicSource.playOnAwake = false;
            _bossMusicSource.loop = true;
            _bossMusicSource.spatialBlend = 0f;
            _bossMusicSource.volume = 0.42f;

            if (AudioMixController.Instance != null)
            {
                if (AudioMixController.Instance.SfxGroup != null) _sfxSource.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;
                if (AudioMixController.Instance.MusicGroup != null) _bossMusicSource.outputAudioMixerGroup = AudioMixController.Instance.MusicGroup;
            }
        }

        private void PlaySfx(AudioClip clip, float vol, float pitch = 1f)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.pitch = pitch;
            float sc = _sfxSource.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
            _sfxSource.PlayOneShot(clip, vol * sc);
        }

        private void BuildBossHud()
        {
            var cGo = new GameObject("BossHUD_Canvas");
            cGo.transform.SetParent(transform, false);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 21;
            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _bossHudGroup = cGo.AddComponent<CanvasGroup>();
            _bossHudGroup.alpha = 0f;
            _bossHudGroup.blocksRaycasts = false;

            var barRoot = new GameObject("BossBarRoot");
            barRoot.transform.SetParent(cGo.transform, false);
            var brt = barRoot.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -92f);
            brt.sizeDelta = new Vector2(760f, 44f);
            barRoot.AddComponent<Image>().color = new Color(0.06f, 0.04f, 0.10f, 0.84f);

            var fillGo = new GameObject("HpFill");
            fillGo.transform.SetParent(barRoot.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0.38f);
            frt.offsetMin = new Vector2(6f, 4f); frt.offsetMax = new Vector2(-6f, 0f);
            _hpFill = fillGo.AddComponent<Image>();
            _hpFill.color = new Color(0.88f, 0.24f, 0.42f, 1f);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillAmount = 1f;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(barRoot.transform, false);
            var lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.38f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-10f, 0f);
            _phaseLabel = lblGo.AddComponent<Text>();
            _phaseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _phaseLabel.fontSize = 18;
            _phaseLabel.fontStyle = FontStyle.Bold;
            _phaseLabel.alignment = TextAnchor.MiddleLeft;
            _phaseLabel.color = Color.white;
            _phaseLabel.text = "GUARDIÃO DA FENDA — FASE I";
        }

        private void RefreshBossHud()
        {
            if (_hpFill != null) _hpFill.fillAmount = Mathf.Clamp01(CurrentHp / Mathf.Max(1f, MaxHp));
            if (_phaseLabel != null)
            {
                string phase = IsPhase2 ? "FASE II" : "FASE I";
                _phaseLabel.text = IsCoreExposed
                    ? $"GUARDIÃO DA FENDA ({phase}) — NÚCLEO EXPOSTO!"
                    : $"GUARDIÃO DA FENDA — {phase}";
                _phaseLabel.color = IsCoreExposed ? new Color(0.32f, 1.0f, 0.90f, 1f) : Color.white;
            }
        }
    }
}
