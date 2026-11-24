using System;
using UnityEngine;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Gerenciador minimo e persistente de Opcoes (PlayerPrefs):
    /// Volume Master, Musica, SFX, Tela Cheia e Qualidade Grafica.
    /// Aplicado automaticamente antes de qualquer cena carregar.
    /// </summary>
    public static class GameSettingsManager
    {
        public const string KeyMasterVol  = "CONCERTO_OPT_MASTER_VOL";
        public const string KeyMusicVol   = "CONCERTO_OPT_MUSIC_VOL";
        public const string KeySfxVol     = "CONCERTO_OPT_SFX_VOL";
        public const string KeyFullscreen = "CONCERTO_OPT_FULLSCREEN";
        public const string KeyQuality     = "CONCERTO_OPT_QUALITY";
        public const string KeyCameraSens  = "CONCERTO_OPT_CAM_SENS";
        public const string KeyReduceShake = "CONCERTO_OPT_REDUCE_SHAKE";

        public static float MasterVolume      { get; private set; } = 0.85f;
        public static float MusicVolume       { get; private set; } = 0.75f;
        public static float SfxVolume         { get; private set; } = 0.85f;
        public static bool  IsFullscreen      { get; private set; } = true;
        public static int   QualityIndex      { get; private set; } = 2;
        public static float CameraSensitivity { get; private set; } = 1.0f;
        public static bool  ReduceCameraShake { get; private set; } = false;
        public static float CameraShakeScale  => ReduceCameraShake ? 0.25f : 1.0f;

        public static event Action OnSettingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadAndApply()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVol, 0.85f));
            MusicVolume  = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVol,  0.75f));
            SfxVolume    = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVol,    0.85f));
            IsFullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            int defQ = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, maxQ);
            QualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, defQ), 0, maxQ);
            CameraSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyCameraSens, 1.0f), 0.3f, 2.5f);
            ReduceCameraShake = PlayerPrefs.GetInt(KeyReduceShake, 0) == 1;

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
