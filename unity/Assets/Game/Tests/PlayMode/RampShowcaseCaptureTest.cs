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
    /// Runner PlayMode determinista para capturar 30s (180 frames @ 6fps -> 30.0s MP4)
    /// inspecionando todas as 3 rampas do jogo (Z2 Mezzanine, Z3 Vo Segunda, Z4 Arena)
    /// com exatamente os mesmos pontos de camera para rampas_antes.mp4 e rampas_depois.mp4.
    /// </summary>
    public sealed class RampShowcaseCaptureTest
    {
        private readonly struct RampShot
        {
            public readonly Vector3 Pivot;
            public readonly float Radius;
            public readonly float Height;
            public readonly float StartAngle;
            public readonly float EndAngle;

            public RampShot(Vector3 pivot, float radius, float height, float startAngle, float endAngle)
            {
                Pivot = pivot;
                Radius = radius;
                Height = height;
                StartAngle = startAngle;
                EndAngle = endAngle;
            }
        }

        // 3 rampas em Caldeiral.unity (10s cada = 30.0s exatos)
        private static readonly RampShot[] Shots =
        {
            new RampShot(new Vector3(-11.8f, 1.8f, -12.6f), 8.0f, 3.0f, -0.8f, 0.8f), // Z2_Mezzanine_Ramp_16_9deg
            new RampShot(new Vector3(13.2f, 1.8f, -22.0f),  8.0f, 3.0f,  2.3f, 3.9f), // Z3_VoSegunda_Ramp_16_9deg
            new RampShot(new Vector3(7.0f, 2.0f, 10.0f),    8.5f, 3.2f, -2.2f, -0.6f) // Z4_Arena_Ramp_16_9deg (mostra topo bloqueado!)
        };

        [UnityTest]
        public IEnumerator Ramps_Capture30sShowcaseFrames()
        {
            string phase = Environment.GetEnvironmentVariable("RAMP_CAPTURE_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "antes";

            string rampDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "rampas"));
            string framesDir = Path.Combine(rampDir, $"_tmp_frames_{phase}");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            const int framesPerRamp = 60; // 3 * 60 = 180 frames @ 6fps = 30.0s
            const int width = 1280;
            const int height = 720;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var camGo = new GameObject("[Ramp_CaptureCam]");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.targetTexture = rt;

            int globalIdx = 0;
            try
            {
                foreach (var shot in Shots)
                {
                    for (int f = 0; f < framesPerRamp; f++)
                    {
                        float t = f / (float)(framesPerRamp - 1);
                        float ang = Mathf.Lerp(shot.StartAngle, shot.EndAngle, t);
                        Vector3 pos = shot.Pivot + new Vector3(
                            Mathf.Sin(ang) * shot.Radius,
                            shot.Height + Mathf.Sin(t * Mathf.PI) * 0.8f,
                            Mathf.Cos(ang) * shot.Radius);
                        cam.transform.position = pos;
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
                }

                Assert.AreEqual(180, globalIdx, "Devem ser capturados 180 frames (30s @ 6fps).");
            }
            finally
            {
                UnityEngine.Object.Destroy(camGo);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
