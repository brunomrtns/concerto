using System;
using System.Collections;
using System.Collections.Generic;
using Studio.Game.Animation;
using Studio.Game.Anomalies;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Studio.Game.Repair
{
    /// <summary>
    /// RepairRitualController — O "Concerto" (Loop de Gameplay do Ato 2):
    ///   - Ritual harmonico satisfatorio (segurar [E] ou clique esquerdo, ~4.5s, com pulsos de ressonancia; cancelavel via [Esc]/[Q]!)
    ///   - Camera Cinemachine (VCam_Repair_Focus, Priority 15) aproxima suavemente durante o reparo
    ///   - Ferramenta alienigena TripoSG (dez_repair_tool) visivel na mao do Dez + faiscas + som de ferramenta
    ///   - Resolucao satisfatoria: Anomaly.Resolve() + chime harmonico + 0.55s do motif da caixinha (CS01) + flash dourado suave!
    ///   - HUD: "Repare as anomalias (k/N)" (extensivel automaticamente se adicionar 6a anomalia no config/inspector!)
    ///   - BEAT FINAL CANONICO: 2s de alivio -> bugiganga da esposa quebra SOZINHA (som seco, sem musica) ->
    ///     todas as luzes da oficina piscam juntas uma vez -> Fim do Ato 2!
    /// </summary>
    public sealed class RepairRitualController : MonoBehaviour
    {
        public const string Act2CompletedPrefKey = "CONCERTO_ACT2_COMPLETED";

        public static RepairRitualController Instance { get; private set; }

        [Header("Referencias de Cena")]
        public CinemachineCamera repairFocusVcam;
        public Volume resolveFlashVolume;
        public GameObject dezRepairToolVisual;
        public ParticleSystem repairSparksParticles;
        public GameObject workbenchTrinketProp;
        public GameObject ceilingLightsRoot;
        public WorkshopAnomalyManager anomalyManager;

        [Header("UI do Ritual (RepairHUD_Canvas)")]
        public CanvasGroup repairPanelGroup;
        public Text repairTitleText;
        public Image repairProgressFill;
        public Image harmonicPulseMarker;
        public Text repairHintText;

        [Header("Audio TEMP (Reusa Motif Caixinha CS01)")]
        public AudioSource channelSource;
        public AudioSource sfxSource;
        public AudioSource musicBoxSnippetSource;
        public AudioClip toolChannelClip;
        public AudioClip resolveChimeClip;
        public AudioClip trinketCrackClip;

        [Header("Parametros Tunaveis")]
        public float harmonicBoostMultiplier = 1.45f;
        public float resolveFlashDuration = 0.65f;
        public float musicBoxSnippetDuration = 0.65f;

        private readonly List<IRepairable> _trackedTargets = new List<IRepairable>();
        private IRepairable _activeTarget;
        private DezLocomotionController _playerLoco;
        private Animator _playerAnim;
        private Cutscene03ReturnController _cs03;

        private bool _isRepairing;
        private bool _isPlayingFinalBeat;
        private bool _act2Completed;
        private float _progress;
        private float _pulsePhase;
        private float _flashTimer;
        private int _totalTargets;
        private int _repairedCount;

        // Hook programatico para testes automatizados e captura de video
        public bool ScriptedHoldChannel { get; set; }

        public bool IsRepairing => _isRepairing;
        public bool IsPlayingFinalBeat => _isPlayingFinalBeat;
        public bool Act2Completed => _act2Completed;
        public int TotalTargets => _totalTargets;
        public int RepairedCount => _repairedCount;
        public float CurrentProgress => _progress;

        public event Action<int, int> OnRepairProgressChanged;
        public event Action OnAct2FinalBeatCompleted;

        private void Awake()
        {
            Instance = this;
            if (repairFocusVcam != null) repairFocusVcam.gameObject.SetActive(false);
            if (resolveFlashVolume != null) { resolveFlashVolume.weight = 0f; resolveFlashVolume.gameObject.SetActive(false); }
            if (dezRepairToolVisual != null) dezRepairToolVisual.SetActive(false);
            if (repairPanelGroup != null) repairPanelGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_cs03 != null) _cs03.OnCutscene03Ended -= HandleCutscene03Ended;
        }

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                _playerLoco = dez.GetComponent<DezLocomotionController>();
                _playerAnim = dez.GetComponentInChildren<Animator>(true);
            }
            if (anomalyManager == null) anomalyManager = FindAnyObjectByType<WorkshopAnomalyManager>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            if (_cs03 != null) _cs03.OnCutscene03Ended += HandleCutscene03Ended;

            RefreshTargets();
        }

        private void HandleCutscene03Ended()
        {
            // Mantem o objetivo canonico "Investigue a oficina" ate o jogador iniciar/concluir o 1o reparo!
            RefreshTargets();
        }

        /// <summary>
        /// Descobre dinamicamente TODOS os Repairables ativos na cena (prova de extensibilidade:
        /// adicionar uma 6a anomalia no config/inspector atualiza TotalTargets para 6 sem tocar em codigo!).
        /// </summary>
        public int RefreshTargets()
        {
            _trackedTargets.Clear();
            var all = FindObjectsByType<Repairable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int repaired = 0;
            int activeTotal = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                _trackedTargets.Add(all[i]);
                activeTotal++;
                if (all[i].IsRepaired) repaired++;
            }
            _totalTargets = activeTotal;
            _repairedCount = repaired;
            return _totalTargets;
        }

        public bool BeginRepair(IRepairable target)
        {
            if (_isRepairing || _isPlayingFinalBeat || target == null || !target.CanBeRepairedNow)
                return false;

            RefreshTargets();
            _activeTarget = target;
            _isRepairing = true;
            _progress = 0f;
            _pulsePhase = 0f;

            if (_playerLoco != null)
            {
                _playerLoco.enabled = false;
                Vector3 toTarget = target.FocusPoint.position - _playerLoco.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    _playerLoco.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }
            if (_playerAnim != null) { _playerAnim.SetFloat("Speed", 0f); _playerAnim.SetBool("IsGrounded", true); }

            if (dezRepairToolVisual != null) dezRepairToolVisual.SetActive(true);
            if (repairSparksParticles != null)
            {
                repairSparksParticles.transform.position = target.FocusPoint.position;
                repairSparksParticles.Play();
            }

            // Aproxima suavemente a camera Cinemachine do ponto de reparo
            if (repairFocusVcam != null)
            {
                Vector3 focus = target.FocusPoint.position;
                Vector3 backDir = _playerLoco != null ? -_playerLoco.transform.forward : Vector3.back;
                repairFocusVcam.transform.position = focus + backDir * 2.15f + Vector3.up * 0.95f + Vector3.right * 0.45f;
                repairFocusVcam.transform.LookAt(focus);
                repairFocusVcam.gameObject.SetActive(true);
            }

            if (repairPanelGroup != null) repairPanelGroup.alpha = 1f;
            if (repairTitleText != null) repairTitleText.text = $"CONSERTANDO: {target.DisplayName.ToUpper()}";
            if (repairHintText != null) repairHintText.text = "Segure [E] ou Clique para sintonizar  •  [Esc] / [Q] Cancelar";
            if (repairProgressFill != null) repairProgressFill.fillAmount = 0f;

            if (channelSource != null && toolChannelClip != null)
            {
                channelSource.clip = toolChannelClip;
                channelSource.loop = true;
                channelSource.volume = 0.55f * GameSettingsManager.SfxVolume;
                channelSource.Play();
            }
            return true;
        }

        /// <summary>
        /// Gate 4: Cancelar o reparo no meio devolve controle e estado 100% limpos sem bug!
        /// </summary>
        public void CancelRepair()
        {
            if (!_isRepairing) return;
            EndRitualVisualsAndRestoreControl();
            _activeTarget = null;
            Debug.Log("[RepairRitual] Reparo cancelado pelo jogador — estado restaurado limpo.");
        }

        private void Update()
        {
            // Decaimento do flash dourado de resolucao
            if (_flashTimer > 0f && resolveFlashVolume != null)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - Time.deltaTime);
                float u = _flashTimer / Mathf.Max(0.05f, resolveFlashDuration);
                resolveFlashVolume.weight = u * u;
                if (_flashTimer <= 0f) resolveFlashVolume.gameObject.SetActive(false);
            }

            if (!_isRepairing || _activeTarget == null) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q))
            {
                CancelRepair();
                return;
            }

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            _pulsePhase += dt * 2.6f;
            float pulseWave = 0.5f + 0.5f * Mathf.Sin(_pulsePhase);

            if (harmonicPulseMarker != null)
            {
                float s = 0.85f + 0.30f * pulseWave;
                harmonicPulseMarker.transform.localScale = Vector3.one * s;
            }

            bool holding = ScriptedHoldChannel || Input.GetKey(KeyCode.E) || Input.GetMouseButton(0);
            if (holding)
            {
                // Ritual atmosferico (sem frustracao): segurar avanca sempre, e nos picos harmonicos acelera suavemente!
                float boost = pulseWave > 0.72f ? harmonicBoostMultiplier : 1.0f;
                _progress = Mathf.Clamp01(_progress + (dt / _activeTarget.RepairDurationSec) * boost);
                if (channelSource != null) channelSource.pitch = 0.88f + _progress * 0.45f;
            }
            else
            {
                if (channelSource != null) channelSource.pitch = 0.82f;
            }

            if (repairProgressFill != null) repairProgressFill.fillAmount = _progress;

            if (_progress >= 1f)
            {
                CompleteActiveRepair();
            }
        }

        private void CompleteActiveRepair()
        {
            var resolved = _activeTarget;
            _activeTarget = null;

            // 1. Resolve a anomalia/objeto generico!
            resolved.OnRepairCompleted();

            // 2. Momento de Resolucao (Coracao do Game Feel): Flash dourado suave + Chime + 0.6s do motif da caixinha!
            if (resolveFlashVolume != null)
            {
                resolveFlashVolume.gameObject.SetActive(true);
                resolveFlashVolume.weight = 1f;
                _flashTimer = resolveFlashDuration;
            }
            if (sfxSource != null && resolveChimeClip != null)
                sfxSource.PlayOneShot(resolveChimeClip, 0.85f * GameSettingsManager.SfxVolume);
            if (musicBoxSnippetSource != null && musicBoxSnippetSource.clip != null)
                StartCoroutine(PlayMusicBoxResolutionSnippet());

            EndRitualVisualsAndRestoreControl();

            // 3. Atualiza contador & HUD ("Repare as anomalias (k/N)")
            RefreshTargets();
            OnRepairProgressChanged?.Invoke(_repairedCount, _totalTargets);

            if (_totalTargets > 0 && _repairedCount >= _totalTargets && !_act2Completed)
            {
                StartCoroutine(RunAct2CanonicalFinalBeat());
            }
            else
            {
                UpdateRepairObjective(flash: true);
            }
        }

        private IEnumerator PlayMusicBoxResolutionSnippet()
        {
            musicBoxSnippetSource.Stop();
            musicBoxSnippetSource.time = 0f;
            musicBoxSnippetSource.volume = 0.62f * GameSettingsManager.MusicVolume;
            musicBoxSnippetSource.Play();
            yield return new WaitForSeconds(musicBoxSnippetDuration);
            musicBoxSnippetSource.Stop();
        }

        private void EndRitualVisualsAndRestoreControl()
        {
            _isRepairing = false;
            if (channelSource != null) channelSource.Stop();
            if (repairSparksParticles != null) repairSparksParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (dezRepairToolVisual != null) dezRepairToolVisual.SetActive(false);
            if (repairFocusVcam != null) repairFocusVcam.gameObject.SetActive(false);
            if (repairPanelGroup != null) repairPanelGroup.alpha = 0f;
            if (_playerLoco != null && !_isPlayingFinalBeat) _playerLoco.enabled = true;
        }

        private void UpdateRepairObjective(bool flash)
        {
            ObjectiveSystem.Set($"Repare as anomalias ({_repairedCount}/{_totalTargets})");
            if (flash) ObjectiveSystem.Flash();
        }

        /// <summary>
        /// BEAT FINAL CANONICO DO ATO 2:
        ///   1. Pausa de 2.0s de alivio ("Oficina estabilizada...")
        ///   2. Bugiganga da esposa na bancada quebra SOZINHA na frente do player (som seco, SEM musica!)
        ///   3. Todas as luzes da oficina piscam juntas uma vez
        ///   4. Fim do Ato 2 — controle devolvido em estado limpo!
        /// </summary>
        private IEnumerator RunAct2CanonicalFinalBeat()
        {
            _isPlayingFinalBeat = true;
            if (_playerLoco != null) _playerLoco.enabled = false;
            if (anomalyManager != null && anomalyManager.persistentMachineHum != null)
                anomalyManager.persistentMachineHum.Stop();

            ObjectiveSystem.Set($"Anomalias ({_repairedCount}/{_totalTargets}) — Oficina estabilizada");
            ObjectiveSystem.Flash();

            // 1. Pausa de 2s de alivio
            yield return new WaitForSeconds(2.0f);

            // 2. Camera focaliza a bancada -> bugiganga da esposa quebra SOZINHA (som seco, sem musica!)
            if (workbenchTrinketProp != null)
            {
                workbenchTrinketProp.SetActive(true);
                if (repairFocusVcam != null)
                {
                    Vector3 tp = workbenchTrinketProp.transform.position;
                    repairFocusVcam.transform.position = tp + new Vector3(0.55f, 0.36f, -0.68f);
                    repairFocusVcam.transform.LookAt(tp);
                    repairFocusVcam.gameObject.SetActive(true);
                }
                yield return new WaitForSeconds(0.45f);

                if (sfxSource != null && trinketCrackClip != null)
                    sfxSource.PlayOneShot(trinketCrackClip, 0.92f * GameSettingsManager.SfxVolume);

                // Estalo fisico: a bugiganga tomba e treme sozinha na bancada!
                Quaternion startRot = workbenchTrinketProp.transform.rotation;
                Quaternion brokenRot = startRot * Quaternion.Euler(28f, 18f, 52f);
                float t = 0f;
                while (t < 0.22f)
                {
                    t += Time.deltaTime;
                    workbenchTrinketProp.transform.rotation = Quaternion.Slerp(startRot, brokenRot, t / 0.22f);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.75f);

            // 3. Todas as luzes da oficina piscam juntas UMA vez!
            if (ceilingLightsRoot != null)
            {
                ceilingLightsRoot.SetActive(false);
                yield return new WaitForSeconds(0.18f);
                ceilingLightsRoot.SetActive(true);
            }

            yield return new WaitForSeconds(0.85f);

            // 4. Encerra Ato 2 em estado 100% limpo!
            if (repairFocusVcam != null) repairFocusVcam.gameObject.SetActive(false);
            if (_playerLoco != null) _playerLoco.enabled = true;

            _act2Completed = true;
            _isPlayingFinalBeat = false;
            PlayerPrefs.SetInt(Act2CompletedPrefKey, 1);
            PlayerPrefs.Save();

            ObjectiveSystem.Set("Fim do Ato 2 — Algo continua errado na oficina");
            ObjectiveSystem.Flash();
            Debug.Log("[RepairRitual] BEAT FINAL DO ATO 2 CONCLUIDO! Bugiganga quebrou sozinha + luzes piscaram.");
            OnAct2FinalBeatCompleted?.Invoke();
        }
    }
}
