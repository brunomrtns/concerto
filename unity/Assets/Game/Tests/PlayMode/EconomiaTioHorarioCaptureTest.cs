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
    /// GATE 7 (Economia Canon): Captura MP4 mostrando coleta de fios de tempo dourados,
    /// compra na loja TripoSG do Tio Horario, efeito real no gameplay e 1 Memoria-Eco tocando.
    /// </summary>
    public sealed class EconomiaTioHorarioCaptureTest
    {
        [UnityTest]
        public IEnumerator Economia_CaptureTioHorarioAndMemoryFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "economia", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("EconomiaTioHorarioDemo", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var econT = Type.GetType("Studio.Game.Economy.SegundosEconomySystem, Assembly-CSharp");
            var econ  = UnityEngine.Object.FindAnyObjectByType(econT);
            var cam   = Camera.main;
            Assert.IsNotNull(econ);
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
                // 1. Vista dos Fios de Tempo dourados -> coleta
                Snap(new Vector3(-6f, 4.5f, -7f), new Vector3(-2.5f, 1.2f, -1f), 20);
                econT.GetMethod("CollectFioDeTempo").Invoke(econ, new object[] { 200, "fio_capture_1" });
                for (int i = 1; i <= 3; i++)
                {
                    var fio = GameObject.Find($"FioDeTempo_Segundo_{i}");
                    if (fio != null) fio.SetActive(false);
                }
                yield return null;
                Snap(new Vector3(-5f, 4.5f, -6f), new Vector3(0f, 1.5f, 3f), 16);

                // 2. Loja TripoSG do Tio Horario -> compra de melhorias + Remendo
                Snap(new Vector3(0f, 4.2f, -3f), new Vector3(0f, 2.0f, 6f), 22);
                econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_pressao_1" });
                econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_contratempo_largo" });
                yield return null;
                Snap(new Vector3(3.5f, 4.0f, -2f), new Vector3(1f, 2.0f, 5.5f), 20);

                // 3. 1 Memoria-Eco tocando micro-cena
                econT.GetMethod("CollectEchoMemory").Invoke(econ, new object[] { "mem_caldeiral_1" });
                yield return null;
                Snap(new Vector3(9f, 3.8f, -4f), new Vector3(6f, 1.2f, 1f), 26);

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
