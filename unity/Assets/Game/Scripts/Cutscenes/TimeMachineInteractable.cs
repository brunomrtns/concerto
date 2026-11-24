using Studio.Game.Dialogue;
using Studio.Game.Parts;
using Studio.Gameplay;
using UnityEngine;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Ponte curta de gameplay (PASSO 2):
    /// Apos entregar as pecas para a Juno (Act1Completed == true), permite interagir com a maquina
    /// ("[E] Ativar maquina") reusando IInteractable e disparando a Cutscene 2 ("A Viagem")!
    /// </summary>
    public sealed class TimeMachineInteractable : MonoBehaviour, IInteractable
    {
        public Cutscene02TravelController cutscene02;

        public string PromptText => DialogueController.Instance != null
            ? DialogueController.Instance.Localize("prompt_activate_machine")
            : "[E] Ativar máquina";

        public Transform IkHandTarget => transform;

        private void Awake()
        {
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (cutscene02 == null || cutscene02.IsPlaying) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive)
                return false;
            return PartsLoopManager.Instance != null && PartsLoopManager.Instance.Act1Completed;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            cutscene02.PlayCutscene();
        }
    }
}
