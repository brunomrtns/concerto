using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Controlador da UI uGUI de Cutscenes (Prefab CutsceneUI_Canvas):
    ///   - Letterbox animado (entra suavemente, abre no corte pra gameplay)
    ///   - Legendas estilizadas por tipo (dialogue / radio / memory) lidas do JSON
    ///   - Title Card cinematografico ("CONCERTO") com fade-in/out
    ///   - Prompt "Segure [Espaco] ou [Esc] para pular" com barra de progresso
    ///   - Objetivo suave pos-cutscene ("Fale com Juno")
    /// </summary>
    public sealed class CutsceneUIOverlay : MonoBehaviour
    {
        [Header("Letterbox")]
        public RectTransform topBar;
        public RectTransform bottomBar;
        public float letterboxHeight = 112f;

        [Header("Legendas")]
        public CanvasGroup subtitleGroup;
        public Text speakerText;
        public Text bodyText;

        [Header("Title Card")]
        public CanvasGroup titleCardGroup;
        public Text titleCardText;

        [Header("Prompt de Skip")]
        public CanvasGroup skipGroup;
        public Text skipHintText;
        public Image skipProgressFill;

        [Header("Objetivo Suave Pos-Cutscene")]
        public CanvasGroup objectiveGroup;
        public Text objectiveText;

        private Coroutine _objectiveRoutine;
        private float _letterboxCurrent;
        private float _letterboxTarget;
        private Image _fadeOverlayImage;

        public void SetFadeAlpha(float alpha, Color? color = null)
        {
            if (_fadeOverlayImage == null && alpha > 0.001f)
            {
                var go = new GameObject("CS_SeamlessFadeOverlay");
                go.transform.SetParent(transform, false);
                go.transform.SetAsLastSibling();
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _fadeOverlayImage = go.AddComponent<Image>();
                _fadeOverlayImage.raycastTarget = false;
            }
            if (_fadeOverlayImage != null)
            {
                Color c = color ?? new Color(0.03f, 0.06f, 0.11f, 1f);
                c.a = Mathf.Clamp01(alpha);
                _fadeOverlayImage.color = c;
                _fadeOverlayImage.gameObject.SetActive(c.a > 0.001f);
            }
        }

        private void Awake()
        {
            HideImmediate();
        }

        private void Update()
        {
            if (topBar != null && bottomBar != null)
            {
                _letterboxCurrent = Mathf.MoveTowards(_letterboxCurrent, _letterboxTarget, Time.unscaledDeltaTime * 3.8f);
                float h = letterboxHeight * _letterboxCurrent;
                topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, h);
                bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, h);
            }
        }

        public void BeginCutscene(string skipHint, string titleText)
        {
            if (_objectiveRoutine != null) { StopCoroutine(_objectiveRoutine); _objectiveRoutine = null; }
            if (objectiveGroup != null) objectiveGroup.alpha = 0f;

            _letterboxTarget = 1f;
            if (skipHintText != null) skipHintText.text = skipHint;
            if (titleCardText != null) titleCardText.text = titleText;
            SetSubtitle(null);
            SetTitleCardAlpha(0f);
            SetSkipPrompt(false, 0f);
        }

        public void EndCutsceneAndShowObjective(string objectiveMsg)
        {
            _letterboxTarget = 0f;
            SetSubtitle(null);
            SetTitleCardAlpha(0f);
            SetSkipPrompt(false, 0f);

            if (!string.IsNullOrEmpty(objectiveMsg) && objectiveGroup != null && objectiveText != null)
            {
                if (_objectiveRoutine != null) StopCoroutine(_objectiveRoutine);
                _objectiveRoutine = StartCoroutine(ShowObjectiveToast(objectiveMsg));
            }
        }

        public void HideImmediate()
        {
            _letterboxCurrent = 0f;
            _letterboxTarget = 0f;
            if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, 0f);
            if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, 0f);
            if (subtitleGroup != null) subtitleGroup.alpha = 0f;
            if (titleCardGroup != null) titleCardGroup.alpha = 0f;
            if (skipGroup != null) skipGroup.alpha = 0f;
            if (objectiveGroup != null) objectiveGroup.alpha = 0f;
        }

        public void SetSubtitle(CutsceneSubtitleLine line)
        {
            if (subtitleGroup == null || bodyText == null) return;
            if (line == null || string.IsNullOrEmpty(line.text))
            {
                subtitleGroup.alpha = 0f;
                return;
            }

            subtitleGroup.alpha = 1f;
            bodyText.text = line.text;

            if (speakerText != null)
            {
                speakerText.text = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker;
                speakerText.gameObject.SetActive(!string.IsNullOrEmpty(line.speaker));
            }

            if (line.style == "memory")
            {
                bodyText.fontStyle = FontStyle.Italic;
                bodyText.color = new Color(1.00f, 0.90f, 0.72f, 1f);
            }
            else if (line.style == "radio")
            {
                bodyText.fontStyle = FontStyle.Normal;
                bodyText.color = new Color(0.68f, 0.94f, 1.00f, 1f);
                if (speakerText != null) speakerText.color = new Color(0.38f, 0.88f, 0.98f, 1f);
            }
            else
            {
                bodyText.fontStyle = FontStyle.Normal;
                bodyText.color = Color.white;
                if (speakerText != null) speakerText.color = new Color(1.00f, 0.80f, 0.42f, 1f);
            }
        }

        public void SetTitleCardAlpha(float alpha)
        {
            if (titleCardGroup != null)
                titleCardGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetSkipPrompt(bool visible, float holdProgress01)
        {
            if (skipGroup != null)
                skipGroup.alpha = visible ? 0.92f : 0f;
            if (skipProgressFill != null)
                skipProgressFill.fillAmount = Mathf.Clamp01(holdProgress01);
        }

        private IEnumerator ShowObjectiveToast(string msg)
        {
            objectiveText.text = $"OBJETIVO  •  {msg}";
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                objectiveGroup.alpha = Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            objectiveGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(6.5f);
            t = 0f;
            while (t < 1.2f)
            {
                t += Time.unscaledDeltaTime;
                objectiveGroup.alpha = 1f - Mathf.Clamp01(t / 1.2f);
                yield return null;
            }
            objectiveGroup.alpha = 0f;
            _objectiveRoutine = null;
        }
    }
}
