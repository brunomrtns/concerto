using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Controlador de Boot (logo TEMP) + Menu Principal de CONCERTO:
    ///   - Boot: logo simples TEMP com fade suave -> entra no Menu Principal
    ///   - Menu Principal: motif da caixinha tocando baixinho + foto da bancada como identidade visual
    ///   - Acoes: Continuar (se houver save) / Novo Jogo / Opcoes (painel compartilhado) / Sair
    /// </summary>
    public sealed class BootAndMainMenuController : MonoBehaviour
    {
        [Header("Boot Splash (TEMP)")]
        public CanvasGroup bootSplashGroup;
        public float bootFadeInSec = 0.6f;
        public float bootHoldSec = 1.6f;
        public float bootFadeOutSec = 0.6f;

        [Header("Menu Principal")]
        public GameObject mainMenuRoot;
        public CanvasGroup mainMenuGroup;
        public Button continueButton;
        public Button newGameButton;
        public Button optionsButton;
        public Button quitButton;

        [Header("Painel de Opcoes Compartilhado")]
        public OptionsPanelUI optionsPanel;

        [Header("Audio (Motif Caixinha CS01)")]
        public AudioSource menuMusicSource;
        public float baseMusicVolume = 0.42f;

        public bool IsInBootSplash { get; private set; }
        public bool IsInMainMenu { get; private set; }
        public bool IsOptionsOpen => optionsPanel != null && optionsPanel.gameObject.activeSelf;

        private Coroutine _bootRoutine;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameSettingsManager.LoadAndApply();
            GameSettingsManager.OnSettingsChanged += ApplyMenuMusicVolume;

            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (newGameButton != null)  newGameButton.onClick.AddListener(OnNewGameClicked);
            if (optionsButton != null)  optionsButton.onClick.AddListener(OnOptionsClicked);
            if (quitButton != null)     quitButton.onClick.AddListener(OnQuitClicked);
            if (optionsPanel != null)
            {
                optionsPanel.gameObject.SetActive(false);
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private void OnDestroy()
        {
            GameSettingsManager.OnSettingsChanged -= ApplyMenuMusicVolume;
            if (optionsPanel != null) optionsPanel.OnClosed -= HandleOptionsClosed;
        }

        private void Start()
        {
            RefreshContinueButton();
            ApplyMenuMusicVolume();
            _bootRoutine = StartCoroutine(RunBootToMenuFlow());
        }

        private void Update()
        {
            if (IsInBootSplash && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                SkipBootSplash();
            }
            else if (IsOptionsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                optionsPanel.Close();
            }
        }

        public void SkipBootSplash()
        {
            if (!IsInBootSplash) return;
            if (_bootRoutine != null) { StopCoroutine(_bootRoutine); _bootRoutine = null; }
            EnterMainMenuImmediate();
        }

        private IEnumerator RunBootToMenuFlow()
        {
            IsInBootSplash = true;
            IsInMainMenu = false;

            if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
            if (bootSplashGroup != null)
            {
                bootSplashGroup.gameObject.SetActive(true);
                bootSplashGroup.alpha = 0f;

                float t = 0f;
                while (t < bootFadeInSec)
                {
                    t += Time.unscaledDeltaTime;
                    bootSplashGroup.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, bootFadeInSec));
                    yield return null;
                }
                bootSplashGroup.alpha = 1f;

                float hold = 0f;
                while (hold < bootHoldSec)
                {
                    hold += Time.unscaledDeltaTime;
                    yield return null;
                }

                t = 0f;
                while (t < bootFadeOutSec)
                {
                    t += Time.unscaledDeltaTime;
                    bootSplashGroup.alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, bootFadeOutSec));
                    yield return null;
                }
                bootSplashGroup.alpha = 0f;
                bootSplashGroup.gameObject.SetActive(false);
            }

            EnterMainMenuImmediate();
        }

        private void EnterMainMenuImmediate()
        {
            IsInBootSplash = false;
            IsInMainMenu = true;
            if (bootSplashGroup != null)
            {
                bootSplashGroup.alpha = 0f;
                bootSplashGroup.gameObject.SetActive(false);
            }
            if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
            if (mainMenuGroup != null) mainMenuGroup.alpha = 1f;
            RefreshContinueButton();

            if (menuMusicSource != null && !menuMusicSource.isPlaying)
            {
                ApplyMenuMusicVolume();
                menuMusicSource.Play();
            }
        }

        public void RefreshContinueButton()
        {
            if (continueButton != null)
                continueButton.gameObject.SetActive(GameSaveState.HasSave);
        }

        public void OnNewGameClicked()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSaveState.PrepareNewGame();
            SceneManager.LoadScene(GameSaveState.DefaultGameScene, LoadSceneMode.Single);
        }

        public void OnContinueClicked()
        {
            if (!GameSaveState.HasSave) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSaveState.PrepareContinue();
            SceneManager.LoadScene(GameSaveState.SavedScene, LoadSceneMode.Single);
        }

        public void OnOptionsClicked()
        {
            if (optionsPanel == null) return;
            if (mainMenuGroup != null) mainMenuGroup.interactable = false;
            optionsPanel.Open();
        }

        private void HandleOptionsClosed()
        {
            if (mainMenuGroup != null) mainMenuGroup.interactable = true;
        }

        public void OnQuitClicked()
        {
            Debug.Log("[MainMenu] Sair solicitado.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ApplyMenuMusicVolume()
        {
            if (menuMusicSource != null)
                menuMusicSource.volume = baseMusicVolume * GameSettingsManager.MusicVolume;
        }
    }
}
