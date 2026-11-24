using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Benchmark real usando Unity.Profiling.ProfilerRecorder nas 3 cenas do projeto:
    ///   1. MainMenu (hub/menus)
    ///   2. Caldeiral (gameplay mais populoso + oficina + NPCs + anomalias)
    ///   3. SetorExterno (patio aberto + SkyRift + POIs TripoSG + Vera)
    /// Grava relatorio JSON/CSV em captures/profiler/antes/ ou captures/profiler/depois/ conforme PERF_PHASE.
    /// Tambem mede latencia real input->acao (<100ms, DESIGN_BIBLE Secao 3) e opcionalmente grava MP4 da cena mais pesada.
    /// </summary>
    public sealed class PerformancePassProfilerBenchmarkTest
    {
        private RenderTexture _rt;
        private Texture2D _tex;

        [UnityTest]
        public IEnumerator ProfileAllThreeScenesAndMeasureInputLatency()
        {
            string phase = Environment.GetEnvironmentVariable("PERF_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "depois";
            bool captureHeavyMp4 = Environment.GetEnvironmentVariable("CAPTURE_PERF_MP4") == "1";

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string outDir = Path.Combine(repoRoot, "captures", "profiler", phase);
            Directory.CreateDirectory(outDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var summary = new StringBuilder();
            summary.AppendLine("scene,frames,avg_ms,p95_ms,peak_ms,hitches_gt_2frames,avg_gc_bytes,peak_gc_bytes,avg_draw_calls,avg_batches,avg_setpass");

            // 1. MainMenu (Hub / Menus)
            yield return ProfileScene("MainMenu", 120, false, null, outDir, summary);

            // 2. Caldeiral (Gameplay mais populoso — tambem captura MP4 se CAPTURE_PERF_MP4=1)
            string mp4FramesDir = Path.Combine(repoRoot, "captures", "profiler", "caldeiral_post_frames");
            yield return ProfileScene("Caldeiral", 180, captureHeavyMp4, mp4FramesDir, outDir, summary);

            // 3. SetorExterno (Cenario aberto + SkyRift + POIs TripoSG)
            yield return ProfileScene("SetorExterno", 150, false, null, outDir, summary);

            // 4. Medicao objetiva de Input->Acao (<100ms, DESIGN_BIBLE Secao 3) em SetorExterno
            var dez = GameObject.Find("Player_Dez_Placeholder");
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dez.GetComponent(locoT);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            yield return null;
            yield return null;

            float tInput = Time.realtimeSinceStartup;
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.forward);
            int respFrames = 0;
            float respMs = 0f;
            for (int i = 0; i < 15; i++)
            {
                yield return null;
                respFrames++;
                float spd = (float)locoT.GetProperty("CurrentSpeed").GetValue(loco);
                if (spd > 0.1f)
                {
                    respMs = (Time.realtimeSinceStartup - tInput) * 1000f;
                    break;
                }
            }
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);

            File.WriteAllText(Path.Combine(outDir, "profiler_summary.csv"), summary.ToString());
            Debug.Log($"[PerfAudit:{phase}] Input->Action latency: {respFrames} frame(s) ({respMs:F1}ms, target <100ms)");
            Debug.Log($"[PerfAudit:{phase}]\n{summary}");

            Assert.LessOrEqual(respFrames, 2, $"Input->acao demorou {respFrames} frames!");
            Assert.Less(respMs, 100f, $"Input->acao ({respMs:F1}ms) excedeu 100ms!");

            if (_rt != null) { UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_tex != null) { UnityEngine.Object.Destroy(_tex); _tex = null; }
        }

        private IEnumerator ProfileScene(string sceneName, int sampleFrames, bool captureFrames, string framesDir, string outDir, StringBuilder summary)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;

            // Warmup (20 frames) para estabilizar shaders/init
            for (int w = 0; w < 20; w++) yield return null;

            // Se for cena jogavel, faz o Dez andar em circulo durante o profiling para exercitar Update/LateUpdate/Fisica/UI!
            GameObject dez = GameObject.Find("Player_Dez_Placeholder");
            Type locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            object loco = (dez != null && locoT != null) ? dez.GetComponent(locoT) : null;
            if (loco != null)
            {
                locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
                locoT.GetProperty("ScriptedRun").SetValue(loco, true);
            }

            if (captureFrames)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
                if (_rt == null) _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                if (_tex == null) _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            }

            using var mainThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            using var gcAllocRec    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
            using var drawCallsRec  = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 15);
            using var batchesRec    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 15);
            using var setPassRec    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 15);

            var frameMsList = new List<double>(sampleFrames);
            long gcSum = 0;
            long gcPeak = 0;
            long dcSum = 0;
            long batchSum = 0;
            long spSum = 0;
            int hitches = 0;

            for (int i = 0; i < sampleFrames; i++)
            {
                if (loco != null)
                {
                    float ang = i * 0.06f;
                    locoT.GetProperty("ScriptedMoveDir").SetValue(loco, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
                }

                yield return null;

                double ms = mainThreadRec.Valid && mainThreadRec.LastValue > 0
                    ? mainThreadRec.LastValue * 1e-6
                    : Time.unscaledDeltaTime * 1000.0;
                frameMsList.Add(ms);
                if (ms > 33.4) hitches++; // >2 frames a 60fps (33.4ms)

                long gc = gcAllocRec.Valid ? gcAllocRec.LastValue : 0;
                gcSum += gc;
                if (gc > gcPeak) gcPeak = gc;

                dcSum += drawCallsRec.Valid ? drawCallsRec.LastValue : 0;
                batchSum += batchesRec.Valid ? batchesRec.LastValue : 0;
                spSum += setPassRec.Valid ? setPassRec.LastValue : 0;

                if (captureFrames && Camera.main != null)
                {
                    var cam = Camera.main;
                    cam.targetTexture = _rt;
                    cam.Render();
                    cam.targetTexture = null;
                    RenderTexture.active = _rt;
                    _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    _tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(framesDir, $"frame_{i:D5}.jpg"), _tex.EncodeToJPG(84));
                }
            }

            if (loco != null)
            {
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
                locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            }

            frameMsList.Sort();
            double avgMs = 0;
            for (int i = 0; i < frameMsList.Count; i++) avgMs += frameMsList[i];
            avgMs /= Mathf.Max(1, frameMsList.Count);
            double p95Ms = frameMsList[Mathf.Clamp((int)(frameMsList.Count * 0.95f), 0, frameMsList.Count - 1)];
            double peakMs = frameMsList[frameMsList.Count - 1];

            long avgGc = gcSum / Mathf.Max(1, sampleFrames);
            long avgDc = dcSum / Mathf.Max(1, sampleFrames);
            long avgBat = batchSum / Mathf.Max(1, sampleFrames);
            long avgSp = spSum / Mathf.Max(1, sampleFrames);

            summary.AppendLine($"{sceneName},{sampleFrames},{avgMs:F2},{p95Ms:F2},{peakMs:F2},{hitches},{avgGc},{gcPeak},{avgDc},{avgBat},{avgSp}");
            File.WriteAllText(Path.Combine(outDir, $"{sceneName}_baseline.txt"),
                $"Scene: {sceneName}\nFrames: {sampleFrames}\nAvg ms: {avgMs:F2}\nP95 ms: {p95Ms:F2}\nPeak ms: {peakMs:F2}\nHitches (>33.4ms): {hitches}\nAvg GC Bytes/frame: {avgGc}\nPeak GC Bytes/frame: {gcPeak}\nAvg DrawCalls: {avgDc}\nAvg Batches: {avgBat}\nAvg SetPass: {avgSp}\n");
        }
    }
}
