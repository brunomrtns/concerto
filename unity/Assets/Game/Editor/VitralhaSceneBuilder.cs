#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Resync;
using Studio.Game.Vitralha;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi o Fuso Vitralha — Floresta de Vidro (Assets/Game/Scenes/Vitralha.unity, area 400x400m)
    /// usando exclusivamente o kit modular de 8 pecas TripoSG em Assets/Game/Art/Generated/vitralha/,
    /// cada peca com 2 variantes de material (Desincronizado azul-verde frio / Restaurado ambar-primavera quente),
    /// encaixadas em grid de 2m, com todos os 7 beats do GDD 05 §4.
    /// </summary>
    public static class VitralhaSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Vitralha.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/vitralha";
        private const string MatDir    = "Assets/Game/Materials/Vitralha";

        [MenuItem("Tools/CONCERTO/Build Vitralha Scene (7 Beats)")]
        public static void BuildVitralhaMenu()
        {
            BuildVitralhaInternal();
        }

        public static void BuildVitralhaFromCommandLine()
        {
            BuildVitralhaInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildVitralhaInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "Vitralha")));
            AssetDatabase.Refresh();

            // 2 variantes de material para o kit inteiro (Desincronizado = frio azul-verde; Restaurado = ambar quente)
            Material matDesync   = EnsureMat($"{MatDir}/Mat_Vitralha_Desincronizado.mat", new Color(0.18f, 0.56f, 0.72f, 1f), new Color(0.08f, 0.34f, 0.50f, 1f), 0.78f, transparent: false);
            Material matRestored = EnsureMat($"{MatDir}/Mat_Vitralha_Restaurado.mat",     new Color(0.92f, 0.66f, 0.24f, 1f), new Color(0.55f, 0.32f, 0.08f, 1f), 0.72f, transparent: false);
            Material matGhost    = EnsureMat($"{MatDir}/Mat_Vitralha_FeixeGhost.mat",     new Color(0.28f, 0.90f, 1.00f, 0.22f), new Color(0.14f, 0.55f, 0.75f, 1f), 0.85f, transparent: true);
            Material matRiver    = EnsureMat($"{MatDir}/Mat_Vitralha_RioVivo.mat",        new Color(0.14f, 0.76f, 0.95f, 0.85f), new Color(0.10f, 0.50f, 0.75f, 1f), 0.90f, transparent: true);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Iluminacao cristalina (fria-primaveril)
            var sunGo = new GameObject("Directional_VitralhaPrismLight");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.84f, 0.96f, 1.0f);
            sun.intensity = 1.35f;
            sunGo.transform.rotation = Quaternion.Euler(46f, -28f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.14f, 0.20f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 6f, -12f);
            camGo.transform.LookAt(new Vector3(0f, 1.5f, 12f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Limites da area jogavel 400x400m (GATE 2: x in [-200..+200], z in [-50..+350])
            var boundsGo = new GameObject("Vitralha_Bounds_400x400");
            var boundsBox = boundsGo.AddComponent<BoxCollider>();
            boundsBox.isTrigger = true;
            boundsBox.center = new Vector3(0f, 20f, 150f);
            boundsBox.size = new Vector3(400f, 60f, 400f);

            // Jogador Dez no inicio do Beat 1 (grid 2m: (0, 0.1, 0))
            var dezGo = SpawnAsset("Assets/Game/Prefabs/Fab3D/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, 0f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null)
                dezGo.AddComponent<DezLocomotionController>();

            var vitRoot = new GameObject("Vitralha_Fuso_Controller");
            var vitCtrl = vitRoot.AddComponent<VitralhaFusoController>();

            // === BEAT 1 (z = 0..48): Entrada pela Estacao de Luz (plataformas de feixes duplos) ===
            var b1Root = new GameObject("Beat1_EstacaoDeLuz");
            vitCtrl.beatCheckpoints[0] = CreateCheckpoint("CP_Beat1_EstacaoLuz", b1Root.transform, new Vector3(0f, 0.1f, 0f));
            PlaceKitPiece("vit_floor_tile", "B1_Floor_Entry", b1Root.transform, new Vector3(0f, 0f, 4f), new Vector3(12f, 1f, 12f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_wall_prism", "B1_Wall_L", b1Root.transform, new Vector3(-8f, 0f, 16f), Vector3.one, matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_wall_prism", "B1_Wall_R", b1Root.transform, new Vector3( 8f, 0f, 16f), Vector3.one, matDesync, addBoxCollider: true);

            var b1ResyncGo = new GameObject("Vitralha_Entry_ResyncZone");
            b1ResyncGo.transform.SetParent(b1Root.transform, false);
            b1ResyncGo.transform.position = new Vector3(0f, 0f, 20f);
            var b1Resync = b1ResyncGo.AddComponent<ResyncZoneController>();
            b1Resync.zoneId = "vitralha_b1_estacao_luz";
            b1Resync.bubbleRadius = 26f;
            var node1 = PlaceKitPiece("vit_resync_node", "B1_ResyncNode", b1ResyncGo.transform, new Vector3(-2f, 0f, 10f), Vector3.one, matDesync, addBoxCollider: false);
            b1Resync.nodeAnchor = node1.transform;

            // Feixe de luz refratada atravessavel SO no Restaurado (GATE 3)
            var beam1 = PlaceKitPiece("vit_light_bridge", "B1_RefractedLightBeam_DualState", b1ResyncGo.transform, new Vector3(0f, 1.0f, 24f), new Vector3(4f, 2f, 8f), matDesync, addBoxCollider: true);
            var beamDual = beam1.AddComponent<ResyncDualStateObject>();
            beamDual.mode = DualStateMode.PassableOnlyInRestored;
            beamDual.desyncMaterial = matDesync;
            beamDual.restoredMaterial = matRestored;
            beamDual.ghostPassableMaterial = matGhost;
            b1Resync.managedObjects.Add(beamDual);
            vitCtrl.entryLightStationZone = b1Resync;

            // === BEAT 2 (z = 48..108): Bosque dos Passaros-Vidro ===
            var b2Root = new GameObject("Beat2_BosquePassarosVidro");
            vitCtrl.beatCheckpoints[1] = CreateCheckpoint("CP_Beat2_Bosque", b2Root.transform, new Vector3(0f, 0.1f, 52f));
            PlaceKitPiece("vit_floor_tile", "B2_Floor", b2Root.transform, new Vector3(0f, 0f, 56f), new Vector3(16f, 1f, 16f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_glass_tree", "B2_Tree_L", b2Root.transform, new Vector3(-10f, 0f, 66f), Vector3.one, matDesync, addBoxCollider: false);
            PlaceKitPiece("vit_glass_tree", "B2_Tree_R", b2Root.transform, new Vector3( 10f, 0f, 78f), Vector3.one, matDesync, addBoxCollider: false);
            PlaceKitPiece("vit_glass_bird_platform", "B2_BirdPlat_1", b2Root.transform, new Vector3(-2f, 2.0f, 74f), new Vector3(2f, 1f, 2f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_glass_bird_platform", "B2_BirdPlat_2", b2Root.transform, new Vector3( 2f, 4.0f, 88f), new Vector3(2f, 1f, 2f), matDesync, addBoxCollider: true);

            // === BEAT 3 (z = 108..156): Mirante do Rio Parado ===
            var b3Root = new GameObject("Beat3_MiranteRioParado");
            vitCtrl.beatCheckpoints[2] = CreateCheckpoint("CP_Beat3_Mirante", b3Root.transform, new Vector3(0f, 4.1f, 112f));
            PlaceKitPiece("vit_floor_tile", "B3_MiranteDeck", b3Root.transform, new Vector3(0f, 4f, 120f), new Vector3(14f, 1f, 14f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_glass_tree", "B3_Tree_Vista", b3Root.transform, new Vector3(12f, 4f, 128f), Vector3.one * 1.2f, matDesync, addBoxCollider: false);

            // === BEAT 4 (z = 156..216): Cachoeira de Vidro (rota de Rebote + Sopro) ===
            var b4Root = new GameObject("Beat4_CachoeiraDeVidro");
            vitCtrl.beatCheckpoints[3] = CreateCheckpoint("CP_Beat4_Cachoeira", b4Root.transform, new Vector3(0f, 4.1f, 160f));
            PlaceKitPiece("vit_waterfall_arch", "B4_WaterfallArch_1", b4Root.transform, new Vector3(0f, 4f, 174f), Vector3.one * 1.4f, matDesync, addBoxCollider: false);
            PlaceKitPiece("vit_floor_tile", "B4_StepPlat_1", b4Root.transform, new Vector3(-4f, 6f, 178f), new Vector3(6f, 1f, 6f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_floor_tile", "B4_StepPlat_2", b4Root.transform, new Vector3( 4f, 8f, 194f), new Vector3(6f, 1f, 6f), matDesync, addBoxCollider: true);
            PlaceKitPiece("vit_waterfall_arch", "B4_WaterfallArch_2", b4Root.transform, new Vector3(0f, 8f, 206f), Vector3.one * 1.5f, matDesync, addBoxCollider: false);

            // === BEAT 5 (z = 216..276): Boss A Estufa Viva (Arena Dual-State + Posto de Reparo <= 8s) ===
            var b5Root = new GameObject("Beat5_ArenaEstufaViva");
            vitCtrl.beatCheckpoints[4] = CreateCheckpoint("CP_Beat5_ArenaBoss", b5Root.transform, new Vector3(0f, 8.1f, 224f));
            var arenaFloor = PlaceKitPiece("vit_floor_tile", "B5_ArenaFloor", b5Root.transform, new Vector3(0f, 8f, 244f), new Vector3(26f, 1f, 26f), matDesync, addBoxCollider: true);

            var arenaResyncGo = new GameObject("Vitralha_BossArena_ResyncZone");
            arenaResyncGo.transform.SetParent(b5Root.transform, false);
            arenaResyncGo.transform.position = new Vector3(0f, 8f, 244f);
            var arenaResync = arenaResyncGo.AddComponent<ResyncZoneController>();
            arenaResync.zoneId = "vitralha_b5_arena_estufa";
            arenaResync.bubbleRadius = 28f;
            var arenaDual = arenaFloor.AddComponent<ResyncDualStateObject>();
            arenaDual.mode = DualStateMode.VisualSwapOnly;
            arenaDual.desyncMaterial = matDesync;
            arenaDual.restoredMaterial = matRestored;
            arenaResync.managedObjects.Add(arenaDual);

            // Posto de Reparo dentro da arena (GDD 05 §7)
            var repairGo = SpawnAsset("Assets/Game/Prefabs/Fab3D/c5_prop_bancada_relogio.prefab", "B5_ArenaRepairStation", b5Root.transform, new Vector3(-10f, 8.1f, 226f), Vector3.one);

            // Hero Piece TripoSG: Boss A Estufa Viva
            var bossGo = PlaceKitPiece("vit_hero_estufa_viva", "Boss_AEstufaViva", b5Root.transform, new Vector3(0f, 8.2f, 250f), Vector3.one, matDesync, addBoxCollider: true);
            var bossCtrl = bossGo.AddComponent<VitralhaEstufaVivaBoss>();
            bossCtrl.arenaResyncZone = arenaResync;
            bossCtrl.repairStationAnchor = repairGo.transform;
            vitCtrl.bossEstufaViva = bossCtrl;

            // === BEAT 6 (z = 276..310): Sala da Reliquia Ponteiro Maior ===
            var b6Root = new GameObject("Beat6_SalaReliquiaPonteiroMaior");
            vitCtrl.beatCheckpoints[5] = CreateCheckpoint("CP_Beat6_Reliquia", b6Root.transform, new Vector3(0f, 8.1f, 286f));
            PlaceKitPiece("vit_floor_tile", "B6_RelicFloor", b6Root.transform, new Vector3(0f, 8f, 290f), new Vector3(12f, 1f, 12f), matRestored, addBoxCollider: true);
            var relicGo = PlaceKitPiece("vit_resync_node", "Relic_PonteiroMaior", b6Root.transform, new Vector3(0f, 8.8f, 292f), Vector3.one * 0.85f, matRestored, addBoxCollider: false);
            vitCtrl.relicPonteiroMaiorObject = relicGo;

            // === BEAT 7 (z = 310..350): Consequencia — O Rio Volta (desague para Maresia) ===
            var b7Root = new GameObject("Beat7_ConsequenciaRioVolta");
            vitCtrl.beatCheckpoints[6] = CreateCheckpoint("CP_Beat7_Consequencia", b7Root.transform, new Vector3(0f, 8.1f, 320f));
            PlaceKitPiece("vit_floor_tile", "B7_OverlookFloor", b7Root.transform, new Vector3(0f, 8f, 324f), new Vector3(14f, 1f, 14f), matRestored, addBoxCollider: true);
            var riverGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riverGo.name = "B7_RiverFlow_ToMaresia";
            riverGo.transform.SetParent(b7Root.transform, false);
            riverGo.transform.position = new Vector3(0f, 2.0f, 340f);
            riverGo.transform.localScale = new Vector3(24f, 0.6f, 60f);
            riverGo.GetComponent<MeshRenderer>().sharedMaterial = matRiver;
            Object.DestroyImmediate(riverGo.GetComponent<Collider>());
            vitCtrl.riverFlowVisual = riverGo;

            EditorSceneManager.SaveScene(sc, ScenePath);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            Debug.Log($"[VitralhaBuilder] Cena '{ScenePath}' (400x400m, 7 beats, kit TripoSG 8 pecas) construida com sucesso.");
        }

        private static Transform CreateCheckpoint(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static GameObject PlaceKitPiece(string slug, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            string fbxPath = $"{KitDir}/{slug}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = mat;

            if (addBoxCollider && go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();
            return go;
        }

        private static GameObject SpawnAsset(string assetPath, string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static Material EnsureMat(string path, Color baseCol, Color emissive, float smoothness, bool transparent)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emissive); }
            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
