using System;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Contrato de interacao do protagonista (Auditoria P1-8 — Studio V2 / CONCERTO).
    /// Suporta prompt contextual e ancora IK opcional para a mao alcancar alavancas/molas.
    /// </summary>
    public interface IInteractable
    {
        string PromptText { get; }
        bool CanInteract(GameObject interactor);
        Transform IkHandTarget { get; }
        void Interact(GameObject interactor);
    }

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

    /// <summary>
    /// Detector de proximidade para o jogador interagir com objetos IInteractable.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float interactRadius = 2.4f;
        public LayerMask interactMask = ~0;

        public IInteractable FocusedTarget { get; private set; }
        public event Action<IInteractable> OnFocusChanged;

        private readonly Collider[] _interactBuf = new Collider[24];

        public IInteractable ScanNearest()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, _interactBuf, interactMask);
            IInteractable best = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _interactBuf[i];
                if (col == null) continue;
                var cand = col.GetComponentInParent<IInteractable>();
                if (cand == null || !cand.CanInteract(gameObject)) continue;
                float dSq = (col.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = cand;
                }
            }

            if (!ReferenceEquals(best, FocusedTarget))
            {
                FocusedTarget = best;
                OnFocusChanged?.Invoke(FocusedTarget);
            }
            return FocusedTarget;
        }

        public bool TryInteractFocused()
        {
            if (FocusedTarget == null || !FocusedTarget.CanInteract(gameObject))
                return false;
            FocusedTarget.Interact(gameObject);
            return true;
        }

        private void Update()
        {
            ScanNearest();
        }
    }
}
