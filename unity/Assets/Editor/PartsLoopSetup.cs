using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Monta o loop das pecas do Ato 1 em Caldeiral.unity:
    ///   - Importa os 3 modelos 3D mecanicos (TripoSG + PBR 4K + emissao ciano)
    ///   - Instancia as N pecas (default 5) nos pontos da Oficina Z1 definidos em parts_loop_config.json
    ///   - Anexa DiegeticChecklistBoard no prop fisico CS01_Quadro_Checklist_Pecas
    ///   - Instala PartsLoopManager ligado ao DialogueController, ObjectiveSystem e NPC_Juno_Oficina
    /// </summary>
    public static class PartsLoopSetup
    {
        private const string ScenePath     = "Assets/Game/Scenes/Caldeiral.unity";
        private const string PartsArtDir   = "Assets/Game/Art/Generated/parts";
        private const string Fab3dMatDir   = "Assets/Game/Materials/Fab3D";
        private const string ConfigPath    = "Assets/Game/Parts/Data/parts_loop_config.json";
        private const string IntroDlgPath  = "Assets/Game/Dialogue/Data/dlg_juno_oficina_intro.json";
        private const string DelivDlgPath  = "Assets/Game/Dialogue/Data/dlg_juno_entrega_pecas.json";
        private const string SfxDir        = "Assets/Game/Audio/SFX/Parts";

        [Serializable]
        private class PartCfgItem { public string id; public string displayName; public string prefabSlug; public Vector3 position; }
        [Serializable]
        private class PartCfgFile { public int requiredCount = 5; public List<PartCfgItem> parts = new List<PartCfgItem>(); }

        [MenuItem("Studio/Concerto/Parts/Setup Act 1 Parts Loop in Caldeiral")]
        public static void SetupMenu() => Setup();

        public static void SetupBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PartsLoopSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Setup()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var cfgAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (cfgAsset == null) throw new FileNotFoundException(ConfigPath);
            var cfg = JsonUtility.FromJson<PartCfgFile>(cfgAsset.text);

            // 1. Prepara materiais PBR + emissao ciano das 3 pecas TripoSG
            var mats = new Dictionary<string, Material>();
            foreach (string slug in new[] { "part_nucleo_ressonancia", "part_bobina_fase", "part_regulador_crono" })
            {
                mats[slug] = EnsurePartMaterial(slug);
            }

            // 2. Anexa DiegeticChecklistBoard no prop fisico CS01_Quadro_Checklist_Pecas
            var boardGo = GameObject.Find("CS01_Quadro_Checklist_Pecas");
            if (boardGo == null) throw new Exception("CS01_Quadro_Checklist_Pecas nao encontrado na cena!");
            var boardType = Type.GetType("Studio.Game.Parts.DiegeticChecklistBoard, Assembly-CSharp");
            var boardComp = boardGo.GetComponent(boardType);
            if (boardComp == null) boardComp = boardGo.AddComponent(boardType);

            // 3. Container idempotente para as pecas + PartsLoopManager
            var oldRoot = GameObject.Find("Act1_Parts_Loop_System");
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            var root = new GameObject("Act1_Parts_Loop_System");

            var pickupType = Type.GetType("Studio.Game.Parts.MachinePartPickup, Assembly-CSharp");
            var mgrType    = Type.GetType("Studio.Game.Parts.PartsLoopManager, Assembly-CSharp");
            var npcType    = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");

            var spawnedPickups = new List<Component>();
            for (int i = 0; i < cfg.parts.Count; i++)
            {
                var p = cfg.parts[i];
                var pGo = new GameObject($"PartPickup_{p.id}");
                pGo.transform.SetParent(root.transform, false);
                pGo.transform.position = p.position;

                string fbxPath = $"{PartsArtDir}/{p.prefabSlug}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) throw new FileNotFoundException($"FBX TripoSG da peca nao encontrado: {fbxPath}");

                var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx, pGo.transform);
                vis.name = "Mesh";
                var mat = mats.ContainsKey(p.prefabSlug) ? mats[p.prefabSlug] : null;
                foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
                    if (mat != null) r.sharedMaterial = mat;

                Bounds b = ProceduralMeshLibrary.ComputeBounds(vis);
                float maxD = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxD > 0.001f) vis.transform.localScale = Vector3.one * (0.42f / maxD);

                // Brilho sutil local para destacar a peca com exploracao justa
                var glowGo = new GameObject("GlowLight");
                glowGo.transform.SetParent(pGo.transform, false);
                var l = glowGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.25f, 0.92f, 1.0f);
                l.intensity = 2.8f;
                l.range = 2.4f;
                l.shadows = LightShadows.None;

                var sc = pGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.55f;

                var comp = pGo.AddComponent(pickupType);
                var soP = new SerializedObject(comp);
                soP.FindProperty("partId").stringValue = p.id;
                soP.FindProperty("displayName").stringValue = p.displayName;
                soP.FindProperty("slotIndex").intValue = i;
                soP.ApplyModifiedPropertiesWithoutUndo();

                spawnedPickups.Add(comp);
            }

            // AudioSource para SFX de pickup e stinger de conclusao
            var sfxSrc = root.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.spatialBlend = 0f;

            var junoGo = GameObject.Find("NPC_Juno_Oficina");
            var junoNpcComp = junoGo != null ? junoGo.GetComponent(npcType) : null;

            var mgrComp = root.AddComponent(mgrType);
            var soMgr = new SerializedObject(mgrComp);
            soMgr.FindProperty("partsConfigJson").objectReferenceValue = cfgAsset;
            soMgr.FindProperty("junoIntroDialogueJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(IntroDlgPath);
            soMgr.FindProperty("junoDeliverDialogueJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(DelivDlgPath);
            soMgr.FindProperty("diegeticBoard").objectReferenceValue = boardComp;
            soMgr.FindProperty("junoNpc").objectReferenceValue = junoNpcComp;
            soMgr.FindProperty("sfxSource").objectReferenceValue = sfxSrc;
            soMgr.FindProperty("pickupChimeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/sfx_part_pickup_TEMP.wav");
            soMgr.FindProperty("act1CompleteStingerClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/sfx_act1_complete_stinger_TEMP.wav");

            var listProp = soMgr.FindProperty("sceneParts");
            listProp.ClearArray();
            listProp.arraySize = spawnedPickups.Count;
            for (int i = 0; i < spawnedPickups.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedPickups[i];
            soMgr.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[PartsLoopSetup] OK! {spawnedPickups.Count} pecas TripoSG + DiegeticChecklistBoard + PartsLoopManager instalados em Caldeiral.unity!");
        }

        private static Material EnsurePartMaterial(string slug)
        {
            string albPath = $"{Fab3dMatDir}/{slug}_albedo.png";
            string msPath  = $"{Fab3dMatDir}/{slug}_metallicsmooth.png";
            string emPath  = $"{Fab3dMatDir}/{slug}_emission.png";
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(albPath);
            var ms  = AssetDatabase.LoadAssetAtPath<Texture2D>(msPath);
            var em  = AssetDatabase.LoadAssetAtPath<Texture2D>(emPath);
            mat.SetTexture("_BaseMap", alb);
            mat.SetColor("_BaseColor", Color.white);
            if (ms != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 0.72f);
            }
            if (em != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", em);
                mat.SetColor("_EmissionColor", new Color(0.25f, 3.2f, 3.6f));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
