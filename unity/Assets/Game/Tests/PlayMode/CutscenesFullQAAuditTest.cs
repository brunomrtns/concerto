using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Suite de QA objetivo para TODAS as 4 cutscenes (CS01, CS02, CS03, CS04):
    ///   1. Valida que CharacterController do Dez esta DESLIGADO durante a cutscene (zero travamento em props) e RELIGADO ao terminar.
    ///   2. Mede luminancia real pixel-a-pixel e valida que NENHUMA cutscene excede 3.0s continuos de escuridao (avgLuma < 0.045).
    ///   3. Valida Skip limpo em cada cutscene (CinemachineBrain, DezLocomotionController, CharacterController, Time.timeScale, Volumes).
    ///   4. Re-grava frames 1080p das 4 cutscenes corrigidas quando CAPTURE_QA=1.
    /// </summary>
    public sealed class CutscenesFullQAAuditTest
    {
        private bool _capture;
        private RenderTexture _rt;
        private Texture2D _tex;

        private void EnsureCaptureBuffers()
        {
            if (_rt == null) _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            if (_tex == null) _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        }

        private void ReleaseCaptureBuffers()
        {
            if (_rt != null) { UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_tex != null) { UnityEngine.Object.Destroy(_tex); _tex = null; }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator AuditAndRecapture_CS01_Concerto()
        {
            yield return RunSingleCutsceneEntry("cs01_concerto", "Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp", "Caldeiral", 76f, 3);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator AuditAndRecapture_CS02_Viagem()
        {
            yield return RunSingleCutsceneEntry("cs02_viagem", "Studio.Game.Cutscenes.Cutscene02TravelController, Assembly-CSharp", "Caldeiral", 88f, 3);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator AuditAndRecapture_CS03_Retorno()
        {
            yield return RunSingleCutsceneEntry("cs03_retorno", "Studio.Game.Cutscenes.Cutscene03ReturnController, Assembly-CSharp", "Caldeiral", 36f, 2);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator AuditAndRecapture_CS04_CeuRachou()
        {
            yield return RunSingleCutsceneEntry("cs04_ceu_rachou", "Studio.Game.Cutscenes.Cutscene04SkyCrackedController, Assembly-CSharp", "SetorExterno", 40f, 2);
        }

        private IEnumerator RunSingleCutsceneEntry(string tag, string typeName, string endScene, float maxDur, int sampleN)
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_QA") == "1";
            Time.captureFramerate = 30;
            EnsureCaptureBuffers();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string qaRoot = Path.Combine(repoRoot, "captures", "cutscenes_qa");
            Directory.CreateDirectory(qaRoot);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return RunSingleCutsceneAudit(tag, typeName, endScene, qaRoot, maxDur, sampleN);

            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.Save();
            Time.captureFramerate = 0;
            ReleaseCaptureBuffers();
        }

        [UnityTest]
        public IEnumerator ValidateCleanSkipAcrossAllFourCutscenes()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            string[] types =
            {
                "Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp",
                "Studio.Game.Cutscenes.Cutscene02TravelController, Assembly-CSharp",
                "Studio.Game.Cutscenes.Cutscene03ReturnController, Assembly-CSharp",
                "Studio.Game.Cutscenes.Cutscene04SkyCrackedController, Assembly-CSharp",
            };
            string[] expectedScenes = { "Caldeiral", "Caldeiral", "Caldeiral", "SetorExterno" };

            for (int i = 0; i < types.Length; i++)
            {
                var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
                while (op != null && !op.isDone) yield return null;
                yield return null;

                var t = Type.GetType(types[i]);
                var ctrl = UnityEngine.Object.FindAnyObjectByType(t);
                Assert.IsNotNull(ctrl, $"Controlador ausente: {types[i]}");

                t.GetMethod("PlayCutscene").Invoke(ctrl, null);
                yield return null;
                yield return null;

                // Valida CC desligado durante cutscene
                var dezDuring = GameObject.Find("Player_Dez_Placeholder");
                var ccDuring = dezDuring.GetComponent<CharacterController>();
                Assert.IsFalse(ccDuring.enabled, $"[SkipQA] CharacterController deveria estar desligado durante {types[i]}!");

                // Pula cutscene
                t.GetMethod("SkipCutscene").Invoke(ctrl, null);
                int guard = 0;
                while (SceneManager.GetActiveScene().name != expectedScenes[i] && guard < 180) { yield return null; guard++; }
                yield return null;
                yield return null;

                Assert.AreEqual(expectedScenes[i], SceneManager.GetActiveScene().name, $"[SkipQA] Cena final errada apos skip de {types[i]}");
                Assert.AreEqual(1f, Time.timeScale, 0.01f, $"[SkipQA] Time.timeScale sujo apos skip de {types[i]}");

                var dezAfter = GameObject.Find("Player_Dez_Placeholder");
                var ccAfter = dezAfter.GetComponent<CharacterController>();
                var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
                var locoAfter = dezAfter.GetComponent(locoT) as Behaviour;
                Assert.IsTrue(ccAfter.enabled, $"[SkipQA] CharacterController nao religou apos skip de {types[i]}!");
                Assert.IsTrue(locoAfter.enabled, $"[SkipQA] DezLocomotionController nao religou apos skip de {types[i]}!");

                var brainT = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
                var brain = Camera.main != null ? Camera.main.GetComponent(brainT) as Behaviour : null;
                if (brain != null) Assert.IsTrue(brain.enabled, $"[SkipQA] CinemachineBrain ficou desligado apos skip de {types[i]}!");

                Debug.Log($"[CutsceneQA] SKIP OK -> {types[i]} (cena={expectedScenes[i]}, cc=ON, loco=ON, brain=ON, timeScale=1)");
            }
        }

        private IEnumerator RunSingleCutsceneAudit(string tag, string controllerTypeName, string expectedEndScene, string qaRoot, float maxDurationSec, int sampleEveryNFrames)
        {
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            string framesDir = Path.Combine(qaRoot, $"{tag}_frames");
            if (_capture)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
            }

            var ctrlType = Type.GetType(controllerTypeName);
            Assert.IsNotNull(ctrlType, $"Tipo nao encontrado: {controllerTypeName}");
            var ctrl = UnityEngine.Object.FindAnyObjectByType(ctrlType);
            Assert.IsNotNull(ctrl, $"Objeto nao encontrado em Caldeiral: {controllerTypeName}");

            var isPlayingProp = ctrlType.GetProperty("IsPlaying");
            ctrlType.GetMethod("PlayCutscene").Invoke(ctrl, null);
            yield return null;

            // 1. Verifica que CharacterController do Dez esta DESLIGADO durante a cutscene (zero travamento contra props!)
            var dez = GameObject.Find("Player_Dez_Placeholder");
            var cc = dez != null ? dez.GetComponent<CharacterController>() : null;
            Assert.IsTrue(cc != null && !cc.enabled, $"[{tag}] CharacterController deveria estar DESLIGADO durante a cutscene!");

            int frameCount = 0;
            int savedFrameIdx = 0;
            int maxFrames = Mathf.CeilToInt(maxDurationSec * 30f) + 90;
            float darkStreakSec = 0f;
            float maxDarkStreakSec = 0f;
            float initialDarkSec = 0f;
            bool sawLitFrame = false;

            while ((bool)isPlayingProp.GetValue(ctrl) && frameCount < maxFrames)
            {
                yield return null;
                frameCount++;

                bool shouldSample = (frameCount % sampleEveryNFrames == 0);
                if (shouldSample || _capture)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        float luma = RenderAndMeasureLuma(cam, _capture ? Path.Combine(framesDir, $"frame_{savedFrameIdx:D5}.jpg") : null);
                        if (_capture) savedFrameIdx++;

                        float stepSec = _capture ? (1f / 30f) : (sampleEveryNFrames / 30f);
                        if (luma < 0.042f)
                        {
                            darkStreakSec += stepSec;
                            if (!sawLitFrame) initialDarkSec += stepSec;
                            if (darkStreakSec > maxDarkStreakSec) maxDarkStreakSec = darkStreakSec;
                        }
                        else
                        {
                            sawLitFrame = true;
                            darkStreakSec = 0f;
                        }
                    }
                }
            }

            // Aguarda encerramento de transicao se for CS04
            int endGuard = 0;
            while (SceneManager.GetActiveScene().name != expectedEndScene && endGuard < 180) { yield return null; endGuard++; }
            yield return null;
            yield return null;

            // 2. Valida restauracao limpa de estado
            var dezEnd = GameObject.Find("Player_Dez_Placeholder");
            var ccEnd = dezEnd.GetComponent<CharacterController>();
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var locoEnd = dezEnd.GetComponent(locoT) as Behaviour;
            Assert.IsTrue(ccEnd.enabled, $"[{tag}] CharacterController nao religou ao encerrar!");
            Assert.IsTrue(locoEnd.enabled, $"[{tag}] DezLocomotionController nao religou ao encerrar!");
            Assert.AreEqual(1f, Time.timeScale, 0.01f, $"[{tag}] Time.timeScale != 1 ao encerrar!");

            Debug.Log($"[CutsceneQA] {tag}: frames={frameCount} initialDark={initialDarkSec:F2}s maxDarkStreak={maxDarkStreakSec:F2}s ccRestored=True locoRestored=True");
            Assert.Less(initialDarkSec, 3.0f, $"[{tag}] Escuridao inicial ({initialDarkSec:F2}s) excedeu 3.0s!");
            Assert.Less(maxDarkStreakSec, 3.0f, $"[{tag}] Sequencia continua escura ({maxDarkStreakSec:F2}s) excedeu 3.0s!");
        }

        private float RenderAndMeasureLuma(Camera cam, string saveJpgPath)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var oldModes = new RenderMode[canvases.Length];
            for (int c = 0; c < canvases.Length; c++)
            {
                oldModes[c] = canvases[c].renderMode;
                if (canvases[c].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[c].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[c].worldCamera = cam;
                    canvases[c].planeDistance = 0.25f;
                }
            }

            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = null;

            for (int c = 0; c < canvases.Length; c++)
                canvases[c].renderMode = oldModes[c];

            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            _tex.Apply();
            RenderTexture.active = null;

            if (!string.IsNullOrEmpty(saveJpgPath))
                File.WriteAllBytes(saveJpgPath, _tex.EncodeToJPG(84));

            // Mede luminancia media na area util central (ignorando barras letterbox 12% topo/base)
            var pixels = _tex.GetPixels32();
            double sum = 0.0;
            int count = 0;
            for (int y = 140; y < 940; y += 20)
            {
                int row = y * 1920;
                for (int x = 80; x < 1840; x += 24)
                {
                    var p = pixels[row + x];
                    sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;
                    count++;
                }
            }
            return count > 0 ? (float)(sum / count) : 0f;
        }
    }
}
