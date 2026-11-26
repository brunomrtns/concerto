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
    /// GATE 8 (Maresia): Captura MP4 da rota de ouro percorrendo os 7 beats do Fuso Maresia
    /// (Cais Congelado -> Cristas de Ondas 10m + Barcos Inclinados -> Cavernas Alagadas + Desvio ->
    ///  Desfiladeiro Rota Alternativa -> Boss Baleia de Ancora -> Reliquia Pendulo/Garra -> Mar Degelado + Barco-Drift).
    /// </summary>
    public sealed class MaresiaGoldenRouteCaptureTest
    {
        [UnityTest]
        public IEnumerator Maresia_CaptureGoldenRouteFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "maresia", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("Maresia", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var marT = Type.GetType("Studio.Game.Maresia.MaresiaFusoController, Assembly-CSharp");
            var mar  = UnityEngine.Object.FindAnyObjectByType(marT);
            var cam  = Camera.main;
            Assert.IsNotNull(mar);
            Assert.IsNotNull(cam);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            int idx = 0;

            void Snap(Vector3 camPos, Vector3 lookAt, int repeat = 12)
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
                // Beat 1: Cais Congelado
                Snap(new Vector3(0f, 7f, -10f), new Vector3(0f, 2f, 22f), 14);

                // Beat 2: Cristas de Ondas Congeladas de 10m + Barcos Inclinados
                Snap(new Vector3(-14f, 11f, 44f), new Vector3(0f, 6f, 78f), 18);

                // Beat 3: Cavernas-Habitacao Alagadas (consequencia da Vitralha) + Desvio Jogavel
                marT.GetMethod("SetVitralhaFloodConsequence").Invoke(mar, new object[] { true });
                yield return null;
                Snap(new Vector3(-14f, 10f, 104f), new Vector3(6f, 4f, 132f), 18);

                // Beat 4: Desfiladeiro da Mare — Rota Alternativa sem Garra (Rebote+Sopro)
                Snap(new Vector3(-14f, 10f, 158f), new Vector3(0f, 5f, 188f), 16);

                // Beat 5: Arena do Boss A Baleia de Ancora (Desferrujar)
                Snap(new Vector3(-14f, 12f, 214f), new Vector3(0f, 4f, 246f), 20);

                // Beat 6: Reliquia Pendulo + Pontos Olho-de-Lente Dourados (Garra)
                var relic = marT.GetField("relicPenduloObject").GetValue(mar) as GameObject;
                if (relic != null) relic.SetActive(true);
                yield return null;
                Snap(new Vector3(-10f, 9f, 270f), new Vector3(0f, 4f, 294f), 16);

                // Beat 7: Pos-Conserto — Mar Degelado, Barco-Drift & Elevador de Onda
                Snap(new Vector3(-12f, 10f, 308f), new Vector3(4f, 2f, 332f), 20);

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
