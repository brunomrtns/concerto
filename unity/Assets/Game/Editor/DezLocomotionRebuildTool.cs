#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Ferramenta Editor para reconstruir a locomocao do Dez (GATES 2 & 3):
    ///   - Valida Avatar Humanoid de hero_dez_humanoid.fbx (22 ossos, T-pose)
    ///   - Configura os 7 clipes reais do Starter Assets como Humanoid (sem warnings)
    ///   - Monta HeroDezController.controller com Blend Tree suave (Idle -> Walk -> Run)
    ///     e maquina de estados JumpStart -> InAir -> JumpLand + iKPass = true.
    /// </summary>
    public static class DezLocomotionRebuildTool
    {
        private const string HeroFbxPath = "Assets/Game/Art/Generated/hero_dez/hero_dez_humanoid.fbx";
        private const string ClipsDir    = "Assets/Game/Animations/StarterAssets";
        private const string CtrlPath    = "Assets/Game/Animations/HeroDezController.controller";

        private struct ClipSpec
        {
            public string FileName;
            public bool Loop;
        }

        private static readonly ClipSpec[] Specs =
        {
            new ClipSpec { FileName = "Stand--Idle.anim.fbx",             Loop = true  },
            new ClipSpec { FileName = "Locomotion--Walk_N.anim.fbx",      Loop = true  },
            new ClipSpec { FileName = "Locomotion--Run_N.anim.fbx",       Loop = true  },
            new ClipSpec { FileName = "Jump--Jump.anim.fbx",              Loop = false },
            new ClipSpec { FileName = "Jump--InAir.anim.fbx",             Loop = true  },
            new ClipSpec { FileName = "Locomotion--Walk_N_Land.anim.fbx", Loop = false },
            new ClipSpec { FileName = "Locomotion--Run_N_Land.anim.fbx",  Loop = false },
        };

        [MenuItem("CONCERTO/Locomotion/Rebuild Dez Animator & Validate Humanoid Rig")]
        public static void RebuildAll()
        {
            // 1. Valida Avatar Humanoid do Dez (GATE 2)
            var heroAssets = AssetDatabase.LoadAllAssetsAtPath(HeroFbxPath);
            Avatar dezAvatar = null;
            foreach (var a in heroAssets)
            {
                if (a is Avatar av) { dezAvatar = av; break; }
            }
            if (dezAvatar == null || !dezAvatar.isValid || !dezAvatar.isHuman)
            {
                Debug.LogError($"[DezLocomotionRebuild] Avatar Humanoid invalido em {HeroFbxPath}!");
                return;
            }
            Debug.Log($"[DezLocomotionRebuild] GATE 2 OK: Avatar '{dezAvatar.name}' isValid={dezAvatar.isValid}, isHuman={dezAvatar.isHuman}");

            // 2. Configura cada clipe FBX como Humanoid com bake de Root Motion limpo
            foreach (var spec in Specs)
            {
                string path = $"{ClipsDir}/{spec.FileName}";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"[DezLocomotionRebuild] Clipe nao encontrado: {path}");
                    return;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                var clipAnims = importer.clipAnimations;
                if (clipAnims == null || clipAnims.Length == 0)
                    clipAnims = importer.defaultClipAnimations;

                for (int i = 0; i < clipAnims.Length; i++)
                {
                    clipAnims[i].loopTime = spec.Loop;
                    clipAnims[i].loopPose = spec.Loop;
                    clipAnims[i].lockRootRotation = true;
                    clipAnims[i].keepOriginalOrientation = true;
                    clipAnims[i].lockRootHeightY = true;
                    clipAnims[i].keepOriginalPositionY = true;
                    clipAnims[i].lockRootPositionXZ = true;
                    clipAnims[i].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clipAnims;
                importer.SaveAndReimport();
            }

            // 3. Carrega os AnimationClips reais importados
            AnimationClip idleClip     = LoadFirstClip($"{ClipsDir}/Stand--Idle.anim.fbx");
            AnimationClip walkClip     = LoadFirstClip($"{ClipsDir}/Locomotion--Walk_N.anim.fbx");
            AnimationClip runClip      = LoadFirstClip($"{ClipsDir}/Locomotion--Run_N.anim.fbx");
            AnimationClip jumpClip     = LoadFirstClip($"{ClipsDir}/Jump--Jump.anim.fbx");
            AnimationClip inAirClip    = LoadFirstClip($"{ClipsDir}/Jump--InAir.anim.fbx");
            AnimationClip walkLandClip = LoadFirstClip($"{ClipsDir}/Locomotion--Walk_N_Land.anim.fbx");
            AnimationClip runLandClip  = LoadFirstClip($"{ClipsDir}/Locomotion--Run_N_Land.anim.fbx");

            if (idleClip == null || walkClip == null || runClip == null || jumpClip == null || inAirClip == null || walkLandClip == null || runLandClip == null)
            {
                Debug.LogError("[DezLocomotionRebuild] Um ou mais AnimationClips falharam ao carregar.");
                return;
            }

            // 4. Reconstroi HeroDezController.controller (GATE 3 — Blend Tree Starter Assets + iKPass)
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
                AssetDatabase.DeleteAsset(CtrlPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            ctrl.AddParameter("Speed",       AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MotionSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump",        AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("FreeFall",    AnimatorControllerParameterType.Bool);
            // Compatibilidade com parametros legados
            ctrl.AddParameter("SpeedRatio",  AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("VerticalVel", AnimatorControllerParameterType.Float);

            var ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == "MotionSpeed") ps[i].defaultFloat = 1f;
                if (ps[i].name == "Grounded" || ps[i].name == "IsGrounded") ps[i].defaultBool = true;
            }
            ctrl.parameters = ps;

            var layers = ctrl.layers;
            layers[0].iKPass = true; // Necessario para Foot Grounding IK (GATE 4)
            ctrl.layers = layers;
            var sm = ctrl.layers[0].stateMachine;

            // Blend Tree 1D principal: Idle (0.0) -> Walk (2.2) -> Run (5.4)
            var locoBlend = new BlendTree
            {
                name = "Idle Walk Run Blend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            locoBlend.AddChild(idleClip, 0.0f);
            locoBlend.AddChild(walkClip, 2.2f);
            locoBlend.AddChild(runClip,  5.4f);
            AssetDatabase.AddObjectToAsset(locoBlend, ctrl);

            var locoState = sm.AddState("Idle Walk Run Blend", new Vector3(260, 120, 0));
            locoState.motion = locoBlend;
            locoState.speedParameterActive = true;
            locoState.speedParameter = "MotionSpeed";
            sm.defaultState = locoState;

            var jumpStartState = sm.AddState("JumpStart", new Vector3(120, 260, 0));
            jumpStartState.motion = jumpClip;

            var inAirState = sm.AddState("InAir", new Vector3(260, 380, 0));
            inAirState.motion = inAirClip;

            var landBlend = new BlendTree
            {
                name = "JumpLand Blend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            landBlend.AddChild(walkLandClip, 0.0f);
            landBlend.AddChild(walkLandClip, 2.2f);
            landBlend.AddChild(runLandClip,  5.4f);
            AssetDatabase.AddObjectToAsset(landBlend, ctrl);

            var jumpLandState = sm.AddState("JumpLand", new Vector3(420, 260, 0));
            jumpLandState.motion = landBlend;

            // Transicoes limpas (Starter Assets pattern)
            var tToJump = locoState.AddTransition(jumpStartState);
            tToJump.hasExitTime = false;
            tToJump.duration = 0.05f;
            tToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var tToFall = locoState.AddTransition(inAirState);
            tToFall.hasExitTime = false;
            tToFall.duration = 0.12f;
            tToFall.AddCondition(AnimatorConditionMode.If, 0, "FreeFall");

            var tJumpToAir = jumpStartState.AddTransition(inAirState);
            tJumpToAir.hasExitTime = true;
            tJumpToAir.exitTime = 0.78f;
            tJumpToAir.duration = 0.14f;

            var tAirToLand = inAirState.AddTransition(jumpLandState);
            tAirToLand.hasExitTime = false;
            tAirToLand.duration = 0.06f;
            tAirToLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

            var tJumpToLand = jumpStartState.AddTransition(jumpLandState);
            tJumpToLand.hasExitTime = false;
            tJumpToLand.duration = 0.06f;
            tJumpToLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

            var tLandToLoco = jumpLandState.AddTransition(locoState);
            tLandToLoco.hasExitTime = true;
            tLandToLoco.exitTime = 0.70f;
            tLandToLoco.duration = 0.15f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DezLocomotionRebuild] GATE 3 OK: {CtrlPath} reconstruido com 7 clipes reais Starter Assets (Idle/Walk/Run/Jump/InAir/WalkLand/RunLand), Blend Tree suave e iKPass=true!");
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    return clip;
            }
            return null;
        }
    }
}
#endif
