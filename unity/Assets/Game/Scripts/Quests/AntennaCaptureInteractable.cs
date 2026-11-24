using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Quests
{
    /// <summary>
    /// AntennaCaptureInteractable — Interacao da antena na Missao "Sinal":
    ///   - Fica disponivel quando os 2 alimentadores do gerador sao restaurados (SignalQuestStep.CaptureAntenna)
    ///   - Executa sequencia curta de captura (3.0s) com feixe ciano para o ceu, audio de telemetria e feedback de registro corrompido
    /// </summary>
    public sealed class AntennaCaptureInteractable : MonoBehaviour, IInteractable
    {
        [Header("Sequencia de Captura")]
        public float captureDurationSec = 3.0f;
        public AudioSource sfxSource;
        public AudioClip captureClip;
        public GameObject skyBeamVisual;
        public Light antennaPulseLight;

        private bool _available;
        private bool _isCapturing;
        private bool _captured;
        private GameObject _hudCanvas;
        private Text _hudStatusText;
        private Image _hudFillImage;

        public bool IsAvailable => _available && !_captured;
        public bool IsCapturing => _isCapturing;
        public bool IsCaptured => _captured;

        public string PromptText => _isCapturing
            ? "Capturando leitura da antena..."
            : "[E] Capturar leitura da antena";

        public Transform IkHandTarget => transform;

        private void Awake()
        {
            if (GetComponent<InteractionHighlight>() == null)
                gameObject.AddComponent<InteractionHighlight>();
            if (skyBeamVisual != null) skyBeamVisual.SetActive(false);
            if (antennaPulseLight != null) antennaPulseLight.enabled = false;
        }

        public void SetCaptureAvailable(bool available)
        {
            _available = available;
            if (!available && !_isCapturing)
            {
                if (skyBeamVisual != null) skyBeamVisual.SetActive(false);
                if (antennaPulseLight != null) antennaPulseLight.enabled = false;
            }
            else if (available && antennaPulseLight != null)
            {
                antennaPulseLight.enabled = true;
                antennaPulseLight.intensity = 2.4f;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!_available || _isCapturing || _captured) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            StartCoroutine(RunCaptureSequenceRoutine(interactor));
        }

        private IEnumerator RunCaptureSequenceRoutine(GameObject interactor)
        {
            _isCapturing = true;
            var loco = interactor != null ? interactor.GetComponent<DezLocomotionController>() : null;
            if (loco != null) loco.enabled = false;

            EnsureCaptureHud();
            _hudCanvas.SetActive(true);
            _hudStatusText.text = "SINTONIZANDO ANTENA → MEDINDO RACHADURA NO CÉU...";
            _hudFillImage.fillAmount = 0f;

            if (skyBeamVisual != null) skyBeamVisual.SetActive(true);
            if (antennaPulseLight != null) antennaPulseLight.enabled = true;

            if (sfxSource != null && captureClip != null)
            {
                sfxSource.Stop();
                sfxSource.PlayOneShot(captureClip, 0.90f * GameSettingsManager.SfxVolume);
            }

            float elapsed = 0f;
            float dur = Mathf.Max(0.4f, captureDurationSec);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / dur);
                _hudFillImage.fillAmount = p;

                if (skyBeamVisual != null)
                {
                    float pulse = 1.0f + 0.35f * Mathf.Sin(elapsed * 22f);
                    skyBeamVisual.transform.localScale = new Vector3(0.38f * pulse, 28f * Mathf.Clamp01(p * 1.4f), 0.38f * pulse);
                }
                if (antennaPulseLight != null)
                    antennaPulseLight.intensity = 3.5f + 2.5f * Mathf.Sin(elapsed * 26f);

                if (p > 0.72f)
                {
                    string stamp = SignalQuestController.Instance != null && SignalQuestController.Instance.Config != null
                        ? SignalQuestController.Instance.Config.corruptedTimestampDisplay
                        : "14:02:11.004 — REGISTRO CORROMPIDO";
                    _hudStatusText.text = $"LEITURA OBTIDA: [{stamp}]";
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.55f);

            if (skyBeamVisual != null) skyBeamVisual.SetActive(false);
            if (antennaPulseLight != null) antennaPulseLight.enabled = false;
            if (_hudCanvas != null) _hudCanvas.SetActive(false);
            if (loco != null) loco.enabled = true;

            _isCapturing = false;
            _captured = true;
            _available = false;

            if (SignalQuestController.Instance != null)
                SignalQuestController.Instance.NotifyAntennaReadingCaptured();
        }

        private void EnsureCaptureHud()
        {
            if (_hudCanvas != null) return;
            _hudCanvas = new GameObject("AntennaCapture_HUD");
            var c = _hudCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 520;
            var sc = _hudCanvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_hudCanvas.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.28f, 0.10f);
            prt.anchorMax = new Vector2(0.72f, 0.21f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.09f, 0.15f, 0.90f);

            var txtGo = new GameObject("StatusText");
            txtGo.transform.SetParent(panel.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.04f, 0.45f);
            trt.anchorMax = new Vector2(0.96f, 0.92f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _hudStatusText = txtGo.AddComponent<Text>();
            _hudStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hudStatusText.fontSize = 23;
            _hudStatusText.fontStyle = FontStyle.Bold;
            _hudStatusText.alignment = TextAnchor.MiddleCenter;
            _hudStatusText.color = new Color(0.42f, 0.96f, 1.0f, 1f);

            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(panel.transform, false);
            var brt = barBg.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.06f, 0.16f);
            brt.anchorMax = new Vector2(0.94f, 0.36f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            barBg.AddComponent<Image>().color = new Color(0.14f, 0.20f, 0.28f, 1f);

            var fill = new GameObject("BarFill");
            fill.transform.SetParent(barBg.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _hudFillImage = fill.AddComponent<Image>();
            _hudFillImage.color = new Color(0.26f, 0.95f, 1.0f, 1f);
            _hudFillImage.type = Image.Type.Filled;
            _hudFillImage.fillMethod = Image.FillMethod.Horizontal;
        }
    }
}
