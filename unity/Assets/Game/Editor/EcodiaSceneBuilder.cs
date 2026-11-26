#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Ecodia;
using Studio.Game.Powers;
using Studio.Game.Resync;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi o Fuso Ecodia — A Cidade que Repete 6 Segundos (Assets/Game/Scenes/Ecodia.unity, 400x400m)
    /// com estetica Art Deco relojoeiro (azul-madrugada + neons quentes), kit modular 8 pecas 100% TripoSG
    /// (4 novas em Assets/Game/Art/Generated/ecodia/ + 4 modulares TripoSG em Assets/Game/Prefabs/Fab3D/),
    /// 2 variantes de material (Desincronizado / Restaurado), loop de 6s, Eco Dez, Micra e Censores.
    /// </summary>
    public static class EcodiaSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Ecodia.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/ecodia";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string MatDir    = "Assets/Game/Materials/Ecodia";

        [MenuItem("Tools/CONCERTO/Build Ecodia Scene (7 Beats)")]
        public static void BuildEcodiaMenu()
        {
            BuildEcodiaInternal();
        }

        public static void BuildEcodiaFromCommandLine()
        {
            BuildEcodiaInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildEcodiaInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "Ecodia")));
            AssetDatabase.Refresh();

            // Paleta Art Deco relojoeiro: azul-madrugada + neons quentes (2 estados)
            Material matDesync   = EnsureMat($"{MatDir}/Mat_Ecodia_Desincronizado.mat", new Color(0.10f, 0.16f, 0.34f, 1f), new Color(0.06f, 0.14f, 0.32f, 1f), 0.72f);
            Material matRestored = EnsureMat($"{MatDir}/Mat_Ecodia_Restaurado.mat",     new Color(0.84f, 0.62f, 0.24f, 1f), new Color(0.60f, 0.34f, 0.08f, 1f), 0.76f);
            Material matNeonWarm = EnsureMat($"{MatDir}/Mat_Ecodia_NeonQuente.mat",     new Color(1.00f, 0.50f, 0.18f, 1f), new Color(0.95f, 0.42f, 0.10f, 1f), 0.88f);
            Material matCensor   = EnsureMat($"{MatDir}/Mat_Ecodia_Censor.mat",         new Color(0.82f, 0.18f, 0.24f, 1f), new Color(0.55f, 0.08f, 0.12f, 1f), 0.82f);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Iluminacao Art Deco azul-madrugada + neons quentes
            var sunGo = new GameObject("Directional_EcodiaMidnightLight");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.68f, 0.96f);
            sun.intensity = 1.15f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -42f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.07f, 0.16f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 8f, -12f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 20f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            var boundsGo = new GameObject("Ecodia_Bounds_400x400");
            var boundsBox = boundsGo.AddComponent<BoxCollider>();
            boundsBox.isTrigger = true;
            boundsBox.center = new Vector3(0f, 22f, 150f);
            boundsBox.size = new Vector3(400f, 70f, 400f);

            // Jogador Dez com Locomotion + DezPowersController (Eco + Estase + Micra!)
            var dezGo = SpawnPrefab($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, 0f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null)
                dezGo.AddComponent<DezLocomotionController>();
            if (dezGo.GetComponent<DezPowersController>() == null)
                dezGo.AddComponent<DezPowersController>();

            var ecoRoot = new GameObject("Ecodia_Fuso_Controller");
            var ecoCtrl = ecoRoot.AddComponent<EcodiaFusoController>();
            var loopSys = ecoRoot.AddComponent<EcodiaTimeLoopSystem>();
            ecoCtrl.timeLoop = loopSys;

            // === BEAT 1 (z = 0..46): Estacao dos 6 Segundos ===
            var b1Root = new GameObject("Beat1_Estacao6Segundos");
            ecoCtrl.beatCheckpoints[0] = CreateCheckpoint("CP_Beat1_Estacao6s", b1Root.transform, new Vector3(0f, 0.1f, 0f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B1_Boulevard_Entry", b1Root.transform, new Vector3(0f, 0f, 10f), new Vector3(14f, 1f, 16f), matDesync, true);
            PlaceKitPiece("eco_artdeco_facade", "B1_Facade_L", b1Root.transform, new Vector3(-12f, 5f, 20f), new Vector3(5f, 7f, 5f), matDesync, true);
            PlaceKitPiece("eco_artdeco_facade", "B1_Facade_R", b1Root.transform, new Vector3( 12f, 5f, 20f), new Vector3(5f, 7f, 5f), matDesync, true);

            var b1ResyncGo = new GameObject("Ecodia_Entry_ResyncZone");
            b1ResyncGo.transform.SetParent(b1Root.transform, false);
            b1ResyncGo.transform.position = new Vector3(0f, 0f, 26f);
            var b1Resync = b1ResyncGo.AddComponent<ResyncZoneController>();
            b1Resync.zoneId = "ecodia_b1_estacao";
            b1Resync.bubbleRadius = 24f;
            var node1 = PlaceFabPiece("c5_prop_no_resincronia", "B1_ResyncNode", b1ResyncGo.transform, new Vector3(-4f, 0f, 18f), Vector3.one, matNeonWarm, false);
            b1Resync.nodeAnchor = node1.transform;
            var dualWalk = PlaceFabPiece("caldeiral_env_floor_tile", "B1_ArtDeco_DualDeck", b1ResyncGo.transform, new Vector3(0f, 0.2f, 34f), new Vector3(6f, 0.5f, 8f), matDesync, true);
            var dualObj = dualWalk.AddComponent<ResyncDualStateObject>();
            dualObj.mode = DualStateMode.PassableOnlyInRestored;
            dualObj.desyncMaterial = matDesync;
            dualObj.restoredMaterial = matRestored;
            b1Resync.managedObjects.Add(dualObj);

            // === BEAT 2 (z = 46..104): Bonde em Loop de 6s + Porta Relojoeira no t=2s (GATE 1) ===
            var b2Root = new GameObject("Beat2_BondeEPorta2s");
            ecoCtrl.beatCheckpoints[1] = CreateCheckpoint("CP_Beat2_BondePorta", b2Root.transform, new Vector3(0f, 0.1f, 48f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B2_RailPlaza", b2Root.transform, new Vector3(0f, 0f, 68f), new Vector3(24f, 1f, 22f), matDesync, true);
            var tram = PlaceKitPiece("eco_artdeco_tram", "B2_LoopTram_6s", b2Root.transform, new Vector3(-18f, 1.4f, 64f), new Vector3(4.5f, 3.0f, 3.0f), matNeonWarm, true);
            loopSys.tramTransform = tram.transform;
            loopSys.tramStartPos = new Vector3(-18f, 1.4f, 64f);
            loopSys.tramEndPos   = new Vector3( 18f, 1.4f, 64f);

            var doorArch = PlaceKitPiece("eco_clockwork_door_arch", "B2_TimedDoor_OpenAt2s", b2Root.transform, new Vector3(0f, 2.2f, 88f), new Vector3(5.5f, 5.5f, 2.2f), matRestored, true);
            loopSys.timedDoorTransform = doorArch.transform;
            loopSys.timedDoorClosedPos = new Vector3(0f, 2.2f, 88f);
            loopSys.timedDoorOpenPos   = new Vector3(0f, 6.2f, 88f);

            // === BEAT 3 (z = 104..156): Praca do Anuncio Neon + 3 Moradores (Quests de Memoria — GATE 2) ===
            var b3Root = new GameObject("Beat3_PracaAnuncioMoradores");
            ecoCtrl.beatCheckpoints[2] = CreateCheckpoint("CP_Beat3_PracaNeon", b3Root.transform, new Vector3(0f, 0.1f, 108f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B3_NeonPlaza", b3Root.transform, new Vector3(0f, 0f, 128f), new Vector3(26f, 1f, 26f), matDesync, true);
            PlaceKitPiece("eco_artdeco_facade", "B3_NeonBillboardTower", b3Root.transform, new Vector3(0f, 6f, 146f), new Vector3(7f, 8f, 4f), matNeonWarm, true);

            string[] npcPrefabs = { "npc_analista_luma_humanoid", "npc_juno_humanoid", "npc_engenheira_vera_humanoid" };
            for (int i = 0; i < 3; i++)
            {
                var npc = SpawnPrefab($"{FabDir}/{npcPrefabs[i]}.prefab", $"B3_Resident_MemoryQuest_{i + 1}", b3Root.transform, new Vector3(-7f + i * 7f, 0.1f, 124f + (i % 2) * 6f), Vector3.one);
                loopSys.loopingNpcTransforms[i] = npc.transform;
            }

            // 2 Censores que passam a patrulhar em tempo real apos o conserto (GATE 2)
            for (int c = 0; c < 2; c++)
            {
                var censor = PlaceFabPiece("caldeiral_env_portal_pillar", $"B3_CensorPatrol_{c + 1}", b3Root.transform, new Vector3(-6f + c * 12f, 1.5f, 134f + c * 6f), new Vector3(1.1f, 1.8f, 1.1f), matCensor, true);
                ecoCtrl.censorTransforms[c] = censor.transform;
            }

            // === BEAT 4 (z = 156..206): Torre Espelho Art Deco ===
            var b4Root = new GameObject("Beat4_TorreEspelho");
            ecoCtrl.beatCheckpoints[3] = CreateCheckpoint("CP_Beat4_TorreEspelho", b4Root.transform, new Vector3(0f, 0.1f, 160f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B4_MirrorBridge", b4Root.transform, new Vector3(0f, 0f, 180f), new Vector3(10f, 1f, 24f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B4_DecoWall_L", b4Root.transform, new Vector3(-7f, 0f, 180f), new Vector3(1f, 2.5f, 3f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B4_DecoWall_R", b4Root.transform, new Vector3( 7f, 0f, 180f), new Vector3(1f, 2.5f, 3f), matDesync, true);

            // === BEAT 5 (z = 206..268): Arena Boss ECO DEZ (GATE 4) ===
            var b5Root = new GameObject("Beat5_ArenaBossEcoDez");
            ecoCtrl.beatCheckpoints[4] = CreateCheckpoint("CP_Beat5_ArenaEcoDez", b5Root.transform, new Vector3(0f, 0.1f, 212f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B5_ArenaClockDial", b5Root.transform, new Vector3(0f, 0f, 236f), new Vector3(30f, 1f, 30f), matDesync, true);

            var repairStation = PlaceFabPiece("c5_prop_no_resincronia", "B5_RepairStation_EcoDez", b5Root.transform, new Vector3(0f, 0.1f, 218f), Vector3.one, matRestored, false);
            var ecoDezGo = PlaceKitPiece("eco_hero_boss_ecodez", "Boss_EcoDez", b5Root.transform, new Vector3(0f, 2.5f, 244f), new Vector3(4.2f, 4.2f, 4.2f), matNeonWarm, true);
            var boss = ecoDezGo.AddComponent<EcodiaBossEcoDez>();
            boss.repairStationAnchor = repairStation.transform;
            ecoCtrl.bossEcoDez = boss;

            // === BEAT 6 (z = 268..310): Reliquia PONTEIRO MENOR -> MICRA (GATE 3) ===
            var b6Root = new GameObject("Beat6_ReliquiaPonteiroMenor");
            ecoCtrl.beatCheckpoints[5] = CreateCheckpoint("CP_Beat6_ReliquiaMicra", b6Root.transform, new Vector3(0f, 0.1f, 272f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B6_AltarMicra", b6Root.transform, new Vector3(0f, 0f, 286f), new Vector3(16f, 1f, 20f), matRestored, true);
            var relicGo = PlaceFabPiece("caldeiral_env_portal_pillar", "Relic_PonteiroMenor_Pickup", b6Root.transform, new Vector3(0f, 1.2f, 284f), new Vector3(0.8f, 0.8f, 0.8f), matNeonWarm, false);
            relicGo.SetActive(false);
            ecoCtrl.relicPonteiroMenorObject = relicGo;

            // === BEAT 7 (z = 310..350): Cidade Acordada + Censores em Tempo Real (GATE 2) ===
            var b7Root = new GameObject("Beat7_CidadeAcordadaCensores");
            ecoCtrl.beatCheckpoints[6] = CreateCheckpoint("CP_Beat7_CidadeAcordada", b7Root.transform, new Vector3(0f, 0.1f, 316f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B7_AwakeDistrictPlaza", b7Root.transform, new Vector3(0f, 0f, 330f), new Vector3(24f, 1f, 24f), matRestored, true);
            PlaceKitPiece("eco_artdeco_facade", "B7_RestoredClocktower", b7Root.transform, new Vector3(0f, 7f, 344f), new Vector3(6f, 9f, 5f), matRestored, true);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EcodiaSceneBuilder] Cena {ScenePath} construida com 7 beats, kit 8 pecas 100% TripoSG (Art Deco 2 estados), Loop 6s, Eco Dez e Micra!");
        }

        private static Transform CreateCheckpoint(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static GameObject PlaceKitPiece(string fbxName, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            var go = SpawnPrefab($"{KitDir}/{fbxName}.fbx", objName, parent, pos, scale);
            ApplyMaterialAndCollider(go, mat, addBoxCollider);
            return go;
        }

        private static GameObject PlaceFabPiece(string prefabName, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            var go = SpawnPrefab($"{FabDir}/{prefabName}.prefab", objName, parent, pos, scale);
            ApplyMaterialAndCollider(go, mat, addBoxCollider);
            return go;
        }

        private static void ApplyMaterialAndCollider(GameObject go, Material mat, bool addBoxCollider)
        {
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            if (addBoxCollider && go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        private static GameObject SpawnPrefab(string assetPath, string objName, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject(objName);
            go.name = objName;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static Material EnsureMat(string path, Color baseColor, Color emission, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == scenePath))
            {
                list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }
    }
}
#endif
