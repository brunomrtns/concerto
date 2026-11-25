using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Teste de regressao + captura dos MP4s ANTES (GATE 1) e DEPOIS (GATE 5) do bug critico BL-001.
    /// </summary>
    public sealed class BacklogBL001ObjectiveNoResetTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado: {fullName}");
            return t;
        }

        private static void Snap(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var pt = cam.targetTexture;
            var pa = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            cam.targetTexture = pt;
            RenderTexture.active = pa;
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
        }

        private IEnumerator CaptureSequence(string phase, bool simulateBug, Type dlgT, object dlg, Type objT, object junoNpc, TextAsset introJson, Camera cam)
        {
            var bugProp = dlgT.GetProperty("SimulateLegacyObjectiveOverwriteBug", BindingFlags.Public | BindingFlags.Static);
            bugProp.SetValue(null, simulateBug);

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "bl001_capture", phase);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var pCurObj  = objT.GetProperty("CurrentObjective");
            var pActive  = dlgT.GetProperty("IsDialogueActive");
            var mStart   = dlgT.GetMethod("StartConversation");
            var mAdvance = dlgT.GetMethod("PressAdvanceInput");

            var metaLines = new List<string>();
            int f = 0;

            void RecordFrame(string stepLabel)
            {
                Snap(cam, rt, tex, Path.Combine(outDir, $"frame_{f:D4}.jpg"));
                string cur = ((string)pCurObj.GetValue(null) ?? "").Replace("\"", "'");
                bool talking = (bool)pActive.GetValue(dlg);
                metaLines.Add($"{{\"f\":{f},\"step\":\"{stepLabel}\",\"talking\":{(talking ? "true" : "false")},\"objective\":\"{cur}\"}}");
                f++;
            }

            // 1. Estado inicial em andamento: "Pecas (2/5)" (20 frames)
            const string midObj = "Peças (2/5)";
            objT.GetMethod("Set").Invoke(null, new object[] { midObj });
            for (int i = 0; i < 20; i++) { RecordFrame("MISSAO EM ANDAMENTO (2/5 PECAS)"); yield return null; }

            // 2. Jogador fala novamente com a Juno (conversa completa)
            mStart.Invoke(dlg, new object[] { junoNpc, introJson });
            int guard = 0;
            while ((bool)pActive.GetValue(dlg) && guard < 24)
            {
                guard++;
                for (int k = 0; k < 4; k++) { RecordFrame("FALANDO NOVAMENTE COM JUNO"); yield return null; }
                mAdvance.Invoke(dlg, null);
                yield return null;
            }

            // 3. Pos-conversa: mostra o objetivo resultante (30 frames)
            string afterStep = simulateBug ? "BUG: OBJETIVO RESETOU APOS FALAR COM NPC!" : "FIX: OBJETIVO PRESERVADO INTACTO!";
            for (int i = 0; i < 30; i++) { RecordFrame(afterStep); yield return null; }

            string finalObj = (string)pCurObj.GetValue(null);
            if (simulateBug)
                Assert.AreNotEqual(midObj, finalObj, "GATE 1: Modo legado deve reproduzir o bug de reset de objetivo!");
            else
                Assert.AreEqual(midObj, finalObj, "GATE 5: Com o fix, objetivo deve permanecer intacto em 'Peças (2/5)'!");

            File.WriteAllText(Path.Combine(outDir, "frames_meta.json"), "[\n" + string.Join(",\n", metaLines) + "\n]", new UTF8Encoding(false));
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            bugProp.SetValue(null, false);
        }

        [UnityTest]
        public IEnumerator BL001_ReproduceBeforeFixAndVerifyAfterFixWithCapture()
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
            var junoNpc = partsT.GetField("junoNpc").GetValue(parts) as Component;
            var introJson = partsT.GetField("junoIntroDialogueJson").GetValue(parts) as TextAsset;
            Assert.IsNotNull(junoNpc);
            Assert.IsNotNull(introJson);

            // Primeira conversa normal (registra dlg_juno_oficina_intro como concluida)
            dlgT.GetMethod("StartConversation").Invoke(dlg, new object[] { junoNpc, introJson });
            for (int g = 0; g < 30 && (bool)dlgT.GetProperty("IsDialogueActive").GetValue(dlg); g++)
            {
                dlgT.GetMethod("PressAdvanceInput").Invoke(dlg, null);
                yield return null;
            }

            // Camera enquadrando Juno e a oficina limpa
            var camGo = new GameObject("BL001_ProofCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            Vector3 jp = junoNpc.transform.position;
            camGo.transform.position = jp + new Vector3(-2.4f, 1.45f, -2.6f);
            camGo.transform.LookAt(jp + Vector3.up * 0.9f);

            // GATE 1: Reproducao fiel do bug ANTES
            yield return CaptureSequence("antes", true, dlgT, dlg, objT, junoNpc, introJson, cam);

            // GATE 5: Mesmo fluxo rodando certo DEPOIS
            yield return CaptureSequence("depois", false, dlgT, dlg, objT, junoNpc, introJson, cam);

            UnityEngine.Object.Destroy(camGo);
            Debug.Log("[BL-001] GATE 1 (ANTES) e GATE 5 (DEPOIS) validados e capturados com sucesso!");
        }
    }
}
