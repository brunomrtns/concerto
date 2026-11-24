using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.Performance
{
    /// <summary>
    /// Benchmarks PlayMode por ato/cena usando Unity.PerformanceTesting + ProfilerRecorder.
    /// Mede: ms/frame medio e pico (P99/Max), hitches > 2 frames (> 33.33ms) por minuto e GC allocs/frame.
    /// Grava relatorio JSON em captures/profiler/<CONCERTO_PERF_PHASE>/<scene>.json (default: baseline).
    /// </summary>
    public sealed class ConcertoPerformanceBenchmarks
    {
        private const int WarmupFrames = 30;
        private const int MeasureFrames = 180;
        private const double HitchThresholdMs = 33.333; // > 2 frames a 60fps

        private static Type GameType(string fullName) => Type.GetType(fullName + ", Assembly-CSharp");

        private static string GetOutputDir()
        {
            string phase = Environment.GetEnvironmentVariable("CONCERTO_PERF_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "baseline";
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string dir = Path.Combine(repoRoot, "captures", "profiler", phase);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private IEnumerator RunActBenchmark(string actLabel, string sceneName, bool drivePlayerMovement)
        {
            if (sceneName != "MainMenu")
            {
                PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
                PlayerPrefs.Save();
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, $"LoadSceneAsync falhou para {sceneName}.");
            while (!op.isDone) yield return null;

            // Pula CS01 se estiver tocando para medir gameplay real
            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            if (cs01T != null)
            {
                var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
                if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                    cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            }

            // Opcional: movimenta o Dez para exercitar Update/LateUpdate/Camera/HUD/Animator/Audio
            Component loco = null;
            Type locoT = drivePlayerMovement ? GameType("Studio.Game.Animation.DezLocomotionController") : null;
            if (locoT != null)
            {
                loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
                if (loco != null)
                {
                    locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
                    locoT.GetProperty("ScriptedRun").SetValue(loco, false);
                }
            }

            // Warmup (estabiliza shader/streaming/primeiros frames)
            for (int w = 0; w < WarmupFrames; w++)
                yield return null;

            var frameTimesMs = new List<double>(MeasureFrames);
            var gcBytesList  = new List<long>(MeasureFrames);
            long totalSetPass = 0;
            long totalDrawCalls = 0;

            using var gcRecorder      = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var drawRecorder    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");

            var sgFrameMs = new SampleGroup("FrameTimeMs", SampleUnit.Millisecond);
            var sgGcBytes = new SampleGroup("GCAllocBytes", SampleUnit.Byte);

            int hitchCount = 0;
            double totalTimeSec = 0.0;

            for (int i = 0; i < MeasureFrames; i++)
            {
                if (loco != null)
                {
                    float ang = (i / (float)MeasureFrames) * Mathf.PI * 2f;
                    locoT.GetProperty("ScriptedMoveDir").SetValue(loco, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
                }

                yield return null;

                double dtMs = Time.unscaledDeltaTime * 1000.0;
                // Descarta apenas picos artificiais de > 500ms de troca de janela do SO
                if (dtMs > 500.0) dtMs = 16.67;
                long gcBytes = gcRecorder.Valid ? gcRecorder.LastValue : 0L;
                long setPass = setPassRecorder.Valid ? setPassRecorder.LastValue : 0L;
                long draws   = drawRecorder.Valid ? drawRecorder.LastValue : 0L;

                frameTimesMs.Add(dtMs);
                gcBytesList.Add(gcBytes);
                totalSetPass += setPass;
                totalDrawCalls += draws;
                totalTimeSec += dtMs / 1000.0;
                if (dtMs > HitchThresholdMs) hitchCount++;

                Measure.Custom(sgFrameMs, dtMs);
                Measure.Custom(sgGcBytes, gcBytes);
            }

            if (loco != null)
            {
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
                locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            }

            frameTimesMs.Sort();
            double sumMs = 0.0;
            for (int i = 0; i < frameTimesMs.Count; i++) sumMs += frameTimesMs[i];
            double avgMs = sumMs / frameTimesMs.Count;
            double p95Ms = frameTimesMs[Mathf.Clamp((int)(frameTimesMs.Count * 0.95f), 0, frameTimesMs.Count - 1)];
            double p99Ms = frameTimesMs[Mathf.Clamp((int)(frameTimesMs.Count * 0.99f), 0, frameTimesMs.Count - 1)];
            double maxMs = frameTimesMs[frameTimesMs.Count - 1];
            double avgFps = avgMs > 0.001 ? 1000.0 / avgMs : 0.0;
            double hitchesPerMin = totalTimeSec > 0.01 ? (hitchCount / totalTimeSec) * 60.0 : 0.0;

            long sumGc = 0, maxGc = 0;
            for (int i = 0; i < gcBytesList.Count; i++)
            {
                sumGc += gcBytesList[i];
                if (gcBytesList[i] > maxGc) maxGc = gcBytesList[i];
            }
            double avgGcBytes = gcBytesList.Count > 0 ? (double)sumGc / gcBytesList.Count : 0.0;
            double avgSetPass = (double)totalSetPass / MeasureFrames;
            double avgDraws   = (double)totalDrawCalls / MeasureFrames;

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"act\": \"{actLabel}\",");
            sb.AppendLine($"  \"scene\": \"{sceneName}\",");
            sb.AppendLine($"  \"frames\": {MeasureFrames},");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"avg_ms\": {0:F3},", avgMs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"p95_ms\": {0:F3},", p95Ms));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"p99_ms\": {0:F3},", p99Ms));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"max_ms\": {0:F3},", maxMs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"avg_fps\": {0:F1},", avgFps));
            sb.AppendLine($"  \"hitch_count\": {hitchCount},");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"hitches_per_min\": {0:F2},", hitchesPerMin));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"avg_gc_bytes\": {0:F1},", avgGcBytes));
            sb.AppendLine($"  \"max_gc_bytes\": {maxGc},");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"avg_setpass\": {0:F1},", avgSetPass));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"avg_drawcalls\": {0:F1}", avgDraws));
            sb.AppendLine("}");

            string outFile = Path.Combine(GetOutputDir(), $"{actLabel}_{sceneName}.json");
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[PerfBench] {actLabel} ({sceneName}): avg={avgMs:F2}ms ({avgFps:F1}fps), p99={p99Ms:F2}ms, max={maxMs:F2}ms, hitches/min={hitchesPerMin:F1}, gc={avgGcBytes:F0}B/frame (max {maxGc}B), draws={avgDraws:F0}");
        }

        [UnityTest, Performance]
        public IEnumerator Bench_00_MainMenu_Hub()
        {
            yield return RunActBenchmark("Act0_Menu", "MainMenu", drivePlayerMovement: false);
        }

        [UnityTest, Performance]
        public IEnumerator Bench_01_Act1_Caldeiral()
        {
            yield return RunActBenchmark("Act1_Oficina", "Caldeiral", drivePlayerMovement: true);
        }

        [UnityTest, Performance]
        public IEnumerator Bench_02_Act2_SetorExterno()
        {
            yield return RunActBenchmark("Act2_SetorExterno", "SetorExterno", drivePlayerMovement: true);
        }

        [UnityTest, Performance]
        public IEnumerator Bench_03_Act3_Epicentro()
        {
            yield return RunActBenchmark("Act3_Epicentro", "Epicentro", drivePlayerMovement: true);
        }
    }
}
