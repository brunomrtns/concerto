using Studio.Game.Dialogue;
using Studio.Game.Save;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Sector
{
    /// <summary>
    /// Porta de conexao bidirecional Oficina (Caldeiral) <-> Setor Externo (SetorExterno):
    ///   - Trava/destrava pela flag CONCERTO_SECTOR_EXTERNO_UNLOCKED (atalho de debug F6; Cutscene 4 destrava no Ato 3)
    ///   - Reusa IInteractable + InteractionHighlight + SaveSystem.Save() antes da transicao
    /// </summary>
    public sealed class SectorDoorInteractable : MonoBehaviour, IInteractable
    {
        public const string SectorUnlockedPrefKey = "CONCERTO_SECTOR_EXTERNO_UNLOCKED";

        [Header("Destino & Trava")]
        public string targetSceneName = "SetorExterno";
        public bool requiresUnlockFlag = true;
        public KeyCode debugToggleUnlockKey = KeyCode.F6;

        [Header("Feedback Visual da Trava (Opcional)")]
        public Renderer statusLightRenderer;
        public Color lockedColor = new Color(0.95f, 0.22f, 0.18f, 1f);
        public Color unlockedColor = new Color(0.28f, 0.94f, 1.0f, 1f);

        public static bool IsSectorUnlocked
        {
            get => PlayerPrefs.GetInt(SectorUnlockedPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SectorUnlockedPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool IsUnlockedNow => !requiresUnlockFlag || IsSectorUnlocked;

        public string PromptText => IsUnlockedNow
            ? (targetSceneName == "SetorExterno" ? "[E] Sair para o Setor Externo" : "[E] Voltar para a Oficina")
            : "[E] Porta do Setor Externo (Trancada — F6 debug)";

        public Transform IkHandTarget => transform;

        private void Awake()
        {
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
            RefreshStatusLight();
        }

        private void Update()
        {
            if (requiresUnlockFlag && Input.GetKeyDown(debugToggleUnlockKey))
            {
                IsSectorUnlocked = !IsSectorUnlocked;
                RefreshStatusLight();
                Debug.Log($"[SectorDoor] Flag {SectorUnlockedPrefKey} = {IsSectorUnlocked}");
            }
        }

        public void RefreshStatusLight()
        {
            if (statusLightRenderer == null) return;
            var mat = statusLightRenderer.sharedMaterial;
            if (mat != null)
            {
                Color c = IsUnlockedNow ? unlockedColor : lockedColor;
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", c * 2.0f);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive)
                return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!IsUnlockedNow)
            {
                Debug.Log("[SectorDoor] Porta trancada! Destrave via Cutscene 4 ou flag F6.");
                return;
            }

            SaveSystem.Save($"cp_porta_{targetSceneName}");
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }
    }
}
