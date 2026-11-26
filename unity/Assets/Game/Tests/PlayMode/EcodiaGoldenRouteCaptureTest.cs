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
    /// GATE 6 (Ecodia): Captura MP4 da rota de ouro mostrando:
    ///   - ANTES (Desincronizado): Loop de 6s rodando (bonde avancando em 6s + porta abrindo no t=2s + moradores repetindo gestos)
    ///   - Boss Eco Dez + Reliquia Ponteiro Menor (Micra)
    ///   - DEPOIS (Restaurado): Cidade viva acordada + Censores patrulhando em tempo real.
    /// </summary>
    public sealed class EcodiaGoldenRouteCaptureTest
    {
        [UnityTest]
        public IEnumerator Ecodia_CaptureGoldenRouteFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "ecodia", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("Ecodia", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var ecoT  = Type.GetType("Studio.Game.Ecodia.EcodiaFusoController, Assembly-CSharp");
            var loopT = Type.GetType("Studio.Game.Ecodia.EcodiaTimeLoopSystem, Assembly-CSharp");
            var eco   = UnityEngine.Object.FindAnyObjectByType(ecoT);
            var loop  = UnityEngine.Object.FindAnyObjectByType(loopT);
            var cam   = Camera.main;
            Assert.IsNotNull(eco);
            Assert.IsNotNull(loop);
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
                // Beat 1: Estacao dos 6s (Art Deco azul-madrugada)
                Snap(new Vector3(0f, 8f, -10f), new Vector3(0f, 3f, 22f), 14);

                // ANTES — Beat 2: Loop de 6s em movimento (bonde cruza trilho e porta abre em t=2.0s)
                var mSetT = loopT.GetMethod("SetLoopTimeForTest");
                for (int step = 0; step < 12; step++)
                {
                    float t = step * 0.5f; // 0.0s .. 5.5s
                    mSetT.Invoke(loop, new object[] { t });
                    yield return null;
                    Snap(new Vector3(-12f, 9f, 44f), new Vector3(0f, 3f, 76f), 4);
                }

                // Beat 3: Praca Neon (moradores presos no loop)
                Snap(new Vector3(-12f, 8f, 108f), new Vector3(0f, 3f, 134f), 14);

                // Beat 5: Arena Boss Eco Dez
                Snap(new Vector3(-12f, 10f, 214f), new Vector3(0f, 3f, 244f), 18);

                // Beat 6: Reliquia Ponteiro Menor (Micra)
                var relic = ecoT.GetField("relicPonteiroMenorObject").GetValue(eco) as GameObject;
                if (relic != null) relic.SetActive(true);
                yield return null;
                Snap(new Vector3(-8f, 8f, 270f), new Vector3(0f, 2f, 286f), 14);

                // DEPOIS — Beat 7: Cidade Acordada (loop quebrado + Censores em tempo real)
                loopT.GetMethod("BreakLoopAndWakeDistrict").Invoke(loop, null);
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
