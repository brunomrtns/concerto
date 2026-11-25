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
    /// Runner PlayMode determinista para Regressao Visual (GATES 1, 2 e 5 — ZERO alteracao de gameplay).
    /// Captura 6 screenshots 1080p fixos pelos atos do jogo em:
    ///   - captures/regressao/baseline/
    ///   - captures/regressao/2026-11-25_prova_diff/ (com 1 cubo visivel temporario em memoria na bancada do Ato 1)
    /// </summary>
    public sealed class VisualRegressionCaptureRunnerTest
    {
        private readonly struct ShotSpec
        {
            public readonly string FileName;
            public readonly string SceneName;
            public readonly Vector3 CamPos;
            public readonly Vector3 LookAt;
            public readonly float Fov;

            public ShotSpec(string fileName, string sceneName, Vector3 camPos, Vector3 lookAt, float fov)
            {
                FileName = fileName;
                SceneName = sceneName;
                CamPos = camPos;
                LookAt = lookAt;
                Fov = fov;
            }
        }

        private static readonly ShotSpec[] Shots =
        {
            new ShotSpec("01_mainmenu_title.png",          "MainMenu",     new Vector3(0f, 1.0f, -10f),       new Vector3(0f, 1.0f, 0f),     60f),
            new ShotSpec("02_ato1_caldeiral_bancada.png",  "Caldeiral",    new Vector3(-25.6f, 2.2f, -20.2f), new Vector3(-29.2f, 1.0f, -16.4f), 55f),
            new ShotSpec("03_ato1_caldeiral_corredor.png", "Caldeiral",    new Vector3(-31.5f, 2.4f, -18.5f), new Vector3(-22.0f, 1.1f, -18.5f), 55f),
            new ShotSpec("04_ato3_setorexterno_patio.png", "SetorExterno", new Vector3(-6.0f, 3.8f, -10.0f),  new Vector3(4.0f, 1.2f, 2.0f),  58f),
            new ShotSpec("05_ato4_epicentro_travessia.png","Epicentro",    new Vector3(-6.0f, 4.0f, -5.0f),   new Vector3(16.0f, 0.8f, 0.0f), 58f),
            new ShotSpec("06_ato4_epicentro_arena.png",    "Epicentro",    new Vector3(96.0f, 5.2f, -8.5f),   new Vector3(116.0f, 1.4f, 0.0f), 60f)
        };

        [UnityTest]
        public IEnumerator VisualRegression_CaptureBaselineAndDiffProof()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "regressao"));
            string baseDir = Path.Combine(root, "baseline");
            string diffDir = Path.Combine(root, "2026-11-25_prova_diff");
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
            if (Directory.Exists(diffDir)) Directory.Delete(diffDir, true);
            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(diffDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            try
            {
                yield return CaptureSet(baseDir, injectDiff: false, rt, tex);
                yield return CaptureSet(diffDir, injectDiff: true, rt, tex);

                Assert.AreEqual(6, Directory.GetFiles(baseDir, "*.png").Length, "Baseline deve conter 6 screenshots fixos.");
                Assert.AreEqual(6, Directory.GetFiles(diffDir, "*.png").Length, "Diff run deve conter 6 screenshots fixos.");
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }

        private IEnumerator CaptureSet(string outDir, bool injectDiff, RenderTexture rt, Texture2D tex)
        {
            string loadedScene = string.Empty;
            for (int i = 0; i < Shots.Length; i++)
            {
                var spec = Shots[i];
                if (spec.SceneName != loadedScene)
                {
                    var op = SceneManager.LoadSceneAsync(spec.SceneName, LoadSceneMode.Single);
                    while (!op.isDone) yield return null;
                    for (int w = 0; w < 5; w++) yield return null;
                    loadedScene = spec.SceneName;
                }

                GameObject tempDiff = null;
                if (injectDiff && spec.FileName == "02_ato1_caldeiral_bancada.png")
                {
                    tempDiff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tempDiff.name = "__VR_TEMP_DIFF_MARKER__";
                    tempDiff.transform.position = new Vector3(-28.6f, 1.35f, -17.1f);
                    tempDiff.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
                    var r = tempDiff.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.SetColor("_BaseColor", new Color(1.0f, 0.12f, 0.62f, 1f));
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", new Color(2.4f, 0.25f, 1.2f, 1f));
                        r.sharedMaterial = mat;
                    }
                    yield return null;
                }

                var camGo = new GameObject("__VR_FixedCam__");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = spec.Fov;
                cam.farClipPlane = 450f;
                cam.transform.position = spec.CamPos;
                cam.transform.LookAt(spec.LookAt);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                tex.Apply(false);
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(outDir, spec.FileName), tex.EncodeToPNG());

                UnityEngine.Object.Destroy(camGo);
                if (tempDiff != null) UnityEngine.Object.Destroy(tempDiff);
                yield return null;
            }
        }
    }
}
