using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// HeroDexRigSetup — RUN dex_rig_v1 (TAREFA 3 + TAREFA 4.1):
    ///   - Cria Mat_Fab3D_hero_dex.mat (URP Lit com hero_dex_basecolor.png + hero_dex_mr.png)
    ///   - Importa hero_dex_humanoid.fbx como Humanoid e valida avatar_valid, avatar_human e bind-pose tilt
    ///   - Monta hero_dex_humanoid.prefab REUSANDO HeroDezController.controller + DezProceduralAnimator + SpringBone
    ///   - Faz swap em Player_Dez_Placeholder no Caldeiral.unity (sem apagar hero_dez_*!)
    /// </summary>
    public static class HeroDexRigSetup
    {
        private const string DexFbxPath  = "Assets/Game/Art/Generated/hero_dex/hero_dex_humanoid.fbx";
        private const string BaseTexPath = "Assets/Game/Art/Generated/hero_dex/hero_dex_basecolor.png";
        private const string MrTexPath   = "Assets/Game/Art/Generated/hero_dex/hero_dex_mr.png";
        private const string DexMatPath  = "Assets/Game/Materials/Fab3D/Mat_Fab3D_hero_dex.mat";
        private const string CtrlPath    = "Assets/Game/Animations/HeroDezController.controller";
        private const string PrefabPath  = "Assets/Game/Prefabs/Fab3D/hero_dex_humanoid.prefab";
        private const string ScenePath   = "Assets/Game/Scenes/Caldeiral.unity";

        [MenuItem("Studio/Concerto/Dex Rig v1/Build Hero Dex Prefab & Swap in Caldeiral")]
        public static void SetupMenu() => BuildAndSwap();

        public static void BuildAndSwapBatch()
        {
            try
            {
                BuildAndSwap();
                CaptureSinglePhotoInScene();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroDexRig] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void CaptureSinglePhotoInScene()
        {
            var dezTarget = GameObject.Find("Player_Dez_Placeholder");
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (dezTarget == null || cam == null) return;

            var porca = GameObject.Find("Companion_Porca_Placeholder");
            if (porca != null) porca.transform.position = new Vector3(-28.0f, 1.35f, -18.0f);

            Vector3 p = dezTarget.transform.position;
            Vector3 fwd = dezTarget.transform.forward;
            Vector3 right = dezTarget.transform.right;
            // Camera 3/4 frontal bem pertinho e iluminada mostrando o Dex inteiro no cenario da Oficina!
            cam.transform.position = p + fwd * 1.95f + right * 0.55f + Vector3.up * 0.92f;
            cam.transform.LookAt(p + Vector3.up * 0.68f);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string outPath = Path.Combine(repoRoot, "evidence", "captures", "hero_dex_in_scene_front.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[HeroDexRig] FOTO SALVA: {outPath}");
        }

        private static Material EnsureDexMaterial()
        {
            // Garante texturas 1024 sRGB/Linear corretas
            var baseImp = AssetImporter.GetAtPath(BaseTexPath) as TextureImporter;
            if (baseImp != null)
            {
                baseImp.sRGBTexture = true;
                baseImp.maxTextureSize = 1024;
                baseImp.SaveAndReimport();
            }
            var mrImp = AssetImporter.GetAtPath(MrTexPath) as TextureImporter;
            if (mrImp != null)
            {
                mrImp.sRGBTexture = false;
                mrImp.maxTextureSize = 1024;
                mrImp.SaveAndReimport();
            }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DexMatPath);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, DexMatPath);
            }
            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.mainTexture = baseTex;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", 0); // Render Face = Both (protege contra faces com winding invertido pos-decimate)
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static void BuildAndSwap()
        {
            AssetDatabase.Refresh();

            // 1. Reimporta hero_dex_humanoid.fbx como Humanoid
            var dexImp = AssetImporter.GetAtPath(DexFbxPath) as ModelImporter;
            if (dexImp == null) throw new FileNotFoundException(DexFbxPath);
            dexImp.animationType = ModelImporterAnimationType.Human;
            dexImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dexImp.importAnimation = false;
            dexImp.importNormals = ModelImporterNormals.Calculate;
            dexImp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(DexFbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new Exception("Avatar Humanoid do hero_dex_humanoid.fbx invalido!");

            var mat = EnsureDexMaterial();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null) throw new FileNotFoundException(CtrlPath);

            // 2. Monta hero_dex_humanoid.prefab + mede bind-pose tilt
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DexFbxPath);
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
                if (mat != null) r.sharedMaterial = mat;
            foreach (var smr in tmp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            var anim = tmp.GetComponent<Animator>() ?? tmp.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Bind-pose check (inclinacao lateral do vetor Hips->Head no bind pose)
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            float bindTiltDeg = 0f;
            if (hips != null && head != null)
            {
                Vector3 spineDir = (head.position - hips.position).normalized;
                bindTiltDeg = Vector3.Angle(new Vector3(spineDir.x, spineDir.y, 0f).normalized, Vector3.up);
            }

            var procType = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            if (procType != null && tmp.GetComponent(procType) == null)
                tmp.AddComponent(procType);

            var springType = Type.GetType("Studio.Game.Animation.SpringBone, Assembly-CSharp");
            int springCount = 0;
            if (springType != null)
            {
                foreach (var t in tmp.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Ear_L" || t.name == "Ear_R" || t.name == "Antenna_Top")
                    {
                        if (t.GetComponent(springType) == null)
                            t.gameObject.AddComponent(springType);
                        springCount++;
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(tmp, PrefabPath);
            UnityEngine.Object.DestroyImmediate(tmp);
            AssetDatabase.SaveAssets();

            // 3. Swap limpo em Player_Dez_Placeholder no Caldeiral.unity (sem apagar hero_dez_humanoid.prefab!)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dezTarget = GameObject.Find("Player_Dez_Placeholder");
            if (dezTarget != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var oldVis = dezTarget.transform.Find("Player_Dez_Placeholder_HeroVisual");
                if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

                var container = new GameObject("Player_Dez_Placeholder_HeroVisual");
                container.transform.SetParent(dezTarget.transform, false);
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                container.transform.localScale = Vector3.one;

                Vector3 rootPos = dezTarget.transform.position;
                dezTarget.transform.position = new Vector3(rootPos.x, 0f, rootPos.z);

                PrefabUtility.InstantiatePrefab(prefab, container.transform);
                var b = ProceduralMeshLibrary.ComputeBounds(container);
                if (b.size.y > 0.001f)
                {
                    float s = 1.25f / b.size.y;
                    container.transform.localScale = Vector3.one * s;
                }
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;

                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // 4. Relatorio JSON de evidencia
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string repPath = Path.Combine(repoRoot, "evidence", "art", "hero_dex_rig_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(repPath));
            string json = "{\n" +
                $"  \"prefab\": \"{PrefabPath}\",\n" +
                $"  \"controller_reused\": \"{CtrlPath}\",\n" +
                $"  \"avatar_valid\": {avatar.isValid.ToString().ToLowerInvariant()},\n" +
                $"  \"avatar_human\": {avatar.isHuman.ToString().ToLowerInvariant()},\n" +
                $"  \"bind_pose_spine_tilt_deg\": {bindTiltDeg.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},\n" +
                $"  \"bind_pose_upright_pass\": {(bindTiltDeg <= 2.0f).ToString().ToLowerInvariant()},\n" +
                $"  \"spring_bones_attached\": {springCount}\n" +
                "}\n";
            File.WriteAllText(repPath, json);
            Debug.Log($"[HeroDexRig] OK! avatar_valid=true | bindTilt={bindTiltDeg:F3}deg | springBones={springCount}");
        }
    }
}
