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
    /// Valida TODOS os Gates do novo cenario "Setor Externo":
    ///   - Gate 1: Compila limpo, 0 erros
    ///   - Gate 2: Walkthrough completo: Oficina -> Porta (trancada -> destravada) -> Setor Externo ->
    ///       Ceu Errado em destaque -> Conversa real com Engenheira Vera (objetivo Ato 3) -> Reparar 1 anomalia do setor!
    ///   - Gate 3: Mede performance (FPS / tempo de frame) na cena nova e registra no log + HUD da captura
    ///   - Captura 1080p MP4 (CAPTURE_SECTOR=1) + 5 screenshots em captures/setor_externo/
    /// </summary>
    public class SetorExternoWalkthroughTest
    {
        private bool _capture;
        private bool _camOverride;
        private Vector3 _camOverridePos;
        private Vector3 _camOverrideLook;
        private string _capDir;
        private string _framesDir;
        private int _frameIdx;
        private RenderTexture _rt;
        private Texture2D _tex;
        private Text _bannerText;
        private GameObject _bannerCanvas;

        [UnityTest]
        public IEnumerator ValidateWorkshopDoorToSetorExternoVeraDialogueAndRepair()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_SECTOR") == "1";

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 0);
            PlayerPrefs.Save();
            Time.captureFramerate = _capture ? 30 : 60;

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "setor_externo");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // 1. Carrega Oficina (Caldeiral) -> verifica porta trancada -> destrava -> entra no Setor Externo!
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var doorGo = GameObject.Find("Door_Caldeiral_To_Sector");
            Assert.IsNotNull(doorGo, "Door_Caldeiral_To_Sector ausente em Caldeiral.unity!");
            var doorType = Type.GetType("Studio.Game.Sector.SectorDoorInteractable, Assembly-CSharp");
            var door = doorGo.GetComponent(doorType);
            Assert.IsFalse((bool)doorType.GetProperty("IsUnlockedNow").GetValue(door), "Porta deveria iniciar trancada antes da flag!");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var cc = dezGo.GetComponent<CharacterController>();
            cc.enabled = false;
            dezGo.transform.position = doorGo.transform.position + new Vector3(-2.1f, 0.05f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            cc.enabled = true;

            // Destrava porta (flag debug / futura Cutscene 4)
            doorType.GetProperty("IsSectorUnlocked").SetValue(null, true);
            doorType.GetMethod("RefreshStatusLight").Invoke(door, null);
            Assert.IsTrue((bool)doorType.GetProperty("IsUnlockedNow").GetValue(door), "Porta deveria estar destravada!");

            if (_capture) CreateBanner("1 / 5 — PORTA DA OFICINA DESTRAVADA → SAINDO PARA O SETOR EXTERNO");
            yield return StepFrames(_capture ? 40 : 4, "01_oficina_porta_setor.png", _capture ? 18 : 1);

            // Interage com a porta -> carrega SetorExterno!
            doorType.GetMethod("Interact").Invoke(door, new object[] { dezGo });
            int guard = 0;
            while (SceneManager.GetActiveScene().name != "SetorExterno" && guard < 180) { yield return null; guard++; }
            Assert.AreEqual("SetorExterno", SceneManager.GetActiveScene().name, "Cena SetorExterno nao carregou!");
            yield return null;
            yield return null;

            // Ativa as 3 anomalias do setor
            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            Assert.IsNotNull(anomMgr, "WorkshopAnomalyManager ausente em SetorExterno!");
            anomMgrType.GetMethod("ActivateAllAnomalies").Invoke(anomMgr, new object[] { false });
            Assert.AreEqual(3, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Setor Externo deveria ter 3 anomalias ativas!");

            // 2. Destaque do CEU ERRADO (Rachadura Luminosa + Aurora Glitch) + Medicao de Performance (Gate 3)
            var cam = Camera.main;
            var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            var brain = brainType != null ? cam.GetComponent(brainType) as Behaviour : null;
            if (brain != null) brain.enabled = false;
            _camOverride = true;
            _camOverridePos = new Vector3(0f, 2.2f, 1.5f);
            _camOverrideLook = new Vector3(0f, 14.5f, 44f);

            float t0 = Time.realtimeSinceStartup;
            int perfFrames = _capture ? 65 : 20;
            if (_capture) CreateBanner("2 / 5 — SETOR EXTERNO: O CÉU ERRADO (RACHADURA + AURORA GLITCH)");
            yield return StepFrames(perfFrames, "02_setor_ceu_errado_destaque.png", _capture ? 30 : 5);
            float avgMs = ((Time.realtimeSinceStartup - t0) / perfFrames) * 1000f;
            Debug.Log($"[SetorExternoPerf] Gate 3 Stats: {perfFrames} frames, avg={avgMs:F1}ms (batchmode capture={_capture})");

            // 3. Visao do patio com os 3 POIs TripoSG (Gerador, Antena, Carga) + Vera + Rachadura ao fundo
            _camOverridePos = new Vector3(0f, 5.2f, -3.5f);
            _camOverrideLook = new Vector3(0f, 5.8f, 22f);
            if (_capture) CreateBanner("3 / 5 — PÁTIO INDUSTRIAL (GERADOR • ANTENA • CARGA • ENGENHEIRA VERA)");
            yield return StepFrames(_capture ? 45 : 4, "03_setor_patio_pois_triposg.png", _capture ? 20 : 1);

            // 4. Conversa real data-driven com a Engenheira Vera -> seta objetivo do Ato 3!
            _camOverride = false;
            var veraGo = GameObject.Find("NPC_Engenheira_Vera");
            Assert.IsNotNull(veraGo, "NPC_Engenheira_Vera ausente em SetorExterno!");
            dezGo = GameObject.Find("Player_Dez_Placeholder");
            cc = dezGo.GetComponent<CharacterController>();
            cc.enabled = false;
            dezGo.transform.position = veraGo.transform.position + new Vector3(1.1f, 0f, -1.3f);
            dezGo.transform.LookAt(veraGo.transform.position);
            cc.enabled = true;
            _camOverride = true;
            _camOverridePos = veraGo.transform.position + new Vector3(2.4f, 1.55f, -2.5f);
            _camOverrideLook = veraGo.transform.position + Vector3.up * 0.9f;

            var npcDlgType = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var dlgCtrlType = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var veraNpc = veraGo.GetComponent(npcDlgType);
            var dlgCtrl = UnityEngine.Object.FindAnyObjectByType(dlgCtrlType);
            Assert.IsNotNull(veraNpc);
            Assert.IsNotNull(dlgCtrl);

            npcDlgType.GetMethod("Interact").Invoke(veraNpc, new object[] { dezGo });
            yield return null;
            Assert.IsTrue((bool)dlgCtrlType.GetProperty("IsDialogueActive").GetValue(dlgCtrl), "Dialogo com Vera deveria iniciar!");

            if (_capture) CreateBanner("4 / 5 — CONVERSA COM ENGENHEIRA VERA (DATA-DRIVEN • OBJETIVO ATO 3)");
            yield return StepFrames(_capture ? 50 : 4, "04_setor_conversa_vera.png", _capture ? 28 : 1);

            // Avanca todas as linhas da conversa ate concluir e setar o objetivo do Ato 3!
            var advMethod = dlgCtrlType.GetMethod("PressAdvanceInput");
            int safety = 0;
            while ((bool)dlgCtrlType.GetProperty("IsDialogueActive").GetValue(dlgCtrl) && safety < 20)
            {
                advMethod.Invoke(dlgCtrl, null);
                yield return StepFrames(_capture ? 14 : 1);
                safety++;
            }

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            string act3Obj = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.IsTrue(act3Obj != null && (act3Obj.Contains("Setor Externo") || act3Obj.Contains("alimentador")), $"Objetivo do Ato 3 nao foi setado apos Vera! Atual: '{act3Obj}'");

            // 5. Repara 1 anomalia do Setor Externo usando o MESMO sistema Repairable / RepairRitualController!
            _camOverride = false;
            if (brain != null) brain.enabled = true;
            var repType = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var repCtrlType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
            var repCtrl = UnityEngine.Object.FindAnyObjectByType(repCtrlType);
            var sectorRep = UnityEngine.Object.FindAnyObjectByType(repType) as Component;
            Assert.IsNotNull(sectorRep, "Nenhum Repairable encontrado no Setor Externo!");

            repType.GetField("repairDurationSec").SetValue(sectorRep, _capture ? 2.5f : 0.6f);
            cc.enabled = false;
            dezGo.transform.position = new Vector3(sectorRep.transform.position.x + 1.2f, 0.05f, sectorRep.transform.position.z - 1.2f);
            dezGo.transform.LookAt(new Vector3(sectorRep.transform.position.x, 0.05f, sectorRep.transform.position.z));
            cc.enabled = true;

            Assert.IsTrue((bool)repCtrlType.GetMethod("BeginRepair").Invoke(repCtrl, new object[] { sectorRep }), "Falha ao iniciar reparo no Setor Externo!");
            repCtrlType.GetProperty("ScriptedHoldChannel").SetValue(repCtrl, true);
            if (_capture) CreateBanner("5 / 5 — REPARANDO ANOMALIA NO SETOR EXTERNO (MESMO SISTEMA REUSADO!)");

            int rGuard = 0;
            while ((bool)repCtrlType.GetProperty("IsRepairing").GetValue(repCtrl) && rGuard < 180)
            {
                yield return StepFrames(1, rGuard == (_capture ? 32 : 2) ? "05_setor_reparo_anomalia.png" : null, 0);
                rGuard++;
            }
            repCtrlType.GetProperty("ScriptedHoldChannel").SetValue(repCtrl, false);
            yield return StepFrames(_capture ? 25 : 4);

            Assert.AreEqual(2, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Reparo no Setor Externo deveria reduzir anomalias ativas de 3 para 2!");

            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.Save();
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
                var cam = Camera.main;
                if (cam == null) continue;
                if (_camOverride)
                {
                    cam.transform.position = _camOverridePos;
                    cam.transform.LookAt(_camOverrideLook);
                }

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
                    Debug.Log($"[SectorCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void CreateBanner(string msg)
        {
            if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
            _bannerCanvas = new GameObject("Sector_ShowcaseBanner");
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
            bar.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.13f, 0.88f);

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
            _bannerText.color = new Color(0.35f, 0.92f, 1.0f, 1f);
            _bannerText.alignment = TextAnchor.MiddleCenter;
            _bannerText.text = msg;
        }
    }
}
