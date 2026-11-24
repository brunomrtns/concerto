using System;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Painel compartilhado de Opcoes (usado tanto no Menu Principal quanto no Pause):
    ///   - Volume Master, Volume Musica, Volume SFX
    ///   - Tela Cheia (Toggle)
    ///   - Qualidade Grafica (anterior / proximo)
    /// Persiste automaticamente via GameSettingsManager (PlayerPrefs).
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
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (qualityPrevButton != null) qualityPrevButton.onClick.AddListener(() => StepQuality(-1));
            if (qualityNextButton != null) qualityNextButton.onClick.AddListener(() => StepQuality(+1));
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            RefreshFromSettings();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        public void RefreshFromSettings()
        {
            _suppressCallbacks = true;
            if (masterSlider != null) masterSlider.value = GameSettingsManager.MasterVolume;
            if (musicSlider != null)  musicSlider.value  = GameSettingsManager.MusicVolume;
            if (sfxSlider != null)    sfxSlider.value    = GameSettingsManager.SfxVolume;
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

        private void OnFullscreenChanged(bool val)
        {
            if (_suppressCallbacks) return;
            GameSettingsManager.SetFullscreen(val);
        }

        private void StepQuality(int delta)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            int next = (GameSettingsManager.QualityIndex + delta + names.Length) % names.Length;
            GameSettingsManager.SetQualityIndex(next);
            UpdateValueLabels();
        }

        private void UpdateValueLabels()
        {
            if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(GameSettingsManager.MasterVolume * 100f)}%";
            if (musicValueText != null)  musicValueText.text  = $"{Mathf.RoundToInt(GameSettingsManager.MusicVolume * 100f)}%";
            if (sfxValueText != null)    sfxValueText.text    = $"{Mathf.RoundToInt(GameSettingsManager.SfxVolume * 100f)}%";
            if (qualityValueText != null)
            {
                string[] names = QualitySettings.names;
                int idx = GameSettingsManager.QualityIndex;
                qualityValueText.text = (names != null && idx >= 0 && idx < names.Length) ? names[idx] : $"Nivel {idx}";
            }
        }
    }
}
