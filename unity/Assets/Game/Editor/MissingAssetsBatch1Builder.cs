#if UNITY_EDITOR
using System.IO;
using Studio.Game.Parts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Builder do Lote 1 de modelos 3D faltantes (exclusivamente TripoSG + PBR 4K):
    ///   - Cria materiais URP Lit (Mat_Fab3D_<slug>.mat)
    ///   - Cria prefabs com escala normalizada e collider adequado em Assets/Game/Prefabs/Fab3D/
    ///   - Atualiza as pecas 4 e 5 e a estacao de parry na cena Caldeiral.unity.
    /// </summary>
    public static class MissingAssetsBatch1Builder
    {
        private struct AssetSpec
        {
            public string Slug;
            public string FbxPath;
            public float TargetHeight;
            public bool TriggerCollider;
        }

        private static readonly AssetSpec[] Specs =
        {
            new AssetSpec { Slug = "part_condensador_fluxo",     FbxPath = "Assets/Game/Art/Generated/parts/part_condensador_fluxo.fbx",         TargetHeight = 0.44f, TriggerCollider = true  },
            new AssetSpec { Slug = "part_estabilizador_orbital", FbxPath = "Assets/Game/Art/Generated/parts/part_estabilizador_orbital.fbx",     TargetHeight = 0.44f, TriggerCollider = true  },
            new AssetSpec { Slug = "prop_fragmento_nucleo",      FbxPath = "Assets/Game/Art/Generated/epicenter/prop_fragmento_nucleo.fbx",      TargetHeight = 0.58f, TriggerCollider = true  },
            new AssetSpec { Slug = "prop_celula_energia",        FbxPath = "Assets/Game/Art/Generated/props/prop_celula_energia.fbx",            TargetHeight = 0.34f, TriggerCollider = true  },
            new AssetSpec { Slug = "prop_estacao_parry",         FbxPath = "Assets/Game/Art/Generated/props/prop_estacao_parry.fbx",             TargetHeight = 1.28f, TriggerCollider = false },
        };

        [MenuItem("CONCERTO/Assets/Build Missing Assets Batch 1 (5 TripoSG Models)")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string matDir = "Assets/Game/Materials/Fab3D";
            const string prefDir = "Assets/Game/Prefabs/Fab3D";
            Directory.CreateDirectory(prefDir);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (var spec in Specs)
            {
                // 1. Material URP Lit PBR
                string matPath = $"{matDir}/Mat_Fab3D_{spec.Slug}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(urpLit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = urpLit;
                }

                var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{matDir}/{spec.Slug}_albedo.png");
                var em  = AssetDatabase.LoadAssetAtPath<Texture2D>($"{matDir}/{spec.Slug}_emission.png");
                var ms  = AssetDatabase.LoadAssetAtPath<Texture2D>($"{matDir}/{spec.Slug}_metallicsmooth.png");

                if (alb != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", alb);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (ms != null && mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", ms);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.72f);
                }
                if (em != null && mat.HasProperty("_EmissionMap"))
                {
                    mat.SetTexture("_EmissionMap", em);
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white * 1.85f);
                }
                EditorUtility.SetDirty(mat);

                // 2. Prefab com escala normalizada + collider adequado
                var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
                if (fbxPrefab == null)
                {
                    Debug.LogError($"[MissingAssetsBatch1] FBX nao encontrado: {spec.FbxPath}");
                    continue;
                }

                var root = new GameObject(spec.Slug);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);

                var rends = visual.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rends.Length; i++)
                    rends[i].sharedMaterial = mat;

                // Multiplica a escala nativa do FBX (preserva o fator 100x do Blender cm->m!) por TargetHeight
                visual.transform.localScale = visual.transform.localScale * spec.TargetHeight;
                visual.transform.localPosition = new Vector3(0f, spec.TargetHeight * 0.5f, 0f);

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(spec.TargetHeight, spec.TargetHeight, spec.TargetHeight);
                box.center = new Vector3(0f, spec.TargetHeight * 0.5f, 0f);
                box.isTrigger = spec.TriggerCollider;

                string prefPath = $"{prefDir}/{spec.Slug}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefPath);
                Object.DestroyImmediate(root);
                Debug.Log($"[MissingAssetsBatch1] PREFAB OK: {prefPath} (h={spec.TargetHeight:F2}m, mat={matPath})");
            }

            // 3. Atualiza part_04 e part_05 na cena Caldeiral.unity para usarem os novos modelos TripoSG!
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Caldeiral.unity", OpenSceneMode.Single);
            var partsMgr = Object.FindAnyObjectByType<PartsLoopManager>();
            if (partsMgr != null && partsMgr.sceneParts != null)
            {
                ReplacePartVisual(partsMgr, 3, $"{prefDir}/part_condensador_fluxo.prefab");
                ReplacePartVisual(partsMgr, 4, $"{prefDir}/part_estabilizador_orbital.prefab");
            }

            // Atualiza visual do ParryTeachStation se existir em Caldeiral
            var parryStation = GameObject.Find("ParryTeachStation");
            var parryPref = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefDir}/prop_estacao_parry.prefab");
            if (parryStation != null && parryPref != null)
            {
                var oldMr = parryStation.GetComponent<MeshRenderer>();
                if (oldMr != null) oldMr.enabled = false;
                var existingVis = parryStation.transform.Find("TripoSG_EstacaoParry_Visual");
                if (existingVis != null) Object.DestroyImmediate(existingVis.gameObject);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(parryPref);
                inst.name = "TripoSG_EstacaoParry_Visual";
                inst.transform.SetParent(parryStation.transform, false);
                inst.transform.localPosition = Vector3.zero;
                var extraCol = inst.GetComponent<Collider>();
                if (extraCol != null) Object.DestroyImmediate(extraCol);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MissingAssetsBatch1] TODOS OS 5 MODELOS IMPORTADOS E INTEGRADOS COM SUCESSO!");
        }

        private static void ReplacePartVisual(PartsLoopManager mgr, int idx, string prefabPath)
        {
            if (idx < 0 || idx >= mgr.sceneParts.Count || mgr.sceneParts[idx] == null) return;
            var pref = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pref == null) return;
            var host = mgr.sceneParts[idx].transform;
            for (int c = host.childCount - 1; c >= 0; c--)
            {
                var child = host.GetChild(c);
                if (child.GetComponent<Light>() != null) continue;
                Object.DestroyImmediate(child.gameObject);
            }
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(pref);
            vis.name = "TripoSG_Visual";
            vis.transform.SetParent(host, false);
            vis.transform.localPosition = Vector3.zero;
            var extraCol = vis.GetComponent<Collider>();
            if (extraCol != null) Object.DestroyImmediate(extraCol);
            Debug.Log($"[MissingAssetsBatch1] Peca slot {idx + 1} atualizada com {prefabPath}");
        }
    }
}
#endif
