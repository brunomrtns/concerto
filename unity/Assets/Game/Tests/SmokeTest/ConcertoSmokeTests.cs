using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.SmokeTest
{
    /// <summary>
    /// Suite Smoke Test oficial de CONCERTO (GATE 3 — QA Automatizado Fase 1).
    /// Cobre exclusivamente o nucleo estavel do jogo:
    ///   (a) Carregamento de cada cena/ato sem erros no console;
    ///   (b) Spawn e resposta de movimento do Dez;
    ///   (c) Pulo, coyote time (~100ms) e jump buffer (~150ms) conforme DESIGN_BIBLE;
    ///   (d) Persistencia de Save em JSON versionado (gravar -> recarregar -> integridade);
    ///   (e) Transicoes de cena sem exceptions.
    /// </summary>
    public sealed class ConcertoSmokeTests
    {
        private readonly List<string> _consoleErrors = new List<string>();

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _consoleErrors.Add($"[{type}] {condition}");
        }

        [SetUp]
        public void SetUp()
        {
            _consoleErrors.Clear();
            Application.logMessageReceived += HandleLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private IEnumerator LoadSceneAndVerifyCleanConsole(string sceneName)
        {
            if (sceneName != "MainMenu")
            {
                PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
                PlayerPrefs.Save();
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, $"LoadSceneAsync retornou null para '{sceneName}'.");
            while (!op.isDone) yield return null;

            for (int i = 0; i < 6; i++) yield return null;

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, $"Cena ativa deveria ser '{sceneName}'.");
            Assert.IsEmpty(_consoleErrors, $"Cena '{sceneName}' gerou erros no console: {string.Join(" | ", _consoleErrors)}");
        }

        // (a) Cada cena / ato existente carrega sem erro no console
        [UnityTest]
        public IEnumerator Smoke_01_MainMenuLoadsWithoutConsoleErrors()
        {
            yield return LoadSceneAndVerifyCleanConsole("MainMenu");
        }

        [UnityTest]
        public IEnumerator Smoke_02_Act1_CaldeiralLoadsWithoutConsoleErrors()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");
        }

        [UnityTest]
        public IEnumerator Smoke_03_Act2_SetorExternoLoadsWithoutConsoleErrors()
        {
            yield return LoadSceneAndVerifyCleanConsole("SetorExterno");
        }

        // (b) Dez spawna na cena e responde a input de movimento
        [UnityTest]
        public IEnumerator Smoke_04_DezSpawnsAndRespondsToMovementInput()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var locoT = GameType("Studio.Game.Animation.DezLocomotionController");
            var loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(loco, "DezLocomotionController deve existir na cena Caldeiral.");

            Vector3 startPos = loco.transform.position;
            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir     = locoT.GetProperty("ScriptedMoveDir");
            var pRun         = locoT.GetProperty("ScriptedRun");

            pUseScripted.SetValue(loco, true);
            pMoveDir.SetValue(loco, Vector3.right);
            pRun.SetValue(loco, false);

            float elapsed = 0f;
            while (elapsed < 0.75f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            pMoveDir.SetValue(loco, Vector3.zero);
            pUseScripted.SetValue(loco, false);

            Vector3 delta = loco.transform.position - startPos;
            delta.y = 0f;
            Assert.Greater(delta.magnitude, 0.35f, $"Dez deveria responder ao input de movimento (deslocamento={delta.magnitude:F3}m).");
            Assert.IsEmpty(_consoleErrors, $"Erros no console durante movimento: {string.Join(" | ", _consoleErrors)}");
        }

        // (c) Pulo / coyote (~100ms) / jump buffer (~150ms) dentro dos valores da DESIGN_BIBLE
        [UnityTest]
        public IEnumerator Smoke_05_JumpCoyoteAndBufferMatchDesignBible()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var locoT = GameType("Studio.Game.Animation.DezLocomotionController");
            var cfgT  = GameType("Studio.Game.Animation.DezMovementConfig");
            var loco  = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(loco, "DezLocomotionController obrigatorio.");

            var cfgProp = locoT.GetProperty("Cfg", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(cfgProp, "Propriedade Cfg nao encontrada em DezLocomotionController.");
            var cfg = cfgProp.GetValue(loco);
            Assert.IsNotNull(cfg, "DezMovementConfig nao pode ser null.");

            float coyoteSec = (float)cfgT.GetField("coyoteTimeSec").GetValue(cfg);
            float bufferSec = (float)cfgT.GetField("jumpBufferSec").GetValue(cfg);
            float jumpHeight = (float)cfgT.GetField("jumpHeight").GetValue(cfg);

            // DESIGN_BIBLE: coyote ~100ms (0.08..0.12s), jump buffer ~150ms (0.12..0.18s)
            Assert.That(coyoteSec, Is.InRange(0.08f, 0.12f), $"coyoteTimeSec ({coyoteSec:F3}s) fora do alvo DESIGN_BIBLE (~100ms).");
            Assert.That(bufferSec, Is.InRange(0.12f, 0.18f), $"jumpBufferSec ({bufferSec:F3}s) fora do alvo DESIGN_BIBLE (~150ms).");
            Assert.Greater(jumpHeight, 0.8f, $"jumpHeight ({jumpHeight:F2}m) abaixo do minimo jogavel.");

            // Executa um pulo real e verifica elevacao vertical
            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pJump        = locoT.GetProperty("ScriptedJump");
            var pJumpHeld    = locoT.GetProperty("ScriptedJumpHeld");

            float startY = loco.transform.position.y;
            float peakY = startY;
            pUseScripted.SetValue(loco, true);
            pJumpHeld.SetValue(loco, true);
            pJump.SetValue(loco, true);

            float elapsedJump = 0f;
            int jumpFrames = 0;
            while (elapsedJump < 0.45f && jumpFrames < 300)
            {
                yield return null;
                jumpFrames++;
                elapsedJump += Time.deltaTime;
                if (jumpFrames == 2) pJump.SetValue(loco, false);
                if (loco.transform.position.y > peakY)
                    peakY = loco.transform.position.y;
            }

            pUseScripted.SetValue(loco, false);

            Assert.Greater(peakY - startY, 0.35f, $"Pulo do Dez deveria elevar verticalmente > 0.35m (ganho={peakY - startY:F3}m).");
            Assert.IsEmpty(_consoleErrors, $"Erros no console durante teste de pulo: {string.Join(" | ", _consoleErrors)}");
        }

        // (d) Save em JSON versionado: gravar -> recarregar -> estado integro
        [UnityTest]
        public IEnumerator Smoke_06_VersionedJsonSaveWriteAndReloadIntegrity()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var saveSysT  = GameType("Studio.Game.Save.SaveSystem");
            var saveDataT = GameType("Studio.Game.Save.SaveGameData");
            var saveVecT  = GameType("Studio.Game.Save.SaveVec3");

            string tempSavePath = Path.Combine(Application.temporaryCachePath, $"concerto_smoke_save_{Guid.NewGuid():N}.json");
            var mSetCustomPath = saveSysT.GetMethod("SetCustomPathForTests");

            try
            {
                mSetCustomPath.Invoke(null, new object[] { tempSavePath });

                Vector3 expectedCp = new Vector3(-24.5f, 0.05f, -17.25f);
                saveSysT.GetMethod("Save").Invoke(null, new object[] { "cp_smoke_test", (Vector3?)expectedCp, (float?)90f });

                Assert.IsTrue(File.Exists(tempSavePath), "Arquivo JSON de save deve ser gravado no disco.");
                string rawJson = File.ReadAllText(tempSavePath);
                StringAssert.Contains("\"schemaVersion\"", rawJson, "JSON de save deve conter campo schemaVersion.");
                StringAssert.Contains("\"cp_smoke_test\"", rawJson, "JSON de save deve conter checkpoint salvo.");

                var tryLoadArgs = new object[] { null };
                bool ok = (bool)saveSysT.GetMethod("TryLoad").Invoke(null, tryLoadArgs);
                Assert.IsTrue(ok, "SaveSystem.TryLoad deve recarregar o JSON gravado sem erros.");

                var loaded = tryLoadArgs[0];
                Assert.IsNotNull(loaded, "SaveGameData carregado nao pode ser null.");
                int version = (int)saveDataT.GetField("schemaVersion").GetValue(loaded);
                string scene = (string)saveDataT.GetField("sceneName").GetValue(loaded);
                string cpId  = (string)saveDataT.GetField("currentCheckpointId").GetValue(loaded);
                var cpBox    = saveDataT.GetField("checkpointPosition").GetValue(loaded);
                Vector3 loadedCp = (Vector3)saveVecT.GetMethod("ToVector3").Invoke(cpBox, null);

                Assert.GreaterOrEqual(version, 1, "schemaVersion deve ser >= 1.");
                Assert.AreEqual("Caldeiral", scene, "sceneName deve ser preservado no reload.");
                Assert.AreEqual("cp_smoke_test", cpId, "currentCheckpointId deve ser preservado no reload.");
                Assert.Less(Vector3.Distance(expectedCp, loadedCp), 0.05f, "checkpointPosition deve ser preservado no reload.");
            }
            finally
            {
                mSetCustomPath.Invoke(null, new object[] { null });
                if (File.Exists(tempSavePath)) File.Delete(tempSavePath);
            }

            Assert.IsEmpty(_consoleErrors, $"Erros no console durante teste de save JSON: {string.Join(" | ", _consoleErrors)}");
        }

        // (e) Transicao de cena sem exceptions
        [UnityTest]
        public IEnumerator Smoke_07_SceneTransitionsWithoutExceptions()
        {
            string[] sequence = { "MainMenu", "Caldeiral", "SetorExterno", "MainMenu" };
            for (int i = 0; i < sequence.Length; i++)
            {
                yield return LoadSceneAndVerifyCleanConsole(sequence[i]);
            }
            Assert.IsEmpty(_consoleErrors, $"Transicoes de cena geraram erros: {string.Join(" | ", _consoleErrors)}");
        }

        // (f) Regressao BL-001 (GATE 4): iniciar missao -> dialogo completo com NPC -> missao segue intacta (sem reset!)
        [UnityTest]
        public IEnumerator Smoke_08_MissionDoesNotResetAfterTalkingToNpc()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T    = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var dlgT     = GameType("Studio.Game.Dialogue.DialogueController");
            var objT     = GameType("Studio.Game.Dialogue.ObjectiveSystem");
            var partsT   = GameType("Studio.Game.Parts.PartsLoopManager");
            var saveSysT = GameType("Studio.Game.Save.SaveSystem");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var dlg   = UnityEngine.Object.FindAnyObjectByType(dlgT);
            var parts = UnityEngine.Object.FindAnyObjectByType(partsT);
            Assert.IsNotNull(dlg, "DialogueController obrigatorio.");
            Assert.IsNotNull(parts, "PartsLoopManager obrigatorio.");

            var junoNpc   = partsT.GetField("junoNpc").GetValue(parts);
            var introJson = partsT.GetField("junoIntroDialogueJson").GetValue(parts) as TextAsset;
            Assert.IsNotNull(junoNpc);
            Assert.IsNotNull(introJson);

            var pActive  = dlgT.GetProperty("IsDialogueActive");
            var mStart   = dlgT.GetMethod("StartConversation");
            var mAdvance = dlgT.GetMethod("PressAdvanceInput");
            var pCurObj  = objT.GetProperty("CurrentObjective");

            // 1. Primeira conversa com Juno inicia a missao
            mStart.Invoke(dlg, new object[] { junoNpc, introJson });
            for (int g = 0; g < 30 && (bool)pActive.GetValue(dlg); g++) { mAdvance.Invoke(dlg, null); yield return null; }
            Assert.IsTrue((bool)partsT.GetProperty("CanCollectParts").GetValue(parts), "Missao de coleta deve iniciar apos 1a conversa.");

            // 2. Avanca progresso da missao e grava em Save JSON versionado
            const string midObj = "Peças (2/5)";
            objT.GetMethod("Set").Invoke(null, new object[] { midObj });

            string tmpSave = Path.Combine(Application.temporaryCachePath, $"concerto_smoke08_{Guid.NewGuid():N}.json");
            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");
            try
            {
                mSetPath.Invoke(null, new object[] { tmpSave });
                saveSysT.GetMethod("Save").Invoke(null, new object[] { "cp_smoke08", null, null });
                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)saveSysT.GetMethod("TryLoad").Invoke(null, loadArgs));
                saveSysT.GetMethod("ApplyLoadedStateToCurrentScene").Invoke(null, new object[] { loadArgs[0] });
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (File.Exists(tmpSave)) File.Delete(tmpSave);
            }

            // 3. Re-conversa completa com Juno -> objetivo DEVE permanecer "Pecas (2/5)" sem reset!
            mStart.Invoke(dlg, new object[] { junoNpc, introJson });
            for (int g = 0; g < 30 && (bool)pActive.GetValue(dlg); g++) { mAdvance.Invoke(dlg, null); yield return null; }

            Assert.AreEqual(midObj, (string)pCurObj.GetValue(null), "Smoke_08 FALHOU: Falar com NPC resetou/trocou o objetivo da missao!");
            Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_08: {string.Join(" | ", _consoleErrors)}");
        }

        // (g) Onboarding (GATE 4): iniciar jogo novo -> completar os 5 passos -> Save JSON reflete progresso!
        [UnityTest]
        public IEnumerator Smoke_09_NewGameOnboardingUpdatesSaveState()
        {
            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            var menuT    = GameType("Studio.Game.Menu.BootAndMainMenuController");
            var onbT     = GameType("Studio.Game.Onboarding.OnboardingSequenceController");
            var stepEnum = GameType("Studio.Game.Onboarding.OnboardingStep");
            var locoT    = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT  = GameType("Studio.Game.Powers.DezPowersController");
            var cs01T    = GameType("Studio.Game.Cutscenes.CutsceneManager");

            string tmpSave = Path.Combine(Application.temporaryCachePath, $"concerto_smoke09_{Guid.NewGuid():N}.json");
            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");

            try
            {
                mSetPath.Invoke(null, new object[] { tmpSave });

                // 1. Iniciar pelo MainMenu -> Novo Jogo
                yield return LoadSceneAndVerifyCleanConsole("MainMenu");
                var menu = UnityEngine.Object.FindAnyObjectByType(menuT);
                Assert.IsNotNull(menu, "BootAndMainMenuController obrigatorio em MainMenu.");
                menuT.GetMethod("SkipBootSplash").Invoke(menu, null);
                yield return null;
                menuT.GetMethod("OnNewGameClicked").Invoke(menu, null);

                float waitLoad = 0f;
                while (SceneManager.GetActiveScene().name != "Caldeiral" && waitLoad < 10f)
                {
                    yield return null;
                    waitLoad += Time.unscaledDeltaTime;
                }
                Assert.AreEqual("Caldeiral", SceneManager.GetActiveScene().name, "Novo Jogo deve carregar Caldeiral.");
                for (int i = 0; i < 6; i++) yield return null;

                // Pula cutscene de abertura (se ativa) para ir direto ao onboarding
                var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
                if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                    cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
                for (int i = 0; i < 4; i++) yield return null;

                var onb = UnityEngine.Object.FindAnyObjectByType(onbT) as Component;
                Assert.IsNotNull(onb, "OnboardingSequenceController deve iniciar automaticamente no Novo Jogo.");
                Assert.IsTrue((bool)onbT.GetProperty("IsActive").GetValue(onb), "Onboarding deve estar ativo apos Novo Jogo.");

                var loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
                var powers = UnityEngine.Object.FindAnyObjectByType(powersT);
                Assert.IsNotNull(loco, "DezLocomotionController obrigatorio.");
                Assert.IsNotNull(powers, "DezPowersController obrigatorio.");

                var pCurStep   = onbT.GetProperty("CurrentStep");
                var pTelegraph = onbT.GetProperty("TelegraphCompletedForCurrentStep");
                var pPromptCnt = onbT.GetProperty("CurrentPromptLineCount");
                var mGetPos    = onbT.GetMethod("GetStationPosition");
                var mIsBarrier = onbT.GetMethod("IsBarrierActive");
                var pUseScript = locoT.GetProperty("UseScriptedInput");
                var pMoveDir   = locoT.GetProperty("ScriptedMoveDir");
                var pJump      = locoT.GetProperty("ScriptedJump");
                var pJumpHeld  = locoT.GetProperty("ScriptedJumpHeld");

                // Passo 1: Mover
                Assert.AreEqual(Enum.Parse(stepEnum, "Move"), pCurStep.GetValue(onb));
                Assert.LessOrEqual((int)pPromptCnt.GetValue(onb), 1, "Onboarding nao pode ter parede de texto (max 1 linha).");
                Assert.IsTrue((bool)mIsBarrier.Invoke(onb, new object[] { Enum.Parse(stepEnum, "Move") }), "Barreira 1 deve bloquear antes de aprender.");
                while (!(bool)pTelegraph.GetValue(onb)) yield return null;

                Vector3 st0 = (Vector3)mGetPos.Invoke(onb, new object[] { Enum.Parse(stepEnum, "Move") });
                pUseScript.SetValue(loco, true);
                float t = 0f;
                while (Equals(pCurStep.GetValue(onb), Enum.Parse(stepEnum, "Move")) && t < 5f)
                {
                    Vector3 d = st0 - loco.transform.position; d.y = 0f;
                    pMoveDir.SetValue(loco, d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward);
                    yield return null;
                    t += Time.deltaTime;
                }
                pMoveDir.SetValue(loco, Vector3.zero);
                Assert.AreEqual(Enum.Parse(stepEnum, "Jump"), pCurStep.GetValue(onb), "Passo 1 (Move) deve avancar para Passo 2 (Jump).");
                Assert.IsFalse((bool)mIsBarrier.Invoke(onb, new object[] { Enum.Parse(stepEnum, "Move") }), "Barreira 1 deve dissolver apos conclusao.");

                // Passo 2: Pular (plato fisico + coyote/buffer)
                while (!(bool)pTelegraph.GetValue(onb)) yield return null;
                Vector3 st1 = (Vector3)mGetPos.Invoke(onb, new object[] { Enum.Parse(stepEnum, "Jump") });
                t = 0f;
                bool jumpSent = false;
                int jumpHoldFrames = 0;
                while (Equals(pCurStep.GetValue(onb), Enum.Parse(stepEnum, "Jump")) && t < 5f)
                {
                    Vector3 d = st1 - loco.transform.position; d.y = 0f;
                    pMoveDir.SetValue(loco, d.sqrMagnitude > 0.04f ? d.normalized : Vector3.zero);
                    if (!jumpSent && d.magnitude < 1.9f)
                    {
                        pJumpHeld.SetValue(loco, true);
                        pJump.SetValue(loco, true);
                        jumpSent = true;
                        jumpHoldFrames = 0;
                    }
                    else if (jumpSent)
                    {
                        jumpHoldFrames++;
                        if (jumpHoldFrames == 2) pJump.SetValue(loco, false);
                        if (jumpHoldFrames == 18) pJumpHeld.SetValue(loco, false);
                    }
                    yield return null;
                    t += Time.deltaTime;
                }
                pJump.SetValue(loco, false);
                pJumpHeld.SetValue(loco, false);
                pMoveDir.SetValue(loco, Vector3.zero);
                pUseScript.SetValue(loco, false);
                Assert.AreEqual(Enum.Parse(stepEnum, "Interact"), pCurStep.GetValue(onb), "Passo 2 (Jump) deve avancar para Passo 3 (Interact).");

                // Passo 3: Interagir
                while (!(bool)pTelegraph.GetValue(onb)) yield return null;
                Assert.IsTrue((bool)onbT.GetMethod("TryInteractCurrentTerminal").Invoke(onb, null), "Interacao com terminal deve concluir Passo 3.");
                yield return null;
                Assert.AreEqual(Enum.Parse(stepEnum, "EcoTemporal"), pCurStep.GetValue(onb), "Passo 3 (Interact) deve avancar para Passo 4 (EcoTemporal).");

                // Passo 4: Eco Temporal
                while (!(bool)pTelegraph.GetValue(onb)) yield return null;
                Assert.IsTrue((bool)powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null), "Eco Temporal deve ativar no Passo 4.");
                yield return null;
                Assert.AreEqual(Enum.Parse(stepEnum, "Estase"), pCurStep.GetValue(onb), "Passo 4 (EcoTemporal) deve avancar para Passo 5 (Estase).");

                // Passo 5: Estase
                while (!(bool)pTelegraph.GetValue(onb)) yield return null;
                Assert.IsTrue((bool)powersT.GetMethod("TriggerEstase").Invoke(powers, null), "Estase deve ativar no Passo 5.");
                yield return null;

                Assert.IsTrue((bool)onbT.GetProperty("IsCompleted").GetValue(onb), "Onboarding deve estar concluido apos os 5 passos.");

                // Verifica persistencia real no Save JSON versionado
                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)saveSysT.GetMethod("TryLoad").Invoke(null, loadArgs), "Save JSON deve existir apos concluir onboarding.");
                var loaded = loadArgs[0];
                var dataT = loaded.GetType();
                Assert.IsTrue((bool)dataT.GetField("onboardingCompleted").GetValue(loaded), "Save JSON deve registrar onboardingCompleted=true.");
                Assert.AreEqual(5, (int)dataT.GetField("onboardingStepIndex").GetValue(loaded), "Save JSON deve registrar onboardingStepIndex=5.");
                var stepsList = dataT.GetField("completedOnboardingSteps").GetValue(loaded) as List<string>;
                Assert.IsNotNull(stepsList);
                Assert.AreEqual(5, stepsList.Count, "Save JSON deve listar os 5 passos concluidos.");

                Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_09: {string.Join(" | ", _consoleErrors)}");
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (File.Exists(tmpSave)) File.Delete(tmpSave);
            }
        }

        // (h) Save a Prova de Corrupcao (GATE 5b): forcar corrupcao do save atual -> recupera do backup rotativo sem crash!
        [UnityTest]
        public IEnumerator Smoke_10_CorruptSaveRecoversFromBackupSlotWithoutCrash()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            var objT     = GameType("Studio.Game.Dialogue.ObjectiveSystem");
            string tmpDir = Path.Combine(Application.temporaryCachePath, $"concerto_smoke10_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            string tmpSave = Path.Combine(tmpDir, "concerto_save.json");

            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");
            var mSave    = saveSysT.GetMethod("Save");
            var mTryLoad = saveSysT.GetMethod("TryLoad");
            var mVerify  = saveSysT.GetMethod("VerifyChecksum");

            try
            {
                mSetPath.Invoke(null, new object[] { tmpSave });

                // Grava 3 saves consecutivos para popular os 3 slots rotativos (atual, bak1, bak2)
                objT.GetMethod("Set").Invoke(null, new object[] { "Smoke10_Slot_MaisAntigo" });
                mSave.Invoke(null, new object[] { "cp_smoke10_1", null, null });
                yield return null;

                objT.GetMethod("Set").Invoke(null, new object[] { "Smoke10_Slot_BackupAnterior" });
                mSave.Invoke(null, new object[] { "cp_smoke10_2", null, null });
                yield return null;

                objT.GetMethod("Set").Invoke(null, new object[] { "Smoke10_Slot_Atual" });
                mSave.Invoke(null, new object[] { "cp_smoke10_3", null, null });
                yield return null;

                string bak1 = (string)saveSysT.GetProperty("Backup1FilePath").GetValue(null);
                string bak2 = (string)saveSysT.GetProperty("Backup2FilePath").GetValue(null);
                Assert.IsTrue(File.Exists(tmpSave), "Slot 0 (atual) deve existir.");
                Assert.IsTrue(File.Exists(bak1), "Slot 1 (bak1) deve existir apos rotacao.");
                Assert.IsTrue(File.Exists(bak2), "Slot 2 (bak2) deve existir apos 3 saves.");

                // 1) Adulteracao sutil de campo (quebra checksum SHA-256 sem quebrar sintaxe JSON)
                string rawJson = File.ReadAllText(tmpSave);
                string tampered = rawJson.Replace("Smoke10_Slot_Atual", "Smoke10_ADULTERADO");
                File.WriteAllText(tmpSave, tampered);

                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)mTryLoad.Invoke(null, loadArgs), "TryLoad deve recuperar do backup quando SHA-256 falhar.");
                Assert.IsTrue((bool)saveSysT.GetProperty("WasLastLoadRecoveredFromBackup").GetValue(null), "Deve sinalizar recuperacao do backup.");
                Assert.AreEqual("bak1", (string)saveSysT.GetProperty("LastRecoveredSlotName").GetValue(null));

                var recovered = loadArgs[0];
                Assert.AreEqual("Smoke10_Slot_BackupAnterior", (string)recovered.GetType().GetField("currentObjective").GetValue(recovered));
                Assert.IsTrue((bool)mVerify.Invoke(null, new object[] { recovered }), "Save recuperado deve ter SHA-256 valido.");

                // 2) Corrupcao severa (JSON truncado/lixo binario) -> recupera novamente sem crash
                File.WriteAllText(tmpSave, "{ \"schemaVersion\": 3, \"corrompido_na_mao\": ");
                loadArgs = new object[] { null };
                Assert.IsTrue((bool)mTryLoad.Invoke(null, loadArgs), "TryLoad deve recuperar do backup quando JSON estiver truncado.");
                Assert.IsTrue((bool)saveSysT.GetProperty("WasLastLoadRecoveredFromBackup").GetValue(null));

                Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_10: {string.Join(" | ", _consoleErrors)}");
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            }
        }

        // (i) Save a Prova de Corrupcao (GATE 5c): save de versao antiga (v1) migra para v3 sem perder dados!
        [UnityTest]
        public IEnumerator Smoke_11_LegacySaveVersionMigratesWithoutDataLoss()
        {
            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            string tmpDir = Path.Combine(Application.temporaryCachePath, $"concerto_smoke11_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            string tmpSave = Path.Combine(tmpDir, "concerto_save.json");

            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");
            var mTryLoad = saveSysT.GetMethod("TryLoad");
            var mVerify  = saveSysT.GetMethod("VerifyChecksum");

            try
            {
                mSetPath.Invoke(null, new object[] { tmpSave });

                // Grava um JSON autentico de schemaVersion=1 (sem payloadChecksum e sem campos v3)
                const string legacyV1Json = @"{
  ""schemaVersion"": 1,
  ""savedAtUtc"": ""2026-01-10T12:00:00Z"",
  ""sceneName"": ""Caldeiral"",
  ""currentCheckpointId"": ""cp_legado_v1"",
  ""currentObjective"": ""Pecas Legado (3/5)"",
  ""partsCollectedMask"": 7,
  ""canCollectParts"": true,
  ""act1Completed"": false,
  ""cs01Played"": true,
  ""energyCells"": 4,
  ""energyBar"": 85.0,
  ""activeToolModeId"": ""mode_combate"",
  ""unlockedPowerIds"": [""eco_temporal""],
  ""resolvedAnomalyIds"": [""anom_bancada""]
}";
                File.WriteAllText(tmpSave, legacyV1Json);
                yield return null;

                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)mTryLoad.Invoke(null, loadArgs), "TryLoad deve migrar save v1 sem falhar.");

                var migrated = loadArgs[0];
                var dataT = migrated.GetType();
                Assert.AreEqual(3, (int)dataT.GetField("schemaVersion").GetValue(migrated), "Schema deve migrar para v3.");
                Assert.AreEqual("Pecas Legado (3/5)", (string)dataT.GetField("currentObjective").GetValue(migrated));
                Assert.AreEqual(7, (int)dataT.GetField("partsCollectedMask").GetValue(migrated));
                Assert.AreEqual(4, (int)dataT.GetField("energyCells").GetValue(migrated));
                Assert.IsTrue((bool)dataT.GetField("powersUnlocked").GetValue(migrated), "Migracao v1->v3 deve inferir powersUnlocked=true a partir de unlockedPowerIds.");
                Assert.IsTrue((bool)mVerify.Invoke(null, new object[] { migrated }), "Save migrado deve receber assinatura SHA-256 valida.");

                Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_11: {string.Join(" | ", _consoleErrors)}");
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            }
        }

        // (j) UI, HUD e Acessibilidade (GATES 1-4): Pause por controle, opcoes PlayerPrefs na hora, daltonismo, remapeamento e legendas
        [UnityTest]
        public IEnumerator Smoke_12_UiAccessibilityOptionsAndPauseControllerNav()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var pauseT = GameType("Studio.Game.Menu.PauseMenuController");
            var optT   = GameType("Studio.Game.Menu.OptionsPanelUI");
            var setT   = GameType("Studio.Game.Menu.GameSettingsManager");
            var cbEnum = GameType("Studio.Game.Menu.ColorblindProfile");
            var a11yT  = GameType("Studio.Game.UI.AccessibilityRuntimeController");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var pause = UnityEngine.Object.FindAnyObjectByType(pauseT);
            var a11y  = UnityEngine.Object.FindAnyObjectByType(a11yT);
            Assert.IsNotNull(pause, "PauseMenuController obrigatorio em Caldeiral.");
            Assert.IsNotNull(a11y, "AccessibilityRuntimeController obrigatorio.");

            // Guarda valores anteriores para restaurar ao fim do teste
            float origBright = (float)setT.GetProperty("Brightness").GetValue(null);
            int   origFont   = (int)setT.GetProperty("FontScaleIndex").GetValue(null);
            bool  origSub    = (bool)setT.GetProperty("SubtitlesEnabled").GetValue(null);
            object origCb    = setT.GetProperty("ColorblindMode").GetValue(null);
            KeyCode origJump = (KeyCode)setT.GetMethod("GetActionKey").Invoke(null, new object[] { "Jump" });

            try
            {
                // GATE 1: Abre Pause, navega por controle (Continuar -> Opcoes) e confirma sem mouse
                Assert.IsTrue((bool)pauseT.GetMethod("TryPause").Invoke(pause, null), "TryPause deve pausar.");
                yield return null;
                pauseT.GetMethod("NavigatePause").Invoke(pause, new object[] { +1 }); // foca Opcoes
                Assert.AreEqual("Opcoes", (string)pauseT.GetProperty("FocusedPauseLabel").GetValue(pause));
                pauseT.GetMethod("ConfirmPauseSelection").Invoke(pause, null); // abre OptionsPanelUI
                yield return null;

                var opt = pauseT.GetField("optionsPanel").GetValue(pause) as Component;
                Assert.IsNotNull(opt);
                Assert.IsTrue(opt.gameObject.activeSelf, "OptionsPanelUI deve abrir via controle.");

                // GATE 2: Navega pelas opcoes via controle (Brilho, Fonte, Legendas, Daltonismo, Remapeamento)
                var mNav = optT.GetMethod("NavigateOption");
                var mAdj = optT.GetMethod("AdjustOption");
                for (int i = 0; i < 5; i++) mNav.Invoke(opt, new object[] { +1 }); // row 5 = Brilho
                mAdj.Invoke(opt, new object[] { +1 });
                Assert.Greater((float)setT.GetProperty("Brightness").GetValue(null), origBright - 0.01f);

                mNav.Invoke(opt, new object[] { +2 }); // row 7 = Escala de Fonte (3 tamanhos)
                setT.GetMethod("SetFontScaleIndex").Invoke(null, new object[] { 2 }); // Grande (1.25x)
                Assert.AreEqual(2, (int)setT.GetProperty("FontScaleIndex").GetValue(null));

                mNav.Invoke(opt, new object[] { +2 }); // row 9 = Modo Daltoico (3 perfis)
                setT.GetMethod("SetColorblindMode").Invoke(null, new object[] { Enum.Parse(cbEnum, "Protanopia") });
                Assert.AreEqual(Enum.Parse(cbEnum, "Protanopia"), setT.GetProperty("ColorblindMode").GetValue(null));

                mNav.Invoke(opt, new object[] { +1 }); // row 10 = Remapear Jump
                setT.GetMethod("SetActionKey").Invoke(null, new object[] { "Jump", KeyCode.F });
                Assert.AreEqual(KeyCode.F, (KeyCode)setT.GetMethod("GetActionKey").Invoke(null, new object[] { "Jump" }));

                // Fecha Opcoes e retoma jogo via controle (Back)
                pauseT.GetMethod("BackPauseNavigation").Invoke(pause, null);
                yield return null;
                pauseT.GetMethod("BackPauseNavigation").Invoke(pause, null);
                yield return null;
                Assert.IsFalse((bool)pauseT.GetProperty("IsPaused").GetValue(pause), "Jogo deve despausar via controle.");

                // GATE 4: Legenda com nome do falante
                setT.GetMethod("SetSubtitlesEnabled").Invoke(null, new object[] { true });
                a11yT.GetMethod("ShowSubtitle").Invoke(a11y, new object[] { "Juno", "Teste de legenda acessivel.", 2.0f });
                yield return null;
                string activeSub = (string)a11yT.GetProperty("ActiveSubtitleText").GetValue(a11y);
                Assert.IsTrue(activeSub.Contains("[JUNO]") && activeSub.Contains("legenda"), $"Legenda esperada com falante, obtido: '{activeSub}'");

                Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_12: {string.Join(" | ", _consoleErrors)}");
            }
            finally
            {
                setT.GetMethod("SetBrightness").Invoke(null, new object[] { origBright });
                setT.GetMethod("SetFontScaleIndex").Invoke(null, new object[] { origFont });
                setT.GetMethod("SetSubtitlesEnabled").Invoke(null, new object[] { origSub });
                setT.GetMethod("SetColorblindMode").Invoke(null, new object[] { origCb });
                setT.GetMethod("SetActionKey").Invoke(null, new object[] { "Jump", origJump });
            }
        }

        // (k) Game Feel Pass (GATES 1-5): hitstop 50-100ms, shake decay <300ms, mesmo frame, squash/stretch e 3 camadas na coleta
        [UnityTest]
        public IEnumerator Smoke_13_GameFeelJuiceMatchesDesignBible()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var juiceT = GameType("Studio.Game.Combat.GameFeelJuiceController");
            var locoT  = GameType("Studio.Game.Animation.DezLocomotionController");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var juice = UnityEngine.Object.FindAnyObjectByType(juiceT);
            var loco  = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(juice, "GameFeelJuiceController obrigatorio.");
            Assert.IsNotNull(loco, "DezLocomotionController obrigatorio.");

            var pDisable = juiceT.GetProperty("DisableJuiceForComparison");
            pDisable.SetValue(null, false);

            // GATE 1: Golpe do Dez — hitstop 50-100ms + shake <300ms + particula + som no MESMO frame
            juiceT.GetMethod("TriggerDezAttackHit").Invoke(juice, new object[] { loco.transform.position + Vector3.forward, false });
            float hitstop = (float)juiceT.GetProperty("LastHitstopAppliedSec").GetValue(juice);
            float shakeDecay = (float)juiceT.GetProperty("LastShakeDecayAppliedSec").GetValue(juice);
            int fHit = (int)juiceT.GetProperty("LastHitHitstopFrame").GetValue(juice);
            int fPart = (int)juiceT.GetProperty("LastHitParticleFrame").GetValue(juice);
            int fSfx = (int)juiceT.GetProperty("LastHitSfxFrame").GetValue(juice);

            Assert.That(hitstop, Is.InRange(0.050f, 0.100f), $"Hitstop ({hitstop:F3}s) fora da faixa DESIGN_BIBLE 50-100ms.");
            Assert.Less(shakeDecay, 0.300f, $"Shake decay ({shakeDecay:F3}s) deve ser <300ms.");
            Assert.AreEqual(fHit, fPart, "Hitstop e particula devem disparar no MESMO frame.");
            Assert.AreEqual(fHit, fSfx, "Hitstop e som devem disparar no MESMO frame.");
            yield return new WaitForSecondsRealtime(0.12f);

            // GATE 2: Receber dano (nao fatal) — flash + shake direcional + pausa curta de input
            juiceT.GetMethod("TriggerPlayerDamaged").Invoke(juice, new object[] { loco.transform, loco.transform.position + Vector3.forward * 2f, false });
            Assert.IsTrue((bool)juiceT.GetProperty("IsInputPausedForHit").GetValue(juice), "Pausa curta de input deve ativar ao receber dano.");
            Assert.Less((float)juiceT.GetProperty("LastShakeDecayAppliedSec").GetValue(juice), 0.300f);
            yield return new WaitForSecondsRealtime(0.16f);

            // GATE 3: Inimigo morto — hitstop maior (95ms, dentro de 50-100ms) sem travar fluxo
            juiceT.GetMethod("TriggerEnemyKilled").Invoke(juice, new object[] { null, Vector3.forward });
            float deathHitstop = (float)juiceT.GetProperty("LastHitstopAppliedSec").GetValue(juice);
            Assert.Greater(deathHitstop, hitstop, "Hitstop de morte deve ser maior que golpe leve.");
            Assert.That(deathHitstop, Is.InRange(0.050f, 0.100f), "Hitstop de morte deve permanecer dentro de 50-100ms.");
            yield return new WaitForSecondsRealtime(0.14f);
            Assert.Greater(Time.timeScale, 0.9f, "Time.timeScale deve voltar a 1.0 sem travar o fluxo.");

            // GATE 4: Pulo / Aterrissagem — squash & stretch + poeira
            juiceT.GetMethod("TriggerJumpStretch").Invoke(juice, null);
            yield return null;
            juiceT.GetMethod("TriggerLandingSquashAndDust").Invoke(juice, new object[] { loco.transform.position });
            Assert.GreaterOrEqual((int)juiceT.GetProperty("JumpStretchCount").GetValue(juice), 1);
            Assert.GreaterOrEqual((int)juiceT.GetProperty("LandingSquashDustCount").GetValue(juice), 1);

            // GATE 5: Coletar item — 3 camadas (visual + som + "clim" HUD)
            juiceT.GetMethod("TriggerItemPickupJuice").Invoke(juice, new object[] { loco.transform.position + Vector3.right, "Peca Nucleo" });
            yield return null;
            Assert.GreaterOrEqual((int)juiceT.GetProperty("Pickup3LayerCount").GetValue(juice), 1);
            Assert.IsTrue((bool)juiceT.GetProperty("IsHudClimVisible").GetValue(juice), "HUD 'clim' deve estar visivel na coleta.");

            Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_13: {string.Join(" | ", _consoleErrors)}");
        }

        // (l) Economia e Progressao (GATE 5): coletar pecas -> contador + save JSON -> comprar upgrades -> efeito aplicado e persistido apos reload!
        [UnityTest]
        public IEnumerator Smoke_14_EconomyPartsCollectAndUpgradePersistsAcrossReload()
        {
            yield return LoadSceneAndVerifyCleanConsole("Caldeiral");

            var cs01T    = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var econT    = GameType("Studio.Game.Parts.EconomyUpgradeSystem");
            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            var hpT      = GameType("Studio.Game.Combat.PlayerHealthController");
            var powersT  = GameType("Studio.Game.Powers.DezPowersController");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var econ   = UnityEngine.Object.FindAnyObjectByType(econT);
            var hp     = UnityEngine.Object.FindAnyObjectByType(hpT);
            var powers = UnityEngine.Object.FindAnyObjectByType(powersT);
            Assert.IsNotNull(econ, "EconomyUpgradeSystem obrigatorio.");
            Assert.IsNotNull(hp, "PlayerHealthController obrigatorio.");
            Assert.IsNotNull(powers, "DezPowersController obrigatorio.");

            string tmpDir = Path.Combine(Application.temporaryCachePath, $"concerto_smoke14_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            string tmpSave = Path.Combine(tmpDir, "concerto_save.json");
            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");

            try
            {
                mSetPath.Invoke(null, new object[] { tmpSave });
                econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);

                // 1. Coleta 3 pecas -> contador + Save JSON atualizados
                var mEarn = econT.GetMethod("NotifyPartEarned");
                mEarn.Invoke(econ, new object[] { 1 });
                mEarn.Invoke(econ, new object[] { 1 });
                mEarn.Invoke(econ, new object[] { 1 });
                yield return null;

                Assert.AreEqual(3, (int)econT.GetProperty("PartsEarned").GetValue(econ));
                Assert.AreEqual(3, (int)econT.GetProperty("PartsAvailable").GetValue(econ));

                // 2. Abre posto de troca e compra 2 upgrades (upg_vida_extra = 1, upg_eco_recarga = 1)
                econT.GetMethod("OpenBenchUI").Invoke(econ, null);
                Assert.IsTrue((bool)econT.GetProperty("IsBenchUiOpen").GetValue(econ));
                Assert.IsTrue((bool)econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_vida_extra" }));
                Assert.IsTrue((bool)econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_eco_recarga" }));
                econT.GetMethod("CloseBenchUI").Invoke(econ, null);
                yield return null;

                // Efeitos aplicados na hora
                Assert.AreEqual(1, (int)econT.GetProperty("PartsAvailable").GetValue(econ), "Saldo deve ser 3 - 2 = 1.");
                Assert.AreEqual(5, (int)hpT.GetProperty("MaxSegments").GetValue(hp), "upg_vida_extra deve elevar vida maxima para 5.");
                var pCfg = powersT.GetProperty("Cfg").GetValue(powers);
                Assert.AreEqual(2.0f, (float)pCfg.GetType().GetField("ecoCooldownSec").GetValue(pCfg), 0.01f, "upg_eco_recarga deve reduzir cooldown do Eco para 2.0s.");

                // 3. Simula reload completo via Save JSON versionado -> progresso e efeitos persistem!
                econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);
                Assert.AreEqual(0, (int)econT.GetProperty("PurchasedCount").GetValue(econ));

                var loadArgs = new object[] { null };
                Assert.IsTrue((bool)saveSysT.GetMethod("TryLoad").Invoke(null, loadArgs), "Save JSON deve carregar apos compra.");
                saveSysT.GetMethod("ApplyLoadedStateToCurrentScene").Invoke(null, new object[] { loadArgs[0] });
                yield return null;

                Assert.AreEqual(3, (int)econT.GetProperty("PartsEarned").GetValue(econ));
                Assert.AreEqual(2, (int)econT.GetProperty("PartsSpent").GetValue(econ));
                Assert.AreEqual(1, (int)econT.GetProperty("PartsAvailable").GetValue(econ));
                Assert.IsTrue((bool)econT.GetMethod("HasUpgrade").Invoke(econ, new object[] { "upg_vida_extra" }));
                Assert.IsTrue((bool)econT.GetMethod("HasUpgrade").Invoke(econ, new object[] { "upg_eco_recarga" }));
                Assert.AreEqual(5, (int)hpT.GetProperty("MaxSegments").GetValue(hp));

                Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_14: {string.Join(" | ", _consoleErrors)}");
            }
            finally
            {
                econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);
                mSetPath.Invoke(null, new object[] { null });
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            }
        }

        // (m) Epicentro V2 (GATES 2-5): 5 checkpoints (<=30s retrabalho), leitura visual e recompensa secreta lateral!
        [UnityTest]
        public IEnumerator Smoke_15_EpicentroV2CheckpointsPacingAndSecretReward()
        {
            yield return LoadSceneAndVerifyCleanConsole("Epicentro");

            var dirT  = GameType("Studio.Game.Epicenter.EpicenterV2LevelDesignDirector");
            var econT = GameType("Studio.Game.Parts.EconomyUpgradeSystem");
            var dir   = UnityEngine.Object.FindAnyObjectByType(dirT);
            Assert.IsNotNull(dir, "EpicenterV2LevelDesignDirector deve estar ativo em Epicentro.");

            int cpCount    = (int)dirT.GetProperty("TotalCheckpoints").GetValue(dir);
            float maxGap   = (float)dirT.GetProperty("MaxCheckpointGapMeters").GetValue(dir);
            float maxSec   = (float)dirT.GetProperty("MaxExpectedRetraceSeconds").GetValue(dir);
            int beacons    = (int)dirT.GetProperty("GuidanceBeaconsCount").GetValue(dir);

            Assert.GreaterOrEqual(cpCount, 5, $"Epicentro V2 deve ter >= 5 checkpoints (encontrado={cpCount}).");
            Assert.LessOrEqual(maxSec, 30.0f, $"Nenhum trecho pode exigir >30s de retrabalho (calculado={maxSec:F1}s, gap={maxGap:F1}m).");
            Assert.GreaterOrEqual(beacons, 10, "Rota principal deve ter balizas de leitura visual.");

            // Valida recompensa da saida lateral secreta (nunca castigada)
            Assert.IsFalse((bool)dirT.GetProperty("SecretRewardCollected").GetValue(dir));
            Assert.IsTrue((bool)dirT.GetMethod("CollectSecretRewardForTest").Invoke(dir, null), "Recompensa lateral secreta deve ser coletavel.");
            Assert.IsTrue((bool)dirT.GetProperty("SecretRewardCollected").GetValue(dir));

            var econ = UnityEngine.Object.FindAnyObjectByType(econT);
            if (econ != null) econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);

            Assert.IsEmpty(_consoleErrors, $"Erros no console durante Smoke_15: {string.Join(" | ", _consoleErrors)}");
        }
    }
}
