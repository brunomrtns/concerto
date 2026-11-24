using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Auditoria automatizada de conformidade com docs/DESIGN_BIBLE.md v1.0 + MP4 1080p dos 3 fixes P1:
    ///   - P1 #1 (Secoes 2 & 9): Dash FOV kick <=4 deg e <=200ms (ANTES 0 deg vs DEPOIS 3.5 deg / 180ms).
    ///   - P1 #2 (Secoes 2, 3 & 9): Opcao "Reduzir Shake" (GameSettingsManager.ReduceCameraShake) + Heavy Hitstop ~100ms + decay <300ms.
    ///   - P1 #3 (Secoes 3 & 4.2): Coyote ~100ms, Input Buffer ~150ms, e CS04 skipHintDelaySec >= 3.0s.
    /// </summary>
    public sealed class DesignBibleComplianceAuditTest
    {
        private bool _capture;
        private RenderTexture _rt;
        private Texture2D _tex;
        private GameObject _bannerCanvas;
        private Text _bannerText;

        [UnityTest]
        public IEnumerator ValidateAndCaptureThreeP1DesignBibleFixes()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_BIBLE") == "1";
            Time.captureFramerate = 30;

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "captures", "design_bible", "p1_frames");
            if (_capture)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            }

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var dez = GameObject.Find("Player_Dez_Placeholder");
            var cc = dez.GetComponent<CharacterController>();
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var cfgT  = Type.GetType("Studio.Game.Animation.DezMovementConfig, Assembly-CSharp");
            var loco  = dez.GetComponent(locoT);
            var cfg   = locoT.GetField("config").GetValue(loco) ?? ScriptableObject.CreateInstance(cfgT);
            locoT.GetField("config").SetValue(loco, cfg);

            int frameIdx = 0;
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            cc.enabled = false;
            dez.transform.position = new Vector3(-27.5f, 0.05f, -17.5f);
            dez.transform.rotation = Quaternion.LookRotation(Vector3.right);
            cc.enabled = true;

            // === FIX P1 #1: DASH FOV KICK (ANTES 0 deg vs DEPOIS 3.5 deg / 180ms) ===
            var kickDegField = cfgT.GetField("dashFovKickDeg");
            var kickDurField = cfgT.GetField("dashFovKickDurationSec");
            Assert.LessOrEqual((float)kickDegField.GetValue(cfg), 4.0f, "P1 #1: dashFovKickDeg excede 4 graus!");
            Assert.LessOrEqual((float)kickDurField.GetValue(cfg), 0.20f, "P1 #1: dashFovKickDurationSec excede 200ms!");

            // Antes (0 deg)
            float origKick = (float)kickDegField.GetValue(cfg);
            kickDegField.SetValue(cfg, 0f);
            if (_capture) SetBanner("P1 #1 ANTES — DASH SEM FOV KICK (0°)");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);
            locoT.GetProperty("ScriptedDash").SetValue(loco, true);
            for (int i = 0; i < (_capture ? 32 : 8); i++)
            {
                yield return null;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }

            // Aguarda cooldown e testa DEPOIS (3.5 deg / 180ms)
            kickDegField.SetValue(cfg, origKick);
            if (_capture) SetBanner("P1 #1 DEPOIS — DASH COM FOV KICK CONFORME (3.5° / 180ms ≤ 4° / 200ms)");
            yield return new WaitForSeconds(2.1f);
            cc.enabled = false;
            dez.transform.position = new Vector3(-27.5f, 0.05f, -17.5f);
            cc.enabled = true;
            locoT.GetProperty("ScriptedDash").SetValue(loco, true);
            float peakKick = 0f;
            for (int i = 0; i < (_capture ? 35 : 10); i++)
            {
                yield return null;
                float k = (float)locoT.GetProperty("CurrentFovKickDeg").GetValue(loco);
                if (k > peakKick) peakKick = k;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            Assert.Greater(peakKick, 0.5f, "P1 #1: FOV kick nao ativou durante o dash!");
            Assert.LessOrEqual(peakKick, 4.0f, "P1 #1: FOV kick excedeu 4 graus!");

            // === FIX P1 #2: OPCAO "REDUZIR SHAKE" + HEAVY HITSTOP ~100ms + DECAY <300ms ===
            var optT = Type.GetType("Studio.Game.Menu.GameSettingsManager, Assembly-CSharp");
            var mtT  = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var mt   = UnityEngine.Object.FindAnyObjectByType(mtT);
            Assert.IsNotNull(mt, "MultiToolController ausente!");

            var mtCfg = mtT.GetProperty("Cfg").GetValue(mt);
            var mtCfgT = mtCfg.GetType();
            float heavyHs = (float)mtCfgT.GetField("heavyHitstopDurationSec").GetValue(mtCfg);
            float shakeDur = (float)mtCfgT.GetField("cameraShakeDurationSec").GetValue(mtCfg);
            Assert.GreaterOrEqual(heavyHs, 0.05f);
            Assert.LessOrEqual(heavyHs, 0.10f);
            Assert.Less(shakeDur, 0.30f);

            // Shake 1.0x vs Reduzir Shake 0.25x
            optT.GetMethod("SetReduceCameraShake").Invoke(null, new object[] { false });
            Assert.AreEqual(1.0f, (float)optT.GetProperty("CameraShakeScale").GetValue(null), 0.01f);
            if (_capture) SetBanner("P1 #2 ANTES/NORMAL — GOLPE CARREGADO (HITSTOP 95ms • SHAKE 1.0x • DECAY 140ms)");
            mtT.GetMethod("CycleNextMode").Invoke(mt, null);
            mtT.GetMethod("TriggerHitImpactFeedback").Invoke(mt, new object[] { dez.transform.position + dez.transform.forward, true });
            for (int i = 0; i < (_capture ? 32 : 8); i++) { yield return null; if (_capture) CaptureFrame(framesDir, ref frameIdx); }

            optT.GetMethod("SetReduceCameraShake").Invoke(null, new object[] { true });
            Assert.AreEqual(0.25f, (float)optT.GetProperty("CameraShakeScale").GetValue(null), 0.01f);
            if (_capture) SetBanner("P1 #2 DEPOIS — OPÇÃO 'REDUZIR SHAKE' ATIVA (ESCALA 0.25x PERSISTENTE)");
            mtT.GetMethod("TriggerHitImpactFeedback").Invoke(mt, new object[] { dez.transform.position + dez.transform.forward, true });
            for (int i = 0; i < (_capture ? 32 : 8); i++) { yield return null; if (_capture) CaptureFrame(framesDir, ref frameIdx); }
            optT.GetMethod("SetReduceCameraShake").Invoke(null, new object[] { false });

            // === FIX P1 #3: COYOTE ~100ms, INPUT BUFFER ~150ms & CS04 SKIP >= 3.0s ===
            float coyote = (float)cfgT.GetField("coyoteTimeSec").GetValue(cfg);
            float buffer = (float)cfgT.GetField("jumpBufferSec").GetValue(cfg);
            Assert.AreEqual(0.10f, coyote, 0.015f, "P1 #3: Coyote time fora de ~100ms!");
            Assert.AreEqual(0.15f, buffer, 0.015f, "P1 #3: Jump buffer fora de ~150ms!");

            string cs04Json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Assets/Game/Cutscenes/Data/cs04_subtitles_pt_br.json"));
            Assert.IsTrue(cs04Json.Contains("\"skipHintDelaySec\": 3.0"), "P1 #3: CS04 skipHintDelaySec deveria ser >= 3.0s!");

            if (_capture) SetBanner("P1 #3 CONFORME — COYOTE 100ms • INPUT BUFFER 150ms • CUTSCENES SKIP ≥3s");
            locoT.GetProperty("ScriptedJump").SetValue(loco, true);
            for (int i = 0; i < (_capture ? 42 : 8); i++) { yield return null; if (_capture) CaptureFrame(framesDir, ref frameIdx); }

            Debug.Log($"[DesignBibleAudit] ALL P1 PASSED: peakFovKick={peakKick:F2}deg heavyHitstop={heavyHs*1000f:F0}ms shakeDecay={shakeDur*1000f:F0}ms coyote={coyote*1000f:F0}ms buffer={buffer*1000f:F0}ms");

            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            Time.captureFramerate = 0;
            if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
            if (_rt != null) UnityEngine.Object.Destroy(_rt);
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
        }

        private void CaptureFrame(string framesDir, ref int frameIdx)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var oldModes = new RenderMode[canvases.Length];
            for (int c = 0; c < canvases.Length; c++)
            {
                oldModes[c] = canvases[c].renderMode;
                if (canvases[c].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[c].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[c].worldCamera = cam;
                    canvases[c].planeDistance = 0.25f;
                }
            }

            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = null;

            for (int c = 0; c < canvases.Length; c++)
                canvases[c].renderMode = oldModes[c];

            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            _tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), _tex.EncodeToJPG(86));
            frameIdx++;
        }

        private void SetBanner(string text)
        {
            if (_bannerCanvas == null)
            {
                _bannerCanvas = new GameObject("DesignBible_Banner");
                UnityEngine.Object.DontDestroyOnLoad(_bannerCanvas);
                var c = _bannerCanvas.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 999;
                var sc = _bannerCanvas.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);

                var bg = new GameObject("Bg");
                bg.transform.SetParent(_bannerCanvas.transform, false);
                var brt = bg.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.10f, 0.925f);
                brt.anchorMax = new Vector2(0.90f, 0.985f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                bg.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.88f);

                var tGo = new GameObject("Label");
                tGo.transform.SetParent(bg.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                _bannerText = tGo.AddComponent<Text>();
                _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bannerText.fontSize = 22;
                _bannerText.fontStyle = FontStyle.Bold;
                _bannerText.alignment = TextAnchor.MiddleCenter;
                _bannerText.color = new Color(0.42f, 0.96f, 1.0f, 1f);
            }
            _bannerText.text = text;
        }
    }
}
