using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Suite dedicada de verificacao dos 9 fixes de pos-review (#93/#94) + captura ANTES/DEPOIS do FIX 1.
    /// </summary>
    public sealed class ReviewFix9394ValidationAndCaptureTest
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
            File.WriteAllBytes(filePath, tex.EncodeToJPG(90));
        }

        [UnityTest]
        public IEnumerator ValidateAllNinePostReviewFixesAndCaptureFix1BeforeAfter()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T      = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var regT       = GameType("Studio.Game.Animation.PlayerRegistry");
            var saveSysT   = GameType("Studio.Game.Save.SaveSystem");
            var saveDataT  = GameType("Studio.Game.Save.SaveGameData");
            var powersT    = GameType("Studio.Game.Powers.DezPowersController");
            var toolT      = GameType("Studio.Game.Tool.MultiToolController");
            var combatModT = GameType("Studio.Game.Tool.CombatToolModule");
            var hitT       = GameType("Studio.Game.Tool.ToolHitInfo");
            var hpT        = GameType("Studio.Game.Combat.PlayerHealthController");
            var zoneT      = GameType("Studio.Game.Combat.CombatEncounterZone");
            var enemyT     = GameType("Studio.Game.Combat.ManifestationEnemyBase");
            var flutuanteT = GameType("Studio.Game.Combat.FlutuanteEnemy");
            var layersT    = GameType("Studio.Game.Collision.CollisionMatrixBootstrap");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            // =====================================================================
            // FIX 4: PlayerRegistry sem GameObject.Find("Player_Dez_Placeholder")
            // =====================================================================
            var dezGo = regT.GetProperty("CurrentGameObject").GetValue(null) as GameObject;
            Assert.IsNotNull(dezGo, "FIX 4: PlayerRegistry.CurrentGameObject deve estar registrado.");
            Assert.AreSame(dezGo, regT.GetMethod("Resolve").Invoke(null, null), "FIX 4: PlayerRegistry.Resolve() deve retornar o Dez registrado.");

            var powers = dezGo.GetComponent(powersT);
            var hp     = dezGo.GetComponent(hpT);
            var tool   = UnityEngine.Object.FindAnyObjectByType(toolT);
            var zone   = UnityEngine.Object.FindAnyObjectByType(zoneT) as Component;
            Assert.IsNotNull(powers, "DezPowersController obrigatorio.");
            Assert.IsNotNull(hp, "PlayerHealthController obrigatorio.");
            Assert.IsNotNull(tool, "MultiToolController obrigatorio.");
            Assert.IsNotNull(zone, "CombatEncounterZone obrigatoria em Caldeiral.");

            // =====================================================================
            // FIX 2: Checkpoints reais registrados e persistidos via SaveSystem
            // =====================================================================
            var saveVecT = GameType("Studio.Game.Save.SaveVec3");
            var curSaveData = saveSysT.GetProperty("CachedState").GetValue(null);
            Assert.IsNotNull(curSaveData, "FIX 2: SaveSystem.CachedState deve existir apos Start().");
            string initialCpId = (string)saveDataT.GetField("currentCheckpointId").GetValue(curSaveData);
            Assert.IsFalse(string.IsNullOrEmpty(initialCpId), "FIX 2: Checkpoint inicial canonico deve ser registrado em Start().");

            Vector3 customCp = dezGo.transform.position + new Vector3(1.25f, 0f, -0.75f);
            hpT.GetMethod("SetCheckpoint").Invoke(hp, new object[] { customCp });
            curSaveData = saveSysT.GetProperty("CachedState").GetValue(null);
            var savedCpBox = saveDataT.GetField("checkpointPosition").GetValue(curSaveData);
            Vector3 savedCp = (Vector3)saveVecT.GetMethod("ToVector3").Invoke(savedCpBox, null);
            Assert.Less(Vector3.Distance(customCp, savedCp), 0.05f, "FIX 2: SetCheckpoint deve persistir checkpointPosition no SaveSystem.");

            // =====================================================================
            // FIX 3 (GATE 4): Progressao fora de PlayerPrefs -> JSON versionado v2
            // =====================================================================
            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
            Assert.IsTrue((bool)powersT.GetProperty("IsUnlocked").GetValue(powers), "FIX 3: Poderes devem estar desbloqueados.");

            // Limpa TODO PlayerPrefs, reseta estado em memoria e simula fechar/reabrir (TryLoad + ApplyLoadedStateToCurrentScene)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            powersT.GetMethod("RestoreFromSave").Invoke(powers, new object[] { false });
            Assert.IsFalse((bool)powersT.GetProperty("IsUnlocked").GetValue(powers), "FIX 3: Estado em memoria resetado antes do reload.");

            var tryLoadArgs = new object[] { null };
            bool loadedOk = (bool)saveSysT.GetMethod("TryLoad").Invoke(null, tryLoadArgs);
            Assert.IsTrue(loadedOk, "FIX 3: SaveSystem.TryLoad deve carregar JSON v2 com sucesso.");
            var loadedData = tryLoadArgs[0];
            Assert.AreEqual(2, (int)saveDataT.GetField("schemaVersion").GetValue(loadedData), "FIX 3: schemaVersion deve ser 2.");
            Assert.IsTrue((bool)saveDataT.GetField("powersUnlocked").GetValue(loadedData), "FIX 3: powersUnlocked deve persistir no JSON v2.");

            saveSysT.GetMethod("ApplyLoadedStateToCurrentScene").Invoke(null, new object[] { loadedData });
            Assert.IsTrue((bool)powersT.GetProperty("IsUnlocked").GetValue(powers), "FIX 3: Poderes devem permanecer desbloqueados apos limpar PlayerPrefs e recarregar JSON.");
            Assert.IsFalse(PlayerPrefs.HasKey("CONCERTO_POWERS_UNLOCKED"), "FIX 3: Chave legada CONCERTO_POWERS_UNLOCKED nao deve existir em PlayerPrefs.");
            Assert.IsFalse(PlayerPrefs.HasKey("CONCERTO_CS03_PLAYED"), "FIX 3: Chave legada CONCERTO_CS03_PLAYED nao deve existir em PlayerPrefs.");

            // Prova tambem migracao automatica v1 -> v2 (lendo legado de PlayerPrefs e limpando em seguida)
            string savePath = (string)saveSysT.GetProperty("SaveFilePath").GetValue(null);
            PlayerPrefs.SetInt("CONCERTO_POWERS_UNLOCKED", 1);
            PlayerPrefs.SetInt("CONCERTO_CS03_PLAYED", 1);
            PlayerPrefs.Save();
            File.WriteAllText(savePath, "{\"schemaVersion\":1,\"sceneName\":\"Caldeiral\",\"powersUnlocked\":false,\"cs03Played\":false}");
            var migArgs = new object[] { null };
            bool migOk = (bool)saveSysT.GetMethod("TryLoad").Invoke(null, migArgs);
            Assert.IsTrue(migOk, "FIX 3: Migracao v1->v2 deve suceder.");
            Assert.AreEqual(2, (int)saveDataT.GetField("schemaVersion").GetValue(migArgs[0]), "FIX 3: Save v1 deve migrar para schemaVersion=2.");
            Assert.IsTrue((bool)saveDataT.GetField("powersUnlocked").GetValue(migArgs[0]), "FIX 3: Migracao v1->v2 deve herdar CONCERTO_POWERS_UNLOCKED.");
            Assert.IsTrue((bool)saveDataT.GetField("cs03Played").GetValue(migArgs[0]), "FIX 3: Migracao v1->v2 deve herdar CONCERTO_CS03_PLAYED.");
            Assert.IsFalse(PlayerPrefs.HasKey("CONCERTO_POWERS_UNLOCKED"), "FIX 3: Migracao deve apagar CONCERTO_POWERS_UNLOCKED de PlayerPrefs.");
            Assert.IsFalse(PlayerPrefs.HasKey("CONCERTO_CS03_PLAYED"), "FIX 3: Migracao deve apagar CONCERTO_CS03_PLAYED de PlayerPrefs.");
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            // =====================================================================
            // FIX 1 (GATE 2 MP4 ANTES/DEPOIS) + FIX 5 (Swing real) + FIX 9 (MPB)
            // =====================================================================
            var estilhacoPrefab = zoneT.GetField("estilhacoPrefab").GetValue(zone) as GameObject;
            var flutuantePrefab = zoneT.GetField("flutuantePrefab").GetValue(zone) as GameObject;
            var balCfg = zoneT.GetField("balanceConfig").GetValue(zone);
            Assert.IsNotNull(estilhacoPrefab, "estilhacoPrefab obrigatorio.");
            Assert.IsNotNull(flutuantePrefab, "flutuantePrefab obrigatorio.");

            toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, false });
            var combatMod = toolT.GetProperty("ActiveModule").GetValue(tool);
            var mHeavyStrike = combatModT.GetMethod("ExecuteHeavyStrike");

            var cc = dezGo.GetComponent<CharacterController>();
            Vector3 arenaOrigin = zone.transform.position;
            Vector3 enemySpot = arenaOrigin + new Vector3(0.0f, 0.05f, 0.0f);
            Vector3 dezSpot   = enemySpot + new Vector3(-1.15f, 0.0f, 0.0f);

            // Camera dedicada fixa (exato mesmo angulo/rota no ANTES e DEPOIS!)
            var camGo = new GameObject("ReviewFix1_CompareCam");
            var capCam = camGo.AddComponent<Camera>();
            capCam.fieldOfView = 42f;
            capCam.nearClipPlane = 0.1f;
            capCam.farClipPlane = 120f;
            camGo.transform.position = enemySpot + new Vector3(-0.35f, 2.05f, -4.15f);
            camGo.transform.LookAt(enemySpot + new Vector3(0f, 0.85f, 0f));

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "review_fix_93_94_capture");
            Directory.CreateDirectory(outDir);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var legacyBugProp = enemyT.GetProperty("SimulateLegacyMagentaParticleBug", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(legacyBugProp, "SimulateLegacyMagentaParticleBug deve existir para captura ANTES/DEPOIS.");

            // --- ANTES (magenta sem shader URP) ---
            legacyBugProp.SetValue(null, true);
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = dezSpot;
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            var beforeEnemyGo = UnityEngine.Object.Instantiate(estilhacoPrefab, enemySpot, Quaternion.LookRotation(Vector3.left, Vector3.up));
            var beforeEnemy = beforeEnemyGo.GetComponent(enemyT);
            if (balCfg != null) enemyT.GetField("config").SetValue(beforeEnemy, balCfg);
            yield return null;

            for (int f = 0; f < 8; f++)
            {
                CaptureFrameJpg(capCam, rt, tex, Path.Combine(outDir, $"before_{f:D4}.jpg"));
                yield return null;
            }
            bool beforeSwung = (bool)mHeavyStrike.Invoke(combatMod, null);
            Assert.IsTrue(beforeSwung, "FIX 5: Swing real ANTES deve disparar.");
            for (int f = 8; f < 36; f++)
            {
                CaptureFrameJpg(capCam, rt, tex, Path.Combine(outDir, $"before_{f:D4}.jpg"));
                yield return null;
            }
            if (beforeEnemyGo != null) UnityEngine.Object.Destroy(beforeEnemyGo);
            var oldVfx = GameObject.Find("GlitchDissolve_VFX");
            if (oldVfx != null) UnityEngine.Object.DestroyImmediate(oldVfx);
            yield return new WaitForSecondsRealtime(0.55f);

            // --- DEPOIS (URP/Particles/Unlit ciano cacheado) ---
            legacyBugProp.SetValue(null, false);
            if (cc != null) cc.enabled = false;
            dezGo.transform.position = dezSpot;
            dezGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            var afterEnemyGo = UnityEngine.Object.Instantiate(estilhacoPrefab, enemySpot, Quaternion.LookRotation(Vector3.left, Vector3.up));
            var afterEnemy = afterEnemyGo.GetComponent(enemyT);
            if (balCfg != null) enemyT.GetField("config").SetValue(afterEnemy, balCfg);
            yield return null;

            for (int f = 0; f < 8; f++)
            {
                CaptureFrameJpg(capCam, rt, tex, Path.Combine(outDir, $"after_{f:D4}.jpg"));
                yield return null;
            }
            bool afterSwung = (bool)mHeavyStrike.Invoke(combatMod, null);
            Assert.IsTrue(afterSwung, "FIX 5: Swing real DEPOIS deve disparar.");
            yield return null;

            // Verifica material real das particulas de dissolve (DEPOIS)
            var dissolveGo = GameObject.Find("GlitchDissolve_VFX");
            Assert.IsNotNull(dissolveGo, "FIX 1: GlitchDissolve_VFX deve ser criado na morte do inimigo.");
            var psr = dissolveGo.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(psr, "FIX 1: ParticleSystemRenderer obrigatorio.");
            Assert.IsNotNull(psr.sharedMaterial, "FIX 1: ParticleSystemRenderer.sharedMaterial nao pode ser null (magenta).");
            Assert.IsNotNull(psr.sharedMaterial.shader, "FIX 1: Shader da particula nao pode ser null.");
            StringAssert.DoesNotContain("Error", psr.sharedMaterial.shader.name, "FIX 1: Shader nao pode ser Hidden/InternalErrorShader.");

            for (int f = 8; f < 36; f++)
            {
                CaptureFrameJpg(capCam, rt, tex, Path.Combine(outDir, $"after_{f:D4}.jpg"));
                yield return null;
            }
            if (afterEnemyGo != null) UnityEngine.Object.Destroy(afterEnemyGo);

            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(camGo);

            // =====================================================================
            // FIX 7: Flutuante Line-of-Sight bloqueado por parede (LayerEnvironment)
            // =====================================================================
            int envLayer = (int)layersT.GetField("LayerEnvironment").GetValue(null);
            Vector3 flutuantePos = dezGo.transform.position + new Vector3(6.0f, 1.5f, 0f);
            var flutGo = UnityEngine.Object.Instantiate(flutuantePrefab, flutuantePos, Quaternion.identity);
            var flutComp = flutGo.GetComponent(flutuanteT);
            yield return null;

            var mHasLos = flutuanteT.GetMethod("HasLineOfSightToPlayer");
            Assert.IsTrue((bool)mHasLos.Invoke(flutComp, null), "FIX 7: Sem parede entre Flutuante e Dez, LOS deve ser true.");

            var wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallGo.name = "Test_LOS_Wall";
            wallGo.layer = envLayer;
            wallGo.transform.position = dezGo.transform.position + new Vector3(3.0f, 1.4f, 0f);
            wallGo.transform.localScale = new Vector3(0.8f, 4.0f, 4.0f);
            Physics.SyncTransforms();

            Assert.IsFalse((bool)mHasLos.Invoke(flutComp, null), "FIX 7: Com parede LayerEnvironment entre Flutuante e Dez, LOS deve ser false.");
            UnityEngine.Object.Destroy(flutGo);

            // =====================================================================
            // FIX 8 (Knockback wall-safe) + FIX 9 (Zero-alloc MaterialPropertyBlock)
            // =====================================================================
            Vector3 wallSurfaceX = wallGo.transform.position + new Vector3(-0.75f, -1.35f, 0f);
            var kbEnemyGo = UnityEngine.Object.Instantiate(estilhacoPrefab, wallSurfaceX, Quaternion.identity);
            var kbEnemy = kbEnemyGo.GetComponent(enemyT);
            yield return null;

            var firstRend = kbEnemyGo.GetComponentInChildren<Renderer>();
            Material originalSharedMat = firstRend != null ? firstRend.sharedMaterial : null;

            var lightHit = Activator.CreateInstance(hitT);
            hitT.GetField("Damage").SetValue(lightHit, 5f);
            hitT.GetField("IsHeavy").SetValue(lightHit, true);
            hitT.GetField("KnockbackDir").SetValue(lightHit, Vector3.right); // em direcao a parede!
            enemyT.GetMethod("ApplyDamage").Invoke(kbEnemy, new object[] { lightHit });
            yield return null;

            // FIX 8: inimigo nao atravessou a parede (wallGo.x == dez.x + 3.0f, halfWidth 0.4f -> parede inicia em dez.x + 2.6f)
            Assert.Less(kbEnemyGo.transform.position.x, wallGo.transform.position.x - 0.35f,
                "FIX 8: Knockback nao pode teleportar inimigo para dentro/alem da parede.");

            // FIX 9: renderer manteve exatamente o mesmo sharedMaterial (zero alocacao de clone .material)
            if (firstRend != null && originalSharedMat != null)
            {
                Assert.AreSame(originalSharedMat, firstRend.sharedMaterial,
                    "FIX 9: Hit flash deve usar MaterialPropertyBlock e nunca clonar Renderer.material.");
            }

            UnityEngine.Object.Destroy(kbEnemyGo);
            UnityEngine.Object.Destroy(wallGo);

            // =====================================================================
            // FIX 6: Eco Temporal e Ferramenta bloqueados durante Knockdown
            // =====================================================================
            int guard = 0;
            while (!(bool)hpT.GetProperty("IsKnockedDown").GetValue(hp) && guard < 6)
            {
                guard++;
                float w = 0f;
                while ((bool)hpT.GetProperty("IsInvulnerable").GetValue(hp) && w < 1.2f)
                {
                    yield return null;
                    w += Time.unscaledDeltaTime;
                }
                hpT.GetMethod("TryTakeDamage").Invoke(hp, new object[] { 1, dezGo.transform.position + Vector3.forward });
            }
            Assert.IsTrue((bool)hpT.GetProperty("IsKnockedDown").GetValue(hp), "FIX 6: Dez deve estar em Knockdown.");
            Assert.IsFalse((bool)powersT.GetMethod("CanUsePowersNow").Invoke(powers, null), "FIX 6: CanUsePowersNow() deve ser false em Knockdown.");
            Assert.IsFalse((bool)powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null), "FIX 6: Eco Temporal deve ser bloqueado em Knockdown.");
            Assert.IsFalse((bool)toolT.GetMethod("CanUseCombatActionsNow").Invoke(tool, null), "FIX 6: CanUseCombatActionsNow() deve ser false em Knockdown.");
            Assert.IsFalse((bool)combatModT.GetMethod("ExecuteLightComboStep").Invoke(combatMod, null), "FIX 6: Ataque leve deve ser bloqueado em Knockdown.");
            Assert.IsFalse((bool)mHeavyStrike.Invoke(combatMod, null), "FIX 6: Ataque pesado deve ser bloqueado em Knockdown.");

            Debug.Log("[ReviewFix9394Test] TODOS OS 9 FIXES VALIDADOS COM SUCESSO!");
        }
    }
}
