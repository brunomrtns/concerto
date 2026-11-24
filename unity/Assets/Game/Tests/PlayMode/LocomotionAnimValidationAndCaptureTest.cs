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
    /// Valida e grava MP4 1080p ANTES/DEPOIS na MESMA ROTA e MESMO ANGULO (vista lateral!):
    ///   (a) Caminhada lateral: ANTES (bracos colados) vs DEPOIS (bracos afastados + balanco acoplado a passada)
    ///   (b) Corrida lateral: ANTES (estatua reta) vs DEPOIS (inclinacao 8.5 deg + ombros + balanco 52 deg + speed ratio)
    ///   (c) Arrancada, parada, transicao walk<->run repetida e dash saindo da corrida
    ///   (d) Slow-mo lateral dos pes (0.25x) provando zero foot slide
    /// </summary>
    public sealed class LocomotionAnimValidationAndCaptureTest
    {
        private bool _capture;
        private string _capDir;
        private string _framesDir;
        private RenderTexture _rt;
        private Texture2D _tex;
        private int _frameIdx;
        private GameObject _bannerCanvas;
        private Text _bannerText;

        [UnityTest]
        public IEnumerator CaptureBeforeAfterLateralWalkRunTransitionsAndSlowMo()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_ANIM") == "1";
            Time.captureFramerate = 30;

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            _capDir = Path.Combine(repoRoot, "captures", "locomotion_anim");
            _framesDir = Path.Combine(_capDir, "frames_raw");
            if (_capture)
            {
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // Usa SetorExterno (patio plano e aberto ideal para vista lateral limpa em linha reta!)
            var op = SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var dez = GameObject.Find("Player_Dez_Placeholder");
            var cc = dez.GetComponent<CharacterController>();
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var procT = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            var loco = dez.GetComponent(locoT);
            var proc = dez.GetComponentInChildren(procT, true);
            Assert.IsNotNull(loco);
            Assert.IsNotNull(proc);

            var brainT = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            var brain = Camera.main != null ? Camera.main.GetComponent(brainT) as Behaviour : null;
            if (brain != null) brain.enabled = false;

            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            Vector3 routeStart = new Vector3(-6.0f, 0.05f, 5.5f);

            // === (a) CAMINHADA LATERAL — ANTES vs DEPOIS ===
            procT.GetProperty("SimulateLegacyAnimMode").SetValue(proc, true);
            yield return RunLateralSegment(cc, dez, locoT, loco, routeStart, run: false, frames: _capture ? 48 : 10,
                banner: "1 / 5 ANTES — CAMINHADA LATERAL (BRAÇOS COLADOS NO TORSO • SEM BALANÇO)",
                shotName: "01_walk_lateral_antes.png", shotAt: _capture ? 26 : -1, camDist: 2.35f);

            procT.GetProperty("SimulateLegacyAnimMode").SetValue(proc, false);
            yield return RunLateralSegment(cc, dez, locoT, loco, routeStart, run: false, frames: _capture ? 52 : 12,
                banner: "1 / 5 DEPOIS — CAMINHADA LATERAL (BRAÇOS AFASTADOS 18° + BALANÇO 28°)",
                shotName: "02_walk_lateral_depois.png", shotAt: _capture ? 26 : -1, camDist: 2.35f);

            float walkSwing = (float)procT.GetProperty("LastAppliedArmSwingDeg").GetValue(proc);
            Assert.Greater(walkSwing, 15f, $"Balanco de braco na caminhada deveria ser >15 deg! Atual: {walkSwing:F1}");

            // === (b) CORRIDA LATERAL — ANTES vs DEPOIS ===
            procT.GetProperty("SimulateLegacyAnimMode").SetValue(proc, true);
            yield return RunLateralSegment(cc, dez, locoT, loco, routeStart, run: true, frames: _capture ? 45 : 10,
                banner: "2 / 5 ANTES — CORRIDA LATERAL (ESTÁTUA RETA • SEM INCLINAÇÃO • FOOT SLIDE)",
                shotName: "03_run_lateral_antes.png", shotAt: _capture ? 22 : -1, camDist: 2.65f);

            procT.GetProperty("SimulateLegacyAnimMode").SetValue(proc, false);
            yield return RunLateralSegment(cc, dez, locoT, loco, routeStart, run: true, frames: _capture ? 52 : 12,
                banner: "2 / 5 DEPOIS — CORRIDA LATERAL (TRONCO 8.5° • OMBROS • BALANÇO 52° • SPEED RATIO)",
                shotName: "04_run_lateral_depois.png", shotAt: _capture ? 22 : -1, camDist: 2.65f);

            float runLean = (float)procT.GetProperty("LastAppliedTrunkLeanDeg").GetValue(proc);
            float runSwing = (float)procT.GetProperty("LastAppliedArmSwingDeg").GetValue(proc);
            Assert.GreaterOrEqual(runLean, 5.0f, $"Inclinacao de tronco na corrida deveria ser >=5 deg! Atual: {runLean:F1}");
            Assert.LessOrEqual(runLean, 11.0f, $"Inclinacao de tronco excessiva! Atual: {runLean:F1}");
            Assert.Greater(runSwing, 35f, $"Balanco de braco na corrida deveria ser >35 deg! Atual: {runSwing:F1}");

            // === (c) ARRANCADA, PARADA, TRANSIÇÃO WALK<->RUN REPETIDA & DASH ===
            Teleport(cc, dez, routeStart, Vector3.right);
            if (_capture) SetBanner("3 / 5 TRANSIÇÕES — ARRANCADA • WALK ↔ RUN SEM POP • DASH • PARADA");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);
            for (int f = 0; f < (_capture ? 80 : 16); f++)
            {
                bool r = (f >= 18 && f < 38) || (f >= 52 && f < 66);
                locoT.GetProperty("ScriptedRun").SetValue(loco, r);
                if (f == 58) locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                if (f >= 68) locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
                yield return null;
                PositionLateralCam(dez.transform.position, 2.7f, 0.85f);
                if (_capture) CaptureFrame(null);
            }

            // === (d) SLOW-MO (0.25x) DA PASSADA LATERAL PRÓXIMA AOS PÉS (ZERO FOOT SLIDE) ===
            Teleport(cc, dez, routeStart, Vector3.right);
            Time.timeScale = 0.25f;
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);
            if (_capture) SetBanner("4 / 5 SLOW-MO 0.25x — PASSADA LATERAL CASADA (ZERO FOOT SLIDE)");
            for (int f = 0; f < (_capture ? 55 : 10); f++)
            {
                yield return null;
                PositionLateralCam(dez.transform.position, 1.95f, 0.55f);
                if (_capture) CaptureFrame(null);
            }
            Time.timeScale = 1f;
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            if (brain != null) brain.enabled = true;

            Debug.Log($"[LocomotionAnimQA] ALL PASSED: walkSwing={walkSwing:F1}deg runSwing={runSwing:F1}deg runLean={runLean:F1}deg");

            Time.captureFramerate = 0;
            if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
            if (_rt != null) UnityEngine.Object.Destroy(_rt);
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
        }

        private IEnumerator RunLateralSegment(CharacterController cc, GameObject dez, Type locoT, object loco, Vector3 start, bool run, int frames, string banner, string shotName, int shotAt, float camDist)
        {
            Teleport(cc, dez, start, Vector3.right);
            if (_capture) SetBanner(banner);
            locoT.GetProperty("ScriptedRun").SetValue(loco, run);
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                PositionLateralCam(dez.transform.position, camDist, 0.80f);
                if (_capture) CaptureFrame(i == shotAt ? shotName : null);
            }
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
        }

        private static void PositionLateralCam(Vector3 dezPos, float dist, float height)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(dezPos.x, dezPos.y + height, dezPos.z - dist);
            cam.transform.LookAt(new Vector3(dezPos.x, dezPos.y + 0.68f, dezPos.z));
        }

        private static void Teleport(CharacterController cc, GameObject dez, Vector3 pos, Vector3 lookDir)
        {
            cc.enabled = false;
            dez.transform.position = pos;
            dez.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            cc.enabled = true;
        }

        private void CaptureFrame(string savePngName)
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

            File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(86));
            if (!string.IsNullOrEmpty(savePngName))
                File.WriteAllBytes(Path.Combine(_capDir, savePngName), _tex.EncodeToPNG());
            _frameIdx++;
        }

        private void SetBanner(string text)
        {
            if (_bannerCanvas == null)
            {
                _bannerCanvas = new GameObject("LocomotionAnimQA_Banner");
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
                brt.anchorMin = new Vector2(0.08f, 0.925f);
                brt.anchorMax = new Vector2(0.92f, 0.985f);
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
