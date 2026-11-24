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
    /// Valida TODOS os Gates do Sistema de Save Real (JSON versionado em persistentDataPath) + Checkpoints:
    ///   - Gate 1: Compila limpo, 0 erros
    ///   - Gate 2 (PROVA REAL no MP4): meio do Ato 2 (2 anomalias resolvidas, 2 celulas, posicao especifica, HUD) ->
    ///       Save JSON -> fecha/reabre MainMenu -> Continuar -> restaura ponto EXATO, anomalias, celulas e HUD!
    ///       + prova de Respawn no ultimo checkpoint preservando o mundo!
    ///   - Gate 3: Corromper arquivo de save manualmente (JSON quebrado + versao futura) -> fallback seguro sem crash!
    /// </summary>
    public class SaveSystemValidationAndCaptureTest
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
        public IEnumerator ValidateRealJsonSaveContinueRespawnAndCorruptFallback()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_SAVE") == "1";

            var saveSysType = Type.GetType("Studio.Game.Save.SaveSystem, Assembly-CSharp");
            Assert.IsNotNull(saveSysType, "SaveSystem nao encontrado!");

            // Usa arquivo temporario isolado em persistentDataPath para nao sujar save do usuario
            string testSavePath = Path.Combine(Application.persistentDataPath, "concerto_save_test_v1.json");
            saveSysType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { testSavePath });
            saveSysType.GetMethod("Delete").Invoke(null, null);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            Time.captureFramerate = _capture ? 30 : 60;

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "save_system");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // === FASE 1: Entra em Caldeiral no meio do Ato 2 (2 anomalias resolvidas, 2 celulas, posicao especifica) ===
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            anomMgrType.GetMethod("ActivateAllAnomalies").Invoke(anomMgr, new object[] { false });

            var allAnoms = anomMgrType.GetMethod("GetAllAnomalies").Invoke(anomMgr, null) as System.Collections.IList;
            allAnoms[0].GetType().GetMethod("Resolve").Invoke(allAnoms[0], null);
            allAnoms[1].GetType().GetMethod("Resolve").Invoke(allAnoms[1], null);
            Assert.AreEqual(3, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr));

            var toolType = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var tool = UnityEngine.Object.FindAnyObjectByType(toolType);
            toolType.GetMethod("TryConsumeBoltCost").Invoke(tool, null); // 3 -> 2 celulas
            Assert.AreEqual(2, (int)toolType.GetProperty("EnergyCells").GetValue(tool));

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            objType.GetMethod("Set").Invoke(null, new object[] { "Repare as anomalias (2/5)" });

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var cc = dezGo.GetComponent<CharacterController>();
            Vector3 savedSpot = new Vector3(-27.2f, 0.05f, -19.2f);
            cc.enabled = false;
            dezGo.transform.position = savedSpot;
            dezGo.transform.rotation = Quaternion.Euler(0f, 120f, 0f);
            cc.enabled = true;

            // Dispara Save real no checkpoint!
            saveSysType.GetMethod("Save").Invoke(null, new object[] { "cp_ato2_meio", (Vector3?)savedSpot, (float?)120f });
            Assert.IsTrue(File.Exists(testSavePath), "Arquivo JSON de save nao foi gravado em persistentDataPath!");
            string rawJson = File.ReadAllText(testSavePath);
            Assert.IsTrue(rawJson.Contains("\"schemaVersion\": 2"), "Schema versionado v2 ausente no JSON!");

            if (_capture) CreateBanner("1 / 4 — MEIO DO ATO 2 SALVO EM JSON (2/5 ANOMALIAS • 2 CÉLULAS)");
            yield return StepFrames(_capture ? 45 : 4, "01_act2_midprogress_saved_icon.png", _capture ? 12 : 1);

            // === FASE 2: Simula fechar e reabrir o app (carrega MainMenu) -> Continuar! ===
            var opMenu = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            while (opMenu != null && !opMenu.isDone) yield return null;
            yield return null;

            var menuType = Type.GetType("Studio.Game.Menu.BootAndMainMenuController, Assembly-CSharp");
            var menu = UnityEngine.Object.FindAnyObjectByType(menuType);
            menuType.GetMethod("SkipBootSplash").Invoke(menu, null);
            yield return null;
            Assert.IsTrue((bool)saveSysType.GetMethod("HasSave").Invoke(null, null), "Menu deveria detectar o save JSON real!");

            if (_capture) CreateBanner("2 / 4 — APP REABERTO → MENU PRINCIPAL COM [CONTINUAR] ATIVO");
            yield return StepFrames(_capture ? 45 : 4, "02_main_menu_continue_real_save.png", _capture ? 20 : 1);

            // Clica Continuar -> carrega Caldeiral e restaura EXATAMENTE o ponto salvo!
            menuType.GetMethod("OnContinueClicked").Invoke(menu, null);
            int guard = 0;
            while (SceneManager.GetActiveScene().name != "Caldeiral" && guard < 180) { yield return null; guard++; }
            yield return null;
            yield return null;

            // === VERIFICA RESTAURACAO EXATA (Gate 2) ===
            dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.Less(Vector3.Distance(dezGo.transform.position, savedSpot), 0.25f, "Gate 2 FALHOU: Posicao do Dez nao foi restaurada exatamente!");
            anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            Assert.AreEqual(3, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Gate 2 FALHOU: Anomalias resolvidas (2/5) nao foram restauradas!");
            tool = UnityEngine.Object.FindAnyObjectByType(toolType);
            Assert.AreEqual(2, (int)toolType.GetProperty("EnergyCells").GetValue(tool), "Gate 2 FALHOU: Celulas de energia (2) nao foram restauradas!");
            Assert.AreEqual("Repare as anomalias (2/5)", (string)objType.GetProperty("CurrentObjective").GetValue(null), "Gate 2 FALHOU: Objetivo nao foi restaurado!");

            if (_capture) CreateBanner("3 / 4 — CONTINUAR: POSIÇÃO, ANOMALIAS (2/5), CÉLULAS E HUD RESTAURADOS!");
            yield return StepFrames(_capture ? 50 : 4, "03_restored_exact_point_hud.png", _capture ? 20 : 1);

            // === PROVA DE RESPAWN NO ULTIMO CHECKPOINT (KNOCKDOWN PRESERVA MUNDO!) ===
            cc = dezGo.GetComponent<CharacterController>();
            cc.enabled = false;
            dezGo.transform.position = new Vector3(-22.0f, 0.05f, -15.0f);
            cc.enabled = true;
            var cached = saveSysType.GetProperty("CachedState").GetValue(null);
            var cpVecObj = cached.GetType().GetField("checkpointPosition").GetValue(cached);
            Vector3 expectedCp = (Vector3)cpVecObj.GetType().GetMethod("ToVector3").Invoke(cpVecObj, null);
            saveSysType.GetMethod("RespawnPlayerAtLastCheckpoint").Invoke(null, null);
            yield return null;
            Assert.Less(Vector3.Distance(dezGo.transform.position, expectedCp), 0.25f, "Respawn de knockdown nao devolveu Dez ao ultimo checkpoint!");
            Assert.AreEqual(3, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Respawn perdeu estado do mundo!");

            if (_capture) CreateBanner("4 / 4 — RESPAWN NO CHECKPOINT PRESERVANDO O MUNDO + TESTE DE CORRUPÇÃO");
            yield return StepFrames(_capture ? 45 : 4, "04_checkpoint_respawn_preserved.png", _capture ? 20 : 1);

            // === GATE 3: PROVA DE FALLBACK SEGURO COM ARQUIVO CORROMPIDO & VERSAO INVALIDA ===
            File.WriteAllText(testSavePath, "{ CORRUPTED_SAVE_FILE_BY_TEST !!!");
            Assert.IsFalse((bool)saveSysType.GetMethod("HasSave").Invoke(null, null), "Gate 3 FALHOU: Save corrompido deveria retornar HasSave() == false sem crashar!");
            Assert.IsTrue(File.Exists(testSavePath + ".corrupt.bak"), "Gate 3: Backup .corrupt.bak deveria ter sido criado!");

            File.WriteAllText(testSavePath, "{\"schemaVersion\": 999, \"sceneName\": \"Caldeiral\"}");
            Assert.IsFalse((bool)saveSysType.GetMethod("HasSave").Invoke(null, null), "Gate 3 FALHOU: Versao futura incompativel deveria cair no fallback seguro!");

            // Limpeza final
            saveSysType.GetMethod("Delete").Invoke(null, null);
            if (File.Exists(testSavePath + ".corrupt.bak")) File.Delete(testSavePath + ".corrupt.bak");
            saveSysType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { null });
            Time.captureFramerate = 0;

            if (_capture)
            {
                if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }
        }

        private IEnumerator StepFrames(int count, string shotName = null, int shotAt = -1)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
                if (!_capture) continue;
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
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
                        canvases[c].planeDistance = Mathf.Max(cam.nearClipPlane + 0.5f, 1.0f);
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
                    Debug.Log($"[SaveCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void CreateBanner(string msg)
        {
            if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
            _bannerCanvas = new GameObject("Save_ShowcaseBanner");
            var c = _bannerCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 95;
            var sc = _bannerCanvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var bar = new GameObject("Bar");
            bar.transform.SetParent(_bannerCanvas.transform, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(1040f, 48f);
            bar.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.88f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(bar.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _bannerText = tGo.AddComponent<Text>();
            _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bannerText.fontSize = 21;
            _bannerText.fontStyle = FontStyle.Bold;
            _bannerText.color = new Color(0.30f, 0.88f, 0.96f, 1f);
            _bannerText.alignment = TextAnchor.MiddleCenter;
            _bannerText.text = msg;
        }
    }
}
