#if UNITY_EDITOR
using System;
using System.IO;
using Studio.Game.Collision;
using Studio.Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.Editor
{
    public static class CombatV2EditorSetup
    {
        private const string EgideFbxPath = "Assets/Game/Art/Generated/enemies/enemy_egide.fbx";
        private const string EgidePrefab  = "Assets/Game/Prefabs/Enemies/Enemy_Egide.prefab";

        [MenuItem("Concerto/Combat v2 (#109)/Build Egide (TripoSG), Wire Parry/LockOn & Teach Station")]
        public static void RunSetup()
        {
            Directory.CreateDirectory("Assets/Game/Prefabs/Enemies");
            AssetDatabase.Refresh();

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(EgideFbxPath);
            if (fbx == null)
                throw new InvalidOperationException($"REGRA INVIOLAVEL TRIPOSG: FBX do Egide ausente em {EgideFbxPath}!");

            var mat = BuildPbrMat("enemy_egide", new Color(0.28f, 0.85f, 1.0f));
            var combatCfg  = AssetDatabase.LoadAssetAtPath<CombatBalanceConfig>("Assets/Game/Config/CombatBalanceConfig.asset");
            var teleClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_telegraph_TEMP.wav");
            var dissClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
            var clangClip  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/caldeiral_sfx/contratempo_parry_clang.wav");
            var revClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");

            var root = new GameObject("Enemy_Egide");
            root.layer = CollisionMatrixBootstrap.LayerEnemy;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.925f, 0f);
            col.height = 1.85f;
            col.radius = 0.62f;

            var sep = root.AddComponent<ActorSolidSeparation>();
            sep.bodyKind = ActorBodyKind.Enemy;
            sep.solidRadius = 0.62f;
            sep.solidHeight = 1.85f;

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            vis.name = "enemy_egide_TripoSG_Visual";
            vis.transform.SetParent(root.transform, false);
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.gameObject.layer = CollisionMatrixBootstrap.LayerEnemy;
            }

            var eg = root.AddComponent<EgideEnemy>();
            eg.config = combatCfg;
            eg.telegraphClip = teleClip;
            eg.dissolveReverseClip = dissClip;
            eg.ricochetClangClip = clangClip;

            var egPrefab = PrefabUtility.SaveAsPrefabAsset(root, EgidePrefab);
            UnityEngine.Object.DestroyImmediate(root);

            WireScene("Assets/Game/Scenes/Caldeiral.unity", clangClip, revClip, egPrefab, addTeachStation: true);
            WireScene("Assets/Game/Scenes/SetorExterno.unity", clangClip, revClip, egPrefab, addTeachStation: false);
            WireScene("Assets/Game/Scenes/Epicentro.unity", clangClip, revClip, egPrefab, addTeachStation: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CombatV2EditorSetup] Setup #109 (Egide TripoSG + ParryAndLockOn + ParryTeachStation) concluido!");
        }

        private static void WireScene(string scenePath, AudioClip clangClip, AudioClip revClip, GameObject egPrefab, bool addTeachStation)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                var p = dez.GetComponent<ParryAndLockOnController>();
                if (p == null) p = dez.AddComponent<ParryAndLockOnController>();
                p.parryClangClip = clangClip;
                p.parryReverseClip = revClip;
                EditorUtility.SetDirty(p);
            }

            var zones = UnityEngine.Object.FindObjectsByType<CombatEncounterZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var z in zones)
            {
                z.egidePrefab = egPrefab;
                EditorUtility.SetDirty(z);
            }

            if (addTeachStation)
            {
                var stGo = GameObject.Find("TeachStation_Absorver_SafeSpace");
                if (stGo == null)
                {
                    stGo = new GameObject("TeachStation_Absorver_SafeSpace");
                    stGo.transform.position = new Vector3(-28.0f, 0.1f, -13.5f);
                }
                if (stGo.GetComponent<ParryTeachStation>() == null)
                    stGo.AddComponent<ParryTeachStation>();
                EditorUtility.SetDirty(stGo);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CombatV2EditorSetup] Cena {scenePath} atualizada.");
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
            if (emi != null && mat.HasProperty("_EmissionMap")) { mat.SetTexture("_EmissionMap", emi); mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.white * 1.85f); }
            else if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emFallback * 0.7f); }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
