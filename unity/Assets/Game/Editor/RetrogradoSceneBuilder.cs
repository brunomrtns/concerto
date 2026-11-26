#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Combat;
using Studio.Game.Resync;
using Studio.Game.Retrogrado;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi o Fuso Retrogrado — O Deserto do Tempo Reverso (Assets/Game/Scenes/Retrogrado.unity, 400x400m)
    /// com paleta ambar-areia e dunas em espiral de mola, kit modular 8 pecas 100% TripoSG (2 estados),
    /// areia caindo para cima, plataformas de janelas invertidas, Boss O Arquivista-Mor, Aro-Escudo e Ruinas pre-Fratura.
    /// </summary>
    public static class RetrogradoSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Retrogrado.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/retrogrado";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string MatDir    = "Assets/Game/Materials/Retrogrado";

        [MenuItem("Tools/CONCERTO/Build Retrogrado Scene (7 Beats)")]
        public static void BuildRetrogradoMenu()
        {
            BuildRetrogradoInternal();
        }

        public static void BuildRetrogradoFromCommandLine()
        {
            BuildRetrogradoInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildRetrogradoInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "Retrogrado")));
            AssetDatabase.Refresh();

            // Paleta ambar-areia + bronze antigo (2 estados)
            Material matDesync   = EnsureMat($"{MatDir}/Mat_Retrogrado_Desincronizado.mat", new Color(0.82f, 0.56f, 0.20f, 1f), new Color(0.45f, 0.25f, 0.05f, 1f), 0.55f);
            Material matRestored = EnsureMat($"{MatDir}/Mat_Retrogrado_Restaurado.mat",     new Color(0.95f, 0.78f, 0.36f, 1f), new Color(0.60f, 0.42f, 0.12f, 1f), 0.78f);
            Material matSandUp   = EnsureMat($"{MatDir}/Mat_Retrogrado_AreiaReversa.mat",   new Color(1.00f, 0.86f, 0.46f, 1f), new Color(0.85f, 0.62f, 0.16f, 1f), 0.90f);
            Material matRuin     = EnsureMat($"{MatDir}/Mat_Retrogrado_RuinaPreFratura.mat", new Color(0.44f, 0.34f, 0.22f, 1f), new Color(0.22f, 0.68f, 0.74f, 1f), 0.68f);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional_RetrogradoAmberSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.86f, 0.62f);
            sun.intensity = 1.45f;
            sunGo.transform.rotation = Quaternion.Euler(36f, -24f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.24f, 0.16f, 0.08f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 8f, -12f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 20f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            var boundsGo = new GameObject("Retrogrado_Bounds_400x400");
            var boundsBox = boundsGo.AddComponent<BoxCollider>();
            boundsBox.isTrigger = true;
            boundsBox.center = new Vector3(0f, 22f, 150f);
            boundsBox.size = new Vector3(400f, 70f, 400f);

            // Jogador Dez com Locomotion + ParryAndLockOnController (Contratempo + Aro-Escudo!)
            var dezGo = SpawnPrefab($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, 0f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null)
                dezGo.AddComponent<DezLocomotionController>();
            if (dezGo.GetComponent<ParryAndLockOnController>() == null)
                dezGo.AddComponent<ParryAndLockOnController>();

            var retRoot = new GameObject("Retrogrado_Fuso_Controller");
            var retCtrl = retRoot.AddComponent<RetrogradoFusoController>();
            var revSys  = retRoot.AddComponent<RetrogradoReverseTimeSystem>();
            retCtrl.reverseTime = revSys;

            // === BEAT 1 (z = 0..48): Dunas em Espiral de Mola ===
            var b1Root = new GameObject("Beat1_DunasEspiralMola");
            retCtrl.beatCheckpoints[0] = CreateCheckpoint("CP_Beat1_Dunas", b1Root.transform, new Vector3(0f, 0.1f, 0f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B1_SandBasin", b1Root.transform, new Vector3(0f, 0f, 12f), new Vector3(16f, 1f, 18f), matDesync, true);
            PlaceKitPiece("ret_spiral_dune_coil", "B1_SpiralDune_L", b1Root.transform, new Vector3(-12f, 3.5f, 22f), new Vector3(6f, 6f, 6f), matDesync, true);
            PlaceKitPiece("ret_spiral_dune_coil", "B1_SpiralDune_R", b1Root.transform, new Vector3( 12f, 3.5f, 26f), new Vector3(6f, 6f, 6f), matDesync, true);

            var b1ResyncGo = new GameObject("Retrogrado_Entry_ResyncZone");
            b1ResyncGo.transform.SetParent(b1Root.transform, false);
            b1ResyncGo.transform.position = new Vector3(0f, 0f, 28f);
            var b1Resync = b1ResyncGo.AddComponent<ResyncZoneController>();
            b1Resync.zoneId = "retrogrado_b1_dunas";
            b1Resync.bubbleRadius = 24f;
            var node1 = PlaceFabPiece("c5_prop_no_resincronia", "B1_ResyncNode", b1ResyncGo.transform, new Vector3(-4f, 0f, 18f), Vector3.one, matRestored, false);
            b1Resync.nodeAnchor = node1.transform;
            var dualWalk = PlaceFabPiece("caldeiral_env_floor_tile", "B1_DualState_Causeway", b1ResyncGo.transform, new Vector3(0f, 0.2f, 34f), new Vector3(6f, 0.5f, 8f), matDesync, true);
            var dualObj = dualWalk.AddComponent<ResyncDualStateObject>();
            dualObj.mode = DualStateMode.PassableOnlyInRestored;
            dualObj.desyncMaterial = matDesync;
            dualObj.restoredMaterial = matRestored;
            b1Resync.managedObjects.Add(dualObj);

            // === BEAT 2 (z = 48..98): Areia Caindo PARA CIMA + Pegadas a Frente (GATE 1) ===
            var b2Root = new GameObject("Beat2_AreiaParaCima");
            retCtrl.beatCheckpoints[1] = CreateCheckpoint("CP_Beat2_AreiaReversa", b2Root.transform, new Vector3(0f, 0.1f, 50f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B2_DunePlateau", b2Root.transform, new Vector3(0f, 0f, 70f), new Vector3(22f, 1f, 22f), matDesync, true);
            for (int m = 0; m < 6; m++)
            {
                var mote = PlaceFabPiece("caldeiral_env_portal_pillar", $"B2_RisingSandMote_{m + 1}", b2Root.transform, new Vector3(-8f + m * 3.2f, 1.0f + m * 1.4f, 64f + (m % 3) * 6f), new Vector3(0.35f, 0.55f, 0.35f), matSandUp, false);
                revSys.risingSandMotes[m] = mote.transform;
            }
            for (int f = 0; f < 3; f++)
            {
                var fp = PlaceFabPiece("caldeiral_env_floor_tile", $"B2_ForwardFootprint_{f + 1}", b2Root.transform, new Vector3(0f, 0.15f, 58f + f * 4f), new Vector3(0.8f, 0.1f, 1.2f), matSandUp, false);
                revSys.forwardFootprints[f] = fp.transform;
            }

            // === BEAT 3 (z = 98..158): Plataformacao de Janelas INVERTIDAS (GATE 1) ===
            var b3Root = new GameObject("Beat3_JanelasInvertidas");
            retCtrl.beatCheckpoints[2] = CreateCheckpoint("CP_Beat3_JanelasInvertidas", b3Root.transform, new Vector3(0f, 0.1f, 102f));
            for (int p = 0; p < 3; p++)
            {
                var platGo = PlaceKitPiece("ret_reverse_hourglass_plat", $"B3_ReverseWindowPlat_{p + 1}", b3Root.transform, new Vector3((p % 2 == 0) ? -3f : 3f, 2.0f + p * 1.6f, 114f + p * 14f), new Vector3(4.2f, 2.2f, 4.2f), matRestored, true);
                var rwp = platGo.AddComponent<ReverseWindowPlatform>();
                rwp.materializeDurationSec = 2.0f;
                rwp.solidThreshold01 = 0.50f;
                revSys.reversePlatforms.Add(rwp);
            }

            // === BEAT 4 (z = 158..208): Portal da Ampulheta ===
            var b4Root = new GameObject("Beat4_PortalAmpulheta");
            retCtrl.beatCheckpoints[3] = CreateCheckpoint("CP_Beat4_PortalAmpulheta", b4Root.transform, new Vector3(0f, 0.1f, 160f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B4_HourglassBridge", b4Root.transform, new Vector3(0f, 0f, 182f), new Vector3(10f, 1f, 24f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B4_CanyonWall_L", b4Root.transform, new Vector3(-7f, 0f, 182f), new Vector3(1f, 2.5f, 3f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B4_CanyonWall_R", b4Root.transform, new Vector3( 7f, 0f, 182f), new Vector3(1f, 2.5f, 3f), matDesync, true);

            // === BEAT 5 (z = 208..268): Arena Boss O ARQUIVISTA-MOR (GATE 4) ===
            var b5Root = new GameObject("Beat5_ArenaArquivistaMor");
            retCtrl.beatCheckpoints[4] = CreateCheckpoint("CP_Beat5_ArenaBoss", b5Root.transform, new Vector3(0f, 0.1f, 212f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B5_AstrolabeArena", b5Root.transform, new Vector3(0f, 0f, 236f), new Vector3(32f, 1f, 32f), matDesync, true);
            var repairStation = PlaceFabPiece("c5_prop_no_resincronia", "B5_RepairStation_Arquivista", b5Root.transform, new Vector3(0f, 0.1f, 218f), Vector3.one, matRestored, false);

            var bossGo = PlaceKitPiece("ret_hero_arquivista_mor", "Boss_ArquivistaMor", b5Root.transform, new Vector3(0f, 3.0f, 246f), new Vector3(5.0f, 5.0f, 5.0f), matRestored, true);
            var boss = bossGo.AddComponent<RetrogradoBossArquivistaMor>();
            boss.repairStationAnchor = repairStation.transform;
            retCtrl.bossArquivista = boss;

            // === BEAT 6 (z = 268..310): Reliquia ARO -> ARO-ESCUDO (GATE 3) ===
            var b6Root = new GameObject("Beat6_ReliquiaAroEscudo");
            retCtrl.beatCheckpoints[5] = CreateCheckpoint("CP_Beat6_ReliquiaAro", b6Root.transform, new Vector3(0f, 0.1f, 272f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B6_AltarAro", b6Root.transform, new Vector3(0f, 0f, 286f), new Vector3(16f, 1f, 20f), matRestored, true);
            var relicGo = PlaceFabPiece("caldeiral_env_portal_pillar", "Relic_AroEscudo_Pickup", b6Root.transform, new Vector3(0f, 1.2f, 284f), new Vector3(0.85f, 0.85f, 0.85f), matSandUp, false);
            relicGo.SetActive(false);
            retCtrl.relicAroObject = relicGo;

            // === BEAT 7 (z = 310..350): Pos-Conserto — Ruinas Pre-Fratura dos Outros Relogios (GATE 2) ===
            var b7Root = new GameObject("Beat7_RuinasPreFratura");
            retCtrl.beatCheckpoints[6] = CreateCheckpoint("CP_Beat7_RuinasPreFratura", b7Root.transform, new Vector3(0f, 0.1f, 316f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B7_RuinsSanctuaryFloor", b7Root.transform, new Vector3(0f, 0f, 330f), new Vector3(24f, 1f, 24f), matRestored, true);

            var ruinsRoot = new GameObject("B7_PreFractureRuins_OutrosRelogios");
            ruinsRoot.transform.SetParent(b7Root.transform, false);
            PlaceKitPiece("ret_prefracture_ruin_arch", "B7_PreFractureArch_Center", ruinsRoot.transform, new Vector3(0f, 4.5f, 338f), new Vector3(7f, 7f, 5f), matRuin, true);
            PlaceKitPiece("ret_prefracture_ruin_arch", "B7_PreFractureArch_Left",   ruinsRoot.transform, new Vector3(-11f, 3.5f, 332f), new Vector3(5f, 5f, 4f), matRuin, true);
            PlaceKitPiece("ret_prefracture_ruin_arch", "B7_PreFractureArch_Right",  ruinsRoot.transform, new Vector3( 11f, 3.5f, 332f), new Vector3(5f, 5f, 4f), matRuin, true);
            ruinsRoot.SetActive(false);
            retCtrl.preFractureRuinsRoot = ruinsRoot;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RetrogradoSceneBuilder] Cena {ScenePath} construida com 7 beats, kit 8 pecas 100% TripoSG (ambar-areia 2 estados), areia pra cima, janelas invertidas, Arquivista-Mor e Ruinas pre-Fratura!");
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
