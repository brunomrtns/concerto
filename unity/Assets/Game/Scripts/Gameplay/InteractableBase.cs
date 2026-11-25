using System;
using UnityEngine;

namespace Studio.Gameplay
{
    public class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "Interagir";
        [SerializeField] private bool singleUse = false;
        [SerializeField] private Transform ikHandTarget;

        private bool _used;

        public event Action<GameObject> OnInteracted;

        public string PromptText => promptText;
        public Transform IkHandTarget => ikHandTarget != null ? ikHandTarget : transform;

        public virtual bool CanInteract(GameObject interactor)
        {
            return !(singleUse && _used);
        }

        public virtual void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            _used = true;
            OnInteracted?.Invoke(interactor);
        }
    }
}
