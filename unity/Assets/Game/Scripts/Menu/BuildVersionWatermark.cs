using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Studio.Game.Animation;
using Studio.Game.Cutscenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    [Serializable]
    public sealed class BuildVersionPayload
    {
        public string version = "dev";
        public string gitHash = "local";
        public string buildDate = "";
        public bool isDemo = true;
    }

    [Serializable]
    public sealed class BuildQaTelemetryResult
    {
        public string version;
        public string sceneName;
        public float durationSec;
        public int totalFrames;
        public float avgFps;
        public float minFps;
        public float maxFps;
        public float p99FrameMs;
        public int consoleErrorCount;
        public List<string> consoleErrors = new List<string>();
        public int screenshotsCaptured;
    }

    /// <summary>
    /// GATE 1: Exibe automaticamente o numero de versao (hash do git + data) na tela inicial (MainMenu)
    /// a partir de Resources/BuildVersionInfo.json gerado pelo script de build.
    /// Tambem oferece telemetria passiva de FPS/erros na BUILD standalone quando iniciada com --demo-qa-telemetry.
    /// </summary>
    public sealed class BuildVersionWatermark : MonoBehaviour
    {
        private static BuildVersionWatermark _instance;
        private Canvas _canvas;
        private Text _versionText;
        private BuildVersionPayload _payload;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("Concerto_BuildVersionWatermark");
            _instance = go.AddComponent<BuildVersionWatermark>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVersionPayload();
            BuildWatermarkUi();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            UpdateVisibility(SceneManager.GetActiveScene().name);

            string telemetryOut = GetCliArg("--demo-qa-telemetry");
            if (!string.IsNullOrEmpty(telemetryOut))
            {
                float benchSec = 25f;
                string durArg = GetCliArg("--demo-qa-seconds");
                if (!string.IsNullOrEmpty(durArg) && float.TryParse(durArg, out float parsed) && parsed >= 5f)
                    benchSec = parsed;
                StartCoroutine(RunStandaloneBuildTelemetry(telemetryOut, benchSec));
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateVisibility(scene.name);
        }

        private void UpdateVisibility(string sceneName)
        {
            if (_canvas != null)
                _canvas.enabled = string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadVersionPayload()
        {
            var asset = Resources.Load<TextAsset>("BuildVersionInfo");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try { _payload = JsonUtility.FromJson<BuildVersionPayload>(asset.text); }
                catch { _payload = null; }
            }
            if (_payload == null)
            {
                _payload = new BuildVersionPayload
                {
                    version = Application.version,
                    gitHash = "local",
                    buildDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    isDemo = true
                };
            }
        }

        private void BuildWatermarkUi()
        {
            var go = new GameObject("BuildVersion_Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 190;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var bgGo = new GameObject("VersionPill");
            bgGo.transform.SetParent(go.transform, false);
            var img = bgGo.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.10f, 0.72f);
            img.raycastTarget = false;
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 18f);
            rt.sizeDelta = new Vector2(360f, 34f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(bgGo.transform, false);
            _versionText = txtGo.AddComponent<Text>();
            _versionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _versionText.fontSize = 15;
            _versionText.fontStyle = FontStyle.Bold;
            _versionText.alignment = TextAnchor.MiddleCenter;
            _versionText.color = new Color(0.82f, 0.92f, 1.0f, 0.95f);
            _versionText.raycastTarget = false;
            _versionText.text = $"CONCERTO DEMO  |  v{_payload.version}";
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        private static string GetCliArg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private IEnumerator RunStandaloneBuildTelemetry(string outDir, float benchSec)
        {
            Directory.CreateDirectory(outDir);
            var errors = new List<string>();
            Application.LogCallback logHandler = (cond, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add($"[{type}] {cond}");
            };
            Application.logMessageReceived += logHandler;

            // Captura tela inicial com versao visivel
            yield return new WaitForSecondsRealtime(1.2f);
            var menu = FindAnyObjectByType<BootAndMainMenuController>();
            if (menu != null) menu.SkipBootSplash();
            yield return new WaitForSecondsRealtime(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "shot_000_mainmenu.png"));
            yield return new WaitForSecondsRealtime(0.4f);

            // Entra no Ato 1 (Caldeiral)
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return new WaitForSecondsRealtime(1.0f);

            var cs01 = FindAnyObjectByType<CutsceneManager>();
            if (cs01 != null && cs01.IsPlaying) cs01.SkipCutscene();
            yield return new WaitForSecondsRealtime(0.5f);

            // Desativa VSync apenas se travar abaixo do refresh, garante alvo >=60fps real
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var loco = FindAnyObjectByType<DezLocomotionController>();
            var frameTimesMs = new List<float>(4096);
            float elapsed = 0f;
            float nextShotAt = 0.5f;
            int shotCount = 1;
            int dirPhase = 0;
            float phaseTimer = 0f;
            Vector3[] patrolDirs = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };

            if (loco != null) loco.UseScriptedInput = true;

            while (elapsed < benchSec)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                phaseTimer += dt;
                if (dt > 0.0001f && dt < 0.5f)
                    frameTimesMs.Add(dt * 1000f);

                if (loco != null)
                {
                    if (phaseTimer >= 2.2f)
                    {
                        phaseTimer = 0f;
                        dirPhase = (dirPhase + 1) % patrolDirs.Length;
                    }
                    loco.ScriptedMoveDir = patrolDirs[dirPhase];
                }

                if (elapsed >= nextShotAt && shotCount < 16)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"shot_{shotCount:D3}_act1.png"));
                    shotCount++;
                    nextShotAt += benchSec / 14f;
                }
                yield return null;
            }

            if (loco != null)
            {
                loco.ScriptedMoveDir = Vector3.zero;
                loco.UseScriptedInput = false;
            }

            Application.logMessageReceived -= logHandler;

            float sumMs = 0f;
            float minMs = float.MaxValue;
            float maxMs = 0f;
            for (int i = 0; i < frameTimesMs.Count; i++)
            {
                float ms = frameTimesMs[i];
                sumMs += ms;
                if (ms < minMs) minMs = ms;
                if (ms > maxMs) maxMs = ms;
            }
            frameTimesMs.Sort();
            float avgMs = frameTimesMs.Count > 0 ? sumMs / frameTimesMs.Count : 16.67f;
            float p99Ms = frameTimesMs.Count > 0 ? frameTimesMs[Mathf.Clamp((int)(frameTimesMs.Count * 0.99f), 0, frameTimesMs.Count - 1)] : avgMs;

            var result = new BuildQaTelemetryResult
            {
                version = _payload.version,
                sceneName = SceneManager.GetActiveScene().name,
                durationSec = elapsed,
                totalFrames = frameTimesMs.Count,
                avgFps = avgMs > 0.01f ? 1000f / avgMs : 0f,
                minFps = maxMs > 0.01f ? 1000f / maxMs : 0f,
                maxFps = minMs > 0.01f ? 1000f / minMs : 0f,
                p99FrameMs = p99Ms,
                consoleErrorCount = errors.Count,
                consoleErrors = errors,
                screenshotsCaptured = shotCount
            };

            File.WriteAllText(Path.Combine(outDir, "build_telemetry.json"), JsonUtility.ToJson(result, true));
            yield return new WaitForSecondsRealtime(0.4f);
            Application.Quit(0);
        }
    }
}
