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
    /// Teste de regressao do BL-001 (CRITICO):
    /// Garante que re-conversar com Juno ou outro NPC NUNCA reseta nem sobrescreve o objetivo atual do jogador.
    /// </summary>
    public sealed class BacklogBL001ObjectiveNoResetTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado: {fullName}");
            return t;
        }

        private static IEnumerator RunFullConversation(Type dlgT, object dlg, object npc, TextAsset json)
        {
            dlgT.GetMethod("StartConversation").Invoke(dlg, new object[] { npc, json });
            var pActive = dlgT.GetProperty("IsDialogueActive");
            var mAdvance = dlgT.GetMethod("PressAdvanceInput");
            int guard = 0;
            while ((bool)pActive.GetValue(dlg) && guard < 40)
            {
                guard++;
                mAdvance.Invoke(dlg, null);
                yield return null;
            }
            Assert.IsFalse((bool)pActive.GetValue(dlg), "Conversa deveria encerrar limpa.");
        }

        [UnityTest]
        public IEnumerator BL001_TalkingToNpcAgainNeverResetsOrOverwritesCurrentObjective()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var dlgT   = GameType("Studio.Game.Dialogue.DialogueController");
            var objT   = GameType("Studio.Game.Dialogue.ObjectiveSystem");
            var partsT = GameType("Studio.Game.Parts.PartsLoopManager");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var dlg   = UnityEngine.Object.FindAnyObjectByType(dlgT);
            var parts = UnityEngine.Object.FindAnyObjectByType(partsT);
            Assert.IsNotNull(dlg, "DialogueController obrigatorio.");
            Assert.IsNotNull(parts, "PartsLoopManager obrigatorio.");

            var junoNpc = partsT.GetField("junoNpc").GetValue(parts);
            var introJson = partsT.GetField("junoIntroDialogueJson").GetValue(parts) as TextAsset;
            var deliverJson = partsT.GetField("junoDeliverDialogueJson").GetValue(parts) as TextAsset;
            Assert.IsNotNull(junoNpc, "junoNpc obrigatorio.");
            Assert.IsNotNull(introJson, "junoIntroDialogueJson obrigatorio.");

            var pCurObj = objT.GetProperty("CurrentObjective");

            // 1. Primeira conversa com Juno -> libera coleta de pecas normalmente
            yield return RunFullConversation(dlgT, dlg, junoNpc, introJson);
            string objAfterFirstTalk = (string)pCurObj.GetValue(null);
            Assert.IsTrue((bool)partsT.GetProperty("CanCollectParts").GetValue(parts), "Primeira conversa deve liberar coleta.");

            // 2. Jogador avanca na missao (ex.: 2/5 pecas coletadas)
            const string midMissionObj = "Peças (2/5)";
            objT.GetMethod("Set").Invoke(null, new object[] { midMissionObj });
            Assert.AreEqual(midMissionObj, (string)pCurObj.GetValue(null));

            // 3. Jogador fala com Juno NOVAMENTE -> objetivo NAO PODE resetar!
            yield return RunFullConversation(dlgT, dlg, junoNpc, introJson);
            Assert.AreEqual(midMissionObj, (string)pCurObj.GetValue(null),
                "BL-001 FALHOU: Re-conversar com Juno resetou/sobrescreveu o objetivo atual!");

            // 4. Jogador avanca para Ato 2 -> falar com Juno de novo NAO PODE voltar para objetivo do Ato 1!
            const string act2Obj = "Repare as anomalias (3/5)";
            objT.GetMethod("Set").Invoke(null, new object[] { act2Obj });
            if (deliverJson != null)
            {
                // Primeira e segunda chamada com deliverJson apos Ato 1/Ato 2
                yield return RunFullConversation(dlgT, dlg, junoNpc, introJson);
                Assert.AreEqual(act2Obj, (string)pCurObj.GetValue(null),
                    "BL-001 FALHOU: Falar com Juno no Ato 2 sobrescreveu o objetivo do Ato 2!");
            }

            // Captura evidencia PNG em captures/backlog/bl001_objetivo_intacto.png
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outPath = Path.Combine(repoRoot, "captures", "backlog", "bl001_objetivo_intacto.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var cam = Camera.main;
            if (cam != null)
            {
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var pt = cam.targetTexture;
                var pa = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0, false);
                tex.Apply(false, false);
                cam.targetTexture = pt;
                RenderTexture.active = pa;
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }

            Debug.Log($"[BL-001] PASSOU: objetivo preservado ('{pCurObj.GetValue(null)}'), primeiro='{objAfterFirstTalk}'.");
        }
    }
}
