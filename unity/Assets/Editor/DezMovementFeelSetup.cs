using System;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// DezMovementFeelSetup — Instala o DezMovementConfig (ScriptableObject),
    /// a Camera 3a Pessoa Cinemachine 3.1.4 (VCam_Gameplay_Dez com ThirdPersonFollow + Deoccluder),
    /// e os efeitos do Impulso Mecanico (Particulas + SFX TEMP) no Player_Dez_Placeholder em Caldeiral.unity.
    /// </summary>
    public static class DezMovementFeelSetup
    {
        private const string ScenePath       = "Assets/Game/Scenes/Caldeiral.unity";
        private const string ConfigDir       = "Assets/Game/Config";
        private const string ConfigAssetPath = "Assets/Game/Config/DezMovementConfig.asset";
        private const string DashSfxPath     = "Assets/Game/Audio/SFX/dez_thruster_dash_TEMP.wav";
        private const string GlowMatPath     = "Assets/Game/Cutscenes/Mat_CS02_Particle_Glow.mat";

        [MenuItem("Studio/Concerto/Player/Setup Dez Movement Feel & Cinemachine 3P Camera")]
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
                Debug.LogError($"[DezMovementFeelSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            EnsureDir(ConfigDir);
            AssetDatabase.Refresh();

            // 1. Cria ou carrega DezMovementConfig.asset (ScriptableObject)
            var cfgType = Type.GetType("Studio.Game.Animation.DezMovementConfig, Assembly-CSharp");
            if (cfgType == null) throw new Exception("DezMovementConfig nao encontrado em Assembly-CSharp!");

            var cfgAsset = AssetDatabase.LoadAssetAtPath(ConfigAssetPath, cfgType);
            if (cfgAsset == null)
            {
                var created = ScriptableObject.CreateInstance(cfgType);
                AssetDatabase.CreateAsset(created, ConfigAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ConfigAssetPath);
                cfgAsset = AssetDatabase.LoadAssetAtPath(ConfigAssetPath, cfgType);
            }

            // 2. Abre Caldeiral.unity
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo == null) throw new Exception("Player_Dez_Placeholder nao encontrado em Caldeiral.unity!");

            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            if (locoType == null) throw new Exception("DezLocomotionController nao encontrado!");
            var locoComp = dezGo.GetComponent(locoType);
            if (locoComp == null) locoComp = dezGo.AddComponent(locoType);

            // 3. Pivot da camera 3a pessoa no Dez
            Transform pivot = dezGo.transform.Find("Dez_CameraTarget");
            if (pivot == null)
            {
                var pGo = new GameObject("Dez_CameraTarget");
                pGo.transform.SetParent(dezGo.transform, false);
                pivot = pGo.transform;
            }
            pivot.localPosition = new Vector3(0f, 1.25f, 0f);
            pivot.localRotation = Quaternion.identity;

            // 4. Particulas + AudioSource dos propulsores (Dash mecanico)
            Transform oldThruster = dezGo.transform.Find("Dez_ThrusterParticles");
            if (oldThruster != null) UnityEngine.Object.DestroyImmediate(oldThruster.gameObject);
            var thGo = new GameObject("Dez_ThrusterParticles");
            thGo.transform.SetParent(dezGo.transform, false);
            thGo.transform.localPosition = new Vector3(0f, 0.72f, -0.22f);
            thGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var ps = thGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.22f;
            main.startLifetime = 0.26f;
            main.startSpeed = 4.5f;
            main.startSize = 0.16f;
            main.startColor = new Color(0.35f, 0.92f, 1.0f, 0.95f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 22f;
            sh.radius = 0.12f;
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            if (glowMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;

            Transform oldAudio = dezGo.transform.Find("Dez_ThrusterAudio");
            if (oldAudio != null) UnityEngine.Object.DestroyImmediate(oldAudio.gameObject);
            var audioGo = new GameObject("Dez_ThrusterAudio");
            audioGo.transform.SetParent(dezGo.transform, false);
            var audioSrc = audioGo.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;
            var dashClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DashSfxPath);

            // 5. Camera 3a Pessoa Cinemachine 3.1.4 (Priority 10 < CS01 20 < CS02 25!)
            var oldVcam = GameObject.Find("VCam_Gameplay_Dez");
            if (oldVcam != null) UnityEngine.Object.DestroyImmediate(oldVcam);
            var vcamGo = new GameObject("VCam_Gameplay_Dez");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Priority = 10;
            vcam.Target.TrackingTarget = pivot;
            vcam.Lens = new LensSettings
            {
                FieldOfView = 52f,
                NearClipPlane = 0.1f,
                FarClipPlane = 200f,
                Dutch = 0f
            };

            var tpFollow = vcamGo.AddComponent<CinemachineThirdPersonFollow>();
            tpFollow.ShoulderOffset = new Vector3(0.38f, 0.12f, 0f);
            tpFollow.VerticalArmLength = 0.18f;
            tpFollow.CameraSide = 1.0f;
            tpFollow.CameraDistance = 3.2f;
            tpFollow.Damping = new Vector3(0.12f, 0.16f, 0.14f);
            tpFollow.AvoidObstacles.Enabled = true;
            tpFollow.AvoidObstacles.CollisionFilter = ~0;
            tpFollow.AvoidObstacles.IgnoreTag = "Player";
            tpFollow.AvoidObstacles.CameraRadius = 0.24f;
            tpFollow.AvoidObstacles.DampingIntoCollision = 0.05f;
            tpFollow.AvoidObstacles.DampingFromCollision = 0.45f;

            // Garante tag Player no Dez para a camera nunca colidir com o proprio corpo do Dez
            dezGo.tag = "Player";

            // Garante CinemachineBrain na Main Camera com blend suave
            var mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (mainCam != null)
            {
                var brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null) brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.45f);
                brain.enabled = true;
            }

            // 6. Liga referencias serializadas no DezLocomotionController
            cfgAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigAssetPath);
            if (cfgAsset == null) throw new Exception($"Falha ao recarregar {ConfigAssetPath}");
            locoType.GetField("config").SetValue(locoComp, cfgAsset);
            var so = new SerializedObject(locoComp);
            so.Update();
            so.FindProperty("config").objectReferenceValue = cfgAsset;
            so.FindProperty("enablePlayableCamera").boolValue = true;
            so.FindProperty("cameraFollowTarget").objectReferenceValue = pivot;
            so.FindProperty("gameplayVcam").objectReferenceValue = vcam;
            so.FindProperty("thrusterParticles").objectReferenceValue = ps;
            so.FindProperty("thrusterAudio").objectReferenceValue = audioSrc;
            so.FindProperty("dashSfxClip").objectReferenceValue = dashClip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(locoComp);
            EditorSceneManager.MarkSceneDirty(scene);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[DezMovementFeelSetup] DezMovementConfig + VCam_Gameplay_Dez (CinemachineThirdPersonFollow + AvoidObstacles) + Propulsores de Dash configurados em Caldeiral.unity!");
        }

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
