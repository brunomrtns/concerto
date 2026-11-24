using System;
using System.Collections;
using Studio.Game.Audio;
using Studio.Game.Menu;
using Studio.Game.Save;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Boss
{
    /// <summary>
    /// Fragmento de Nucleo dropado na morte do Guardiao da Fenda (#103):
    ///   - Interagir [E] -> flash de holograma de ~0.5s (OFICINA NO PASSADO + ESPOSA, SEM dialogo, SEM explicacao!)
    ///   - Seguido pelo cartao "Fim do Ato 3" + auto-save via SaveSystem JSON.
    /// </summary>
    public sealed class CoreFragmentInteractable : MonoBehaviour, IInteractable
    {
        public const string Act3CompletedPrefKey = "CONCERTO_ACT3_COMPLETED";

        public RiftGuardianConfig config;
        public Texture2D hologramWorkshopWifeTexture;
        public AudioClip hologramFlashClip;

        public bool IsCollected { get; private set; }
        public bool HologramFlashShown { get; private set; }
        public bool Act3EndCardShown { get; private set; }

        public event Action OnAct3Finished;

        public string PromptText => IsCollected ? string.Empty : "[E] Tocar Fragmento de Núcleo";
        public Transform IkHandTarget => transform;

        private CanvasGroup _holoGroup;
        private RawImage _holoImage;
        private CanvasGroup _endCardGroup;

        private void Awake()
        {
            BuildVictoryOverlays();
        }

        private void Update()
        {
            if (!IsCollected)
                transform.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);
        }

        public bool CanInteract(GameObject interactor) => !IsCollected;

        public void Interact(GameObject interactor)
        {
            if (IsCollected) return;
            IsCollected = true;
            StartCoroutine(RunHologramFlashAndAct3EndCard());
        }

        private IEnumerator RunHologramFlashAndAct3EndCard()
        {
            // 1. Flash de holograma de MEIO SEGUNDO (oficina no passado + esposa — ZERO dialogo, ZERO explicacao!)
            if (hologramFlashClip != null)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                    src.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;
                src.PlayOneShot(hologramFlashClip, 0.90f);
            }

            if (_holoImage != null && hologramWorkshopWifeTexture != null)
                _holoImage.texture = hologramWorkshopWifeTexture;

            float flashDur = config != null ? config.hologramFlashDurationSec : 0.52f;
            if (_holoGroup != null) _holoGroup.alpha = 0.92f;
            HologramFlashShown = true;
            Debug.Log($"[CoreFragment] Flash de holograma (oficina no passado + esposa) exibido por {flashDur:F2}s — SEM dialogo, SEM explicacao.");

            float t = 0f;
            while (t < flashDur)
            {
                t += Time.unscaledDeltaTime;
                if (_holoGroup != null)
                {
                    float flicker = 0.80f + 0.20f * Mathf.Sin(t * 75f);
                    _holoGroup.alpha = flicker * (1f - Mathf.Clamp01((t - flashDur * 0.7f) / (flashDur * 0.3f)));
                }
                yield return null;
            }
            if (_holoGroup != null) _holoGroup.alpha = 0f;

            // Pausa silenciosa de impacto (0.45s)
            t = 0f;
            while (t < 0.45f) { t += Time.unscaledDeltaTime; yield return null; }

            // 2. Cartao "Fim do Ato 3" + Auto-Save JSON versionado
            PlayerPrefs.SetInt(Act3CompletedPrefKey, 1);
            PlayerPrefs.Save();
            SaveSystem.Save("act3_boss_defeated", transform.position);

            if (_endCardGroup != null)
            {
                t = 0f;
                while (t < 0.5f)
                {
                    t += Time.unscaledDeltaTime;
                    _endCardGroup.alpha = Mathf.Clamp01(t / 0.5f);
                    yield return null;
                }
                _endCardGroup.alpha = 1f;
            }
            Act3EndCardShown = true;
            Debug.Log("[CoreFragment] Cartao 'FIM DO ATO 3' exibido + Auto-Save concluido.");
            OnAct3Finished?.Invoke();
        }

        private void BuildVictoryOverlays()
        {
            var cGo = new GameObject("CoreFragment_VictoryCanvas");
            cGo.transform.SetParent(transform, false);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Flash de holograma (tint ciano-holografico sutil sobre a foto da oficina + esposa)
            var hGo = new GameObject("HologramFlash_OfficePast");
            hGo.transform.SetParent(cGo.transform, false);
            var hrt = hGo.AddComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            _holoImage = hGo.AddComponent<RawImage>();
            _holoImage.color = new Color(0.62f, 0.94f, 1.0f, 1f);
            _holoGroup = hGo.AddComponent<CanvasGroup>();
            _holoGroup.alpha = 0f;
            _holoGroup.blocksRaycasts = false;

            // Cartao Fim do Ato 3
            var eGo = new GameObject("Act3EndCard");
            eGo.transform.SetParent(cGo.transform, false);
            var ert = eGo.AddComponent<RectTransform>();
            ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
            ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
            eGo.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.92f);
            _endCardGroup = eGo.AddComponent<CanvasGroup>();
            _endCardGroup.alpha = 0f;
            _endCardGroup.blocksRaycasts = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(eGo.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.56f); trt.anchorMax = new Vector2(0.5f, 0.56f);
            trt.sizeDelta = new Vector2(900f, 90f);
            var tTxt = titleGo.AddComponent<Text>();
            tTxt.font = font;
            tTxt.fontSize = 54;
            tTxt.fontStyle = FontStyle.Bold;
            tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.color = new Color(0.75f, 0.96f, 1.0f, 1f);
            tTxt.text = config != null ? config.act3EndCardTitle : "FIM DO ATO 3";

            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(eGo.transform, false);
            var srt = subGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.45f); srt.anchorMax = new Vector2(0.5f, 0.45f);
            srt.sizeDelta = new Vector2(980f, 60f);
            var sTxt = subGo.AddComponent<Text>();
            sTxt.font = font;
            sTxt.fontSize = 24;
            sTxt.alignment = TextAnchor.MiddleCenter;
            sTxt.color = new Color(0.82f, 0.86f, 0.92f, 0.9f);
            sTxt.text = config != null ? config.act3EndCardSubtitle : "O Coração da Fenda silenciou... mas a imagem permanece.";
        }
    }
}
