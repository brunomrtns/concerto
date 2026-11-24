using System;
using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Powers;
using Studio.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Combat
{
    /// <summary>
    /// PlayerHealthController (#94) — HP do Dez em 4 segmentos, vignette vermelha no dano (<280ms),
    /// integracao com i-frames do Eco Temporal e Knockdown -> Respawn no checkpoint preservando estado.
    /// </summary>
    public sealed class PlayerHealthController : MonoBehaviour
    {
        public static PlayerHealthController Instance { get; private set; }

        [Header("Config & Audio")]
        public CombatBalanceConfig config;
        public AudioSource hurtAudioSource;
        public AudioClip hurtClip;

        [Header("Checkpoint de Respawn (Oficina / Area Atual)")]
        public Vector3 respawnPosition = new Vector3(-30.0f, 0.08f, -18.0f);
        public bool useStartPositionAsDefaultCheckpoint = true;

        private CombatBalanceConfig _runtimeCfg;
        public CombatBalanceConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<CombatBalanceConfig>();
                return _runtimeCfg;
            }
        }

        public int MaxSegments => Cfg.playerMaxHpSegments;
        public int CurrentSegments { get; private set; }
        public bool IsKnockedDown { get; private set; }
        public bool IsInvulnerable => _postHitInvulnTimer > 0f || (DezPowersController.Instance != null && DezPowersController.Instance.IsInvulnerable);
        public int DamageTakenCount { get; private set; }
        public int EcoEvadeCount { get; private set; }
        public int KnockdownCount { get; private set; }
        public int RespawnCount { get; private set; }

        public event Action<int, int> OnHealthChanged;
        public event Action OnPlayerKnockedDown;
        public event Action OnPlayerRespawned;

        private float _postHitInvulnTimer;
        private float _vignetteTimer;
        private DezLocomotionController _loco;
        private CharacterController _cc;

        // HUD de 4 segmentos + Vignette vermelha + Banner Knockdown
        private CanvasGroup _hudGroup;
        private Image[] _segmentImages;
        private Image _redVignette;
        private CanvasGroup _knockdownBanner;
        private int _lastHudSegments = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            PlayerRegistry.Register(gameObject);
            _loco = GetComponent<DezLocomotionController>();
            _cc = GetComponent<CharacterController>();

            if (useStartPositionAsDefaultCheckpoint)
                respawnPosition = transform.position;

            CurrentSegments = MaxSegments;
            EnsureAudioSource();
            BuildHealthHud();
            RefreshHealthHud();
        }

        private Cutscene03ReturnController _cs03;

        private void Start()
        {
            // FIX 2: integra checkpoints canonicos reais via SaveSystem (reuso obrigatorio, zero sistema paralelo)
            string sceneName = SceneManager.GetActiveScene().name;
            var saved = SaveSystem.CachedState ?? SaveSystem.Load();
            if (saved != null && saved.sceneName == sceneName)
            {
                respawnPosition = saved.checkpointPosition.ToVector3();
            }
            else if (!SaveSystem.IsPendingContinueLoad && sceneName == "Caldeiral")
            {
                SetCheckpointWithId("cp_oficina_inicio", transform.position);
            }
            else if (!SaveSystem.IsPendingContinueLoad && sceneName == "SetorExterno")
            {
                SetCheckpointWithId("cp_setor_externo_entrada", transform.position);
            }

            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            if (_cs03 != null) _cs03.OnCutscene03Ended += HandleCs03Checkpoint;
            SaveSystem.OnGameLoaded += HandleSaveLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PlayerRegistry.Unregister(gameObject);
            if (_cs03 != null) _cs03.OnCutscene03Ended -= HandleCs03Checkpoint;
            SaveSystem.OnGameLoaded -= HandleSaveLoaded;
        }

        private void HandleCs03Checkpoint()
        {
            SetCheckpointWithId("cp_oficina_pos_cs03", transform.position);
        }

        private void HandleSaveLoaded(SaveGameData data)
        {
            if (data != null)
                respawnPosition = data.checkpointPosition.ToVector3();
        }

        public void SetCheckpoint(Vector3 worldPos)
        {
            SetCheckpointWithId("cp_runtime_update", worldPos);
        }

        public void SetCheckpointWithId(string checkpointId, Vector3 worldPos)
        {
            respawnPosition = worldPos;
            SaveSystem.Save(checkpointId, worldPos, transform.eulerAngles.y);
        }

        public void SetCheckpointFromSaveOnly(Vector3 worldPos)
        {
            respawnPosition = worldPos;
        }

        public void RestoreFullHealth()
        {
            CurrentSegments = MaxSegments;
            RefreshHealthHud();
            OnHealthChanged?.Invoke(CurrentSegments, MaxSegments);
        }

        public bool TryTakeDamage(int segments, Vector3 hitFromWorldPos)
        {
            if (segments <= 0 || IsKnockedDown) return false;
            if (Time.timeScale <= 0.01f) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;

            // Respeita i-frames reais do Eco Temporal (#93)!
            if (DezPowersController.Instance != null && DezPowersController.Instance.IsInvulnerable)
            {
                EcoEvadeCount++;
                Debug.Log("[PlayerHealth] Ataque EVADIDO pelos i-frames do Eco Temporal!");
                return false;
            }

            if (_postHitInvulnTimer > 0f) return false;

            CurrentSegments = Mathf.Max(0, CurrentSegments - segments);
            DamageTakenCount++;
            _postHitInvulnTimer = Cfg.playerPostHitInvulnSec;
            _vignetteTimer = Cfg.damageVignetteDecaySec;

            PlayHurtSfx();
            RefreshHealthHud();
            OnHealthChanged?.Invoke(CurrentSegments, MaxSegments);

            // Micro knockback horizontal
            if (_cc != null && _cc.enabled)
            {
                Vector3 away = transform.position - hitFromWorldPos;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                    _cc.Move(away.normalized * 0.45f);
            }

            if (CurrentSegments <= 0)
                StartCoroutine(RunKnockdownAndRespawn());

            return true;
        }

        private IEnumerator RunKnockdownAndRespawn()
        {
            IsKnockedDown = true;
            KnockdownCount++;
            if (_loco != null) _loco.enabled = false;
            if (_knockdownBanner != null) _knockdownBanner.alpha = 1f;
            OnPlayerKnockedDown?.Invoke();
            Debug.Log("[PlayerHealth] Dez sofreu KNOCKDOWN (HP 0/4) — iniciando respawn no checkpoint preservando estado.");

            float t = 0f;
            float wait = Mathf.Max(0.4f, Cfg.knockdownRespawnDelaySec);
            while (t < wait)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Respawn seguro no checkpoint preservando anomalias/progresso!
            if (_cc != null) _cc.enabled = false;
            transform.position = respawnPosition;
            if (_cc != null) _cc.enabled = true;

            CurrentSegments = MaxSegments;
            _postHitInvulnTimer = 1.2f;
            IsKnockedDown = false;
            RespawnCount++;
            if (_knockdownBanner != null) _knockdownBanner.alpha = 0f;
            if (_loco != null) _loco.enabled = true;

            RefreshHealthHud();
            OnHealthChanged?.Invoke(CurrentSegments, MaxSegments);
            OnPlayerRespawned?.Invoke();
            Debug.Log($"[PlayerHealth] Respawn concluido em {respawnPosition} com HP restaurado ({CurrentSegments}/{MaxSegments}).");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_postHitInvulnTimer > 0f)
                _postHitInvulnTimer = Mathf.Max(0f, _postHitInvulnTimer - dt);

            if (_vignetteTimer > 0f && _redVignette != null)
            {
                _vignetteTimer = Mathf.Max(0f, _vignetteTimer - dt);
                float a = Mathf.Clamp01(_vignetteTimer / Mathf.Max(0.05f, Cfg.damageVignetteDecaySec)) * 0.42f;
                _redVignette.color = new Color(0.85f, 0.08f, 0.08f, a);
            }
        }

        private void EnsureAudioSource()
        {
            if (hurtAudioSource == null)
            {
                var child = new GameObject("Dez_HurtAudio");
                child.transform.SetParent(transform, false);
                hurtAudioSource = child.AddComponent<AudioSource>();
                hurtAudioSource.playOnAwake = false;
                hurtAudioSource.spatialBlend = 0f;
            }
            if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                hurtAudioSource.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;
        }

        private void PlayHurtSfx()
        {
            if (hurtClip == null || hurtAudioSource == null) return;
            hurtAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            float sc = hurtAudioSource.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
            hurtAudioSource.PlayOneShot(hurtClip, 0.85f * sc);
        }

        private void BuildHealthHud()
        {
            var canvasGo = new GameObject("PlayerHP_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Vignette vermelha de dano (tela inteira, nao bloqueia raycasts)
            var vigGo = new GameObject("DamageVignette");
            vigGo.transform.SetParent(canvasGo.transform, false);
            var vrt = vigGo.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            _redVignette = vigGo.AddComponent<Image>();
            _redVignette.color = new Color(0.85f, 0.08f, 0.08f, 0f);
            _redVignette.raycastTarget = false;

            // Barra de 4 segmentos no canto superior esquerdo
            var rootGo = new GameObject("HP_SegmentsRoot");
            rootGo.transform.SetParent(canvasGo.transform, false);
            var rrt = rootGo.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(32f, -32f);
            rrt.sizeDelta = new Vector2(260f, 44f);
            _hudGroup = rootGo.AddComponent<CanvasGroup>();

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.10f, 0.72f);

            int max = MaxSegments;
            _segmentImages = new Image[max];
            float segW = (240f - (max - 1) * 6f) / max;
            for (int i = 0; i < max; i++)
            {
                var sGo = new GameObject($"HP_Seg_{i}");
                sGo.transform.SetParent(rootGo.transform, false);
                var srt = sGo.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0f, 0.5f);
                srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(10f + i * (segW + 6f), 0f);
                srt.sizeDelta = new Vector2(segW, 22f);
                var img = sGo.AddComponent<Image>();
                img.color = new Color(0.22f, 0.90f, 0.78f, 1f);
                _segmentImages[i] = img;
            }

            // Banner de Knockdown
            var kdGo = new GameObject("KnockdownBanner");
            kdGo.transform.SetParent(canvasGo.transform, false);
            var krt = kdGo.AddComponent<RectTransform>();
            krt.anchorMin = new Vector2(0.5f, 0.5f);
            krt.anchorMax = new Vector2(0.5f, 0.5f);
            krt.sizeDelta = new Vector2(640f, 76f);
            var kbg = kdGo.AddComponent<Image>();
            kbg.color = new Color(0.12f, 0.03f, 0.04f, 0.86f);
            _knockdownBanner = kdGo.AddComponent<CanvasGroup>();
            _knockdownBanner.alpha = 0f;

            var ktxtGo = new GameObject("Text");
            ktxtGo.transform.SetParent(kdGo.transform, false);
            var trt = ktxtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var txt = ktxtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 0.78f, 0.78f, 1f);
            txt.text = "DEZ CAIU — Retornando ao checkpoint...";
        }

        private void RefreshHealthHud()
        {
            if (_segmentImages == null || CurrentSegments == _lastHudSegments) return;
            _lastHudSegments = CurrentSegments;
            for (int i = 0; i < _segmentImages.Length; i++)
            {
                if (_segmentImages[i] == null) continue;
                bool filled = i < CurrentSegments;
                _segmentImages[i].color = filled
                    ? (CurrentSegments == 1 ? new Color(0.95f, 0.28f, 0.22f, 1f) : new Color(0.22f, 0.90f, 0.78f, 1f))
                    : new Color(0.20f, 0.22f, 0.26f, 0.45f);
            }
        }
    }
}
