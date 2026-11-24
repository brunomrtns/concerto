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
    /// Valida TODOS os Gates do fluxo Boot -> Menu Principal -> Novo Jogo / Continuar + Pause + Opcoes:
    ///   - Gate 1: Boot -> Menu -> Novo Jogo (Cutscene 1 roda!) -> Continuar (oficina no estado salvo!) -> zero estado preso
    ///   - Gate 2: Pause funciona em gameplay (timeScale=0, input travado), BLOQUEADO em Cutscene/Dialogo, Opcoes persistem
    ///   - Gate 3: Captura 1080p MP4 (CAPTURE_MENU=1) + 5 screenshots em captures/menu_flow/
    /// </summary>
    public class MenuFlowValidationAndCaptureTest
    {
        private string _framesDir;
        private int _frameIdx;
        private bool _capture;
        private RenderTexture _rt;
        private Texture2D _tex;

        [UnityTest]
        public IEnumerator ValidateBootMenuNewGamePauseOptionsAndContinue()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_MENU") == "1";
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string capDir   = Path.Combine(repoRoot, "captures", "menu_flow");
            _framesDir      = Path.Combine(capDir, "frames_raw");

            if (_capture)
            {
                Directory.CreateDirectory(capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // Limpa save antes de iniciar o teste do fluxo completo
            var saveType = Type.GetType("Studio.Game.Menu.GameSaveState, Assembly-CSharp");
            var settingsType = Type.GetType("Studio.Game.Menu.GameSettingsManager, Assembly-CSharp");
            Assert.IsNotNull(saveType, "GameSaveState nao encontrado!");
            Assert.IsNotNull(settingsType, "GameSettingsManager nao encontrado!");
            saveType.GetMethod("PrepareNewGame").Invoke(null, null);

            // 1. Carrega MainMenu (Boot Splash -> Menu Principal)
            var op = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;

            var menuCtrlType = Type.GetType("Studio.Game.Menu.BootAndMainMenuController, Assembly-CSharp");
            var menuCtrl = UnityEngine.Object.FindAnyObjectByType(menuCtrlType);
            Assert.IsNotNull(menuCtrl, "BootAndMainMenuController nao encontrado em MainMenu!");

            // Aguarda Boot Splash aparecer bem visivel e captura screenshot 01
            yield return CaptureFrames(24, capDir, "01_boot_splash.png", shotAtFrame: 18);

            // Pula/conclui Boot Splash -> Menu Principal
            menuCtrlType.GetMethod("SkipBootSplash").Invoke(menuCtrl, null);
            yield return null;
            Assert.IsTrue((bool)menuCtrlType.GetProperty("IsInMainMenu").GetValue(menuCtrl), "Deveria estar no Menu Principal!");
            yield return CaptureFrames(30, capDir, "02_main_menu_concerto.png", shotAtFrame: 15);

            // 2. Abre Opcoes, altera valores e valida persistencia (Gate 2)
            menuCtrlType.GetMethod("OnOptionsClicked").Invoke(menuCtrl, null);
            yield return null;
            Assert.IsTrue((bool)menuCtrlType.GetProperty("IsOptionsOpen").GetValue(menuCtrl), "Painel de Opcoes deveria abrir!");

            settingsType.GetMethod("SetMasterVolume").Invoke(null, new object[] { 0.62f });
            settingsType.GetMethod("SetMusicVolume").Invoke(null, new object[] { 0.45f });
            settingsType.GetMethod("SetSfxVolume").Invoke(null, new object[] { 0.77f });
            settingsType.GetMethod("SetFullscreen").Invoke(null, new object[] { false });
            settingsType.GetMethod("SetQualityIndex").Invoke(null, new object[] { 1 });

            var optPanel = menuCtrlType.GetField("optionsPanel").GetValue(menuCtrl);
            optPanel.GetType().GetMethod("RefreshFromSettings").Invoke(optPanel, null);
            yield return CaptureFrames(30, capDir, "03_options_panel.png", shotAtFrame: 15);

            // Simula reinicio recarregando de PlayerPrefs!
            settingsType.GetMethod("LoadAndApply").Invoke(null, null);
            Assert.AreEqual(0.62f, (float)settingsType.GetProperty("MasterVolume").GetValue(null), 0.01f, "MasterVolume nao persistiu!");
            Assert.AreEqual(0.45f, (float)settingsType.GetProperty("MusicVolume").GetValue(null), 0.01f, "MusicVolume nao persistiu!");
            Assert.AreEqual(0.77f, (float)settingsType.GetProperty("SfxVolume").GetValue(null), 0.01f, "SfxVolume nao persistiu!");
            Assert.AreEqual(false, (bool)settingsType.GetProperty("IsFullscreen").GetValue(null), "Fullscreen nao persistiu!");
            Assert.AreEqual(1, (int)settingsType.GetProperty("QualityIndex").GetValue(null), "QualityIndex nao persistiu!");

            optPanel.GetType().GetMethod("Close").Invoke(optPanel, null);
            yield return null;

            // 3. Novo Jogo -> Cutscene 1 roda automaticamente + Pause BLOQUEADO durante Cutscene (Gate 1 & 2)
            menuCtrlType.GetMethod("OnNewGameClicked").Invoke(menuCtrl, null);
            yield return WaitForScene("Caldeiral");
            yield return null;
            yield return null;

            var cs01Type = Type.GetType("Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp");
            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01Type);
            Assert.IsNotNull(cs01, "CutsceneManager nao encontrado em Caldeiral!");
            Assert.IsTrue((bool)cs01Type.GetProperty("IsPlaying").GetValue(cs01), "Gate 1 FALHOU: Novo Jogo deveria rodar a Cutscene 1!");

            var pauseType = Type.GetType("Studio.Game.Menu.PauseMenuController, Assembly-CSharp");
            var pauseCtrl = UnityEngine.Object.FindAnyObjectByType(pauseType);
            Assert.IsNotNull(pauseCtrl, "PauseMenuController nao encontrado em Caldeiral!");
            Assert.IsFalse((bool)pauseType.GetMethod("CanOpenPauseNow").Invoke(pauseCtrl, null), "Pause deveria estar BLOQUEADO durante a Cutscene 1!");
            Assert.IsFalse((bool)pauseType.GetMethod("TryPause").Invoke(pauseCtrl, null), "TryPause deveria retornar false durante a Cutscene 1!");

            yield return CaptureFrames(20);

            // Pula Cutscene 1 -> entra no gameplay normal
            cs01Type.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;
            yield return null;

            // Libera e coleta 2 pecas para criar progresso real a ser salvo e restaurado!
            var partsMgrType = Type.GetType("Studio.Game.Parts.PartsLoopManager, Assembly-CSharp");
            var partsMgr = UnityEngine.Object.FindAnyObjectByType(partsMgrType);
            var handleConv = partsMgrType.GetMethod("HandleConversationFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            handleConv.Invoke(partsMgr, new object[] { "dlg_juno_oficina_intro" });

            var partType = Type.GetType("Studio.Game.Parts.MachinePartPickup, Assembly-CSharp");
            var parts = UnityEngine.Object.FindObjectsByType(partType, FindObjectsSortMode.None);
            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            int picked = 0;
            foreach (var p in parts)
            {
                if (picked >= 2) break;
                partType.GetMethod("Interact").Invoke(p, new object[] { dezGo });
                picked++;
            }
            yield return null;

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            string savedObjBeforeMenu = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.AreEqual(2, (int)partsMgrType.GetProperty("CollectedCount").GetValue(partsMgr), "Deveria ter 2 pecas coletadas!");

            // 4. Abre Pause em gameplay real (Gate 2: Time.timeScale == 0, inputs travados, audio pausado!)
            Assert.IsTrue((bool)pauseType.GetMethod("TryPause").Invoke(pauseCtrl, null), "Gate 2 FALHOU: Pause deveria abrir durante gameplay!");
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, 0.001f, "Gate 2 FALHOU: Time.timeScale deveria ser 0 durante Pause!");
            Assert.IsTrue(AudioListener.pause, "AudioListener.pause deveria ser true durante Pause!");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;
            Assert.IsFalse(loco.enabled, "Gate 2 FALHOU: DezLocomotionController deveria ficar travado durante Pause!");

            yield return CaptureFrames(30, capDir, "04_pause_menu_gameplay.png", shotAtFrame: 15);

            // Clica Voltar ao Menu -> verifica modal de confirmacao -> confirma retorno limpo ao Menu Principal!
            pauseType.GetMethod("OnReturnToMenuClicked").Invoke(pauseCtrl, null);
            yield return null;
            Assert.IsTrue((bool)pauseType.GetProperty("IsConfirmModalOpen").GetValue(pauseCtrl), "Modal de confirmacao deveria abrir pois ha progresso!");
            yield return CaptureFrames(15);

            pauseType.GetMethod("ConfirmReturnToMenu").Invoke(pauseCtrl, null);
            yield return WaitForScene("MainMenu");
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "Gate 1 FALHOU: Time.timeScale preso apos voltar ao Menu!");
            Assert.IsFalse(AudioListener.pause, "AudioListener.pause preso apos voltar ao Menu!");

            // 5. No Menu Principal: botao Continuar deve estar ativo -> clica Continuar -> restaura oficina no estado salvo!
            menuCtrl = UnityEngine.Object.FindAnyObjectByType(menuCtrlType);
            menuCtrlType.GetMethod("SkipBootSplash").Invoke(menuCtrl, null);
            yield return null;
            Assert.IsTrue((bool)saveType.GetProperty("HasSave").GetValue(null), "Save minimo deveria existir!");
            yield return CaptureFrames(20);

            menuCtrlType.GetMethod("OnContinueClicked").Invoke(menuCtrl, null);
            yield return WaitForScene("Caldeiral");
            yield return null;
            yield return null;

            cs01 = UnityEngine.Object.FindAnyObjectByType(cs01Type);
            Assert.IsFalse((bool)cs01Type.GetProperty("IsPlaying").GetValue(cs01), "Gate 1 FALHOU: Continuar NAO deveria rodar a Cutscene 1 de novo!");

            partsMgr = UnityEngine.Object.FindAnyObjectByType(partsMgrType);
            Assert.AreEqual(2, (int)partsMgrType.GetProperty("CollectedCount").GetValue(partsMgr), "Gate 1 FALHOU: Continuar deveria restaurar 2 pecas coletadas!");
            string restoredObj = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.AreEqual(savedObjBeforeMenu, restoredObj, "Gate 1 FALHOU: Continuar deveria restaurar o objetivo salvo!");

            yield return CaptureFrames(30, capDir, "05_continue_restored_state.png", shotAtFrame: 15);

            if (_capture)
            {
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }
        }

        private IEnumerator WaitForScene(string sceneName)
        {
            int guard = 0;
            while (SceneManager.GetActiveScene().name != sceneName && guard < 180)
            {
                yield return null;
                guard++;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, $"Cena '{sceneName}' nao carregou!");
        }

        private IEnumerator CaptureFrames(int count, string capDir = null, string shotName = null, int shotAtFrame = -1)
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

                File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(92));
                if (!string.IsNullOrEmpty(shotName) && i == shotAtFrame)
                {
                    File.WriteAllBytes(Path.Combine(capDir, shotName), _tex.EncodeToPNG());
                    Debug.Log($"[MenuCapture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }
    }
}
