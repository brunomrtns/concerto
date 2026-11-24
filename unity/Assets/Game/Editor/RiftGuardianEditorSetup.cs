#if UNITY_EDITOR
using System;
using System.IO;
using Studio.Game.Boss;
using Studio.Game.Collision;
using Studio.Game.Combat;
using Studio.Game.Epicenter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.Editor
{
    public static class RiftGuardianEditorSetup
    {
        private const string ConfigPath   = "Assets/Game/Config/RiftGuardianConfig.asset";
        private const string BossFbxPath  = "Assets/Game/Art/Generated/boss/boss_guardiao_fenda.fbx";
        private const string BossPrefab   = "Assets/Game/Prefabs/Enemies/Boss_Guardiao_Da_Fenda.prefab";
        private const string ScenePath    = "Assets/Game/Scenes/Epicentro.unity";

        [MenuItem("Concerto/Boss (#103)/Build Guardiao da Fenda (TripoSG) & Wire Epicentro")]
        public static void RunSetup()
        {
            Directory.CreateDirectory("Assets/Game/Config");
            Directory.CreateDirectory("Assets/Game/Prefabs/Enemies");
            AssetDatabase.Refresh();

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BossFbxPath);
            if (fbx == null)
                throw new InvalidOperationException($"REGRA INVIOLAVEL TRIPOSG: FBX do Boss ausente em {BossFbxPath}!");

            var cfg = AssetDatabase.LoadAssetAtPath<RiftGuardianConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<RiftGuardianConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                EditorUtility.SetDirty(cfg);
            }

            var mat = BuildPbrMat("boss_guardiao_fenda", new Color(0.28f, 0.90f, 1.0f));
            var estPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/Enemy_Estilhaco.prefab");
            var combatCfg   = AssetDatabase.LoadAssetAtPath<CombatBalanceConfig>("Assets/Game/Config/CombatBalanceConfig.asset");
            var holoTex     = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Textures/Cutscenes/cs01_foto_dez_esposa.png");
            var bossMusic   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Music/concerto_bgm/caldeiral_combat_contratempo.ogg");
            var teleClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_telegraph_TEMP.wav");
            var slamClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Boss/sfx_boss_slam_TEMP.wav");
            var dissClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
            var holoClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Boss/sfx_hologram_flash_TEMP.wav");

            var root = new GameObject("Boss_Guardiao_Da_Fenda");
            root.layer = CollisionMatrixBootstrap.LayerEnemy;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.6f, 0f);
            col.height = 3.2f;
            col.radius = 1.05f;

            var sep = root.AddComponent<ActorSolidSeparation>();
            sep.bodyKind = ActorBodyKind.Enemy;
            sep.solidRadius = 1.05f;
            sep.solidHeight = 3.2f;

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            vis.name = "boss_guardiao_fenda_TripoSG_Visual";
            vis.transform.SetParent(root.transform, false);
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.gameObject.layer = CollisionMatrixBootstrap.LayerEnemy;
            }

            var ctrl = root.AddComponent<RiftGuardianBossController>();
            ctrl.config = cfg;
            ctrl.minionBalanceConfig = combatCfg;
            ctrl.estilhacoPrefab = estPrefab;
            ctrl.hologramWorkshopWifeTexture = holoTex;
            ctrl.bossMusicClip = bossMusic;
            ctrl.telegraphClip = teleClip;
            ctrl.slamClip = slamClip;
            ctrl.dissolveClip = dissClip;
            ctrl.hologramFlashClip = holoClip;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
            UnityEngine.Object.DestroyImmediate(root);

            // Pluga o Boss na Arena Final do Epicentro (S3_HeartOfRift_BossTriggerStub)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var existing = GameObject.Find("Boss_Guardiao_Da_Fenda");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Boss_Guardiao_Da_Fenda";
            inst.transform.position = new Vector3(122.5f, 0.9f, 0f);
            inst.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            var stub = UnityEngine.Object.FindAnyObjectByType<EpicenterArenaBossStub>();
            var bCtrl = inst.GetComponent<RiftGuardianBossController>();
            if (bCtrl != null)
            {
                bCtrl.arenaStub = stub;
                bCtrl.arenaCenter = new Vector3(116f, 0.9f, 0f);
                bCtrl.arenaRetryCheckpoint = new Vector3(104f, 1.05f, 0f);
                EditorUtility.SetDirty(bCtrl);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RiftGuardianEditorSetup] Boss TripoSG criado e plugado na Arena Final de Epicentro.unity!");
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
            if (emi != null && mat.HasProperty("_EmissionMap")) { mat.SetTexture("_EmissionMap", emi); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.white * 2.1f); }
            else if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emFallback * 0.8f); }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
