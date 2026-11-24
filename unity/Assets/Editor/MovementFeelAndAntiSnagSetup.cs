using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Aplica os fixes de anti-travamento, PhysicsMaterial zero-friction e SFX de passos/landing
    /// nas cenas jogaveis (Caldeiral.unity e SetorExterno.unity).
    /// </summary>
    public static class MovementFeelAndAntiSnagSetup
    {
        private const string ZeroFrictionMatPath = "Assets/Game/Materials/PhysMat_ZeroFriction.physicMaterial";
        private const string FloorSfxPath  = "Assets/Game/Audio/SFX/Movement/footstep_floor_TEMP.wav";
        private const string MetalSfxPath  = "Assets/Game/Audio/SFX/Movement/footstep_metal_TEMP.wav";
        private const string PuddleSfxPath = "Assets/Game/Audio/SFX/Movement/footstep_puddle_TEMP.wav";
        private const string LandSfxPath   = "Assets/Game/Audio/SFX/Movement/landing_thud_TEMP.wav";

        private static readonly string[] PlayableScenes =
        {
            "Assets/Game/Scenes/Caldeiral.unity",
            "Assets/Game/Scenes/SetorExterno.unity",
        };

        [MenuItem("Studio/Concerto/Movement/Apply Anti-Snag & Surface Footsteps to All Scenes")]
        public static void ApplyMenu() => ApplyAll();

        public static void ApplyBatch()
        {
            try
            {
                ApplyAll();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MovementSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyAll()
        {
            AssetDatabase.Refresh();

            // 1. Cria PhysicsMaterial zero-friction canonico (elimina atrito lateral em quinas/paredes)
            var zeroMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ZeroFrictionMatPath);
            if (zeroMat == null)
            {
                zeroMat = new PhysicsMaterial("PhysMat_ZeroFriction");
                AssetDatabase.CreateAsset(zeroMat, ZeroFrictionMatPath);
            }
            zeroMat.dynamicFriction = 0f;
            zeroMat.staticFriction = 0f;
            zeroMat.bounciness = 0f;
            zeroMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            zeroMat.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(zeroMat);

            var floorClip  = AssetDatabase.LoadAssetAtPath<AudioClip>(FloorSfxPath);
            var metalClip  = AssetDatabase.LoadAssetAtPath<AudioClip>(MetalSfxPath);
            var puddleClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PuddleSfxPath);
            var landClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(LandSfxPath);
            var locoType   = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");

            foreach (string scenePath in PlayableScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var dez = GameObject.Find("Player_Dez_Placeholder");
                if (dez == null) continue;

                // Ajusta CharacterController com skinWidth=0.05, stepOffset=0.36, slopeLimit=50, minMoveDistance=0
                var cc = dez.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.skinWidth = 0.05f;
                    cc.stepOffset = 0.36f;
                    cc.slopeLimit = 50f;
                    cc.minMoveDistance = 0f;
                    cc.material = zeroMat;
                }

                // Garante AudioSource dedicado de passos/landing no Dez
                Transform footGo = dez.transform.Find("Dez_FootstepAudio");
                if (footGo == null)
                {
                    var g = new GameObject("Dez_FootstepAudio");
                    g.transform.SetParent(dez.transform, false);
                    footGo = g.transform;
                }
                var footSrc = footGo.GetComponent<AudioSource>();
                if (footSrc == null) footSrc = footGo.gameObject.AddComponent<AudioSource>();
                footSrc.playOnAwake = false;
                footSrc.spatialBlend = 0f;

                var loco = dez.GetComponent(locoType);
                if (loco != null)
                {
                    var so = new SerializedObject(loco);
                    so.FindProperty("footstepAudio").objectReferenceValue      = footSrc;
                    so.FindProperty("footstepFloorClip").objectReferenceValue  = floorClip;
                    so.FindProperty("footstepMetalClip").objectReferenceValue  = metalClip;
                    so.FindProperty("footstepPuddleClip").objectReferenceValue = puddleClip;
                    so.FindProperty("landingThudClip").objectReferenceValue    = landClip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[MovementSetup] Anti-snag + ZeroFriction + Surface Footsteps aplicados em {scenePath}");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
