using System;
using System.Collections.Generic;
using Studio.Game.Anomalies;
using Studio.Game.Dialogue;
using Studio.Game.Repair;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Quests
{
    [Serializable]
    public sealed class SignalQuestConfigData
    {
        public string questId = "quest_signal";
        public string title = "Sinal";
        public List<string> feederAnomalyIds = new List<string>();
        public string introDialogueId = "dlg_vera_signal_intro";
        public string resultDialogueId = "dlg_vera_signal_result";
        public string objectiveStep0TalkVera = "Fale com Vera";
        public string objectiveStep1RestoreFeedersPrefix = "Restaure o alimentador do gerador";
        public string objectiveStep2CaptureAntenna = "Capture a leitura da antena";
        public string objectiveStep3ReportVera = "Mostre a leitura para Vera";
        public string objectiveStep4ReachEpicenter = "Chegue ao epicentro";
        public string checkpointStep1 = "cp_signal_step1_feeders";
        public string checkpointStep2 = "cp_signal_step2_antenna_ready";
        public string checkpointStep3 = "cp_signal_step3_reading_captured";
        public string checkpointStep4 = "cp_signal_completed_east_gate";
        public string corruptedTimestampDisplay = "14:02:11.004 — REGISTRO CORROMPIDO";

        public static SignalQuestConfigData FromJson(TextAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return new SignalQuestConfigData();
            return JsonUtility.FromJson<SignalQuestConfigData>(asset.text) ?? new SignalQuestConfigData();
        }
    }

    public enum SignalQuestStep
    {
        NotStarted = 0,
        RestoreFeeders = 1,
        CaptureAntenna = 2,
        ReportToVera = 3,
        Completed = 4,
    }

    /// <summary>
    /// SignalQuestController — Cadeia data-driven da missao "Sinal" (Ato 3 / Setor Externo):
    ///   Step 0 (NotStarted)     : Falar com Engenheira Vera (dlg_vera_signal_intro.json)
    ///   Step 1 (RestoreFeeders) : Restaurar 2 alimentadores do gerador (reusa Anomaly + Repairable) -> Auto-save 1
    ///   Step 2 (CaptureAntenna) : Capturar leitura na antena (AntennaCaptureInteractable)           -> Auto-save 2
    ///   Step 3 (ReportToVera)   : Mostrar registro corrompido para Vera (dlg_vera_signal_result)    -> Auto-save 3
    ///   Step 4 (Completed)      : Portao Leste destrava + objetivo "Chegue ao epicentro"            -> Auto-save final
    /// </summary>
    public sealed class SignalQuestController : MonoBehaviour
    {
        public static SignalQuestController Instance { get; private set; }

        [Header("Dados JSON (Zero Hardcode)")]
        public TextAsset questConfigJson;
        public TextAsset veraIntroDialogueJson;
        public TextAsset veraResultDialogueJson;

        [Header("Referencias de Cena (Reusadas)")]
        public NpcDialogueInteractable veraNpc;
        public WorkshopAnomalyManager anomalyManager;
        public AntennaCaptureInteractable antennaCapture;
        public EastGateInteractable eastGate;

        private SignalQuestConfigData _cfg;
        private SignalQuestStep _step = SignalQuestStep.NotStarted;
        private int _feedersRestored;

        public SignalQuestStep CurrentStep => _step;
        public int CurrentStepInt => (int)_step;
        public int FeedersRestored => _feedersRestored;
        public int FeedersTotal => _cfg != null && _cfg.feederAnomalyIds != null ? _cfg.feederAnomalyIds.Count : 2;
        public SignalQuestConfigData Config => _cfg;

        public event Action<SignalQuestStep> OnStepChanged;

        private void Awake()
        {
            Instance = this;
            _cfg = SignalQuestConfigData.FromJson(questConfigJson);
        }

        private void Start()
        {
            if (DialogueController.Instance != null)
                DialogueController.Instance.OnConversationFinished += HandleConversationFinished;
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.OnRepairProgressChanged += HandleRepairProgressChanged;

            ApplyStepToScene(saveCheckpoint: false, flashObjective: false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (DialogueController.Instance != null)
                DialogueController.Instance.OnConversationFinished -= HandleConversationFinished;
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.OnRepairProgressChanged -= HandleRepairProgressChanged;
        }

        private void HandleConversationFinished(string dialogueId)
        {
            if (_cfg == null || string.IsNullOrEmpty(dialogueId)) return;

            if (_step == SignalQuestStep.NotStarted && dialogueId == _cfg.introDialogueId)
            {
                // Passo 1: Restaurar 2 alimentadores do gerador + Checkpoint/Auto-save 1
                if (anomalyManager == null) anomalyManager = FindAnyObjectByType<WorkshopAnomalyManager>();
                if (anomalyManager != null && !anomalyManager.AreAnomaliesActive)
                    anomalyManager.ActivateAllAnomalies(persistFlag: false);

                RecountFeeders();
                SetStep(SignalQuestStep.RestoreFeeders, saveCheckpoint: true, flashObjective: true);
            }
            else if (_step == SignalQuestStep.ReportToVera && dialogueId == _cfg.resultDialogueId)
            {
                // Passo 4: Vera interpreta como registro corrompido -> destrava Portao Leste + "Chegue ao epicentro"!
                EastGateInteractable.IsEastGateUnlocked = true;
                if (eastGate != null) eastGate.RefreshGateVisuals(animateOpen: true);

                SetStep(SignalQuestStep.Completed, saveCheckpoint: true, flashObjective: true);
            }
        }

        private void HandleRepairProgressChanged(int repaired, int total)
        {
            if (_step != SignalQuestStep.RestoreFeeders) return;

            RecountFeeders();
            if (_feedersRestored >= FeedersTotal)
            {
                // Passo 2: Alimentadores restaurados -> habilita captura na antena + Checkpoint/Auto-save 2!
                SetStep(SignalQuestStep.CaptureAntenna, saveCheckpoint: true, flashObjective: true);
            }
            else
            {
                UpdateObjectiveText(flash: true);
            }
        }

        public void NotifyAntennaReadingCaptured()
        {
            if (_step != SignalQuestStep.CaptureAntenna) return;

            // Passo 3: Leitura corrompida obtida -> retornar para Vera + Checkpoint/Auto-save 3!
            SetStep(SignalQuestStep.ReportToVera, saveCheckpoint: true, flashObjective: true);
        }

        public void RestoreFromSave(int savedStep, bool eastGateUnlocked)
        {
            if (_cfg == null) _cfg = SignalQuestConfigData.FromJson(questConfigJson);

            int clamped = Mathf.Clamp(savedStep, 0, (int)SignalQuestStep.Completed);
            _step = (SignalQuestStep)clamped;

            if (eastGateUnlocked || _step == SignalQuestStep.Completed)
                EastGateInteractable.IsEastGateUnlocked = true;

            RecountFeeders();
            ApplyStepToScene(saveCheckpoint: false, flashObjective: false);
            Debug.Log($"[SignalQuest] Restaurado do save no passo {_step} (feeders={_feedersRestored}/{FeedersTotal}, eastGate={EastGateInteractable.IsEastGateUnlocked}).");
        }

        private void SetStep(SignalQuestStep next, bool saveCheckpoint, bool flashObjective)
        {
            _step = next;
            ApplyStepToScene(saveCheckpoint, flashObjective);
            OnStepChanged?.Invoke(_step);
        }

        private void ApplyStepToScene(bool saveCheckpoint, bool flashObjective)
        {
            // 1. Seleciona o dialogo data-driven correto da Vera conforme o passo da missao
            if (veraNpc != null)
            {
                if (_step >= SignalQuestStep.ReportToVera && veraResultDialogueJson != null)
                    veraNpc.dialogueJson = veraResultDialogueJson;
                else if (veraIntroDialogueJson != null)
                    veraNpc.dialogueJson = veraIntroDialogueJson;
            }

            // 2. Habilita captura da antena exatamente no passo CaptureAntenna
            if (antennaCapture != null)
                antennaCapture.SetCaptureAvailable(_step == SignalQuestStep.CaptureAntenna);

            // 3. Atualiza Portao Leste
            if (eastGate != null)
                eastGate.RefreshGateVisuals(animateOpen: _step == SignalQuestStep.Completed);

            // 4. Atualiza objetivo no HUD (so sobrescreve quando a missao ja esta ativa ou em andamento)
            if (_step != SignalQuestStep.NotStarted || string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
                UpdateObjectiveText(flashObjective);

            // 5. Auto-save no SaveSystem nos passos-chave da cadeia!
            if (saveCheckpoint && _cfg != null)
            {
                string cp = _step switch
                {
                    SignalQuestStep.RestoreFeeders => _cfg.checkpointStep1,
                    SignalQuestStep.CaptureAntenna => _cfg.checkpointStep2,
                    SignalQuestStep.ReportToVera   => _cfg.checkpointStep3,
                    SignalQuestStep.Completed      => _cfg.checkpointStep4,
                    _ => null,
                };
                if (!string.IsNullOrEmpty(cp))
                    SaveSystem.Save(cp);
            }
        }

        private void UpdateObjectiveText(bool flash)
        {
            if (_cfg == null) return;
            string text = _step switch
            {
                SignalQuestStep.NotStarted     => _cfg.objectiveStep0TalkVera,
                SignalQuestStep.RestoreFeeders => $"{_cfg.objectiveStep1RestoreFeedersPrefix} ({_feedersRestored}/{FeedersTotal})",
                SignalQuestStep.CaptureAntenna => _cfg.objectiveStep2CaptureAntenna,
                SignalQuestStep.ReportToVera   => _cfg.objectiveStep3ReportVera,
                SignalQuestStep.Completed      => _cfg.objectiveStep4ReachEpicenter,
                _ => _cfg.objectiveStep0TalkVera,
            };
            ObjectiveSystem.Set(text);
            if (flash) ObjectiveSystem.Flash();
        }

        private void RecountFeeders()
        {
            _feedersRestored = 0;
            if (anomalyManager == null) anomalyManager = FindAnyObjectByType<WorkshopAnomalyManager>();
            if (anomalyManager == null || _cfg == null || _cfg.feederAnomalyIds == null) return;

            var all = anomalyManager.GetAllAnomalies();
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a != null && _cfg.feederAnomalyIds.Contains(a.anomalyId) && !a.IsAnomalous)
                    _feedersRestored++;
            }
        }
    }
}
