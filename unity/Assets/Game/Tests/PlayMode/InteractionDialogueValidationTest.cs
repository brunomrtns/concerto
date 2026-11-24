using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class InteractionDialogueValidationTest
    {
        [UnityTest]
        public IEnumerator ValidateInteractionDialogueSystemAndDataProof()
        {
            bool capture = Environment.GetEnvironmentVariable("CAPTURE_DIALOGUE") == "1";

            // Marca Cutscene 01 como ja tocada para cair direto no gameplay da oficina!
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

            var dezGo  = GameObject.Find("Player_Dez_Placeholder");
            var junoGo = GameObject.Find("NPC_Juno_Oficina");
            var sysGo  = GameObject.Find("Dialogue_And_Objective_System");
            Assert.IsNotNull(dezGo,  "Player_Dez_Placeholder nao encontrado");
            Assert.IsNotNull(junoGo, "NPC_Juno_Oficina nao encontrado");
            Assert.IsNotNull(sysGo,  "Dialogue_And_Objective_System nao encontrado");

            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var dlgType  = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var npcType  = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var objType  = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");

            var loco    = (MonoBehaviour)dezGo.GetComponent(locoType);
            var dlgCtrl = sysGo.GetComponent(dlgType);
            var junoNpc = junoGo.GetComponent(npcType);

            var hasFocusProp     = dlgType.GetProperty("HasFocusedInteractable");
            var isDlgActiveProp  = dlgType.GetProperty("IsDialogueActive");
            var isTypingProp     = dlgType.GetProperty("IsTyping");
            var displayedProp    = dlgType.GetProperty("CurrentDisplayedText");
            var fullProp         = dlgType.GetProperty("CurrentFullText");
            var triggerInteract  = dlgType.GetMethod("TriggerFocusedInteraction");
            var pressAdvance     = dlgType.GetMethod("PressAdvanceInput");
            var scanMethod       = dlgType.GetMethod("ScanFocusedInteractable");
            var curObjProp       = objType.GetProperty("CurrentObjective");

            // Posiciona Dez a 4.0m da Juno olhando para ela (para gravar aproximacao -> prompt -> conversa -> objetivo!)
            Vector3 jPos = junoGo.transform.position;
            Vector3 approachDir = new Vector3(-1f, 0f, 0.25f).normalized;
            Vector3 startPos = jPos + approachDir * 3.6f;
            startPos.y = 0.02f;

            var cc = dezGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = startPos;
            dezGo.transform.rotation = Quaternion.LookRotation((jPos - startPos).normalized);
            if (cc != null) cc.enabled = true;

            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            var hudCanvas = sysGo.GetComponentInChildren<Canvas>(true);
            RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture rt = null;
            Texture2D tex = null;
            string capDir = null;
            string framesDir = null;
            int frameIdx = 0;

            if (capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                capDir = Path.Combine(repoRoot, "captures", "interaction_dialogue");
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

            void Snap(string savePngName = null)
            {
                if (!capture || cam == null) return;
                // Camera 3a pessoa cinematografica acompanhando Dez olhando para Juno
                Vector3 pivot = dezGo.transform.position + Vector3.up * 1.15f;
                Vector3 toJuno = (junoGo.transform.position - dezGo.transform.position);
                toJuno.y = 0f;
                Vector3 fwd = toJuno.sqrMagnitude > 0.01f ? toJuno.normalized : dezGo.transform.forward;
                cam.transform.position = pivot - fwd * 2.65f + Vector3.up * 0.55f + Vector3.Cross(Vector3.up, fwd) * 0.45f;
                cam.transform.LookAt(Vector3.Lerp(pivot, junoGo.transform.position + Vector3.up * 0.95f, 0.55f));

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), tex.EncodeToJPG(92));
                if (!string.IsNullOrEmpty(savePngName))
                    File.WriteAllBytes(Path.Combine(capDir, savePngName), tex.EncodeToPNG());
                frameIdx++;
            }

            // 1. Aproxima Dez ate ~1.6m da Juno (mostrando objetivo inicial "Fale com Juno" e depois prompt "[E] Falar")
            locoType.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoType.GetProperty("ScriptedRun").SetValue(loco, false);
            for (int i = 0; i < 45; i++)
            {
                Vector3 toJ = junoGo.transform.position - dezGo.transform.position;
                toJ.y = 0f;
                locoType.GetProperty("ScriptedMoveDir").SetValue(loco, toJ.magnitude > 1.55f ? toJ.normalized : Vector3.zero);
                yield return null;
                Snap(i == 44 ? "01_prompt_falar_highlight.png" : null);
            }
            locoType.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            yield return null;
            scanMethod.Invoke(dlgCtrl, null);

            // GATE 2A: Prompt aparece ao chegar perto + mirar
            Assert.IsTrue((bool)hasFocusProp.GetValue(dlgCtrl), "Deveria focar a NPC_Juno_Oficina ao chegar a ~1.6m!");
            for (int i = 0; i < 18; i++) { yield return null; Snap(); }

            // 2. Inicia conversa ([E] Falar)
            triggerInteract.Invoke(dlgCtrl, null);
            yield return null;
            Assert.IsTrue((bool)isDlgActiveProp.GetValue(dlgCtrl), "Dialogo deveria estar ativo!");
            Assert.IsFalse(loco.enabled, "Movimento do jogador precisa estar travado durante o dialogo!");

            // GATE 2B: Testa interrupcao da digitacao na Linha 1 (revela linha inteira primeiro sem pular!)
            for (int i = 0; i < 14; i++) { yield return null; Snap(i == 10 ? "02_dialogo_digitando.png" : null); }
            Assert.IsTrue((bool)isTypingProp.GetValue(dlgCtrl), "Deveria ainda estar digitando a linha 1!");
            string partial = (string)displayedProp.GetValue(dlgCtrl);
            string fullL1  = (string)fullProp.GetValue(dlgCtrl);
            Assert.Less(partial.Length, fullL1.Length, "Texto parcial deveria ser menor que o texto completo antes do reveal!");

            pressAdvance.Invoke(dlgCtrl, null); // Revela linha inteira!
            yield return null;
            Assert.IsFalse((bool)isTypingProp.GetValue(dlgCtrl), "Apos apertar durante digitacao, deve revelar a linha inteira!");
            Assert.AreEqual(fullL1, (string)displayedProp.GetValue(dlgCtrl), "Texto exibido deve igualar a linha completa!");
            for (int i = 0; i < 24; i++) { yield return null; Snap(i == 5 ? "03_dialogo_linha_completa.png" : null); }

            // 3. Avanca pelas demais linhas da conversa canonica (Linhas 2..5)
            int safety = 0;
            while ((bool)isDlgActiveProp.GetValue(dlgCtrl) && safety < 20)
            {
                pressAdvance.Invoke(dlgCtrl, null); // Avanca para proxima linha (ou encerra)
                yield return null;
                if (!(bool)isDlgActiveProp.GetValue(dlgCtrl)) break;

                // Deixa digitar quase tudo para ficar bonito no MP4
                for (int k = 0; k < 42; k++)
                {
                    yield return null;
                    Snap();
                    if (!(bool)isTypingProp.GetValue(dlgCtrl) && k > 24) break;
                }
                if ((bool)isTypingProp.GetValue(dlgCtrl))
                {
                    pressAdvance.Invoke(dlgCtrl, null);
                    yield return null;
                }
                for (int k = 0; k < 22; k++) { yield return null; Snap(); }
                safety++;
            }

            // GATE 2C: Fim do dialogo -> controle devolvido + objetivo atualizado no HUD!
            Assert.IsFalse((bool)isDlgActiveProp.GetValue(dlgCtrl), "Dialogo deveria ter encerrado!");
            Assert.IsTrue(loco.enabled, "Controle do jogador precisa estar devolvido apos o dialogo!");
            string newObj = (string)curObjProp.GetValue(null);
            Assert.AreEqual("Encontre as peças da máquina (0/3)", newObj, "Objetivo pos-dialogo incorreto!");

            for (int i = 0; i < 36; i++) { yield return null; Snap(i == 10 ? "04_pos_dialogo_objetivo_hud.png" : null); }

            // =========================================================================
            // GATE 3 — PROVA DE SISTEMA: Troca APENAS o arquivo de dados JSON (ZERO codigo!)
            // =========================================================================
#if UNITY_EDITOR
            var secondJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Dialogue/Data/dlg_juno_teste_sistema.json");
            Assert.IsNotNull(secondJson, "dlg_juno_teste_sistema.json nao encontrado!");
            var origJsonField = npcType.GetField("dialogueJson");
            var origJson = (TextAsset)origJsonField.GetValue(junoNpc);
            origJsonField.SetValue(junoNpc, secondJson);

            yield return null;
            scanMethod.Invoke(dlgCtrl, null);
            triggerInteract.Invoke(dlgCtrl, null);
            yield return null;
            Assert.IsTrue((bool)isDlgActiveProp.GetValue(dlgCtrl), "Segunda conversa (data-only) deveria iniciar!");
            string secondLineFull = (string)fullProp.GetValue(dlgCtrl);
            Assert.IsTrue(secondLineFull.Contains("PROVA DE SISTEMA"), $"Esperava texto da segunda conversa JSON, recebeu: {secondLineFull}");

            // Encerra a segunda conversa limpo
            for (int s = 0; s < 6 && (bool)isDlgActiveProp.GetValue(dlgCtrl); s++)
            {
                pressAdvance.Invoke(dlgCtrl, null);
                yield return null;
            }
            Assert.IsFalse((bool)isDlgActiveProp.GetValue(dlgCtrl));
            Assert.AreEqual("Objetivo de teste validado (0/5)", (string)curObjProp.GetValue(null));

            // Restaura o JSON canonico em memoria
            origJsonField.SetValue(junoNpc, origJson);
#endif

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
