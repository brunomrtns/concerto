using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Valida TODOS os Gates do Sistema de Reparo ("O Concerto" — Ato 2):
    ///   - Gate 1: Compila limpo, 0 erros
    ///   - Gate 2: Nenhuma anomalia reparavel antes da CS03; pos-CS03 repara todas as 5 -> Beat Final canonico dispara!
    ///   - Gate 3 (Extensibilidade): Adicionar uma 6a anomalia Repairable sem tocar em codigo -> TotalTargets == 6!
    ///   - Gate 4 (Cancelamento): Cancelar no meio do minigame restaura estado 100% limpo sem bug!
    ///   - Gate 5: Captura MP4 1080p (CAPTURE_REPAIR=1) + 4 screenshots em captures/repair_act2/
    /// </summary>
    public class RepairSystemValidationAndCaptureTest
    {
        private bool _capture;
        private string _capDir;
        private string _framesDir;
        private int _frameIdx;
        private RenderTexture _rt;
        private Texture2D _tex;

        [UnityTest]
        public IEnumerator ValidateAct2RepairLoopCancelExtensibilityAndFinalBeat()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_REPAIR") == "1";

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_ANOMALIES_ACTIVE");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_COMPLETED");
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

            var repCtrlType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
            var repType     = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            Assert.IsNotNull(repCtrlType);
            Assert.IsNotNull(repType);
            Assert.IsNotNull(anomMgrType);

            var repCtrl = UnityEngine.Object.FindAnyObjectByType(repCtrlType);
            var anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            Assert.IsNotNull(repCtrl, "RepairRitualController ausente em Caldeiral.unity!");
            Assert.IsNotNull(anomMgr, "WorkshopAnomalyManager ausente em Caldeiral.unity!");

            var repairables = UnityEngine.Object.FindObjectsByType(repType, FindObjectsSortMode.None);
            Assert.AreEqual(5, repairables.Length, "Deveriam existir 5 Repairables vinculados as 5 anomalias!");

            // === GATE 2 (Pre-CS03): NENHUMA anomalia pode ser reparavel antes da Cutscene 3! ===
            foreach (var r in repairables)
                Assert.IsFalse((bool)repType.GetProperty("CanBeRepairedNow").GetValue(r), "Gate 2 FALHOU: Anomalia reparavel antes da Cutscene 3!");

            // === GATE 3 (PROVA DE EXTENSIBILIDADE SEM CODIGO): 6a anomalia via config/inspector -> TotalTargets == 6! ===
            var sixthGo = new GameObject("Temp_6th_Repairable_Proof");
            var sixthRep = sixthGo.AddComponent(repType);
            repType.GetField("displayName").SetValue(sixthRep, "Valvula Extra (Prova 6a)");
            int countWith6th = (int)repCtrlType.GetMethod("RefreshTargets").Invoke(repCtrl, null);
            Assert.AreEqual(6, countWith6th, "Gate 3 FALHOU: Adicionar 6o Repairable deveria atualizar TotalTargets para 6 sem tocar em codigo!");
            UnityEngine.Object.DestroyImmediate(sixthGo);
            int countBackTo5 = (int)repCtrlType.GetMethod("RefreshTargets").Invoke(repCtrl, null);
            Assert.AreEqual(5, countBackTo5, "TotalTargets deveria voltar a 5 apos remover a 6a temporaria!");

            // Ativa as 5 anomalias (simulando conclusao da Cutscene 3)
            anomMgrType.GetMethod("ActivateAllAnomalies").Invoke(anomMgr, new object[] { false });
            yield return null;

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var cc = dezGo.GetComponent<CharacterController>();
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;

            var isRepairingProp = repCtrlType.GetProperty("IsRepairing");
            var isFinalBeatProp = repCtrlType.GetProperty("IsPlayingFinalBeat");
            var act2DoneProp    = repCtrlType.GetProperty("Act2Completed");
            var holdProp        = repCtrlType.GetProperty("ScriptedHoldChannel");
            var beginMethod     = repCtrlType.GetMethod("BeginRepair");
            var cancelMethod    = repCtrlType.GetMethod("CancelRepair");

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "repair_act2");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // === GATE 4: CANCELAMENTO LIMPO NO MEIO DO REPARO ===
            var firstRep = repairables[0] as Component;
            TeleportDezNear(cc, dezGo, firstRep.transform.position);
            Assert.IsTrue((bool)beginMethod.Invoke(repCtrl, new object[] { firstRep }), "Deveria iniciar reparo da anomalia 1!");
            holdProp.SetValue(repCtrl, true);
            yield return StepFrames(25);
            Assert.IsTrue((bool)isRepairingProp.GetValue(repCtrl), "Ainda deveria estar em reparo no meio do progresso!");
            cancelMethod.Invoke(repCtrl, null);
            holdProp.SetValue(repCtrl, false);
            yield return StepFrames(10);

            Assert.IsFalse((bool)isRepairingProp.GetValue(repCtrl), "Gate 4 FALHOU: CancelRepair nao encerrou o modo de reparo!");
            Assert.IsTrue((bool)repType.GetProperty("CanBeRepairedNow").GetValue(firstRep), "Gate 4 FALHOU: Anomalia cancelada deveria continuar disponivel!");
            Assert.IsTrue(loco.enabled, "Gate 4 FALHOU: Controle do Dez nao foi devolvido apos cancelar!");

            // === GATE 2: REPARAR TODAS AS 5 ANOMALIAS + BEAT FINAL CANONICO ===
            for (int i = 0; i < repairables.Length; i++)
            {
                var rComp = repairables[i] as Component;
                // Acelera levemente o ritual no teste rapido sem captura para rodar em segundos
                if (!_capture) repType.GetField("repairDurationSec").SetValue(rComp, 1.0f);
                else repType.GetField("repairDurationSec").SetValue(rComp, 3.2f);

                TeleportDezNear(cc, dezGo, rComp.transform.position);
                yield return StepFrames(_capture ? 18 : 2);

                Assert.IsTrue((bool)beginMethod.Invoke(repCtrl, new object[] { rComp }), $"Falha ao iniciar reparo {i + 1}/5!");
                holdProp.SetValue(repCtrl, true);

                int guard = 0;
                while ((bool)isRepairingProp.GetValue(repCtrl) && guard < 240)
                {
                    string shot = (i == 0 && guard == 38) ? "01_repair_ritual_channeling.png" : null;
                    yield return StepFrames(1, shotName: shot, shotAt: 0);
                    guard++;
                }
                holdProp.SetValue(repCtrl, false);
                Assert.IsTrue((bool)repType.GetProperty("IsRepaired").GetValue(rComp), $"Anomalia {i + 1}/5 nao foi marcada como reparada!");

                string postShot = (i == 0) ? "02_repair_resolution_flash.png"
                                : (i == 2) ? "03_repair_hud_progress.png"
                                : null;
                yield return StepFrames(_capture ? 28 : 4, shotName: postShot, shotAt: _capture ? 4 : 0);
            }

            // Aguarda o Beat Final Canonico (2s de alivio -> bugiganga quebra sozinha -> luzes piscam -> Fim do Ato 2!)
            int finalGuard = 0;
            while (!(bool)act2DoneProp.GetValue(repCtrl) && finalGuard < 360)
            {
                string shot = (finalGuard == (_capture ? 88 : 10)) ? "04_act2_final_beat_trinket_crack.png" : null;
                yield return StepFrames(1, shotName: shot, shotAt: 0);
                finalGuard++;
            }

            Assert.IsTrue((bool)act2DoneProp.GetValue(repCtrl), "Gate 2 FALHOU: Beat final do Ato 2 nao concluiu!");
            Assert.IsFalse((bool)isFinalBeatProp.GetValue(repCtrl), "Beat final ficou preso!");
            Assert.IsTrue(loco.enabled, "Controle do Dez nao voltou limpo apos o Beat Final do Ato 2!");
            Assert.AreEqual(0, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Todas as 5 anomalias deveriam estar resolvidas!");

            // Limpa PlayerPrefs de teste
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_COMPLETED");
            PlayerPrefs.Save();
            Time.captureFramerate = 0;

            if (_capture)
            {
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }
        }

        private void TeleportDezNear(CharacterController cc, GameObject dez, Vector3 targetPos)
        {
            Vector3 p = new Vector3(targetPos.x - 1.1f, 0.05f, targetPos.z - 1.1f);
            cc.enabled = false;
            dez.transform.position = p;
            Vector3 dir = targetPos - p; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) dez.transform.rotation = Quaternion.LookRotation(dir.normalized);
            cc.enabled = true;
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
                    Debug.Log($"[RepairCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }
    }
}
