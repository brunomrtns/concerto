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
    /// Valida TODOS os Gates de Movimento & Game Feel do Dez:
    ///   - Gate 1: Compila limpo, 0 erros, DezMovementConfig (SO) carregado
    ///   - Gate 2: Captura MP4 1080p (~65s @ 30fps, CAPTURE_MOVEMENT=1) demonstrando:
    ///       1. Corrida & Parada Brusca
    ///       2. Pulo (Alto vs Corte de Altura + Buffer)
    ///       3. Impulso Mecanico (Dash dos propulsores, cooldown 2s, SEM i-frames)
    ///       4. Camera 3a Pessoa Cinemachine 3.1.4 + Colisao com Parede
    ///       5. Combinacao livre na oficina
    /// </summary>
    public class DezMovementFeelValidationAndCaptureTest
    {
        private bool _capture;
        private string _capDir;
        private string _framesDir;
        private int _frameIdx;
        private RenderTexture _rt;
        private Texture2D _tex;
        private Text _bannerText;
        private GameObject _bannerCanvas;

        [UnityTest]
        public IEnumerator ValidateAndCaptureDezMovementFeel()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_MOVEMENT") == "1";

            // Pula Cutscene 1 para testar diretamente o gameplay na oficina
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            Time.captureFramerate = _capture ? 30 : 60;

#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Game/Scenes/Caldeiral.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
#else
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
#endif
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder nao encontrado!");

            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            Assert.IsNotNull(locoType, "DezLocomotionController nao encontrado!");
            var loco = dezGo.GetComponent(locoType);
            Assert.IsNotNull(loco);

            var cfgVal = locoType.GetField("config").GetValue(loco);
            Assert.IsNotNull(cfgVal, "DezMovementConfig (ScriptableObject) nao esta atribuido no DezLocomotionController!");

            // Garante Cinemachine 3a pessoa ativa
            var vcam = locoType.GetField("gameplayVcam").GetValue(loco) as Component;
            Assert.IsNotNull(vcam, "VCam_Gameplay_Dez nao atribuida!");
            var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            Assert.IsNotNull(brainType, "CinemachineBrain type nao encontrado!");
            var brain = Camera.main.GetComponent(brainType) as Behaviour;
            Assert.IsNotNull(brain, "CinemachineBrain ausente na Main Camera!");
            Assert.IsTrue(brain.enabled, "CinemachineBrain deveria estar ativo para a camera 3a pessoa!");

            // Regra canonica: Dash mecanico NAO tem i-frames (Eco e task futura!)
            Assert.IsFalse((bool)locoType.GetProperty("HasIFrames").GetValue(loco), "Dash mecanico NAO pode ter i-frames!");

            // Posiciona Dez em espaco aberto da oficina Z1 para showcase limpo
            var cc = dezGo.GetComponent<CharacterController>();
            cc.enabled = false;
            dezGo.transform.position = new Vector3(-28.0f, 0.05f, -20.5f);
            dezGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            cc.enabled = true;

            var useScriptedProp  = locoType.GetProperty("UseScriptedInput");
            var moveDirProp      = locoType.GetProperty("ScriptedMoveDir");
            var runProp          = locoType.GetProperty("ScriptedRun");
            var jumpProp         = locoType.GetProperty("ScriptedJump");
            var jumpHeldProp     = locoType.GetProperty("ScriptedJumpHeld");
            var dashProp         = locoType.GetProperty("ScriptedDash");
            var camYawDeltaProp  = locoType.GetProperty("ScriptedCamYawDelta");
            var curSpeedProp     = locoType.GetProperty("CurrentSpeed");
            var isDashingProp    = locoType.GetProperty("IsDashing");
            var dashCdProp       = locoType.GetProperty("DashCooldownRemaining");
            var tryDashMethod    = locoType.GetMethod("TryTriggerDash");

            useScriptedProp.SetValue(loco, true);
            jumpHeldProp.SetValue(loco, true);

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "movement_feel");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);

                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
                CreateShowcaseBanner();
            }

            int scale = _capture ? 1 : 6; // Em modo rapido sem captura, roda 6x mais curto mantendo todos os asserts!

            // === FASE 1 (0..14s): CORRIDA, CURVAS & PARADA BRUSCA ===
            SetBanner("1 / 5 — CORRIDA RESPONSIVA, CURVAS & PARADA BRUSCA");
            moveDirProp.SetValue(loco, Vector3.right);
            runProp.SetValue(loco, true);
            yield return StepFrames(90 / scale, shotName: "01_run_and_shoulder_cam.png", shotAt: 45 / scale);
            float runSpd = (float)curSpeedProp.GetValue(loco);
            Assert.Greater(runSpd, 4.5f, $"Velocidade de corrida deveria passar de 4.5 m/s! Atual: {runSpd:F2}");

            // Parada brusca!
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(45 / scale);
            float stopSpd = (float)curSpeedProp.GetValue(loco);
            Assert.Less(stopSpd, 0.25f, $"Parada brusca deveria zerar velocidade rapidamente! Atual: {stopSpd:F2}");

            // Corrida em curva / retorno
            moveDirProp.SetValue(loco, (Vector3.left + Vector3.forward * 0.4f).normalized);
            runProp.SetValue(loco, true);
            yield return StepFrames(120 / scale);
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(45 / scale);

            // Giro rapido esquerda-direita
            moveDirProp.SetValue(loco, Vector3.back);
            yield return StepFrames(60 / scale);
            moveDirProp.SetValue(loco, Vector3.forward);
            yield return StepFrames(60 / scale);
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(20 / scale);

            // === FASE 2 (14..28s): PULO ALTO vs CORTE DE ALTURA + BUFFER ===
            SetBanner("2 / 5 — PULO (ALTURA COMPLETA vs CORTE DE ALTURA + BUFFER)");
            float groundY = dezGo.transform.position.y;

            // Pulo completo (segurando Espaco)
            moveDirProp.SetValue(loco, Vector3.right * 0.6f);
            runProp.SetValue(loco, false);
            jumpHeldProp.SetValue(loco, true);
            jumpProp.SetValue(loco, true);
            float maxFullY = groundY;
            for (int i = 0; i < (60 / scale); i++)
            {
                yield return StepFrames(1, shotName: (i == (12 / scale)) ? "02_jump_variable_height.png" : null, shotAt: 0);
                if (dezGo.transform.position.y > maxFullY) maxFullY = dezGo.transform.position.y;
            }

            // Pulo curto (solta Espaco logo apos subir -> corte de altura!)
            jumpHeldProp.SetValue(loco, true);
            jumpProp.SetValue(loco, true);
            float maxShortY = groundY;
            for (int i = 0; i < (60 / scale); i++)
            {
                if (i == 3) jumpHeldProp.SetValue(loco, false);
                yield return StepFrames(1);
                if (dezGo.transform.position.y > maxShortY) maxShortY = dezGo.transform.position.y;
            }
            Assert.Greater(maxFullY - groundY, 0.85f, $"Pulo completo muito baixo: {maxFullY - groundY:F2}m");
            Assert.Less(maxShortY, maxFullY - 0.12f, $"Corte de altura nao reduziu o pulo curto! Full={maxFullY:F2}, Short={maxShortY:F2}");

            // Mais 2 pulos ritmados em movimento para o video
            jumpHeldProp.SetValue(loco, true);
            moveDirProp.SetValue(loco, Vector3.left);
            runProp.SetValue(loco, true);
            for (int k = 0; k < 2; k++)
            {
                jumpProp.SetValue(loco, true);
                yield return StepFrames(75 / scale);
            }
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(30 / scale);

            // === FASE 3 (28..42s): IMPULSO MECANICO (DASH — COOLDOWN 2s, SEM I-FRAMES) ===
            SetBanner("3 / 5 — IMPULSO MECÂNICO (PROPULSORES • COOLDOWN 2s • SEM I-FRAMES)");
            moveDirProp.SetValue(loco, Vector3.right);
            runProp.SetValue(loco, true);
            dashProp.SetValue(loco, true);
            yield return StepFrames(2);
            Assert.IsTrue((bool)isDashingProp.GetValue(loco), "Impulso mecanico deveria estar ativo!");
            Assert.Greater((float)curSpeedProp.GetValue(loco), 10.0f, "Velocidade do impulso deveria passar de 10 m/s!");
            Assert.Greater((float)dashCdProp.GetValue(loco), 1.5f, "Cooldown de ~2s deveria estar ativo!");
            // Tentar dar dash de novo imediatamente deve falhar por cooldown!
            Assert.IsFalse((bool)tryDashMethod.Invoke(loco, new object[] { Vector3.right }), "Cooldown de 2s deveria bloquear segundo dash imediato!");

            yield return StepFrames(18 / scale, shotName: "03_thruster_dash_burst.png", shotAt: 1);
            moveDirProp.SetValue(loco, Vector3.zero);
            // Aguarda cooldown de 2s + segundo dash de volta!
            yield return StepFrames(90); // 3s em 30fps (ou 1.5s em 60fps — se rapido, espera 2.1s reais)
            while ((float)dashCdProp.GetValue(loco) > 0.01f) yield return null;

            moveDirProp.SetValue(loco, Vector3.left);
            dashProp.SetValue(loco, true);
            yield return StepFrames(30 / scale);
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(120 / scale);

            // === FASE 4 (42..52s): CAMERA 3a PESSOA + COLISAO COM PAREDE ===
            SetBanner("4 / 5 — CÂMERA 3ª PESSOA (CINEMACHINE + COLISÃO COM PAREDE)");
            // Leva Dez bem perto da parede sul da oficina e gira a camera contra a parede!
            cc.enabled = false;
            dezGo.transform.position = new Vector3(-31.8f, 0.05f, -22.8f);
            dezGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            cc.enabled = true;

            float minWallCamDist = 99f;
            for (int i = 0; i < (180 / scale); i++)
            {
                camYawDeltaProp.SetValue(loco, 1.8f * scale);
                yield return StepFrames(1, shotName: (i == (85 / scale)) ? "04_camera_wall_collision.png" : null, shotAt: 0);
                float d = Vector3.Distance(Camera.main.transform.position, dezGo.transform.position + Vector3.up * 1.25f);
                if (d < minWallCamDist) minWallCamDist = d;
            }
            Assert.Less(minWallCamDist, 2.95f, $"Colisao de camera com parede nao aproximou a camera! MinDist={minWallCamDist:F2}m");

            // === FASE 5 (52..65s): COMBINACAO LIVRE NA OFICINA ===
            SetBanner("5 / 5 — COMBINAÇÃO LIVRE (CORRIDA + IMPULSO + PULO NA OFICINA)");
            cc.enabled = false;
            dezGo.transform.position = new Vector3(-29.0f, 0.05f, -20.0f);
            cc.enabled = true;

            moveDirProp.SetValue(loco, (Vector3.right + Vector3.forward * 0.35f).normalized);
            runProp.SetValue(loco, true);
            yield return StepFrames(45 / scale);
            while ((float)dashCdProp.GetValue(loco) > 0.01f) yield return null;
            dashProp.SetValue(loco, true);
            yield return StepFrames(30 / scale);
            jumpHeldProp.SetValue(loco, true);
            jumpProp.SetValue(loco, true);
            yield return StepFrames(75 / scale);
            moveDirProp.SetValue(loco, Vector3.left);
            yield return StepFrames(120 / scale);
            moveDirProp.SetValue(loco, Vector3.zero);
            yield return StepFrames(60 / scale);

            useScriptedProp.SetValue(loco, false);
            Time.captureFramerate = 0;

            if (_capture)
            {
                if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
                Debug.Log($"[MovementFeelCapture] Captura concluida: {_frameIdx} frames gravados!");
            }
        }

        private IEnumerator StepFrames(int count, string shotName = null, int shotAt = -1)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
                if (!_capture) continue;

                var cam = Camera.main;
                if (cam == null) continue;

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
                RenderTexture.active = _rt;
                _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                _tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;

                for (int c = 0; c < canvases.Length; c++)
                {
                    if (oldModes[c] == RenderMode.ScreenSpaceOverlay)
                        canvases[c].renderMode = RenderMode.ScreenSpaceOverlay;
                }

                File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(90));
                if (!string.IsNullOrEmpty(shotName) && i == shotAt)
                {
                    File.WriteAllBytes(Path.Combine(_capDir, shotName), _tex.EncodeToPNG());
                    Debug.Log($"[MovementFeelCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void CreateShowcaseBanner()
        {
            _bannerCanvas = new GameObject("MovementFeel_ShowcaseBanner");
            var c = _bannerCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 95;
            var sc = _bannerCanvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var bar = new GameObject("BannerBar");
            bar.transform.SetParent(_bannerCanvas.transform, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(960f, 48f);
            bar.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.85f);

            var txtGo = new GameObject("BannerText");
            txtGo.transform.SetParent(bar.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _bannerText = txtGo.AddComponent<Text>();
            _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bannerText.fontSize = 22;
            _bannerText.fontStyle = FontStyle.Bold;
            _bannerText.color = new Color(0.99f, 0.85f, 0.58f, 1f);
            _bannerText.alignment = TextAnchor.MiddleCenter;
        }

        private void SetBanner(string msg)
        {
            if (_bannerText != null) _bannerText.text = msg;
        }
    }
}
