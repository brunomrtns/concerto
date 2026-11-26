using System;
using Studio.Game.Animation;
using Studio.Game.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Cutscenes
{
    [Serializable]
    public sealed class Cs02OfertaLineDef
    {
        public string lineId;
        public string speaker;
        public string emotion;
        public string subtitlePtBr;
        public float startSec;
        public float endSec;
        public AudioClip voClip;
    }

    /// <summary>
    /// CS02 "A OFERTA" — O Twist do Meio de Jogo (GDD 01 §3 Ato 2 + 09-cutscenes):
    ///   - A Maestrina Iria fala com Dez atraves de um Censor existente (ZERO modelos 3D novos).
    ///   - Tom: a tentacao e real — o jogo NAO diz que e mentira.
    ///   - Trigger: dispara automaticamente UMA vez ao coletar a 2a Reliquia.
    ///   - Skippable SOMENTE apos 3.0s (MinSkipElapsedSec = 3.0f); pause real funciona.
    ///   - Trava gameplay (DezLocomotionController) durante a cena e libera limpo no fim sem resetar objetivo/missao.
    ///   - Flag 'cs02OfertaPlayed' persiste no Save JSON (reload nao reprime nem repete).
    /// </summary>
    public sealed class CutsceneAOfetaController : MonoBehaviour
    {
        public const float MinSkipElapsedSec = 3.0f;
        public const int RequiredRelicsToTrigger = 2;

        public static CutsceneAOfetaController Instance { get; private set; }

        [Header("Referencias")]
        public GameObject censorVesselActor; // Censor existente atraves do qual Iria fala
        public DezLocomotionController playerLoco;
        public AudioSource voAudioSource;
        public Cs02OfertaLineDef[] lines;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasPlayedOnce { get; private set; }
        public bool GameplayLocked => IsPlaying;
        public bool CanSkipNow => IsPlaying && ElapsedSec >= MinSkipElapsedSec;
        public float ElapsedSec { get; private set; }
        public float TotalDurationSec { get; private set; } = 32.0f;
        public int CurrentLineIndex { get; private set; } = -1;
        public string CurrentSubtitle { get; private set; } = string.Empty;
        public int TriggerCount { get; private set; }

        public event Action OnCs02OfertaStarted;
        public event Action OnCs02OfertaEnded;

        private Text _subtitleText;
        private CanvasGroup _subtitleGroup;
        private bool _locoWasEnabledBeforeCutscene;

        private void Awake()
        {
            Instance = this;
            EnsureDefaultLines();
            BuildSubtitleOverlay();
            if (voAudioSource == null)
            {
                voAudioSource = gameObject.AddComponent<AudioSource>();
                voAudioSource.playOnAwake = false;
                voAudioSource.spatialBlend = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureDefaultLines()
        {
            if (lines != null && lines.Length > 0) return;
            lines = new[]
            {
                new Cs02OfertaLineDef { lineId = "cs02_iria_01",  speaker = "Iria (Censor)", emotion = "ameacador-suave",  startSec = 0.0f,  endSec = 6.5f,  subtitlePtBr = "Devolva minhas reliquias... e devolvo sua irma." },
                new Cs02OfertaLineDef { lineId = "cs02_dez_01",   speaker = "Dez",           emotion = "urgente",          startSec = 6.5f,  endSec = 10.5f, subtitlePtBr = "Onde voce ta guardando ela?" },
                new Cs02OfertaLineDef { lineId = "cs02_iria_02",  speaker = "Iria (Censor)", emotion = "calmo-persuasivo", startSec = 10.5f, endSec = 17.0f, subtitlePtBr = "No ultimo segundo intacto da Corona. Traga as reliquias e eu abro a caixa." },
                new Cs02OfertaLineDef { lineId = "cs02_porca_01", speaker = "Porca",         emotion = "apreensivo",       startSec = 17.0f, endSec = 21.5f, subtitlePtBr = "A voz dela nao tremeu nem meio compasso, Dez." },
                new Cs02OfertaLineDef { lineId = "cs02_iria_03",  speaker = "Iria (Censor)", emotion = "firme",            startSec = 21.5f, endSec = 27.0f, subtitlePtBr = "Pense bem no que vale mais, Desiderio." },
                new Cs02OfertaLineDef { lineId = "cs02_dez_02",   speaker = "Dez",           emotion = "contido",          startSec = 27.0f, endSec = 32.0f, subtitlePtBr = "Vamos continuar andando, Porca. Mas eu ouvi cada palavra." }
            };
            TotalDurationSec = 32.0f;
        }

        private void BuildSubtitleOverlay()
        {
            var canvasGo = new GameObject("CS02_Oferta_SubtitleCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            _subtitleGroup = canvasGo.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            _subtitleGroup.blocksRaycasts = false;

            var bgGo = new GameObject("SubtitleBar");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var img = bgGo.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.12f, 0.82f);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.12f, 0.05f);
            rt.anchorMax = new Vector2(0.88f, 0.16f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txtGo = new GameObject("SubtitleText");
            txtGo.transform.SetParent(bgGo.transform, false);
            _subtitleText = txtGo.AddComponent<Text>();
            _subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _subtitleText.fontSize = 24;
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.color = new Color(0.98f, 0.94f, 0.82f, 1f);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 8f);
            trt.offsetMax = new Vector2(-16f, -8f);
        }

        /// <summary>GATE 3 & 5: Trigger automatico ao atingir 2 Reliquias; dispara UMA unica vez.</summary>
        public bool EvaluateRelicCountTrigger(int collectedRelicsCount)
        {
            if (HasPlayedOnce || IsPlaying) return false;
            if (collectedRelicsCount < RequiredRelicsToTrigger) return false;
            return PlayCutscene();
        }

        public static int CountCollectedRelicsInSave(SaveGameData data)
        {
            if (data == null) return 0;
            int n = 0;
            if (data.relicPonteiroMaiorUnlocked) n++;
            if (data.relicPenduloUnlocked) n++;
            if (data.relicPonteiroMenorUnlocked) n++;
            if (data.relicAroEscudoUnlocked) n++;
            return n;
        }

        public bool PlayCutscene()
        {
            if (IsPlaying || HasPlayedOnce) return false;
            if (playerLoco == null) playerLoco = FindAnyObjectByType<DezLocomotionController>();

            IsPlaying = true;
            IsPaused = false;
            HasPlayedOnce = true;
            TriggerCount++;
            ElapsedSec = 0f;
            CurrentLineIndex = -1;
            CurrentSubtitle = string.Empty;

            if (censorVesselActor != null) censorVesselActor.SetActive(true);
            if (playerLoco != null)
            {
                _locoWasEnabledBeforeCutscene = playerLoco.enabled;
                playerLoco.enabled = false; // GATE 4: trava gameplay durante a cutscene
            }
            if (_subtitleGroup != null) _subtitleGroup.alpha = 1f;

            UpdateSubtitleAtTime(0f);
            OnCs02OfertaStarted?.Invoke();
            return true;
        }

        public void SetPaused(bool paused)
        {
            if (!IsPlaying) return;
            IsPaused = paused;
            if (voAudioSource != null)
            {
                if (paused) voAudioSource.Pause();
                else voAudioSource.UnPause();
            }
        }

        /// <summary>GATE 3: Skippable SOMENTE apos 3.0s.</summary>
        public bool TrySkip()
        {
            if (!CanSkipNow) return false;
            FinishCutscene(persistSave: true);
            return true;
        }

        public void AdvanceTimeForTest(float dt)
        {
            if (!IsPlaying || IsPaused || dt <= 0f) return;
            ElapsedSec += dt;
            if (ElapsedSec >= TotalDurationSec)
            {
                FinishCutscene(persistSave: true);
                return;
            }
            UpdateSubtitleAtTime(ElapsedSec);
        }

        private void Update()
        {
            if (!IsPlaying || IsPaused || Time.timeScale <= 0.001f) return;
            if (Input.GetKeyDown(KeyCode.Escape))
                TrySkip();
            AdvanceTimeForTest(Time.deltaTime);
        }

        private void UpdateSubtitleAtTime(float t)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (t >= l.startSec && t < l.endSec)
                {
                    if (CurrentLineIndex != i)
                    {
                        CurrentLineIndex = i;
                        CurrentSubtitle = $"{l.speaker}: \"{l.subtitlePtBr}\"";
                        if (_subtitleText != null) _subtitleText.text = CurrentSubtitle;
                        if (voAudioSource != null && l.voClip != null)
                        {
                            voAudioSource.Stop();
                            voAudioSource.clip = l.voClip;
                            voAudioSource.Play();
                        }
                    }
                    return;
                }
            }
        }

        private void FinishCutscene(bool persistSave)
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            IsPaused = false;
            CurrentSubtitle = string.Empty;
            if (voAudioSource != null) voAudioSource.Stop();
            if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;

            // GATE 4: libera gameplay limpo sem resetar objetivo/missao
            if (playerLoco != null)
                playerLoco.enabled = _locoWasEnabledBeforeCutscene || true;

            if (persistSave)
                SaveSystem.Save();

            OnCs02OfertaEnded?.Invoke();
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.cs02OfertaPlayed = HasPlayedOnce;
        }

        public void RestoreFromSave(SaveGameData data)
        {
            if (data == null) return;
            HasPlayedOnce = data.cs02OfertaPlayed;
        }
    }
}
