#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Maresia;
using Studio.Game.Resync;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi o Fuso Maresia — O Mar Parado (Assets/Game/Scenes/Maresia.unity, area 400x400m)
    /// usando kit modular de 8 pecas 100% TripoSG (4 novas em Assets/Game/Art/Generated/maresia/
    /// + 4 modulares TripoSG em Assets/Game/Prefabs/Fab3D/), com 2 variantes de material
    /// (Desincronizado azul-aco/madeira umida / Restaurado turquesa/latao quente) e 7 beats do GDD 03 §2.3.
    /// </summary>
    public static class MaresiaSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Maresia.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/maresia";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string MatDir    = "Assets/Game/Materials/Maresia";

        [MenuItem("Tools/CONCERTO/Build Maresia Scene (7 Beats)")]
        public static void BuildMaresiaMenu()
        {
            BuildMaresiaInternal();
        }

        public static void BuildMaresiaFromCommandLine()
        {
            BuildMaresiaInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildMaresiaInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "Maresia")));
            AssetDatabase.Refresh();

            // 2 variantes de material madeira/latao (Desincronizado congelado / Restaurado degelado)
            Material matDesync   = EnsureMat($"{MatDir}/Mat_Maresia_Desincronizado.mat", new Color(0.16f, 0.34f, 0.48f, 1f), new Color(0.05f, 0.14f, 0.24f, 1f), 0.70f, transparent: false);
            Material matRestored = EnsureMat($"{MatDir}/Mat_Maresia_Restaurado.mat",     new Color(0.76f, 0.56f, 0.24f, 1f), new Color(0.28f, 0.18f, 0.05f, 1f), 0.62f, transparent: false);
            Material matGoldLens = EnsureMat($"{MatDir}/Mat_Maresia_OlhoLenteOuro.mat",  new Color(0.98f, 0.80f, 0.20f, 1f), new Color(0.75f, 0.52f, 0.08f, 1f), 0.88f, transparent: false);
            Material matFlood    = EnsureMat($"{MatDir}/Mat_Maresia_AguaAlagada.mat",    new Color(0.08f, 0.48f, 0.68f, 0.78f), new Color(0.04f, 0.26f, 0.42f, 1f), 0.90f, transparent: true);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Iluminacao maritima fria-dourada
            var sunGo = new GameObject("Directional_MaresiaSeaLight");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.86f, 0.93f, 1.0f);
            sun.intensity = 1.35f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -34f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.14f, 0.22f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 7f, -12f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 12f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Limites jogaveis 400x400m
            var boundsGo = new GameObject("Maresia_Bounds_400x400");
            var boundsBox = boundsGo.AddComponent<BoxCollider>();
            boundsBox.isTrigger = true;
            boundsBox.center = new Vector3(0f, 22f, 150f);
            boundsBox.size = new Vector3(400f, 70f, 400f);

            // Jogador Dez no Beat 1
            var dezGo = SpawnPrefab($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, 0f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null)
                dezGo.AddComponent<DezLocomotionController>();

            var marRoot = new GameObject("Maresia_Fuso_Controller");
            var marCtrl = marRoot.AddComponent<MaresiaFusoController>();

            // === BEAT 1 (z = 0..46): Entrada do Cais Congelado ===
            var b1Root = new GameObject("Beat1_CaisCongelado");
            marCtrl.beatCheckpoints[0] = CreateCheckpoint("CP_Beat1_Cais", b1Root.transform, new Vector3(0f, 0.1f, 0f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B1_Pier_Entry", b1Root.transform, new Vector3(0f, 0f, 6f), new Vector3(12f, 1f, 14f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B1_LighthouseWall_L", b1Root.transform, new Vector3(-10f, 0f, 18f), new Vector3(1f, 2.2f, 2f), matDesync, true);
            PlaceFabPiece("caldeiral_env_wall_panel", "B1_LighthouseWall_R", b1Root.transform, new Vector3( 10f, 0f, 18f), new Vector3(1f, 2.2f, 2f), matDesync, true);

            var b1ResyncGo = new GameObject("Maresia_Entry_ResyncZone");
            b1ResyncGo.transform.SetParent(b1Root.transform, false);
            b1ResyncGo.transform.position = new Vector3(0f, 0f, 24f);
            var b1Resync = b1ResyncGo.AddComponent<ResyncZoneController>();
            b1Resync.zoneId = "maresia_b1_cais";
            b1Resync.bubbleRadius = 24f;
            var node1 = PlaceFabPiece("c5_prop_no_resincronia", "B1_ResyncNode", b1ResyncGo.transform, new Vector3(-3f, 0f, 14f), Vector3.one, matDesync, false);
            b1Resync.nodeAnchor = node1.transform;
            var b1DualDeck = PlaceFabPiece("caldeiral_env_floor_tile", "B1_DualState_Deck", b1ResyncGo.transform, new Vector3(0f, 0.2f, 32f), new Vector3(6f, 0.5f, 8f), matDesync, true);
            var b1Dual = b1DualDeck.AddComponent<ResyncDualStateObject>();
            b1Dual.mode = DualStateMode.PassableOnlyInRestored;
            b1Dual.desyncMaterial = matDesync;
            b1Dual.restoredMaterial = matRestored;
            b1Resync.managedObjects.Add(b1Dual);

            // === BEAT 2 (z = 46..104): Cristas de Ondas Congeladas de 10m + Barcos Inclinados (GATE 1) ===
            var b2Root = new GameObject("Beat2_CristasOndas10m");
            marCtrl.beatCheckpoints[1] = CreateCheckpoint("CP_Beat2_CristasOndas", b2Root.transform, new Vector3(0f, 0.1f, 48f));
            for (int i = 0; i < 3; i++)
            {
                float z = 54f + i * 16f;
                float x = (i % 2 == 0) ? -4f : 4f;
                float yCrest = 3.2f + i * 3.4f; // sobe ate ~10m de crista congelada
                PlaceKitPiece("mar_frozen_wave_crest", $"B2_WaveCrest10m_{i}", b2Root.transform, new Vector3(x, yCrest * 0.5f, z), new Vector3(5f, 5f, 5f), matDesync, true);
                var boat = PlaceKitPiece("mar_tilted_boat_plat", $"B2_TiltedBoatPlat_{i}", b2Root.transform, new Vector3(-x * 0.5f, yCrest, z + 6f), new Vector3(3.2f, 2.0f, 4.5f), matRestored, true);
                boat.transform.rotation = Quaternion.Euler(-14f, i * 18f - 12f, (i % 2 == 0) ? 12f : -12f);
            }

            // === BEAT 3 (z = 104..156): Cavernas-Habitacao Alagadas — Consequencia da Vitralha (GATE 5) ===
            var b3Root = new GameObject("Beat3_CavernasHabitacaoAlagadas");
            marCtrl.beatCheckpoints[2] = CreateCheckpoint("CP_Beat3_Cavernas", b3Root.transform, new Vector3(0f, 0.1f, 106f));
            PlaceKitPiece("mar_flooded_cavern_arch", "B3_CavernArch_Entry", b3Root.transform, new Vector3(0f, 2f, 118f), new Vector3(6f, 6f, 6f), matDesync, true);
            PlaceKitPiece("mar_flooded_cavern_arch", "B3_CavernArch_Exit",  b3Root.transform, new Vector3(0f, 2f, 142f), new Vector3(6f, 6f, 6f), matDesync, true);

            var floodWater = PlaceFabPiece("caldeiral_env_floor_tile", "B3_FloodedCavernWater", b3Root.transform, new Vector3(0f, 2.2f, 130f), new Vector3(24f, 0.3f, 28f), matFlood, false);
            floodWater.SetActive(false);
            marCtrl.floodWaterVisual = floodWater;

            // Rota jogavel de desvio elevado quando Vitralha alaga as cavernas (3 waypoints)
            var detourRoot = new GameObject("B3_FloodDetourQuest_HighRoute");
            detourRoot.transform.SetParent(b3Root.transform, false);
            for (int w = 0; w < 3; w++)
            {
                Vector3 wpPos = new Vector3(10f + w * 2f, 5.0f + w * 0.8f, 114f + w * 14f);
                PlaceKitPiece("mar_tilted_boat_plat", $"B3_DetourLedge_{w}", detourRoot.transform, wpPos - Vector3.up * 0.5f, new Vector3(2.8f, 1.2f, 3.6f), matRestored, true);
                var wp = new GameObject($"WP_Detour_{w + 1}");
                wp.transform.SetParent(detourRoot.transform, false);
                wp.transform.position = wpPos;
            }

            // === BEAT 4 (z = 156..210): Desfiladeiro da Mare — Rota Alternativa Rebote+Sopro (GATE 2) ===
            var b4Root = new GameObject("Beat4_DesfiladeiroRotaAlternativa");
            marCtrl.beatCheckpoints[3] = CreateCheckpoint("CP_Beat4_Desfiladeiro", b4Root.transform, new Vector3(0f, 0.1f, 158f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B4_AltRoute_Step1_Rebote", b4Root.transform, new Vector3(-6f, 2.8f, 170f), new Vector3(4f, 0.8f, 5f), matRestored, true);
            PlaceFabPiece("caldeiral_env_floor_tile", "B4_AltRoute_Step2_Sopro",  b4Root.transform, new Vector3( 0f, 5.2f, 184f), new Vector3(4f, 0.8f, 5f), matRestored, true);
            PlaceFabPiece("caldeiral_env_floor_tile", "B4_AltRoute_Landing",      b4Root.transform, new Vector3( 0f, 5.2f, 200f), new Vector3(8f, 0.8f, 8f), matDesync, true);

            // Olhos-de-lente dourados para revisita com Garra
            CreateGrappleLensPoint("B4_GrappleLens_1", b4Root.transform, new Vector3(5f, 8.5f, 176f), matGoldLens);
            CreateGrappleLensPoint("B4_GrappleLens_2", b4Root.transform, new Vector3(5f, 9.0f, 188f), matGoldLens);

            // === BEAT 5 (z = 210..268): Arena Boss A Baleia de Ancora (Desferrujar — GATE 3) ===
            var b5Root = new GameObject("Beat5_ArenaBaleiaAncora");
            marCtrl.beatCheckpoints[4] = CreateCheckpoint("CP_Beat5_ArenaBoss", b5Root.transform, new Vector3(0f, 0.1f, 214f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B5_ArenaDeck", b5Root.transform, new Vector3(0f, 0f, 238f), new Vector3(32f, 1f, 34f), matDesync, true);

            var repairStation = PlaceFabPiece("c5_prop_no_resincronia", "B5_RepairStation_Boss", b5Root.transform, new Vector3(0f, 0.1f, 218f), Vector3.one, matRestored, false);
            var whaleGo = PlaceKitPiece("mar_hero_baleia_ancora", "Boss_BaleiaDeAncora", b5Root.transform, new Vector3(0f, 3.5f, 246f), new Vector3(7f, 7f, 7f), matDesync, true);
            var boss = whaleGo.AddComponent<MaresiaBaleiaAncoraBoss>();
            boss.repairStationAnchor = repairStation.transform;
            marCtrl.bossBaleia = boss;

            // === BEAT 6 (z = 268..310): Reliquia Pendulo -> Garra + Pontos Olho-de-Lente (GATE 4) ===
            var b6Root = new GameObject("Beat6_ReliquiaPendulo");
            marCtrl.beatCheckpoints[5] = CreateCheckpoint("CP_Beat6_ReliquiaPendulo", b6Root.transform, new Vector3(0f, 0.1f, 272f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B6_AltarDeck", b6Root.transform, new Vector3(0f, 0f, 286f), new Vector3(16f, 1f, 22f), matRestored, true);
            var relicGo = PlaceFabPiece("caldeiral_env_portal_pillar", "Relic_Pendulo_Pickup", b6Root.transform, new Vector3(0f, 1.2f, 282f), new Vector3(0.8f, 0.8f, 0.8f), matGoldLens, false);
            relicGo.SetActive(false);
            marCtrl.relicPenduloObject = relicGo;

            // 3 pontos olho-de-lente dourados espacados <= 10m para swing circular + combo Garra->Sopro->Rebote
            CreateGrappleLensPoint("B6_GrappleLens_Swing1", b6Root.transform, new Vector3(0f, 5.5f, 290f), matGoldLens);
            CreateGrappleLensPoint("B6_GrappleLens_Swing2", b6Root.transform, new Vector3(0f, 6.0f, 300f), matGoldLens);
            CreateGrappleLensPoint("B6_GrappleLens_Swing3", b6Root.transform, new Vector3(0f, 6.5f, 310f), matGoldLens);

            // === BEAT 7 (z = 310..350): Pos-Conserto — Mar Degela, Barco-Drift & Elevador de Onda (GATE 6) ===
            var b7Root = new GameObject("Beat7_MarDegeladoBarcoDrift");
            marCtrl.beatCheckpoints[6] = CreateCheckpoint("CP_Beat7_MarDegelado", b7Root.transform, new Vector3(0f, 0.1f, 316f));
            PlaceFabPiece("caldeiral_env_floor_tile", "B7_HarborDock", b7Root.transform, new Vector3(0f, 0f, 326f), new Vector3(22f, 1f, 22f), matRestored, true);
            var driftBoat = PlaceKitPiece("mar_tilted_boat_plat", "B7_DriftBoat_Navigation", b7Root.transform, new Vector3(0f, 0.8f, 322f), new Vector3(3.2f, 1.8f, 4.8f), matRestored, true);
            marCtrl.driftBoatRoot = driftBoat.transform;
            var waveElev = PlaceKitPiece("mar_frozen_wave_crest", "B7_WaveElevatorPlatform", b7Root.transform, new Vector3(8f, 1.5f, 332f), new Vector3(3.5f, 1.5f, 3.5f), matRestored, true);
            marCtrl.waveElevatorPlatform = waveElev.transform;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MaresiaSceneBuilder] Cena {ScenePath} construida com 7 beats, kit 8 pecas 100% TripoSG (2 estados), Baleia de Ancora e Barco-Drift!");
        }

        private static GameObject CreateGrappleLensPoint(string name, Transform parent, Vector3 pos, Material goldMat)
        {
            var go = PlaceFabPiece("caldeiral_env_portal_pillar", name, parent, pos, new Vector3(0.45f, 0.45f, 0.45f), goldMat, false);
            var lens = go.AddComponent<GrappleLensPoint>();
            lens.snapRadius = 12.0f;
            return go;
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
            string assetPath = $"{KitDir}/{fbxName}.fbx";
            var go = SpawnPrefab(assetPath, objName, parent, pos, scale);
            ApplyMaterialAndCollider(go, mat, addBoxCollider);
            return go;
        }

        private static GameObject PlaceFabPiece(string prefabName, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            string assetPath = $"{FabDir}/{prefabName}.prefab";
            var go = SpawnPrefab(assetPath, objName, parent, pos, scale);
            ApplyMaterialAndCollider(go, mat, addBoxCollider);
            return go;
        }

        private static void ApplyMaterialAndCollider(GameObject go, Material mat, bool addBoxCollider)
        {
            if (mat != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            }
            if (addBoxCollider && go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        private static GameObject SpawnPrefab(string assetPath, string objName, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go;
            if (prefab != null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
            {
                Debug.LogWarning($"[MaresiaSceneBuilder] Asset nao encontrado: {assetPath}");
                go = new GameObject(objName);
            }
            go.name = objName;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static Material EnsureMat(string path, Color baseColor, Color emission, float smoothness, bool transparent)
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
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
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
