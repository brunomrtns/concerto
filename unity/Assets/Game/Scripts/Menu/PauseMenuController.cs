using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Combat;
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

        public static PauseMenuController Instance { get; private set; }

        public bool IsPaused { get; private set; }
        public int PauseCycleCount { get; private set; }
        public bool IsConfirmModalOpen => confirmMenuModal != null && confirmMenuModal.activeSelf;
        public int FocusedPauseIndex => _pauseFocusIndex;
        public string FocusedPauseLabel => _pauseFocusIndex == 0 ? "Continuar" : (_pauseFocusIndex == 1 ? "Opcoes" : "Menu Principal");

        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;
        private Cutscene04SkyCrackedController _cs04;
        private DezLocomotionController _playerLoco;
        private bool _locoWasEnabledBeforePause;
        private int _pauseFocusIndex;
        private float _navCooldownUntil;
        private float _lastPauseSaveUnscaledTime = -999f;

        private void Awake()
        {
            Instance = this;
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
            if (Instance == this) Instance = null;
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
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            _cs04 = FindAnyObjectByType<Cutscene04SkyCrackedController>();
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
            // GATE 1: Abrir/fechar Pause por teclado (Esc / tecla remapeada) OU controle (Start = JoystickButton7)
            KeyCode pauseKey = GameSettingsManager.GetActionKey("Pause");
            bool pausePressed = Input.GetKeyDown(pauseKey) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
            bool backPressed  = Input.GetKeyDown(KeyCode.JoystickButton1); // Gamepad B / Circulo

            bool navDown = false;
            bool navUp = false;
            if (IsPaused && Time.unscaledTime >= _navCooldownUntil)
            {
                float vAxis = Input.GetAxisRaw("Vertical");
                navDown = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || vAxis < -0.55f;
                navUp   = !navDown && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || vAxis > 0.55f);
            }

            bool confirmPressed = IsPaused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0));
            ProcessInputFrame(pausePressed, backPressed, navDown, navUp, confirmPressed, applyNavCooldown: true);
        }

        /// <summary>
        /// BL-005: Entrada simulada (Playcaller / Smoke Tests) independente de foco da Game View
        /// e totalmente funcional com Time.timeScale = 0 (usa Time.unscaledTime).
        /// </summary>
        public bool SimulateKeyPress(KeyCode key)
        {
            KeyCode pauseKey = GameSettingsManager.GetActionKey("Pause");
            bool pausePressed   = key == KeyCode.Escape || key == pauseKey || key == KeyCode.JoystickButton7;
            bool backPressed    = key == KeyCode.JoystickButton1;
            bool navDown        = IsPaused && (key == KeyCode.DownArrow || key == KeyCode.S);
            bool navUp          = IsPaused && (key == KeyCode.UpArrow || key == KeyCode.W);
            bool confirmPressed = IsPaused && (key == KeyCode.Return || key == KeyCode.Space || key == KeyCode.JoystickButton0);
            return ProcessInputFrame(pausePressed, backPressed, navDown, navUp, confirmPressed, applyNavCooldown: false);
        }

        private bool ProcessInputFrame(bool pausePressed, bool backPressed, bool navDown, bool navUp, bool confirmPressed, bool applyNavCooldown)
        {
            if (IsPaused)
            {
                if (optionsPanel != null && optionsPanel.gameObject.activeSelf)
                {
                    if (pausePressed || backPressed)
                    {
                        optionsPanel.Close();
                        return true;
                    }
                    return false;
                }

                if (pausePressed || backPressed)
                {
                    BackPauseNavigation();
                    return true;
                }

                if (navDown)
                {
                    NavigatePause(+1);
                    if (applyNavCooldown) _navCooldownUntil = Time.unscaledTime + 0.18f;
                    return true;
                }
                if (navUp)
                {
                    NavigatePause(-1);
                    if (applyNavCooldown) _navCooldownUntil = Time.unscaledTime + 0.18f;
                    return true;
                }

                if (confirmPressed)
                {
                    ConfirmPauseSelection();
                    return true;
                }
                return false;
            }

            if (!pausePressed) return false;

            if (CanOpenPauseNow())
            {
                PauseGame();
                return true;
            }

            if (AudioMixController.Instance != null)
                AudioMixController.Instance.PlayUiDeny();
            return false;
        }

        public void NavigatePause(int delta)
        {
            if (!IsPaused) return;
            if (IsConfirmModalOpen)
            {
                _pauseFocusIndex = (_pauseFocusIndex == 0) ? 1 : 0;
                SelectButtonVisual(_pauseFocusIndex == 0 ? confirmReturnNoButton : confirmReturnYesButton);
                return;
            }
            _pauseFocusIndex = (_pauseFocusIndex + delta + 3) % 3;
            ApplyPauseFocusVisual();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        public void ConfirmPauseSelection()
        {
            if (!IsPaused) return;
            if (IsConfirmModalOpen)
            {
                if (_pauseFocusIndex == 1) ConfirmReturnToMenu();
                else CancelReturnToMenu();
                return;
            }
            if (_pauseFocusIndex == 0) ResumeGame();
            else if (_pauseFocusIndex == 1) OpenOptions();
            else OnReturnToMenuClicked();
        }

        public void BackPauseNavigation()
        {
            if (!IsPaused) return;
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
        }

        private void ApplyPauseFocusVisual()
        {
            Button[] btns = { resumeButton, optionsButton, returnToMenuButton };
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                var img = btns[i].targetGraphic as Image;
                if (img != null)
                    img.color = (i == _pauseFocusIndex) ? new Color(0.22f, 0.78f, 1.0f, 1f) : new Color(0.16f, 0.22f, 0.30f, 0.92f);
            }
            if (_pauseFocusIndex >= 0 && _pauseFocusIndex < btns.Length)
                SelectButtonVisual(btns[_pauseFocusIndex]);
        }

        private static void SelectButtonVisual(Button btn)
        {
            if (btn != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btn.gameObject);
        }

        /// <summary>
        /// Pause bloqueado durante cutscene e dialogo (decisao documentada em MENU_FLOW.md).
        /// </summary>
        public bool CanOpenPauseNow()
        {
            if (_cs01 != null && _cs01.IsPlaying) return false;
            if (_cs02 != null && _cs02.IsPlaying) return false;
            if (_cs03 != null && _cs03.IsPlaying) return false;
            if (_cs04 != null && _cs04.IsPlaying) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return false;
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

            if (Time.unscaledTime - _lastPauseSaveUnscaledTime >= 2.0f)
            {
                GameSaveState.SaveCurrentWorkshopState();
                _lastPauseSaveUnscaledTime = Time.unscaledTime;
            }

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
            _pauseFocusIndex = 0;
            ApplyPauseFocusVisual();
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
            PauseCycleCount++;
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
