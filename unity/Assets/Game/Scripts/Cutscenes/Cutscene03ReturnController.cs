using System;
using Studio.Game.Animation;
using Studio.Game.Anomalies;
using Studio.Game.Dialogue;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Cutscene03ReturnController — Cutscene 3 ("O Retorno", 32s, 4 Beats), reusando 100% o padrao CS01/CS02:
    ///   - Beat 1 (0:00–0:08): Flash de luz, silencio. Oficina do presente APARENTEMENTE normal.
    ///   - Beat 2 (0:08–0:18): Dez guarda a foto com um sorriso. Respira. Momento de paz curto.
    ///   - Beat 3 (0:18–0:28): Nota a bugiganga quebrada de novo na bancada; lampada pisca; zumbido grave da maquina continua; corte pro preto sem musica (so zumbido).
    ///   - Beat 4 (0:28–0:32): Devolve controle na oficina + ativa as 5 anomalias ambientais + objetivo "Investigue a oficina".
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class Cutscene03ReturnController : MonoBehaviour
    {
        public const string PlayedPrefKey = "CONCERTO_CS03_PLAYED";

        [Header("Config & Dados")]
        public string cutsceneId = "cs03_o_retorno";
        public TextAsset subtitleJson;
        public KeyCode replayDebugKey = KeyCode.F7;
        public float skipHoldDuration = 0.85f;
        public bool autoChainAfterCutscene02NaturalEnd = false;

        [Header("Referencias de Cena (Reusadas + Rig CS03)")]
        public CutsceneUIOverlay uiOverlay;
        public GameObject camerasRoot;
        public Volume returnFlashVolume;
        public GameObject brokenTrinketProp;
        public GameObject ceilingLightsRoot;
        public WorkshopAnomalyManager anomalyManager;

        [Header("Audio TEMP")]
        public AudioSource sfxSource;
        public AudioSource humSource;
        public AudioClip flashReturnClip;
        public AudioClip machineHumClip;

        private PlayableDirector _director;
        private CutsceneSubtitleConfig _config;
        private CinemachineBrain _cmBrain;
        private DezLocomotionController _playerLoco;
        private CharacterController _playerCc;
        private Animator _playerAnim;
        private Cutscene02TravelController _cs02;

        private bool _isPlaying;
        private float _elapsed;
        private float _skipHoldTimer;
        private bool _flickerActive;
        private readonly CutsceneActorGuard _actorGuard = new CutsceneActorGuard();

        public bool IsPlaying => _isPlaying;
        public event Action OnCutscene03Ended;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            _config = CutsceneSubtitleConfig.FromJson(subtitleJson);
            RestoreRigImmediate();
        }

        private void Start()
        {
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            if (_cs02 != null)
                _cs02.OnCutscene02CompletedNaturally += HandleCutscene02Completed;
        }

        private void OnDestroy()
        {
            if (_cs02 != null)
                _cs02.OnCutscene02CompletedNaturally -= HandleCutscene02Completed;
        }

        private void HandleCutscene02Completed()
        {
            if (autoChainAfterCutscene02NaturalEnd && !_isPlaying)
                PlayCutscene();
        }

        private void CacheActors()
        {
            var cam = Camera.main;
            if (cam != null) _cmBrain = cam.GetComponent<CinemachineBrain>();
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                _playerLoco = dez.GetComponent<DezLocomotionController>();
                _playerCc   = dez.GetComponent<CharacterController>();
                _playerAnim = dez.GetComponentInChildren<Animator>(true);
            }
        }

        public void PlayCutscene()
        {
            if (_isPlaying) return;
            if (_director == null) _director = GetComponent<PlayableDirector>();
            if (_config == null) _config = CutsceneSubtitleConfig.FromJson(subtitleJson);
            CacheActors();

            _isPlaying = true;
            _elapsed = 0f;
            _skipHoldTimer = 0f;
            _flickerActive = false;
            Time.timeScale = 1f;

            if (_playerLoco != null) _playerLoco.enabled = false;
            _actorGuard.BeginCutscene(_playerLoco != null ? _playerLoco.gameObject : null);
            if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }

            // Posiciona Dez em frente a bancada com a foto e a bugiganga quebrada (coreografado, sem colisao)
            if (_playerLoco != null)
            {
                _playerLoco.transform.position = new Vector3(-28.4f, 0.02f, -18.2f);
                _playerLoco.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, 0f, 0.89f));
            }

            if (brokenTrinketProp != null) brokenTrinketProp.SetActive(true);
            if (camerasRoot != null)
            {
                camerasRoot.SetActive(true);
                var bq = camerasRoot.transform.Find("VCam_CS03_B4_CutToBlack/CS03_CutToBlack_Backdrop");
                if (bq != null) bq.gameObject.SetActive(false);
            }
            if (returnFlashVolume != null) { returnFlashVolume.gameObject.SetActive(true); returnFlashVolume.weight = 1f; }
            if (_cmBrain != null) _cmBrain.enabled = true;

            if (uiOverlay != null)
                uiOverlay.BeginCutscene(_config.skipHintText, string.Empty);

            _director.time = 0;
            _director.Play();
        }

        public void SkipCutscene()
        {
            if (!_isPlaying) return;
            FinishCutscene(skipped: true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(replayDebugKey))
            {
                if (_isPlaying) FinishCutscene(skipped: true);
                PlayCutscene();
                return;
            }

            if (!_isPlaying || _director == null) return;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            _elapsed = (float)_director.time;

            if (uiOverlay != null && _config != null)
            {
                uiOverlay.SetSubtitle(_config.GetActiveLine(_elapsed));

                bool canSkip = _elapsed >= _config.skipHintDelaySec;
                if (canSkip)
                {
                    bool holding = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Escape);
                    _skipHoldTimer = holding
                        ? Mathf.Min(skipHoldDuration, _skipHoldTimer + dt)
                        : Mathf.Max(0f, _skipHoldTimer - dt * 2.5f);
                    uiOverlay.SetSkipPrompt(true, _skipHoldTimer / Mathf.Max(0.1f, skipHoldDuration));
                    if (_skipHoldTimer >= skipHoldDuration)
                    {
                        SkipCutscene();
                        return;
                    }
                }
                else
                {
                    uiOverlay.SetSkipPrompt(false, 0f);
                }
            }

            // Beat 1 (0..1.8s): Flash branco de retorno decai rapido (<=1.8s, imagem util <1.2s, regra QA <=3s!)
            if (returnFlashVolume != null)
            {
                if (_elapsed <= 1.8f)
                    returnFlashVolume.weight = Mathf.Clamp01(1f - (_elapsed / 1.8f));
                else
                    returnFlashVolume.weight = 0f;
            }

            // Beat 3 (20..26s): Lampada da oficina pisca breve (nunca escura por >0.2s)
            if (_flickerActive && ceilingLightsRoot != null)
            {
                bool on = Mathf.Sin(_elapsed * 31f) > -0.75f;
                ceilingLightsRoot.SetActive(on);
            }

            float dur = _config != null && _config.totalDurationSec > 1f ? _config.totalDurationSec : (float)_director.duration;
            // Corte seco final pro preto limitado a <=2.0s (regra QA <3.0s!)
            if (camerasRoot != null)
            {
                var bq = camerasRoot.transform.Find("VCam_CS03_B4_CutToBlack/CS03_CutToBlack_Backdrop");
                if (bq != null) bq.gameObject.SetActive(_elapsed >= dur - 2.0f);
            }
            if (_elapsed >= dur - 0.05f || _director.state != PlayState.Playing)
            {
                FinishCutscene(skipped: false);
            }
        }

        private void FinishCutscene(bool skipped)
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            _flickerActive = false;

            if (_director != null) _director.Stop();
            if (sfxSource != null) sfxSource.Stop();
            if (humSource != null) humSource.Stop();

            RestoreRigImmediate();

            // Ativa as 5 anomalias ambientais do Ato 2 (Gate 2)!
            if (anomalyManager == null) anomalyManager = FindAnyObjectByType<WorkshopAnomalyManager>();
            if (anomalyManager != null) anomalyManager.ActivateAllAnomalies(persistFlag: true);

            PlayerPrefs.SetInt(PlayedPrefKey, 1);
            PlayerPrefs.Save();

            if (_playerLoco != null)
            {
                _playerLoco.transform.position = new Vector3(-28.4f, 0.02f, -18.4f);
                _playerLoco.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }
            }
            _actorGuard.EndCutscene();
            if (_playerLoco != null) _playerLoco.enabled = true;

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (uiOverlay != null)
                uiOverlay.EndCutsceneAndShowObjective(string.Empty);

            ObjectiveSystem.Set(_config != null ? _config.postObjectiveText : "Investigue a oficina");
            ObjectiveSystem.Flash();
            Debug.Log($"[Cutscene03] Cutscene 3 ('O Retorno') encerrada (skipped={skipped}). 5 anomalias ativas + objetivo 'Investigue a oficina'.");
            OnCutscene03Ended?.Invoke();
        }

        private void RestoreRigImmediate()
        {
            _flickerActive = false;
            if (camerasRoot != null) camerasRoot.SetActive(false);
            if (returnFlashVolume != null) { returnFlashVolume.weight = 0f; returnFlashVolume.gameObject.SetActive(false); }
            if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(true);
        }

        private void TeleportDez(Vector3 pos, Quaternion rot)
        {
            if (_playerLoco == null) return;
            bool hadCc = _playerCc != null && _playerCc.enabled;
            if (hadCc) _playerCc.enabled = false;
            _playerLoco.transform.position = pos;
            _playerLoco.transform.rotation = rot;
            if (hadCc) _playerCc.enabled = true;
        }

        // === RECEIVERS DE SIGNALEMITTERS DO TIMELINE (CS03) ===
        public void OnSignalCS03Beat1Flash()
        {
            if (sfxSource != null && flashReturnClip != null)
            {
                sfxSource.Stop();
                sfxSource.clip = flashReturnClip;
                sfxSource.volume = 0.72f;
                sfxSource.Play();
            }
        }

        public void OnSignalCS03Beat2PhotoPeace()
        {
            // Silencio pacifico — Dez respira e olha a foto
            if (humSource != null) humSource.Stop();
        }

        public void OnSignalCS03Beat3BrokenAndHum()
        {
            _flickerActive = true;
            if (brokenTrinketProp != null) brokenTrinketProp.SetActive(true);
            if (humSource != null && machineHumClip != null)
            {
                humSource.clip = machineHumClip;
                humSource.loop = true;
                humSource.volume = 0.76f;
                humSource.Play();
            }
        }

        public void OnSignalCS03Beat3CutToBlack()
        {
            _flickerActive = false;
            if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(true);
            // Sem musica — so o zumbido grave no escuro!
        }

        public void OnSignalCS03End()
        {
            FinishCutscene(skipped: false);
        }
    }
}
