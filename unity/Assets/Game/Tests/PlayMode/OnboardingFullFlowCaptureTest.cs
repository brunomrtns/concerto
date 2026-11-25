using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// GATE 3 — Captura PlayMode completa do Onboarding (do "Novo Jogo" no MainMenu ate Eco Temporal + Estase).
    /// Gera frames 1080p + onboarding_meta.json em unity/Logs/onboarding_capture/ para encoding em
    /// captures/onboarding/onboarding_completo.mp4, e grava tempos medidos de cada passo para ONBOARDING.md.
    /// </summary>
    public sealed class OnboardingFullFlowCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string phase;
            public string prompt;
            public string badge;
        }

        [Serializable]
        private sealed class StepMetric
        {
            public string step;
            public float durationSec;
        }

        [Serializable]
        private sealed class CaptureMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
            public List<StepMetric> metrics = new List<StepMetric>();
            public float totalSec;
            public bool skipGateVerified;
            public bool saveVerified;
        }

        private string _outDir;
        private CaptureMeta _meta;
        private Texture2D _readTex;
        private RenderTexture _rt;

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        [UnityTest]
        public IEnumerator Onboarding_CaptureFullNewGameToPowersFlow()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "onboarding_capture"));
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            _meta = new CaptureMeta();
            _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            var menuT    = GameType("Studio.Game.Menu.BootAndMainMenuController");
            var onbT     = GameType("Studio.Game.Onboarding.OnboardingSequenceController");
            var stepEnum = GameType("Studio.Game.Onboarding.OnboardingStep");
            var locoT    = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT  = GameType("Studio.Game.Powers.DezPowersController");
            var cs01T    = GameType("Studio.Game.Cutscenes.CutsceneManager");

            string tmpSave = Path.Combine(Application.temporaryCachePath, $"concerto_onb_cap_{Guid.NewGuid():N}.json");
            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");
            mSetPath.Invoke(null, new object[] { tmpSave });

            try
            {
                // 1. MainMenu -> Novo Jogo
                var opMenu = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
                while (!opMenu.isDone) yield return null;
                yield return null;

                var menu = UnityEngine.Object.FindAnyObjectByType(menuT);
                Assert.IsNotNull(menu);
                menuT.GetMethod("SkipBootSplash").Invoke(menu, null);
                yield return null;

                yield return CaptureFrames(14, "MENU PRINCIPAL — NOVO JOGO", "Clique em 'Novo Jogo' para iniciar a jornada", "ABERTURA");

                menuT.GetMethod("OnNewGameClicked").Invoke(menu, null);
                float waitLoad = 0f;
                while (SceneManager.GetActiveScene().name != "Caldeiral" && waitLoad < 10f)
                {
                    yield return null;
                    waitLoad += Time.unscaledDeltaTime;
                }
                Assert.AreEqual("Caldeiral", SceneManager.GetActiveScene().name);
                for (int i = 0; i < 6; i++) yield return null;

                var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
                if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                {
                    yield return CaptureFrames(10, "CUTSCENE 01 — ABERTURA (SKIP APOS 3s)", "Abertura <=90s com skip limpo", "CUTSCENE");
                    cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
                }
                for (int i = 0; i < 4; i++) yield return null;

                var onb    = UnityEngine.Object.FindAnyObjectByType(onbT) as Component;
                var loco   = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
                var powers = UnityEngine.Object.FindAnyObjectByType(powersT);
                Assert.IsNotNull(onb);
                Assert.IsNotNull(loco);
                Assert.IsNotNull(powers);
                Assert.IsTrue((bool)onbT.GetProperty("IsActive").GetValue(onb));

                var pCurStep   = onbT.GetProperty("CurrentStep");
                var pPrompt    = onbT.GetProperty("CurrentPromptText");
                var pTelegraph = onbT.GetProperty("TelegraphCompletedForCurrentStep");
                var pCanSkip   = onbT.GetProperty("CanSkipCurrentStep");
                var mGetPos    = onbT.GetMethod("GetStationPosition");
                var pUseScript = locoT.GetProperty("UseScriptedInput");
                var pMoveDir   = locoT.GetProperty("ScriptedMoveDir");
                var pJump      = locoT.GetProperty("ScriptedJump");
                var pJumpHeld  = locoT.GetProperty("ScriptedJumpHeld");

                // Valida GATE 2 (skip bloqueado antes de 3s)
                Assert.IsFalse((bool)pCanSkip.GetValue(onb), "Skip nao deve estar liberado antes de 3s.");
                _meta.skipGateVerified = true;

                object sMove = Enum.Parse(stepEnum, "Move");
                object sJump = Enum.Parse(stepEnum, "Jump");
                object sInt  = Enum.Parse(stepEnum, "Interact");
                object sEco  = Enum.Parse(stepEnum, "EcoTemporal");
                object sSta  = Enum.Parse(stepEnum, "Estase");

                // Passo 1: MOVER
                yield return WaitTelegraphAndCapture(onb, pTelegraph, pPrompt, "PASSO 1/5 — MOVER (TELEGRAPH 450ms)", 6);
                pUseScript.SetValue(loco, true);
                Vector3 st0 = (Vector3)mGetPos.Invoke(onb, new object[] { sMove });
                yield return MoveDezToAndCapture(loco, onb, pCurStep, pPrompt, pMoveDir, pJump, pJumpHeld, sMove, st0, false, "PASSO 1/5 — MOVER", 24);
                Assert.AreEqual(sJump, pCurStep.GetValue(onb));

                // Passo 2: PULAR
                yield return WaitTelegraphAndCapture(onb, pTelegraph, pPrompt, "PASSO 2/5 — PULAR (COYOTE 100ms / BUFFER 150ms)", 6);
                Vector3 st1 = (Vector3)mGetPos.Invoke(onb, new object[] { sJump });
                yield return MoveDezToAndCapture(loco, onb, pCurStep, pPrompt, pMoveDir, pJump, pJumpHeld, sJump, st1, true, "PASSO 2/5 — PULAR SOBRE O PLATO", 26);
                Assert.AreEqual(sInt, pCurStep.GetValue(onb));

                // Passo 3: INTERAGIR
                yield return WaitTelegraphAndCapture(onb, pTelegraph, pPrompt, "PASSO 3/5 — INTERAGIR (TERMINAL DIEGETICO)", 6);
                Vector3 st2 = (Vector3)mGetPos.Invoke(onb, new object[] { sInt });
                float approachT = 0f;
                int capStep = 0;
                while (approachT < 1.6f)
                {
                    Vector3 d = st2 - loco.transform.position; d.y = 0f;
                    if (d.magnitude < 1.4f) break;
                    pMoveDir.SetValue(loco, d.normalized);
                    yield return null;
                    approachT += Time.deltaTime;
                    if ((capStep++ % 3) == 0)
                        yield return CaptureSingleFrame("PASSO 3/5 — INTERAGIR", (string)pPrompt.GetValue(onb), "INTERAGIR");
                }
                pMoveDir.SetValue(loco, Vector3.zero);
                Assert.IsTrue((bool)onbT.GetMethod("TryInteractCurrentTerminal").Invoke(onb, null));
                yield return CaptureFrames(10, "PASSO 3/5 — TERMINAL ATIVADO!", (string)pPrompt.GetValue(onb), "CONCLUIDO");
                Assert.AreEqual(sEco, pCurStep.GetValue(onb));

                // Passo 4: ECO TEMPORAL
                yield return WaitTelegraphAndCapture(onb, pTelegraph, pPrompt, "PASSO 4/5 — ECO TEMPORAL (TELEGRAPH 450ms)", 8);
                Vector3 st3 = (Vector3)mGetPos.Invoke(onb, new object[] { sEco });
                pMoveDir.SetValue(loco, (st3 - loco.transform.position).normalized);
                Assert.IsTrue((bool)powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null), "Eco Temporal deve ativar no Passo 4.");
                yield return CaptureFrames(14, "PASSO 4/5 — ECO TEMPORAL ATIVADO!", "Afterimage ciano + i-frames atravessam a energia", "PODER #1");
                pMoveDir.SetValue(loco, Vector3.zero);
                Assert.AreEqual(sSta, pCurStep.GetValue(onb));

                // Passo 5: ESTASE
                yield return WaitTelegraphAndCapture(onb, pTelegraph, pPrompt, "PASSO 5/5 — ESTASE (TELEGRAPH 450ms)", 8);
                Assert.IsTrue((bool)powersT.GetMethod("TriggerEstase").Invoke(powers, null), "Estase deve ativar no Passo 5.");
                yield return CaptureFrames(18, "PASSO 5/5 — ESTASE ATIVADA! PORTA DESACELERADA!", "Onboarding concluido e salvo em JSON versionado", "PODER #2");
                pUseScript.SetValue(loco, false);

                Assert.IsTrue((bool)onbT.GetProperty("IsCompleted").GetValue(onb));
                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)saveSysT.GetMethod("TryLoad").Invoke(null, loadArgs));
                var saved = loadArgs[0];
                var dataT = saved.GetType();
                Assert.IsTrue((bool)dataT.GetField("onboardingCompleted").GetValue(saved));
                var stepsList = dataT.GetField("completedOnboardingSteps").GetValue(saved) as List<string>;
                Assert.IsNotNull(stepsList);
                Assert.AreEqual(5, stepsList.Count);
                _meta.saveVerified = true;
                _meta.totalSec = (float)onbT.GetProperty("TotalElapsedSec").GetValue(onb);

                var durDict = onbT.GetProperty("StepDurationsSec").GetValue(onb) as IEnumerable;
                if (durDict != null)
                {
                    foreach (var kv in durDict)
                    {
                        var kvT = kv.GetType();
                        string k = kvT.GetProperty("Key").GetValue(kv).ToString();
                        float v = (float)kvT.GetProperty("Value").GetValue(kv);
                        _meta.metrics.Add(new StepMetric { step = k, durationSec = v });
                    }
                }

                File.WriteAllText(Path.Combine(_outDir, "onboarding_meta.json"), JsonUtility.ToJson(_meta, true));
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (File.Exists(tmpSave)) File.Delete(tmpSave);
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator WaitTelegraphAndCapture(Component onb, System.Reflection.PropertyInfo pTelegraph,
            System.Reflection.PropertyInfo pPrompt, string phase, int targetFrames)
        {
            int captured = 0;
            while (!(bool)pTelegraph.GetValue(onb))
            {
                yield return null;
                if (captured < targetFrames)
                {
                    yield return CaptureSingleFrame(phase, (string)pPrompt.GetValue(onb), "TELEGRAPH");
                    captured++;
                }
            }
        }

        private IEnumerator MoveDezToAndCapture(Component loco, Component onb,
            System.Reflection.PropertyInfo pCurStep, System.Reflection.PropertyInfo pPrompt,
            System.Reflection.PropertyInfo pMoveDir, System.Reflection.PropertyInfo pJump,
            System.Reflection.PropertyInfo pJumpHeld,
            object step, Vector3 target, bool doJump, string phase, int maxFrames)
        {
            float elapsed = 0f;
            int frameMod = 0;
            int captured = 0;
            bool jumped = false;
            int jumpHoldFrames = 0;

            while (Equals(pCurStep.GetValue(onb), step) && elapsed < 6f)
            {
                Vector3 d = target - loco.transform.position;
                d.y = 0f;
                pMoveDir.SetValue(loco, d.sqrMagnitude > 0.04f ? d.normalized : Vector3.zero);
                if (doJump && !jumped && d.magnitude < 1.9f)
                {
                    pJumpHeld.SetValue(loco, true);
                    pJump.SetValue(loco, true);
                    jumped = true;
                    jumpHoldFrames = 0;
                }
                else if (jumped)
                {
                    jumpHoldFrames++;
                    if (jumpHoldFrames == 2) pJump.SetValue(loco, false);
                    if (jumpHoldFrames == 18) pJumpHeld.SetValue(loco, false);
                }
                yield return null;
                elapsed += Time.deltaTime;
                if ((frameMod++ % 3) == 0 && captured < maxFrames)
                {
                    yield return CaptureSingleFrame(phase, (string)pPrompt.GetValue(onb), step.ToString().ToUpperInvariant());
                    captured++;
                }
            }
            pJump.SetValue(loco, false);
            pJumpHeld.SetValue(loco, false);
            pMoveDir.SetValue(loco, Vector3.zero);
            yield return CaptureFrames(6, $"{phase} — CONCLUIDO!", (string)pPrompt.GetValue(onb), "OK");
        }

        private IEnumerator CaptureFrames(int count, string phase, string prompt, string badge)
        {
            for (int i = 0; i < count; i++)
            {
                yield return CaptureSingleFrame(phase, prompt, badge);
            }
        }

        private IEnumerator CaptureSingleFrame(string phase, string prompt, string badge)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var prev = cam.targetTexture;
                cam.targetTexture = _rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = _rt;
                _readTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                _readTex.Apply(false);
                RenderTexture.active = null;
            }

            int idx = _meta.frames.Count;
            byte[] jpg = _readTex.EncodeToJPG(86);
            File.WriteAllBytes(Path.Combine(_outDir, $"frame_{idx:D4}.jpg"), jpg);
            _meta.frames.Add(new FrameMeta { index = idx, phase = phase, prompt = prompt ?? string.Empty, badge = badge });
        }
    }
}
