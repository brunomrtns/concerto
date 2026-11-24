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
    public sealed class ManifestacoesValidationAndCaptureTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
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
        public IEnumerator ValidateManifestacoesCombatLoopKnockdownRespawnAndCapture()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T   = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var locoT   = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var toolT   = GameType("Studio.Game.Tool.MultiToolController");
            var combatModT = GameType("Studio.Game.Tool.CombatToolModule");
            var hpT     = GameType("Studio.Game.Combat.PlayerHealthController");
            var zoneT   = GameType("Studio.Game.Combat.CombatEncounterZone");
            var enemyT  = GameType("Studio.Game.Combat.ManifestationEnemyBase");
            var regT    = GameType("Studio.Game.Animation.PlayerRegistry");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var dezGo = regT.GetProperty("CurrentGameObject").GetValue(null) as GameObject;
            Assert.IsNotNull(dezGo, "PlayerRegistry.CurrentGameObject obrigatorio (FIX 4).");
            var loco   = dezGo.GetComponent(locoT);
            var powers = dezGo.GetComponent(powersT);
            var hp     = dezGo.GetComponent(hpT);
            var tool   = UnityEngine.Object.FindAnyObjectByType(toolT);
            var zone   = UnityEngine.Object.FindAnyObjectByType(zoneT);

            Assert.IsNotNull(powers, "DezPowersController obrigatorio (#93).");
            Assert.IsNotNull(hp, "PlayerHealthController obrigatorio (#94).");
            Assert.IsNotNull(tool, "MultiToolController obrigatorio (FIX 5).");
            Assert.IsNotNull(zone, "CombatEncounterZone obrigatoria em Caldeiral (#94).");

            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
            toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, false });
            var combatMod = toolT.GetProperty("ActiveModule").GetValue(tool);
            Assert.IsNotNull(combatMod, "CombatToolModule deve estar equipado (FIX 5).");
            Assert.IsTrue(combatModT.IsInstanceOfType(combatMod), "ActiveModule deve ser CombatToolModule (FIX 5).");

            // Posiciona Dez proximo a zona de combate
            var zoneComp = zone as Component;
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = zoneComp.transform.position + new Vector3(-2.5f, 0.05f, 0f);
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;
            hpT.GetMethod("SetCheckpoint").Invoke(hp, new object[] { dezGo.transform.position });

            // Ativa wave unica (2 Estilhacos + 1 Flutuante TripoSG)
            zoneT.GetMethod("TriggerEncounter").Invoke(zone, null);
            yield return null;

            int spawnedCount = (int)zoneT.GetProperty("AliveCount").GetValue(zone);
            Assert.GreaterOrEqual(spawnedCount, 3, "Wave deve spawnar Estilhacos e Flutuante TripoSG.");

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "manifestacoes_capture");
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

            // Fase 1 (0..4s): Aproxima-se, Estilhaco da telegraph, Dez usa ECO TEMPORAL para evadir!
            pMoveDir.SetValue(loco, Vector3.right);
            pRun.SetValue(loco, true);
            LogEv(0.4f, "telegraph", "SFX/Combat/sfx_enemy_telegraph_TEMP.wav");
            for (int i = 0; i < 45; i++) { Cap(); yield return null; simT += dtStep; }

            powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
            LogEv(simT, "eco_evade", "SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");
            // Prova de evasao com i-frames do Eco!
            bool dmgEvaded = (bool)hpT.GetMethod("TryTakeDamage").Invoke(hp, new object[] { 1, dezGo.transform.position + Vector3.right });
            Assert.IsFalse(dmgEvaded, "Ataque durante Eco Temporal deve ser evadido!");
            for (int i = 0; i < 30; i++) { Cap(); yield return null; simT += dtStep; }

            // Fase 2 (5..11s): Ativa ESTASE (desacelera inimigos + projeteis) e derrota os 2 Estilhacos + Flutuante com golpes carregados!
            powersT.GetMethod("TriggerEstase").Invoke(powers, null);
            LogEv(simT, "estase", "SFX/Powers/sfx_estase_pulse_TEMP.wav");
            for (int i = 0; i < 20; i++) { Cap(); yield return null; simT += dtStep; }

            var enemies = UnityEngine.Object.FindObjectsByType(enemyT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var mHeavyStrike = combatModT.GetMethod("ExecuteHeavyStrike");
            var mLightStep   = combatModT.GetMethod("ExecuteLightComboStep");
            var pIsAlive     = enemyT.GetProperty("IsAlive");
            int kills = 0;
            for (int e = 0; e < enemies.Length; e++)
            {
                var eComp = enemies[e] as Component;
                if (eComp == null || !(bool)pIsAlive.GetValue(eComp)) continue;

                // FIX 5: Posiciona o Dez em alcance melee real encarando o inimigo e dispara o swing REAL da MultiTool (OverlapSphere -> IDamageable.ApplyDamage)
                Vector3 toE = (eComp.transform.position - dezGo.transform.position);
                toE.y = 0f;
                Vector3 approachDir = toE.sqrMagnitude > 0.05f ? toE.normalized : Vector3.right;
                pMoveDir.SetValue(loco, approachDir);
                for (int k = 0; k < 10; k++) { Cap(); yield return null; simT += dtStep; }

                if (cc != null) cc.enabled = false;
                Vector3 ePos = eComp.transform.position;
                dezGo.transform.position = new Vector3(ePos.x - approachDir.x * 1.15f, dezGo.transform.position.y, ePos.z - approachDir.z * 1.15f);
                dezGo.transform.rotation = Quaternion.LookRotation(approachDir, Vector3.up);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();

                int swingAttempts = 0;
                while (eComp != null && (bool)pIsAlive.GetValue(eComp) && swingAttempts < 6)
                {
                    swingAttempts++;
                    // Aguarda cooldown real do golpe
                    float cdWait = 0f;
                    bool swung = false;
                    while (!swung && cdWait < 1.2f)
                    {
                        swung = (bool)(swingAttempts == 1 ? mHeavyStrike.Invoke(combatMod, null) : mLightStep.Invoke(combatMod, null));
                        if (!swung)
                        {
                            Cap();
                            yield return null;
                            cdWait += Time.unscaledDeltaTime;
                            simT += dtStep;
                        }
                    }
                    LogEv(simT, "tool_swing", "SFX/Tool/tool_melee_swing_TEMP.wav");
                    LogEv(simT + 0.06f, "tool_hit", "SFX/Tool/tool_melee_hit_TEMP.wav");
                    for (int k = 0; k < 6; k++) { Cap(); yield return null; simT += dtStep; }
                }

                Assert.IsFalse(eComp != null && (bool)pIsAlive.GetValue(eComp), $"Inimigo {eComp?.name} deve morrer pelo swing real da ferramenta (FIX 5).");
                LogEv(simT + 0.14f, "enemy_dissolve", "SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
                kills++;

                for (int k = 0; k < 18; k++) { Cap(); yield return null; simT += dtStep; }
            }

            int realHeavyHits = (int)combatModT.GetProperty("TotalHeavyHitsLanded").GetValue(combatMod);
            int realLightHits = (int)combatModT.GetProperty("TotalLightHitsLanded").GetValue(combatMod);
            Assert.GreaterOrEqual(realHeavyHits + realLightHits, 3, "FIX 5: Hitbox real da CombatToolModule deve registrar >= 3 acertos reais em inimigos.");
            Assert.IsTrue((bool)zoneT.GetProperty("IsCleared").GetValue(zone), "Zona de combate deve ser concluida apos eliminar todos os inimigos.");
            Assert.GreaterOrEqual(kills, 3, "Todos os 3 inimigos devem ser eliminados pelo caminho real de swing.");

            // Fase 3: Prova de dano no Dez ate HP zerar (0/4), vignette vermelha, KNOCKDOWN e RESPAWN no checkpoint!
            pMoveDir.SetValue(loco, Vector3.zero);
            int guard = 0;
            while ((int)hpT.GetProperty("CurrentSegments").GetValue(hp) > 0 && !(bool)hpT.GetProperty("IsKnockedDown").GetValue(hp) && guard < 6)
            {
                guard++;
                float w = 0f;
                while ((bool)hpT.GetProperty("IsInvulnerable").GetValue(hp) && w < 1.2f)
                {
                    Cap();
                    yield return null;
                    w += Time.unscaledDeltaTime;
                    simT += dtStep;
                }
                bool took = (bool)hpT.GetMethod("TryTakeDamage").Invoke(hp, new object[] { 1, dezGo.transform.position + Vector3.forward });
                if (took) LogEv(simT, "player_hurt", "SFX/Combat/sfx_player_hurt_TEMP.wav");
                for (int k = 0; k < 8; k++) { Cap(); yield return null; simT += dtStep; }
            }

            Assert.IsTrue((bool)hpT.GetProperty("IsKnockedDown").GetValue(hp), "HP 0/4 deve disparar KNOCKDOWN.");
            // Aguarda respawn automatico no checkpoint
            float waitRespawn = 0f;
            while ((bool)hpT.GetProperty("IsKnockedDown").GetValue(hp) && waitRespawn < 3.5f)
            {
                Cap();
                yield return null;
                waitRespawn += Time.unscaledDeltaTime;
                simT += dtStep;
            }
            Assert.IsFalse((bool)hpT.GetProperty("IsKnockedDown").GetValue(hp), "Dez deve concluir respawn automaticamente.");
            Assert.AreEqual(4, (int)hpT.GetProperty("CurrentSegments").GetValue(hp), "HP deve restaurar 4/4 apos respawn.");
            Assert.GreaterOrEqual((int)hpT.GetProperty("RespawnCount").GetValue(hp), 1, "RespawnCount deve ser >= 1.");

            for (int k = 0; k < 18; k++) { Cap(); yield return null; simT += dtStep; }

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
            File.WriteAllText(Path.Combine(outDir, "manifestacoes_events.json"), sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[ManifestacoesTest #94] PASSOU: spawned={spawnedCount}, kills={kills}, ecoEvades={hpT.GetProperty("EcoEvadeCount").GetValue(hp)}, knockdowns={hpT.GetProperty("KnockdownCount").GetValue(hp)}, respawns={hpT.GetProperty("RespawnCount").GetValue(hp)}, frames={frame}");
        }
    }
}
