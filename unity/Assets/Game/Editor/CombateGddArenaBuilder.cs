#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi a Arena de Combate GDD (Assets/Game/Scenes/CombateGddArena.unity):
    ///   - 3 rotas de verticalidade (GDD 05 §7: 2-4 rotas) com pecas TripoSG Fab3D.
    ///   - 5 inimigos canon gerados via TripoSG em Assets/Game/Art/Generated/combat_gdd/
    ///     organizados em 3 ondas com no maximo 2 tipos simultaneos por onda.
    ///   - Dez com PressureResourceSystem, FaiscaRangedController e ParryAndLockOnController.
    /// </summary>
    public static class CombateGddArenaBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/CombateGddArena.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/combat_gdd";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string MatDir    = "Assets/Game/Materials/CombatGdd";

        [MenuItem("Tools/CONCERTO/Build Combate GDD Arena Scene")]
        public static void BuildArenaMenu() => BuildArenaInternal();

        public static void BuildArenaFromCommandLine()
        {
            BuildArenaInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildArenaInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "CombatGdd")));
            AssetDatabase.Refresh();

            Material matFloor  = EnsureMat($"{MatDir}/Mat_Arena_Floor.mat",   new Color(0.22f, 0.24f, 0.28f, 1f), new Color(0.04f, 0.06f, 0.10f, 1f), 0.55f);
            Material matRoute  = EnsureMat($"{MatDir}/Mat_Arena_Route.mat",   new Color(0.76f, 0.56f, 0.24f, 1f), new Color(0.25f, 0.16f, 0.04f, 1f), 0.65f);
            Material matRust   = EnsureMat($"{MatDir}/Mat_Enemy_Ferrugem.mat", new Color(0.66f, 0.30f, 0.14f, 1f), new Color(0.18f, 0.06f, 0.02f, 1f), 0.48f);
            Material matCensor = EnsureMat($"{MatDir}/Mat_Enemy_Censor.mat",  new Color(0.20f, 0.24f, 0.34f, 1f), new Color(0.50f, 0.08f, 0.12f, 1f), 0.78f);
            Material matBubble = EnsureMat($"{MatDir}/Mat_Shield_Bubble.mat", new Color(0.20f, 0.86f, 0.98f, 0.35f), new Color(0.10f, 0.55f, 0.75f, 1f), 0.90f, transparent: true);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional_ArenaLight");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.96f, 0.92f, 0.86f);
            sun.intensity = 1.4f;
            sunGo.transform.rotation = Quaternion.Euler(44f, -32f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 10f, -18f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 4f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Chao principal da arena + 3 rotas de verticalidade (GATE 5: 2-4 rotas)
            var envRoot = new GameObject("Arena_Geometry_3VerticalRoutes");
            PlaceFab("caldeiral_env_floor_tile", "Arena_MainFloor", envRoot.transform, Vector3.zero, new Vector3(36f, 1f, 36f), matFloor, true);
            PlaceFab("caldeiral_env_floor_tile", "VerticalRoute_1_WestMezzanine", envRoot.transform, new Vector3(-11f, 3.2f, 4f), new Vector3(6f, 0.8f, 12f), matRoute, true);
            PlaceFab("caldeiral_env_floor_tile", "VerticalRoute_2_EastMezzanine", envRoot.transform, new Vector3( 11f, 3.2f, 4f), new Vector3(6f, 0.8f, 12f), matRoute, true);
            PlaceFab("caldeiral_env_floor_tile", "VerticalRoute_3_NorthOverpass", envRoot.transform, new Vector3(  0f, 5.2f, 12f), new Vector3(14f, 0.8f, 5f), matRoute, true);

            // Jogador Dez com Pressao + Faisca + Contratempo
            var dezGo = SpawnAsset($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, -8f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null) dezGo.AddComponent<DezLocomotionController>();
            if (dezGo.GetComponent<PressureResourceSystem>() == null) dezGo.AddComponent<PressureResourceSystem>();
            if (dezGo.GetComponent<FaiscaRangedController>() == null) dezGo.AddComponent<FaiscaRangedController>();
            if (dezGo.GetComponent<ParryAndLockOnController>() == null) dezGo.AddComponent<ParryAndLockOnController>();

            // Diretor de 3 ondas (MAX 2 tipos simultaneos por onda!)
            var dirGo = new GameObject("GddCombat_WaveDirector");
            var dir = dirGo.AddComponent<GddCombatWaveDirector>();
            dir.verticalityRouteCount = 3;

            // Onda 1: Ferrugem-Trabalhadora + Ferrugem-Espelho (2 tipos)
            var eTrab = SpawnEnemy<FerrugemTrabalhadoraEnemy>("enemy_ferrugem_trabalhadora", "Wave1_FerrugemTrabalhadora", dirGo.transform, new Vector3(-4f, 1.5f, 4f), new Vector3(2.2f, 2.2f, 2.2f), matRust);
            var eEsp1 = SpawnEnemy<FerrugemEspelhoEnemy>("enemy_ferrugem_espelho", "Wave1_FerrugemEspelho", dirGo.transform, new Vector3( 4f, 1.5f, 6f), new Vector3(2.1f, 2.1f, 2.1f), matRust);

            // Onda 2: Ferrugem-Coletora (com bolha) + Censor-Patrulha (2 tipos)
            var eCol  = SpawnEnemy<FerrugemColetoraEnemy>("enemy_ferrugem_coletora", "Wave2_FerrugemColetora", dirGo.transform, new Vector3(-4f, 1.6f, 5f), new Vector3(2.2f, 2.2f, 2.2f), matRust);
            var bubble = PlaceFab("c5_prop_no_resincronia", "ShieldBubbleVisual", eCol.transform, eCol.transform.position, new Vector3(1.8f, 1.8f, 1.8f), matBubble, false);
            eCol.shieldBubbleVisual = bubble;
            var ePat  = SpawnEnemy<CensorPatrulhaEnemy>("enemy_censor_patrulha", "Wave2_CensorPatrulha", dirGo.transform, new Vector3( 4f, 1.5f, 5f), new Vector3(2.1f, 2.1f, 2.1f), matCensor);

            // Onda 3: Censor-Pesado + Ferrugem-Espelho (2 tipos)
            var ePes  = SpawnEnemy<CensorPesadoEnemy>("enemy_censor_pesado", "Wave3_CensorPesado", dirGo.transform, new Vector3(0f, 1.9f, 6f), new Vector3(2.8f, 2.8f, 2.8f), matCensor);
            var eEsp2 = SpawnEnemy<FerrugemEspelhoEnemy>("enemy_ferrugem_espelho", "Wave3_FerrugemEspelho", dirGo.transform, new Vector3( 6f, 3.8f, 4f), new Vector3(2.1f, 2.1f, 2.1f), matRust);

            dir.waves = new[]
            {
                new GddCombatWaveDef { waveName = "Onda 1 — Trabalhadora + Espelho", enemies = new GddCanonEnemyBase[] { eTrab, eEsp1 } },
                new GddCombatWaveDef { waveName = "Onda 2 — Coletora + Censor-Patrulha", enemies = new GddCanonEnemyBase[] { eCol, ePat } },
                new GddCombatWaveDef { waveName = "Onda 3 — Censor-Pesado + Espelho", enemies = new GddCanonEnemyBase[] { ePes, eEsp2 } }
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CombateGddArenaBuilder] Cena {ScenePath} construida com 5 inimigos canon TripoSG, 3 ondas (<=2 tipos/onda) e 3 rotas de verticalidade!");
        }

        private static T SpawnEnemy<T>(string fbxName, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat) where T : GddCanonEnemyBase
        {
            var go = SpawnAsset($"{KitDir}/{fbxName}.fbx", objName, parent, pos, scale);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();
            return go.AddComponent<T>();
        }

        private static GameObject PlaceFab(string prefabName, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            var go = SpawnAsset($"{FabDir}/{prefabName}.prefab", objName, parent, pos, scale);
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            if (addBoxCollider && go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();
            return go;
        }

        private static GameObject SpawnAsset(string assetPath, string objName, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject(objName);
            go.name = objName;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static Material EnsureMat(string path, Color baseColor, Color emission, float smoothness, bool transparent = false)
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
