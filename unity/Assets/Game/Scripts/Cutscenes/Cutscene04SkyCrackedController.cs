using System;
using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Repair;
using Studio.Game.Save;
using Studio.Game.Sector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Cutscene04SkyCrackedController — Cutscene 4 ("O Ceu Rachou", 36s, 4 Beats, ponte Oficina -> Ato 3 / Setor Externo):
    ///   - BEAT 1 (0:00–0:10): Oficina. Alarme discreto entra, luzes caem pra backup. Juno entra apressada: "Dez. Ta acontecendo la fora."
    ///   - BEAT 2 (0:10–0:18): Os dois ate a porta do setor. Porta pesada destrava com esforco (som e o heroi: metal, pressao, vento).
    ///                         Pre-carrega SetorExterno.unity em background e faz transicao SEAMLESS na abertura da porta!
    ///   - BEAT 3 (0:18–0:32): Ja em SetorExterno! Segura na revelacao: o ceu do setor com a rachadura luminosa. Silencio, so vento.
    ///                         Engenheira Vera ao longe, parada olhando pra cima.
    ///   - BEAT 4 (0:32–0:36): Sem dialogo. Objetivo "Fale com Vera". Controle entregue ao player JA no setor + SaveSystem.Save() automatico!
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class Cutscene04SkyCrackedController : MonoBehaviour
    {
        public const string PlayedPrefKey = "CONCERTO_CS04_PLAYED";
        public static Cutscene04SkyCrackedController ActiveInstance { get; private set; }

        [Header("Config & Dados")]
        public string cutsceneId = "cs04_o_ceu_rachou";
        public string targetSectorScene = "SetorExterno";
        public TextAsset subtitleJson;
        public KeyCode replayDebugKey = KeyCode.F8;
        public float skipHoldDuration = 0.85f;
        public bool autoChainAfterAct2FinalBeat = true;
        public float chainDelayAfterAct2Sec = 1.6f;

        [Header("Referencias Oficina (Caldeiral)")]
        public CutsceneUIOverlay uiOverlay;
        public GameObject ceilingLightsRoot;
        public GameObject backupLightsRoot;

        [Header("Audio TEMP (Reuso + SFX CS04)")]
        public AudioSource sfxSource;
        public AudioSource windSource;
        public AudioClip alarmBackupClip;
        public AudioClip heavyDoorUnlockClip;
        public AudioClip coldWindClip;

        private PlayableDirector _director;
        private CutsceneSubtitleConfig _config;
        private CinemachineBrain _cmBrain;
        private DezLocomotionController _playerLoco;
        private CharacterController _playerCc;
        private Animator _playerAnim;
        private GameObject _junoGo;
        private Animator _junoAnim;
        private MonoBehaviour _junoRoutine;
        private Transform _doorFrame;
        private Vector3 _doorFrameStartPos;

        private bool _isPlaying;
        private bool _inSectorScene;
        private bool _sceneLoadStarted;
        private bool _sceneSwapDone;
        private bool _beat2Started;
        private bool _beat3Started;
        private bool _finishing;
        private float _elapsed;
        private float _skipHoldTimer;
        private AsyncOperation _preloadOp;
        private Coroutine _chainRoutine;
        private readonly CutsceneActorGuard _actorGuard = new CutsceneActorGuard();

        public bool IsPlaying => _isPlaying;
        public bool IsInSectorScene => _inSectorScene;
        public float ElapsedSeconds => _elapsed;
        public event Action OnCutscene04Ended;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            _config = CutsceneSubtitleConfig.FromJson(subtitleJson);
            if (backupLightsRoot != null) backupLightsRoot.SetActive(false);
        }

        private void Start()
        {
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.OnAct2FinalBeatCompleted += HandleAct2FinalBeatCompleted;
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.OnAct2FinalBeatCompleted -= HandleAct2FinalBeatCompleted;
        }

        private void HandleAct2FinalBeatCompleted()
        {
            if (!autoChainAfterAct2FinalBeat || _isPlaying) return;
            if (_chainRoutine != null) StopCoroutine(_chainRoutine);
            _chainRoutine = StartCoroutine(ChainAfterDelayRoutine());
        }

        private IEnumerator ChainAfterDelayRoutine()
        {
            yield return new WaitForSeconds(chainDelayAfterAct2Sec);
            _chainRoutine = null;
            if (!_isPlaying) PlayCutscene();
        }

        private void CacheCurrentSceneActors()
        {
            var cam = Camera.main;
            _cmBrain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;

            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                _playerLoco = dez.GetComponent<DezLocomotionController>();
                _playerCc   = dez.GetComponent<CharacterController>();
                _playerAnim = dez.GetComponentInChildren<Animator>(true);
            }
            else
            {
                _playerLoco = null;
                _playerCc = null;
                _playerAnim = null;
            }

            if (!_inSectorScene)
            {
                _junoGo = GameObject.Find("NPC_Juno_Oficina");
                if (_junoGo != null)
                {
                    _junoAnim = _junoGo.GetComponentInChildren<Animator>(true);
                    var rType = Type.GetType("Studio.Game.Dialogue.AlienNpcRoutineController, Assembly-CSharp");
                    _junoRoutine = rType != null ? _junoGo.GetComponent(rType) as MonoBehaviour : null;
                }

                var doorGo = GameObject.Find("Door_Caldeiral_To_Sector");
                _doorFrame = doorGo != null ? doorGo.transform.Find("BlastDoor_Frame") : null;
                if (_doorFrame != null) _doorFrameStartPos = _doorFrame.localPosition;
            }
        }

        public void PlayCutscene()
        {
            if (_isPlaying) return;
            if (_director == null) _director = GetComponent<PlayableDirector>();
            if (_config == null) _config = CutsceneSubtitleConfig.FromJson(subtitleJson);

            ActiveInstance = this;
            _isPlaying = true;
            _finishing = false;
            _inSectorScene = SceneManager.GetActiveScene().name == targetSectorScene;
            _sceneLoadStarted = false;
            _sceneSwapDone = _inSectorScene;
            _beat2Started = false;
            _beat3Started = false;
            _elapsed = 0f;
            _skipHoldTimer = 0f;
            Time.timeScale = 1f;

            // Persiste o runner + UI overlay + AudioSources durante a transicao seamless Caldeiral -> SetorExterno!
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            if (uiOverlay != null)
            {
                uiOverlay.transform.SetParent(transform, true);
                uiOverlay.BeginCutscene(_config != null ? _config.skipHintText : "Segure [Espaço] ou [Esc] para pular", string.Empty);
                uiOverlay.SetFadeAlpha(0f);
            }

            CacheCurrentSceneActors();
            LockPlayer(true);
            _actorGuard.BeginCutscene(_playerLoco != null ? _playerLoco.gameObject : null);

            // BEAT 1 (0:00–0:10): Oficina cai para luzes de backup (com intensidade legivel >= 6.8) + alarme + Juno entra apressada
            if (!_inSectorScene)
            {
                if (DialogueController.Instance != null) DialogueController.Instance.enabled = false;
                if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(false);
                if (backupLightsRoot != null)
                {
                    backupLightsRoot.SetActive(true);
                    foreach (var l in backupLightsRoot.GetComponentsInChildren<Light>(true))
                    {
                        l.intensity = Mathf.Max(l.intensity, 7.2f);
                        l.range = Mathf.Max(l.range, 24f);
                    }
                }

                TeleportDez(new Vector3(-25.6f, 0.02f, -18.0f), Quaternion.LookRotation(new Vector3(-0.75f, 0f, -0.65f)));
                if (_junoRoutine != null) _junoRoutine.enabled = false;
                if (_junoGo != null)
                {
                    _junoGo.transform.position = new Vector3(-28.4f, 0.02f, -19.5f);
                    _junoGo.transform.LookAt(new Vector3(-25.6f, 0.02f, -18.0f));
                }

                if (sfxSource != null && alarmBackupClip != null)
                {
                    sfxSource.Stop();
                    sfxSource.clip = alarmBackupClip;
                    sfxSource.loop = false;
                    sfxSource.volume = 0.72f * GameSettingsManager.SfxVolume;
                    sfxSource.Play();
                }
            }

            if (_cmBrain != null) _cmBrain.enabled = false;
            if (_director != null && _director.playableAsset != null)
            {
                _director.time = 0;
                _director.Play();
            }
        }

        public void SkipCutscene()
        {
            if (!_isPlaying || _finishing) return;
            StartCoroutine(FinishAndHandOverControlRoutine(skipped: true));
        }

        private void Update()
        {
            if (Input.GetKeyDown(replayDebugKey) && !_isPlaying)
            {
                PlayCutscene();
                return;
            }

            if (!_isPlaying || _finishing) return;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            _elapsed += dt;

            // Legendas + Skip por segurar Espaco / Esc (100% padrao CS01–CS03)
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

            // === BEAT 1 (0:00–0:10): Oficina com luz de backup + Juno entra apressada ===
            if (_elapsed < 10.0f && !_inSectorScene)
            {
                UpdateBeat1WorkshopAlarm(dt);
            }
            // === BEAT 2 (0:10–0:18): Os dois ate a porta do setor + destrava com esforco + pre-load SetorExterno ===
            else if (_elapsed < 18.0f && !_sceneSwapDone)
            {
                UpdateBeat2HeavyDoorAndPreload(dt);
            }
            // === TRANSIÇÃO SEAMLESS (17.7s..18.2s) -> BEAT 3 (0:18–0:32): Ceu Rachado + Vera ao longe ===
            else if (_elapsed < 32.0f)
            {
                if (!_sceneSwapDone)
                {
                    StartCoroutine(ActivatePreloadedSectorSceneSeamless());
                }
                else
                {
                    UpdateBeat3SkyRevealAndVera(dt);
                }
            }
            // === BEAT 4 (0:32–0:36): Sem dialogo, pousa atras do Dez -> devolve controle + auto-save ===
            else
            {
                UpdateBeat4SettleAndFinish(dt);
            }
        }

        private void UpdateBeat1WorkshopAlarm(float dt)
        {
            // Juno corre apressada ate perto do Dez (0.4s -> 3.4s) no espaco livre da oficina
            if (_junoGo != null)
            {
                Vector3 target = new Vector3(-26.6f, 0.02f, -18.5f);
                float tRun = Mathf.Clamp01((_elapsed - 0.4f) / 3.0f);
                _junoGo.transform.position = Vector3.Lerp(new Vector3(-28.4f, 0.02f, -19.5f), target, tRun);
                _junoGo.transform.LookAt(new Vector3(-25.6f, 0.02f, -18.0f));
                if (_junoAnim != null) _junoAnim.SetFloat("Speed", tRun < 0.98f ? 2.8f : 0f);
            }

            // Camera limpa e desobstruida na oficina em luz de backup acompanhando Juno chegando ao Dez
            var cam = Camera.main;
            if (cam != null)
            {
                float t = Mathf.Clamp01(_elapsed / 10.0f);
                Vector3 p0 = new Vector3(-24.0f, 1.65f, -16.4f);
                Vector3 p1 = new Vector3(-24.5f, 1.48f, -16.8f);
                cam.transform.position = Vector3.Lerp(p0, p1, t);
                cam.transform.LookAt(new Vector3(-26.1f, 0.85f, -18.2f));
            }
        }

        private void UpdateBeat2HeavyDoorAndPreload(float dt)
        {
            if (!_beat2Started)
            {
                _beat2Started = true;

                // Destrava a flag real da porta do Setor Externo!
                SectorDoorInteractable.IsSectorUnlocked = true;
                var doorComp = FindAnyObjectByType<SectorDoorInteractable>();
                if (doorComp != null) doorComp.RefreshStatusLight();

                // Posiciona Dez e Juno em frente a porta pesada do corredor leste (-16.5, 0, -13.5)
                TeleportDez(new Vector3(-18.6f, 0.02f, -13.3f), Quaternion.LookRotation(Vector3.right));
                if (_junoGo != null)
                {
                    _junoGo.transform.position = new Vector3(-19.2f, 0.02f, -14.3f);
                    _junoGo.transform.LookAt(new Vector3(-16.5f, 0.5f, -13.5f));
                    if (_junoAnim != null) _junoAnim.SetFloat("Speed", 0f);
                }

                // Som heroi do Beat 2: metal, travas, pressao, vento do outro lado!
                if (sfxSource != null && heavyDoorUnlockClip != null)
                {
                    sfxSource.Stop();
                    sfxSource.clip = heavyDoorUnlockClip;
                    sfxSource.loop = false;
                    sfxSource.volume = 0.95f * GameSettingsManager.SfxVolume;
                    sfxSource.Play();
                }

                // Pre-carrega SetorExterno.unity em background para transicao SEAMLESS!
                if (!_sceneLoadStarted)
                {
                    _sceneLoadStarted = true;
                    _preloadOp = SceneManager.LoadSceneAsync(targetSectorScene, LoadSceneMode.Single);
                    if (_preloadOp != null) _preloadOp.allowSceneActivation = false;
                }
            }

            float localT = Mathf.Clamp01((_elapsed - 10.0f) / 8.0f);

            // A partir de 13.4s a porta pesada treme e desliza abrindo com esforco!
            if (_doorFrame != null && localT > 0.42f)
            {
                float openT = Mathf.Clamp01((localT - 0.42f) / 0.52f);
                float shake = Mathf.Sin(_elapsed * 55f) * 0.025f * (1f - openT);
                _doorFrame.localPosition = _doorFrameStartPos + new Vector3(openT * 2.2f, shake, 0f);
            }

            // Camera no corredor leste enquadrando Dez, Juno e a porta pesada abrindo (100% livre de paredes e iluminada!)
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 c0 = new Vector3(-20.4f, 1.85f, -11.4f);
                Vector3 c1 = new Vector3(-19.2f, 1.65f, -12.0f);
                cam.transform.position = Vector3.Lerp(c0, c1, localT);
                cam.transform.LookAt(new Vector3(-17.4f, 1.15f, -13.5f));
            }

            if (uiOverlay != null && _elapsed >= 17.55f)
            {
                float f = Mathf.Clamp01((_elapsed - 17.55f) / 0.40f);
                uiOverlay.SetFadeAlpha(f * 0.85f);
            }
        }

        private IEnumerator ActivatePreloadedSectorSceneSeamless()
        {
            _sceneSwapDone = true;

            if (_preloadOp == null)
                _preloadOp = SceneManager.LoadSceneAsync(targetSectorScene, LoadSceneMode.Single);

            if (_preloadOp != null)
            {
                _preloadOp.allowSceneActivation = true;
                while (!_preloadOp.isDone) yield return null;
            }

            _inSectorScene = true;
            CacheCurrentSceneActors();
            LockPlayer(true);
            if (_cmBrain != null) _cmBrain.enabled = false;

            // Posiciona Dez ja na entrada do Setor Externo olhando para o ceu rachado e Vera
            TeleportDez(new Vector3(0f, 0.05f, 3.0f), Quaternion.identity);

            // Silencio na oficina -> apenas vento frio do Setor Externo!
            if (sfxSource != null) sfxSource.Stop();
            if (windSource != null && coldWindClip != null)
            {
                windSource.clip = coldWindClip;
                windSource.loop = true;
                windSource.volume = 0.55f * GameSettingsManager.SfxVolume;
                windSource.Play();
            }

            _beat3Started = true;
        }

        private void UpdateBeat3SkyRevealAndVera(float dt)
        {
            if (!_beat3Started) return;

            // Abre o fade suave em 0.45s ja revelando o Ceu Rachado!
            if (uiOverlay != null)
            {
                float fOut = 1f - Mathf.Clamp01((_elapsed - 18.0f) / 0.45f);
                uiOverlay.SetFadeAlpha(fOut * 0.85f);
            }

            var cam = Camera.main;
            if (cam == null) return;

            if (_elapsed < 26.0f)
            {
                // Shot 3A (18s–26s): SEGURA na revelacao do CEU ERRADO (Rachadura Luminosa + Aurora Glitch)
                float t = Mathf.Clamp01((_elapsed - 18.0f) / 8.0f);
                cam.transform.position = Vector3.Lerp(new Vector3(0f, 2.0f, 1.2f), new Vector3(0f, 2.8f, 3.0f), t);
                cam.transform.LookAt(Vector3.Lerp(new Vector3(0f, 13.5f, 44f), new Vector3(0f, 16.0f, 44f), t));
            }
            else
            {
                // Shot 3B (26s–32s): Enquadra Engenheira Vera ao longe, parada olhando para cima em silencio
                float t = Mathf.Clamp01((_elapsed - 26.0f) / 6.0f);
                var vera = GameObject.Find("NPC_Engenheira_Vera");
                Vector3 vPos = vera != null ? vera.transform.position : new Vector3(-2.6f, 0.02f, 8.2f);
                if (vera != null)
                {
                    // Vera olha para o horizonte / rachadura no ceu
                    vera.transform.rotation = Quaternion.LookRotation(new Vector3(0.15f, 0f, 1f));
                }
                cam.transform.position = Vector3.Lerp(vPos + new Vector3(2.8f, 1.35f, -4.8f), vPos + new Vector3(1.9f, 1.15f, -3.2f), t);
                cam.transform.LookAt(vPos + new Vector3(0f, 1.65f, 4.5f));
            }
        }

        private void UpdateBeat4SettleAndFinish(float dt)
        {
            float dur = _config != null && _config.totalDurationSec > 1f ? _config.totalDurationSec : 36.0f;
            float t = Mathf.Clamp01((_elapsed - 32.0f) / Mathf.Max(0.5f, dur - 32.0f));

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(new Vector3(0f, 2.6f, -0.8f), new Vector3(0f, 2.2f, -0.2f), t);
                cam.transform.LookAt(new Vector3(-1.2f, 1.5f, 10.5f));
            }

            if (_elapsed >= dur)
            {
                StartCoroutine(FinishAndHandOverControlRoutine(skipped: false));
            }
        }

        private IEnumerator FinishAndHandOverControlRoutine(bool skipped)
        {
            if (_finishing) yield break;
            _finishing = true;
            _isPlaying = false;

            if (_director != null) _director.Stop();
            if (sfxSource != null) sfxSource.Stop();
            if (windSource != null) windSource.Stop();

            // Garante flag real da porta + flag da CS04
            SectorDoorInteractable.IsSectorUnlocked = true;
            PlayerPrefs.SetInt(PlayedPrefKey, 1);
            PlayerPrefs.Save();

            // Se pulou ainda na Oficina, garante transicao limpa para SetorExterno antes de entregar controle!
            if (SceneManager.GetActiveScene().name != targetSectorScene)
            {
                if (_preloadOp != null)
                {
                    _preloadOp.allowSceneActivation = true;
                    while (!_preloadOp.isDone) yield return null;
                }
                else
                {
                    var op = SceneManager.LoadSceneAsync(targetSectorScene, LoadSceneMode.Single);
                    while (op != null && !op.isDone) yield return null;
                }
                yield return null;
            }

            _inSectorScene = true;
            CacheCurrentSceneActors();

            // Posiciona Dez na entrada do Setor Externo e devolve controle 100% limpo
            TeleportDez(new Vector3(0f, 0.05f, 3.0f), Quaternion.identity);
            _actorGuard.EndCutscene();
            LockPlayer(false);
            if (_cmBrain != null) _cmBrain.enabled = true;
            if (DialogueController.Instance != null) DialogueController.Instance.enabled = true;

            // Ativa as 3 anomalias do Setor Externo para o Ato 3
            var anomMgr = FindAnyObjectByType<Studio.Game.Anomalies.WorkshopAnomalyManager>();
            if (anomMgr != null && !anomMgr.AreAnomaliesActive)
                anomMgr.ActivateAllAnomalies(persistFlag: false);

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (uiOverlay != null)
            {
                uiOverlay.SetFadeAlpha(0f);
                uiOverlay.EndCutsceneAndShowObjective(string.Empty);
            }

            string postObj = _config != null && !string.IsNullOrEmpty(_config.postObjectiveText)
                ? _config.postObjectiveText
                : "Fale com Vera";
            ObjectiveSystem.Set(postObj);
            ObjectiveSystem.Flash();

            // GATE 3: Auto-save dispara no fim JA dentro do Setor Externo!
            SaveSystem.Save("cp_pos_cs04_setor_externo", new Vector3(0f, 0.05f, 3.0f), 0f);

            Debug.Log($"[Cutscene04] Cutscene 4 ('O Ceu Rachou') concluida (skipped={skipped}). Controle entregue em SetorExterno + objetivo '{postObj}' + auto-save gravado!");
            OnCutscene04Ended?.Invoke();

            // Libera runner DontDestroyOnLoad apos encerrar com seguranca
            Destroy(gameObject, 0.1f);
        }

        private void LockPlayer(bool locked)
        {
            if (_playerLoco != null) _playerLoco.enabled = !locked;
            if (_playerAnim != null && locked)
            {
                _playerAnim.SetFloat("Speed", 0f);
                _playerAnim.SetBool("IsGrounded", true);
            }
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
    }
}
