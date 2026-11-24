#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Studio.Game.Collision;
using Studio.Game.Combat;
using Studio.Game.Epicenter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Editor
{
    public static class EpicenterSceneBuilder
    {
        private const string BaseScenePath    = "Assets/Game/Scenes/SetorExterno.unity";
        private const string TargetScenePath  = "Assets/Game/Scenes/Epicentro.unity";
        private const string ConfigPath       = "Assets/Game/Config/EpicenterConfig.asset";
        private const string PlatformFbxPath  = "Assets/Game/Art/Generated/epicenter/epicenter_crystal_platform.fbx";
        private const string MonolithFbxPath  = "Assets/Game/Art/Generated/epicenter/epicenter_rift_monolith.fbx";
        private const string EstPrefabPath    = "Assets/Game/Prefabs/Enemies/Enemy_Estilhaco.prefab";
        private const string FluPrefabPath    = "Assets/Game/Prefabs/Enemies/Enemy_Flutuante.prefab";

        [MenuItem("Concerto/Epicenter (#102)/Build Epicentro Scene (TripoSG) & Add to BuildSettings")]
        public static void BuildEpicenterScene()
        {
            Directory.CreateDirectory("Assets/Game/Config");
            AssetDatabase.Refresh();

            var platFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformFbxPath);
            var monoFbx = AssetDatabase.LoadAssetAtPath<GameObject>(MonolithFbxPath);
            if (platFbx == null || monoFbx == null)
                throw new InvalidOperationException("REGRA INVIOLAVEL TRIPOSG: FBX de plataforma ou monolito do Epicentro ausente!");

            var estPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EstPrefabPath);
            var fluPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FluPrefabPath);
            var combatCfg = AssetDatabase.LoadAssetAtPath<CombatBalanceConfig>("Assets/Game/Config/CombatBalanceConfig.asset");
            var telegraphClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_telegraph_TEMP.wav");
            var dissolveClip  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
            var omenClip      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Epicenter/sfx_epicenter_boss_omen_TEMP.wav");

            var cfg = AssetDatabase.LoadAssetAtPath<EpicenterConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<EpicenterConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                EditorUtility.SetDirty(cfg);
            }

            var platMat = BuildPbrMat("epicenter_crystal_platform", new Color(0.24f, 0.85f, 1.0f));
            var monoMat = BuildPbrMat("epicenter_rift_monolith", new Color(0.75f, 0.22f, 1.0f));

            // Clona SetorExterno para preservar Player_Dez_Placeholder, Camera, MultiTool, Powers, HP e PauseMenu 100% integrados
            AssetDatabase.CopyAsset(BaseScenePath, TargetScenePath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            // Remove apenas objetos especificos do SetorExterno, mantendo Player, Camera, EventSystem, Managers e Light
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                string n = roots[i].name;
                bool keep = n.Contains("Player_Dez") || n.Contains("Main Camera") || n.Contains("Camera")
                         || n.Contains("EventSystem") || n.Contains("MultiTool") || n.Contains("Pause")
                         || n.Contains("Dialogue") || n.Contains("Save") || n.Contains("Directional Light")
                         || n.Contains("VCam") || n.Contains("HUD");
                if (!keep)
                    UnityEngine.Object.DestroyImmediate(roots[i]);
            }

            // Atmosfera do Rasgo no Mundo (ceu escuro violeta-azul + neblina fria)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.26f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.12f, 1f);
            RenderSettings.fogDensity = 0.011f;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.03f, 0.10f, 1f);
            }

            var levelRoot = new GameObject("Epicentro_LevelRoot");

            // === SECAO 1: TRAVESSIA (X = 0 .. 44) ===
            var cp1 = CreateCheckpoint(levelRoot.transform, "Checkpoint_S1_Travessia", new Vector3(0f, 0.1f, 0f));
            SpawnPlatform(levelRoot.transform, "S1_StartPlatform", platFbx, platMat, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 1f, 1.5f), EpicenterPlatformMode.Static, cfg);
            SpawnPlatform(levelRoot.transform, "S1_DashPlatform", platFbx, platMat, new Vector3(10.5f, 0.2f, 0f), new Vector3(1.1f, 1f, 1.1f), EpicenterPlatformMode.Static, cfg);

            var moving = SpawnPlatform(levelRoot.transform, "S1_MovingLinearPlatform", platFbx, platMat, new Vector3(19.5f, 0.4f, 0f), new Vector3(1.0f, 1f, 1.0f), EpicenterPlatformMode.MovingLinear, cfg);
            moving.moveAxis = Vector3.forward;
            moving.moveRange = 3.2f;

            SpawnPlatform(levelRoot.transform, "S1_RotatingPlatform", platFbx, platMat, new Vector3(28.5f, 0.6f, 0f), new Vector3(1.2f, 1f, 1.2f), EpicenterPlatformMode.Rotating, cfg);

            // PROVA DO ESTASE EM PUZZLE: plataforma rapida demais sem Estase (10.5 m/s -> 5.25 m/s em Estase!)
            var fastStasis = SpawnPlatform(levelRoot.transform, "S1_FastStasisPuzzlePlatform", platFbx, platMat, new Vector3(38.0f, 0.8f, 0f), new Vector3(1.1f, 1f, 1.1f), EpicenterPlatformMode.FastStasisPuzzle, cfg);
            fastStasis.moveAxis = Vector3.forward;
            fastStasis.moveRange = 5.0f;

            // === SECAO 2: COMBATE (2 WAVES CURTAS + COBERTURA VERTICAL TRIPOSG, X = 50 .. 90) ===
            var cp2 = CreateCheckpoint(levelRoot.transform, "Checkpoint_S2_Combate", new Vector3(50f, 0.9f, 0f));
            SpawnPlatform(levelRoot.transform, "S2_CombatArena_A", platFbx, platMat, new Vector3(58f, 0.8f, 0f), new Vector3(4.2f, 1.2f, 3.6f), EpicenterPlatformMode.Static, cfg);
            SpawnPlatform(levelRoot.transform, "S2_CombatBridge",  platFbx, platMat, new Vector3(71f, 0.8f, 0f), new Vector3(2.0f, 1.0f, 1.4f), EpicenterPlatformMode.Static, cfg);
            SpawnPlatform(levelRoot.transform, "S2_CombatArena_B", platFbx, platMat, new Vector3(84f, 0.8f, 0f), new Vector3(4.4f, 1.2f, 3.8f), EpicenterPlatformMode.Static, cfg);

            // Coberturas verticais (Monolitos TripoSG)
            SpawnMonolith(levelRoot.transform, "S2_Cover_A1", monoFbx, monoMat, new Vector3(55f, 0.8f,  4.2f), 0.9f);
            SpawnMonolith(levelRoot.transform, "S2_Cover_A2", monoFbx, monoMat, new Vector3(61f, 0.8f, -4.0f), 1.0f);
            SpawnMonolith(levelRoot.transform, "S2_Cover_B1", monoFbx, monoMat, new Vector3(81f, 0.8f,  4.6f), 1.1f);
            SpawnMonolith(levelRoot.transform, "S2_Cover_B2", monoFbx, monoMat, new Vector3(87f, 0.8f, -4.4f), 0.95f);

            // 2 Waves curtas config-driven (reusando CombatEncounterZone + Prefabs TripoSG #94)
            CreateWave(levelRoot.transform, "Epicenter_Wave1_Zone", new Vector3(58f, 0.9f, 0f), estPrefab, fluPrefab, combatCfg, telegraphClip, dissolveClip, new List<EnemySpawnSlot>
            {
                new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3( 3.0f, 0f,  2.2f) },
                new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3( 3.5f, 0f, -2.2f) },
                new EnemySpawnSlot { enemyType = ManifestationType.Flutuante, localOffset = new Vector3( 6.5f, 0f,  0.0f) }
            });
            CreateWave(levelRoot.transform, "Epicenter_Wave2_Zone", new Vector3(84f, 0.9f, 0f), estPrefab, fluPrefab, combatCfg, telegraphClip, dissolveClip, new List<EnemySpawnSlot>
            {
                new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3( 2.8f, 0f,  2.8f) },
                new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3( 3.2f, 0f, -2.6f) },
                new EnemySpawnSlot { enemyType = ManifestationType.Flutuante, localOffset = new Vector3( 6.8f, 0f,  2.0f) },
                new EnemySpawnSlot { enemyType = ManifestationType.Flutuante, localOffset = new Vector3( 6.8f, 0f, -2.0f) }
            });

            // === SECAO 3: CORACAO DA FENDA — ARENA FINAL CIRCULAR (X = 116) ===
            var cp3 = CreateCheckpoint(levelRoot.transform, "Checkpoint_S3_ArenaFinal", new Vector3(102f, 0.9f, 0f));
            SpawnPlatform(levelRoot.transform, "S3_ArenaBridge", platFbx, platMat, new Vector3(99f, 0.8f, 0f), new Vector3(2.2f, 1.0f, 1.6f), EpicenterPlatformMode.Static, cfg);
            SpawnPlatform(levelRoot.transform, "S3_HeartOfRift_Arena", platFbx, platMat, new Vector3(116f, 0.8f, 0f), new Vector3(6.8f, 1.3f, 6.8f), EpicenterPlatformMode.Static, cfg);

            // Anel monumental de 8 monolitos TripoSG ao redor da arena final
            for (int i = 0; i < 8; i++)
            {
                float ang = (i / 8f) * Mathf.PI * 2f;
                Vector3 p = new Vector3(116f + Mathf.Cos(ang) * 14.5f, 0.8f, Mathf.Sin(ang) * 14.5f);
                SpawnMonolith(levelRoot.transform, $"S3_ArenaMonolith_{i + 1}", monoFbx, monoMat, p, 1.45f);
            }

            // Ceu-rachadura gigante luminoso por tras da arena (VFX economico e dramatico)
            var riftSky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            riftSky.name = "S3_GiantRiftSkyCrack";
            UnityEngine.Object.DestroyImmediate(riftSky.GetComponent<Collider>());
            riftSky.transform.SetParent(levelRoot.transform, false);
            riftSky.transform.position = new Vector3(148f, 16f, 0f);
            riftSky.transform.rotation = Quaternion.Euler(0f, -90f, 14f);
            riftSky.transform.localScale = new Vector3(6.5f, 44f, 1f);
            var riftMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (riftMat.HasProperty("_BaseColor")) riftMat.SetColor("_BaseColor", new Color(0.55f, 0.88f, 1.0f, 1f));
            riftSky.GetComponent<Renderer>().sharedMaterial = riftMat;

            // Trigger Stub do Coracao da Fenda (onde #103 pluga o Guardiao da Fenda!)
            var stubGo = new GameObject("S3_HeartOfRift_BossTriggerStub");
            stubGo.transform.SetParent(levelRoot.transform, false);
            stubGo.transform.position = new Vector3(116f, 1.0f, 0f);
            var stub = stubGo.AddComponent<EpicenterArenaBossStub>();
            stub.config = cfg;
            stub.omenDryHitClip = omenClip;

            // Controlador de Checkpoints + Void Kill + Gravidade Instavel
            var cpCtrlGo = new GameObject("Epicenter_CheckpointAndVoidController");
            cpCtrlGo.transform.SetParent(levelRoot.transform, false);
            var cpCtrl = cpCtrlGo.AddComponent<EpicenterCheckpointAndVoidZone>();
            cpCtrl.config = cfg;
            cpCtrl.sectionCheckpoints = new[] { cp1, cp2, cp3 };

            // Posiciona o Dez no inicio da Secao 1
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                dez.transform.position = new Vector3(0f, 0.15f, 0f);
                dez.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            }

            EditorSceneManager.SaveScene(scene);
            EnsureSceneInBuildSettings(TargetScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EpicenterSceneBuilder] Cena Epicentro.unity criada (3 secoes, TripoSG, BuildSettings OK)!");
        }

        private static Transform CreateCheckpoint(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static EpicenterPlatformController SpawnPlatform(
            Transform parent, string name, GameObject fbx, Material mat,
            Vector3 pos, Vector3 scale, EpicenterPlatformMode mode, EpicenterConfig cfg)
        {
            var root = new GameObject(name);
            root.layer = CollisionMatrixBootstrap.LayerEnvironment;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.localScale = scale;

            // BoxCollider plano e limpo no topo (zero enganchamento — DESIGN_BIBLE Secao 2!)
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(4.4f, 0.8f, 4.4f);
            box.center = new Vector3(0f, -0.4f, 0f);

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            vis.name = name + "_TripoSG";
            vis.transform.SetParent(root.transform, false);
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.gameObject.layer = CollisionMatrixBootstrap.LayerEnvironment;
            }

            var ctrl = root.AddComponent<EpicenterPlatformController>();
            ctrl.mode = mode;
            ctrl.config = cfg;
            return ctrl;
        }

        private static void SpawnMonolith(Transform parent, string name, GameObject fbx, Material mat, Vector3 pos, float scale)
        {
            var root = new GameObject(name);
            root.layer = CollisionMatrixBootstrap.LayerProps;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.localScale = Vector3.one * scale;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.8f, 0f);
            col.height = 3.6f;
            col.radius = 0.85f;

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            vis.name = name + "_TripoSG";
            vis.transform.SetParent(root.transform, false);
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.gameObject.layer = CollisionMatrixBootstrap.LayerProps;
            }
        }

        private static void CreateWave(
            Transform parent, string name, Vector3 pos,
            GameObject estPrefab, GameObject fluPrefab, CombatBalanceConfig combatCfg,
            AudioClip telegraphClip, AudioClip dissolveClip, List<EnemySpawnSlot> slots)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var z = go.AddComponent<CombatEncounterZone>();
            z.encounterId = name.ToLowerInvariant();
            z.requireAct2FinalBeat = false;
            z.triggerRadius = 11.0f;
            z.estilhacoPrefab = estPrefab;
            z.flutuantePrefab = fluPrefab;
            z.balanceConfig = combatCfg;
            z.telegraphClip = telegraphClip;
            z.dissolveReverseClip = dissolveClip;
            z.waveSlots = slots;
        }

        private static Material BuildPbrMat(string slug, Color emFallback)
        {
            string matPath = $"Assets/Game/Materials/Fab3D/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_albedo.png");
            var met = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_metallicsmooth.png");
            var emi = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_emission.png");
            if (alb != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", alb);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (met != null && mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (emi != null && mat.HasProperty("_EmissionMap")) { mat.SetTexture("_EmissionMap", emi); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.white * 1.9f); }
            else if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emFallback * 0.7f); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < list.Count; i++)
                if (list[i].path == scenePath) { list[i].enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
