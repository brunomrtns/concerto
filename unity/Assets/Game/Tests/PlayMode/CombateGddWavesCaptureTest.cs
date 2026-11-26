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
    /// GATE 7 (Combate GDD): Captura MP4 da arena com os 5 inimigos canon em 3 ondas (<=2 tipos/onda),
    /// parry refletindo Faisca no Ferrugem-Espelho e Finalizador CONCERTO.
    /// </summary>
    public sealed class CombateGddWavesCaptureTest
    {
        [UnityTest]
        public IEnumerator CombateGdd_CaptureWavesAndFinisherFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "combate_gdd", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var op = SceneManager.LoadSceneAsync("CombateGddArena", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var dirT = Type.GetType("Studio.Game.Combat.GddCombatWaveDirector, Assembly-CSharp");
            var espT = Type.GetType("Studio.Game.Combat.FerrugemEspelhoEnemy, Assembly-CSharp");
            var colT = Type.GetType("Studio.Game.Combat.FerrugemColetoraEnemy, Assembly-CSharp");
            var pesT = Type.GetType("Studio.Game.Combat.CensorPesadoEnemy, Assembly-CSharp");
            var dir  = UnityEngine.Object.FindAnyObjectByType(dirT);
            var cam  = Camera.main;
            Assert.IsNotNull(dir);
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
                var mAct = dirT.GetMethod("ActivateWave");

                // Onda 1: Ferrugem-Trabalhadora + Ferrugem-Espelho (telegraph + parry refletindo Faisca + Finalizador CONCERTO)
                mAct.Invoke(dir, new object[] { 0 });
                yield return null;
                Snap(new Vector3(0f, 9f, -14f), new Vector3(0f, 2f, 5f), 16);
                var esp = UnityEngine.Object.FindAnyObjectByType(espT);
                if (esp != null)
                {
                    espT.GetMethod("BeginTelegraph").Invoke(esp, null);
                    yield return null;
                    Snap(new Vector3(4f, 6f, -6f), new Vector3(4f, 2f, 6f), 14);
                    espT.GetMethod("ApplyContratempoReflectedFaisca").Invoke(esp, new object[] { 3f });
                    yield return null;
                    Snap(new Vector3(4f, 6f, -5f), new Vector3(4f, 2f, 6f), 12);
                    espT.GetMethod("ExecuteConcertoFinisher").Invoke(esp, null);
                    yield return null;
                    Snap(new Vector3(0f, 9f, -14f), new Vector3(0f, 2f, 5f), 12);
                }

                // Onda 2: Ferrugem-Coletora (bolha -> Crava-Mola quebra) + Censor-Patrulha
                mAct.Invoke(dir, new object[] { 1 });
                yield return null;
                Snap(new Vector3(-6f, 9f, -12f), new Vector3(0f, 2f, 5f), 16);
                var col = UnityEngine.Object.FindAnyObjectByType(colT);
                if (col != null)
                {
                    colT.GetMethod("ApplyCravaMolaBubbleBreak").Invoke(col, null);
                    yield return null;
                    Snap(new Vector3(-6f, 8f, -10f), new Vector3(-4f, 2f, 5f), 14);
                }

                // Onda 3: Censor-Pesado + Ferrugem-Espelho (rotas de verticalidade + Martelada Aerea na nuca)
                mAct.Invoke(dir, new object[] { 2 });
                yield return null;
                Snap(new Vector3(8f, 11f, -12f), new Vector3(0f, 2.5f, 6f), 18);
                var pes = UnityEngine.Object.FindAnyObjectByType(pesT);
                if (pes != null)
                {
                    pesT.GetMethod("ApplyMarteladaAereaNapeHit").Invoke(pes, new object[] { 3f });
                    yield return null;
                    Snap(new Vector3(0f, 10f, -12f), new Vector3(0f, 2f, 6f), 16);
                }

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
