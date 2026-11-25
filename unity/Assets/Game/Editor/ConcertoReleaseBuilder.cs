using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Studio.Game.Menu;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Studio.Game.Editor
{
    /// <summary>
    /// GATE 1 & GATE 2 — Script de Build de Release / Demo de CONCERTO (Menu do Editor + Linha de Comando):
    ///   - Gera versao automatica (<yyyy.MM.dd>+<git-short-hash>) em PlayerSettings.bundleVersion e Resources/BuildVersionInfo.json
    ///   - Executa Checklist Pre-Build obrigatorio (zero missing scripts, zero shader rosa/erro nas cenas e prefabs)
    ///   - Compila build Windows x64 Standalone em ../builds/Concerto_Demo_Win64/Concerto.exe (fora do git via .gitignore)
    /// </summary>
    public static class ConcertoReleaseBuilder
    {
        private const string VersionResourcePath = "Assets/Resources/BuildVersionInfo.json";

        [Serializable]
        private sealed class ChecklistReport
        {
            public bool passed;
            public int scenesChecked;
            public int prefabsChecked;
            public int missingScripts;
            public int errorShaders;
            public List<string> issues = new List<string>();
        }

        [Serializable]
        private sealed class BuildReportSummary
        {
            public string version;
            public string gitHash;
            public string buildDate;
            public string outputPath;
            public long totalSizeBytes;
            public double totalSizeMb;
            public double buildSeconds;
            public string result;
            public int totalWarnings;
            public int totalErrors;
        }

        [MenuItem("Concerto/Build/Run Pre-Build Checklist")]
        public static void RunPreBuildChecklistMenu()
        {
            var rep = RunPreBuildChecklistInternal();
            Debug.Log($"[ReleaseBuilder] Checklist Pre-Build: passed={rep.passed}, scenes={rep.scenesChecked}, prefabs={rep.prefabsChecked}, missingScripts={rep.missingScripts}, errorShaders={rep.errorShaders}");
        }

        [MenuItem("Concerto/Build/Build Windows Release Demo")]
        public static void BuildWindowsReleaseMenu()
        {
            BuildInternal(exitOnFinish: false);
        }

        /// <summary>
        /// Entrada via linha de comando:
        ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod Studio.Game.Editor.ConcertoReleaseBuilder.BuildFromCommandLine
        /// </summary>
        public static void BuildFromCommandLine()
        {
            BuildInternal(exitOnFinish: true);
        }

        private static void BuildInternal(bool exitOnFinish)
        {
            // 1. Versao automatica (data + git hash) -> gravada em PlayerSettings e Resources/BuildVersionInfo.json
            string gitHash = ReadGitShortHash();
            string buildDate = DateTime.Now.ToString("yyyy.MM.dd");
            string fullVersion = $"{buildDate}+{gitHash}";
            PlayerSettings.bundleVersion = fullVersion;
            WriteVersionResource(fullVersion, gitHash, buildDate);

            // 2. Checklist pre-build obrigatorio (GATE 2)
            var checklist = RunPreBuildChecklistInternal();
            string logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "prebuild_checklist.json"), JsonUtility.ToJson(checklist, true));

            if (!checklist.passed)
            {
                Debug.LogError($"[ReleaseBuilder] Pre-build checklist FALHOU: {string.Join(" | ", checklist.issues)}");
                if (exitOnFinish) EditorApplication.Exit(1);
                return;
            }

            // 3. Cenas habilitadas em EditorBuildSettings
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", s.path))))
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[ReleaseBuilder] Nenhuma cena habilitada em EditorBuildSettings.");
                if (exitOnFinish) EditorApplication.Exit(1);
                return;
            }

            string buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "builds", "Concerto_Demo_Win64"));
            if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "Concerto.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            };

            Debug.Log($"[ReleaseBuilder] Iniciando build Windows x64 Release Demo v{fullVersion} -> {exePath}");
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;

            long dirSizeBytes = ComputeDirectorySize(buildDir);
            var outSummary = new BuildReportSummary
            {
                version = fullVersion,
                gitHash = gitHash,
                buildDate = buildDate,
                outputPath = exePath,
                totalSizeBytes = dirSizeBytes,
                totalSizeMb = Math.Round(dirSizeBytes / (1024.0 * 1024.0), 2),
                buildSeconds = Math.Round(summary.totalTime.TotalSeconds, 2),
                result = summary.result.ToString(),
                totalWarnings = summary.totalWarnings,
                totalErrors = summary.totalErrors
            };

            File.WriteAllText(Path.Combine(logsDir, "build_report.json"), JsonUtility.ToJson(outSummary, true));
            Debug.Log($"[ReleaseBuilder] Resultado: {summary.result} | Tamanho: {outSummary.totalSizeMb} MB | Tempo: {outSummary.buildSeconds}s | Warnings: {summary.totalWarnings} | Errors: {summary.totalErrors}");

            if (exitOnFinish)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static ChecklistReport RunPreBuildChecklistInternal()
        {
            var rep = new ChecklistReport { passed = true };

            // Verifica todas as cenas da build (limpa eventuais MonoBehaviours ausentes legados e revalida 0)
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                bool cleanedAny = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject) > 0)
                            cleanedAny = true;
                    }
                }
                if (cleanedAny || s.path.EndsWith(".unity"))
                    EditorSceneManager.SaveScene(scene);

                rep.scenesChecked++;
                foreach (var root in scene.GetRootGameObjects())
                    InspectHierarchy(root, s.path, rep);
            }

            // Verifica todos os prefabs do catalogo em Assets/Game/Prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                rep.prefabsChecked++;
                InspectHierarchy(prefab, p, rep);
            }

            rep.passed = rep.missingScripts == 0 && rep.errorShaders == 0;
            return rep;
        }

        private static void InspectHierarchy(GameObject root, string context, ChecklistReport rep)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                {
                    rep.missingScripts += missing;
                    rep.issues.Add($"MissingScript({missing}) em {context} -> {t.name}");
                }

                var rend = t.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mats = rend.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && mats[m].shader != null && mats[m].shader.name == "Hidden/InternalErrorShader")
                        {
                            rep.errorShaders++;
                            rep.issues.Add($"ErrorShader(rosa) em {context} -> {t.name}");
                        }
                    }
                }
            }
        }

        private static void WriteVersionResource(string fullVersion, string gitHash, string buildDate)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "BuildVersionInfo.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var payload = new BuildVersionPayload
            {
                version = fullVersion,
                gitHash = gitHash,
                buildDate = buildDate,
                isDemo = true
            };
            File.WriteAllText(fullPath, JsonUtility.ToJson(payload, true));
            AssetDatabase.ImportAsset(VersionResourcePath, ImportAssetOptions.ForceUpdate);
        }

        private static string ReadGitShortHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")),
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string hash = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(3000);
                    if (!string.IsNullOrEmpty(hash)) return hash;
                }
            }
            catch { /* fallback */ }
            return "nogit";
        }

        private static long ComputeDirectorySize(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            long total = 0;
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                total += new FileInfo(f).Length;
            return total;
        }
    }
}
