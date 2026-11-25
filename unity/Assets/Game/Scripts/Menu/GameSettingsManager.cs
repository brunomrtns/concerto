using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Menu
{
    public enum ColorblindProfile
    {
        None = 0,
        Protanopia = 1,
        Deuteranopia = 2,
        Tritanopia = 3
    }

    /// <summary>
    /// Gerenciador persistente de Opcoes & Acessibilidade (PlayerPrefs — EXCLUSIVO para opcoes!):
    ///   - 5 buses de audio: Master, Musica, SFX, Ambiencia, UI
    ///   - Video & Camera: Tela Cheia, Qualidade, Brilho, Sensibilidade de Camera, Reduzir Shake
    ///   - Acessibilidade: Escala de Fonte (3 tamanhos), Legendas On/Off, Modo Daltoico (3 perfis)
    ///   - Remapeamento de Teclas: Pular, Interagir, Eco Temporal, Estase, Pausa
    /// Aplicado na hora sem restart. Estado de jogo NUNCA fica aqui (vive no SaveSystem JSON).
    /// </summary>
    public static class GameSettingsManager
    {
        public const string KeyMasterVol      = "CONCERTO_OPT_MASTER_VOL";
        public const string KeyMusicVol       = "CONCERTO_OPT_MUSIC_VOL";
        public const string KeySfxVol         = "CONCERTO_OPT_SFX_VOL";
        public const string KeyAmbienceVol    = "CONCERTO_OPT_AMBIENCE_VOL";
        public const string KeyUiVol          = "CONCERTO_OPT_UI_VOL";
        public const string KeyFullscreen     = "CONCERTO_OPT_FULLSCREEN";
        public const string KeyQuality        = "CONCERTO_OPT_QUALITY";
        public const string KeyCameraSens     = "CONCERTO_OPT_CAM_SENS";
        public const string KeyReduceShake    = "CONCERTO_OPT_REDUCE_SHAKE";
        public const string KeyBrightness     = "CONCERTO_OPT_BRIGHTNESS";
        public const string KeyFontScale      = "CONCERTO_OPT_FONT_SCALE";
        public const string KeySubtitles      = "CONCERTO_OPT_SUBTITLES";
        public const string KeyColorblind     = "CONCERTO_OPT_COLORBLIND";
        public const string KeyBindPrefix     = "CONCERTO_OPT_KEY_";

        public static readonly string[] RemappableActions = { "Jump", "Interact", "EcoTemporal", "Estase", "Pause" };
        public static readonly float[] FontScaleFactors = { 0.85f, 1.00f, 1.25f };
        public static readonly string[] FontScaleNames = { "Pequena", "Normal", "Grande" };

        private static readonly Dictionary<string, KeyCode> DefaultKeys = new Dictionary<string, KeyCode>
        {
            { "Jump",        KeyCode.Space },
            { "Interact",    KeyCode.E },
            { "EcoTemporal", KeyCode.C },
            { "Estase",      KeyCode.R },
            { "Pause",       KeyCode.Escape }
        };

        private static readonly Dictionary<string, KeyCode> _actionKeys = new Dictionary<string, KeyCode>(DefaultKeys);

        public static float MasterVolume      { get; private set; } = 0.85f;
        public static float MusicVolume       { get; private set; } = 0.70f;
        public static float SfxVolume         { get; private set; } = 0.85f;
        public static float AmbienceVolume    { get; private set; } = 0.70f;
        public static float UiVolume          { get; private set; } = 0.80f;
        public static bool  IsFullscreen      { get; private set; } = true;
        public static int   QualityIndex      { get; private set; } = 2;
        public static float CameraSensitivity { get; private set; } = 1.0f;
        public static bool  ReduceCameraShake { get; private set; } = false;
        public static float Brightness        { get; private set; } = 1.0f;
        public static int   FontScaleIndex    { get; private set; } = 1; // 0=Pequena, 1=Normal, 2=Grande
        public static bool  SubtitlesEnabled  { get; private set; } = true;
        public static ColorblindProfile ColorblindMode { get; private set; } = ColorblindProfile.None;

        public static float CameraShakeScale  => ReduceCameraShake ? 0.25f : 1.0f;
        public static float FontScaleFactor   => FontScaleFactors[Mathf.Clamp(FontScaleIndex, 0, FontScaleFactors.Length - 1)];
        public static string FontScaleLabel   => FontScaleNames[Mathf.Clamp(FontScaleIndex, 0, FontScaleNames.Length - 1)];

        public static event Action OnSettingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadAndApply()
        {
            MasterVolume   = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVol,   0.85f));
            MusicVolume    = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVol,    0.70f));
            SfxVolume      = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVol,      0.85f));
            AmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyAmbienceVol, 0.70f));
            UiVolume       = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyUiVol,       0.80f));
            IsFullscreen   = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            int defQ = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, maxQ);
            QualityIndex      = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, defQ), 0, maxQ);
            CameraSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyCameraSens, 1.0f), 0.3f, 2.5f);
            ReduceCameraShake = PlayerPrefs.GetInt(KeyReduceShake, 0) == 1;
            Brightness        = Mathf.Clamp(PlayerPrefs.GetFloat(KeyBrightness, 1.0f), 0.5f, 1.5f);
            FontScaleIndex    = Mathf.Clamp(PlayerPrefs.GetInt(KeyFontScale, 1), 0, 2);
            SubtitlesEnabled  = PlayerPrefs.GetInt(KeySubtitles, 1) == 1;
            ColorblindMode    = (ColorblindProfile)Mathf.Clamp(PlayerPrefs.GetInt(KeyColorblind, 0), 0, 3);

            foreach (var kv in DefaultKeys)
            {
                int savedCode = PlayerPrefs.GetInt(KeyBindPrefix + kv.Key, (int)kv.Value);
                _actionKeys[kv.Key] = Enum.IsDefined(typeof(KeyCode), savedCode) ? (KeyCode)savedCode : kv.Value;
            }

            ApplyRuntime();
        }

        public static void SetBrightness(float v)
        {
            Brightness = Mathf.Clamp(v, 0.5f, 1.5f);
            PlayerPrefs.SetFloat(KeyBrightness, Brightness);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetFontScaleIndex(int index)
        {
            FontScaleIndex = Mathf.Clamp(index, 0, FontScaleFactors.Length - 1);
            PlayerPrefs.SetInt(KeyFontScale, FontScaleIndex);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetSubtitlesEnabled(bool enabled)
        {
            SubtitlesEnabled = enabled;
            PlayerPrefs.SetInt(KeySubtitles, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetColorblindMode(ColorblindProfile profile)
        {
            ColorblindMode = profile;
            PlayerPrefs.SetInt(KeyColorblind, (int)profile);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static KeyCode GetActionKey(string action)
        {
            if (string.IsNullOrEmpty(action)) return KeyCode.None;
            if (_actionKeys.TryGetValue(action, out var k)) return k;
            return DefaultKeys.TryGetValue(action, out var def) ? def : KeyCode.None;
        }

        public static void SetActionKey(string action, KeyCode key)
        {
            if (string.IsNullOrEmpty(action) || key == KeyCode.None) return;
            _actionKeys[action] = key;
            PlayerPrefs.SetInt(KeyBindPrefix + action, (int)key);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void ResetActionKeys()
        {
            foreach (var kv in DefaultKeys)
            {
                _actionKeys[kv.Key] = kv.Value;
                PlayerPrefs.SetInt(KeyBindPrefix + kv.Key, (int)kv.Value);
            }
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetReduceCameraShake(bool reduce)
        {
            ReduceCameraShake = reduce;
            PlayerPrefs.SetInt(KeyReduceShake, reduce ? 1 : 0);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetCameraSensitivity(float v)
        {
            CameraSensitivity = Mathf.Clamp(v, 0.3f, 2.5f);
            PlayerPrefs.SetFloat(KeyCameraSens, CameraSensitivity);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetMasterVolume(float v)
        {
            MasterVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyMusicVol, MusicVolume);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeySfxVol, SfxVolume);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetAmbienceVolume(float v)
        {
            AmbienceVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyAmbienceVol, AmbienceVolume);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetUiVolume(float v)
        {
            UiVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyUiVol, UiVolume);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetFullscreen(bool fullscreen)
        {
            IsFullscreen = fullscreen;
            PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void SetQualityIndex(int index)
        {
            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            QualityIndex = Mathf.Clamp(index, 0, maxQ);
            PlayerPrefs.SetInt(KeyQuality, QualityIndex);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        private static void ApplyRuntime()
        {
            AudioListener.volume = MasterVolume;
            if (!Application.isBatchMode)
                Screen.fullScreen = IsFullscreen;
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(QualityIndex, true);
            OnSettingsChanged?.Invoke();
        }
    }
}
