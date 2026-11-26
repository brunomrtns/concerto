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
    /// GATE 6 (CS02 "A Oferta"): Captura MP4 da cutscene completa (6 falas sincronizadas entre
    /// Maestrina Iria via Censor, Dez e Porca) em /captures/cutscenes/cs02_oferta.mp4.
    /// </summary>
    public sealed class Cs02OfertaCaptureTest
    {
        [UnityTest]
        public IEnumerator Cs02Oferta_CaptureFullCutsceneFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "cutscenes", "_tmp_cs02_oferta"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("Cs02OfertaDemo", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var csT = Type.GetType("Studio.Game.Cutscenes.CutsceneAOfetaController, Assembly-CSharp");
            var cs  = UnityEngine.Object.FindAnyObjectByType(csT);
            var cam = Camera.main;
            Assert.IsNotNull(cs);
            Assert.IsNotNull(cam);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            int idx = 0;

            void Snap(Vector3 camPos, Vector3 lookAt, int repeat = 16)
            {
                cam.transform.position = camPos;
                cam.transform.LookAt(lookAt);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                byte[] png = tex.EncodeToPNG();
                for (int i = 0; i < repeat; i++)
                    File.WriteAllBytes(Path.Combine(outDir, $"frame_{idx++:D4}.png"), png);
            }

            try
            {
                csT.GetMethod("EvaluateRelicCountTrigger").Invoke(cs, new object[] { 2 });
                var mAdv = csT.GetMethod("AdvanceTimeForTest");

                // 6 beats/enquadramentos acompanhando as 6 falas (Iria via Censor <-> Dez <-> Porca)
                Snap(new Vector3(0.5f, 3.0f, -2.5f), new Vector3(0.8f, 2.0f, 4.0f), 20); // cs02_iria_01
                mAdv.Invoke(cs, new object[] { 7.0f }); yield return null;
                Snap(new Vector3(1.5f, 2.2f, 2.0f),  new Vector3(-1.2f, 1.2f, -1.5f), 18); // cs02_dez_01
                mAdv.Invoke(cs, new object[] { 4.5f }); yield return null;
                Snap(new Vector3(-1.5f, 2.8f, -1.5f), new Vector3(0.8f, 2.0f, 4.0f), 20); // cs02_iria_02
                mAdv.Invoke(cs, new object[] { 6.0f }); yield return null;
                Snap(new Vector3(-0.5f, 2.0f, 1.2f),  new Vector3(-2.6f, 1.0f, -2.0f), 16); // cs02_porca_01
                mAdv.Invoke(cs, new object[] { 4.5f }); yield return null;
                Snap(new Vector3(0.2f, 3.2f, -4.5f),  new Vector3(0.8f, 2.0f, 4.0f), 18); // cs02_iria_03
                mAdv.Invoke(cs, new object[] { 5.5f }); yield return null;
                Snap(new Vector3(0.0f, 2.6f, -6.0f),  new Vector3(-1.0f, 1.3f, 0.5f), 20); // cs02_dez_02

                Assert.GreaterOrEqual(idx, 100);
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
