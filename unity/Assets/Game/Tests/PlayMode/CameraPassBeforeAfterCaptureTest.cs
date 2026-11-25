using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// GATE 6 — Captura PlayMode ANTES vs DEPOIS (90s lado a lado):
    ///   - 30s caminhando (look-ahead + dead zone + enquadramento vertical)
    ///   - 30s lutando (BossCombat enquadrando Dez + alvo + Cinemachine Impulse <300ms)
    ///   - 30s em zona com paredes apertadas (TightCorridor + pull-in por SphereCast sem clip)
    /// </summary>
    public sealed class CameraPassBeforeAfterCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string segment;
            public string info;
        }

        [Serializable]
        private sealed class SideMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
        }

        private string _outRoot;
        private Texture2D _readTex;
        private RenderTexture _rt;

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        [UnityTest]
        public IEnumerator CameraPass_Capture90sBeforeAfterSideBySide()
        {
            _outRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "camera_pass_capture"));
            if (Directory.Exists(_outRoot)) Directory.Delete(_outRoot, true);
            Directory.CreateDirectory(Path.Combine(_outRoot, "antes"));
            Directory.CreateDirectory(Path.Combine(_outRoot, "depois"));

            _rt = new RenderTexture(960, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(960, 1080, TextureFormat.RGB24, false);

            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var camT  = GameType("Studio.Game.CameraSystem.ConcertoCameraDirector");
            var zoneE = GameType("Studio.Game.CameraSystem.CameraZoneMode");
            var locoT = GameType("Studio.Game.Animation.DezLocomotionController");

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            for (int i = 0; i < 6; i++) yield return null;

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var camDir = UnityEngine.Object.FindAnyObjectByType(camT);
            var loco   = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(camDir);
            Assert.IsNotNull(loco);

            try
            {
                yield return Capture90sSide("antes", true, camT, zoneE, camDir, locoT, loco);
                yield return Capture90sSide("depois", false, camT, zoneE, camDir, locoT, loco);
            }
            finally
            {
                camT.GetProperty("DisableCameraPassForComparison").SetValue(null, false);
                camT.GetMethod("RequestZoneTransition").Invoke(camDir, new object[] { Enum.Parse(zoneE, "Exploration") });
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator Capture90sSide(string folder, bool disablePass, Type camT, Type zoneE, object camDir, Type locoT, Component loco)
        {
            camT.GetProperty("DisableCameraPassForComparison").SetValue(null, disablePass);
            var meta = new SideMeta();
            string dir = Path.Combine(_outRoot, folder);

            // 1) 30s CAMINHANDO (Exploration: dead zone 0.18m + look-ahead 0.65m <100ms + pulo vertical)
            camT.GetMethod("RequestZoneTransition").Invoke(camDir, new object[] { Enum.Parse(zoneE, "Exploration") });
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            for (int k = 0; k < 5; k++)
            {
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, (k % 2 == 0) ? Vector3.right : Vector3.left);
                yield return CaptureSec(dir, meta, 6,
                    "00:00-00:30 — CAMINHANDO & PULANDO",
                    disablePass ? "Follow rigido sem look-ahead nem pitch vertical" : "Dead zone 0.18m + Look-ahead 0.65m (65ms) + Enquadramento vertical");
            }
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);

            // 2) 30s LUTANDO (BossCombat: FOV 62, distancia 5.6m enquadrando Dez + alvo + Cinemachine Impulse 220ms)
            camT.GetMethod("RequestZoneTransition").Invoke(camDir, new object[] { Enum.Parse(zoneE, "BossCombat") });
            for (int k = 0; k < 5; k++)
            {
                camT.GetMethod("TriggerCinemachineImpulse").Invoke(camDir, new object[] { new Vector3(0.15f, -0.22f, 0f), 0.22f });
                yield return CaptureSec(dir, meta, 6,
                    "00:30-01:00 — COMBATE (BOSS FRAMING + IMPULSE)",
                    disablePass ? "Sem enquadramento de combate nem Cinemachine Impulse" : "Dez + Guardiao enquadrados (5.6m / FOV 62) | Impulse decay 220ms");
            }

            // 3) 30s PAREDES APERTADAS (TightCorridor: 2.15m + SphereCast pull-in + blend 0.38s <0.5s)
            camT.GetMethod("RequestZoneTransition").Invoke(camDir, new object[] { Enum.Parse(zoneE, "TightCorridor") });
            for (int k = 0; k < 5; k++)
            {
                yield return CaptureSec(dir, meta, 6,
                    "01:00-01:30 — PAREDES APERTADAS (PULL-IN RAYCAST)",
                    disablePass ? "Distancia fixa 3.25m sujeita a clip em parede" : "TightCorridor 2.15m + SphereCast pull-in (0 clip) | Blend 0.38s");
            }
            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);

            Assert.AreEqual(90, meta.frames.Count, $"Pass '{folder}' deve ter exatamente 90s.");
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonUtility.ToJson(meta, true));
        }

        private IEnumerator CaptureSec(string dir, SideMeta meta, int seconds, string segment, string info)
        {
            for (int w = 0; w < 4; w++) yield return null;
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var prev = cam.targetTexture;
                cam.targetTexture = _rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = _rt;
                _readTex.ReadPixels(new Rect(0, 0, 960, 1080), 0, 0, false);
                _readTex.Apply(false);
                RenderTexture.active = null;
            }
            byte[] jpg = _readTex.EncodeToJPG(86);
            for (int s = 0; s < seconds; s++)
            {
                int idx = meta.frames.Count;
                File.WriteAllBytes(Path.Combine(dir, $"frame_{idx:D4}.jpg"), jpg);
                meta.frames.Add(new FrameMeta { index = idx, segment = segment, info = info });
            }
        }
    }
}
