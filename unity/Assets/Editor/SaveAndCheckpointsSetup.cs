using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// SaveAndCheckpointsSetup — Instala o SaveCheckpointHUD (icone sutil de 1s)
    /// e 3 triggers discretos de Checkpoint de area em Caldeiral.unity.
    /// </summary>
    public static class SaveAndCheckpointsSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Caldeiral.unity";

        [MenuItem("Studio/Concerto/Save/Setup Checkpoints & Save Indicator HUD in Caldeiral")]
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
                Debug.LogError($"[SaveAndCheckpointsSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var hudType = Type.GetType("Studio.Game.Save.SaveCheckpointHUD, Assembly-CSharp");
            var cpType  = Type.GetType("Studio.Game.Save.CheckpointTrigger, Assembly-CSharp");
            if (hudType == null || cpType == null)
                throw new Exception("Tipos de Studio.Game.Save nao encontrados!");

            var oldMgr = GameObject.Find("Save_Checkpoints_Manager");
            if (oldMgr != null) UnityEngine.Object.DestroyImmediate(oldMgr);
            var root = new GameObject("Save_Checkpoints_Manager");
            var hudComp = root.AddComponent(hudType);

            // Icone sutil de auto-save (1s no canto inferior esquerdo — sem tela de loading!)
            var (cg, txt) = BuildSaveIndicatorCanvas(root.transform);
            var so = new SerializedObject(hudComp);
            so.FindProperty("saveIndicatorGroup").objectReferenceValue = cg;
            so.FindProperty("saveIndicatorText").objectReferenceValue = txt;
            so.FindProperty("indicatorDurationSec").floatValue = 1.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 3 Checkpoints discretos de entrada de area na oficina
            CreateCheckpoint(root.transform, cpType, "cp_oficina_bancada",   new Vector3(-28.5f, 0.8f, -18.5f), new Vector3(3.0f, 2.0f, 3.0f));
            CreateCheckpoint(root.transform, cpType, "cp_oficina_maquina",   new Vector3(-26.2f, 0.8f, -16.8f), new Vector3(3.2f, 2.0f, 3.2f));
            CreateCheckpoint(root.transform, cpType, "cp_oficina_passarela", new Vector3(-18.5f, 0.8f, -18.5f), new Vector3(3.5f, 2.5f, 3.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[SaveAndCheckpointsSetup] SaveCheckpointHUD + 3 CheckpointTriggers instalados em Caldeiral.unity!");
        }

        private static void CreateCheckpoint(Transform parent, Type cpType, string id, Vector3 pos, Vector3 size)
        {
            var go = new GameObject($"CP_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            var cp = go.AddComponent(cpType);
            var so = new SerializedObject(cp);
            so.FindProperty("checkpointId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (CanvasGroup, Text) BuildSaveIndicatorCanvas(Transform parent)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("SaveIndicator_Canvas");
            canvasGo.transform.SetParent(parent, false);
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 85;
            var sc = canvasGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var pill = new GameObject("SavePill");
            pill.transform.SetParent(canvasGo.transform, false);
            var rt = pill.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(28f, 28f);
            rt.sizeDelta = new Vector2(150f, 36f);
            var cg = pill.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            pill.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.84f);
            var outl = pill.AddComponent<Outline>();
            outl.effectColor = new Color(0.30f, 0.88f, 0.96f, 0.90f);
            outl.effectDistance = new Vector2(1.5f, -1.5f);

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(pill.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = font;
            txt.text = "● SALVO";
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(0.30f, 0.88f, 0.96f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;

            return (cg, txt);
        }
    }
}
