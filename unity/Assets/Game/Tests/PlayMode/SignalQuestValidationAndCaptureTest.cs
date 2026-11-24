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
    /// Valida TODOS os Gates da Missao "Sinal" (Ato 3 / Setor Externo):
    ///   - Gate 1: Compila limpo, 0 erros de console.
    ///   - Gate 2: MP4 1080p completo: conversa Vera -> 2 reparos de alimentadores -> captura na antena -> conversa do registro corrompido -> Portao Leste destravado.
    ///   - Gate 3: Interrompivel e retomavel: save automatico no meio da missao -> MainMenu -> Continuar retoma no passo exato (CaptureAntenna)!
    /// </summary>
    public sealed class SignalQuestValidationAndCaptureTest
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
        public IEnumerator ValidateSignalQuestFullChainInterruptResumeAndEastGateUnlock()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_SIGNAL") == "1";
            Time.captureFramerate = 30;

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string tempSave = Path.Combine(Application.temporaryCachePath, "quest_signal_gate3_save.json");
            var saveType = Type.GetType("Studio.Game.Save.SaveSystem, Assembly-CSharp");
            Assert.IsNotNull(saveType);
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { tempSave });
            saveType.GetMethod("Delete").Invoke(null, null);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 1);
            PlayerPrefs.DeleteKey("CONCERTO_EAST_GATE_UNLOCKED");
            PlayerPrefs.Save();

            if (_capture)
            {
                _capDir = Path.Combine(repoRoot, "captures", "quest_signal");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // 1. Carrega SetorExterno e inicia conversa introdutoria com a Engenheira Vera (data-driven!)
            var op = SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var qType     = Type.GetType("Studio.Game.Quests.SignalQuestController, Assembly-CSharp");
            var gateType  = Type.GetType("Studio.Game.Quests.EastGateInteractable, Assembly-CSharp");
            var capType   = Type.GetType("Studio.Game.Quests.AntennaCaptureInteractable, Assembly-CSharp");
            var dlgType   = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var npcType   = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var objType   = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            var repType   = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var repCtrlT  = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");

            var qCtrl = UnityEngine.Object.FindAnyObjectByType(qType);
            Assert.IsNotNull(qCtrl, "SignalQuestController ausente em SetorExterno!");
            Assert.AreEqual(0, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl));
            Assert.IsFalse((bool)gateType.GetProperty("IsEastGateUnlocked").GetValue(null), "Portao Leste deveria iniciar trancado!");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var veraGo = GameObject.Find("NPC_Engenheira_Vera");
            TeleportDezNear(dezGo, veraGo.transform.position, new Vector3(1.1f, 0f, -1.4f));

            var dlgCtrl = UnityEngine.Object.FindAnyObjectByType(dlgType);
            var veraNpc = veraGo.GetComponent(npcType);
            npcType.GetMethod("Interact").Invoke(veraNpc, new object[] { dezGo });
            yield return null;

            if (_capture) UpdateBanner("1 / 6 — MISSÃO 'SINAL': VERA PEDE PARA RESTAURAR A ANTENA");
            yield return StepFrames(_capture ? 42 : 2, "01_signal_conversa_intro_vera.png", _capture ? 28 : 0);
            yield return AdvanceDialogueToEnd(dlgType, dlgCtrl, _capture ? 14 : 1);

            Assert.AreEqual(1, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl), "Passo deveria avancar para RestoreFeeders (1)!");
            Assert.IsTrue(((string)objType.GetProperty("CurrentObjective").GetValue(null)).Contains("Restaure o alimentador"));
            Assert.IsTrue((bool)saveType.GetMethod("HasSave").Invoke(null, null), "Checkpoint 1 deveria ter gravado save!");

            // 2. Repara os 2 alimentadores do gerador (reusando Repairable + RepairRitualController!)
            var repCtrl = UnityEngine.Object.FindAnyObjectByType(repCtrlT);
            var allReps = UnityEngine.Object.FindObjectsByType(repType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            string[] feederIds = { "sector_anom_01_gerador", "sector_anom_03_carga" };

            for (int f = 0; f < feederIds.Length; f++)
            {
                Component targetRep = null;
                foreach (Component r in allReps)
                {
                    if ((string)repType.GetField("repairableId").GetValue(r) == feederIds[f])
                    {
                        targetRep = r;
                        break;
                    }
                }
                Assert.IsNotNull(targetRep, $"Alimentador '{feederIds[f]}' nao encontrado!");

                repType.GetField("repairDurationSec").SetValue(targetRep, _capture ? 1.6f : 0.4f);
                TeleportDezNear(dezGo, targetRep.transform.position, new Vector3(1.3f, 0f, -1.3f));

                if (_capture) UpdateBanner($"2 / 6 — RESTAURANDO ALIMENTADOR DO GERADOR ({f + 1}/2)");
                Assert.IsTrue((bool)repCtrlT.GetMethod("BeginRepair").Invoke(repCtrl, new object[] { targetRep }));
                repCtrlT.GetProperty("ScriptedHoldChannel").SetValue(repCtrl, true);
                int rGuard = 0;
                while ((bool)repCtrlT.GetProperty("IsRepairing").GetValue(repCtrl) && rGuard < 160)
                {
                    string shot = (f == 0 && rGuard == (_capture ? 22 : 1)) ? "02_signal_reparo_alimentadores.png" : null;
                    yield return StepFrames(1, shot, 0);
                    rGuard++;
                }
                repCtrlT.GetProperty("ScriptedHoldChannel").SetValue(repCtrl, false);
                yield return StepFrames(_capture ? 18 : 2);
            }

            Assert.AreEqual(2, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl), "Apos 2 alimentadores, passo deveria ser CaptureAntenna (2)!");
            Assert.AreEqual("Capture a leitura da antena", (string)objType.GetProperty("CurrentObjective").GetValue(null));

            // 3. GATE 3 (PROVA NO VIDEO): Interrompe no MEIO da missao -> MainMenu -> Continuar retoma no passo CaptureAntenna (2)!
            if (_capture) UpdateBanner("3 / 6 — GATE 3: SAVE NO MEIO → MENU → CONTINUAR RETOMA NA ANTENA!");
            var mmOp = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            while (mmOp != null && !mmOp.isDone) yield return null;
            yield return StepFrames(_capture ? 28 : 2);

            object[] contArgs = new object[] { null };
            Assert.IsTrue((bool)saveType.GetMethod("MarkContinueRequested").Invoke(null, contArgs));
            Assert.AreEqual("SetorExterno", contArgs[0] as string);

            var resumeOp = SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            while (resumeOp != null && !resumeOp.isDone) yield return null;
            yield return StepFrames(_capture ? 32 : 3, "03_signal_save_continuar_meio_missao.png", _capture ? 18 : 1);

            // Re-busca referencias na cena recarregada e valida que o passo 2 foi restaurado intacto!
            qCtrl = UnityEngine.Object.FindAnyObjectByType(qType);
            Assert.AreEqual(2, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl), "Gate 3 FALHOU: Continuar nao retomou no passo CaptureAntenna (2)!");
            Assert.AreEqual("Capture a leitura da antena", (string)objType.GetProperty("CurrentObjective").GetValue(null));

            // 4. Interage com a Antena -> sequencia curta de captura com feixe ciano + registro corrompido!
            dezGo = GameObject.Find("Player_Dez_Placeholder");
            var capComp = UnityEngine.Object.FindAnyObjectByType(capType) as Component;
            Assert.IsNotNull(capComp);
            Assert.IsTrue((bool)capType.GetProperty("IsAvailable").GetValue(capComp), "Captura da antena deveria estar habilitada apos Continuar!");

            TeleportDezNear(dezGo, capComp.transform.position, new Vector3(-2.6f, 0f, -2.6f));
            if (_capture) UpdateBanner("4 / 6 — CAPTURANDO LEITURA NA ANTENA (FEIXE CIANO + TELEMETRIA)");
            capType.GetMethod("Interact").Invoke(capComp, new object[] { dezGo });

            int capGuard = 0;
            while ((bool)capType.GetProperty("IsCapturing").GetValue(capComp) && capGuard < 220)
            {
                string shot = (capGuard == (_capture ? 58 : 2)) ? "04_signal_captura_antena.png" : null;
                yield return StepFrames(1, shot, 0);
                capGuard++;
            }
            yield return StepFrames(_capture ? 18 : 2);

            Assert.AreEqual(3, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl), "Apos captura, passo deveria ser ReportToVera (3)!");
            Assert.AreEqual("Mostre a leitura para Vera", (string)objType.GetProperty("CurrentObjective").GetValue(null));

            // 5. CORACAO DO VIDEO: Conversa de resultado com Vera (registro corrompido 14h02m11s -> Epicentro alem do Portao Leste!)
            veraGo = GameObject.Find("NPC_Engenheira_Vera");
            dlgCtrl = UnityEngine.Object.FindAnyObjectByType(dlgType);
            veraNpc = veraGo.GetComponent(npcType);
            TeleportDezNear(dezGo, veraGo.transform.position, new Vector3(1.1f, 0f, -1.4f));

            npcType.GetMethod("Interact").Invoke(veraNpc, new object[] { dezGo });
            yield return null;
            if (_capture) UpdateBanner("5 / 6 — LEITURA CORROMPIDA (14h02m11s): VERA APONTA O EPICENTRO");

            // Avanca ate a linha 2 (revelacao do timestamp exato) para tirar a screenshot heroica e segue ate o fim!
            var advM = dlgType.GetMethod("PressAdvanceInput");
            var activeP = dlgType.GetProperty("IsDialogueActive");
            int lineStep = 0;
            bool shotResult = false;
            while ((bool)activeP.GetValue(dlgCtrl) && lineStep < 24)
            {
                yield return StepFrames(_capture ? 22 : 1, (!shotResult && lineStep == 2) ? "05_signal_conversa_registro_corrompido.png" : null, _capture ? 18 : 0);
                if (lineStep == 2) shotResult = true;
                advM.Invoke(dlgCtrl, null);
                lineStep++;
            }
            yield return StepFrames(_capture ? 20 : 2);

            // 6. Valida conclusao: Portao Leste destravado + folhas abrindo + objetivo "Chegue ao epicentro"!
            Assert.AreEqual(4, (int)qType.GetProperty("CurrentStepInt").GetValue(qCtrl), "Missao deveria estar concluida (4)!");
            Assert.IsTrue((bool)gateType.GetProperty("IsEastGateUnlocked").GetValue(null), "Portao Leste deveria estar destravado!");
            Assert.AreEqual("Chegue ao epicentro", (string)objType.GetProperty("CurrentObjective").GetValue(null));

            var gateGo = GameObject.Find("EastGate_To_Epicenter");
            TeleportDezNear(dezGo, gateGo.transform.position, new Vector3(-4.2f, 0f, -1.2f));
            if (_capture) UpdateBanner("6 / 6 — PORTÃO LESTE DESTRAVADO → OBJETIVO: 'CHEGUE AO EPICENTRO'");
            yield return StepFrames(_capture ? 48 : 6, "06_signal_portao_leste_destravado.png", _capture ? 32 : 2);

            // Limpeza
            saveType.GetMethod("Delete").Invoke(null, null);
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { null });
            PlayerPrefs.DeleteKey("CONCERTO_EAST_GATE_UNLOCKED");
            PlayerPrefs.Save();
            Time.captureFramerate = 0;

            if (_capture)
            {
                if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }
        }

        private IEnumerator AdvanceDialogueToEnd(Type dlgType, object dlgCtrl, int framesPerPress)
        {
            var advM = dlgType.GetMethod("PressAdvanceInput");
            var activeP = dlgType.GetProperty("IsDialogueActive");
            int safety = 0;
            while ((bool)activeP.GetValue(dlgCtrl) && safety < 24)
            {
                advM.Invoke(dlgCtrl, null);
                yield return StepFrames(framesPerPress);
                safety++;
            }
        }

        private static void TeleportDezNear(GameObject dez, Vector3 targetPos, Vector3 offset)
        {
            var cc = dez.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dez.transform.position = new Vector3(targetPos.x + offset.x, 0.05f, targetPos.z + offset.z);
            dez.transform.LookAt(new Vector3(targetPos.x, 0.05f, targetPos.z));
            if (cc != null) cc.enabled = true;
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
                cam.targetTexture = null;

                for (int c = 0; c < canvases.Length; c++)
                    canvases[c].renderMode = oldModes[c];

                RenderTexture.active = _rt;
                _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                _tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(88));
                if (!string.IsNullOrEmpty(shotName) && i == shotAt)
                {
                    File.WriteAllBytes(Path.Combine(_capDir, shotName), _tex.EncodeToPNG());
                    Debug.Log($"[SignalCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void UpdateBanner(string text)
        {
            if (_bannerCanvas == null)
            {
                _bannerCanvas = new GameObject("Signal_Capture_Banner");
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
                brt.anchorMin = new Vector2(0.16f, 0.925f);
                brt.anchorMax = new Vector2(0.84f, 0.985f);
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
