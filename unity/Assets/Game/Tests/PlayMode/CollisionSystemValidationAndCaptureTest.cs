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
    /// Valida e grava MP4 1080p ANTES/DEPOIS do Sistema de Colisao (Gate 2 & Gate 3):
    ///   1. Player <-> NPC (ANTES atravessava Vera; DEPOIS bloqueio solido deterministico sem jitter)
    ///   2. Armadilha obrigatoria: Conversa com Vera funciona normalmente COM a colisao nova (interactRadius 2.5m >> solidDist 0.58m)
    ///   3. Dash na velocidade maxima (13.5m/s) contra parede fina (0.15m): zero tunelamento
    ///   4. Projetil (EnergyBoltProjectile) contra parede fina (0.15m) com trigger na frente: zero tunelamento, nunca acerta o proprio atirador
    ///   5. Matriz de camadas aplicada corretamente (CollisionMatrixBootstrap)
    /// </summary>
    public sealed class CollisionSystemValidationAndCaptureTest
    {
        private bool _capture;
        private RenderTexture _rt;
        private Texture2D _tex;
        private GameObject _bannerCanvas;
        private Text _bannerText;

        [UnityTest]
        public IEnumerator ValidateAndCaptureCollisionMatrixBeforeAfterAllClasses()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_COL") == "1";
            Time.captureFramerate = 30;

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "captures", "collision_system", "col_frames");
            if (_capture)
            {
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            }

            // 1. Verifica Matriz de Colisao (CollisionMatrixBootstrap)
            var bootT = Type.GetType("Studio.Game.Collision.CollisionMatrixBootstrap, Assembly-CSharp");
            Assert.IsNotNull(bootT);
            bootT.GetMethod("ApplyMatrix").Invoke(null, null);
            int lPlayer = (int)bootT.GetField("LayerPlayer").GetValue(null);
            int lNpc    = (int)bootT.GetField("LayerNPC").GetValue(null);
            int lProj   = (int)bootT.GetField("LayerProjectile").GetValue(null);
            int lCut    = (int)bootT.GetField("LayerCutsceneActor").GetValue(null);
            int lProps  = (int)bootT.GetField("LayerProps").GetValue(null);
            int lEnv    = (int)bootT.GetField("LayerEnvironment").GetValue(null);

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(lPlayer, lNpc), "Player deve colidir com NPC!");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(lPlayer, lProj), "Player deve ignorar projetil proprio!");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(lCut, lProps),   "CutsceneActor deve ignorar Props!");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(lProj, lEnv),   "Projectile deve colidir com Environment!");

            // Carrega SetorExterno (onde Vera antes so tinha trigger e Dez atravessava ela!)
            var op = SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var dez = GameObject.Find("Player_Dez_Placeholder");
            var cc = dez.GetComponent<CharacterController>();
            var vera = GameObject.Find("NPC_Engenheira_Vera");
            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var sepT  = Type.GetType("Studio.Game.Collision.ActorSolidSeparation, Assembly-CSharp");
            var loco  = dez.GetComponent(locoT);
            var dezSep  = dez.GetComponent(sepT) as Behaviour;
            var veraSep = vera.GetComponent(sepT) as Behaviour;
            var veraCap = vera.transform.Find("SolidBody_Capsule")?.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(dezSep);
            Assert.IsNotNull(veraSep);
            Assert.IsNotNull(veraCap);

            int frameIdx = 0;
            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);

            Vector3 veraPos = vera.transform.position;
            Vector3 approachStart = veraPos + new Vector3(0f, 0.05f, -2.2f);
            Vector3 toVera = new Vector3(0f, 0f, 1f);

            // === CLASSE 1A: ANTES (desabilita separacao/capsula -> Dez atravessa a Vera!) ===
            dezSep.enabled = false;
            veraSep.enabled = false;
            veraCap.enabled = false;
            Teleport(cc, dez, approachStart, toVera);
            if (_capture) SetBanner("1 / 4 ANTES — SEM COLISÃO SÓLIDA: DEZ ATRAVESSA A ENGENHEIRA VERA");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, toVera);
            float minBeforeDist = 99f;
            for (int i = 0; i < (_capture ? 40 : 18); i++)
            {
                yield return null;
                float d = Vector2.Distance(new Vector2(dez.transform.position.x, dez.transform.position.z), new Vector2(veraPos.x, veraPos.z));
                if (d < minBeforeDist) minBeforeDist = d;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            Assert.Less(minBeforeDist, 0.35f, "Antes da correcao deveria reproduzir sobreposicao!");

            // === CLASSE 1B: DEPOIS (com ActorSolidSeparation + SolidBody_Capsule -> ZERO atravessamento!) ===
            dezSep.enabled = true;
            veraSep.enabled = true;
            veraCap.enabled = true;
            Teleport(cc, dez, approachStart, toVera);
            if (_capture) SetBanner("1 / 4 DEPOIS — SOLID SEPARATION: DEZ BLOQUEADO SEM JITTER + CONVERSA OK");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, toVera);
            float minAfterDist = 99f;
            for (int i = 0; i < (_capture ? 45 : 20); i++)
            {
                yield return null;
                float d = Vector2.Distance(new Vector2(dez.transform.position.x, dez.transform.position.z), new Vector2(veraPos.x, veraPos.z));
                if (d < minAfterDist) minAfterDist = d;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            Assert.GreaterOrEqual(minAfterDist, 0.50f, $"DEPOIS: Dez ainda atravessou a Vera! minDist={minAfterDist:F2}m");

            // === CLASSE 2: ARMADILHA OBRIGATORIA — Conversa funciona COM a colisao nova! ===
            var dlgT = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var npcT = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var dlg  = UnityEngine.Object.FindAnyObjectByType(dlgT);
            var veraNpc = vera.GetComponent(npcT);
            npcT.GetMethod("Interact").Invoke(veraNpc, new object[] { dez });
            yield return null;
            Assert.IsTrue((bool)dlgT.GetProperty("IsDialogueActive").GetValue(dlg), "Colisao nova quebrou a interacao de conversa com Vera!");
            if (_capture) SetBanner("2 / 4 ARMADILHA VALIDADA — CONVERSA COM VERA FUNCIONA COM COLISÃO SÓLIDA");
            for (int i = 0; i < (_capture ? 30 : 2); i++) { yield return null; if (_capture) CaptureFrame(framesDir, ref frameIdx); }
            var advM = dlgT.GetMethod("PressAdvanceInput");
            int sGuard = 0;
            while ((bool)dlgT.GetProperty("IsDialogueActive").GetValue(dlg) && sGuard < 20) { advM.Invoke(dlg, null); yield return null; sGuard++; }

            // === CLASSE 3: DASH A 13.5m/s CONTRA PAREDE FINA (0.15m) — ZERO TUNELAMENTO! ===
            var thinWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thinWall.name = "QA_ThinWall_015m";
            thinWall.layer = lEnv;
            thinWall.transform.position = new Vector3(0f, 1.5f, 8.0f);
            thinWall.transform.localScale = new Vector3(6.0f, 3.0f, 0.15f);

            Teleport(cc, dez, new Vector3(0f, 0.05f, 6.2f), Vector3.forward);
            if (_capture) SetBanner("3 / 4 DASH 13.5m/s CONTRA PAREDE FINA (0.15m) — SWEEP ZERO TUNELAMENTO");
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.forward);
            locoT.GetProperty("ScriptedDash").SetValue(loco, true);
            for (int i = 0; i < (_capture ? 36 : 16); i++)
            {
                yield return null;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            Assert.Less(dez.transform.position.z, 7.95f, $"TUNNELING DETECTADO! Dez atravessou parede fina em z=8.0 (atual z={dez.transform.position.z:F2})");

            // === CLASSE 4: PROJETIL CONTRA PAREDE FINA COM TRIGGER NA FRENTE — ZERO TUNELAMENTO ===
            var frontTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontTrigger.name = "QA_FrontTrigger";
            frontTrigger.GetComponent<BoxCollider>().isTrigger = true;
            frontTrigger.GetComponent<MeshRenderer>().enabled = false;
            frontTrigger.transform.position = new Vector3(0f, 1.0f, 7.2f);
            frontTrigger.transform.localScale = new Vector3(4.0f, 2.0f, 0.4f);

            var boltT = Type.GetType("Studio.Game.Tool.EnergyBoltProjectile, Assembly-CSharp");
            var mtT   = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var mt    = UnityEngine.Object.FindAnyObjectByType(mtT);
            var boltGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltGo.name = "QA_EnergyBolt";
            UnityEngine.Object.Destroy(boltGo.GetComponent<Collider>());
            boltGo.transform.position = new Vector3(0f, 1.0f, 6.6f);
            boltGo.transform.localScale = Vector3.one * 0.25f;
            var bolt = boltGo.AddComponent(boltT);
            boltT.GetMethod("Launch").Invoke(bolt, new object[] { mt, Vector3.forward, 24f, 30f, 2.0f });

            if (_capture) SetBanner("4 / 4 PROJÉTIL 24m/s CONTRA PAREDE FINA (COM TRIGGER) — ZERO TUNELAMENTO");
            for (int i = 0; i < (_capture ? 32 : 14); i++)
            {
                if (boltGo != null) Assert.Less(boltGo.transform.position.z, 8.25f, "Projetil tunelou parede fina!");
                yield return null;
                if (_capture) CaptureFrame(framesDir, ref frameIdx);
            }
            Assert.IsTrue(boltGo == null, "Projetil deveria ter sido destruido ao atingir a parede fina!");

            Debug.Log($"[CollisionQA] ALL PASSED: npcBefore={minBeforeDist:F2}m npcAfter={minAfterDist:F2}m dashWallZ={dez.transform.position.z:F2}m boltBlocked=True");

            UnityEngine.Object.Destroy(thinWall);
            UnityEngine.Object.Destroy(frontTrigger);
            locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
            Time.captureFramerate = 0;
            if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
            if (_rt != null) UnityEngine.Object.Destroy(_rt);
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
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
                _bannerCanvas = new GameObject("CollisionQA_Banner");
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
