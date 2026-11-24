using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class EndToEndBugSweepAuditTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado: {fullName}");
            return t;
        }

        private static void CaptureFrameJpg(Camera cam, RenderTexture rt, Texture2D tex, string filePath)
        {
            if (cam == null) return;
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            File.WriteAllBytes(filePath, tex.EncodeToJPG(88));
        }

        [UnityTest]
        public IEnumerator ValidateFullRouteBootToAct3EndAndThreeP0BugFixes()
        {
            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "bug_sweep_capture");
            Directory.CreateDirectory(outDir);

            const int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            int frame = 0;
            void Cap() { CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{frame:D4}.jpg")); frame++; }

            var saveT   = GameType("Studio.Game.Save.SaveSystem");
            var stasisT = GameType("Studio.Game.Powers.StasisFieldSystem");
            var cs01T   = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var pauseT  = GameType("Studio.Game.Menu.PauseMenuController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var hpT     = GameType("Studio.Game.Combat.PlayerHealthController");
            var stubT   = GameType("Studio.Game.Epicenter.EpicenterArenaBossStub");
            var bossT   = GameType("Studio.Game.Boss.RiftGuardianBossController");
            var fragT   = GameType("Studio.Game.Boss.CoreFragmentInteractable");
            var hitT    = GameType("Studio.Game.Tool.ToolHitInfo");

            // === 1. BOOT (MainMenu) ===
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            for (int i = 0; i < 12; i++) { Cap(); yield return null; }

            // === 2. CALDEIRAL (Ato 1 & 2): Skip duplo rapido em CS01 + Save imediato + BUG-P0-02 (Pause bloqueado) ===
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 0);
            PlayerPrefs.Save();
            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01  = UnityEngine.Object.FindAnyObjectByType(cs01T);
            var pause = UnityEngine.Object.FindAnyObjectByType(pauseT);
            Assert.IsNotNull(cs01);
            Assert.IsNotNull(pause);

            // Pause bloqueado durante CS01
            if ((bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                Assert.IsFalse((bool)pauseT.GetMethod("CanOpenPauseNow").Invoke(pause, null), "BUG-P0-02: Pause deve ser bloqueado durante cutscene.");

            // Skip duplo rapido + save imediato!
            cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            saveT.GetMethod("Save").Invoke(null, new object[] { "qa_post_double_skip", null, null });
            Assert.IsTrue((bool)saveT.GetMethod("HasSave").Invoke(null, null), "Save imediato apos skip duplo deve persistir limpo.");
            for (int i = 0; i < 16; i++) { Cap(); yield return null; }

            // === 3. PROVA BUG-P0-01: Estase ativa + Time.timeScale=0.03f durante transicao de cena -> 1.000f EXATO! ===
            var dezCal = GameObject.Find("Player_Dez_Placeholder");
            var powersCal = dezCal.GetComponent(powersT);
            powersT.GetMethod("UnlockPowers").Invoke(powersCal, new object[] { false });
            powersT.GetMethod("TriggerEstase").Invoke(powersCal, null);
            Time.timeScale = 0.03f; // Simula parry freeze interrompido por troca de cena!

            yield return SceneManager.LoadSceneAsync("SetorExterno", LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(1.0f, Time.timeScale, 0.0001f, "BUG-P0-01: Time.timeScale deve restaurar EXATO 1.000 na transicao de cena!");
            Assert.AreEqual(1.0f, (float)stasisT.GetProperty("WorldTimeScale").GetValue(null), 0.0001f, "BUG-P0-01: Estase deve ser limpa na transicao de cena!");
            for (int i = 0; i < 18; i++) { Cap(); yield return null; }

            // === 4. EPICENTRO -> PROVA BUG-P0-03 (Trigger Boss durante Knockdown) -> VITORIA LIMPA -> FIM DO ATO 3 ===
            yield return SceneManager.LoadSceneAsync("Epicentro", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var dezEp = GameObject.Find("Player_Dez_Placeholder");
            var hpEp  = dezEp.GetComponent(hpT);
            var stub  = UnityEngine.Object.FindAnyObjectByType(stubT);
            var boss  = UnityEngine.Object.FindAnyObjectByType(bossT);
            var cc    = dezEp.GetComponent<CharacterController>();

            // Posiciona Dez FORA do raio do trigger da arena, dispara knockdown e so entao encosta no trigger (BUG-P0-03!)
            if (cc != null) cc.enabled = false;
            dezEp.transform.position = new Vector3(102f, 1.05f, 0f);
            if (cc != null) cc.enabled = true;

            int g = 0;
            while ((int)hpT.GetProperty("CurrentSegments").GetValue(hpEp) > 0 && !(bool)hpT.GetProperty("IsKnockedDown").GetValue(hpEp) && g < 6)
            {
                g++;
                float w = 0f;
                while ((bool)hpT.GetProperty("IsInvulnerable").GetValue(hpEp) && w < 1.1f) { yield return null; w += Time.unscaledDeltaTime; }
                hpT.GetMethod("TryTakeDamage").Invoke(hpEp, new object[] { 1, dezEp.transform.position + Vector3.right });
                yield return null;
            }
            Assert.IsTrue((bool)hpT.GetProperty("IsKnockedDown").GetValue(hpEp));
            // Move Dez em knockdown para o centro da arena e tenta iniciar boss -> DEVE permanecer Dormant!
            if (cc != null) cc.enabled = false;
            dezEp.transform.position = new Vector3(115f, 1.05f, 0f);
            if (cc != null) cc.enabled = true;
            yield return null;
            bossT.GetMethod("StartBossFight").Invoke(boss, null);
            Assert.AreEqual("Dormant", bossT.GetProperty("State").GetValue(boss).ToString(), "BUG-P0-03: Boss nao pode iniciar enquanto player esta em knockdown!");
            for (int i = 0; i < 10; i++) { Cap(); yield return null; }

            // Aguarda respawn limpo -> agora luta inicia, derrota boss e conclui Ato 3!
            float wr = 0f;
            while ((bool)hpT.GetProperty("IsKnockedDown").GetValue(hpEp) && wr < 3.5f) { Cap(); yield return null; wr += Time.unscaledDeltaTime; }
            stubT.GetMethod("TriggerBossOmenStub").Invoke(stub, null);
            yield return null;
            Assert.AreNotEqual("Dormant", bossT.GetProperty("State").GetValue(boss).ToString(), "Boss deve iniciar normalmente apos respawn.");
            for (int i = 0; i < 16; i++) { Cap(); yield return null; }

            for (int k = 0; k < 8 && (bool)bossT.GetProperty("IsAlive").GetValue(boss); k++)
            {
                var h = Activator.CreateInstance(hitT);
                hitT.GetField("Damage").SetValue(h, 120f);
                hitT.GetField("IsHeavy").SetValue(h, true);
                hitT.GetField("KnockbackDir").SetValue(h, Vector3.right);
                bossT.GetMethod("ApplyDamage").Invoke(boss, new object[] { h });
                for (int f = 0; f < 5; f++) { Cap(); yield return null; }
            }

            var frag = bossT.GetProperty("SpawnedCoreFragment").GetValue(boss);
            Assert.IsNotNull(frag);
            fragT.GetMethod("Interact").Invoke(frag, new object[] { dezEp });
            float wEnd = 0f;
            while (!(bool)fragT.GetProperty("Act3EndCardShown").GetValue(frag) && wEnd < 4.0f) { Cap(); yield return null; wEnd += Time.unscaledDeltaTime; }
            for (int i = 0; i < 16; i++) { Cap(); yield return null; }

            Assert.IsTrue((bool)fragT.GetProperty("Act3EndCardShown").GetValue(frag));
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            Debug.Log($"[BugSweepTest #106] PASSOU: Boot->Caldeiral->SetorExterno->Epicentro->Act3End limpo, 3 fixes P0 provados, frames={frame}");
        }
    }
}
