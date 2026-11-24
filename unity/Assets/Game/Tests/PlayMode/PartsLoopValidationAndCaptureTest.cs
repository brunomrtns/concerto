using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class PartsLoopValidationAndCaptureTest
    {
        [UnityTest]
        public IEnumerator ValidateFullAct1PartsLoopAndConfigN3Proof()
        {
            bool capture = Environment.GetEnvironmentVariable("CAPTURE_PARTS") == "1";

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            Time.captureFramerate = capture ? 30 : 60;

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

            var dezGo   = GameObject.Find("Player_Dez_Placeholder");
            var junoGo  = GameObject.Find("NPC_Juno_Oficina");
            var dlgGo   = GameObject.Find("Dialogue_And_Objective_System");
            var partsGo = GameObject.Find("Act1_Parts_Loop_System");
            var boardGo = GameObject.Find("CS01_Quadro_Checklist_Pecas");
            Assert.IsNotNull(dezGo);
            Assert.IsNotNull(junoGo);
            Assert.IsNotNull(dlgGo);
            Assert.IsNotNull(partsGo);
            Assert.IsNotNull(boardGo);

            var dlgType  = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var objType  = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            var mgrType  = Type.GetType("Studio.Game.Parts.PartsLoopManager, Assembly-CSharp");
            var boardType= Type.GetType("Studio.Game.Parts.DiegeticChecklistBoard, Assembly-CSharp");

            var dlgCtrl  = dlgGo.GetComponent(dlgType);
            var partsMgr = partsGo.GetComponent(mgrType);
            var board    = boardGo.GetComponent(boardType);
            var cc       = dezGo.GetComponent<CharacterController>();
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var locoComp = dezGo.GetComponent(locoType);
            if (locoComp != null) locoType.GetProperty("UseScriptedInput").SetValue(locoComp, true);

            var isDlgActiveProp = dlgType.GetProperty("IsDialogueActive");
            var triggerInteract = dlgType.GetMethod("TriggerFocusedInteraction");
            var pressAdvance    = dlgType.GetMethod("PressAdvanceInput");
            var scanMethod      = dlgType.GetMethod("ScanFocusedInteractable");
            var curObjProp      = objType.GetProperty("CurrentObjective");
            var collectedProp   = mgrType.GetProperty("CollectedCount");
            var requiredProp    = mgrType.GetProperty("RequiredCount");
            var allCollProp     = mgrType.GetProperty("AllCollected");
            var act1DoneProp    = mgrType.GetProperty("Act1Completed");
            var applyConfig     = mgrType.GetMethod("ApplyConfig");

            Assert.AreEqual(5, (int)requiredProp.GetValue(partsMgr), "Default N deve ser 5!");

            // Setup captura 1080p opcional
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            var hudCanvas = dlgGo.GetComponentInChildren<Canvas>(true);
            RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture rt = null;
            Texture2D tex = null;
            string capDir = null, framesDir = null;
            int frameIdx = 0;

            if (capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                capDir = Path.Combine(repoRoot, "captures", "parts_loop");
                framesDir = Path.Combine(capDir, "frames_raw");
                Directory.CreateDirectory(capDir);
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);

                if (hudCanvas != null && cam != null)
                {
                    oldMode = hudCanvas.renderMode;
                    hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    hudCanvas.worldCamera = cam;
                    hudCanvas.planeDistance = 0.25f;
                }
                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            }

            void Snap(Vector3 camPos, Vector3 lookAt, string savePng = null)
            {
                if (!capture || cam == null) return;
                cam.transform.position = camPos;
                cam.transform.LookAt(lookAt);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), tex.EncodeToJPG(90));
                if (!string.IsNullOrEmpty(savePng))
                    File.WriteAllBytes(Path.Combine(capDir, savePng), tex.EncodeToPNG());
                frameIdx++;
            }

            void MoveDez(Vector3 pos, Vector3 lookTarget)
            {
                if (cc != null) cc.enabled = false;
                dezGo.transform.position = pos;
                Vector3 d = lookTarget - pos; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) dezGo.transform.rotation = Quaternion.LookRotation(d.normalized);
                if (cc != null) cc.enabled = true;
            }

            // =========================================================================
            // 1. CONVERSA INTRO COM JUNO
            // =========================================================================
            Vector3 nearJuno = junoGo.transform.position + new Vector3(-1.35f, 0f, 0.25f);
            nearJuno.y = 0.02f;
            MoveDez(nearJuno, junoGo.transform.position);
            yield return null;
            scanMethod.Invoke(dlgCtrl, null);
            for (int f = 0; f < 18; f++) { yield return null; Snap(nearJuno + new Vector3(-2.1f, 1.5f, -0.8f), junoGo.transform.position + Vector3.up * 0.9f); }

            triggerInteract.Invoke(dlgCtrl, null);
            yield return null;
            Assert.IsTrue((bool)isDlgActiveProp.GetValue(dlgCtrl));
            for (int step = 0; step < 12 && (bool)isDlgActiveProp.GetValue(dlgCtrl); step++)
            {
                for (int f = 0; f < 14; f++) { yield return null; Snap(nearJuno + new Vector3(-2.1f, 1.5f, -0.8f), junoGo.transform.position + Vector3.up * 0.9f); }
                pressAdvance.Invoke(dlgCtrl, null);
                yield return null;
            }
            Assert.IsFalse((bool)isDlgActiveProp.GetValue(dlgCtrl));

            // =========================================================================
            // 2. COLETA TODAS AS 5 PECAS + VERIFICA QUADRO DIEGETICO & CONTADOR HUD
            // =========================================================================
            var scenePartsField = mgrType.GetField("sceneParts");
            var partsList = (IList)scenePartsField.GetValue(partsMgr);

            for (int i = 0; i < 5; i++)
            {
                var pComp = (Component)partsList[i];
                Vector3 pPos = pComp.transform.position;
                Vector3 standPos = pPos + (new Vector3(-29.0f, 0f, -18.0f) - new Vector3(pPos.x, 0f, pPos.z)).normalized * 1.15f;
                standPos.y = 0.02f;
                MoveDez(standPos, pPos);
                Vector3 camP = standPos + (standPos - pPos).normalized * 1.9f + Vector3.up * 1.45f;
                if (cam != null) { cam.transform.position = camP; cam.transform.LookAt(pPos); }
                yield return null;
                scanMethod.Invoke(dlgCtrl, null);

                for (int f = 0; f < 22; f++)
                {
                    yield return null;
                    Snap(camP, pPos, (i == 0 && f == 12) ? "01_peca_prompt_pegar.png" : null);
                }

                triggerInteract.Invoke(dlgCtrl, null);
                yield return null;
                Assert.AreEqual(i + 1, (int)collectedProp.GetValue(partsMgr), $"Deveria ter coletado {i + 1} pecas!");

                for (int f = 0; f < 18; f++)
                {
                    yield return null;
                    Snap(camP, pPos, (i == 1 && f == 4) ? "02_hud_contador_flash_2_5.png" : null);
                }

                // Mostra o quadro diegetico atualizado apos coletar a peca 3 e a peca 5!
                if (i == 2 || i == 4)
                {
                    Vector3 bPos = boardGo.transform.position;
                    for (int f = 0; f < 30; f++)
                    {
                        yield return null;
                        Snap(bPos + new Vector3(0f, -0.1f, -2.35f), bPos, (i == 4 && f == 12) ? "03_quadro_diegetico_5_5.png" : null);
                    }
                }
            }

            Assert.IsTrue((bool)allCollProp.GetValue(partsMgr), "Todas as 5 pecas deveriam estar coletadas!");
            Assert.AreEqual("Leve as peças para Juno", (string)curObjProp.GetValue(null));

            // =========================================================================
            // 3. ENTREGA PARA JUNO -> CONVERSA FINAL -> ATO 1 CONCLUIDO!
            // =========================================================================
            MoveDez(nearJuno, junoGo.transform.position);
            yield return null;
            scanMethod.Invoke(dlgCtrl, null);
            triggerInteract.Invoke(dlgCtrl, null);
            yield return null;
            Assert.IsTrue((bool)isDlgActiveProp.GetValue(dlgCtrl));

            for (int step = 0; step < 10 && (bool)isDlgActiveProp.GetValue(dlgCtrl); step++)
            {
                for (int f = 0; f < 16; f++) { yield return null; Snap(nearJuno + new Vector3(-2.1f, 1.5f, -0.8f), junoGo.transform.position + Vector3.up * 0.9f); }
                pressAdvance.Invoke(dlgCtrl, null);
                yield return null;
            }
            Assert.IsTrue((bool)act1DoneProp.GetValue(partsMgr), "Ato 1 deveria estar concluido apos entregar pecas!");
            for (int f = 0; f < 35; f++)
            {
                yield return null;
                Snap(nearJuno + new Vector3(-2.1f, 1.5f, -0.8f), junoGo.transform.position + Vector3.up * 0.9f, f == 10 ? "04_ato1_concluido_entrega.png" : null);
            }

            // =========================================================================
            // GATE 3 — PROVA DE CONFIG: Muda N para 3 via config JSON (ZERO codigo!)
            // =========================================================================
            var origCfg = (TextAsset)mgrType.GetField("partsConfigJson").GetValue(partsMgr);
            string jsonN3 = origCfg.text.Replace("\"requiredCount\": 5", "\"requiredCount\": 3");
            var cfgN3 = new TextAsset(jsonN3);
            applyConfig.Invoke(partsMgr, new object[] { cfgN3 });
            yield return null;

            Assert.AreEqual(3, (int)requiredProp.GetValue(partsMgr), "Config N=3 deveria atualizar RequiredCount para 3!");
            Assert.AreEqual(3, (int)boardType.GetProperty("SlotCount").GetValue(board), "Quadro diegetico deveria mostrar 3 slots!");

            // Inicia conversa intro + coleta 3 pecas -> completa o loop!
            MoveDez(nearJuno, junoGo.transform.position);
            yield return null;
            scanMethod.Invoke(dlgCtrl, null);
            triggerInteract.Invoke(dlgCtrl, null);
            yield return null;
            for (int s = 0; s < 12 && (bool)isDlgActiveProp.GetValue(dlgCtrl); s++) { pressAdvance.Invoke(dlgCtrl, null); yield return null; }

            for (int i = 0; i < 3; i++)
            {
                var pComp = (Component)partsList[i];
                MoveDez(pComp.transform.position + new Vector3(0.8f, 0f, 0f), pComp.transform.position);
                yield return null;
                scanMethod.Invoke(dlgCtrl, null);
                triggerInteract.Invoke(dlgCtrl, null);
                yield return null;
            }
            Assert.IsTrue((bool)allCollProp.GetValue(partsMgr), "Com N=3 na config, coletar 3 pecas deve completar a coleta!");
            Assert.AreEqual("Leve as peças para Juno", (string)curObjProp.GetValue(null));

            if (capture)
            {
                if (hudCanvas != null) hudCanvas.renderMode = oldMode;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
            Time.captureFramerate = 0;
        }
    }
}
