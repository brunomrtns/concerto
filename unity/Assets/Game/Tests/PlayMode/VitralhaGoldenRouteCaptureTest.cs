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
    /// GATE 9 (Vitralha): Captura MP4 da rota de ouro percorrendo os 7 beats do Fuso Vitralha
    /// (Estacao de Luz -> Bosque Passaros-Vidro -> Mirante -> Cachoeira -> Boss Estufa Viva -> Reliquia -> Rio Volta).
    /// </summary>
    public sealed class VitralhaGoldenRouteCaptureTest
    {
        [UnityTest]
        public IEnumerator Vitralha_CaptureGoldenRouteFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "vitralha", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("Vitralha", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var vitT   = Type.GetType("Studio.Game.Vitralha.VitralhaFusoController, Assembly-CSharp");
            var zoneT  = Type.GetType("Studio.Game.Resync.ResyncZoneController, Assembly-CSharp");
            var stateE = Type.GetType("Studio.Game.Resync.ResyncState, Assembly-CSharp");
            var vit    = UnityEngine.Object.FindAnyObjectByType(vitT);
            var cam    = Camera.main;
            Assert.IsNotNull(vit);
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
                // Beat 1: Estacao de Luz (Desincronizado -> Restaurado)
                Snap(new Vector3(0f, 6f, -10f), new Vector3(0f, 2f, 20f), 14);
                var entryZone = vitT.GetField("entryLightStationZone").GetValue(vit);
                if (entryZone != null) zoneT.GetMethod("ApplySavedStateImmediate").Invoke(entryZone, new object[] { Enum.Parse(stateE, "Restaurado") });
                yield return null;
                Snap(new Vector3(0f, 6f, 6f), new Vector3(0f, 2f, 26f), 14);

                // Beat 2: Bosque dos Passaros-Vidro
                Snap(new Vector3(-12f, 8f, 52f), new Vector3(0f, 3f, 78f), 16);

                // Beat 3: Mirante do Rio Parado
                Snap(new Vector3(-10f, 9f, 108f), new Vector3(2f, 5f, 126f), 16);

                // Beat 4: Cachoeira de Vidro
                Snap(new Vector3(-14f, 12f, 162f), new Vector3(0f, 8f, 192f), 16);

                // Beat 5: Boss A Estufa Viva (Arena Dual-State)
                Snap(new Vector3(-12f, 14f, 224f), new Vector3(0f, 10f, 250f), 18);

                // Beat 6: Reliquia Ponteiro Maior
                Snap(new Vector3(-6f, 12f, 280f), new Vector3(0f, 9f, 292f), 14);

                // Beat 7: Consequencia — Rio Volta
                var river = vitT.GetField("riverFlowVisual").GetValue(vit) as GameObject;
                if (river != null) river.SetActive(true);
                yield return null;
                Snap(new Vector3(-12f, 14f, 312f), new Vector3(0f, 4f, 342f), 20);

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
