using Studio.Game.Dialogue;
using Studio.Gameplay;
using UnityEngine;

namespace Studio.Game.Quests
{
    /// <summary>
    /// EastGateInteractable — Portao Leste do Setor Externo rumo ao Epicentro:
    ///   - Inicia trancado (luz vermelha); destrava ao concluir a Missao "Sinal" via flag CONCERTO_EAST_GATE_UNLOCKED
    ///   - Quando destravado, desliza as folhas metalicas abrindo passagem e exibe prompt do proximo ato
    /// </summary>
    public sealed class EastGateInteractable : MonoBehaviour, IInteractable
    {
        public const string EastGateUnlockedPrefKey = "CONCERTO_EAST_GATE_UNLOCKED";

        [Header("Visuais do Portao Leste")]
        public Transform leftDoorLeaf;
        public Transform rightDoorLeaf;
        public Renderer statusLightRenderer;
        public Color lockedColor = new Color(0.95f, 0.20f, 0.16f, 1f);
        public Color unlockedColor = new Color(0.26f, 0.96f, 1.0f, 1f);
        public KeyCode debugToggleKey = KeyCode.F9;

        private Vector3 _leftClosedPos;
        private Vector3 _rightClosedPos;
        private bool _cachedClosed;
        private float _openAmount;

        public static bool IsEastGateUnlocked
        {
            get => PlayerPrefs.GetInt(EastGateUnlockedPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(EastGateUnlockedPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public string PromptText => IsEastGateUnlocked
            ? "[E] Portão Leste Destravado — Rumo ao Epicentro"
            : "[E] Portão Leste (Trancado — Aguardando leitura da antena)";

        public Transform IkHandTarget => transform;

        private void Awake()
        {
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
            CacheClosedPositions();
            RefreshGateVisuals(animateOpen: false);
        }

        private void CacheClosedPositions()
        {
            if (_cachedClosed) return;
            _cachedClosed = true;
            if (leftDoorLeaf != null) _leftClosedPos = leftDoorLeaf.localPosition;
            if (rightDoorLeaf != null) _rightClosedPos = rightDoorLeaf.localPosition;
        }

        private void Update()
        {
            if (Input.GetKeyDown(debugToggleKey))
            {
                IsEastGateUnlocked = !IsEastGateUnlocked;
                RefreshGateVisuals(animateOpen: IsEastGateUnlocked);
            }

            float target = IsEastGateUnlocked ? 1f : 0f;
            if (!Mathf.Approximately(_openAmount, target))
            {
                _openAmount = Mathf.MoveTowards(_openAmount, target, Time.deltaTime * 0.9f);
                ApplyLeafOffset(_openAmount);
            }
        }

        public void RefreshGateVisuals(bool animateOpen)
        {
            CacheClosedPositions();
            bool unlocked = IsEastGateUnlocked;
            if (statusLightRenderer != null && statusLightRenderer.sharedMaterial != null)
            {
                var mat = statusLightRenderer.sharedMaterial;
                Color c = unlocked ? unlockedColor : lockedColor;
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", c * 2.2f);
            }

            if (!animateOpen)
            {
                _openAmount = unlocked ? 1f : 0f;
                ApplyLeafOffset(_openAmount);
            }
        }

        private void ApplyLeafOffset(float t)
        {
            if (leftDoorLeaf != null) leftDoorLeaf.localPosition = _leftClosedPos + new Vector3(0f, 0f, -t * 2.4f);
            if (rightDoorLeaf != null) rightDoorLeaf.localPosition = _rightClosedPos + new Vector3(0f, 0f, t * 2.4f);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!IsEastGateUnlocked)
            {
                Debug.Log("[EastGate] Portao Leste trancado! Conclua a missao 'Sinal' com a Vera.");
                return;
            }
            Debug.Log("[EastGate] Portao Leste aberto — caminho para o Epicentro liberado!");
        }
    }
}
