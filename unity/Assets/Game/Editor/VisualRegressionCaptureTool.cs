using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.Editor
{
    /// <summary>
    /// GATE 1 & GATE 2 — Ferramenta de Regressao Visual (100% Editor-only, ZERO impacto em gameplay).
    /// Captura 6 screenshots deterministas em posicoes fixas de camera pelos atos do jogo em
    /// /captures/regressao/baseline/ ou /captures/regressao/<data>/.
    /// </summary>
    public static class VisualRegressionCaptureTool
    {
        private readonly struct CameraShotSpec
        {
            public readonly string FileName;
            public readonly string ScenePath;
            public readonly Vector3 CamPos;
            public readonly Vector3 LookAt;
            public readonly float Fov;

            public CameraShotSpec(string fileName, string scenePath, Vector3 camPos, Vector3 lookAt, float fov)
            {
                FileName = fileName;
                ScenePath = scenePath;
                CamPos = camPos;
                LookAt = lookAt;
                Fov = fov;
            }
        }

        private static readonly CameraShotSpec[] Shots =
        {
            new CameraShotSpec("01_mainmenu_title.png",          "Assets/Game/Scenes/MainMenu.unity",     new Vector3(0f, 1.0f, -10f),      new Vector3(0f, 1.0f, 0f),     60f),
            new CameraShotSpec("02_ato1_caldeiral_bancada.png",  "Assets/Game/Scenes/Caldeiral.unity",    new Vector3(-25.6f, 2.2f, -20.2f),new Vector3(-29.2f, 1.0f, -16.4f), 55f),
            new CameraShotSpec("03_ato1_caldeiral_corredor.png", "Assets/Game/Scenes/Caldeiral.unity",    new Vector3(-31.5f, 2.4f, -18.5f),new Vector3(-22.0f, 1.1f, -18.5f), 55f),
            new CameraShotSpec("04_ato3_setorexterno_patio.png", "Assets/Game/Scenes/SetorExterno.unity", new Vector3(-6.0f, 3.8f, -10.0f), new Vector3(4.0f, 1.2f, 2.0f),  58f),
            new CameraShotSpec("05_ato4_epicentro_travessia.png","Assets/Game/Scenes/Epicentro.unity",    new Vector3(-6.0f, 4.0f, -5.0f),  new Vector3(16.0f, 0.8f, 0.0f), 58f),
            new CameraShotSpec("06_ato4_epicentro_arena.png",    "Assets/Game/Scenes/Epicentro.unity",    new Vector3(96.0f, 5.2f, -8.5f),  new Vector3(116.0f, 1.4f, 0.0f), 60f)
        };

        private static string RegressaoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "regressao"));

        [MenuItem("Concerto/QA/Visual Regression — Capture Baseline")]
        public static void CaptureBaselineMenu()
        {
            CaptureSet(Path.Combine(RegressaoRoot, "baseline"), injectTestDiff: false);
            Debug.Log("[VisualRegression] Baseline capturada com sucesso em captures/regressao/baseline/");
        }

        [MenuItem("Concerto/QA/Visual Regression — Capture Dated Run")]
        public static void CaptureDatedRunMenu()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            CaptureSet(Path.Combine(RegressaoRoot, stamp), injectTestDiff: false);
            Debug.Log($"[VisualRegression] Captura salva em captures/regressao/{stamp}/");
        }

        [MenuItem("Concerto/QA/Visual Regression — Update Baseline (Intentional)")]
        public static void UpdateBaselineMenu()
        {
            CaptureBaselineMenu();
        }

        /// <summary>
        /// CLI: -executeMethod Studio.Game.Editor.VisualRegressionCaptureTool.CaptureFromCommandLine
        /// Flags opcionais: -vrOutDir <dir> | -vrInjectTestDiff
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            string outDir = GetCliArg("-vrOutDir");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(RegressaoRoot, DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            bool injectDiff = HasCliFlag("-vrInjectTestDiff");

            CaptureSet(outDir, injectDiff);
            Debug.Log($"[VisualRegression] Captura CLI concluida em {outDir} (injectDiff={injectDiff}).");
            EditorApplication.Exit(0);
        }

        private static void CaptureSet(string outDir, bool injectTestDiff)
        {
            Directory.CreateDirectory(outDir);

            // Configuracao grafica fixa para determinismo entre builds
            int prevQuality = QualitySettings.GetQualityLevel();
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(Mathf.Min(2, QualitySettings.names.Length - 1), true);

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            try
            {
                string currentScenePath = string.Empty;
                for (int i = 0; i < Shots.Length; i++)
                {
                    var spec = Shots[i];
                    if (spec.ScenePath != currentScenePath)
                    {
                        EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                        currentScenePath = spec.ScenePath;
                    }

                    GameObject tempDiffObj = null;
                    if (injectTestDiff && spec.FileName == "02_ato1_caldeiral_bancada.png")
                    {
                        // Objeto temporario APENAS em memoria (nunca salvo na cena!) para provar GATE 5
                        tempDiffObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tempDiffObj.name = "__VR_TEMP_DIFF_MARKER__";
                        tempDiffObj.transform.position = new Vector3(-28.6f, 1.35f, -17.1f);
                        tempDiffObj.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                        var r = tempDiffObj.GetComponent<Renderer>();
                        if (r != null)
                        {
                            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                            mat.SetColor("_BaseColor", new Color(1.0f, 0.12f, 0.62f, 1f));
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", new Color(2.4f, 0.25f, 1.2f, 1f));
                            r.sharedMaterial = mat;
                        }
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

                    UnityEngine.Object.DestroyImmediate(camGo);
                    if (tempDiffObj != null) UnityEngine.Object.DestroyImmediate(tempDiffObj);
                }
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                if (prevQuality >= 0) QualitySettings.SetQualityLevel(prevQuality, true);
            }
        }

        private static string GetCliArg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static bool HasCliFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
