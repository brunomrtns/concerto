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
    /// Runner PlayMode determinista para capturar 30s de camera (180 frames @ 6fps -> 30.0s MP4)
    /// nos pontos de maior incidencia de Z-Fighting (Antes/Depois) sem alterar gameplay.
    /// </summary>
    public sealed class ZFightingCaptureRunnerTest
    {
        private readonly struct OrbitShot
        {
            public readonly string SceneName;
            public readonly Vector3 Pivot;
            public readonly float Radius;
            public readonly float Height;

            public OrbitShot(string sceneName, Vector3 pivot, float radius, float height)
            {
                SceneName = sceneName;
                Pivot = pivot;
                Radius = radius;
                Height = height;
            }
        }

        private static readonly OrbitShot[] Shots =
        {
            new OrbitShot("Caldeiral",    new Vector3(-22.5f, 2.4f, -18.0f), 7.5f, 2.2f),
            new OrbitShot("SetorExterno", new Vector3(0.0f, 1.8f, 14.0f),   11.0f, 4.8f),
            new OrbitShot("Epicentro",    new Vector3(0.0f, 2.0f, 12.0f),   11.5f, 5.2f),
        };

        [UnityTest]
        public IEnumerator ZFighting_Capture30sShowcaseFrames()
        {
            string phase = Environment.GetEnvironmentVariable("ZFIGHT_CAPTURE_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "antes";

            string visualDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "visual"));
            string framesDir = Path.Combine(visualDir, $"_tmp_frames_{phase}");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            const int framesPerScene = 60; // 3 * 60 = 180 frames @ 6fps = 30.0s
            const int width = 1280;
            const int height = 720;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            int globalIdx = 0;

            try
            {
                foreach (var shot in Shots)
                {
                    var op = SceneManager.LoadSceneAsync(shot.SceneName, LoadSceneMode.Single);
                    while (!op.isDone) yield return null;
                    yield return null;
                    yield return null;

                    var camGo = new GameObject("[ZFight_CaptureCam]");
                    var cam = camGo.AddComponent<Camera>();
                    cam.fieldOfView = 55f;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 250f;
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.targetTexture = rt;

                    for (int f = 0; f < framesPerScene; f++)
                    {
                        float t = f / (float)(framesPerScene - 1);
                        float angle = Mathf.Lerp(-0.65f, 0.65f, t);
                        Vector3 camPos = shot.Pivot + new Vector3(
                            Mathf.Sin(angle) * shot.Radius,
                            shot.Height + Mathf.Sin(t * Mathf.PI * 2f) * 0.45f,
                            -Mathf.Cos(angle) * shot.Radius);
                        cam.transform.position = camPos;
                        cam.transform.LookAt(shot.Pivot);
                        cam.Render();

                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;

                        File.WriteAllBytes(Path.Combine(framesDir, $"frame_{globalIdx:D4}.png"), tex.EncodeToPNG());
                        globalIdx++;
                        if (f % 15 == 0) yield return null;
                    }

                    UnityEngine.Object.Destroy(camGo);
                }

                Assert.AreEqual(180, globalIdx, "Devem ser capturados 180 frames (30s @ 6fps).");
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
