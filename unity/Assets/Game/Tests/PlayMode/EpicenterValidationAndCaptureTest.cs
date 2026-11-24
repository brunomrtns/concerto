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
    public sealed class EpicenterValidationAndCaptureTest
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
        public IEnumerator ValidateEpicenterFullLevelStasisPuzzleVoidRespawnWavesAndArenaStub()
        {
            PlayerPrefs.SetInt("CONCERTO_CS03_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_POWERS_UNLOCKED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Epicentro", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var locoT   = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var platT   = GameType("Studio.Game.Epicenter.EpicenterPlatformController");
            var cpT     = GameType("Studio.Game.Epicenter.EpicenterCheckpointAndVoidZone");
            var stubT   = GameType("Studio.Game.Epicenter.EpicenterArenaBossStub");
            var zoneT   = GameType("Studio.Game.Combat.CombatEncounterZone");
            var enemyT  = GameType("Studio.Game.Combat.ManifestationEnemyBase");
            var hitT    = GameType("Studio.Game.Tool.ToolHitInfo");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder obrigatorio em Epicentro.");
            var cc     = dezGo.GetComponent<CharacterController>();
            var loco   = dezGo.GetComponent(locoT);
            var powers = dezGo.GetComponent(powersT);
            var cpCtrl = UnityEngine.Object.FindAnyObjectByType(cpT);
            var stub   = UnityEngine.Object.FindAnyObjectByType(stubT);

            Assert.IsNotNull(cpCtrl, "EpicenterCheckpointAndVoidZone obrigatorio.");
            Assert.IsNotNull(stub, "EpicenterArenaBossStub obrigatorio na Secao 3.");
            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "epicenter_capture");
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

            void MoveDez(Vector3 pos)
            {
                if (cc != null) cc.enabled = false;
                dezGo.transform.position = pos;
                dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                if (cc != null) cc.enabled = true;
            }

            // === SECAO 1: TRAVESSIA + PROVA DO ESTASE + QUEDA NO VAZIO ===
            pMoveDir.SetValue(loco, Vector3.right);
            pRun.SetValue(loco, true);
            for (int i = 0; i < 24; i++) { Cap(); yield return null; simT += dtStep; }

            // Prova 1: Queda no vazio -> respawn limpo no Checkpoint S1!
            MoveDez(new Vector3(5f, -9.0f, 0f));
            yield return null;
            yield return null;
            int voidRespawns = (int)cpT.GetProperty("VoidRespawnCount").GetValue(cpCtrl);
            Assert.GreaterOrEqual(voidRespawns, 1, "Queda no vazio deve acionar respawn limpo no checkpoint!");
            for (int i = 0; i < 15; i++) { Cap(); yield return null; simT += dtStep; }

            // Prova 2: Puzzle do Estase na S1_FastStasisPuzzlePlatform!
            var fastPlatGo = GameObject.Find("S1_FastStasisPuzzlePlatform");
            Assert.IsNotNull(fastPlatGo, "S1_FastStasisPuzzlePlatform obrigatoria.");
            var fastPlat = fastPlatGo.GetComponent(platT);
            MoveDez(new Vector3(30f, 1.0f, 0f));
            for (int i = 0; i < 18; i++) { Cap(); yield return null; simT += dtStep; }

            powersT.GetMethod("TriggerEstase").Invoke(powers, null);
            LogEv(simT, "estase_puzzle", "SFX/Powers/sfx_estase_pulse_TEMP.wav");
            yield return null;
            Assert.IsTrue((bool)platT.GetProperty("IsSlowedByStasis").GetValue(fastPlat), "Plataforma rapida deve desacelerar 50% com Estase!");
            for (int i = 0; i < 24; i++) { Cap(); yield return null; simT += dtStep; }

            // === SECAO 2: COMBATE (2 WAVES CURTAS) ===
            var zones = UnityEngine.Object.FindObjectsByType(zoneT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(zones.Length, 2, "Secao 2 deve ter 2 waves config-driven.");
            int clearedWaves = 0;
            for (int zIdx = 0; zIdx < zones.Length; zIdx++)
            {
                var zComp = zones[zIdx] as Component;
                MoveDez(zComp.transform.position + new Vector3(-4.5f, 0.2f, 0f));
                zoneT.GetMethod("TriggerEncounter").Invoke(zComp, null);
                yield return null;
                for (int i = 0; i < 18; i++) { Cap(); yield return null; simT += dtStep; }

                var enemies = UnityEngine.Object.FindObjectsByType(enemyT, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var eObj in enemies)
                {
                    var eComp = eObj as Component;
                    if (eComp == null) continue;
                    var hitInfo = Activator.CreateInstance(hitT);
                    hitT.GetField("Damage").SetValue(hitInfo, 60f);
                    hitT.GetField("IsHeavy").SetValue(hitInfo, true);
                    hitT.GetField("KnockbackDir").SetValue(hitInfo, Vector3.right);
                    enemyT.GetMethod("ApplyDamage").Invoke(eComp, new object[] { hitInfo });
                    LogEv(simT, "wave_kill", "SFX/Tool/tool_melee_hit_TEMP.wav");
                    for (int k = 0; k < 10; k++) { Cap(); yield return null; simT += dtStep; }
                }
                if ((bool)zoneT.GetProperty("IsCleared").GetValue(zComp)) clearedWaves++;
            }
            Assert.GreaterOrEqual(clearedWaves, 2, "Ambas as waves da Secao 2 devem ser concluidas.");

            // === SECAO 3: CORACAO DA FENDA — ARENA FINAL + TRIGGER STUB ===
            MoveDez(new Vector3(114f, 1.1f, 0f));
            pMoveDir.SetValue(loco, Vector3.right);
            for (int i = 0; i < 20; i++) { Cap(); yield return null; simT += dtStep; }
            Assert.IsTrue((bool)stubT.GetProperty("IsTriggered").GetValue(stub), "Trigger stub do Coracao da Fenda deve disparar na Arena Final.");
            LogEv(simT, "boss_omen", "SFX/Epicenter/sfx_epicenter_boss_omen_TEMP.wav");
            for (int i = 0; i < 24; i++) { Cap(); yield return null; simT += dtStep; }

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
            File.WriteAllText(Path.Combine(outDir, "epicenter_events.json"), sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[EpicenterTest #102] PASSOU: voidRespawns={voidRespawns}, stasisPuzzle=True, wavesCleared={clearedWaves}, arenaStub=True, frames={frame}");
        }
    }
}
