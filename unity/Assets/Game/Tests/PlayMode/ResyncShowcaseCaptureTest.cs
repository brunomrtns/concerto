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
    /// GATE 6 (Re-Sincronia): Captura sequencia de frames mostrando a zona demo alternando 2x
    /// (Desincronizado -> Restaurado -> Desincronizado), exibindo o wireframe fantasma (0.8s),
    /// onda em anel, atordoamento do inimigo (1s) e mudanca de rota/colisao.
    /// </summary>
    public sealed class ResyncShowcaseCaptureTest
    {
        [UnityTest]
        public IEnumerator Resync_CaptureDualStateDemoFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "resincronia", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var zoneT  = Type.GetType("Studio.Game.Resync.ResyncZoneController, Assembly-CSharp");
            var enemyT = Type.GetType("Studio.Game.Combat.EstilhacoEnemy, Assembly-CSharp");
            var zone = zoneT.GetMethod("FindZoneById").Invoke(null, new object[] { "caldeiral_demo_resync" }) as Component;
            Assert.IsNotNull(zone);

            var enemyGo = new GameObject("Demo_ResyncEnemy");
            enemyGo.transform.position = zone.transform.position + new Vector3(0.4f, 0.2f, 2.2f);
            enemyGo.AddComponent(enemyT);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var camGo = new GameObject("[Resync_CaptureCam]");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = zone.transform.position + new Vector3(0f, 5.2f, -9.2f);
            cam.transform.LookAt(zone.transform.position + new Vector3(0f, 1.2f, 0f));
            cam.targetTexture = rt;

            int frameIdx = 0;
            void Snap()
            {
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{frameIdx:D4}.png"), tex.EncodeToPNG());
                frameIdx++;
            }

            try
            {
                // 1. Estado inicial (Desincronizado) — 12 frames
                for (int i = 0; i < 12; i++) { Snap(); yield return null; }

                // 2. Alternancia 1: Desincronizado -> Restaurado (mostra onda em anel + wireframe 0.8s + inimigo atordoado)
                zoneT.GetMethod("TryTriggerResync").Invoke(zone, null);
                float t = 0f;
                while (t < 2.2f)
                {
                    Snap();
                    yield return null;
                    t += Time.deltaTime;
                }

                // 3. Alternancia 2: Restaurado -> Desincronizado (limpa cooldown para demo continua em video)
                zoneT.GetMethod("ForceClearCooldownForTest").Invoke(zone, null);
                zoneT.GetMethod("TryTriggerResync").Invoke(zone, null);
                t = 0f;
                while (t < 2.2f)
                {
                    Snap();
                    yield return null;
                    t += Time.deltaTime;
                }

                Assert.GreaterOrEqual(frameIdx, 40, "Devem ser capturados >= 40 frames da demonstracao Re-Sincronia.");
            }
            finally
            {
                UnityEngine.Object.Destroy(camGo);
                UnityEngine.Object.Destroy(enemyGo);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
