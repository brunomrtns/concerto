#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Combat;
using Studio.Game.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi a cena de demonstracao da Economia Canon (Assets/Game/Scenes/EconomiaTioHorarioDemo.unity):
    ///   - Posto/Loja do Tio Horario gerado via TripoSG (tio_horario_shop_booth.fbx) + NPC Tio Horario.
    ///   - Fios de Tempo dourados (fio_de_tempo_segundo.fbx) coletaveis.
    ///   - 1 Memoria-Eco interativa tocando micro-cena narrativa.
    ///   - Dez com SegundosEconomySystem, PressureResourceSystem, FaiscaRangedController e ParryAndLockOnController.
    /// </summary>
    public static class EconomiaTioHorarioSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/EconomiaTioHorarioDemo.unity";
        private const string KitDir    = "Assets/Game/Art/Generated/economia";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string MatDir    = "Assets/Game/Materials/Economia";

        [MenuItem("Tools/CONCERTO/Build Economia Tio Horario Demo Scene")]
        public static void BuildMenu() => BuildInternal();

        public static void BuildFromCommandLine()
        {
            BuildInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildInternal()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Materials", "Economia")));
            AssetDatabase.Refresh();

            Material matFloor  = EnsureMat($"{MatDir}/Mat_Econ_Floor.mat",     new Color(0.24f, 0.22f, 0.20f, 1f), new Color(0.05f, 0.04f, 0.03f, 1f), 0.50f);
            Material matBooth  = EnsureMat($"{MatDir}/Mat_Econ_TioBooth.mat",  new Color(0.74f, 0.50f, 0.22f, 1f), new Color(0.32f, 0.20f, 0.06f, 1f), 0.65f);
            Material matGold   = EnsureMat($"{MatDir}/Mat_Econ_FioTempo.mat",  new Color(1.00f, 0.84f, 0.24f, 1f), new Color(0.90f, 0.66f, 0.10f, 1f), 0.92f);
            Material matMemory = EnsureMat($"{MatDir}/Mat_Econ_MemoriaEco.mat",new Color(0.28f, 0.88f, 1.00f, 1f), new Color(0.18f, 0.72f, 0.95f, 1f), 0.88f);
            AssetDatabase.SaveAssets();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional_WorkshopSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.92f, 0.78f);
            sun.intensity = 1.4f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -30f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 56f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.09f, 0.12f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 6f, -12f);
            camGo.transform.LookAt(new Vector3(0f, 2f, 4f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            PlaceAsset($"{FabDir}/caldeiral_env_floor_tile.prefab", "Econ_MainPlaza", null, Vector3.zero, new Vector3(26f, 1f, 26f), matFloor, true);

            // Posto/Loja do Tio Horario (TripoSG) + NPC Tio Horario
            PlaceAsset($"{KitDir}/tio_horario_shop_booth.fbx", "TioHorario_ShopBooth_TripoSG", null, new Vector3(0f, 2.0f, 6f), new Vector3(4.5f, 4.5f, 4.5f), matBooth, true);
            PlaceAsset($"{FabDir}/npc_tio_horario_humanoid.prefab", "NPC_TioHorario_Shopkeeper", null, new Vector3(2.8f, 0.1f, 4.5f), Vector3.one, null, false);

            // 3 Fios de Tempo dourados (Segundos)
            for (int i = 0; i < 3; i++)
                PlaceAsset($"{KitDir}/fio_de_tempo_segundo.fbx", $"FioDeTempo_Segundo_{i + 1}", null, new Vector3(-5f + i * 2.5f, 1.2f, -1f), new Vector3(1.1f, 1.1f, 1.1f), matGold, false);

            // 1 Pedestal de Memoria-Eco
            PlaceAsset($"{FabDir}/c5_prop_no_resincronia.prefab", "MemoriaEco_Caldeiral_1", null, new Vector3(6f, 0.2f, 1f), new Vector3(1.2f, 1.2f, 1.2f), matMemory, false);

            // Jogador Dez com Economia + Combate (para efeitos reais das melhorias!)
            var dezGo = PlaceAsset($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(0f, 0.1f, -5f), Vector3.one, null, false);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            if (dezGo.GetComponent<DezLocomotionController>() == null) dezGo.AddComponent<DezLocomotionController>();
            if (dezGo.GetComponent<PressureResourceSystem>() == null) dezGo.AddComponent<PressureResourceSystem>();
            if (dezGo.GetComponent<FaiscaRangedController>() == null) dezGo.AddComponent<FaiscaRangedController>();
            if (dezGo.GetComponent<ParryAndLockOnController>() == null) dezGo.AddComponent<ParryAndLockOnController>();
            if (dezGo.GetComponent<SegundosEconomySystem>() == null) dezGo.AddComponent<SegundosEconomySystem>();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EconomiaTioHorarioSceneBuilder] Cena {ScenePath} construida com Loja TripoSG do Tio Horario, Fios de Tempo e Memoria-Eco!");
        }

        private static GameObject PlaceAsset(string assetPath, string objName, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool addBoxCollider)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject(objName);
            go.name = objName;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mat;
            if (addBoxCollider && go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();
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
