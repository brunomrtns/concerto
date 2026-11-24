using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class Cutscene01ValidationAndCaptureTest
    {
        [UnityTest]
        public IEnumerator ValidateSkipReplayAndCaptureCutscene01()
        {
            bool fullCapture = Environment.GetEnvironmentVariable("CAPTURE_CS01") == "1";

            // Garante que a cutscene rode no teste
            PlayerPrefs.DeleteKey("CONCERTO_CS01_PLAYED");
            PlayerPrefs.Save();

            Time.captureFramerate = fullCapture ? 30 : 60;

#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Game/Scenes/Caldeiral.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
#else
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
#endif
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var csGo = GameObject.Find("CS01_Opening_Concerto");
            Assert.IsNotNull(csGo, "CS01_Opening_Concerto nao encontrado em Caldeiral.unity!");

            var mgrType = Type.GetType("Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp");
            Assert.IsNotNull(mgrType, "CutsceneManager nao encontrado!");
            var mgr = csGo.GetComponent(mgrType);
            Assert.IsNotNull(mgr);

            var isPlayingProp = mgrType.GetProperty("IsPlaying");
            var skipMethod    = mgrType.GetMethod("SkipCutscene");
            var replayMethod  = mgrType.GetMethod("ReplayCutscene");
            var director      = csGo.GetComponent<PlayableDirector>();

            // Verifica que iniciou automaticamente no primeiro boot!
            Assert.IsTrue((bool)isPlayingProp.GetValue(mgr), "Cutscene 01 deveria iniciar automaticamente no primeiro boot!");

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string capDir   = Path.Combine(repoRoot, "captures", "cs01_abertura");
            string framesDir = Path.Combine(capDir, "frames_raw");

            if (fullCapture)
            {
                Directory.CreateDirectory(capDir);
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);

                // 5 screenshots de alta qualidade (1 por Beat!)
                var shotTimes = new (float t, string name)[]
                {
                    (4.5f,  "01_beat1_bancada_foto_caixinha.png"),
                    (16.0f, "02_beat2_oficina_maquina_lona.png"),
                    (33.5f, "03_beat3_checklist_juno_dez.png"),
                    (50.5f, "04_beat4_foto_emocional_eco.png"),
                    (65.5f, "05_beat5_maquina_titlecard_concerto.png"),
                };
                int nextShot = 0;

                int frameIdx = 0;
                float fpsSum = 0f;
                int fpsSamples = 0;
                float lastReal = Time.realtimeSinceStartup;

                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                var uiCanvas = csGo.GetComponentInChildren<Canvas>(true);
                RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
                if (uiCanvas != null && cam != null)
                {
                    oldMode = uiCanvas.renderMode;
                    uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    uiCanvas.worldCamera = cam;
                    uiCanvas.planeDistance = 0.25f;
                }

                int w = 1920, h = 1080;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);

                // Grava toda a cutscene (~72s @ 30fps = ~2160 frames) incluindo UI (letterbox, legendas, title card!)
                while ((bool)isPlayingProp.GetValue(mgr) && frameIdx < 2250)
                {
                    yield return null;

                    float now = Time.realtimeSinceStartup;
                    float rdt = now - lastReal;
                    lastReal = now;
                    if (rdt > 0.0001f) { fpsSum += 1f / rdt; fpsSamples++; }

                    double curT = director != null ? director.time : (frameIdx / 30.0);

                    if (cam != null)
                    {
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        cam.targetTexture = null;
                        RenderTexture.active = null;

                        // Salva JPG q90 para frames do video (super rapido!) e PNG lossless para os 5 screenshots!
                        File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), tex.EncodeToJPG(92));

                        if (nextShot < shotTimes.Length && curT >= shotTimes[nextShot].t)
                        {
                            File.WriteAllBytes(Path.Combine(capDir, shotTimes[nextShot].name), tex.EncodeToPNG());
                            nextShot++;
                        }
                    }
                    frameIdx++;
                }

                // Captura mais 45 frames pos-cutscene (1.5s) mostrando corte limpo pra gameplay + objetivo "Fale com Juno"
                for (int post = 0; post < 45; post++)
                {
                    yield return null;
                    if (cam != null)
                    {
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        cam.targetTexture = null;
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), tex.EncodeToJPG(92));
                    }
                    frameIdx++;
                }

                if (uiCanvas != null) uiCanvas.renderMode = oldMode;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);

                float avgFps = fpsSamples > 0 ? fpsSum / fpsSamples : 0f;
                File.WriteAllText(
                    Path.Combine(capDir, "capture_stats.json"),
                    $"{{\n  \"frames\": {frameIdx},\n  \"fps_capture_rate\": 30,\n  \"avg_editor_fps_during_capture\": {avgFps:F1},\n  \"screenshots_saved\": {nextShot}\n}}\n"
                );
            }
            else
            {
                // Modo rapido (Gate de Skip + Replay): avanca 20 frames, pula no meio, verifica controle restaurado!
                for (int i = 0; i < 20; i++) yield return null;
                skipMethod.Invoke(mgr, null);
                yield return null;
            }

            // GATE 3: Verifica estado de gameplay 100% restaurado!
            Assert.IsFalse((bool)isPlayingProp.GetValue(mgr), "Cutscene deveria estar parada apos fim/skip.");
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f, "Time.timeScale precisa ser 1.0 apos cutscene.");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = (MonoBehaviour)dezGo.GetComponent(locoType);
            Assert.IsTrue(loco.enabled, "DezLocomotionController precisa estar ativo apos cutscene!");

            var lightRig = GameObject.Find("CS01_CutsceneLightRig");
            Assert.IsNull(lightRig, "CS01_CutsceneLightRig precisa estar desativado fora da cutscene!");

            // GATE 4: Testa Replay da intro + Skip no meio!
            replayMethod.Invoke(mgr, null);
            yield return null;
            Assert.IsTrue((bool)isPlayingProp.GetValue(mgr), "ReplayCutscene() deveria reiniciar a Cutscene 01!");
            for (int i = 0; i < 10; i++) yield return null;
            skipMethod.Invoke(mgr, null);
            yield return null;
            Assert.IsFalse((bool)isPlayingProp.GetValue(mgr), "Skip durante replay deveria encerrar limpo!");
            Assert.IsTrue(loco.enabled, "Controle do jogador precisa voltar ativo apos skip no replay!");

            Time.captureFramerate = 0;
        }
    }
}
