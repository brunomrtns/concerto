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
    /// HeroDezAAASetup — RUN mega_v6 (TAREFA 2 + TAREFA 3):
    ///   - Importa os 7 clips MoMask 60fps como Humanoid (loop + root lock)
    ///   - Reimporta hero_dez_humanoid.fbx (22 ossos + Ear_L, Ear_R, Antenna_Top)
    ///   - Anexa SpringBone nas orelhas/antena + DezProceduralAnimator (Foot IK + Landing Dip + Respiracao + Look-At)
    ///   - Monta HeroDezController.controller v2 com Blend Tree 2D (Speed x TurnRate) + IK Pass = true
    ///   - Anexa DezLocomotionController (CharacterController + gravidade -18 m/s^2) em Player_Dez_Placeholder
    /// </summary>
    public static class HeroDezAAASetup
    {
        private const string DezFbxPath  = "Assets/Game/Art/Generated/hero_dez/hero_dez_humanoid.fbx";
        private const string DezMatPath  = "Assets/Game/Materials/Fab3D/Mat_Fab3D_hero_dez.mat";
        private const string MoMaskDir   = "Assets/Game/Animations/MoMask";
        private const string Ual1FbxPath = "Assets/Game/Art/Characters/Quaternius/Animations/UAL1_Standard.fbx";
        private const string CtrlPath    = "Assets/Game/Animations/HeroDezController.controller";
        private const string PrefabPath  = "Assets/Game/Prefabs/Fab3D/hero_dez_humanoid.prefab";
        private const string ScenePath   = "Assets/Game/Scenes/Caldeiral.unity";

        [MenuItem("Studio/Concerto/Mega v6/Build Dez AAA Animation Stack")]
        public static void SetupMenu() => BuildStack();

        public static void BuildStackBatch()
        {
            try
            {
                BuildStack();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroDezAAA] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static AnimationClip ImportMoMaskClip(string fileName, bool loop)
        {
            string path = $"{MoMaskDir}/{fileName}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[HeroDezAAA] MoMask FBX nao encontrado: {path}");
                return null;
            }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.resampleCurves = true;

            var clips = imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = fileName;
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = false;
                    clips[i].heightFromFeet = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips;
            }
            imp.SaveAndReimport();

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null)
            {
                Debug.Log($"[HeroDezAAA] MoMask Clip OK: {fileName} | isHuman={clip.isHumanMotion} | fps={clip.frameRate} | len={clip.length:F2}s");
            }
            return clip;
        }

        private static AnimationClip FindUalFallback(string exactName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Ual1FbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.Equals(exactName, StringComparison.OrdinalIgnoreCase));
        }

        public static void BuildStack()
        {
            AssetDatabase.Refresh();

            // 1. Reimporta hero_dez_humanoid.fbx (agora com Ear_L, Ear_R, Antenna_Top!)
            var dezImp = AssetImporter.GetAtPath(DezFbxPath) as ModelImporter;
            if (dezImp == null) throw new FileNotFoundException(DezFbxPath);
            dezImp.animationType = ModelImporterAnimationType.Human;
            dezImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dezImp.importAnimation = false;
            dezImp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(DezFbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new Exception("Avatar Humanoid do hero_dez_humanoid.fbx invalido!");

            // 2. Importa os 7 clips MoMask 60fps Humanoid (com fallback UAL caso algum falhe)
            var cIdle  = ImportMoMaskClip("dez_idle_breath", true)  ?? FindUalFallback("Idle_Loop");
            var cWalk  = ImportMoMaskClip("dez_walk_loop",   true)  ?? FindUalFallback("Walk_Loop");
            var cRun   = ImportMoMaskClip("dez_run_loop",    true)  ?? FindUalFallback("Sprint_Loop");
            var cTurnL = ImportMoMaskClip("dez_turn_left",   true)  ?? FindUalFallback("Walk_Loop");
            var cTurnR = ImportMoMaskClip("dez_turn_right",  true)  ?? FindUalFallback("Walk_Loop");
            var cJump  = ImportMoMaskClip("dez_jump_full",   false) ?? FindUalFallback("Jump_Start");

            // 3. Cria HeroDezController.controller v2 com Blend Tree 2D + IK Pass = true!
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
                AssetDatabase.DeleteAsset(CtrlPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            ctrl.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            ctrl.AddParameter("TurnRate",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("VerticalVel", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump",        AnimatorControllerParameterType.Trigger);

            // Ativa IK Pass na Base Layer (obrigatorio para OnAnimatorIK!)
            var layers = ctrl.layers;
            layers[0].iKPass = true;
            ctrl.layers = layers;
            var sm = ctrl.layers[0].stateMachine;

            // Blend Tree 2D Freeform Directional: X = TurnRate (-1..1), Y = Speed (0..4.4)
            var blendTree = new BlendTree
            {
                name = "Locomotion_2D",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "TurnRate",
                blendParameterY = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, ctrl);

            blendTree.AddChild(cIdle,  new Vector2( 0.0f, 0.0f));
            blendTree.AddChild(cTurnL, new Vector2(-0.8f, 0.0f));
            blendTree.AddChild(cTurnR, new Vector2( 0.8f, 0.0f));
            blendTree.AddChild(cWalk,  new Vector2( 0.0f, 2.0f));
            blendTree.AddChild(cRun,   new Vector2( 0.0f, 4.4f));

            var sLoco = sm.AddState("Locomotion");
            sLoco.motion = blendTree;
            sm.defaultState = sLoco;

            var sJump = sm.AddState("Jump");
            sJump.motion = cJump;

            var tToJump = sm.AddAnyStateTransition(sJump);
            tToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            tToJump.duration = 0.08f;
            tToJump.hasExitTime = false;

            var tJumpBack = sJump.AddTransition(sLoco);
            tJumpBack.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tJumpBack.duration = 0.14f;
            tJumpBack.hasExitTime = true;
            tJumpBack.exitTime = 0.72f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // 4. Monta prefab hero_dez_humanoid.prefab com PBR + Animator + DezProceduralAnimator + SpringBones!
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DezFbxPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DezMatPath);
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

            var procType = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            if (procType != null && tmp.GetComponent(procType) == null)
                tmp.AddComponent(procType);

            // Anexa SpringBone nos 3 ossos secundarios (Ear_L, Ear_R, Antenna_Top)
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

            // 5. Atualiza Player_Dez_Placeholder no Caldeiral.unity com DezLocomotionController + prefab AAA
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dezTarget = GameObject.Find("Player_Dez_Placeholder");
            if (dezTarget != null)
            {
                var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
                if (locoType != null && dezTarget.GetComponent(locoType) == null)
                    dezTarget.AddComponent(locoType);

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
                // Humanoid com heightFromFeet=true ancora a sola dos pes exatamente em localPosition.y = 0!
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // 6. Grava relatorio JSON
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string repPath = Path.Combine(repoRoot, "evidence", "art", "hero_dez_aaa_v6_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(repPath));
            string json = "{\n" +
                $"  \"avatar_valid\": true,\n" +
                $"  \"ik_pass_enabled\": true,\n" +
                $"  \"spring_bones_attached\": {springCount},\n" +
                $"  \"clips\": {{\n" +
                $"    \"idle\": \"{(cIdle ? cIdle.name : "")} (human={cIdle?.isHumanMotion}, fps={cIdle?.frameRate})\",\n" +
                $"    \"walk\": \"{(cWalk ? cWalk.name : "")} (human={cWalk?.isHumanMotion}, fps={cWalk?.frameRate})\",\n" +
                $"    \"run\": \"{(cRun ? cRun.name : "")} (human={cRun?.isHumanMotion}, fps={cRun?.frameRate})\",\n" +
                $"    \"turn_l\": \"{(cTurnL ? cTurnL.name : "")} (human={cTurnL?.isHumanMotion}, fps={cTurnL?.frameRate})\",\n" +
                $"    \"turn_r\": \"{(cTurnR ? cTurnR.name : "")} (human={cTurnR?.isHumanMotion}, fps={cTurnR?.frameRate})\",\n" +
                $"    \"jump\": \"{(cJump ? cJump.name : "")} (human={cJump?.isHumanMotion}, fps={cJump?.frameRate})\"\n" +
                $"  }}\n" +
                "}\n";
            File.WriteAllText(repPath, json);
            Debug.Log($"[HeroDezAAA] STACK AAA CONCLUIDA! springBones={springCount} | report={repPath}");
        }
    }
}
