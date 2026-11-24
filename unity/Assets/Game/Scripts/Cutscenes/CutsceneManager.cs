using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Studio.Game.Animation;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// CutsceneManager — Sistema canonico e reutilizavel de cutscenes in-engine de CONCERTO:
    ///   - Unity Timeline (PlayableDirector) + Cinemachine 3.x (CinemachineBrain + CinemachineCamera)
    ///   - Rig de luz proprio da cutscene + URP Volume temporario (100% restaurados ao terminar/pular)
    ///   - Legendas via JSON (CutsceneSubtitleConfig) + Letterbox uGUI (CutsceneUIOverlay)
    ///   - Skippable (segurar Espaco/Esc apos 3s) com restauracao perfeita do gameplay
    ///   - Disparo automatico no primeiro play (PlayerPrefs) + atalho de debug F9 (replay) e F10 (reset flag)
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class CutsceneManager : MonoBehaviour
    {
        public const string PlayedPrefKey = "CONCERTO_CS01_PLAYED";

        [Header("Configuracao & Dados")]
        public string cutsceneId = "cs01_abertura_concerto";
        public TextAsset subtitleJson;
        public bool playOnFirstBoot = true;
        public bool forcePlayForCapture = false;

        [Header("Atalhos de Debug")]
        public KeyCode replayDebugKey = KeyCode.F9;
        public KeyCode resetFlagDebugKey = KeyCode.F10;
        public float skipHoldDuration = 0.85f;

        [Header("Referencias de Cena (Cutscene Rig)")]
        public GameObject cutsceneLightRig;
        public Volume cutscenePostProcessVolume;
        public GameObject cutsceneCamerasRoot;
        public CutsceneUIOverlay uiOverlay;

        [Header("Audio TEMP (SignalEmitters)")]
        public AudioSource musicBoxSource;
        public AudioSource machineHumSource;
        public AudioSource sfxOneShotSource;
        public AudioClip radioChimeClip;
        public AudioClip memoryEchoClip;

        [Header("Atores & Posicoes da Cutscene 1")]
        public Vector3 dezStartPos = new Vector3(-30.1f, 0.02f, -17.9f);
        public Vector3 dezBoardPos = new Vector3(-28.7f, 0.02f, -15.2f);
        public Vector3 dezBoardLook = new Vector3(-28.7f, 1.5f, -12.2f);
        public Vector3 junoMarkPos = new Vector3(-27.2f, 0.02f, -15.8f);
        public Vector3 machineLookPos = new Vector3(-25.8f, 1.0f, -19.2f);

        private PlayableDirector _director;
        private CutsceneSubtitleConfig _config;
        private CinemachineBrain _cmBrain;
        private DezLocomotionController _playerLoco;
        private CharacterController _playerCc;
        private Animator _playerAnim;
        private AlienNpcRoutineController _junoRoutine;
        private CharacterController _junoCc;
        private Animator _junoAnim;

        private bool _isPlaying;
        private float _skipHoldTimer;
        private float _elapsed;
        private bool _dezWalkingToBoard;

        public bool IsPlaying => _isPlaying;
        public event Action OnCutsceneEnded;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            _config = CutsceneSubtitleConfig.FromJson(subtitleJson);

            // Garante rig de luz, volume e cameras da cutscene desligados fora da cutscene
            SetCutsceneRigActive(false);
        }

        private void Start()
        {
            CacheSceneActors();

            bool alreadyPlayed = PlayerPrefs.GetInt(PlayedPrefKey, 0) == 1;
            bool envForce = Environment.GetEnvironmentVariable("FORCE_CS01_PLAY") == "1";

            if (forcePlayForCapture || envForce || (playOnFirstBoot && !alreadyPlayed))
            {
                PlayCutscene();
            }
        }

        private void CacheSceneActors()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                _cmBrain = cam.GetComponent<CinemachineBrain>();
                if (_cmBrain != null) _cmBrain.enabled = false;
            }

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo != null)
            {
                _playerLoco = dezGo.GetComponent<DezLocomotionController>();
                _playerCc   = dezGo.GetComponent<CharacterController>();
                _playerAnim = dezGo.GetComponentInChildren<Animator>(true);
            }

            var junoGo = GameObject.Find("NPC_Juno_Oficina");
            if (junoGo != null)
            {
                _junoRoutine = junoGo.GetComponent<AlienNpcRoutineController>();
                _junoCc      = junoGo.GetComponent<CharacterController>();
                _junoAnim    = junoGo.GetComponentInChildren<Animator>(true);
            }
        }

        public void PlayCutscene()
        {
            if (_isPlaying) return;
            if (_director == null) _director = GetComponent<PlayableDirector>();
            if (_config == null) _config = CutsceneSubtitleConfig.FromJson(subtitleJson);
            CacheSceneActors();

            _isPlaying = true;
            _elapsed = 0f;
            _skipHoldTimer = 0f;
            _dezWalkingToBoard = false;
            Time.timeScale = 1f;

            // 1. Suspende controle de gameplay e rotina da Juno com seguranca
            if (_playerLoco != null) _playerLoco.enabled = false;
            if (_junoRoutine != null) _junoRoutine.enabled = false;

            // Posiciona Dez perto da bancada olhando a foto/caixinha e Juno pronta pro Beat 3
            TeleportActor(_playerLoco != null ? _playerLoco.transform : null, _playerCc, dezStartPos, Quaternion.LookRotation(new Vector3(1f, 0f, 0.35f)));
            TeleportActor(_junoRoutine != null ? _junoRoutine.transform : null, _junoCc, junoMarkPos, Quaternion.LookRotation(dezBoardPos - junoMarkPos));
            if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }
            if (_junoAnim != null)   { _junoAnim.SetFloat("Speed", 0f);   _junoAnim.SetBool("IsGrounded", true); }

            // 2. Liga Rig de luz, Post-Processing temporario, Cameras virtuais e CinemachineBrain
            SetCutsceneRigActive(true);
            if (_cmBrain != null) _cmBrain.enabled = true;

            // 3. UI Letterbox + Legendas
            if (uiOverlay != null)
                uiOverlay.BeginCutscene(_config.skipHintText, _config.titleCardText);

            // 4. Dispara Timeline
            _director.time = 0;
            _director.Play();

            // Marca flag de primeiro play
            PlayerPrefs.SetInt(PlayedPrefKey, 1);
            PlayerPrefs.Save();
        }

        public void ReplayCutscene()
        {
            if (_isPlaying) FinishCutscene(skipped: true);
            PlayCutscene();
        }

        public void SkipCutscene()
        {
            if (!_isPlaying) return;
            FinishCutscene(skipped: true);
        }

        private void Update()
        {
            // Atalhos de debug (F9 = replay da intro, F10 = limpa flag de primeiro play)
            if (Input.GetKeyDown(replayDebugKey))
            {
                ReplayCutscene();
                return;
            }
            if (Input.GetKeyDown(resetFlagDebugKey))
            {
                PlayerPrefs.DeleteKey(PlayedPrefKey);
                PlayerPrefs.Save();
                Debug.Log("[CutsceneManager] Flag CONCERTO_CS01_PLAYED resetada (F10).");
            }

            if (!_isPlaying || _director == null) return;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            _elapsed = (float)_director.time;

            // 1. Atualiza legendas do JSON
            if (uiOverlay != null && _config != null)
            {
                uiOverlay.SetSubtitle(_config.GetActiveLine(_elapsed));

                // Title Card fade cinematografico no Beat 5
                float tcAlpha = 0f;
                if (_elapsed >= _config.titleCardStartSec && _elapsed <= _config.titleCardEndSec)
                {
                    float fadeIn  = Mathf.Clamp01((_elapsed - _config.titleCardStartSec) / 1.2f);
                    float fadeOut = Mathf.Clamp01((_config.titleCardEndSec - _elapsed) / 1.0f);
                    tcAlpha = Mathf.Min(fadeIn, fadeOut);
                }
                uiOverlay.SetTitleCardAlpha(tcAlpha);

                // Prompt de Skip aparece apos 3s
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

            // 2. Coreografia sutil do Dez durante os Beats (anda ate o quadro no Beat 3, vira pra maquina no Beat 5)
            UpdateActorChoreography(dt);

            // 3. Encerramento automatico ao fim do Timeline
            float dur = _config != null && _config.totalDurationSec > 1f ? _config.totalDurationSec : (float)_director.duration;
            if (_elapsed >= dur - 0.05f || _director.state != PlayState.Playing)
            {
                FinishCutscene(skipped: false);
            }
        }

        private void UpdateActorChoreography(float dt)
        {
            if (_playerLoco == null) return;
            Transform dezT = _playerLoco.transform;

            // Beat 3 (25s..45s): Dez caminha ate o quadro de checklist e depois vira para Juno
            if (_dezWalkingToBoard && _elapsed < 45f)
            {
                Vector3 toBoard = dezBoardPos - dezT.position;
                toBoard.y = 0f;
                if (toBoard.magnitude > 0.15f)
                {
                    Vector3 step = toBoard.normalized * 1.45f * dt;
                    if (_playerCc != null && _playerCc.enabled) _playerCc.Move(step + Vector3.down * 2f * dt);
                    else dezT.position += step;
                    dezT.rotation = Quaternion.RotateTowards(dezT.rotation, Quaternion.LookRotation(toBoard.normalized), 360f * dt);
                    if (_playerAnim != null) _playerAnim.SetFloat("Speed", 1.5f, 0.08f, dt);
                }
                else
                {
                    if (_playerAnim != null) _playerAnim.SetFloat("Speed", 0f, 0.08f, dt);
                    Vector3 lookDir = (_elapsed > 28.5f ? (junoMarkPos - dezT.position) : (dezBoardLook - dezT.position));
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                        dezT.rotation = Quaternion.RotateTowards(dezT.rotation, Quaternion.LookRotation(lookDir.normalized), 220f * dt);
                }
            }
            // Beat 5 (60s..72s): Dez vira decidido para a maquina coberta
            else if (_elapsed >= 59.5f)
            {
                if (_playerAnim != null) _playerAnim.SetFloat("Speed", 0f, 0.08f, dt);
                Vector3 toMachine = machineLookPos - dezT.position;
                toMachine.y = 0f;
                if (toMachine.sqrMagnitude > 0.01f)
                    dezT.rotation = Quaternion.RotateTowards(dezT.rotation, Quaternion.LookRotation(toMachine.normalized), 180f * dt);
            }
        }

        private void FinishCutscene(bool skipped)
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            _dezWalkingToBoard = false;

            if (_director != null) _director.Stop();

            // Para todos os audios da cutscene
            if (musicBoxSource != null) musicBoxSource.Stop();
            if (machineHumSource != null) machineHumSource.Stop();
            if (sfxOneShotSource != null) sfxOneShotSource.Stop();

            // Desliga rig de luz, post-processing, cameras virtuais e CinemachineBrain -> devolve camera 3a pessoa do gameplay!
            SetCutsceneRigActive(false);
            if (_cmBrain != null) _cmBrain.enabled = false;

            // Garante Dez posicionado de pe e limpo na oficina olhando para a Juno / oficina
            if (_playerLoco != null)
            {
                Vector3 endPos = new Vector3(-29.2f, 0.02f, -18.8f);
                Quaternion endRot = Quaternion.LookRotation((junoMarkPos - endPos).normalized);
                TeleportActor(_playerLoco.transform, _playerCc, endPos, endRot);
                if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }
                _playerLoco.enabled = true;
            }
            if (_junoRoutine != null) _junoRoutine.enabled = true;

            // Garante estado de gameplay 100% integro
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (uiOverlay != null)
                uiOverlay.EndCutsceneAndShowObjective(_config != null ? _config.postObjectiveText : "Fale com Juno");

            Debug.Log($"[CutsceneManager] Cutscene '{cutsceneId}' finalizada (skipped={skipped}). Controle devolvido ao jogador.");
            OnCutsceneEnded?.Invoke();
        }

        private void SetCutsceneRigActive(bool active)
        {
            if (cutsceneLightRig != null) cutsceneLightRig.SetActive(active);
            if (cutscenePostProcessVolume != null)
            {
                cutscenePostProcessVolume.gameObject.SetActive(active);
                cutscenePostProcessVolume.weight = active ? 1f : 0f;
            }
            if (cutsceneCamerasRoot != null) cutsceneCamerasRoot.SetActive(active);
        }

        private static void TeleportActor(Transform t, CharacterController cc, Vector3 pos, Quaternion rot)
        {
            if (t == null) return;
            bool hadCc = cc != null && cc.enabled;
            if (hadCc) cc.enabled = false;
            t.position = pos;
            t.rotation = rot;
            if (hadCc) cc.enabled = true;
        }

        // === RECEIVERS DE SIGNALEMITTERS DO TIMELINE ===
        public void OnSignalMusicBox()
        {
            if (musicBoxSource != null) { musicBoxSource.volume = 0.65f; musicBoxSource.Play(); }
        }

        public void OnSignalRadioChime()
        {
            if (sfxOneShotSource != null && radioChimeClip != null)
                sfxOneShotSource.PlayOneShot(radioChimeClip, 0.55f);
        }

        public void OnSignalMachineHum()
        {
            if (machineHumSource != null) { machineHumSource.volume = 0.45f; machineHumSource.Play(); }
        }

        public void OnSignalDezWalkToBoard()
        {
            _dezWalkingToBoard = true;
        }

        public void OnSignalMemoryEcho()
        {
            if (sfxOneShotSource != null && memoryEchoClip != null)
                sfxOneShotSource.PlayOneShot(memoryEchoClip, 0.72f);
        }

        public void OnSignalTitleCardMotif()
        {
            if (musicBoxSource != null)
            {
                musicBoxSource.Stop();
                musicBoxSource.volume = 0.75f;
                musicBoxSource.Play();
            }
        }

        public void OnSignalCutsceneEnd()
        {
            FinishCutscene(skipped: false);
        }
    }
}
