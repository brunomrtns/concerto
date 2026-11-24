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
    /// Valida TODOS os Gates da Ferramenta Multi-Uso (CONCERTAR / COMBATE):
    ///   - Gate 1: Compila limpo, 0 erros, MultiToolConfig carregado
    ///   - Gate 2: Reparo continua funcionando identico em modo CONCERTAR e custa 0 celulas SEMPRE!
    ///   - Gate 3: MP4 1080p (CAPTURE_TOOL=1) + 4 screenshots demonstrando:
    ///       troca de modos, combo leve (2 golpes, custo 0), golpe carregado (custo 0),
    ///       tiro carregado consumindo 1 celula, coleta de celula, e reparo gratis!
    /// </summary>
    public class MultiToolValidationAndCaptureTest
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
        public IEnumerator ValidateMultiToolModesCombatEnergyAndFreeRepair()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_TOOL") == "1";

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_ANOMALIES_ACTIVE");
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

            var toolType   = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var combatType = Type.GetType("Studio.Game.Tool.CombatToolModule, Assembly-CSharp");
            var dummyType  = Type.GetType("Studio.Game.Tool.WorkshopTrainingTarget, Assembly-CSharp");
            var cellType   = Type.GetType("Studio.Game.Tool.EnergyCellPickup, Assembly-CSharp");
            Assert.IsNotNull(toolType);

            var tool   = UnityEngine.Object.FindAnyObjectByType(toolType);
            var combat = UnityEngine.Object.FindAnyObjectByType(combatType);
            var dummy  = UnityEngine.Object.FindAnyObjectByType(dummyType) as Component;
            Assert.IsNotNull(tool, "MultiToolController ausente em Caldeiral.unity!");
            Assert.IsNotNull(combat, "CombatToolModule ausente!");
            Assert.IsNotNull(dummy, "Tool_TrainingTarget_Dummy ausente!");
            Assert.IsNotNull(toolType.GetField("config").GetValue(tool), "MultiToolConfig nao atribuido!");

            // Posiciona Dez de frente para o alvo de treino
            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var cc = dezGo.GetComponent<CharacterController>();
            cc.enabled = false;
            dezGo.transform.position = dummy.transform.position + new Vector3(-1.35f, -0.70f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            cc.enabled = true;

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "tool_system");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
                CreateBanner();
            }

            var isRepairProp = toolType.GetProperty("IsRepairModeActive");
            var cellsProp    = toolType.GetProperty("EnergyCells");
            var hitstopProp  = toolType.GetProperty("HitstopTriggerCount");
            var cycleMethod  = toolType.GetMethod("CycleNextMode");

            // 1. Comeca em modo CONCERTAR
            SetBanner("1 / 5 — MODO CONCERTAR (ATIVO POR PADRÃO • CUSTO 0 SEMPRE)");
            Assert.IsTrue((bool)isRepairProp.GetValue(tool), "Ferramenta deve iniciar em modo CONCERTAR!");
            int startCells = (int)cellsProp.GetValue(tool);
            yield return StepFrames(_capture ? 45 : 4, "01_mode_concertar_hud.png", _capture ? 20 : 1);

            // 2. Troca para modo COMBATE (Tab)
            SetBanner("2 / 5 — TROCA DE MODO [TAB] → MODO COMBATE");
            cycleMethod.Invoke(tool, null);
            Assert.IsFalse((bool)isRepairProp.GetValue(tool), "CycleNextMode deveria trocar para COMBATE!");
            yield return StepFrames(_capture ? 35 : 4);

            // 3. Combo leve de 2 golpes (custo 0 celulas!) + Hitstop + Shake
            SetBanner("3 / 5 — COMBO LEVE (2 GOLPES • CUSTO 0 • HITSTOP + SHAKE)");
            combatType.GetMethod("ExecuteLightComboStep").Invoke(combat, null);
            yield return StepFrames(_capture ? 13 : 20, "02_combat_light_combo_hit.png", _capture ? 4 : 1);
            combatType.GetMethod("ExecuteLightComboStep").Invoke(combat, null);
            Assert.AreEqual(2, (int)combatType.GetProperty("LastComboStep").GetValue(combat), "Combo leve deveria chegar ao golpe 2!");
            Assert.AreEqual(startCells, (int)cellsProp.GetValue(tool), "Combo leve NAO pode consumir celulas!");
            Assert.GreaterOrEqual((int)hitstopProp.GetValue(tool), 1, "Hitstop deveria ter disparado no acerto!");
            yield return StepFrames(_capture ? 30 : 4);

            // 4. Golpe carregado (custo 0 celulas!)
            SetBanner("4 / 5 — GOLPE CARREGADO (IMPACTO PESADO • CUSTO 0)");
            yield return new WaitForSecondsRealtime(0.35f);
            combatType.GetMethod("ExecuteHeavyStrike").Invoke(combat, null);
            Assert.AreEqual(startCells, (int)cellsProp.GetValue(tool), "Golpe carregado NAO pode consumir celulas!");
            yield return StepFrames(_capture ? 40 : 6);

            // 5. Tiro carregado (Bolt de energia consumindo 1 celula!) + Coleta de celula
            SetBanner("5 / 5 — TIRO DE ENERGIA (CONSOME 1 CÉLULA) + REPARO GRÁTIS");
            cc.enabled = false;
            dezGo.transform.position = dummy.transform.position + new Vector3(-2.6f, -0.70f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            cc.enabled = true;
            yield return StepFrames(_capture ? 15 : 2);

            bool fired = (bool)combatType.GetMethod("TryFireEnergyBolt").Invoke(combat, null);
            Assert.IsTrue(fired, "Tiro carregado deveria disparar!");
            Assert.AreEqual(startCells - 1, (int)cellsProp.GetValue(tool), "Tiro carregado deveria consumir exatamente 1 celula!");
            yield return StepFrames(_capture ? 35 : 8, "03_combat_energy_bolt_cell.png", _capture ? 4 : 1);

            // Coleta uma celula coletavel do cenario!
            var cell = UnityEngine.Object.FindAnyObjectByType(cellType);
            Assert.IsNotNull(cell, "Nenhuma EnergyCellPickup encontrada na cena!");
            Assert.IsTrue((bool)cellType.GetMethod("TryCollect").Invoke(cell, null), "Coleta de celula falhou!");
            Assert.AreEqual(startCells, (int)cellsProp.GetValue(tool), "Coleta de celula deveria restaurar o contador!");

            // 6. Volta para modo CONCERTAR e prova que o reparo continua 100% gratis (0 celulas)!
            cycleMethod.Invoke(tool, null);
            Assert.IsTrue((bool)isRepairProp.GetValue(tool), "Deveria voltar para modo CONCERTAR!");

            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            anomMgrType.GetMethod("ActivateAllAnomalies").Invoke(anomMgr, new object[] { false });
            yield return null;

            var repType = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var firstRep = UnityEngine.Object.FindAnyObjectByType(repType) as Component;
            Assert.IsTrue((bool)repType.GetMethod("CanInteract").Invoke(firstRep, new object[] { dezGo }), "Gate 2 FALHOU: Repairable deveria aceitar interacao no modo CONCERTAR!");
            int cellsBeforeRepair = (int)cellsProp.GetValue(tool);
            repType.GetMethod("OnRepairCompleted").Invoke(firstRep, null);
            Assert.AreEqual(cellsBeforeRepair, (int)cellsProp.GetValue(tool), "REGRA INVIOLAVEL: Reparo custa 0 celulas SEMPRE!");
            yield return StepFrames(_capture ? 35 : 4, "04_free_repair_concertar_mode.png", _capture ? 15 : 1);

            Time.timeScale = 1f;
            Time.captureFramerate = 0;

            if (_capture)
            {
                UnityEngine.Object.Destroy(_bannerCanvas);
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
                    Debug.Log($"[ToolCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void CreateBanner()
        {
            _bannerCanvas = new GameObject("Tool_ShowcaseBanner");
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
            rt.sizeDelta = new Vector2(960f, 48f);
            bar.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.86f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(bar.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _bannerText = tGo.AddComponent<Text>();
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
