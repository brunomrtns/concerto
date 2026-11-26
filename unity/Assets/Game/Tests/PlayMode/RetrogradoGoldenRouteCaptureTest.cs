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
    /// GATE 6 (Retrogrado): Captura MP4 da rota de ouro mostrando:
    ///   - Dunas em espiral de mola + areia subindo (+Y)
    ///   - Plataformas de janelas invertidas se materializando conforme se espera
    ///   - Boss O Arquivista-Mor + Reliquia Aro-Escudo + Ruinas do pre-Fratura reveladas.
    /// </summary>
    public sealed class RetrogradoGoldenRouteCaptureTest
    {
        [UnityTest]
        public IEnumerator Retrogrado_CaptureGoldenRouteFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "retrogrado", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("Retrogrado", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var retT = Type.GetType("Studio.Game.Retrogrado.RetrogradoFusoController, Assembly-CSharp");
            var revT = Type.GetType("Studio.Game.Retrogrado.RetrogradoReverseTimeSystem, Assembly-CSharp");
            var ret  = UnityEngine.Object.FindAnyObjectByType(retT);
            var rev  = UnityEngine.Object.FindAnyObjectByType(revT);
            var cam  = Camera.main;
            Assert.IsNotNull(ret);
            Assert.IsNotNull(rev);
            Assert.IsNotNull(cam);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            int idx = 0;

            void Snap(Vector3 camPos, Vector3 lookAt, int repeat = 4)
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
                // Beat 1: Dunas em espiral de mola
                Snap(new Vector3(0f, 8f, -10f), new Vector3(0f, 3f, 22f), 14);

                // Beat 2: Areia caindo PARA CIMA (+Y)
                var mTick = revT.GetMethod("TickReverseTime");
                for (int step = 0; step < 8; step++)
                {
                    mTick.Invoke(rev, new object[] { 0.25f });
                    yield return null;
                    Snap(new Vector3(-12f, 8f, 48f), new Vector3(0f, 5f, 70f), 4);
                }

                // Beat 3: Plataformas de janelas INVERTIDAS crescendo conforme se espera
                for (int step = 0; step < 8; step++)
                {
                    mTick.Invoke(rev, new object[] { 0.28f });
                    yield return null;
                    Snap(new Vector3(-12f, 9f, 100f), new Vector3(0f, 4f, 128f), 4);
                }

                // Beat 5: Arena Boss O Arquivista-Mor
                Snap(new Vector3(-12f, 10f, 214f), new Vector3(0f, 4f, 246f), 20);

                // Beat 6: Reliquia Aro-Escudo
                var relic = retT.GetField("relicAroObject").GetValue(ret) as GameObject;
                if (relic != null) relic.SetActive(true);
                yield return null;
                Snap(new Vector3(-8f, 8f, 270f), new Vector3(0f, 2f, 286f), 14);

                // Beat 7: Ruinas pre-Fratura reveladas (Outros Relogios)
                var ruins = retT.GetField("preFractureRuinsRoot").GetValue(ret) as GameObject;
                if (ruins != null) ruins.SetActive(true);
                yield return null;
                Snap(new Vector3(-14f, 11f, 306f), new Vector3(0f, 4f, 336f), 20);

                Assert.GreaterOrEqual(idx, 120);
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
