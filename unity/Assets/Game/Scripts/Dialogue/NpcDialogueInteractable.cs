using Studio.Gameplay;
using UnityEngine;

namespace Studio.Game.Dialogue
{
    /// <summary>
    /// Componente de NPC interagivel 100% data-driven:
    /// basta atribuir qualquer arquivo JSON de conversa (dialogueJson) no Inspector — ZERO codigo por conversa!
    /// </summary>
    public sealed class NpcDialogueInteractable : MonoBehaviour, IInteractable
    {
        [Header("Dados da Conversa (JSON)")]
        public TextAsset dialogueJson;
        public Transform lookAnchor;

        private InteractionHighlight _highlight;

        public string PromptKey
        {
            get
            {
                var data = DialogueConversationData.FromJson(dialogueJson);
                return data != null && !string.IsNullOrEmpty(data.promptKey) ? data.promptKey : "prompt_talk";
            }
        }

        public string PromptText => DialogueController.Instance != null
            ? DialogueController.Instance.Localize(PromptKey)
            : PromptKey;

        public Transform IkHandTarget => lookAnchor != null ? lookAnchor : transform;

        private void Awake()
        {
            _highlight = GetComponent<InteractionHighlight>();
            if (_highlight == null) _highlight = gameObject.AddComponent<InteractionHighlight>();
        }

        public void SetFocused(bool focused)
        {
            if (_highlight != null) _highlight.SetHighlighted(focused);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (dialogueJson == null) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive)
                return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (DialogueController.Instance != null)
            {
                DialogueController.Instance.StartConversation(this, dialogueJson);
            }
        }
    }
}
