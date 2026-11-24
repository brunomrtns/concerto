using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Parts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Controlador de Pause in-game (Esc) na oficina Caldeiral.unity:
    ///   - Pausa de verdade: Time.timeScale = 0, AudioListener.pause = true, DezLocomotionController desabilitado
    ///   - BLOQUEADO durante Cutscene 1, Cutscene 2 e Dialogo (evita conflito com Skip [Esc] e trava de dialogo!)
    ///   - Botoes: Continuar / Opcoes (mesmo OptionsPanelUI) / Voltar ao Menu (com confirmacao se houver progresso)
    ///   - Restaura estado salvo quando o jogador entra via "Continuar" no Menu Principal
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Paineis")]
        public GameObject pauseRoot;
        public CanvasGroup pauseButtonsGroup;
        public OptionsPanelUI optionsPanel;
        public GameObject confirmMenuModal;

        [Header("Botoes de Pause")]
        public Button resumeButton;
        public Button optionsButton;
        public Button returnToMenuButton;
        public Button confirmReturnYesButton;
        public Button confirmReturnNoButton;

        public bool IsPaused { get; private set; }
        public bool IsConfirmModalOpen => confirmMenuModal != null && confirmMenuModal.activeSelf;

        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private DezLocomotionController _playerLoco;
        private bool _locoWasEnabledBeforePause;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSettingsManager.LoadAndApply();

            if (resumeButton != null)           resumeButton.onClick.AddListener(ResumeGame);
            if (optionsButton != null)          optionsButton.onClick.AddListener(OpenOptions);
            if (returnToMenuButton != null)     returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            if (confirmReturnYesButton != null) confirmReturnYesButton.onClick.AddListener(ConfirmReturnToMenu);
            if (confirmReturnNoButton != null)  confirmReturnNoButton.onClick.AddListener(CancelReturnToMenu);

            if (pauseRoot != null) pauseRoot.SetActive(false);
            if (confirmMenuModal != null) confirmMenuModal.SetActive(false);
            if (optionsPanel != null)
            {
                optionsPanel.gameObject.SetActive(false);
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private void OnDestroy()
        {
            if (optionsPanel != null) optionsPanel.OnClosed -= HandleOptionsClosed;
            if (PartsLoopManager.Instance != null)
            {
                PartsLoopManager.Instance.OnPartCountChanged -= HandleProgressChanged;
                PartsLoopManager.Instance.OnAct1Completed -= HandleAct1Completed;
            }
            if (_cs01 != null) _cs01.OnCutsceneEnded -= HandleCutsceneEndedSave;

            // Garante que nunca saia da cena deixando o tempo pausado
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Start()
        {
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null) _playerLoco = dez.GetComponent<DezLocomotionController>();

            if (PartsLoopManager.Instance != null)
            {
                PartsLoopManager.Instance.OnPartCountChanged += HandleProgressChanged;
                PartsLoopManager.Instance.OnAct1Completed += HandleAct1Completed;
            }
            if (_cs01 != null)
                _cs01.OnCutsceneEnded += HandleCutsceneEndedSave;

            // Se entrou via "Continuar" no Menu Principal, restaura pecas + objetivo salvo!
            GameSaveState.TryRestoreOnWorkshopBoot();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (IsPaused)
            {
                if (optionsPanel != null && optionsPanel.gameObject.activeSelf)
                {
                    optionsPanel.Close();
                    return;
                }
                if (IsConfirmModalOpen)
                {
                    CancelReturnToMenu();
                    return;
                }
                ResumeGame();
                return;
            }

            if (CanOpenPauseNow())
                PauseGame();
            else if (AudioMixController.Instance != null)
                AudioMixController.Instance.PlayUiDeny();
        }

        /// <summary>
        /// Pause bloqueado durante cutscene e dialogo (decisao documentada em MENU_FLOW.md).
        /// </summary>
        public bool CanOpenPauseNow()
        {
            if (_cs01 != null && _cs01.IsPlaying) return false;
            if (_cs02 != null && _cs02.IsPlaying) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            return true;
        }

        public bool TryPause()
        {
            if (IsPaused || !CanOpenPauseNow()) return false;
            PauseGame();
            return true;
        }

        public void PauseGame()
        {
            if (IsPaused) return;
            IsPaused = true;

            GameSaveState.SaveCurrentWorkshopState();

            if (_playerLoco == null)
            {
                var dez = GameObject.Find("Player_Dez_Placeholder");
                if (dez != null) _playerLoco = dez.GetComponent<DezLocomotionController>();
            }
            _locoWasEnabledBeforePause = _playerLoco != null && _playerLoco.enabled;
            if (_playerLoco != null) _playerLoco.enabled = false;

            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (pauseRoot != null) pauseRoot.SetActive(true);
            if (pauseButtonsGroup != null) pauseButtonsGroup.interactable = true;
            if (confirmMenuModal != null) confirmMenuModal.SetActive(false);
            if (optionsPanel != null) optionsPanel.gameObject.SetActive(false);
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiOpen();
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiClose();
            IsPaused = false;

            if (optionsPanel != null) optionsPanel.gameObject.SetActive(false);
            if (confirmMenuModal != null) confirmMenuModal.SetActive(false);
            if (pauseRoot != null) pauseRoot.SetActive(false);

            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_playerLoco != null && _locoWasEnabledBeforePause)
                _playerLoco.enabled = true;
        }

        public void OpenOptions()
        {
            if (!IsPaused || optionsPanel == null) return;
            if (pauseButtonsGroup != null) pauseButtonsGroup.interactable = false;
            optionsPanel.Open();
        }

        private void HandleOptionsClosed()
        {
            if (pauseButtonsGroup != null) pauseButtonsGroup.interactable = true;
        }

        public void OnReturnToMenuClicked()
        {
            if (!IsPaused) return;
            GameSaveState.SaveCurrentWorkshopState();

            if (GameSaveState.HasActiveProgress() && confirmMenuModal != null)
            {
                if (pauseButtonsGroup != null) pauseButtonsGroup.interactable = false;
                confirmMenuModal.SetActive(true);
                if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiOpen();
            }
            else
            {
                ConfirmReturnToMenu();
            }
        }

        public void CancelReturnToMenu()
        {
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiClose();
            if (confirmMenuModal != null) confirmMenuModal.SetActive(false);
            if (pauseButtonsGroup != null) pauseButtonsGroup.interactable = true;
        }

        public void ConfirmReturnToMenu()
        {
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
            GameSaveState.SaveCurrentWorkshopState();
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        private void HandleProgressChanged(int collected, int required) => GameSaveState.SaveCurrentWorkshopState();
        private void HandleAct1Completed() => GameSaveState.SaveCurrentWorkshopState();
        private void HandleCutsceneEndedSave() => GameSaveState.SaveCurrentWorkshopState();
    }
}
