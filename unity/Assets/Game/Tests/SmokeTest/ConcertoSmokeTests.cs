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

            for (int f = 0; f < 30; f++)
            {
                yield return null;
                if (f == 2) pJump.SetValue(loco, false);
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
    }
}
