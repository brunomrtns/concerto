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
    public sealed class CombatV2ValidationAndCaptureTest
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
        public IEnumerator ValidateCombatV2KitTeachEgideTokensLockOnAndCapture()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_CS03_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_POWERS_UNLOCKED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T   = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var locoT   = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var toolT   = GameType("Studio.Game.Tool.MultiToolController");
            var cModT   = GameType("Studio.Game.Tool.CombatToolModule");
            var hitT    = GameType("Studio.Game.Tool.ToolHitInfo");
            var parryT  = GameType("Studio.Game.Combat.ParryAndLockOnController");
            var teachT  = GameType("Studio.Game.Combat.ParryTeachStation");
            var coordT  = GameType("Studio.Game.Combat.CombatGroupCoordinator");
            var zoneT   = GameType("Studio.Game.Combat.CombatEncounterZone");
            var egideT  = GameType("Studio.Game.Combat.EgideEnemy");
            var enemyT  = GameType("Studio.Game.Combat.ManifestationEnemyBase");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var dezGo  = GameObject.Find("Player_Dez_Placeholder");
            var loco   = dezGo.GetComponent(locoT);
            var powers = dezGo.GetComponent(powersT);
            var parry  = dezGo.GetComponent(parryT);
            var tool   = UnityEngine.Object.FindAnyObjectByType(toolT);
            var teach  = UnityEngine.Object.FindAnyObjectByType(teachT);
            var zone   = UnityEngine.Object.FindAnyObjectByType(zoneT);

            Assert.IsNotNull(parry, "ParryAndLockOnController obrigatorio (#109).");
            Assert.IsNotNull(teach, "ParryTeachStation (espaco seguro TEACH) obrigatoria em Caldeiral (#109).");
            Assert.IsNotNull(zone, "CombatEncounterZone obrigatoria.");

            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
            toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, false });
            var cMod = toolT.GetProperty("ActiveModule").GetValue(tool);
            Assert.IsNotNull(cMod, "CombatToolModule deve estar equipado.");

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "combat_v2_capture");
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
            int frame = 0;
            float simT = 0f;
            void Cap() { CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{frame:D4}.jpg")); frame++; }

            // === 1. TEACH EM ESPACO SEGURO (Gate 3 — DESIGN_BIBLE Secao 5) ===
            var teachComp = teach as Component;
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = teachComp.transform.position + new Vector3(-3.2f, 0.05f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;

            var safeProj = teachT.GetMethod("EmitSafeTrainingPulse").Invoke(teach, null);
            Assert.IsNotNull(safeProj, "Estacao TEACH deve emitir pulso seguro (dano 0).");
            for (int i = 0; i < 6; i++) { Cap(); yield return null; simT += dtStep; }
            parryT.GetMethod("TryTriggerParry").Invoke(parry, null);
            bool reflectedSafe = (bool)parryT.GetMethod("TryReflectProjectile").Invoke(parry, new object[] { safeProj });
            Assert.IsTrue(reflectedSafe, "ABSORVER deve refletir o pulso de treino em espaco seguro.");
            Assert.IsTrue((bool)teachT.GetProperty("TeachCompleted").GetValue(teach), "TEACH deve marcar concluido apos parry seguro.");
            LogEv(simT, "teach_parry", "SFX/caldeiral_sfx/contratempo_parry_clang.wav");
            for (int i = 0; i < 16; i++) { Cap(); yield return null; simT += dtStep; }

            // === 2. COMBO DE 3 GOLPES + CANCELAMENTO COM ECO TEMPORAL ===
            for (int step = 1; step <= 3; step++)
            {
                float w = 0f;
                while (!(bool)cModT.GetMethod("ExecuteLightComboStep").Invoke(cMod, null) && w < 0.6f)
                {
                    Cap(); yield return null; w += Time.deltaTime; simT += dtStep;
                }
                Assert.AreEqual(step, (int)cModT.GetProperty("LastComboStep").GetValue(cMod), $"Combo deve atingir golpe {step}/3.");
                LogEv(simT, $"combo_{step}", "SFX/Tool/tool_melee_swing_TEMP.wav");
                for (int k = 0; k < 4; k++) { Cap(); yield return null; simT += dtStep; }
            }
            // Cancela recovery do 3o golpe com Eco Temporal!
            Assert.IsTrue((bool)cModT.GetProperty("IsInEcoCancelWindow").GetValue(cMod), "Janela de cancelamento com Eco deve estar ativa apos golpe.");
            powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
            LogEv(simT, "eco_cancel", "SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");
            Assert.GreaterOrEqual((int)cModT.GetProperty("CancelIntoEcoCount").GetValue(cMod), 1, "Eco deve cancelar o recovery do ataque.");
            for (int i = 0; i < 14; i++) { Cap(); yield return null; simT += dtStep; }

            // === 3. GRUPO DE 4 INIMIGOS (INCL. EGIDE TRIPOSG): TOKENS <= 2 + LOCK-ON + RICOCHETE FRONTAL + QUEBRA POR PARRY E FLANQUEIO ===
            var egidePrefab = zoneT.GetField("egidePrefab").GetValue(zone) as GameObject;
            Assert.IsNotNull(egidePrefab, "Prefab TripoSG do Egide obrigatorio.");
            var zComp = zone as Component;
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = zComp.transform.position + new Vector3(-2.5f, 0.05f, 0f);
            if (cc != null) cc.enabled = true;

            zoneT.GetMethod("TriggerEncounter").Invoke(zone, null);
            var egideGo = UnityEngine.Object.Instantiate(egidePrefab, dezGo.transform.position + new Vector3(3.0f, 0f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            var egide = egideGo.GetComponent(egideT);
            Assert.IsNotNull(egide, "EgideEnemy obrigatorio no prefab TripoSG.");
            yield return null;

            // Verifica Tokens <= 2 mesmo com 4 inimigos ativos!
            int activeTokens = (int)coordT.GetProperty("ActiveAttackTokensCount").GetValue(null);
            Assert.LessOrEqual(activeTokens, 2, "Maximo 2 inimigos podem deter token de ataque simultaneamente!");

            // Lock-on suave + troca de alvo!
            parryT.GetMethod("SelectNextLockTarget").Invoke(parry, null);
            parryT.GetMethod("SelectNextLockTarget").Invoke(parry, null);
            Assert.GreaterOrEqual((int)parryT.GetProperty("LockSwitchCount").GetValue(parry), 1, "Lock-on suave deve trocar de alvo.");

            // Prova do Egide: golpe FRONTAL ricocheteia (dano 0)!
            float hpBefore = (float)enemyT.GetProperty("CurrentHp").GetValue(egide);
            var frontHit = Activator.CreateInstance(hitT);
            hitT.GetField("Damage").SetValue(frontHit, 36f);
            hitT.GetField("IsHeavy").SetValue(frontHit, true);
            hitT.GetField("KnockbackDir").SetValue(frontHit, Vector3.right); // de frente contra Egide olhando left
            egideT.GetMethod("ApplyDamage").Invoke(egide, new object[] { frontHit });
            Assert.AreEqual(hpBefore, (float)enemyT.GetProperty("CurrentHp").GetValue(egide), 0.01f, "Golpe frontal no Egide deve ricochetear!");
            Assert.GreaterOrEqual((int)egideT.GetProperty("FrontRicochetCount").GetValue(egide), 1);
            LogEv(simT, "egide_ricochet", "SFX/caldeiral_sfx/contratempo_parry_clang.wav");
            for (int i = 0; i < 12; i++) { Cap(); yield return null; simT += dtStep; }

            // Prova 1 de quebra do Egide: FLANQUEIO pelas costas!
            var flankHit = Activator.CreateInstance(hitT);
            hitT.GetField("Damage").SetValue(flankHit, 25f);
            hitT.GetField("IsHeavy").SetValue(flankHit, false);
            hitT.GetField("KnockbackDir").SetValue(flankHit, Vector3.left); // pelas costas!
            egideT.GetMethod("ApplyDamage").Invoke(egide, new object[] { flankHit });
            Assert.GreaterOrEqual((int)egideT.GetProperty("FlankHitsReceived").GetValue(egide), 1, "Flanqueio pelas costas deve causar dano no Egide.");
            LogEv(simT, "egide_flank", "SFX/Tool/tool_melee_hit_TEMP.wav");
            for (int i = 0; i < 10; i++) { Cap(); yield return null; simT += dtStep; }

            // Prova 2 de quebra do Egide: ABSORVER (parry) -> guarda quebrada -> golpe frontal agora atravessa e derrota!
            float wCd = 0f;
            while ((float)parryT.GetProperty("ParryCooldownRemaining").GetValue(parry) > 0f && wCd < 3.5f)
            {
                Cap(); yield return null; wCd += Time.unscaledDeltaTime; simT += dtStep;
            }
            parryT.GetMethod("TryTriggerParry").Invoke(parry, null);
            bool meleeParried = (bool)parryT.GetMethod("TryInterceptMeleeAttack").Invoke(parry, new object[] { egide });
            Assert.IsTrue(meleeParried, "ABSORVER deve atordoar o Egide!");
            Assert.IsTrue((bool)egideT.GetProperty("IsGuardBroken").GetValue(egide), "Guarda do Egide deve quebrar apos ABSORVER.");
            LogEv(simT, "egide_parry_break", "SFX/caldeiral_sfx/contratempo_parry_clang.wav");

            var punishHit = Activator.CreateInstance(hitT);
            hitT.GetField("Damage").SetValue(punishHit, 60f);
            hitT.GetField("IsHeavy").SetValue(punishHit, true);
            hitT.GetField("KnockbackDir").SetValue(punishHit, Vector3.right);
            egideT.GetMethod("ApplyDamage").Invoke(egide, new object[] { punishHit });
            Assert.GreaterOrEqual((int)egideT.GetProperty("ParryBreakHitsReceived").GetValue(egide), 1);
            LogEv(simT, "egide_kill", "SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");

            // Finaliza os demais inimigos da wave para disparar micro-slowmo de finalizacao
            var rest = UnityEngine.Object.FindObjectsByType(enemyT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in rest)
            {
                var rc = r as Component;
                if (rc == null) continue;
                var kh = Activator.CreateInstance(hitT);
                hitT.GetField("Damage").SetValue(kh, 80f);
                hitT.GetField("IsHeavy").SetValue(kh, true);
                hitT.GetField("KnockbackDir").SetValue(kh, Vector3.right);
                enemyT.GetMethod("ApplyDamage").Invoke(rc, new object[] { kh });
                for (int k = 0; k < 8; k++) { Cap(); yield return null; simT += dtStep; }
            }

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
            File.WriteAllText(Path.Combine(outDir, "combat_v2_events.json"), sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[CombatV2Test #109] PASSOU: teachParry=True, combo3=True, ecoCancel=1, tokens<=2, egideRicochet+Flank+ParryBreak=True, frames={frame}");
        }
    }
}
