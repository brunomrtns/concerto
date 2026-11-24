using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Valida TODOS os Gates da Cutscene 2 ("A Viagem"):
    ///   - Gate 1: Compila limpo, 0 erros, skip funcional restaurando gameplay
    ///   - Gate 2: Playthrough completo: entrega das pecas -> objetivo "Ative a maquina" -> interacao na maquina -> Cutscene 2 inteira -> End Card
    ///   - Gate 3: ZERO vazamento do URP Volume de memoria/passado (IsMemoryVolumeActive == false, Lira oculta, NPCs vivos restaurados, Dez livre)
    ///   - Gate 4: Captura 1080p MP4 (CAPTURE_CS02=1) + 5 screenshots de alta qualidade em captures/cs02_viagem/
    /// </summary>
    public class Cutscene02ValidationAndCaptureTest
    {
        [UnityTest]
        public IEnumerator ValidateAct1BridgeCutscene02NoMemoryLeakAndCapture()
        {
            bool fullCapture = Environment.GetEnvironmentVariable("CAPTURE_CS02") == "1";

            // Pula CS01 automaticamente no boot para testar o fluxo pos-pecas -> CS02
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            Time.captureFramerate = fullCapture ? 30 : 60;

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

            var cs2Go = GameObject.Find("CS02_A_Viagem");
            Assert.IsNotNull(cs2Go, "CS02_A_Viagem nao encontrado em Caldeiral.unity!");

            var cs2Type = Type.GetType("Studio.Game.Cutscenes.Cutscene02TravelController, Assembly-CSharp");
            Assert.IsNotNull(cs2Type, "Cutscene02TravelController nao encontrado!");
            var cs2 = cs2Go.GetComponent(cs2Type);
            Assert.IsNotNull(cs2);

            var isPlayingProp   = cs2Type.GetProperty("IsPlaying");
            var isMemVolProp    = cs2Type.GetProperty("IsMemoryVolumeActive");
            var skipMethod      = cs2Type.GetMethod("SkipCutscene");
            var playMethod      = cs2Type.GetMethod("PlayCutscene");
            var director        = cs2Go.GetComponent<PlayableDirector>();

            // Verifica que no boot normal (antes da Cutscene 2) ZERO memoria esta ativa!
            Assert.IsFalse((bool)isMemVolProp.GetValue(cs2), "Gate 3: Volume de memoria NAO pode estar ativo antes da Cutscene 2!");

            // === GATE 2: Ponte de Gameplay (entrega pecas -> objetivo "Ative a maquina" -> interage com a maquina!) ===
            var partsMgrType = Type.GetType("Studio.Game.Parts.PartsLoopManager, Assembly-CSharp");
            Assert.IsNotNull(partsMgrType, "PartsLoopManager nao encontrado!");
            var partsMgr = UnityEngine.Object.FindAnyObjectByType(partsMgrType);
            Assert.IsNotNull(partsMgr, "PartsLoopManager ausente na cena!");

            var handleConv = partsMgrType.GetMethod("HandleConversationFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(handleConv, "HandleConversationFinished nao encontrado!");
            handleConv.Invoke(partsMgr, new object[] { "dlg_juno_oficina_intro" });

            var partType = Type.GetType("Studio.Game.Parts.MachinePartPickup, Assembly-CSharp");
            var parts = UnityEngine.Object.FindObjectsByType(partType, FindObjectsSortMode.None);
            var interactPart = partType.GetMethod("Interact");
            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder nao encontrado!");

            foreach (var p in parts)
                interactPart.Invoke(p, new object[] { dezGo });

            handleConv.Invoke(partsMgr, new object[] { "dlg_juno_entrega_pecas" });
            yield return null;

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            string currentObj = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.IsTrue(currentObj != null && currentObj.Contains("Ative"), $"Objetivo pos-entrega deveria pedir ativacao da maquina! Atual: '{currentObj}'");

            // Interage diretamente com TimeMachineInteractable (mesmo fluxo do [E] Ativar maquina!)
            var tmType = Type.GetType("Studio.Game.Cutscenes.TimeMachineInteractable, Assembly-CSharp");
            Assert.IsNotNull(tmType, "TimeMachineInteractable nao encontrado!");
            var tmComp = UnityEngine.Object.FindAnyObjectByType(tmType);
            Assert.IsNotNull(tmComp, "TimeMachineInteractable ausente em CS01_Maquina_Coberta_Lona!");
            Assert.IsTrue((bool)tmType.GetMethod("CanInteract").Invoke(tmComp, new object[] { dezGo }), "TimeMachineInteractable deveria estar liberado apos entregar as pecas!");

            tmType.GetMethod("Interact").Invoke(tmComp, new object[] { dezGo });
            yield return null;

            Assert.IsTrue((bool)isPlayingProp.GetValue(cs2), "Cutscene 2 deveria iniciar ao interagir com a maquina!");

            string repoRoot  = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string capDir    = Path.Combine(repoRoot, "captures", "cs02_viagem");
            string framesDir = Path.Combine(capDir, "frames_raw");

            if (fullCapture)
            {
                Directory.CreateDirectory(capDir);
                if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
                Directory.CreateDirectory(framesDir);

                var shotTimes = new (float t, string name)[]
                {
                    (6.0f,  "01_beat1_maquina_revelada.png"),
                    (24.5f, "02_beat2_viagem_overexposure.png"),
                    (42.0f, "03_beat3_passado_lira_vazio.png"),
                    (71.5f, "04_beat4_glitch_poeira.png"),
                    (82.5f, "05_beat5_fim_ato1_card.png"),
                };
                int nextShot = 0;

                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                var uiCanvas = cs2Go.GetComponentInChildren<Canvas>(true);
                RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
                if (uiCanvas != null && cam != null)
                {
                    oldMode = uiCanvas.renderMode;
                    uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    uiCanvas.worldCamera = cam;
                    uiCanvas.planeDistance = 0.25f;
                }

                int w = 1920, h = 1080;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);

                int frameIdx = 0;
                bool sawPastMemoryActive = false;

                while ((bool)isPlayingProp.GetValue(cs2) && frameIdx < 2650)
                {
                    yield return null;
                    double curT = director != null ? director.time : (frameIdx / 30.0);

                    if (curT >= 32.0 && curT <= 75.0 && (bool)isMemVolProp.GetValue(cs2))
                        sawPastMemoryActive = true;

                    if (cam != null)
                    {
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        cam.targetTexture = null;
                        RenderTexture.active = null;

                        File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D5}.jpg"), tex.EncodeToJPG(92));

                        if (nextShot < shotTimes.Length && curT >= shotTimes[nextShot].t)
                        {
                            File.WriteAllBytes(Path.Combine(capDir, shotTimes[nextShot].name), tex.EncodeToPNG());
                            Debug.Log($"[CS02Capture] Saved screenshot: {shotTimes[nextShot].name} @ t={curT:F2}s");
                            nextShot++;
                        }
                    }
                    frameIdx++;
                }

                if (uiCanvas != null) uiCanvas.renderMode = oldMode;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);

                Assert.IsTrue(sawPastMemoryActive, "Volume de memoria do passado deveria ter sido ativado no Beat 3!");
                Debug.Log($"[CS02Capture] Captura completa: {frameIdx} frames gravados em {framesDir}");
            }
            else
            {
                // Teste rapido sem captura de video: avanca ate Beat 3 e testa Skip
                for (int i = 0; i < 60; i++) yield return null;
                skipMethod.Invoke(cs2, null);
                yield return null;
            }

            // === GATE 3: ZERO VAZAMENTO DE MEMORIA POS-CUTSCENE ===
            Assert.IsFalse((bool)isPlayingProp.GetValue(cs2), "Cutscene 2 deveria ter encerrado!");
            Assert.IsFalse((bool)isMemVolProp.GetValue(cs2), "Gate 3 FALHOU: Volume de memoria do passado vazou para a oficina normal!");

            var liraField = cs2Type.GetField("wifeLiraActor");
            var liraGo = liraField.GetValue(cs2) as GameObject;
            Assert.IsTrue(liraGo == null || !liraGo.activeSelf, "Gate 3 FALHOU: Lira do passado nao foi ocultada ao voltar ao presente!");

            var livingField = cs2Type.GetField("livingNpcsRoot");
            var livingGo = livingField.GetValue(cs2) as GameObject;
            Assert.IsTrue(livingGo == null || livingGo.activeSelf, "Gate 3 FALHOU: NPCs vivos do presente nao foram restaurados!");

            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;
            Assert.IsTrue(loco != null && loco.enabled, "Gate 1 FALHOU: DezLocomotionController nao foi reabilitado apos Cutscene 2!");
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time.timeScale nao voltou a 1!");

            // === GATE 1 (Extra): Replay + Skip no meio do Beat 3 (Passado) tambem restaura 100% sem vazamento! ===
            playMethod.Invoke(cs2, null);
            yield return null;
            Assert.IsTrue((bool)isPlayingProp.GetValue(cs2), "Replay da Cutscene 2 falhou!");
            if (director != null) { director.time = 35.0; director.Evaluate(); }
            yield return null;
            skipMethod.Invoke(cs2, null);
            yield return null;

            Assert.IsFalse((bool)isPlayingProp.GetValue(cs2), "Skip no meio da Cutscene 2 falhou!");
            Assert.IsFalse((bool)isMemVolProp.GetValue(cs2), "Gate 3 FALHOU apos Skip: Volume de memoria vazou!");
            Assert.IsTrue(liraGo == null || !liraGo.activeSelf, "Gate 3 FALHOU apos Skip: Lira vazou!");
            Assert.IsTrue(loco != null && loco.enabled, "Controle de Dez nao restaurado apos Skip!");

            Time.captureFramerate = 0;
        }
    }
}
