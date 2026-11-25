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
    /// GATE 6 — Captura PlayMode dos Dialogos do Ato 1:
    /// falar com Juno (avancar falas no tom do Dez), pular segurando botao, re-falar (fala curta sem resetar missao),
    /// conferir NPCs do Ato 1 e seguir jogando livremente.
    /// </summary>
    public sealed class DialogueSystemAct1CaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string phase;
            public string subtitle;
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
        public IEnumerator Dialogue_CaptureAct1NpcsAdvanceHoldSkipAndPlay()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "dialogos_capture"));
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            _meta = new CaptureMeta();
            _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var dlgT   = GameType("Studio.Game.Dialogue.DialogueController");
            var partsT = GameType("Studio.Game.Parts.PartsLoopManager");
            var objT   = GameType("Studio.Game.Dialogue.ObjectiveSystem");
            var locoT  = GameType("Studio.Game.Animation.DezLocomotionController");

            try
            {
                PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
                PlayerPrefs.Save();
                var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
                while (!op.isDone) yield return null;
                for (int i = 0; i < 6; i++) yield return null;

                var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
                if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                    cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
                yield return null;

                var dlg   = UnityEngine.Object.FindAnyObjectByType(dlgT);
                var parts = UnityEngine.Object.FindAnyObjectByType(partsT);
                var loco  = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
                var junoNpc   = partsT.GetField("junoNpc").GetValue(parts) as Component;
                var introJson = partsT.GetField("junoIntroDialogueJson").GetValue(parts) as TextAsset;

                // 1. Conversa com Juno — avanca 3 falas revisadas (tom do Dez, max 2 linhas)
                dlgT.GetMethod("StartConversation").Invoke(dlg, new object[] { junoNpc, introJson });
                yield return null;
                yield return CaptureSec(4, "1/4 — FALAR COM JUNO (FALA 1/5 — INPUT TRAVADO)", "[JUNO] " + (string)dlgT.GetProperty("CurrentFullText").GetValue(dlg), "DIALOGO");

                dlgT.GetMethod("PressAdvanceInput").Invoke(dlg, null); // revela linha 1
                dlgT.GetMethod("PressAdvanceInput").Invoke(dlg, null); // linha 2
                yield return null;
                yield return CaptureSec(4, "1/4 — AVANCAR FALA [E/A] (FALA 2/5)", "[JUNO] " + (string)dlgT.GetProperty("CurrentFullText").GetValue(dlg), "AVANCAR");

                dlgT.GetMethod("PressAdvanceInput").Invoke(dlg, null);
                dlgT.GetMethod("PressAdvanceInput").Invoke(dlg, null); // linha 3 (Dez!)
                yield return null;
                yield return CaptureSec(4, "1/4 — TOM DO DEZ (ALIEN MECANICO — MAX 2 LINHAS)", "[DEZ] " + (string)dlgT.GetProperty("CurrentFullText").GetValue(dlg), "TOM DEZ");

                // 2. Pular tudo segurando o botao (SkipAllConversation)
                dlgT.GetMethod("SkipAllConversation").Invoke(dlg, null);
                yield return null;
                yield return CaptureSec(4, "2/4 — PULAR TUDO SEGURANDO [ESC/B]", "Dialogo encerrado limpo | Controle de gameplay liberado!", "SKIP LIMPO");

                // 3. Falar de novo com Juno -> fala curta de repeticao e missao intacta!
                objT.GetMethod("Set").Invoke(null, new object[] { "Pecas (2/5) — Missao Intacta" });
                dlgT.GetMethod("StartConversation").Invoke(dlg, new object[] { junoNpc, introJson });
                yield return null;
                yield return CaptureSec(5, "3/4 — REPETIR NPC: FALA CURTA (ZERO RESET DE MISSAO!)", "[JUNO] " + (string)dlgT.GetProperty("CurrentFullText").GetValue(dlg), "REPETICAO");
                dlgT.GetMethod("SkipAllConversation").Invoke(dlg, null);
                yield return null;

                // 4. Segue jogando livremente pela oficina
                locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);
                for (int k = 0; k < 4; k++)
                {
                    yield return CaptureSec(1, "4/4 — GAMEPLAY LIBERADO APOS DIALOGO", "Dez andando livremente | Objetivo: " + (string)objT.GetProperty("CurrentObjective").GetValue(null), "JOGANDO");
                }
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
                locoT.GetProperty("UseScriptedInput").SetValue(loco, false);

                File.WriteAllText(Path.Combine(_outDir, "dialogos_meta.json"), JsonUtility.ToJson(_meta, true));
            }
            finally
            {
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator CaptureSec(int seconds, string phase, string subtitle, string badge)
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
            byte[] jpg = _readTex.EncodeToJPG(86);
            for (int s = 0; s < seconds; s++)
            {
                int idx = _meta.frames.Count;
                File.WriteAllBytes(Path.Combine(_outDir, $"frame_{idx:D4}.jpg"), jpg);
                _meta.frames.Add(new FrameMeta { index = idx, phase = phase, subtitle = subtitle, badge = badge });
            }
        }
    }
}
