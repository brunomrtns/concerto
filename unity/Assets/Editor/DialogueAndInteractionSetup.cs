using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// Monta o sistema de Interacao + Dialogo Data-Driven + Objetivo no HUD dentro de Caldeiral.unity:
    ///   - Cria Prefab uGUI DialogueHUD_Canvas.prefab (Prompt [E] Falar, Caixa de Dialogo datilografavel, Objetivo HUD)
    ///   - Anexa NpcDialogueInteractable na NPC_Juno_Oficina existente apontando para dlg_juno_oficina_intro.json
    ///   - Nao toca na Cutscene 01 nem cria personagem novo!
    /// </summary>
    public static class DialogueAndInteractionSetup
    {
        private const string ScenePath     = "Assets/Game/Scenes/Caldeiral.unity";
        private const string HudPrefabPath = "Assets/Game/Prefabs/UI/DialogueHUD_Canvas.prefab";
        private const string LocJsonPath   = "Assets/Game/Dialogue/Data/strings_pt_br.json";
        private const string JunoDlgPath   = "Assets/Game/Dialogue/Data/dlg_juno_oficina_intro.json";

        [MenuItem("Studio/Concerto/Dialogue/Setup Interaction + Dialogue + Objective HUD in Caldeiral")]
        public static void SetupMenu() => Setup();

        public static void SetupBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Setup()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Cria Prefab uGUI DialogueHUD_Canvas.prefab
            var hudPrefab = BuildDialogueHudPrefab();

            // 2. Instala Dialogue_And_Objective_System na cena (idempotente)
            var oldSys = GameObject.Find("Dialogue_And_Objective_System");
            if (oldSys != null) UnityEngine.Object.DestroyImmediate(oldSys);

            var sysRoot = new GameObject("Dialogue_And_Objective_System");
            var hudInst = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, sysRoot.transform);
            hudInst.name = "DialogueHUD_Canvas";

            var ctrlType = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
            var objType  = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            var npcType  = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            if (ctrlType == null || objType == null || npcType == null)
                throw new Exception("Scripts de Dialogue nao encontrados em Assembly-CSharp!");

            var objComp  = hudInst.GetComponent(objType);
            var ctrlComp = sysRoot.AddComponent(ctrlType);

            var soCtrl = new SerializedObject(ctrlComp);
            soCtrl.FindProperty("localizationJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(LocJsonPath);
            soCtrl.FindProperty("initialObjectiveKey").stringValue = "obj_talk_juno";
            soCtrl.FindProperty("interactRadius").floatValue = 2.5f;
            soCtrl.FindProperty("minAimDot").floatValue = 0.25f;
            soCtrl.FindProperty("charsPerSecond").floatValue = 44f;
            soCtrl.FindProperty("promptGroup").objectReferenceValue      = hudInst.transform.Find("Interact_Prompt").GetComponent<CanvasGroup>();
            soCtrl.FindProperty("promptText").objectReferenceValue       = hudInst.transform.Find("Interact_Prompt/PromptText").GetComponent<Text>();
            soCtrl.FindProperty("dialogueBoxGroup").objectReferenceValue = hudInst.transform.Find("Dialogue_Box").GetComponent<CanvasGroup>();
            soCtrl.FindProperty("speakerNameText").objectReferenceValue  = hudInst.transform.Find("Dialogue_Box/SpeakerName").GetComponent<Text>();
            soCtrl.FindProperty("dialogueBodyText").objectReferenceValue = hudInst.transform.Find("Dialogue_Box/BodyText").GetComponent<Text>();
            soCtrl.FindProperty("advanceHintText").objectReferenceValue  = hudInst.transform.Find("Dialogue_Box/AdvanceHint").GetComponent<Text>();
            soCtrl.FindProperty("objectiveSystem").objectReferenceValue  = objComp;
            soCtrl.ApplyModifiedPropertiesWithoutUndo();

            // 3. Anexa NpcDialogueInteractable na NPC_Juno_Oficina JÁ existente na oficina!
            var juno = GameObject.Find("NPC_Juno_Oficina");
            if (juno == null) throw new Exception("NPC_Juno_Oficina nao encontrado em Caldeiral.unity!");

            var junoInteract = juno.GetComponent(npcType);
            if (junoInteract == null) junoInteract = juno.AddComponent(npcType);

            var soJuno = new SerializedObject(junoInteract);
            soJuno.FindProperty("dialogueJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(JunoDlgPath);
            soJuno.ApplyModifiedPropertiesWithoutUndo();

            // Garante um SphereCollider trigger pequeno de interacao na Juno se nao houver
            var trig = juno.transform.Find("InteractTrigger");
            if (trig == null)
            {
                var tGo = new GameObject("InteractTrigger");
                tGo.transform.SetParent(juno.transform, false);
                tGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                var sc = tGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.65f;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[DialogueSetup] Sistema de Interacao + Dialogo + ObjectiveSystem instalado com sucesso na NPC_Juno_Oficina em Caldeiral.unity!");
        }

        private static GameObject BuildDialogueHudPrefab()
        {
            var root = new GameObject("DialogueHUD_Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Objetivo Discreto no canto superior esquerdo (UM objetivo ativo por vez)
            var objPanel = CreatePanel(root.transform, "Objective_HUD", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -32), new Vector2(540, 58), new Color(0.05f, 0.07f, 0.10f, 0.80f));
            var objGroup = objPanel.gameObject.AddComponent<CanvasGroup>();
            CreatePanel(objPanel, "AccentBar", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0), new Color(0.24f, 0.88f, 0.98f, 1f));
            var prefixTxt = CreateText(objPanel, "PrefixLabel", font, 16, TextAnchor.UpperLeft, new Color(0.35f, 0.90f, 1.0f), new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -6), new Vector2(-24, -12));
            prefixTxt.fontStyle = FontStyle.Bold;
            var objTxt = CreateText(objPanel, "ObjectiveLabel", font, 22, TextAnchor.LowerLeft, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 6), new Vector2(-24, -12));

            // 2. Prompt contextual "[E] Falar" (centro-baixo da tela)
            var promptPanel = CreatePanel(root.transform, "Interact_Prompt", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 210), new Vector2(260, 52), new Color(0.05f, 0.07f, 0.10f, 0.86f));
            promptPanel.gameObject.AddComponent<CanvasGroup>();
            CreatePanel(promptPanel, "TopLine", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 3), new Color(1.0f, 0.78f, 0.35f, 1f));
            var promptTxt = CreateText(promptPanel, "PromptText", font, 24, TextAnchor.MiddleCenter, new Color(1.0f, 0.94f, 0.80f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            promptTxt.fontStyle = FontStyle.Bold;

            // 3. Caixa de Dialogo inferior (nome do falante + texto datilografavel + dica [E] Continuar)
            var dlgPanel = CreatePanel(root.transform, "Dialogue_Box", new Vector2(0.14f, 0), new Vector2(0.86f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(0, 195), new Color(0.04f, 0.05f, 0.08f, 0.90f));
            dlgPanel.gameObject.AddComponent<CanvasGroup>();
            CreatePanel(dlgPanel, "TopBorder", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 4), new Color(0.95f, 0.72f, 0.30f, 1f));

            var speakerTxt = CreateText(dlgPanel, "SpeakerName", font, 26, TextAnchor.UpperLeft, new Color(1.0f, 0.80f, 0.38f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -16), new Vector2(-56, 36));
            speakerTxt.fontStyle = FontStyle.Bold;

            var bodyTxt = CreateText(dlgPanel, "BodyText", font, 28, TextAnchor.UpperLeft, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, -56), new Vector2(-56, -96));

            var advTxt = CreateText(dlgPanel, "AdvanceHint", font, 20, TextAnchor.LowerRight, new Color(0.55f, 0.92f, 1.0f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(-24, 14), new Vector2(-48, 30));
            advTxt.fontStyle = FontStyle.Bold;

            // Anexa ObjectiveSystem no root do HUD
            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            var objComp = root.AddComponent(objType);
            var soObj = new SerializedObject(objComp);
            soObj.FindProperty("hudGroup").objectReferenceValue = objGroup;
            soObj.FindProperty("prefixLabel").objectReferenceValue = prefixTxt;
            soObj.FindProperty("objectiveLabel").objectReferenceValue = objTxt;
            soObj.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return rt;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor align, Color col, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
