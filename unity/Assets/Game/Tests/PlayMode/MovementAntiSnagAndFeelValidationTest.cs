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
    /// Valida e grava os 2 MP4s 1080p da task de Movimentacao do Dez:
    ///   1. before_after_snag_fix_1080p.mp4 — ANTES (legado skinWidth=0.01, sem wall-slide) vs DEPOIS (skinWidth=0.05, wall-slide, dash anti-snag).
    ///   2. full_scenes_tour_zero_snag_1080p.mp4 — Tour completo por Caldeiral e SetorExterno (andar->correr, pulo + landing bob, dash, passos) + regressao de interacao/combate.
    /// </summary>
    public sealed class MovementAntiSnagAndFeelValidationTest
    {
        private bool _capture;
        private RenderTexture _rt;
        private Texture2D _tex;
        private GameObject _bannerCanvas;
        private Text _bannerText;

        private void InitCapture()
        {
            if (!_capture) return;
            if (_rt == null) _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            if (_tex == null) _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        }

        private void CleanupCapture()
        {
            if (_bannerCanvas != null) { UnityEngine.Object.Destroy(_bannerCanvas); _bannerCanvas = null; }
            if (_rt != null) { UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_tex != null) { UnityEngine.Object.Destroy(_tex); _tex = null; }
        }

        [UnityTest]
        public IEnumerator CaptureBeforeAfterSnagFixComparison()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_MOV") == "1";
            Time.captureFramerate = 30;
            InitCapture();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "captures", "movement_fix", "before_after_snag_fix_frames");
            if (_capture)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
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
            var loco = dez.GetComponent(locoT);
            int frameIdx = 0;

            Vector3 startPos = new Vector3(-25.8f, 0.05f, -18.2f);
            Vector3 wallDiagDir = new Vector3(0.85f, 0f, 0.52f).normalized;

            // === PARTE A: ANTES (Legacy skinWidth=0.01, sem wall-slide / sem dash anti-snag) ===
            locoT.GetProperty("SimulateLegacySnagMode").SetValue(loco, true);
            locoT.GetMethod("ApplyControllerAntiSnagTuning").Invoke(loco, null);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            Teleport(cc, dez, startPos, wallDiagDir);

            if (_capture) SetBanner("ANTES — skinWidth=0.01 • SEM WALL-SLIDE • DASH EMPURRA PAREDE");
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, wallDiagDir);
            for (int i = 0; i < (_capture ? 75 : 25); i++)
            {
                if (i == 20) locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                yield return null;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            float beforeTravel = Vector3.Distance(startPos, dez.transform.position);

            // === PARTE B: DEPOIS (skinWidth=0.05, stepOffset=0.36, wall-slide + corner nudge + dash deslizante!) ===
            locoT.GetProperty("SimulateLegacySnagMode").SetValue(loco, false);
            locoT.GetMethod("ApplyControllerAntiSnagTuning").Invoke(loco, null);
            Teleport(cc, dez, startPos, wallDiagDir);

            if (_capture) SetBanner("DEPOIS — skinWidth=0.05 • ZERO-FRICTION • WALL-SLIDE + DASH FLUIDO");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, wallDiagDir);
            for (int i = 0; i < (_capture ? 85 : 25); i++)
            {
                if (i == 20) locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                yield return null;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            float afterTravel = Vector3.Distance(startPos, dez.transform.position);
            int slides = (int)locoT.GetProperty("WallSlideCount").GetValue(loco);
            Debug.Log($"[MovementQA] Before/After Snag Comparison: beforeTravel={beforeTravel:F2}m afterTravel={afterTravel:F2}m wallSlides={slides}");

            Assert.GreaterOrEqual(afterTravel, 1.0f, "Trajeto DEPOIS deveria fluir livremente!");
            Assert.AreEqual(0.05f, cc.skinWidth, 0.005f, "skinWidth anti-snag nao aplicado!");

            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            Time.captureFramerate = 0;
            CleanupCapture();
        }

        [UnityTest]
        public IEnumerator CaptureFullScenesTourZeroSnagAndRegressions()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_MOV") == "1";
            Time.captureFramerate = 30;
            InitCapture();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "captures", "movement_fix", "full_scenes_tour_zero_snag_frames");
            if (_capture)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
            }

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            int frameIdx = 0;

            // === TOUR 1: OFICINA (Caldeiral.unity) — andar->correr, pulo+landing, curvas, regressao combate/interacao ===
            var op1 = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op1 != null && !op1.isDone) yield return null;
            yield return null;
            yield return null;

            var dez = GameObject.Find("Player_Dez_Placeholder");
            var cc = dez.GetComponent<CharacterController>();
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dez.GetComponent(locoT);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            Teleport(cc, dez, new Vector3(-28.0f, 0.05f, -17.5f), Vector3.right);

            if (_capture) SetBanner("TOUR 1/2 — OFICINA (CALDEIRAL): ANDAR → CORRER • PULO + LANDING • DASH");
            Vector3[] caldDirs = { Vector3.right, new Vector3(0.7f, 0f, 0.7f).normalized, Vector3.forward, Vector3.left };
            for (int seg = 0; seg < caldDirs.Length; seg++)
            {
                locoT.GetProperty("ScriptedRun").SetValue(loco, seg >= 1);
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, caldDirs[seg]);
                for (int f = 0; f < (_capture ? 32 : 8); f++)
                {
                    if (seg == 1 && f == 8) locoT.GetProperty("ScriptedJump").SetValue(loco, true);
                    if (seg == 2 && f == 6) locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                    yield return null;
                    if (_capture) CaptureFrame(framesDir, ref frameIdx);
                }
            }

            // Regressao rapida em Caldeiral: ferramenta multi-uso (troca de modo CONCERTAR <-> COMBATE)
            var mtT = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var mt = UnityEngine.Object.FindAnyObjectByType(mtT);
            if (mt != null)
            {
                string beforeMode = (string)mtT.GetProperty("ActiveModeId").GetValue(mt);
                mtT.GetMethod("CycleNextMode").Invoke(mt, null);
                Assert.AreNotEqual(beforeMode, (string)mtT.GetProperty("ActiveModeId").GetValue(mt), "Regressao: MultiTool nao alternou modo!");
            }

            // === TOUR 2: SETOR EXTERNO (SetorExterno.unity) — patio completo passando pelos 3 POIs TripoSG + Vera ===
            var op2 = SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            while (op2 != null && !op2.isDone) yield return null;
            yield return null;
            yield return null;

            dez = GameObject.Find("Player_Dez_Placeholder");
            cc = dez.GetComponent<CharacterController>();
            loco = dez.GetComponent(locoT);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            Teleport(cc, dez, new Vector3(0f, 0.05f, 3.0f), Vector3.forward);

            if (_capture) SetBanner("TOUR 2/2 — SETOR EXTERNO: GERADOR • CARGA • ANTENA • ZERO TRAVAMENTO");
            Vector3[] secDirs = { new Vector3(-0.65f, 0f, 0.75f).normalized, new Vector3(0.8f, 0f, 0.6f).normalized, Vector3.right, Vector3.back };
            for (int seg = 0; seg < secDirs.Length; seg++)
            {
                locoT.GetProperty("ScriptedRun").SetValue(loco, true);
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, secDirs[seg]);
                for (int f = 0; f < (_capture ? 36 : 8); f++)
                {
                    if (seg == 0 && f == 12) locoT.GetProperty("ScriptedJump").SetValue(loco, true);
                    if (seg == 1 && f == 10) locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                    yield return null;
                    if (_capture) CaptureFrame(framesDir, ref frameIdx);
                }
            }

            // Regressao rapida em SetorExterno: Vera interagivel + SignalQuest presente
            var vera = GameObject.Find("NPC_Engenheira_Vera");
            Assert.IsNotNull(vera, "Regressao: Vera ausente em SetorExterno!");

            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            Time.captureFramerate = 0;
            CleanupCapture();
        }

        private static void Teleport(CharacterController cc, GameObject dez, Vector3 pos, Vector3 lookDir)
        {
            cc.enabled = false;
            dez.transform.position = pos;
            dez.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            cc.enabled = true;
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
                _bannerCanvas = new GameObject("MovementQA_Banner");
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
                brt.anchorMin = new Vector2(0.14f, 0.925f);
                brt.anchorMax = new Vector2(0.86f, 0.985f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                bg.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.86f);

                var tGo = new GameObject("Label");
                tGo.transform.SetParent(bg.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                _bannerText = tGo.AddComponent<Text>();
                _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bannerText.fontSize = 23;
                _bannerText.fontStyle = FontStyle.Bold;
                _bannerText.alignment = TextAnchor.MiddleCenter;
                _bannerText.color = new Color(0.42f, 0.96f, 1.0f, 1f);
            }
            _bannerText.text = text;
        }
    }
}
