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
    /// GATE 7 (Hub A Orquestra): Captura frames do Hub, abertura da Tela de Fusos,
    /// inspecao de Beneficio/Custo, confirmacao de conserto e chegada do NPC na sala de concertos.
    /// </summary>
    public sealed class HubOrquestraShowcaseCaptureTest
    {
        [UnityTest]
        public IEnumerator HubOrquestra_CaptureDemoFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "hub", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("AOrquestra", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var hubT = Type.GetType("Studio.Game.Hub.HubOrquestraController, Assembly-CSharp");
            var uiT  = Type.GetType("Studio.Game.Hub.TelaDeFusosUI, Assembly-CSharp");
            var hub  = UnityEngine.Object.FindAnyObjectByType(hubT);
            var ui   = UnityEngine.Object.FindAnyObjectByType(uiT);
            var cam  = Camera.main;
            Assert.IsNotNull(hub);
            Assert.IsNotNull(ui);
            Assert.IsNotNull(cam);

            // Para que a Screen Space Overlay Canvas apareca no RenderTexture da camera durante batchmode,
            // alterna temporariamente para ScreenSpaceCamera apontando para a MainCamera.
            var canvas = (ui as Component).GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1.0f;
            }

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            int idx = 0;

            void Snap(int repeat = 1)
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                byte[] bytes = tex.EncodeToPNG();
                for (int r = 0; r < repeat; r++)
                    File.WriteAllBytes(Path.Combine(outDir, $"frame_{idx++:D4}.png"), bytes);
            }

            try
            {
                // 1. Vista da antiga sala de concertos antes das restauracoes (0 NPCs)
                Snap(18);

                // 2. Abre Tela de Fusos -> seleciona Caldeiral (le Beneficio/Custo)
                uiT.GetMethod("Open").Invoke(ui, null);
                uiT.GetMethod("SelectFuso").Invoke(ui, new object[] { "caldeiral" });
                yield return null;
                Snap(18);

                // 3. Seleciona Vitralha (le Beneficio/Custo) e confirma conserto!
                uiT.GetMethod("SelectFuso").Invoke(ui, new object[] { "vitralha" });
                yield return null;
                Snap(18);

                uiT.GetMethod("ConfirmSelectedFuso").Invoke(ui, null);
                yield return null;
                Snap(18);

                // 4. Confirma Caldeiral tambem -> fecha Tela de Fusos -> ve Vo Segunda e Tio Horario no Hub!
                uiT.GetMethod("SelectFuso").Invoke(ui, new object[] { "caldeiral" });
                uiT.GetMethod("ConfirmSelectedFuso").Invoke(ui, null);
                yield return null;
                Snap(12);

                uiT.GetMethod("Close").Invoke(ui, null);
                yield return null;
                Snap(24);

                Assert.GreaterOrEqual(idx, 90);
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
