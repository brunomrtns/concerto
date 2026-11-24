using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Aplica a Matriz de Colisao canonica + ActorSolidSeparation (com SolidBody_Capsule zero-friction)
    /// em todas as cenas jogaveis (Caldeiral.unity e SetorExterno.unity).
    /// </summary>
    public static class CollisionSystemSetupBuilder
    {
        private const string ZeroFrictionMatPath = "Assets/Game/Materials/PhysMat_ZeroFriction.physicMaterial";
        private static readonly string[] PlayableScenes =
        {
            "Assets/Game/Scenes/Caldeiral.unity",
            "Assets/Game/Scenes/SetorExterno.unity",
        };

        [MenuItem("Studio/Concerto/Collision/Apply Collision Matrix & Solid Separation to All Scenes")]
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
                Debug.LogError($"[CollisionSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyAll()
        {
            AssetDatabase.Refresh();

            var bootType = Type.GetType("Studio.Game.Collision.CollisionMatrixBootstrap, Assembly-CSharp");
            var sepType  = Type.GetType("Studio.Game.Collision.ActorSolidSeparation, Assembly-CSharp");
            var kindType = Type.GetType("Studio.Game.Collision.ActorBodyKind, Assembly-CSharp");
            if (bootType == null || sepType == null || kindType == null)
                throw new Exception("Scripts Studio.Game.Collision nao encontrados!");

            bootType.GetMethod("ApplyMatrix").Invoke(null, null);
            var zeroMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ZeroFrictionMatPath);

            int layerPlayer = (int)bootType.GetField("LayerPlayer").GetValue(null);
            int layerNpc    = (int)bootType.GetField("LayerNPC").GetValue(null);

            foreach (string scenePath in PlayableScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // 1. Player_Dez_Placeholder -> Layer Player + ActorSolidSeparation(Player)
                var dez = GameObject.Find("Player_Dez_Placeholder");
                if (dez != null)
                {
                    dez.layer = layerPlayer;
                    var sep = dez.GetComponent(sepType);
                    if (sep == null) sep = dez.AddComponent(sepType);
                    var so = new SerializedObject(sep);
                    so.FindProperty("bodyKind").enumValueIndex = 0; // Player
                    so.FindProperty("solidRadius").floatValue = 0.28f;
                    so.FindProperty("solidHeight").floatValue = 1.25f;
                    so.FindProperty("createSolidCapsuleIfMissing").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // 2. Todos os NPCs na cena -> Layer NPC + ActorSolidSeparation(NPC) + SolidBody_Capsule (radius=0.30 << interactRadius=2.5)
                var npcCount = 0;
                var allGos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var go in allGos)
                {
                    if (go == null || !go.name.StartsWith("NPC_")) continue;
                    go.layer = layerNpc;
                    var sep = go.GetComponent(sepType);
                    if (sep == null) sep = go.AddComponent(sepType);
                    var so = new SerializedObject(sep);
                    so.FindProperty("bodyKind").enumValueIndex = 1; // NPC
                    so.FindProperty("solidRadius").floatValue = 0.36f;
                    so.FindProperty("solidHeight").floatValue = 1.25f;
                    so.FindProperty("createSolidCapsuleIfMissing").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // Garante SolidBody_Capsule em tempo de edicao com PhysMat_ZeroFriction
                    Transform capT = go.transform.Find("SolidBody_Capsule");
                    if (capT == null)
                    {
                        var cgo = new GameObject("SolidBody_Capsule");
                        cgo.transform.SetParent(go.transform, false);
                        capT = cgo.transform;
                    }
                    capT.gameObject.layer = layerNpc;
                    var cap = capT.GetComponent<CapsuleCollider>();
                    if (cap == null) cap = capT.gameObject.AddComponent<CapsuleCollider>();
                    cap.isTrigger = false;
                    cap.radius = 0.36f;
                    cap.height = 1.25f;
                    cap.center = new Vector3(0f, 0.625f, 0f);
                    if (zeroMat != null) cap.sharedMaterial = zeroMat;
                    npcCount++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[CollisionSetup] Matriz + ActorSolidSeparation aplicados em {scenePath} (NPCs={npcCount})");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
