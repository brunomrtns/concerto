using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Importa os 5 NPCs alienigenas inteligentes (TripoSG + PBR 4K + UniRig Humanoid),
    /// arranca TODOS os "palitoes" rigidos da cena (NPC_*_Placeholder visuais e E1..E4_Ferrugem_*_Placeholder)
    /// e instala os 5 alienigenas com rotinas vivas (AlienNpcRoutineController + Clean_Walk / Clean_Idle).
    /// </summary>
    public static class CaldeiralAlienNpcsSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Caldeiral.unity";
        private const string NpcArtDir = "Assets/Game/Art/Generated/npcs";
        private const string MatDir    = "Assets/Game/Materials/Fab3D";
        private const string PrefabDir = "Assets/Game/Prefabs/Fab3D";
        private const string CtrlPath  = "Assets/Game/Animations/HeroDezController.controller";

        private struct StopDef
        {
            public string activity;
            public Vector3 pos;
            public Vector3 look;
            public float dwell;
        }

        private static readonly string[] Slugs =
        {
            "npc_juno",
            "npc_mecanico_brito",
            "npc_tio_horario",
            "npc_vo_segunda",
            "npc_analista_luma",
        };

        [MenuItem("Studio/Concerto/Caldeiral/Build & Place 5 Alien NPCs (Replace Stick Figures)")]
        public static void ApplyMenu() => Apply();

        public static void ApplyBatch()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralAlienNpcs] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null) throw new FileNotFoundException($"AnimatorController nao encontrado: {CtrlPath}");

            // 1. Configura Humanoid Avatar + Material PBR + Prefab para cada um dos 5 alienigenas
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string slug in Slugs)
            {
                string fbxPath = $"{NpcArtDir}/{slug}/{slug}_humanoid.fbx";
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), fbxPath)))
                {
                    Debug.LogWarning($"[CaldeiralAlienNpcs] FBX ainda nao encontrado, pulando: {fbxPath}");
                    continue;
                }
                var avatar = ConfigureHumanoidFbx(fbxPath);
                var mat    = EnsureNpcMaterial(slug);
                var prefab = CreateNpcPrefab(slug, fbxPath, avatar, mat, ctrl);
                prefabs[slug] = prefab;
            }

            if (prefabs.Count == 0)
                throw new Exception("Nenhum FBX de NPC encontrado para montar na cena!");

            // 2. Abre Caldeiral.unity e arranca TODOS os palitoes gigantes!
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            string[] stickEnemies =
            {
                "E1_Ferrugem_Trabalhadora_Placeholder",
                "E2_Ferrugem_Espelho_Placeholder",
                "E3_Ferrugem_Coletora_Placeholder",
                "E4_Ferrugem_Trabalhadora_Placeholder",
            };
            int removedSticks = 0;
            foreach (string sName in stickEnemies)
            {
                var go = GameObject.Find(sName);
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    removedSticks++;
                }
            }

            // Substitui visual rigido de NPC_Tio_Horario_Placeholder (mantendo InteractableBase!)
            if (prefabs.ContainsKey("npc_tio_horario"))
            {
                ReplaceExistingNpcVisual("NPC_Tio_Horario_Placeholder", prefabs["npc_tio_horario"], targetHeight: 1.32f, displayName: "Tio Horario", stationary: true);
            }
            // Substitui visual rigido de NPC_Vo_Segunda_Placeholder (mantendo InteractableBase!)
            if (prefabs.ContainsKey("npc_vo_segunda"))
            {
                ReplaceExistingNpcVisual("NPC_Vo_Segunda_Placeholder", prefabs["npc_vo_segunda"], targetHeight: 1.26f, displayName: "Vo Segunda", stationary: true);
            }

            // 3. Container limpo para os 3 NPCs alienigenas com rotinas vivas andando pela Oficina & Passarela!
            var oldRoot = GameObject.Find("Caldeiral_Living_Alien_NPCs");
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            var npcRoot = new GameObject("Caldeiral_Living_Alien_NPCs").transform;

            // A) JUNO — Aprendiz Tecnica de Nave (Oficina Z1: Nave -> Estante de Cristais -> Bancada!)
            if (prefabs.ContainsKey("npc_juno"))
            {
                SpawnRoamingNpc(
                    npcRoot, "NPC_Juno_Oficina", prefabs["npc_juno"], targetHeight: 1.18f,
                    displayName: "Juno (Aprendiz de Nave)",
                    startPos: new Vector3(-27.3f, 0.02f, -19.5f),
                    walkSpeed: 1.35f,
                    stops: new[]
                    {
                        new StopDef { activity = "Inspecionando motor da nave",   pos = new Vector3(-27.3f, 0.02f, -19.5f), look = new Vector3(-26.0f, 0.90f, -21.2f), dwell = 6.0f },
                        new StopDef { activity = "Checando cristais na estante",  pos = new Vector3(-29.0f, 0.02f, -13.9f), look = new Vector3(-29.0f, 1.50f, -12.6f), dwell = 5.0f },
                        new StopDef { activity = "Trabalhando na bancada",        pos = new Vector3(-30.1f, 0.02f, -17.5f), look = new Vector3(-29.0f, 1.00f, -17.5f), dwell = 4.5f },
                    }
                );
            }

            // B) BRITO — Tecnico de Turbinas (Circula entre a Nave na Oficina Z1, o Portal e a Passarela Z2!)
            if (prefabs.ContainsKey("npc_mecanico_brito"))
            {
                SpawnRoamingNpc(
                    npcRoot, "NPC_Brito_Tecnico", prefabs["npc_mecanico_brito"], targetHeight: 1.30f,
                    displayName: "Brito (Tecnico de Turbinas)",
                    startPos: new Vector3(-24.2f, 0.02f, -19.2f),
                    walkSpeed: 1.25f,
                    stops: new[]
                    {
                        new StopDef { activity = "Checando diagnostico da nave",        pos = new Vector3(-24.2f, 0.02f, -19.2f), look = new Vector3(-26.0f, 0.85f, -21.2f), dwell = 5.5f },
                        new StopDef { activity = "Inspecionando pilares do portal",     pos = new Vector3(-21.2f, 0.02f, -18.0f), look = new Vector3(-22.7f, 1.60f, -20.5f), dwell = 4.0f },
                        new StopDef { activity = "Verificando tubulacao Passarela Z2",  pos = new Vector3(-15.5f, 0.02f, -18.2f), look = new Vector3(-13.8f, 1.20f, -20.5f), dwell = 5.5f },
                    }
                );
            }

            // C) LUMA — Inspetora de Energia (Patrulha e inspeciona a Passarela Z2!)
            if (prefabs.ContainsKey("npc_analista_luma"))
            {
                SpawnRoamingNpc(
                    npcRoot, "NPC_Luma_Inspetora", prefabs["npc_analista_luma"], targetHeight: 1.28f,
                    displayName: "Luma (Inspetora de Energia)",
                    startPos: new Vector3(-10.5f, 0.02f, -21.5f),
                    walkSpeed: 1.38f,
                    stops: new[]
                    {
                        new StopDef { activity = "Escaneando painel sul Z2",       pos = new Vector3(-10.5f, 0.02f, -21.5f), look = new Vector3(-9.0f,  1.50f, -24.6f), dwell = 5.5f },
                        new StopDef { activity = "Inspecionando passagem leste Z2", pos = new Vector3(-4.2f,  0.02f, -18.0f), look = new Vector3(-0.7f,  1.50f, -18.0f), dwell = 5.0f },
                        new StopDef { activity = "Consultando Tio Horario",         pos = new Vector3(-12.8f, 0.02f, -19.2f), look = new Vector3(-13.8f, 1.10f, -20.5f), dwell = 4.5f },
                    }
                );
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 4. Captura fotos de verificacao na Oficina Z1 (Juno + Brito + Nave + Cristais) e Passarela Z2 (Tio Horario + Luma)!
            CapturePreview(
                new Vector3(-33.5f, 2.2f, -21.8f),
                new Vector3(-26.8f, 1.0f, -18.5f),
                "caldeiral_z1_alien_npcs.png"
            );
            CapturePreview(
                new Vector3(-16.5f, 2.8f, -23.0f),
                new Vector3(-11.5f, 1.1f, -19.8f),
                "caldeiral_z2_alien_npcs.png"
            );

            Debug.Log($"[CaldeiralAlienNpcs] OK! Installed {prefabs.Count} alien NPCs (TripoSG+PBR+UniRig), removed {removedSticks} stick-figure placeholders.");
        }

        private static Avatar ConfigureHumanoidFbx(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) throw new FileNotFoundException(fbxPath);

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();

            var hd = imp.humanDescription;
            if (hd.human != null && hd.human.Length > 0)
            {
                hd.human = hd.human.Where(h =>
                    h.humanName != "LeftEye" &&
                    h.humanName != "RightEye" &&
                    h.humanName != "UpperChest").ToArray();
                imp.humanDescription = hd;
                imp.SaveAndReimport();
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new Exception($"Avatar Humanoid invalido em {fbxPath}!");
            return avatar;
        }

        private static Material EnsureNpcMaterial(string slug)
        {
            string albPath = $"{MatDir}/{slug}_albedo.png";
            string msPath  = $"{MatDir}/{slug}_metallicsmooth.png";
            string emPath  = $"{MatDir}/{slug}_emission.png";
            string matPath = $"{MatDir}/Mat_Fab3D_{slug}.mat";

            ConfigureTex(albPath);
            ConfigureTex(msPath);
            ConfigureTex(emPath);

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
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Smoothness", 0.55f);
            }
            if (em != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", em);
                mat.SetColor("_EmissionColor", new Color(0.2f, 2.5f, 3.0f));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.maxTextureSize = 2048;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }

        private static GameObject CreateNpcPrefab(string slug, string fbxPath, Avatar avatar, Material mat, AnimatorController ctrl)
        {
            string prefabPath = $"{PrefabDir}/{slug}_humanoid.prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);

            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;
            foreach (var smr in tmp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            var anim = tmp.GetComponent<Animator>();
            if (anim == null) anim = tmp.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var procType = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            if (procType != null && tmp.GetComponent(procType) == null)
                tmp.AddComponent(procType);

            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, prefabPath);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved;
        }

        private static void AttachAndConfigureRoutine(GameObject go, string displayName, bool stationary, float walkSpeed, StopDef[] stops)
        {
            var routineType = Type.GetType("Studio.Game.Animation.AlienNpcRoutineController, Assembly-CSharp");
            if (routineType == null) throw new Exception("AlienNpcRoutineController nao encontrado em Assembly-CSharp!");

            var comp = go.GetComponent(routineType);
            if (comp == null) comp = go.AddComponent(routineType);

            var so = new SerializedObject(comp);
            so.FindProperty("npcDisplayName").stringValue = displayName;
            so.FindProperty("stationaryMode").boolValue = stationary;
            so.FindProperty("walkSpeed").floatValue = walkSpeed;

            var stopsProp = so.FindProperty("stops");
            stopsProp.ClearArray();
            if (stops != null)
            {
                stopsProp.arraySize = stops.Length;
                for (int i = 0; i < stops.Length; i++)
                {
                    var elem = stopsProp.GetArrayElementAtIndex(i);
                    elem.FindPropertyRelative("activityName").stringValue = stops[i].activity;
                    elem.FindPropertyRelative("position").vector3Value    = stops[i].pos;
                    elem.FindPropertyRelative("lookTarget").vector3Value  = stops[i].look;
                    elem.FindPropertyRelative("dwellSeconds").floatValue  = stops[i].dwell;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReplaceExistingNpcVisual(string targetName, GameObject npcPrefab, float targetHeight, string displayName, bool stationary)
        {
            var go = GameObject.Find(targetName);
            if (go == null) return;

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.enabled = false;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.enabled = false;

            var oldVis = go.transform.Find("AlienNpcVisual");
            if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

            var visContainer = new GameObject("AlienNpcVisual");
            visContainer.transform.SetParent(go.transform, false);
            visContainer.transform.localPosition = Vector3.zero;
            visContainer.transform.localRotation = Quaternion.identity;

            PrefabUtility.InstantiatePrefab(npcPrefab, visContainer.transform);
            NormalizeCharacterHeight(visContainer, targetHeight);

            AttachAndConfigureRoutine(go, displayName, stationary, 1.3f, null);
        }

        private static void SpawnRoamingNpc(
            Transform parent, string name, GameObject npcPrefab, float targetHeight,
            string displayName, Vector3 startPos, float walkSpeed, StopDef[] stops)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = startPos;
            if (stops != null && stops.Length > 0)
            {
                Vector3 toLook = stops[0].look - startPos;
                toLook.y = 0f;
                if (toLook.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.LookRotation(toLook.normalized, Vector3.up);
            }

            var vis = new GameObject("AlienNpcVisual");
            vis.transform.SetParent(go.transform, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            PrefabUtility.InstantiatePrefab(npcPrefab, vis.transform);
            NormalizeCharacterHeight(vis, targetHeight);

            var cc = go.AddComponent<CharacterController>();
            cc.height = targetHeight;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, targetHeight * 0.5f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.25f;
            cc.skinWidth = 0.01f;

            AttachAndConfigureRoutine(go, displayName, false, walkSpeed, stops);
        }

        private static void NormalizeCharacterHeight(GameObject visContainer, float targetHeight)
        {
            Bounds b = ProceduralMeshLibrary.ComputeBounds(visContainer);
            if (b.size.y > 0.001f)
            {
                float s = targetHeight / b.size.y;
                visContainer.transform.localScale = Vector3.one * s;
            }
            visContainer.transform.localPosition = Vector3.zero;
        }

        private static void CapturePreview(Vector3 camPos, Vector3 lookAt, string filename)
        {
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            Vector3 oldPos = cam.transform.position;
            Quaternion oldRot = cam.transform.rotation;

            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string outPath = Path.Combine(repoRoot, "evidence", "captures", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            cam.transform.position = oldPos;
            cam.transform.rotation = oldRot;
        }
    }
}
