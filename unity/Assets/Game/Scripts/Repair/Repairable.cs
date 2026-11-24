using System;
using Studio.Game.Anomalies;
using Studio.Game.Dialogue;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Studio.Game.Repair
{
    /// <summary>
    /// Componente generico Repairable:
    ///   - Pode ser anexado a qualquer Anomaly existente OU a qualquer objeto futuro (maquinas, portas, NPCs)
    ///   - Reusa IInteractable + InteractionHighlight (sem duplicar sistema de interacao!)
    ///   - Quando vinculado a uma Anomaly, so fica reparavel quando Anomaly.IsAnomalous == true (apos Cutscene 3!)
    /// </summary>
    public sealed class Repairable : MonoBehaviour, IInteractable, IRepairable
    {
        [Header("Identidade & Config")]
        public string repairableId;
        public string displayName = "Anomalia";
        public float repairDurationSec = 4.5f;
        public Transform customFocusPoint;

        [Header("Vinculo Opcional com Anomaly")]
        public Anomaly linkedAnomaly;

        [Header("Evento no Inspector (Extensibilidade sem Codigo)")]
        public UnityEvent onRepairedUnityEvent;

        private bool _standaloneRepaired;

        public string RepairableId => string.IsNullOrEmpty(repairableId)
            ? (linkedAnomaly != null ? linkedAnomaly.anomalyId : gameObject.name)
            : repairableId;

        public string DisplayName => !string.IsNullOrEmpty(displayName)
            ? displayName
            : (linkedAnomaly != null ? linkedAnomaly.displayName : gameObject.name);

        public float RepairDurationSec => Mathf.Max(1.0f, repairDurationSec);
        public Transform FocusPoint => customFocusPoint != null ? customFocusPoint : transform;

        public bool IsRepaired => linkedAnomaly != null ? !linkedAnomaly.IsAnomalous : _standaloneRepaired;

        public bool CanBeRepairedNow
        {
            get
            {
                if (!gameObject.activeInHierarchy) return false;
                if (linkedAnomaly != null) return linkedAnomaly.IsAnomalous;
                return !_standaloneRepaired;
            }
        }

        public string PromptText => $"[E] Consertar — {DisplayName}";
        public Transform IkHandTarget => FocusPoint;

        public event Action<IRepairable> OnRepaired;

        private void Awake()
        {
            if (linkedAnomaly == null) linkedAnomaly = GetComponent<Anomaly>();
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!CanBeRepairedNow) return false;
            if (Studio.Game.Tool.MultiToolController.Instance != null && !Studio.Game.Tool.MultiToolController.Instance.IsRepairModeActive)
                return false;
            if (RepairRitualController.Instance != null && RepairRitualController.Instance.IsRepairing)
                return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive)
                return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.BeginRepair(this);
        }

        public void OnRepairCompleted()
        {
            if (linkedAnomaly != null)
                linkedAnomaly.Resolve();
            else
                _standaloneRepaired = true;

            onRepairedUnityEvent?.Invoke();
            OnRepaired?.Invoke(this);
        }
    }
}
