using Studio.Game.Dialogue;
using Studio.Gameplay;
using UnityEngine;

namespace Studio.Game.Parts
{
    /// <summary>
    /// Peca mecanica coletavel (reusa IInteractable do sistema de interacao existente).
    /// Brilho + rotacao sutil + bobbing; ao coletar notifica PartsLoopManager e se oculta.
    /// </summary>
    public sealed class MachinePartPickup : MonoBehaviour, IInteractable
    {
        [Header("Identidade da Peca")]
        public string partId;
        public string displayName;
        public int slotIndex;

        [Header("Feedback Visual Sutil")]
        public float rotateSpeedDeg = 55f;
        public float bobAmplitude = 0.06f;
        public float bobSpeed = 2.4f;

        private Vector3 _basePos;
        private float _phase;
        private bool _collected;

        public bool IsCollected => _collected;

        public string PromptText => DialogueController.Instance != null
            ? DialogueController.Instance.Localize("prompt_pickup")
            : "[E] Pegar peça";

        public Transform IkHandTarget => transform;

        private void Awake()
        {
            _basePos = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
        }

        private void Update()
        {
            if (_collected) return;
            transform.Rotate(0f, rotateSpeedDeg * Time.deltaTime, 0f, Space.World);
            float yOff = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, _basePos.y + yOff, _basePos.z);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_collected || !gameObject.activeInHierarchy) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive)
                return false;
            return PartsLoopManager.Instance == null || PartsLoopManager.Instance.CanCollectParts;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            _collected = true;
            if (Studio.Game.Combat.GameFeelJuiceController.Instance != null)
                Studio.Game.Combat.GameFeelJuiceController.Instance.TriggerItemPickupJuice(transform.position, gameObject.name);
            if (EconomyUpgradeSystem.Instance != null)
                EconomyUpgradeSystem.Instance.NotifyPartEarned(1);
            if (PartsLoopManager.Instance != null)
                PartsLoopManager.Instance.NotifyPartCollected(this);
            gameObject.SetActive(false);
        }

        public void ResetForReplay()
        {
            _collected = false;
            transform.position = _basePos;
            gameObject.SetActive(true);
        }

        public void MarkCollectedSilent()
        {
            _collected = true;
            gameObject.SetActive(false);
        }
    }
}
