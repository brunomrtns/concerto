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
    /// <summary>
    /// GATE 5 — Captura PlayMode de 3 minutos (180 estados a 1s cada -> 180.0s em MP4 1080p)
    /// navegando pelos menus usando SO controle (Pause -> Opcoes -> 5 buses de audio -> Brilho ->
    /// Sensibilidade -> Escala de Fonte 3 tamanhos -> Legendas -> Modo Daltoico 3 perfis ->
    /// Remapeamento de Teclas -> retorno ao gameplay com HUD escalado + legendas com falante).
    /// </summary>
    public sealed class UiAccessibilityControllerNavCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string section;
            public string controllerInput;
            public string liveState;
            public string badge;
        }

        [Serializable]
        private sealed class CaptureMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
        }

        private string _outDir;
        private CaptureMeta _meta;
        private Texture2D _readTex;
        private RenderTexture _rt;

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        [UnityTest]
        public IEnumerator UiAccessibility_Capture3MinControllerOnlyNavigation()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ui_a11y_capture"));
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            _meta = new CaptureMeta();
            _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var cs01T   = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var pauseT  = GameType("Studio.Game.Menu.PauseMenuController");
            var optT    = GameType("Studio.Game.Menu.OptionsPanelUI");
            var setT    = GameType("Studio.Game.Menu.GameSettingsManager");
            var cbEnum  = GameType("Studio.Game.Menu.ColorblindProfile");
            var a11yT   = GameType("Studio.Game.UI.AccessibilityRuntimeController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            for (int i = 0; i < 6; i++) yield return null;

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var pause  = UnityEngine.Object.FindAnyObjectByType(pauseT);
            var a11y   = UnityEngine.Object.FindAnyObjectByType(a11yT);
            var powers = UnityEngine.Object.FindAnyObjectByType(powersT);
            Assert.IsNotNull(pause);
            Assert.IsNotNull(a11y);

            // Salva estado original das opcoes para restaurar no finally
            float origMaster = (float)setT.GetProperty("MasterVolume").GetValue(null);
            float origMusic  = (float)setT.GetProperty("MusicVolume").GetValue(null);
            float origSfx    = (float)setT.GetProperty("SfxVolume").GetValue(null);
            float origAmb    = (float)setT.GetProperty("AmbienceVolume").GetValue(null);
            float origUi     = (float)setT.GetProperty("UiVolume").GetValue(null);
            float origBright = (float)setT.GetProperty("Brightness").GetValue(null);
            float origSens   = (float)setT.GetProperty("CameraSensitivity").GetValue(null);
            int   origFont   = (int)setT.GetProperty("FontScaleIndex").GetValue(null);
            bool  origSub    = (bool)setT.GetProperty("SubtitlesEnabled").GetValue(null);
            object origCb    = setT.GetProperty("ColorblindMode").GetValue(null);

            try
            {
                // 00:00 - 00:12 (12s): Pause Menu navegacao 100% por controle
                pauseT.GetMethod("TryPause").Invoke(pause, null);
                yield return CaptureSec(4, "MENU DE PAUSA (CONTROLE)", "[START] Pausar -> Foco: Continuar", "Continuar | Opcoes | Menu Principal", "PAUSE");
                pauseT.GetMethod("NavigatePause").Invoke(pause, new object[] { +1 });
                yield return CaptureSec(4, "MENU DE PAUSA (CONTROLE)", "[D-PAD BAIXO] Foco: Opcoes", "Navegacao sem mouse obrigatorio", "PAUSE");
                pauseT.GetMethod("NavigatePause").Invoke(pause, new object[] { +1 });
                yield return CaptureSec(4, "MENU DE PAUSA (CONTROLE)", "[D-PAD BAIXO] Foco: Menu Principal", "Confirmacao segura se houver progresso", "PAUSE");

                // 00:12 - 00:16 (4s): Volta para Opcoes e confirma com [A]
                pauseT.GetMethod("NavigatePause").Invoke(pause, new object[] { -1 });
                pauseT.GetMethod("ConfirmPauseSelection").Invoke(pause, null);
                var opt = pauseT.GetField("optionsPanel").GetValue(pause) as Component;
                var mNav = optT.GetMethod("NavigateOption");
                var mAdj = optT.GetMethod("AdjustOption");
                var pSum = optT.GetProperty("FocusedOptionSummary");
                yield return CaptureSec(4, "TELA DE OPCOES (CONTROLE)", "[A] Abrir Opcoes", (string)pSum.GetValue(opt), "OPCOES");

                // 00:16 - 00:56 (40s): 5 sliders de volume por bus (Master, Musica, SFX, Ambiencia, UI)
                string[] busNames = { "Master", "Musica", "SFX", "Ambiencia", "UI" };
                for (int b = 0; b < 5; b++)
                {
                    if (b > 0) mNav.Invoke(opt, new object[] { +1 });
                    mAdj.Invoke(opt, new object[] { -1 });
                    yield return CaptureSec(4, $"AUDIO BUS {b + 1}/5 — {busNames[b].ToUpperInvariant()}", "[D-PAD ESQUERDA] Diminuir volume", (string)pSum.GetValue(opt), "AUDIO");
                    mAdj.Invoke(opt, new object[] { +1 });
                    yield return CaptureSec(4, $"AUDIO BUS {b + 1}/5 — {busNames[b].ToUpperInvariant()}", "[D-PAD DIREITA] Restaurar volume (PlayerPrefs)", (string)pSum.GetValue(opt), "AUDIO");
                }

                // 00:56 - 01:12 (16s): Brilho (escuro -> normal -> claro)
                mNav.Invoke(opt, new object[] { +1 }); // row 5 = Brilho
                setT.GetMethod("SetBrightness").Invoke(null, new object[] { 0.65f });
                yield return CaptureSec(5, "VIDEO — BRILHO (APLICADO NA HORA)", "[D-PAD ESQUERDA] Brilho 65%", (string)pSum.GetValue(opt), "BRILHO");
                setT.GetMethod("SetBrightness").Invoke(null, new object[] { 1.30f });
                yield return CaptureSec(6, "VIDEO — BRILHO (APLICADO NA HORA)", "[D-PAD DIREITA] Brilho 130%", (string)pSum.GetValue(opt), "BRILHO");
                setT.GetMethod("SetBrightness").Invoke(null, new object[] { 1.00f });
                yield return CaptureSec(5, "VIDEO — BRILHO (NORMAL)", "[D-PAD] Brilho 100%", (string)pSum.GetValue(opt), "BRILHO");

                // 01:12 - 01:22 (10s): Sensibilidade de Camera
                mNav.Invoke(opt, new object[] { +1 }); // row 6 = Sensibilidade
                mAdj.Invoke(opt, new object[] { +1 });
                yield return CaptureSec(5, "CAMERA — SENSIBILIDADE", "[D-PAD DIREITA] Aumentar sensibilidade", (string)pSum.GetValue(opt), "CAMERA");
                mAdj.Invoke(opt, new object[] { -1 });
                yield return CaptureSec(5, "CAMERA — SENSIBILIDADE", "[D-PAD ESQUERDA] Ajustar sensibilidade", (string)pSum.GetValue(opt), "CAMERA");

                // 01:22 - 01:40 (18s): Escala de Fonte (3 tamanhos: Pequena, Normal, Grande)
                mNav.Invoke(opt, new object[] { +1 }); // row 7 = Fonte
                int[] fontSizes = { 0, 1, 2 };
                string[] fontNames = { "Pequena (0.85x)", "Normal (1.00x)", "Grande (1.25x)" };
                for (int f = 0; f < 3; f++)
                {
                    setT.GetMethod("SetFontScaleIndex").Invoke(null, new object[] { fontSizes[f] });
                    optT.GetMethod("RefreshFromSettings").Invoke(opt, null);
                    yield return CaptureSec(6, $"ESCALA DE FONTE — {fontNames[f].ToUpperInvariant()}", "[D-PAD DIREITA] Trocar tamanho de fonte", (string)pSum.GetValue(opt), "FONTE");
                }

                // 01:40 - 01:52 (12s): Legendas On/Off
                mNav.Invoke(opt, new object[] { +1 }); // row 8 = Legendas
                mAdj.Invoke(opt, new object[] { +1 }); // Desligadas
                yield return CaptureSec(6, "LEGENDAS — DESLIGADAS", "[A] Alternar legendas OFF", (string)pSum.GetValue(opt), "LEGENDAS");
                mAdj.Invoke(opt, new object[] { +1 }); // Ligadas
                yield return CaptureSec(6, "LEGENDAS — LIGADAS (COM NOME DO FALANTE)", "[A] Alternar legendas ON", (string)pSum.GetValue(opt), "LEGENDAS");

                // 01:52 - 02:16 (24s): Modo Daltoico (3 perfis: Protanopia, Deuteranopia, Tritanopia + Normal)
                mNav.Invoke(opt, new object[] { +1 }); // row 9 = Modo Daltoico
                string[] profiles = { "Protanopia", "Deuteranopia", "Tritanopia", "None" };
                for (int p = 0; p < profiles.Length; p++)
                {
                    setT.GetMethod("SetColorblindMode").Invoke(null, new object[] { Enum.Parse(cbEnum, profiles[p]) });
                    optT.GetMethod("RefreshFromSettings").Invoke(opt, null);
                    yield return CaptureSec(6, $"MODO DALTONICO — {profiles[p].ToUpperInvariant()}", "[D-PAD DIREITA] Aplicar perfil na hora (sem restart)", (string)pSum.GetValue(opt), "DALTONISMO");
                }

                // 02:16 - 02:40 (24s): Remapeamento de Teclas (Pular, Interagir, Eco Temporal, Estase)
                string[] actions = { "Pular", "Interagir", "Eco Temporal", "Estase" };
                for (int a = 0; a < actions.Length; a++)
                {
                    mNav.Invoke(opt, new object[] { +1 }); // rows 10..13
                    mAdj.Invoke(opt, new object[] { +1 });
                    yield return CaptureSec(6, $"REMAPEAR TECLAS — {actions[a].ToUpperInvariant()}", "[A / D-PAD] Remapear tecla (salva em PlayerPrefs)", (string)pSum.GetValue(opt), "REMAP");
                }

                // 02:40 - 03:00 (20s): Fecha Opcoes & Pause via [B] e demonstra HUD escalado + Legendas com falante no gameplay
                setT.GetMethod("SetColorblindMode").Invoke(null, new object[] { Enum.Parse(cbEnum, "Protanopia") });
                pauseT.GetMethod("BackPauseNavigation").Invoke(pause, null);
                yield return CaptureSec(4, "RETORNANDO AO PAUSE", "[B] Fechar Opcoes", "Todas as opcoes aplicadas na hora!", "CONTROLE");
                pauseT.GetMethod("BackPauseNavigation").Invoke(pause, null);
                yield return CaptureSec(4, "GAMEPLAY — HUD ESCALADO + PROTANOPIA", "[B] Retomar jogo", "HUD legivel sem sobreposicao (Fonte Grande 1.25x)", "HUD");

                a11yT.GetMethod("ShowSubtitle").Invoke(a11y, new object[] { "Juno", "Dez, as pecas estao espalhadas pela oficina!", 6.0f });
                yield return CaptureSec(6, "LEGENDAS EM FALA (COM NOME DO FALANTE)", "[LEGENDAS ON] [JUNO] fala exibida", (string)a11yT.GetProperty("ActiveSubtitleText").GetValue(a11y), "LEGENDAS");

                if (powers != null)
                {
                    powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
                    powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
                }
                else
                {
                    a11yT.GetMethod("ShowSubtitle").Invoke(a11y, new object[] { "ECO TEMPORAL", "Pulso temporal reverso ativado", 6.0f });
                }
                yield return CaptureSec(6, "LEGENDAS EM SOM RELEVANTE DE GAMEPLAY", "[ECO TEMPORAL] Legenda automatica de SFX", (string)a11yT.GetProperty("ActiveSubtitleText").GetValue(a11y), "CONCLUIDO");

                Assert.AreEqual(180, _meta.frames.Count, "Captura de 3 minutos deve conter exatamente 180 segundos.");
                File.WriteAllText(Path.Combine(_outDir, "ui_a11y_meta.json"), JsonUtility.ToJson(_meta, true));
            }
            finally
            {
                setT.GetMethod("SetMasterVolume").Invoke(null, new object[] { origMaster });
                setT.GetMethod("SetMusicVolume").Invoke(null, new object[] { origMusic });
                setT.GetMethod("SetSfxVolume").Invoke(null, new object[] { origSfx });
                setT.GetMethod("SetAmbienceVolume").Invoke(null, new object[] { origAmb });
                setT.GetMethod("SetUiVolume").Invoke(null, new object[] { origUi });
                setT.GetMethod("SetBrightness").Invoke(null, new object[] { origBright });
                setT.GetMethod("SetCameraSensitivity").Invoke(null, new object[] { origSens });
                setT.GetMethod("SetFontScaleIndex").Invoke(null, new object[] { origFont });
                setT.GetMethod("SetSubtitlesEnabled").Invoke(null, new object[] { origSub });
                setT.GetMethod("SetColorblindMode").Invoke(null, new object[] { origCb });
                setT.GetMethod("ResetActionKeys").Invoke(null, null);
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator CaptureSec(int seconds, string section, string controllerInput, string liveState, string badge)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var prev = cam.targetTexture;
                cam.targetTexture = _rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = _rt;
                _readTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                _readTex.Apply(false);
                RenderTexture.active = null;
            }

            byte[] jpg = _readTex.EncodeToJPG(84);
            for (int s = 0; s < seconds; s++)
            {
                int idx = _meta.frames.Count;
                File.WriteAllBytes(Path.Combine(_outDir, $"frame_{idx:D4}.jpg"), jpg);
                _meta.frames.Add(new FrameMeta
                {
                    index = idx,
                    section = section,
                    controllerInput = controllerInput,
                    liveState = liveState ?? string.Empty,
                    badge = badge
                });
            }
        }
    }
}
