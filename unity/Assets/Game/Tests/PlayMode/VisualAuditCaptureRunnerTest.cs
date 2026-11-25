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
    /// Runner PlayMode para Auditoria Visual (GATES 1 e 6):
    /// Captura screenshots 1280x720 por ato (ato1=Caldeiral, ato2=SetorExterno, ato3=Epicentro)
    /// nos angulos fixos da regressao visual + angulos extras de suspeita, em fases "antes" e "depois".
    /// </summary>
    public sealed class VisualAuditCaptureRunnerTest
    {
        private readonly struct AuditShot
        {
            public readonly string ActFolder;
            public readonly string FileName;
            public readonly string SceneName;
            public readonly Vector3 CamPos;
            public readonly Vector3 LookAt;
            public readonly float Fov;

            public AuditShot(string actFolder, string fileName, string sceneName, Vector3 camPos, Vector3 lookAt, float fov)
            {
                ActFolder = actFolder;
                FileName = fileName;
                SceneName = sceneName;
                CamPos = camPos;
                LookAt = lookAt;
                Fov = fov;
            }
        }

        private static readonly AuditShot[] Shots =
        {
            // Ato 1 — Caldeiral (regressoes fixas + angulos de suspeita: nave, bancada, rampa Z2, patio Z3, arena Z4)
            new AuditShot("ato1", "01_z1_oficina_bancada.png", "Caldeiral", new Vector3(-25.6f, 2.2f, -20.2f), new Vector3(-29.2f, 1.0f, -16.4f), 55f),
            new AuditShot("ato1", "02_z1_corredor_arco.png",   "Caldeiral", new Vector3(-31.5f, 2.4f, -18.5f), new Vector3(-22.0f, 1.1f, -18.5f), 55f),
            new AuditShot("ato1", "03_z2_rampa_porta.png",     "Caldeiral", new Vector3(-18.5f, 3.0f, -16.5f), new Vector3(-10.5f, 1.6f, -12.6f), 58f),
            new AuditShot("ato1", "04_z3_patio_sirenes.png",   "Caldeiral", new Vector3(4.5f,   3.4f, -16.0f), new Vector3(13.5f,  1.6f, -21.5f), 58f),
            new AuditShot("ato1", "05_z4_camara_central.png",  "Caldeiral", new Vector3(-4.0f,  4.2f,   1.5f), new Vector3(4.0f,   2.0f,   9.5f), 60f),

            // Ato 2 — Setor Externo (patio, geradores/antenas, corredor externo)
            new AuditShot("ato2", "01_patio_entrada.png",      "SetorExterno", new Vector3(-6.0f, 3.8f, -10.0f), new Vector3(4.0f, 1.2f,  2.0f), 58f),
            new AuditShot("ato2", "02_zona_geradores.png",     "SetorExterno", new Vector3(10.0f, 3.6f,   6.0f), new Vector3(0.0f, 1.2f, 16.0f), 58f),
            new AuditShot("ato2", "03_antena_vista_geral.png", "SetorExterno", new Vector3(-9.0f, 4.8f,  18.0f), new Vector3(2.0f, 1.5f, 26.0f), 60f),

            // Ato 3 — Epicentro (travessia, plataformas flutuantes, arena do Guardiao)
            new AuditShot("ato3", "01_travessia_inicial.png",  "Epicentro", new Vector3(-6.0f, 4.0f, -5.0f), new Vector3(16.0f,  0.8f, 0.0f), 58f),
            new AuditShot("ato3", "02_plataformas_meio.png",   "Epicentro", new Vector3(36.0f, 4.5f, -9.0f), new Vector3(56.0f,  1.2f, 0.0f), 58f),
            new AuditShot("ato3", "03_arena_guardiao.png",     "Epicentro", new Vector3(96.0f, 5.2f, -8.5f), new Vector3(116.0f, 1.4f, 0.0f), 60f),
        };

        [UnityTest]
        public IEnumerator VisualAudit_CaptureAllActs()
        {
            string phase = Environment.GetEnvironmentVariable("VISUAL_AUDIT_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "antes";

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "visual_auditoria"));
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            string loadedScene = null;

            try
            {
                foreach (var shot in Shots)
                {
                    if (loadedScene != shot.SceneName)
                    {
                        var op = SceneManager.LoadSceneAsync(shot.SceneName, LoadSceneMode.Single);
                        while (!op.isDone) yield return null;
                        yield return null;
                        yield return null;
                        loadedScene = shot.SceneName;
                    }

                    string outDir = Path.Combine(root, shot.ActFolder, phase);
                    Directory.CreateDirectory(outDir);

                    var camGo = new GameObject("[VisualAudit_Cam]");
                    var cam = camGo.AddComponent<Camera>();
                    cam.fieldOfView = shot.Fov;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 300f;
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.transform.position = shot.CamPos;
                    cam.transform.LookAt(shot.LookAt);
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    File.WriteAllBytes(Path.Combine(outDir, shot.FileName), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(camGo);
                }
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
