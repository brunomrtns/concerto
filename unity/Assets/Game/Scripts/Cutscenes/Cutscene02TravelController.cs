using System;
using Studio.Game.Animation;
using Studio.Game.Dialogue;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Cutscene02TravelController — Controlador da Cutscene 2 ("A Viagem", 85s, 5 Beats),
    /// reusando 100% o padrao canonico da Cutscene 1 (Timeline + Cinemachine 3.1.4 + CutsceneUIOverlay + JSON + Skip):
    ///   - Beat 1 (0-15s): Ativacao da maquina, luzes piscando, lona cai revelando a maquina completa
    ///   - Beat 2 (15-30s): Viagem no tempo economica (overexposure + bloom + particulas + som esticado + silencio subito)
    ///   - Beat 3 (30-65s): Oficina NO PASSADO (Memory URP Volume quente, espaco da maquina VAZIO, esposa Lira na bancada + caixinha de musica)
    ///   - Beat 4 (65-80s): Primeiro errinho (som distorce, luz pisca, poeira cai do teto, corte seco pro preto)
    ///   - Beat 5 (80-85s): Cartao TEMP "FIM DO ATO 1 — CONCERTO" -> restaura 100% a oficina normal (ZERO vazamento do volume de memoria!)
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class Cutscene02TravelController : MonoBehaviour
    {
        [Header("Config & Dados")]
        public string cutsceneId = "cs02_a_viagem";
        public TextAsset subtitleJson;
        public KeyCode replayDebugKey = KeyCode.F8;
        public float skipHoldDuration = 0.85f;

        [Header("Referencias de Cena (Reusadas + Rig CS02)")]
        public CutsceneUIOverlay uiOverlay;
        public GameObject camerasRoot;
        public GameObject pastMemoryLightRig;
        public Volume warpExposureVolume;
        public Volume pastMemoryVolume;

        [Header("Props & Atores de Estado (Presente vs Passado)")]
        public GameObject coveredMachineProp;     // CS01_Maquina_Coberta_Lona (some quando lona cai e no passado!)
        public GameObject uncoveredMachineProp;   // CS02_Maquina_Completa (aparece no Beat 1, some no passado!)
        public GameObject wifeLiraActor;          // NPC_Lira_Passado (so aparece no Beat 3/4 no passado!)
        public GameObject livingNpcsRoot;         // Caldeiral_Living_Alien_NPCs (oculto no passado, restaurado depois!)
        public GameObject ceilingLightsRoot;      // Caldeiral_CeilingLights (pisca no Beat 1 e Beat 4!)
        public ParticleSystem warpParticles;      // Particulas economicas da dobra temporal (Beat 2)
        public ParticleSystem ceilingDustParticles; // Poeira caindo do teto sem tempestade (Beat 4)

        [Header("Audio TEMP")]
        public AudioSource mainAudioSource;
        public AudioSource musicBoxSource;
        public AudioClip machineChargeClip;
        public AudioClip timewarpStretchClip;
        public AudioClip glitchDistortClip;

        private PlayableDirector _director;
        private CutsceneSubtitleConfig _config;
        private CinemachineBrain _cmBrain;
        private DezLocomotionController _playerLoco;
        private CharacterController _playerCc;
        private Animator _playerAnim;

        private bool _isPlaying;
        private float _elapsed;
        private float _skipHoldTimer;
        private bool _inPastMode;
        private bool _flickerActive;
        private ColorAdjustments _warpColorAdj;
        private Bloom _warpBloom;

        public bool IsPlaying => _isPlaying;
        public bool IsMemoryVolumeActive => pastMemoryVolume != null && pastMemoryVolume.gameObject.activeInHierarchy && pastMemoryVolume.weight > 0.01f;
        public event Action OnCutscene02Ended;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            _config = CutsceneSubtitleConfig.FromJson(subtitleJson);

            if (warpExposureVolume != null && warpExposureVolume.sharedProfile != null)
            {
                warpExposureVolume.sharedProfile.TryGet(out _warpColorAdj);
                warpExposureVolume.sharedProfile.TryGet(out _warpBloom);
            }

            RestorePresentStateImmediate(afterActivation: false);
        }

        private void CacheActors()
        {
            var cam = Camera.main;
            if (cam != null) _cmBrain = cam.GetComponent<CinemachineBrain>();

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo != null)
            {
                _playerLoco = dezGo.GetComponent<DezLocomotionController>();
                _playerCc   = dezGo.GetComponent<CharacterController>();
                _playerAnim = dezGo.GetComponentInChildren<Animator>(true);
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
            _inPastMode = false;
            _flickerActive = false;
            Time.timeScale = 1f;

            if (_playerLoco != null) _playerLoco.enabled = false;
            if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }

            // Posiciona Dez em frente a maquina para o Beat 1
            TeleportDez(new Vector3(-27.2f, 0.02f, -15.6f), Quaternion.LookRotation(new Vector3(1f, 0f, 0.1f)));

            // Estado inicial Beat 1 (presente: lona ainda cobrindo, maquina completa pronta pra revelar)
            if (coveredMachineProp != null) coveredMachineProp.SetActive(true);
            if (uncoveredMachineProp != null) uncoveredMachineProp.SetActive(false);
            if (wifeLiraActor != null) wifeLiraActor.SetActive(false);
            if (pastMemoryLightRig != null) pastMemoryLightRig.SetActive(false);
            if (pastMemoryVolume != null) { pastMemoryVolume.weight = 0f; pastMemoryVolume.gameObject.SetActive(false); }
            if (warpExposureVolume != null) { warpExposureVolume.weight = 0f; warpExposureVolume.gameObject.SetActive(true); }
            if (camerasRoot != null) camerasRoot.SetActive(true);
            if (_cmBrain != null) _cmBrain.enabled = true;

            if (uiOverlay != null)
                uiOverlay.BeginCutscene(_config.skipHintText, _config.titleCardText);

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

            // 1. Legendas + Skip + Cartao Fim do Ato 1
            if (uiOverlay != null && _config != null)
            {
                uiOverlay.SetSubtitle(_config.GetActiveLine(_elapsed));

                // Corte seco pro preto em 78.5s -> Cartao "FIM DO ATO 1 / CONCERTO" (80.0s..84.8s)
                float cardAlpha = 0f;
                if (_elapsed >= _config.titleCardStartSec && _elapsed <= _config.titleCardEndSec)
                {
                    cardAlpha = Mathf.Clamp01((_elapsed - _config.titleCardStartSec) / 0.8f);
                }
                uiOverlay.SetTitleCardAlpha(cardAlpha);

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

            // 2. Efeito economico de viagem no tempo no Beat 2 (15s..30s): curva de overexposure + bloom
            if (warpExposureVolume != null)
            {
                if (_elapsed >= 15.0f && _elapsed <= 30.5f)
                {
                    float u = (_elapsed - 15.0f) / 15.5f;
                    // Pico branco forte em u ~ 0.72 (t ~ 26s), depois dissolve suave pro passado!
                    float peak = u < 0.72f ? Mathf.Pow(u / 0.72f, 2.2f) : Mathf.Clamp01(1f - (u - 0.72f) / 0.28f);
                    warpExposureVolume.weight = peak;
                }
                else if (_elapsed >= 78.2f && _elapsed < 80.2f)
                {
                    // Corte seco pro preto no fim do Beat 4!
                    warpExposureVolume.weight = 0f;
                }
                else
                {
                    warpExposureVolume.weight = 0f;
                }
            }

            // 3. Pisca sutil das luzes da oficina durante Beat 1 (carga) e Beat 4 (o primeiro errinho!)
            if (_flickerActive && ceilingLightsRoot != null)
            {
                bool on = Mathf.Sin(_elapsed * 28f) > -0.35f;
                ceilingLightsRoot.SetActive(on);
            }

            // 4. Encerramento automatico ao fim do Timeline
            float dur = _config != null && _config.totalDurationSec > 1f ? _config.totalDurationSec : (float)_director.duration;
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
            if (mainAudioSource != null) mainAudioSource.Stop();
            if (musicBoxSource != null) musicBoxSource.Stop();

            // CRITICO (Gate 3): Restaura 100% a oficina do presente — ZERO vazamento de memoria/post-processing!
            RestorePresentStateImmediate(afterActivation: true);
            if (_cmBrain != null) _cmBrain.enabled = false;

            if (_playerLoco != null)
            {
                TeleportDez(new Vector3(-28.5f, 0.02f, -18.5f), Quaternion.Euler(0f, 45f, 0f));
                if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }
                _playerLoco.enabled = true;
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (uiOverlay != null)
                uiOverlay.EndCutsceneAndShowObjective(string.Empty);

            ObjectiveSystem.Set(_config != null ? _config.postObjectiveText : "Fim do Ato 1");
            Debug.Log($"[Cutscene02] Cutscene 2 ('A Viagem') encerrada (skipped={skipped}). Oficina restaurada sem vazamento de memoria.");
            OnCutscene02Ended?.Invoke();
        }

        private void RestorePresentStateImmediate(bool afterActivation)
        {
            _inPastMode = false;
            _flickerActive = false;
            if (camerasRoot != null) camerasRoot.SetActive(false);
            if (pastMemoryLightRig != null) pastMemoryLightRig.SetActive(false);
            if (pastMemoryVolume != null) { pastMemoryVolume.weight = 0f; pastMemoryVolume.gameObject.SetActive(false); }
            if (warpExposureVolume != null) { warpExposureVolume.weight = 0f; warpExposureVolume.gameObject.SetActive(false); }
            if (warpParticles != null) warpParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (ceilingDustParticles != null) ceilingDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (wifeLiraActor != null) wifeLiraActor.SetActive(false);
            if (livingNpcsRoot != null) livingNpcsRoot.SetActive(true);
            if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(true);
            // Antes da ativacao (Ato 1): maquina sob lona. Depois da ativacao: maquina completa revelada.
            if (coveredMachineProp != null) coveredMachineProp.SetActive(!afterActivation);
            if (uncoveredMachineProp != null) uncoveredMachineProp.SetActive(afterActivation);
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

        // === RECEIVERS DE SIGNALEMITTERS DO TIMELINE (CS02) ===
        public void OnSignalBeat1ChargeAndUncover()
        {
            _flickerActive = true;
            if (mainAudioSource != null && machineChargeClip != null)
            {
                mainAudioSource.Stop();
                mainAudioSource.clip = machineChargeClip;
                mainAudioSource.volume = 0.72f;
                mainAudioSource.Play();
            }
            // Lona cai revelando a maquina do tempo completa!
            if (coveredMachineProp != null) coveredMachineProp.SetActive(false);
            if (uncoveredMachineProp != null) uncoveredMachineProp.SetActive(true);
        }

        public void OnSignalBeat2TimeWarp()
        {
            _flickerActive = false;
            if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(true);
            if (warpParticles != null) warpParticles.Play();
            if (mainAudioSource != null && timewarpStretchClip != null)
            {
                mainAudioSource.Stop();
                mainAudioSource.clip = timewarpStretchClip;
                mainAudioSource.volume = 0.78f;
                mainAudioSource.Play();
            }
        }

        public void OnSignalBeat3EnterPastMemory()
        {
            _inPastMode = true;
            if (warpParticles != null) warpParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Detalhe de cuidado canonico: espaco da maquina VAZIO e limpo no passado!
            if (coveredMachineProp != null) coveredMachineProp.SetActive(false);
            if (uncoveredMachineProp != null) uncoveredMachineProp.SetActive(false);
            if (livingNpcsRoot != null) livingNpcsRoot.SetActive(false);

            // Esposa Lira na bancada consertando a bugiganga + Dez ao lado dela!
            if (wifeLiraActor != null) wifeLiraActor.SetActive(true);
            TeleportDez(new Vector3(-28.4f, 0.02f, -18.2f), Quaternion.LookRotation(new Vector3(-0.6f, 0f, 0.8f)));

            // Liga tratamento de memoria quente/suave (URP Volume + Luz suave)
            if (pastMemoryLightRig != null) pastMemoryLightRig.SetActive(true);
            if (pastMemoryVolume != null)
            {
                pastMemoryVolume.gameObject.SetActive(true);
                pastMemoryVolume.weight = 1f;
            }

            // Reusa o tema da caixinha de musica da Cutscene 1!
            if (musicBoxSource != null) { musicBoxSource.volume = 0.68f; musicBoxSource.Play(); }
        }

        public void OnSignalBeat4FirstGlitch()
        {
            _flickerActive = true;
            if (ceilingDustParticles != null) ceilingDustParticles.Play();
            if (musicBoxSource != null) musicBoxSource.Stop();
            if (mainAudioSource != null && glitchDistortClip != null)
            {
                mainAudioSource.Stop();
                mainAudioSource.clip = glitchDistortClip;
                mainAudioSource.volume = 0.78f;
                mainAudioSource.Play();
            }
        }

        public void OnSignalBeat5CutToBlackCard()
        {
            _flickerActive = false;
            if (ceilingLightsRoot != null) ceilingLightsRoot.SetActive(false);
            if (pastMemoryLightRig != null) pastMemoryLightRig.SetActive(false);
        }

        public void OnSignalCutscene02End()
        {
            FinishCutscene(skipped: false);
        }
    }
}
