using System;
using System.Collections;
using System.Collections.Generic;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Combat;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Repair;
using Studio.Game.Save;
using Studio.Game.Tool;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Powers
{
    /// <summary>
    /// DezPowersController (#93) — Poderes Temporais do Dez (Eco Temporal + Estase):
    ///   - Desbloqueio: flag pos-Cutscene 3 (sem explicar a origem ao jogador) + hint curta na tela
    ///   - ECO TEMPORAL ([Alt] ou [C]): dodge curto com i-frames reais (~0.30s), afterimage ciano que some, cooldown 3s, som reverso ("re")
    ///   - ESTASE ([R]): pulsacao local que desacelera o MUNDO (50% por 3s) sem desacelerar o Dez; cooldown 10s; volta EXATO a 1.000f
    ///   - Bloqueado em cutscenes, dialogo, pause e reparo (regressao zero)
    ///   - HUD discreto consistente com ToolHUD_Canvas (dirty-checked, zero alocacao por frame)
    /// </summary>
    public sealed class DezPowersController : MonoBehaviour
    {
        public const string UnlockedPrefKey = "CONCERTO_POWERS_UNLOCKED";

        public static DezPowersController Instance { get; private set; }

        [Header("Config Data-Driven (ScriptableObject)")]
        public DezPowersConfig config;

        [Header("Audio SFX")]
        public AudioSource sfxSource;
        public AudioClip ecoReverseClip;
        public AudioClip stasisPulseClip;

        // Runtime config fallback
        private DezPowersConfig _runtimeCfg;
        public DezPowersConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<DezPowersConfig>();
                return _runtimeCfg;
            }
        }

        // Poderes registrados (lista extensivel para um 3o poder futuro)
        private readonly List<DezPowerBase> _powers = new List<DezPowerBase>(4);
        private EcoTemporalPower _ecoPower;
        private EstasePower _stasisPower;

        // Estado runtime
        private bool _isUnlocked;
        private float _ecoDashTimer;
        private Vector3 _ecoDashDir;
        private float _iFramesTimer;
        private float _afterimageNextTime;
        private int _afterimagesRemaining;
        private float _hintTimer;
        private Material _afterimageMat;
        private Material _pulseMat;

        // Referencias cacheadas (zero FindAnyObjectByType no Update!)
        private CharacterController _cc;
        private DezLocomotionController _loco;
        private SkinnedMeshRenderer[] _skinnedMeshes;
        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;
        private Cutscene04SkyCrackedController _cs04;

        // HUD discreto consistente com ToolHUD_Canvas
        private CanvasGroup _hudGroup;
        private Image _ecoFillImg;
        private Text _ecoCdText;
        private Image _stasisFillImg;
        private Text _stasisCdText;
        private CanvasGroup _hintGroup;
        private Text _hintText;
        private int _lastEcoDeci = -1;
        private int _lastStasisDeci = -1;

        // Telemetria / API publica (usada em combate #94/#103/#109 e testes PlayMode)
        public bool IsUnlocked => _isUnlocked;
        public bool IsInvulnerable => _iFramesTimer > 0f;
        public bool IsEcoDashing => _ecoDashTimer > 0f;
        public float EcoCooldownRemaining => _ecoPower != null ? _ecoPower.CooldownRemaining : 0f;
        public float StasisCooldownRemaining => _stasisPower != null ? _stasisPower.CooldownRemaining : 0f;
        public float DezTimeScale => 1.0f; // Regra canonica: Estase NUNCA desacelera o Dez!
        public int EcoActivationCount { get; private set; }
        public int StasisActivationCount { get; private set; }
        public int AfterimageSpawnCount { get; private set; }
        public int BlockedDamageCount { get; private set; }
        public int ReceivedDamageCount { get; private set; }

        public event Action OnEcoActivated;
        public event Action OnStasisActivated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _cc = GetComponent<CharacterController>();
            _loco = GetComponent<DezLocomotionController>();
            _skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(false);

            _ecoPower = new EcoTemporalPower(Cfg);
            _stasisPower = new EstasePower(Cfg);
            _powers.Add(_ecoPower);
            _powers.Add(_stasisPower);

            EnsureAudioSource();
            BuildDiscretePowersHud();
        }

        private void Start()
        {
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            _cs04 = FindAnyObjectByType<Cutscene04SkyCrackedController>();

            if (_cs03 != null)
                _cs03.OnCutscene03Ended += HandleCutscene03Ended;

            // FIX 3: Progressao vive EXCLUSIVAMENTE no SaveSystem JSON versionado (com migracao automatica de PlayerPrefs legados!)
            var saved = SaveSystem.CachedState ?? SaveSystem.Load();
            bool unlockedFromSave = saved != null && (saved.powersUnlocked || saved.cs03Played || (saved.unlockedPowerIds != null && saved.unlockedPowerIds.Count > 0));
            bool legacyPrefs = PlayerPrefs.GetInt(Cutscene03ReturnController.PlayedPrefKey, 0) == 1
                            || PlayerPrefs.GetInt(UnlockedPrefKey, 0) == 1;

            if (unlockedFromSave)
            {
                _isUnlocked = true;
            }
            else if (legacyPrefs)
            {
                // Migracao unica: importa do PlayerPrefs legado para o SaveSystem JSON v2 e remove do PlayerPrefs
                _isUnlocked = true;
                PlayerPrefs.DeleteKey(UnlockedPrefKey);
                PlayerPrefs.DeleteKey(Cutscene03ReturnController.PlayedPrefKey);
                PlayerPrefs.Save();
                if (!SaveSystem.IsPendingContinueLoad)
                    SaveSystem.Save();
            }
        }

        public void RestoreFromSave(bool unlocked)
        {
            _isUnlocked = unlocked;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_cs03 != null) _cs03.OnCutscene03Ended -= HandleCutscene03Ended;
            StasisFieldSystem.ClearAll();
            if (_afterimageMat != null) Destroy(_afterimageMat);
            if (_pulseMat != null) Destroy(_pulseMat);
        }

        private void HandleCutscene03Ended()
        {
            UnlockPowers(showHint: true);
        }

        public void UnlockPowers(bool showHint = true)
        {
            bool wasUnlocked = _isUnlocked;
            _isUnlocked = true;

            // FIX 3: persiste no SaveSystem JSON versionado (NUNCA em PlayerPrefs!)
            if (PlayerPrefs.HasKey(UnlockedPrefKey))
            {
                PlayerPrefs.DeleteKey(UnlockedPrefKey);
                PlayerPrefs.Save();
            }
            SaveSystem.Save();

            if (showHint && !wasUnlocked)
            {
                _hintTimer = Cfg.firstUnlockHintDurationSec;
                if (_hintText != null) _hintText.text = Cfg.firstUnlockHintText;
                if (_hintGroup != null) _hintGroup.alpha = 1f;
                Debug.Log($"[DezPowers] Poderes desbloqueados pos-CS03: {Cfg.firstUnlockHintText}");
            }
        }

        public bool CanUsePowersNow()
        {
            if (!_isUnlocked) return false;
            if (Time.timeScale <= 0.01f) return false;
            if (_cs01 != null && _cs01.IsPlaying) return false;
            if (_cs02 != null && _cs02.IsPlaying) return false;
            if (_cs03 != null && _cs03.IsPlaying) return false;
            if (_cs04 != null && _cs04.IsPlaying) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            if (RepairRitualController.Instance != null && (RepairRitualController.Instance.IsRepairing || RepairRitualController.Instance.IsPlayingFinalBeat)) return false;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return false;
            return true;
        }

        /// <summary>
        /// Prova real de i-frames: retorna false (dano bloqueado) durante Eco Temporal, true (dano recebido) fora.
        /// </summary>
        public bool TryReceiveTestDamage(float amount)
        {
            if (IsInvulnerable)
            {
                BlockedDamageCount++;
                Debug.Log($"[DezPowers] Dano ({amount:F1}) BLOQUEADO por i-frames do Eco Temporal!");
                return false;
            }
            ReceivedDamageCount++;
            return true;
        }

        public bool TriggerEcoTemporal()
        {
            if (!CanUsePowersNow()) return false;
            return _ecoPower != null && _ecoPower.TryActivate(this);
        }

        public bool TriggerEstase()
        {
            if (!CanUsePowersNow()) return false;
            return _stasisPower != null && _stasisPower.TryActivate(this);
        }

        internal bool ExecuteEcoInternal()
        {
            // Combate v2 (#109): cancela recovery de ataque se Eco for ativado na janela de cancelamento
            if (MultiToolController.Instance != null && MultiToolController.Instance.ActiveModule is CombatToolModule combatMod)
                combatMod.TryCancelRecoveryWithEco();

            var cfg = Cfg;
            Vector3 dir = Vector3.zero;
            if (_loco != null && _loco.UseScriptedInput && _loco.ScriptedMoveDir.sqrMagnitude > 0.01f)
            {
                dir = _loco.ScriptedMoveDir;
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
                        dir = fwd * v + right * h;
                    }
                    else
                    {
                        dir = new Vector3(h, 0f, v);
                    }
                }
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            _ecoDashDir = dir.normalized;
            transform.rotation = Quaternion.LookRotation(_ecoDashDir, Vector3.up);

            _ecoDashTimer = cfg.ecoDurationSec;
            _iFramesTimer = cfg.ecoIFramesSec;
            _afterimagesRemaining = cfg.ecoAfterimageCount;
            _afterimageNextTime = 0f;
            EcoActivationCount++;

            PlayPowerSfx(ecoReverseClip, 0.85f, 1.0f);
            SpawnCyanAfterimage();
            OnEcoActivated?.Invoke();
            return true;
        }

        internal bool ExecuteEstaseInternal()
        {
            var cfg = Cfg;
            StasisFieldSystem.SpawnField(transform.position, cfg.stasisRadius, cfg.stasisWorldTimeScale, cfg.stasisDurationSec);
            StasisActivationCount++;

            PlayPowerSfx(stasisPulseClip, 0.88f, 1.0f);
            StartCoroutine(SpawnStasisPulseRing(transform.position + Vector3.up * 0.9f, cfg.stasisRadius));
            OnStasisActivated?.Invoke();
            return true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale <= 0.01f) return;

            // Se entrar em cutscene no meio da Estase, limpa imediatamente para nao travar timeScale
            if ((_cs01 != null && _cs01.IsPlaying) || (_cs02 != null && _cs02.IsPlaying)
             || (_cs03 != null && _cs03.IsPlaying) || (_cs04 != null && _cs04.IsPlaying))
            {
                if (StasisFieldSystem.IsAnyStasisActive) StasisFieldSystem.ClearAll();
            }

            // Atualiza Estase em tempo real (nao depende do timeScale local!)
            StasisFieldSystem.Tick(dt);

            // Atualiza cooldowns dos poderes registrados
            for (int i = 0; i < _powers.Count; i++)
                _powers[i].TickCooldown(dt);

            // Atualiza i-frames do Eco Temporal
            if (_iFramesTimer > 0f)
                _iFramesTimer = Mathf.Max(0f, _iFramesTimer - dt);

            // Atualiza deslocamento curto + rastro de afterimages cianos do Eco Temporal
            if (_ecoDashTimer > 0f)
            {
                _ecoDashTimer = Mathf.Max(0f, _ecoDashTimer - dt);
                if (_cc != null && _cc.enabled)
                    _cc.Move(_ecoDashDir * (Cfg.ecoDashSpeed * dt) + Vector3.down * (2.0f * dt));

                if (_afterimagesRemaining > 0)
                {
                    _afterimageNextTime -= dt;
                    if (_afterimageNextTime <= 0f)
                    {
                        SpawnCyanAfterimage();
                        _afterimagesRemaining--;
                        _afterimageNextTime = Cfg.ecoDurationSec / Mathf.Max(1, Cfg.ecoAfterimageCount);
                    }
                }
            }

            // Atualiza hint curta pos-CS03
            if (_hintTimer > 0f)
            {
                _hintTimer = Mathf.Max(0f, _hintTimer - dt);
                if (_hintGroup != null)
                    _hintGroup.alpha = _hintTimer < 0.8f ? Mathf.Clamp01(_hintTimer / 0.8f) : 1f;
            }

            // Leitura de input do jogador
            bool canUse = CanUsePowersNow();
            if (canUse)
            {
                if (Input.GetKeyDown(Cfg.ecoPrimaryKey) || Input.GetKeyDown(Cfg.ecoAltKey))
                    TriggerEcoTemporal();
                if (Input.GetKeyDown(Cfg.stasisKey))
                    TriggerEstase();
            }

            RefreshHud(canUse);
        }

        private void EnsureAudioSource()
        {
            if (sfxSource == null)
            {
                var child = new GameObject("Dez_PowersAudio");
                child.transform.SetParent(transform, false);
                sfxSource = child.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
            if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                sfxSource.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;
        }

        private void PlayPowerSfx(AudioClip clip, float vol, float pitch)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = pitch;
            float scale = sfxSource.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
            sfxSource.PlayOneShot(clip, vol * scale);
        }

        private void SpawnCyanAfterimage()
        {
            AfterimageSpawnCount++;
            if (_afterimageMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _afterimageMat = new Material(sh);
                if (_afterimageMat.HasProperty("_Surface")) _afterimageMat.SetFloat("_Surface", 1f);
                _afterimageMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _afterimageMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _afterimageMat.SetInt("_ZWrite", 0);
                _afterimageMat.renderQueue = 3000;
                SetMatColor(_afterimageMat, Cfg.ecoAfterimageColor);
            }

            // Usa BakeMesh leve do SkinnedMeshRenderer real do Dez (silhueta fiel, economico!)
            if (_skinnedMeshes != null && _skinnedMeshes.Length > 0 && _skinnedMeshes[0] != null)
            {
                var smr = _skinnedMeshes[0];
                var ghost = new GameObject("Dez_EcoAfterimage");
                ghost.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                ghost.transform.localScale = smr.transform.lossyScale;

                var baked = new Mesh();
                smr.BakeMesh(baked);
                var mf = ghost.AddComponent<MeshFilter>();
                mf.sharedMesh = baked;
                var mr = ghost.AddComponent<MeshRenderer>();
                var instMat = new Material(_afterimageMat);
                mr.sharedMaterial = instMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                StartCoroutine(FadeAndDestroyGhost(ghost, instMat, baked, Cfg.ecoAfterimageLifetimeSec, Cfg.ecoAfterimageColor));
            }
        }

        private IEnumerator FadeAndDestroyGhost(GameObject go, Material mat, Mesh mesh, float lifetime, Color baseCol)
        {
            float t = 0f;
            while (t < lifetime && go != null)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(1f - (t / Mathf.Max(0.05f, lifetime))) * baseCol.a;
                SetMatColor(mat, new Color(baseCol.r, baseCol.g, baseCol.b, a));
                yield return null;
            }
            if (go != null) Destroy(go);
            if (mat != null) Destroy(mat);
            if (mesh != null) Destroy(mesh);
        }

        private IEnumerator SpawnStasisPulseRing(Vector3 center, float maxRadius)
        {
            if (_pulseMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _pulseMat = new Material(sh);
                if (_pulseMat.HasProperty("_Surface")) _pulseMat.SetFloat("_Surface", 1f);
                _pulseMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _pulseMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _pulseMat.SetInt("_ZWrite", 0);
                _pulseMat.renderQueue = 3010;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ring.name = "Estase_PulseSphere_VFX";
            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ring.transform.position = center;
            var mat = new Material(_pulseMat);
            var mr = ring.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Color c = Cfg.stasisPulseColor;
            float dur = 0.45f;
            float t = 0f;
            while (t < dur && ring != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float scale = Mathf.Lerp(0.8f, maxRadius * 0.65f, 1f - (1f - u) * (1f - u));
                ring.transform.localScale = Vector3.one * scale;
                SetMatColor(mat, new Color(c.r, c.g, c.b, c.a * (1f - u)));
                yield return null;
            }
            if (ring != null) Destroy(ring);
            if (mat != null) Destroy(mat);
        }

        private static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private void BuildDiscretePowersHud()
        {
            var canvasGo = new GameObject("PowersHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _hudGroup = canvasGo.AddComponent<CanvasGroup>();
            _hudGroup.alpha = 0f;
            _hudGroup.blocksRaycasts = false;
            _hudGroup.interactable = false;

            // BL-003 (QA-02): Posiciona slots de poderes ACIMA do painel ToolHUD_Canvas (y=196, zero overlap!)
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreatePowerSlot(canvasGo.transform, font, "ECO [ALT/C]", new Vector2(-240f, 196f), new Color(0.18f, 0.85f, 0.96f, 0.92f), out _ecoFillImg, out _ecoCdText);
            CreatePowerSlot(canvasGo.transform, font, "ESTASE [R]",  new Vector2(-40f,  196f), new Color(0.32f, 0.72f, 1.00f, 0.92f), out _stasisFillImg, out _stasisCdText);

            // Hint discreta pos-CS03 no topo-centro inferior
            var hintGo = new GameObject("PowersUnlockHint");
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hRect = hintGo.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 0f);
            hRect.anchorMax = new Vector2(0.5f, 0f);
            hRect.pivot = new Vector2(0.5f, 0f);
            hRect.anchoredPosition = new Vector2(0f, 140f);
            hRect.sizeDelta = new Vector2(920f, 48f);
            var bg = hintGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.78f);
            _hintGroup = hintGo.AddComponent<CanvasGroup>();
            _hintGroup.alpha = 0f;

            var txtGo = new GameObject("HintText");
            txtGo.transform.SetParent(hintGo.transform, false);
            var tRect = txtGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            _hintText = txtGo.AddComponent<Text>();
            _hintText.font = font;
            _hintText.fontSize = 20;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = new Color(0.75f, 0.95f, 1f, 1f);
        }

        private static void CreatePowerSlot(
            Transform parent,
            Font font,
            string label,
            Vector2 anchoredPos,
            Color accent,
            out Image fillImg,
            out Text cdText)
        {
            var root = new GameObject("Slot_" + label);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(180f, 58f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.76f);

            var barGo = new GameObject("Fill");
            barGo.transform.SetParent(root.transform, false);
            var brt = barGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.18f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            fillImg = barGo.AddComponent<Image>();
            fillImg.color = accent;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(root.transform, false);
            var lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.45f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = new Vector2(-8f, -2f);
            var lbl = lblGo.AddComponent<Text>();
            lbl.font = font;
            lbl.fontSize = 15;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.color = Color.white;
            lbl.text = label;

            var cdGo = new GameObject("Status");
            cdGo.transform.SetParent(root.transform, false);
            var crt = cdGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0.18f);
            crt.anchorMax = new Vector2(1f, 0.52f);
            crt.offsetMin = new Vector2(8f, 0f);
            crt.offsetMax = new Vector2(-8f, 0f);
            cdText = cdGo.AddComponent<Text>();
            cdText.font = font;
            cdText.fontSize = 14;
            cdText.alignment = TextAnchor.MiddleLeft;
            cdText.color = accent;
            cdText.text = "PRONTO";
        }

        private float _lastEcoFill = -1f;
        private float _lastStasisFill = -1f;

        private void RefreshHud(bool canUse)
        {
            if (_hudGroup != null)
            {
                float targetAlpha = canUse ? 1f : 0f;
                if (Mathf.Abs(_hudGroup.alpha - targetAlpha) > 0.001f)
                    _hudGroup.alpha = targetAlpha;
            }
            if (!canUse) return;

            if (_ecoFillImg != null && _ecoPower != null)
            {
                float f = _ecoPower.CooldownFill01;
                if (Mathf.Abs(f - _lastEcoFill) > 0.002f) { _lastEcoFill = f; _ecoFillImg.fillAmount = f; }
            }
            if (_stasisFillImg != null && _stasisPower != null)
            {
                float f = _stasisPower.CooldownFill01;
                if (Mathf.Abs(f - _lastStasisFill) > 0.002f) { _lastStasisFill = f; _stasisFillImg.fillAmount = f; }
            }

            int ecoDeci = _ecoPower != null ? Mathf.CeilToInt(_ecoPower.CooldownRemaining * 10f) : 0;
            if (ecoDeci != _lastEcoDeci && _ecoCdText != null)
            {
                _lastEcoDeci = ecoDeci;
                _ecoCdText.text = ecoDeci <= 0 ? "PRONTO" : $"{ecoDeci * 0.1f:F1}s";
            }

            int stDeci = _stasisPower != null ? Mathf.CeilToInt(_stasisPower.CooldownRemaining * 10f) : 0;
            if (stDeci != _lastStasisDeci && _stasisCdText != null)
            {
                _lastStasisDeci = stDeci;
                _stasisCdText.text = StasisFieldSystem.IsAnyStasisActive
                    ? $"ATIVO ({StasisFieldSystem.ActiveRemainingSec:F1}s)"
                    : (stDeci <= 0 ? "PRONTO" : $"{stDeci * 0.1f:F1}s");
            }
        }

        private sealed class EcoTemporalPower : DezPowerBase
        {
            private readonly DezPowersConfig _cfg;
            public EcoTemporalPower(DezPowersConfig cfg) => _cfg = cfg;
            public override string PowerId => "eco_temporal";
            public override string DisplayName => "Eco Temporal";
            public override KeyCode PrimaryKey => _cfg.ecoPrimaryKey;
            public override float CooldownDuration => _cfg.ecoCooldownSec;
            protected override bool OnActivate(DezPowersController owner) => owner.ExecuteEcoInternal();
        }

        private sealed class EstasePower : DezPowerBase
        {
            private readonly DezPowersConfig _cfg;
            public EstasePower(DezPowersConfig cfg) => _cfg = cfg;
            public override string PowerId => "estase";
            public override string DisplayName => "Estase";
            public override KeyCode PrimaryKey => _cfg.stasisKey;
            public override float CooldownDuration => _cfg.stasisCooldownSec;
            protected override bool OnActivate(DezPowersController owner) => owner.ExecuteEstaseInternal();
        }
    }
}
