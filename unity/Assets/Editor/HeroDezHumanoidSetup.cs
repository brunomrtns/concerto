using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Studio.Editor
{
    /// <summary>
    /// HeroDezHumanoidSetup — Estoria S-char-02 / 5.1-B2:
    /// Configura hero_dez_humanoid.fbx como Humanoid Avatar (sem retoque manual),
    /// gera HeroDezController.controller com Idle, Walk, Run, Jump e Turn (UAL1+UAL2),
    /// atualiza Player_Dez_Placeholder no Caldeiral e captura frames reais de animacao para MP4/sheet.
    /// </summary>
    public static class HeroDezHumanoidSetup
    {
        private const string DezFbxPath  = "Assets/Game/Art/Generated/hero_dez/hero_dez_humanoid.fbx";
        private const string DezMatPath  = "Assets/Game/Materials/Fab3D/Mat_Fab3D_hero_dez.mat";
        private const string Ual1FbxPath = "Assets/Game/Art/Characters/Quaternius/Animations/UAL1_Standard.fbx";
        private const string Ual2FbxPath = "Assets/Game/Art/Characters/Quaternius/Animations/UAL2_Standard.fbx";
        private const string AnimDir     = "Assets/Game/Animations";
        private const string CtrlPath    = "Assets/Game/Animations/HeroDezController.controller";
        private const string PrefabPath  = "Assets/Game/Prefabs/Fab3D/hero_dez_humanoid.prefab";
        private const string ScenePath   = "Assets/Game/Scenes/Caldeiral.unity";

        [MenuItem("Studio/Concerto/C5 Herois/Setup Dez Humanoid + UAL Showcase")]
        public static void SetupMenu() => SetupAndVerify();

        public static void SetupAndVerifyBatch()
        {
            try
            {
                SetupAndVerify();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroDezHumanoid] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void SetupAndVerify()
        {
            AssetDatabase.Refresh();

            // 1. Configura hero_dez_humanoid.fbx como Humanoid
            var dezImp = AssetImporter.GetAtPath(DezFbxPath) as ModelImporter;
            if (dezImp == null)
                throw new FileNotFoundException($"ModelImporter nao encontrado: {DezFbxPath}");

            dezImp.animationType = ModelImporterAnimationType.Human;
            dezImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dezImp.importAnimation = false;
            dezImp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(DezFbxPath).OfType<Avatar>().FirstOrDefault();
            bool avatarValid = avatar != null && avatar.isValid && avatar.isHuman;
            Debug.Log($"[HeroDezHumanoid] Avatar: name={(avatar ? avatar.name : "NULL")} | isValid={(avatar && avatar.isValid)} | isHuman={(avatar && avatar.isHuman)}");

            // 2. Garante UAL1 + UAL2 como Humanoid e coleta todos os clips
            var allClips = new List<AnimationClip>();
            foreach (var ualPath in new[] { Ual1FbxPath, Ual2FbxPath })
            {
                var ualImp = AssetImporter.GetAtPath(ualPath) as ModelImporter;
                if (ualImp != null)
                {
                    if (ualImp.animationType != ModelImporterAnimationType.Human)
                    {
                        ualImp.animationType = ModelImporterAnimationType.Human;
                        ualImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        ualImp.SaveAndReimport();
                    }
                    allClips.AddRange(AssetDatabase.LoadAllAssetsAtPath(ualPath)
                        .OfType<AnimationClip>()
                        .Where(c => !c.name.StartsWith("__preview__")));
                }
            }

            AnimationClip FindExactOrContains(params string[] candidates)
            {
                foreach (var kw in candidates)
                {
                    var exact = allClips.FirstOrDefault(c => c.name.Equals(kw, StringComparison.OrdinalIgnoreCase));
                    if (exact != null) return exact;
                }
                foreach (var kw in candidates)
                {
                    var match = allClips.FirstOrDefault(c => c.name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match != null) return match;
                }
                return allClips.FirstOrDefault();
            }

            var idleClip = FindExactOrContains("Idle_Loop", "Idle");
            var walkClip = FindExactOrContains("Walk_Loop", "Walk");
            var runClip  = FindExactOrContains("Sprint_Loop", "Jog_Fwd_Loop", "Run_Loop", "Run");
            var jumpClip = FindExactOrContains("Jump_Start", "Jump_Loop", "Jump");
            var turnClip = FindExactOrContains("Strafe_Right", "Strafe_Left", "Turn_Right", "Turn_Left", "Roll", "Dance_Loop");

            // 3. Cria AnimatorController com Idle, Walk, Run, Jump, Turn
            if (!AssetDatabase.IsValidFolder(AnimDir))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), AnimDir));
                AssetDatabase.Refresh();
            }
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
                AssetDatabase.DeleteAsset(CtrlPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Turn", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;

            var sIdle = sm.AddState("Idle"); sIdle.motion = idleClip; sm.defaultState = sIdle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walkClip;
            var sRun  = sm.AddState("Run");  sRun.motion  = runClip;
            var sJump = sm.AddState("Jump"); sJump.motion = jumpClip;
            var sTurn = sm.AddState("Turn"); sTurn.motion = turnClip;

            var tIW = sIdle.AddTransition(sWalk); tIW.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); tIW.hasExitTime = false;
            var tWI = sWalk.AddTransition(sIdle); tWI.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");    tWI.hasExitTime = false;
            var tWR = sWalk.AddTransition(sRun);  tWR.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed"); tWR.hasExitTime = false;
            var tRW = sRun.AddTransition(sWalk);  tRW.AddCondition(AnimatorConditionMode.Less, 3.5f, "Speed");    tRW.hasExitTime = false;
            var tAnyJ = sm.AddAnyStateTransition(sJump); tAnyJ.AddCondition(AnimatorConditionMode.If, 0f, "Jump"); tAnyJ.hasExitTime = false;
            var tJI = sJump.AddTransition(sIdle); tJI.hasExitTime = true; tJI.exitTime = 0.9f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // 4. Cria prefab hero_dez_humanoid.prefab com material PBR + Animator
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DezFbxPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DezMatPath);
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
            {
                if (mat != null) r.sharedMaterial = mat;
            }
            var anim = tmp.GetComponent<Animator>() ?? tmp.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            PrefabUtility.SaveAsPrefabAsset(tmp, PrefabPath);
            UnityEngine.Object.DestroyImmediate(tmp);
            AssetDatabase.SaveAssets();

            // 5. Atualiza Player_Dez_Placeholder no Caldeiral.unity e captura frames das 5 animacoes!
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dezTarget = GameObject.Find("Player_Dez_Placeholder");
            bool sceneUpdated = false;
            GameObject dezInst = null;
            if (dezTarget != null && avatarValid)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var oldVis = dezTarget.transform.Find("Player_Dez_Placeholder_HeroVisual");
                if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

                var container = new GameObject("Player_Dez_Placeholder_HeroVisual");
                container.transform.SetParent(dezTarget.transform, false);
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                container.transform.localScale = Vector3.one;

                dezInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                var b = ProceduralMeshLibrary.ComputeBounds(container);
                if (b.size.y > 0.001f)
                {
                    float s = 1.25f / b.size.y;
                    container.transform.localScale = Vector3.one * s;
                    b = ProceduralMeshLibrary.ComputeBounds(container);
                    Vector3 tPos = dezTarget.transform.position;
                    container.transform.position += new Vector3(tPos.x - b.center.x, tPos.y - b.min.y, tPos.z - b.center.z);
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
                sceneUpdated = true;
            }

            // 6. Captura sequencia de frames reais de animacao (Idle, Walk, Run, Jump, Turn)
            if (dezInst != null)
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                var dezAnim = dezInst.GetComponent<Animator>();
                if (cam != null && dezAnim != null)
                {
                    CaptureAnimationShowcase(cam, dezAnim, new[] {
                        ("Idle", idleClip),
                        ("Walk", walkClip),
                        ("Run",  runClip),
                        ("Jump", jumpClip),
                        ("Turn", turnClip),
                    });
                }
            }

            // 7. Grava relatorio JSON
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string repPath = Path.Combine(repoRoot, "evidence", "art", "hero_dez_humanoid_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(repPath));
            string json = "{\n" +
                $"  \"asset\": \"{DezFbxPath}\",\n" +
                $"  \"avatar_valid\": {avatarValid.ToString().ToLowerInvariant()},\n" +
                $"  \"avatar_human\": {(avatar != null && avatar.isHuman).ToString().ToLowerInvariant()},\n" +
                $"  \"ual_clips_found\": {allClips.Count},\n" +
                $"  \"clip_idle\": \"{(idleClip ? idleClip.name : "")}\",\n" +
                $"  \"clip_walk\": \"{(walkClip ? walkClip.name : "")}\",\n" +
                $"  \"clip_run\": \"{(runClip ? runClip.name : "")}\",\n" +
                $"  \"clip_jump\": \"{(jumpClip ? jumpClip.name : "")}\",\n" +
                $"  \"clip_turn\": \"{(turnClip ? turnClip.name : "")}\",\n" +
                $"  \"controller\": \"{CtrlPath}\",\n" +
                $"  \"prefab\": \"{PrefabPath}\",\n" +
                $"  \"scene_updated\": {sceneUpdated.ToString().ToLowerInvariant()}\n" +
                "}\n";
            File.WriteAllText(repPath, json);
            Debug.Log($"[HeroDezHumanoid] CONCLUIDO! clips=({idleClip?.name}, {walkClip?.name}, {runClip?.name}, {jumpClip?.name}, {turnClip?.name})");
        }

        private static void CaptureAnimationShowcase(Camera cam, Animator anim, (string label, AnimationClip clip)[] states)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "evidence", "captures", "dez_ual_anim_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            // Camera frontal 3/4 focada no Dez A na Oficina Z1
            cam.transform.position = new Vector3(-31.2f, 1.05f, -19.0f);
            cam.transform.LookAt(new Vector3(-33.0f, 0.70f, -20.0f));

            int width = 960, height = 540;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Warmup URP
            for (int w = 0; w < 2; w++) { cam.targetTexture = rt; cam.Render(); }

            int frameIdx = 0;
            foreach (var st in states)
            {
                if (st.clip == null) continue;
                var graph = PlayableGraph.Create($"Showcase_{st.label}");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "AnimOut", anim);
                var clipPlayable = AnimationClipPlayable.Create(graph, st.clip);
                clipPlayable.SetApplyFootIK(true);
                output.SetSourcePlayable(clipPlayable);
                graph.Play();

                int nFrames = 12;
                float dt = Mathf.Max(0.04f, st.clip.length / nFrames);
                for (int i = 0; i < nFrames; i++)
                {
                    clipPlayable.SetTime(i * dt);
                    graph.Evaluate(0f);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D3}_{st.label}.png"), tex.EncodeToPNG());
                    frameIdx++;
                }
                graph.Destroy();
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[HeroDezHumanoid] {frameIdx} frames de animacao UAL gravados em {framesDir}");
        }
    }
}
