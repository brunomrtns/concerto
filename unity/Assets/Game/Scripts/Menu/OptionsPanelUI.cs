using System;
using Studio.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Painel compartilhado de Opcoes & Acessibilidade (Menu Principal + Pause — GATE 2):
    ///   - 5 buses de audio: Master, Musica, SFX, Ambiencia, UI
    ///   - Brilho, Sensibilidade de Camera, Tela Cheia, Qualidade
    ///   - Escala de Fonte (3 tamanhos), Legendas On/Off, Modo Daltoico (3 perfis), Remapear Teclas
    ///   - Navegacao 100% por controle e teclado (setas/D-pad + confirmar + voltar), aplica na hora sem restart.
    /// </summary>
    public sealed class OptionsPanelUI : MonoBehaviour
    {
        public const int TotalOptionRows = 14;

        [Header("Audio")]
        public Slider masterSlider;
        public Text masterValueText;
        public Slider musicSlider;
        public Text musicValueText;
        public Slider sfxSlider;
        public Text sfxValueText;
        public Slider ambienceSlider;
        public Text ambienceValueText;
        public Slider uiSlider;
        public Text uiValueText;

        [Header("Video")]
        public Toggle fullscreenToggle;
        public Button qualityPrevButton;
        public Button qualityNextButton;
        public Text qualityValueText;

        [Header("Acao")]
        public Button backButton;

        public event Action OnClosed;

        public int FocusedRowIndex => _focusedRow;
        public bool IsAwaitingKeyCapture => _awaitingKeyAction != null;
        public string FocusedOptionSummary => FormatRowLabel(_focusedRow);

        private bool _suppressCallbacks;
        private int _focusedRow;
        private float _navCooldownUntil;
        private string _awaitingKeyAction;

        // Painel runtime de Acessibilidade & Controles (renderizado junto com Opcoes)
        private GameObject _a11yCard;
        private readonly Text[] _a11yRowTexts = new Text[TotalOptionRows];
        private readonly Image[] _a11yRowBgs = new Image[TotalOptionRows];

        private static readonly KeyCode[] CycleRemapKeys =
        {
            KeyCode.Space, KeyCode.E, KeyCode.C, KeyCode.R, KeyCode.F, KeyCode.Q, KeyCode.LeftShift, KeyCode.Tab
        };

        private void Awake()
        {
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicSlider != null)  musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxSlider != null)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (ambienceSlider != null) ambienceSlider.onValueChanged.AddListener(OnAmbienceChanged);
            if (uiSlider != null)       uiSlider.onValueChanged.AddListener(OnUiChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (qualityPrevButton != null) qualityPrevButton.onClick.AddListener(() => StepQuality(-1));
            if (qualityNextButton != null) qualityNextButton.onClick.AddListener(() => StepQuality(+1));
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            EnsureAccessibilityOverlayPanel();
            _focusedRow = 0;
            _awaitingKeyAction = null;
            RefreshFromSettings();
            gameObject.SetActive(true);
            RefreshAccessibilityRowsVisual();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiOpen();
        }

        public void Close()
        {
            _awaitingKeyAction = null;
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiClose();
            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_awaitingKeyAction != null)
            {
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    if (k == KeyCode.None || k == KeyCode.Escape || k >= KeyCode.Mouse0) continue;
                    if (Input.GetKeyDown(k))
                    {
                        GameSettingsManager.SetActionKey(_awaitingKeyAction, k);
                        _awaitingKeyAction = null;
                        RefreshAccessibilityRowsVisual();
                        if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
                        return;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
                {
                    _awaitingKeyAction = null;
                    RefreshAccessibilityRowsVisual();
                }
                return;
            }

            if (Time.unscaledTime >= _navCooldownUntil)
            {
                float v = Input.GetAxisRaw("Vertical");
                float h = Input.GetAxisRaw("Horizontal");

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || v < -0.55f)
                {
                    NavigateOption(+1);
                    _navCooldownUntil = Time.unscaledTime + 0.16f;
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || v > 0.55f)
                {
                    NavigateOption(-1);
                    _navCooldownUntil = Time.unscaledTime + 0.16f;
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || h > 0.55f)
                {
                    AdjustOption(+1);
                    _navCooldownUntil = Time.unscaledTime + 0.14f;
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || h < -0.55f)
                {
                    AdjustOption(-1);
                    _navCooldownUntil = Time.unscaledTime + 0.14f;
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                ConfirmOption();
            }
            else if (Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                Close();
            }
        }

        /// <summary>
        /// GATE 1/2: Navegacao por controle/teclado entre todas as opcoes (audio, brilho, camera, fonte, legendas, daltonismo, remapeamento).
        /// </summary>
        public void NavigateOption(int delta)
        {
            _focusedRow = (_focusedRow + delta + TotalOptionRows) % TotalOptionRows;
            RefreshAccessibilityRowsVisual();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        public void AdjustOption(int dir)
        {
            float step = dir >= 0 ? 0.10f : -0.10f;
            switch (_focusedRow)
            {
                case 0: GameSettingsManager.SetMasterVolume(GameSettingsManager.MasterVolume + step); break;
                case 1: GameSettingsManager.SetMusicVolume(GameSettingsManager.MusicVolume + step); break;
                case 2: GameSettingsManager.SetSfxVolume(GameSettingsManager.SfxVolume + step); break;
                case 3: GameSettingsManager.SetAmbienceVolume(GameSettingsManager.AmbienceVolume + step); break;
                case 4: GameSettingsManager.SetUiVolume(GameSettingsManager.UiVolume + step); break;
                case 5: GameSettingsManager.SetBrightness(GameSettingsManager.Brightness + step); break;
                case 6: GameSettingsManager.SetCameraSensitivity(GameSettingsManager.CameraSensitivity + (dir >= 0 ? 0.2f : -0.2f)); break;
                case 7: GameSettingsManager.SetFontScaleIndex((GameSettingsManager.FontScaleIndex + dir + 3) % 3); break;
                case 8: GameSettingsManager.SetSubtitlesEnabled(!GameSettingsManager.SubtitlesEnabled); break;
                case 9:
                {
                    int next = ((int)GameSettingsManager.ColorblindMode + dir + 4) % 4;
                    GameSettingsManager.SetColorblindMode((ColorblindProfile)next);
                    break;
                }
                case 10: CycleActionKey("Jump", dir); break;
                case 11: CycleActionKey("Interact", dir); break;
                case 12: CycleActionKey("EcoTemporal", dir); break;
                case 13: CycleActionKey("Estase", dir); break;
            }
            RefreshFromSettings();
            RefreshAccessibilityRowsVisual();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        public void ConfirmOption()
        {
            if (_focusedRow == 8)
            {
                GameSettingsManager.SetSubtitlesEnabled(!GameSettingsManager.SubtitlesEnabled);
            }
            else if (_focusedRow == 7 || _focusedRow == 9)
            {
                AdjustOption(+1);
                return;
            }
            else if (_focusedRow >= 10 && _focusedRow <= 13)
            {
                string action = _focusedRow == 10 ? "Jump"
                    : (_focusedRow == 11 ? "Interact" : (_focusedRow == 12 ? "EcoTemporal" : "Estase"));
                CycleActionKey(action, +1);
            }
            else
            {
                AdjustOption(+1);
                return;
            }
            RefreshFromSettings();
            RefreshAccessibilityRowsVisual();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        private static void CycleActionKey(string action, int dir)
        {
            KeyCode current = GameSettingsManager.GetActionKey(action);
            int idx = Array.IndexOf(CycleRemapKeys, current);
            int next = (idx < 0 ? 0 : (idx + dir + CycleRemapKeys.Length) % CycleRemapKeys.Length);
            GameSettingsManager.SetActionKey(action, CycleRemapKeys[next]);
        }

        public void RefreshFromSettings()
        {
            _suppressCallbacks = true;
            if (masterSlider != null)   masterSlider.value   = GameSettingsManager.MasterVolume;
            if (musicSlider != null)    musicSlider.value    = GameSettingsManager.MusicVolume;
            if (sfxSlider != null)      sfxSlider.value      = GameSettingsManager.SfxVolume;
            if (ambienceSlider != null) ambienceSlider.value = GameSettingsManager.AmbienceVolume;
            if (uiSlider != null)       uiSlider.value       = GameSettingsManager.UiVolume;
            if (fullscreenToggle != null) fullscreenToggle.isOn = GameSettingsManager.IsFullscreen;
            UpdateValueLabels();
            RefreshAccessibilityRowsVisual();
            _suppressCallbacks = false;
        }

        private void OnMasterChanged(float v)   { if (!_suppressCallbacks) { GameSettingsManager.SetMasterVolume(v);   UpdateValueLabels(); RefreshAccessibilityRowsVisual(); } }
        private void OnMusicChanged(float v)    { if (!_suppressCallbacks) { GameSettingsManager.SetMusicVolume(v);    UpdateValueLabels(); RefreshAccessibilityRowsVisual(); } }
        private void OnSfxChanged(float v)      { if (!_suppressCallbacks) { GameSettingsManager.SetSfxVolume(v);      UpdateValueLabels(); RefreshAccessibilityRowsVisual(); } }
        private void OnAmbienceChanged(float v) { if (!_suppressCallbacks) { GameSettingsManager.SetAmbienceVolume(v); UpdateValueLabels(); RefreshAccessibilityRowsVisual(); } }
        private void OnUiChanged(float v)       { if (!_suppressCallbacks) { GameSettingsManager.SetUiVolume(v);       UpdateValueLabels(); RefreshAccessibilityRowsVisual(); } }

        private void OnFullscreenChanged(bool val)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetFullscreen(val);
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        private void StepQuality(int delta)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            int next = (GameSettingsManager.QualityIndex + delta + names.Length) % names.Length;
            GameSettingsManager.SetQualityIndex(next);
            UpdateValueLabels();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        private void UpdateValueLabels()
        {
            if (masterValueText != null)   masterValueText.text   = $"{Mathf.RoundToInt(GameSettingsManager.MasterVolume * 100f)}%";
            if (musicValueText != null)    musicValueText.text    = $"{Mathf.RoundToInt(GameSettingsManager.MusicVolume * 100f)}%";
            if (sfxValueText != null)      sfxValueText.text      = $"{Mathf.RoundToInt(GameSettingsManager.SfxVolume * 100f)}%";
            if (ambienceValueText != null) ambienceValueText.text = $"{Mathf.RoundToInt(GameSettingsManager.AmbienceVolume * 100f)}%";
            if (uiValueText != null)       uiValueText.text       = $"{Mathf.RoundToInt(GameSettingsManager.UiVolume * 100f)}%";
            if (qualityValueText != null)
            {
                string[] names = QualitySettings.names;
                int idx = GameSettingsManager.QualityIndex;
                qualityValueText.text = (names != null && idx >= 0 && idx < names.Length) ? names[idx] : $"Nivel {idx}";
            }
        }

        private void EnsureAccessibilityOverlayPanel()
        {
            if (_a11yCard != null) return;

            _a11yCard = new GameObject("AccessibilityAndControls_Panel");
            _a11yCard.transform.SetParent(transform, false);
            var bg = _a11yCard.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.13f, 0.94f);
            var rt = _a11yCard.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(760f, 620f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleGo = new GameObject("Header");
            titleGo.transform.SetParent(_a11yCard.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = font;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.28f, 0.88f, 1f, 1f);
            title.text = "OPCOES, ACESSIBILIDADE & CONTROLES  [D-Pad: Navegar | A: Alterar | B: Voltar]";
            var tr = titleGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -12f);
            tr.sizeDelta = new Vector2(-24f, 36f);

            for (int i = 0; i < TotalOptionRows; i++)
            {
                var row = new GameObject($"OptionRow_{i:D2}");
                row.transform.SetParent(_a11yCard.transform, false);
                var rowImg = row.AddComponent<Image>();
                rowImg.color = new Color(0.10f, 0.14f, 0.20f, 0.85f);
                var rr = row.GetComponent<RectTransform>();
                rr.anchorMin = new Vector2(0.5f, 1f);
                rr.anchorMax = new Vector2(0.5f, 1f);
                rr.pivot = new Vector2(0.5f, 1f);
                rr.anchoredPosition = new Vector2(0f, -58f - i * 38f);
                rr.sizeDelta = new Vector2(710f, 34f);
                _a11yRowBgs[i] = rowImg;

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(row.transform, false);
                var txt = txtGo.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = 18;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = Color.white;
                var txr = txtGo.GetComponent<RectTransform>();
                txr.anchorMin = Vector2.zero;
                txr.anchorMax = Vector2.one;
                txr.offsetMin = new Vector2(16f, 0f);
                txr.offsetMax = new Vector2(-16f, 0f);
                _a11yRowTexts[i] = txt;
            }
        }

        private void RefreshAccessibilityRowsVisual()
        {
            if (_a11yCard == null) return;
            for (int i = 0; i < TotalOptionRows; i++)
            {
                bool selected = (i == _focusedRow);
                if (_a11yRowBgs[i] != null)
                    _a11yRowBgs[i].color = selected
                        ? new Color(0.15f, 0.56f, 0.78f, 0.96f)
                        : new Color(0.10f, 0.14f, 0.20f, 0.82f);
                if (_a11yRowTexts[i] != null)
                {
                    _a11yRowTexts[i].text = (selected ? "> " : "  ") + FormatRowLabel(i);
                    _a11yRowTexts[i].fontSize = Mathf.Clamp(Mathf.RoundToInt(18f * GameSettingsManager.FontScaleFactor), 14, 24);
                }
            }
        }

        private static string FormatRowLabel(int row)
        {
            switch (row)
            {
                case 0:  return $"Audio — Master:           {Mathf.RoundToInt(GameSettingsManager.MasterVolume * 100f)}%";
                case 1:  return $"Audio — Musica:           {Mathf.RoundToInt(GameSettingsManager.MusicVolume * 100f)}%";
                case 2:  return $"Audio — SFX:              {Mathf.RoundToInt(GameSettingsManager.SfxVolume * 100f)}%";
                case 3:  return $"Audio — Ambiencia:        {Mathf.RoundToInt(GameSettingsManager.AmbienceVolume * 100f)}%";
                case 4:  return $"Audio — UI:               {Mathf.RoundToInt(GameSettingsManager.UiVolume * 100f)}%";
                case 5:  return $"Video — Brilho:           {Mathf.RoundToInt(GameSettingsManager.Brightness * 100f)}%";
                case 6:  return $"Camera — Sensibilidade:   {GameSettingsManager.CameraSensitivity:F1}x";
                case 7:  return $"Acessibilidade — Fonte:   {GameSettingsManager.FontScaleLabel} ({GameSettingsManager.FontScaleFactor:F2}x)";
                case 8:  return $"Acessibilidade — Legendas: {(GameSettingsManager.SubtitlesEnabled ? "LIGADAS" : "DESLIGADAS")}";
                case 9:  return $"Daltonismo — Perfil:      {GameSettingsManager.ColorblindMode}";
                case 10: return $"Remapear — Pular:         [{GameSettingsManager.GetActionKey("Jump")}]";
                case 11: return $"Remapear — Interagir:     [{GameSettingsManager.GetActionKey("Interact")}]";
                case 12: return $"Remapear — Eco Temporal:  [{GameSettingsManager.GetActionKey("EcoTemporal")}]";
                case 13: return $"Remapear — Estase:        [{GameSettingsManager.GetActionKey("Estase")}]";
                default: return string.Empty;
            }
        }
    }
}
