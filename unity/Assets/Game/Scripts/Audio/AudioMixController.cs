using System;
using System.Collections.Generic;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Studio.Game.Audio
{
    /// <summary>
    /// AudioMixController — Controlador global de Mix, Ducking suave, Ambiencia por Area (crossfade) e UI SFX.
    /// Estende GameSettingsManager (PlayerPrefs) sem criar sistema paralelo:
    ///   - AudioMixer (ConcertoMasterMixer): Master / Music / SFX / Ambience / UI
    ///   - Ducking automatico suave (-12 dB, fade 0.35s entrada / 0.65s saida, ZERO corte seco) em cutscenes e dialogos
    ///   - Crossfade continuo de ambiencia entre Caldeiral (oficina quente) e SetorExterno (vento frio)
    ///   - UI SFX (abrir/fechar/confirmar/negar) com ignoreListenerPause = true para funcionar no Pause
    /// </summary>
    public sealed class AudioMixController : MonoBehaviour
    {
        public const string ParamMasterVol   = "MasterVol";
        public const string ParamMusicVol    = "MusicVol";
        public const string ParamSfxVol      = "SfxVol";
        public const string ParamAmbienceVol = "AmbienceVol";
        public const string ParamUiVol       = "UiVol";

        public static AudioMixController Instance { get; private set; }

        [Header("Banco & Mixer (carregado automaticamente de Resources/Audio/ConcertoAudioBank)")]
        public ConcertoAudioBank bank;

        [Header("Ducking de Cutscene / Dialogo (DESIGN_BIBLE Secao 4 & 8.3)")]
        [Range(0.10f, 0.60f)] public float duckTargetFactor = 0.25f; // -12 dB
        public float duckFadeInSec = 0.35f;
        public float duckFadeOutSec = 0.65f;

        [Header("Mix Alvo (Base Gains)")]
        [Range(0f, 1f)] public float gameplayMusicBaseVolume = 0.38f;
        [Range(0f, 1f)] public float areaAmbienceBaseVolume  = 0.32f;
        [Range(0f, 1f)] public float uiSfxBaseVolume         = 0.72f;
        public float ambienceCrossfadeSec = 1.5f;

        // Fontes internas gerenciadas (roteadas para seus respectivos AudioMixerGroups)
        private AudioSource _musicSource;
        private AudioSource _ambA;
        private AudioSource _ambB;
        private AudioSource _uiSource;
        private bool _usingAmbA = true;
        private float _ambFadeProgress = 1f;
        private float _ambFadeDuration = 1.5f;
        private float _ambFadeStartA;
        private float _ambFadeStartB;

        // Ducking state
        private float _currentDuckFactor = 1f;
        private float _lastAppliedMusicDb = float.NaN;
        private readonly HashSet<string> _manualDuckKeys = new HashSet<string>();

        // Cached scene controllers (zero FindAnyObjectByType in Update — respeita Pass de Performance!)
        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;
        private Cutscene04SkyCrackedController _cs04;
        private DialoguePresenter _dialoguePresenter;

        public float CurrentMusicDuckFactor => _currentDuckFactor;
        public bool IsDuckingActive => ShouldDuckNow();
        public float CurrentMusicEffectiveGain => GameSettingsManager.MusicVolume * _currentDuckFactor;
        public bool IsAmbienceCrossfading => _ambFadeProgress < 0.999f;
        public string ActiveAmbienceClipName
        {
            get
            {
                var active = _usingAmbA ? _ambA : _ambB;
                return (active != null && active.clip != null) ? active.clip.name : string.Empty;
            }
        }
        public int UiSfxTriggerCount { get; private set; }
        public int AmbienceCrossfadeCount { get; private set; }

        public AudioMixer MasterMixer => bank != null ? bank.masterMixer : null;
        public AudioMixerGroup MusicGroup => bank != null ? bank.musicGroup : null;
        public AudioMixerGroup SfxGroup => bank != null ? bank.sfxGroup : null;
        public AudioMixerGroup AmbienceGroup => bank != null ? bank.ambienceGroup : null;
        public AudioMixerGroup UiGroup => bank != null ? bank.uiGroup : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapped()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<AudioMixController>();
            if (existing != null) return;

            var go = new GameObject("Concerto_AudioMixController");
            go.AddComponent<AudioMixController>();
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

            EnsureBankLoaded();
            EnsureInternalSources();

            GameSettingsManager.LoadAndApply();
            GameSettingsManager.OnSettingsChanged += ApplyAllBusVolumes;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            ApplyAllBusVolumes();
            RefreshSceneContext(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameSettingsManager.OnSettingsChanged -= ApplyAllBusVolumes;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void EnsureBankLoaded()
        {
            if (bank == null)
                bank = Resources.Load<ConcertoAudioBank>("Audio/ConcertoAudioBank");
        }

        private void EnsureInternalSources()
        {
            if (_musicSource == null)
            {
                _musicSource = CreateChildSource("Bus_Music_BGM", MusicGroup, loop: true, ignorePause: false);
                _musicSource.volume = gameplayMusicBaseVolume;
            }
            if (_ambA == null)
            {
                _ambA = CreateChildSource("Bus_Ambience_A", AmbienceGroup, loop: true, ignorePause: false);
                _ambA.volume = 0f;
            }
            if (_ambB == null)
            {
                _ambB = CreateChildSource("Bus_Ambience_B", AmbienceGroup, loop: true, ignorePause: false);
                _ambB.volume = 0f;
            }
            if (_uiSource == null)
            {
                _uiSource = CreateChildSource("Bus_UI_SFX", UiGroup, loop: false, ignorePause: true);
                _uiSource.volume = 1f;
            }
        }

        private AudioSource CreateChildSource(string childName, AudioMixerGroup group, bool loop, bool ignorePause)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = loop;
            src.ignoreListenerPause = ignorePause;
            if (group != null) src.outputAudioMixerGroup = group;
            return src;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshSceneContext(scene.name);
        }

        public void RefreshSceneContext(string sceneName)
        {
            EnsureBankLoaded();
            EnsureInternalSources();

            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            _cs04 = FindAnyObjectByType<Cutscene04SkyCrackedController>();
            _dialoguePresenter = FindAnyObjectByType<DialoguePresenter>();

            RouteSceneAudioSources();

            if (bank == null) return;

            bool isMainMenu = sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSector = sceneName.IndexOf("SetorExterno", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMainMenu)
            {
                if (_musicSource != null && _musicSource.isPlaying) _musicSource.Stop();
                CrossfadeAmbience(null, 0.8f);
            }
            else if (isSector)
            {
                SetGameplayMusic(bank.sectorMusicClip);
                CrossfadeAmbience(bank.sectorAmbienceClip, ambienceCrossfadeSec);
            }
            else
            {
                SetGameplayMusic(bank.caldeiralMusicClip);
                CrossfadeAmbience(bank.workshopAmbienceClip, ambienceCrossfadeSec);
            }
        }

        /// <summary>
        /// Roteia AudioSources existentes da cena para o AudioMixer global (estende o que ja existe sem duplicar).
        /// </summary>
        public void RouteSceneAudioSources()
        {
            if (bank == null) return;
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                var src = sources[i];
                if (src == null || src.outputAudioMixerGroup != null) continue;

                string n = src.gameObject.name;
                if (ContainsAny(n, "Music", "BGM", "Motif"))
                    src.outputAudioMixerGroup = bank.musicGroup;
                else if (ContainsAny(n, "Ambience", "Wind", "Hum"))
                    src.outputAudioMixerGroup = bank.ambienceGroup;
                else if (ContainsAny(n, "UI", "Menu", "Canvas"))
                    src.outputAudioMixerGroup = bank.uiGroup;
                else
                    src.outputAudioMixerGroup = bank.sfxGroup;
            }
        }

        private static bool ContainsAny(string text, string a, string b, string c)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetGameplayMusic(AudioClip clip)
        {
            if (_musicSource == null) return;
            if (clip == null)
            {
                if (_musicSource.isPlaying) _musicSource.Stop();
                return;
            }
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.Play();
            ApplyMusicDuckGain();
        }

        public void CrossfadeAmbience(AudioClip nextClip, float durationSec = 1.5f)
        {
            EnsureInternalSources();
            var current = _usingAmbA ? _ambA : _ambB;
            if (current != null && current.clip == nextClip && (nextClip == null || current.isPlaying))
                return;

            _usingAmbA = !_usingAmbA;
            var target = _usingAmbA ? _ambA : _ambB;
            target.clip = nextClip;
            target.volume = 0f;
            if (nextClip != null) target.Play();
            else target.Stop();

            _ambFadeStartA = _ambA.volume;
            _ambFadeStartB = _ambB.volume;
            _ambFadeDuration = Mathf.Max(0.15f, durationSec);
            _ambFadeProgress = 0f;
            AmbienceCrossfadeCount++;
        }

        public void PushDucking(string key)
        {
            if (!string.IsNullOrEmpty(key)) _manualDuckKeys.Add(key);
        }

        public void PopDucking(string key)
        {
            if (!string.IsNullOrEmpty(key)) _manualDuckKeys.Remove(key);
        }

        public bool ShouldDuckNow()
        {
            if (_manualDuckKeys.Count > 0) return true;
            if (_cs01 != null && _cs01.IsPlaying) return true;
            if (_cs02 != null && _cs02.IsPlaying) return true;
            if (_cs03 != null && _cs03.IsPlaying) return true;
            if (_cs04 != null && _cs04.IsPlaying) return true;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return true;
            if (_dialoguePresenter != null && _dialoguePresenter.IsSpeaking) return true;
            return false;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.1f);

            // 1. Ducking suave da musica (sem corte seco!)
            bool wantDuck = ShouldDuckNow();
            float target = wantDuck ? duckTargetFactor : 1f;
            if (Mathf.Abs(_currentDuckFactor - target) > 0.0005f)
            {
                float speed = wantDuck
                    ? (1f - duckTargetFactor) / Mathf.Max(0.08f, duckFadeInSec)
                    : (1f - duckTargetFactor) / Mathf.Max(0.08f, duckFadeOutSec);
                _currentDuckFactor = Mathf.MoveTowards(_currentDuckFactor, target, speed * dt);
                ApplyMusicDuckGain();
            }

            // 2. Crossfade continuo de ambiencia entre zonas
            if (_ambFadeProgress < 1f)
            {
                _ambFadeProgress = Mathf.Min(1f, _ambFadeProgress + dt / _ambFadeDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, _ambFadeProgress);
                float targetVol = areaAmbienceBaseVolume * (MasterMixer != null ? 1f : GameSettingsManager.AmbienceVolume);

                float destA = (_usingAmbA && _ambA.clip != null) ? targetVol : 0f;
                float destB = (!_usingAmbA && _ambB.clip != null) ? targetVol : 0f;

                _ambA.volume = Mathf.Lerp(_ambFadeStartA, destA, smooth);
                _ambB.volume = Mathf.Lerp(_ambFadeStartB, destB, smooth);

                if (_ambFadeProgress >= 1f)
                {
                    if (destA <= 0.001f && _ambA.isPlaying) _ambA.Stop();
                    if (destB <= 0.001f && _ambB.isPlaying) _ambB.Stop();
                }
            }
        }

        public void ApplyAllBusVolumes()
        {
            var mixer = MasterMixer;
            if (mixer != null)
            {
                mixer.SetFloat(ParamMasterVol,   LinearToDecibels(GameSettingsManager.MasterVolume));
                mixer.SetFloat(ParamSfxVol,      LinearToDecibels(GameSettingsManager.SfxVolume));
                mixer.SetFloat(ParamAmbienceVol, LinearToDecibels(GameSettingsManager.AmbienceVolume));
                mixer.SetFloat(ParamUiVol,       LinearToDecibels(GameSettingsManager.UiVolume));
            }
            _lastAppliedMusicDb = float.NaN;
            ApplyMusicDuckGain();

            if (_ambFadeProgress >= 1f && _ambA != null && _ambB != null)
            {
                float targetVol = areaAmbienceBaseVolume * (mixer != null ? 1f : GameSettingsManager.AmbienceVolume);
                if (_usingAmbA && _ambA.clip != null) _ambA.volume = targetVol;
                if (!_usingAmbA && _ambB.clip != null) _ambB.volume = targetVol;
            }
        }

        private void ApplyMusicDuckGain()
        {
            float effective = Mathf.Clamp01(GameSettingsManager.MusicVolume * _currentDuckFactor);
            float db = LinearToDecibels(effective);
            var mixer = MasterMixer;
            if (mixer != null)
            {
                if (float.IsNaN(_lastAppliedMusicDb) || Mathf.Abs(_lastAppliedMusicDb - db) > 0.05f)
                {
                    mixer.SetFloat(ParamMusicVol, db);
                    _lastAppliedMusicDb = db;
                }
                if (_musicSource != null)
                    _musicSource.volume = gameplayMusicBaseVolume;
            }
            else if (_musicSource != null)
            {
                _musicSource.volume = gameplayMusicBaseVolume * effective;
            }
        }

        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Clamp(Mathf.Log10(linear) * 20f, -80f, 0f);
        }

        // === UI SFX Centralizados (ignoram AudioListener.pause) ===

        public void PlayUiOpen()    => PlayUiClip(bank != null ? bank.uiOpenClip : null, 1.00f);
        public void PlayUiClose()   => PlayUiClip(bank != null ? bank.uiCloseClip : null, 0.96f);
        public void PlayUiConfirm() => PlayUiClip(bank != null ? bank.uiConfirmClip : null, 1.00f);
        public void PlayUiDeny()    => PlayUiClip(bank != null ? bank.uiDenyClip : null, 0.95f);

        private void PlayUiClip(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            EnsureInternalSources();
            if (_uiSource == null) return;
            _uiSource.pitch = pitch;
            float vol = uiSfxBaseVolume * (MasterMixer != null ? 1f : GameSettingsManager.UiVolume);
            _uiSource.PlayOneShot(clip, vol);
            UiSfxTriggerCount++;
        }
    }
}
