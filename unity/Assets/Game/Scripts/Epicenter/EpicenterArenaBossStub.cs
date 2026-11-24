using System;
using System.Collections;
using Studio.Game.Audio;
using Studio.Game.Combat;
using Studio.Game.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Epicenter
{
    /// <summary>
    /// Trigger Stub do Coracao da Fenda (Secao 3 do Epicentro — #102):
    ///   - Ao entrar no centro da arena circular: batida seca grave + tremor sutil (<280ms) + fade rapido + objetivo "SOBREVIVA".
    ///   - Expoe OnBossArenaTriggered onde a task #103 (Guardiao da Fenda) pluga diretamente sem refatoracao.
    /// </summary>
    public sealed class EpicenterArenaBossStub : MonoBehaviour
    {
        public EpicenterConfig config;
        public AudioSource omenAudioSource;
        public AudioClip omenDryHitClip;

        public bool IsTriggered { get; private set; }
        public event Action OnBossArenaTriggered;

        private Transform _player;
        private CanvasGroup _fadeGroup;
        private CanvasGroup _objectiveGroup;
        private Text _objectiveText;

        private void Awake()
        {
            if (omenAudioSource == null)
            {
                omenAudioSource = gameObject.AddComponent<AudioSource>();
                omenAudioSource.playOnAwake = false;
                omenAudioSource.spatialBlend = 0f;
            }
            if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                omenAudioSource.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;

            BuildStubOverlay();
        }

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null) _player = dez.transform;
        }

        private void Update()
        {
            if (IsTriggered || _player == null) return;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return;
            float r = config != null ? config.arenaTriggerRadius : 5.5f;
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= r * r)
                TriggerBossOmenStub();
        }

        public void TriggerBossOmenStub()
        {
            if (IsTriggered) return;
            IsTriggered = true;
            if (omenDryHitClip != null && omenAudioSource != null)
            {
                float sc = omenAudioSource.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
                omenAudioSource.PlayOneShot(omenDryHitClip, 0.92f * sc);
            }
            StartCoroutine(RunOmenSequence());
            OnBossArenaTriggered?.Invoke();
            Debug.Log("[EpicenterArenaStub] Trigger do Coracao da Fenda disparado: batida seca + tremor + objetivo 'SOBREVIVA' (pronto para #103).");
        }

        private IEnumerator RunOmenSequence()
        {
            // Tremor sutil respeitando ReduceCameraShake e decay < 300ms (DESIGN_BIBLE Secao 3 & 9)
            var cam = Camera.main;
            float dur = config != null ? Mathf.Min(0.28f, config.omenTremorDurationSec) : 0.25f;
            float amp = (config != null ? config.omenTremorAmplitude : 0.12f) * GameSettingsManager.CameraShakeScale;
            if (cam != null)
            {
                Vector3 orig = cam.transform.localPosition;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float fall = 1f - (t / dur);
                    cam.transform.localPosition = orig + new Vector3(Mathf.Sin(t * 95f) * amp * fall, Mathf.Cos(t * 110f) * amp * fall, 0f);
                    yield return null;
                }
                cam.transform.localPosition = orig;
            }

            // Fade rapido (0.35s) + exibe objetivo "SOBREVIVA"
            if (_fadeGroup != null)
            {
                float t = 0f;
                while (t < 0.22f) { t += Time.unscaledDeltaTime; _fadeGroup.alpha = Mathf.Clamp01(t / 0.22f) * 0.75f; yield return null; }
                t = 0f;
                while (t < 0.35f) { t += Time.unscaledDeltaTime; _fadeGroup.alpha = (1f - Mathf.Clamp01(t / 0.35f)) * 0.75f; yield return null; }
                _fadeGroup.alpha = 0f;
            }

            if (_objectiveText != null)
                _objectiveText.text = config != null ? config.surviveObjectiveText : "OBJETIVO: SOBREVIVA";
            if (_objectiveGroup != null)
                _objectiveGroup.alpha = 1f;
        }

        private void BuildStubOverlay()
        {
            var cGo = new GameObject("EpicenterArena_Overlay");
            cGo.transform.SetParent(transform, false);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 22;
            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var fGo = new GameObject("QuickFade");
            fGo.transform.SetParent(cGo.transform, false);
            var frt = fGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            fGo.AddComponent<Image>().color = Color.black;
            _fadeGroup = fGo.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;

            var oGo = new GameObject("SurviveObjectiveBanner");
            oGo.transform.SetParent(cGo.transform, false);
            var ort = oGo.AddComponent<RectTransform>();
            ort.anchorMin = new Vector2(0.5f, 1f); ort.anchorMax = new Vector2(0.5f, 1f);
            ort.pivot = new Vector2(0.5f, 1f);
            ort.anchoredPosition = new Vector2(0f, -34f);
            ort.sizeDelta = new Vector2(520f, 52f);
            oGo.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.14f, 0.84f);
            _objectiveGroup = oGo.AddComponent<CanvasGroup>();
            _objectiveGroup.alpha = 0f;

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(oGo.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _objectiveText = tGo.AddComponent<Text>();
            _objectiveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _objectiveText.fontSize = 24;
            _objectiveText.fontStyle = FontStyle.Bold;
            _objectiveText.alignment = TextAnchor.MiddleCenter;
            _objectiveText.color = new Color(0.96f, 0.35f, 0.32f, 1f);
        }
    }
}
