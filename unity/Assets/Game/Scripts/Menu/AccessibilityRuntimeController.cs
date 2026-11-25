using System.Collections.Generic;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Powers;
using Studio.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.UI
{
    /// <summary>
    /// AccessibilityRuntimeController — Runtime global de Acessibilidade, HUD Dinamico e Legendas (GATES 2, 3 e 4):
    ///   - Aplica na hora (sem restart): Brilho, Modo Daltoico (3 perfis: Protanopia, Deuteranopia, Tritanopia),
    ///     Escala de Fonte (3 tamanhos: Pequena 0.85x, Normal 1.00x, Grande 1.25x) e Legendas On/Off.
    ///   - Legendas globais para TODA fala e som relevante de gameplay com nome do falante/fonte sonora.
    ///   - Zero alocacoes por frame; zero modelos 3D criados.
    /// </summary>
    public sealed class AccessibilityRuntimeController : MonoBehaviour
    {
        public static AccessibilityRuntimeController Instance { get; private set; }

        private Canvas _overlayCanvas;
        private Image _brightnessOverlay;
        private Image _colorblindOverlay;
        private Text _colorblindBadgeText;
        private GameObject _colorblindBadgeRoot;

        private CanvasGroup _subtitleGroup;
        private Text _subtitleText;
        private float _subtitleTimer;

        private readonly Dictionary<int, int> _baseFontSizes = new Dictionary<int, int>();
        private DezPowersController _boundPowers;

        public string ActiveSubtitleText => _subtitleText != null && _subtitleGroup != null && _subtitleGroup.alpha > 0.05f
            ? _subtitleText.text
            : string.Empty;
        public bool IsSubtitleVisible => _subtitleGroup != null && _subtitleGroup.alpha > 0.05f;
        public Color CurrentColorblindTint => _colorblindOverlay != null ? _colorblindOverlay.color : Color.clear;
        public float CurrentBrightnessOverlayAlpha => _brightnessOverlay != null ? _brightnessOverlay.color.a : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("Concerto_AccessibilityRuntime");
            go.AddComponent<AccessibilityRuntimeController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildOverlayCanvas();
            GameSettingsManager.OnSettingsChanged += ApplyAllAccessibilitySettings;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SaveSystem.OnGameSaved += HandleGameSavedSubtitle;
            SaveSystem.OnBackupRecovered += HandleBackupRecoveredSubtitle;

            BindSceneEvents();
            ApplyAllAccessibilitySettings();
        }

        private void OnDestroy()
        {
            GameSettingsManager.OnSettingsChanged -= ApplyAllAccessibilitySettings;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SaveSystem.OnGameSaved -= HandleGameSavedSubtitle;
            SaveSystem.OnBackupRecovered -= HandleBackupRecoveredSubtitle;
            UnbindPowers();
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _baseFontSizes.Clear();
            BindSceneEvents();
            ApplyAllAccessibilitySettings();
        }

        private void BindSceneEvents()
        {
            UnbindPowers();
            _boundPowers = FindAnyObjectByType<DezPowersController>();
            if (_boundPowers != null)
            {
                _boundPowers.OnEcoActivated += HandleEcoSoundSubtitle;
                _boundPowers.OnStasisActivated += HandleStasisSoundSubtitle;
            }
        }

        private void UnbindPowers()
        {
            if (_boundPowers == null) return;
            _boundPowers.OnEcoActivated -= HandleEcoSoundSubtitle;
            _boundPowers.OnStasisActivated -= HandleStasisSoundSubtitle;
            _boundPowers = null;
        }

        private void HandleEcoSoundSubtitle() => ShowSubtitle("ECO TEMPORAL", "Pulso temporal reverso ativado", 2.2f);
        private void HandleStasisSoundSubtitle() => ShowSubtitle("ESTASE", "Campo temporal desacelerado (50%)", 2.5f);
        private void HandleGameSavedSubtitle(SaveGameData _) => ShowSubtitle("SISTEMA", "Checkpoint salvo", 1.8f);
        private void HandleBackupRecoveredSubtitle(string slot, string _) => ShowSubtitle("SISTEMA", $"Backup automatico ({slot}) restaurado", 3.5f);

        /// <summary>
        /// GATE 4: Exibe legenda com nome do falante ou fonte sonora relevante (respeita SubtitlesEnabled e FontScale).
        /// </summary>
        public void ShowSubtitle(string speakerOrSource, string text, float durationSec = 3.0f)
        {
            if (_subtitleGroup == null || _subtitleText == null) return;
            if (!GameSettingsManager.SubtitlesEnabled || string.IsNullOrWhiteSpace(text))
            {
                _subtitleGroup.alpha = 0f;
                _subtitleTimer = 0f;
                return;
            }

            string tag = string.IsNullOrWhiteSpace(speakerOrSource) ? "AUDIO" : speakerOrSource.Trim().ToUpperInvariant();
            _subtitleText.text = $"[{tag}] {text.Trim()}";
            _subtitleText.fontSize = Mathf.RoundToInt(24f * GameSettingsManager.FontScaleFactor);
            _subtitleGroup.alpha = 1f;
            _subtitleTimer = Mathf.Max(0.8f, durationSec);
        }

        public void ApplyAllAccessibilitySettings()
        {
            ApplyBrightnessOverlay();
            ApplyColorblindProfile();
            ApplyFontScaleToHud();

            if (!GameSettingsManager.SubtitlesEnabled && _subtitleGroup != null)
            {
                _subtitleGroup.alpha = 0f;
                _subtitleTimer = 0f;
            }
        }

        private void Update()
        {
            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.unscaledDeltaTime;
                if (_subtitleTimer <= 0f && _subtitleGroup != null)
                    _subtitleGroup.alpha = 0f;
            }
        }

        private void ApplyBrightnessOverlay()
        {
            if (_brightnessOverlay == null) return;
            float b = GameSettingsManager.Brightness; // 0.5 .. 1.5
            if (b < 0.99f)
            {
                float alpha = Mathf.Clamp01((1.0f - b) * 0.95f);
                _brightnessOverlay.color = new Color(0f, 0f, 0f, alpha);
            }
            else if (b > 1.01f)
            {
                float alpha = Mathf.Clamp01((b - 1.0f) * 0.36f);
                _brightnessOverlay.color = new Color(1f, 1f, 0.96f, alpha);
            }
            else
            {
                _brightnessOverlay.color = Color.clear;
            }
        }

        private void ApplyColorblindProfile()
        {
            if (_colorblindOverlay == null) return;
            switch (GameSettingsManager.ColorblindMode)
            {
                case ColorblindProfile.Protanopia:
                    _colorblindOverlay.color = new Color(0.08f, 0.52f, 0.95f, 0.14f);
                    SetColorblindBadge("DALTONISMO: PROTANOPIA");
                    break;
                case ColorblindProfile.Deuteranopia:
                    _colorblindOverlay.color = new Color(0.88f, 0.56f, 0.12f, 0.14f);
                    SetColorblindBadge("DALTONISMO: DEUTERANOPIA");
                    break;
                case ColorblindProfile.Tritanopia:
                    _colorblindOverlay.color = new Color(0.86f, 0.22f, 0.58f, 0.14f);
                    SetColorblindBadge("DALTONISMO: TRITANOPIA");
                    break;
                default:
                    _colorblindOverlay.color = Color.clear;
                    SetColorblindBadge(string.Empty);
                    break;
            }
        }

        private void SetColorblindBadge(string label)
        {
            if (_colorblindBadgeRoot == null || _colorblindBadgeText == null) return;
            bool show = !string.IsNullOrEmpty(label);
            _colorblindBadgeRoot.SetActive(show);
            if (show)
            {
                _colorblindBadgeText.text = label;
                _colorblindBadgeText.fontSize = Mathf.RoundToInt(14f * GameSettingsManager.FontScaleFactor);
            }
        }

        /// <summary>
        /// GATE 3: Escala textos de HUD/Legendas/Dialogos proporcionalmente a opcao de fonte (3 tamanhos),
        /// preservando legibilidade e sem sobreposicao.
        /// </summary>
        private void ApplyFontScaleToHud()
        {
            float scale = GameSettingsManager.FontScaleFactor;
            if (_subtitleText != null)
                _subtitleText.fontSize = Mathf.RoundToInt(24f * scale);

            var allTexts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                var t = allTexts[i];
                if (t == null || t == _subtitleText || t == _colorblindBadgeText) continue;

                // Escala HUDs de vida, objetivo, poderes, ferramenta, dialogos e legendas
                string rootName = t.transform.root != null ? t.transform.root.name : string.Empty;
                bool isHudOrDialogue = rootName.Contains("HUD") || rootName.Contains("Hud")
                    || rootName.Contains("Dialogue") || rootName.Contains("Cutscene")
                    || rootName.Contains("Onboarding") || rootName.Contains("Objective")
                    || rootName.Contains("Powers") || rootName.Contains("Health");
                if (!isHudOrDialogue) continue;

                int id = t.GetInstanceID();
                if (!_baseFontSizes.TryGetValue(id, out int baseSize))
                {
                    baseSize = Mathf.Max(10, t.fontSize);
                    _baseFontSizes[id] = baseSize;
                }
                t.fontSize = Mathf.Clamp(Mathf.RoundToInt(baseSize * scale), 10, 44);
            }
        }

        private void BuildOverlayCanvas()
        {
            var go = new GameObject("AccessibilityOverlay_Canvas");
            go.transform.SetParent(transform, false);
            _overlayCanvas = go.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 180;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Camada de brilho (non-raycast)
            var brightGo = new GameObject("BrightnessOverlay");
            brightGo.transform.SetParent(go.transform, false);
            _brightnessOverlay = brightGo.AddComponent<Image>();
            _brightnessOverlay.raycastTarget = false;
            _brightnessOverlay.color = Color.clear;
            StretchFull(brightGo.GetComponent<RectTransform>());

            // Camada de perfil daltoico (non-raycast)
            var cbGo = new GameObject("ColorblindOverlay");
            cbGo.transform.SetParent(go.transform, false);
            _colorblindOverlay = cbGo.AddComponent<Image>();
            _colorblindOverlay.raycastTarget = false;
            _colorblindOverlay.color = Color.clear;
            StretchFull(cbGo.GetComponent<RectTransform>());

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Badge discreto no topo-direito quando perfil daltoico ativo
            _colorblindBadgeRoot = new GameObject("ColorblindBadge");
            _colorblindBadgeRoot.transform.SetParent(go.transform, false);
            var badgeBg = _colorblindBadgeRoot.AddComponent<Image>();
            badgeBg.raycastTarget = false;
            badgeBg.color = new Color(0.05f, 0.08f, 0.14f, 0.78f);
            var br = _colorblindBadgeRoot.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(1f, 1f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(1f, 1f);
            br.anchoredPosition = new Vector2(-24f, -20f);
            br.sizeDelta = new Vector2(280f, 34f);

            var badgeTxtGo = new GameObject("Label");
            badgeTxtGo.transform.SetParent(_colorblindBadgeRoot.transform, false);
            _colorblindBadgeText = badgeTxtGo.AddComponent<Text>();
            _colorblindBadgeText.font = font;
            _colorblindBadgeText.fontSize = 14;
            _colorblindBadgeText.fontStyle = FontStyle.Bold;
            _colorblindBadgeText.alignment = TextAnchor.MiddleCenter;
            _colorblindBadgeText.color = new Color(0.95f, 0.98f, 1f, 1f);
            _colorblindBadgeText.raycastTarget = false;
            StretchFull(badgeTxtGo.GetComponent<RectTransform>());
            _colorblindBadgeRoot.SetActive(false);

            // Barra de legendas globais (com nome do falante / fonte sonora) — GATE 4
            var subRoot = new GameObject("GlobalSubtitleBar");
            subRoot.transform.SetParent(go.transform, false);
            var subBg = subRoot.AddComponent<Image>();
            subBg.raycastTarget = false;
            subBg.color = new Color(0.04f, 0.06f, 0.10f, 0.86f);
            _subtitleGroup = subRoot.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            _subtitleGroup.blocksRaycasts = false;
            _subtitleGroup.interactable = false;
            var sr = subRoot.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.5f, 0f);
            sr.anchorMax = new Vector2(0.5f, 0f);
            sr.pivot = new Vector2(0.5f, 0f);
            sr.anchoredPosition = new Vector2(0f, 128f);
            sr.sizeDelta = new Vector2(920f, 58f);

            var subTxtGo = new GameObject("SubtitleText");
            subTxtGo.transform.SetParent(subRoot.transform, false);
            _subtitleText = subTxtGo.AddComponent<Text>();
            _subtitleText.font = font;
            _subtitleText.fontSize = 24;
            _subtitleText.fontStyle = FontStyle.Bold;
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.color = Color.white;
            _subtitleText.raycastTarget = false;
            StretchFull(subTxtGo.GetComponent<RectTransform>());
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
