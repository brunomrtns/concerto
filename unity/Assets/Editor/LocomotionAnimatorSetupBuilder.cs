using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Corrige o degrau 4 do diagnostico em HeroDezController.controller:
    ///   - Casa thresholds do BlendTree Locomotion_1D exatamente com DezMovementConfig (Idle=0, Walk=2.2, Run=5.4)
    ///   - Elimina mistura pastosa walk+run durante caminhada normal (2.2 m/s)
    ///   - Adiciona parametro LocomotionSpeedMult ligado ao speedParameter do estado Locomotion (zero foot slide!)
    /// </summary>
    public static class LocomotionAnimatorSetupBuilder
    {
        private const string CtrlPath = "Assets/Game/Animations/HeroDezController.controller";

        [MenuItem("Studio/Concerto/Animation/Fix HeroDezController Blend Thresholds & Speed Ratio")]
        public static void FixMenu() => Fix();

        public static void FixBatch()
        {
            try
            {
                Fix();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocomotionAnimFix] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Fix()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null) throw new Exception($"AnimatorController nao encontrado: {CtrlPath}");

            // 1. Garante parametro float LocomotionSpeedMult (default 1.0)
            if (!ctrl.parameters.Any(p => p.name == "LocomotionSpeedMult"))
            {
                ctrl.AddParameter(new AnimatorControllerParameter
                {
                    name = "LocomotionSpeedMult",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1.0f,
                });
            }

            // 2. Ajusta estado Locomotion e thresholds do BlendTree 1D para 0.0 / 2.2 / 5.4
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states)
            {
                if (cs.state.name != "Locomotion") continue;

                cs.state.speedParameterActive = true;
                cs.state.speedParameter = "LocomotionSpeedMult";

                if (cs.state.motion is BlendTree bt)
                {
                    var children = bt.children;
                    if (children != null && children.Length >= 3)
                    {
                        children[0].threshold = 0.0f;  // Idle
                        children[1].threshold = 2.2f;  // Walk (igual a DezMovementConfig.walkSpeed)
                        children[2].threshold = 5.4f;  // Run  (igual a DezMovementConfig.runSpeed)
                        bt.useAutomaticThresholds = false;
                        bt.children = children;
                        EditorUtility.SetDirty(bt);
                    }
                }
                EditorUtility.SetDirty(cs.state);
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[LocomotionAnimFix] HeroDezController atualizado: thresholds 0.0 / 2.2 / 5.4 + LocomotionSpeedMult ativo!");
        }
    }
}
