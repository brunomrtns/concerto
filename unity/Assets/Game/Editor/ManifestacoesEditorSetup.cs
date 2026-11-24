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
    public static class ManifestacoesEditorSetup
    {
        private const string ConfigPath   = "Assets/Game/Config/CombatBalanceConfig.asset";
        private const string PrefabsDir   = "Assets/Game/Prefabs/Enemies";
        private const string EstPrefab    = PrefabsDir + "/Enemy_Estilhaco.prefab";
        private const string FluPrefab    = PrefabsDir + "/Enemy_Flutuante.prefab";

        [MenuItem("Concerto/Manifestacoes (#94)/Build TripoSG Prefabs, Config & Wire Scenes")]
        public static void RunSetup()
        {
            Directory.CreateDirectory("Assets/Game/Config");
            Directory.CreateDirectory(PrefabsDir);
            AssetDatabase.Refresh();

            var cfg = AssetDatabase.LoadAssetAtPath<CombatBalanceConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<CombatBalanceConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                EditorUtility.SetDirty(cfg);
            }

            var telegraphClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_telegraph_TEMP.wav");
            var dissolveClip  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_enemy_dissolve_reverse_TEMP.wav");
            var hurtClip      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Combat/sfx_player_hurt_TEMP.wav");

            var estPrefab = BuildTripoSgEnemyPrefab(
                "enemy_estilhaco",
                "Assets/Game/Art/Generated/enemies/enemy_estilhaco.fbx",
                EstPrefab,
                typeof(EstilhacoEnemy),
                cfg, telegraphClip, dissolveClip,
                new Color(0.25f, 0.88f, 1.0f),
                colliderHeight: 1.45f, colliderRadius: 0.48f);

            var fluPrefab = BuildTripoSgEnemyPrefab(
                "enemy_flutuante",
                "Assets/Game/Art/Generated/enemies/enemy_flutuante.fbx",
                FluPrefab,
                typeof(FlutuanteEnemy),
                cfg, telegraphClip, dissolveClip,
                new Color(0.82f, 0.25f, 1.0f),
                colliderHeight: 1.50f, colliderRadius: 0.55f);

            WireScene("Assets/Game/Scenes/Caldeiral.unity", cfg, hurtClip, estPrefab, fluPrefab, telegraphClip, dissolveClip, addEncounterZone: true);
            WireScene("Assets/Game/Scenes/SetorExterno.unity", cfg, hurtClip, estPrefab, fluPrefab, telegraphClip, dissolveClip, addEncounterZone: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ManifestacoesEditorSetup] Setup #94 (Estilhaco + Flutuante TripoSG + PlayerHealth + EncounterZone) concluido!");
        }

        private static GameObject BuildTripoSgEnemyPrefab(
            string slug,
            string fbxPath,
            string prefabPath,
            Type enemyComponentType,
            CombatBalanceConfig cfg,
            AudioClip telegraphClip,
            AudioClip dissolveClip,
            Color emissionFallback,
            float colliderHeight,
            float colliderRadius)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
                throw new InvalidOperationException($"REGRA INVIOLAVEL TRIPOSG: FBX ausente em {fbxPath}!");

            var mat = BuildPbrMaterial(slug, emissionFallback);

            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            root.layer = CollisionMatrixBootstrap.LayerEnemy;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            col.height = colliderHeight;
            col.radius = colliderRadius;

            var sep = root.AddComponent<ActorSolidSeparation>();
            sep.bodyKind = ActorBodyKind.Enemy;
            sep.solidRadius = colliderRadius;
            sep.solidHeight = colliderHeight;

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            visual.name = slug + "_TripoSG_Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.gameObject.layer = CollisionMatrixBootstrap.LayerEnemy;
            }

            var comp = root.AddComponent(enemyComponentType) as ManifestationEnemyBase;
            if (comp != null)
            {
                comp.config = cfg;
                comp.telegraphClip = telegraphClip;
                comp.dissolveReverseClip = dissolveClip;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[ManifestacoesEditorSetup] Prefab TripoSG criado: {prefabPath}");
            return prefab;
        }

        private static Material BuildPbrMaterial(string slug, Color emissionFallback)
        {
            string matPath = $"Assets/Game/Materials/Fab3D/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_albedo.png");
            var metal  = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_metallicsmooth.png");
            var emiss  = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Materials/Fab3D/{slug}_emission.png");

            if (albedo != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (metal != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metal);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (emiss != null && mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emiss);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 1.85f);
            }
            else if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionFallback * 0.65f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void WireScene(
            string scenePath,
            CombatBalanceConfig cfg,
            AudioClip hurtClip,
            GameObject estPrefab,
            GameObject fluPrefab,
            AudioClip telegraphClip,
            AudioClip dissolveClip,
            bool addEncounterZone)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                var hp = dez.GetComponent<PlayerHealthController>();
                if (hp == null) hp = dez.AddComponent<PlayerHealthController>();
                hp.config = cfg;
                hp.hurtClip = hurtClip;
                EditorUtility.SetDirty(hp);
            }

            if (addEncounterZone)
            {
                var zoneGo = GameObject.Find("CombatZone_Act2_Manifestacoes");
                if (zoneGo == null)
                {
                    zoneGo = new GameObject("CombatZone_Act2_Manifestacoes");
                    zoneGo.transform.position = new Vector3(-24.0f, 0.05f, -18.0f);
                }
                var zone = zoneGo.GetComponent<CombatEncounterZone>();
                if (zone == null) zone = zoneGo.AddComponent<CombatEncounterZone>();
                zone.encounterId = "caldeiral_act2_first_manifestations";
                zone.requireAct2FinalBeat = true;
                zone.estilhacoPrefab = estPrefab;
                zone.flutuantePrefab = fluPrefab;
                zone.balanceConfig = cfg;
                zone.telegraphClip = telegraphClip;
                zone.dissolveReverseClip = dissolveClip;
                EditorUtility.SetDirty(zone);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ManifestacoesEditorSetup] Cena {scenePath} atualizada.");
        }
    }
}
#endif
