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

        private static Dictionary<string, AnimationClip> ConfigureAndLoadUalClips(string fbxPath)
        {
            var result = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return result;

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.resampleCurves = true;

            var loopSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Loop"
            };
            var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Loop", "Jump_Land"
            };

            var clips = imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                bool changed = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    string shortName = clips[i].name.Contains("|")
                        ? clips[i].name.Substring(clips[i].name.LastIndexOf('|') + 1)
                        : clips[i].name;
                    if (!targetSet.Contains(shortName)) continue;

                    bool isLoop = loopSet.Contains(shortName);
                    clips[i].name = shortName;
                    clips[i].loopTime = isLoop;
                    clips[i].loopPose = isLoop;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = false;
                    clips[i].rotationOffset = 0f;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = false;
                    clips[i].heightFromFeet = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalPositionXZ = true;
                    changed = true;
                }
                if (changed)
                {
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                }
            }

            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
            {
                if (c.name.StartsWith("__preview__")) continue;
                string shortName = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                if (targetSet.Contains(shortName))
                {
                    result[shortName] = c;
                    Debug.Log($"[HeroDezAAA] UAL1 Clip OK: {shortName} | isHuman={c.isHumanMotion} | fps={c.frameRate} | len={c.length:F2}s");
                }
            }
            return result;
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

            // 2. Arquitetura AAA Hibrida (ADR-012 v2):
            //    - Idle expressivo: MoMask dez_idle_breath (aprovado pelo diretor!)
            //    - Locomocao ciclica + Pulo em 3 fases reais: UAL1_Standard.fbx (CC0 profissional, 100% ciclico, pes retos!)
            var ual = ConfigureAndLoadUalClips(Ual1FbxPath);
            var cIdle      = ImportMoMaskClip("dez_idle_breath", true) ?? (ual.ContainsKey("Idle_Loop") ? ual["Idle_Loop"] : FindUalFallback("Idle_Loop"));
            var cWalk      = ual.ContainsKey("Walk_Loop")    ? ual["Walk_Loop"]    : ImportMoMaskClip("dez_walk_loop", true);
            var cJog       = ual.ContainsKey("Jog_Fwd_Loop") ? ual["Jog_Fwd_Loop"] : cWalk;
            var cRun       = ual.ContainsKey("Sprint_Loop")  ? ual["Sprint_Loop"]  : ImportMoMaskClip("dez_run_loop", true);
            var cJumpStart = ual.ContainsKey("Jump_Start")   ? ual["Jump_Start"]   : ImportMoMaskClip("dez_jump_full", false);
            var cJumpLoop  = ual.ContainsKey("Jump_Loop")    ? ual["Jump_Loop"]    : cJumpStart;
            var cJumpLand  = ual.ContainsKey("Jump_Land")    ? ual["Jump_Land"]    : cIdle;

            // 3. Cria HeroDezController.controller v3: Blend Tree 2D (Idle/Walk/Jog/Sprint + Turn pivo) + Pulo 3 Fases + IK Pass = true!
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

            // Blend Tree 2D Freeform Directional: X = TurnRate (-1..1), Y = Speed (0..3.45)
            var blendTree = new BlendTree
            {
                name = "Locomotion_2D",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "TurnRate",
                blendParameterY = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, ctrl);

            blendTree.AddChild(cIdle, new Vector2( 0.0f, 0.00f));
            // Turn in Place usa Walk_Loop com passo suave acompanhando o giro (nunca luta contra o Transform!)
            blendTree.AddChild(cWalk, new Vector2(-0.85f, 0.00f));
            blendTree.AddChild(cWalk, new Vector2( 0.85f, 0.00f));
            blendTree.AddChild(cWalk, new Vector2( 0.0f, 1.45f));
            blendTree.AddChild(cJog,  new Vector2( 0.0f, 2.45f));
            blendTree.AddChild(cRun,  new Vector2( 0.0f, 3.45f));
            // Ajusta timeScale dos filhos Turn para passos pivo naturais (0.78x)
            var ch = blendTree.children;
            if (ch.Length >= 3)
            {
                ch[1].timeScale = 0.78f;
                ch[2].timeScale = 0.78f;
                blendTree.children = ch;
            }

            var sLoco = sm.AddState("Locomotion");
            sLoco.motion = blendTree;
            sm.defaultState = sLoco;

            // Pulo em 3 Fases AAA: Jump_Start (impulso) -> Jump_Loop (no ar) -> Jump_Land (pouso) -> Locomotion!
            var sJumpStart = sm.AddState("Jump_Start");
            sJumpStart.motion = cJumpStart;
            var sJumpLoop = sm.AddState("Jump_Loop");
            sJumpLoop.motion = cJumpLoop;
            var sJumpLand = sm.AddState("Jump_Land");
            sJumpLand.motion = cJumpLand;

            var tToJump = sm.AddAnyStateTransition(sJumpStart);
            tToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            tToJump.duration = 0.06f;
            tToJump.hasExitTime = false;

            var tStartToLoop = sJumpStart.AddTransition(sJumpLoop);
            tStartToLoop.hasExitTime = true;
            tStartToLoop.exitTime = 0.72f;
            tStartToLoop.duration = 0.12f;

            var tStartToLand = sJumpStart.AddTransition(sJumpLand);
            tStartToLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tStartToLand.AddCondition(AnimatorConditionMode.Less, 0.1f, "VerticalVel");
            tStartToLand.hasExitTime = false;
            tStartToLand.duration = 0.08f;

            var tLoopToLand = sJumpLoop.AddTransition(sJumpLand);
            tLoopToLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tLoopToLand.AddCondition(AnimatorConditionMode.Less, 0.1f, "VerticalVel");
            tLoopToLand.hasExitTime = false;
            tLoopToLand.duration = 0.08f;

            var tLandToLoco = sJumpLand.AddTransition(sLoco);
            tLandToLoco.hasExitTime = true;
            tLandToLoco.exitTime = 0.62f;
            tLandToLoco.duration = 0.18f;

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
                $"    \"jog\": \"{(cJog ? cJog.name : "")} (human={cJog?.isHumanMotion}, fps={cJog?.frameRate})\",\n" +
                $"    \"run\": \"{(cRun ? cRun.name : "")} (human={cRun?.isHumanMotion}, fps={cRun?.frameRate})\",\n" +
                $"    \"jump_start\": \"{(cJumpStart ? cJumpStart.name : "")} (human={cJumpStart?.isHumanMotion}, fps={cJumpStart?.frameRate})\",\n" +
                $"    \"jump_loop\": \"{(cJumpLoop ? cJumpLoop.name : "")} (human={cJumpLoop?.isHumanMotion}, fps={cJumpLoop?.frameRate})\",\n" +
                $"    \"jump_land\": \"{(cJumpLand ? cJumpLand.name : "")} (human={cJumpLand?.isHumanMotion}, fps={cJumpLand?.frameRate})\"\n" +
                $"  }}\n" +
                "}\n";
            File.WriteAllText(repPath, json);
            Debug.Log($"[HeroDezAAA] STACK AAA CONCLUIDA! springBones={springCount} | report={repPath}");
        }
    }
}
