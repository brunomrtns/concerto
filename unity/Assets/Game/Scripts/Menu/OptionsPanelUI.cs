using System;
using Studio.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Painel compartilhado de Opcoes (usado tanto no Menu Principal quanto no Pause):
    ///   - Volume Master, Volume Musica, Volume SFX, Volume Ambiencia, Volume UI
    ///   - Tela Cheia (Toggle)
    ///   - Qualidade Grafica (anterior / proximo)
    /// Persiste automaticamente via GameSettingsManager (PlayerPrefs — exclusivo para opcoes).
    /// </summary>
    public sealed class OptionsPanelUI : MonoBehaviour
    {
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

        private bool _suppressCallbacks;

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
            RefreshFromSettings();
            gameObject.SetActive(true);
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiOpen();
        }

        public void Close()
        {
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiClose();
            gameObject.SetActive(false);
            OnClosed?.Invoke();
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
            _suppressCallbacks = false;
        }

        private void OnMasterChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetMasterVolume(v);
            UpdateValueLabels();
        }

        private void OnMusicChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetMusicVolume(v);
            UpdateValueLabels();
        }

        private void OnSfxChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetSfxVolume(v);
            UpdateValueLabels();
        }

        private void OnAmbienceChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetAmbienceVolume(v);
            UpdateValueLabels();
        }

        private void OnUiChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetUiVolume(v);
            UpdateValueLabels();
        }

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
    }
}
