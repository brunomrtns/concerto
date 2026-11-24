using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class RiftGuardianBossValidationAndCaptureTest
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
        public IEnumerator ValidateBossFullFightRetryPhase2CoreFragmentHologramAndAct3Card()
        {
            PlayerPrefs.SetInt("CONCERTO_CS03_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_POWERS_UNLOCKED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Epicentro", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var locoT   = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var toolT   = GameType("Studio.Game.Tool.MultiToolController");
            var hitT    = GameType("Studio.Game.Tool.ToolHitInfo");
            var hpT     = GameType("Studio.Game.Combat.PlayerHealthController");
            var stubT   = GameType("Studio.Game.Epicenter.EpicenterArenaBossStub");
            var bossT   = GameType("Studio.Game.Boss.RiftGuardianBossController");
            var fragT   = GameType("Studio.Game.Boss.CoreFragmentInteractable");

            var dezGo  = GameObject.Find("Player_Dez_Placeholder");
            var cc     = dezGo.GetComponent<CharacterController>();
            var loco   = dezGo.GetComponent(locoT);
            var powers = dezGo.GetComponent(powersT);
            var hp     = dezGo.GetComponent(hpT);
            var tool   = UnityEngine.Object.FindAnyObjectByType(toolT);
            var stub   = UnityEngine.Object.FindAnyObjectByType(stubT);
            var boss   = UnityEngine.Object.FindAnyObjectByType(bossT);

            Assert.IsNotNull(boss, "RiftGuardianBossController obrigatorio na Arena Final de Epicentro.");
            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
            if (tool != null) toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, false });

            // Posiciona Dez entrando na Arena Final do Coracao da Fenda
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = new Vector3(114f, 1.05f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;

            stubT.GetMethod("TriggerBossOmenStub").Invoke(stub, null);
            yield return null;

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "boss_capture");
            Directory.CreateDirectory(outDir);
            var eventsLog = new List<string>();
            void LogEv(float t, string ev, string clip)
            {
                eventsLog.Add(string.Format(CultureInfo.InvariantCulture,
                    "{{\"t\":{0:F3},\"event\":\"{1}\",\"clip\":\"{2}\"}}", t, ev, clip));
            }

            const int width = 1280, height = 720;
            const float fps = 15f;
            const float dtStep = 1f / fps;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir     = locoT.GetProperty("ScriptedMoveDir");
            var pRun         = locoT.GetProperty("ScriptedRun");
            pUseScripted.SetValue(loco, true);

            int frame = 0;
            float simT = 0f;
            void Cap()
            {
                CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{frame:D4}.jpg"));
                frame++;
            }

            // === PROVA 1 (GATE 4): RETRY LIMPO NA ARENA ===
            // Aplica 1 dano no boss, depois derruba Dez (knockdown) -> respawn no checkpoint da arena -> boss reseta para 100% HP!
            var hitPre = Activator.CreateInstance(hitT);
            hitT.GetField("Damage").SetValue(hitPre, 40f);
            hitT.GetField("IsHeavy").SetValue(hitPre, true);
            hitT.GetField("KnockbackDir").SetValue(hitPre, Vector3.right);
            bossT.GetMethod("ApplyDamage").Invoke(boss, new object[] { hitPre });
            LogEv(simT, "omen", "SFX/Epicenter/sfx_epicenter_boss_omen_TEMP.wav");
            for (int i = 0; i < 16; i++) { Cap(); yield return null; simT += dtStep; }

            // Simula knockdown do Dez na arena
            int guard = 0;
            while ((int)hpT.GetProperty("CurrentSegments").GetValue(hp) > 0 && !(bool)hpT.GetProperty("IsKnockedDown").GetValue(hp) && guard < 6)
            {
                guard++;
                float w = 0f;
                while ((bool)hpT.GetProperty("IsInvulnerable").GetValue(hp) && w < 1.1f) { yield return null; w += Time.unscaledDeltaTime; }
                hpT.GetMethod("TryTakeDamage").Invoke(hp, new object[] { 1, dezGo.transform.position + Vector3.right });
                yield return null;
            }
            float wResp = 0f;
            while ((bool)hpT.GetProperty("IsKnockedDown").GetValue(hp) && wResp < 3.5f) { Cap(); yield return null; wResp += Time.unscaledDeltaTime; simT += dtStep; }

            Assert.GreaterOrEqual((int)bossT.GetProperty("RetryResetCount").GetValue(boss), 1, "Boss deve reiniciar limpo apos retry no checkpoint da arena.");
            Assert.AreEqual((float)bossT.GetProperty("MaxHp").GetValue(boss), (float)bossT.GetProperty("CurrentHp").GetValue(boss), 0.1f, "HP do Boss deve restaurar 100% no retry.");

            // === PROVA 2 (GATE 2): LUTA COMPLETA — FASE 1 -> FASE 2 -> VITORIA! ===
            pMoveDir.SetValue(loco, Vector3.right);
            pRun.SetValue(loco, true);
            for (int i = 0; i < 24; i++) { Cap(); yield return null; simT += dtStep; }

            // Eco Temporal desviando na Fase 1 + punicao ate cruzar 50% HP (Fase 2!)
            powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
            LogEv(simT, "eco", "SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");
            for (int i = 0; i < 18; i++) { Cap(); yield return null; simT += dtStep; }

            LogEv(simT, "slam", "SFX/Boss/sfx_boss_slam_TEMP.wav");
            for (int golpe = 0; golpe < 4; golpe++)
            {
                if (tool != null) toolT.GetMethod("PlayToolSwingFeedback").Invoke(tool, new object[] { true, 2 });
                LogEv(simT, "hit", "SFX/Tool/tool_melee_hit_TEMP.wav");
                var h = Activator.CreateInstance(hitT);
                hitT.GetField("Damage").SetValue(h, 95f);
                hitT.GetField("IsHeavy").SetValue(h, true);
                hitT.GetField("KnockbackDir").SetValue(h, Vector3.right);
                bossT.GetMethod("ApplyDamage").Invoke(boss, new object[] { h });
                for (int k = 0; k < 14; k++) { Cap(); yield return null; simT += dtStep; }
            }

            Assert.IsTrue((bool)bossT.GetProperty("IsPhase2").GetValue(boss), "Boss deve entrar na Fase 2 ao cruzar 50% HP!");

            // Fase 2: Estase ativa para atravessar spray/Estilhacos e finalizar o Boss!
            powersT.GetMethod("TriggerEstase").Invoke(powers, null);
            LogEv(simT, "estase", "SFX/Powers/sfx_estase_pulse_TEMP.wav");
            for (int i = 0; i < 22; i++) { Cap(); yield return null; simT += dtStep; }

            int finGuard = 0;
            while ((bool)bossT.GetProperty("IsAlive").GetValue(boss) && finGuard < 8)
            {
                finGuard++;
                if (tool != null) toolT.GetMethod("PlayToolSwingFeedback").Invoke(tool, new object[] { true, 2 });
                LogEv(simT, "hit_f2", "SFX/Tool/tool_melee_hit_TEMP.wav");
                var h = Activator.CreateInstance(hitT);
                hitT.GetField("Damage").SetValue(h, 110f);
                hitT.GetField("IsHeavy").SetValue(h, true);
                hitT.GetField("KnockbackDir").SetValue(h, Vector3.right);
                bossT.GetMethod("ApplyDamage").Invoke(boss, new object[] { h });
                for (int k = 0; k < 12; k++) { Cap(); yield return null; simT += dtStep; }
            }
            Assert.IsFalse((bool)bossT.GetProperty("IsAlive").GetValue(boss), "Guardiao da Fenda deve morrer com HP 0.");
            LogEv(simT, "boss_dissolve", "SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
            for (int i = 0; i < 18; i++) { Cap(); yield return null; simT += dtStep; }

            // === PROVA 3: FRAGMENTO DE NUCLEO -> FLASH DE HOLOGRAMA (0.5s, SEM DIALOGO) -> CARTAO FIM DO ATO 3 ===
            var frag = bossT.GetProperty("SpawnedCoreFragment").GetValue(boss);
            Assert.IsNotNull(frag, "Fragmento de Nucleo deve ser dropado na morte do boss.");
            fragT.GetMethod("Interact").Invoke(frag, new object[] { dezGo });
            LogEv(simT, "hologram_flash", "SFX/Boss/sfx_hologram_flash_TEMP.wav");

            float wEnd = 0f;
            while (!(bool)fragT.GetProperty("Act3EndCardShown").GetValue(frag) && wEnd < 4.0f)
            {
                Cap();
                yield return null;
                wEnd += Time.unscaledDeltaTime;
                simT += dtStep;
            }
            for (int i = 0; i < 20; i++) { Cap(); yield return null; simT += dtStep; }

            Assert.IsTrue((bool)fragT.GetProperty("HologramFlashShown").GetValue(frag), "Flash de holograma da oficina + esposa (0.5s) obrigatorio.");
            Assert.IsTrue((bool)fragT.GetProperty("Act3EndCardShown").GetValue(frag), "Cartao 'FIM DO ATO 3' obrigatorio.");

            pMoveDir.SetValue(loco, Vector3.zero);
            pUseScripted.SetValue(loco, false);
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_sec\": {0:F2},", frame * dtStep));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"fps\": {0:F2},", fps));
            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < eventsLog.Count; i++)
                sb.AppendLine("    " + eventsLog[i] + (i < eventsLog.Count - 1 ? "," : ""));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "boss_events.json"), sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[BossTest #103] PASSOU: retryReset=1, phase2=True, defeated=True, hologramFlash=True, act3EndCard=True, frames={frame}");
        }
    }
}
