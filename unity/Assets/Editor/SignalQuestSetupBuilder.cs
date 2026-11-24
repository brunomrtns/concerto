using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Instala a Missao "Sinal" dentro de SetorExterno.unity (idempotente):
    ///   - Quest_Signal_Manager (SignalQuestController data-driven via quest_signal_config.json)
    ///   - AntennaCaptureInteractable + feixe ciano + audio na POI_02_Antena_TripoSG existente
    ///   - EastGate_To_Epicenter (EastGateInteractable) no perimetro leste do patio
    /// </summary>
    public static class SignalQuestSetupBuilder
    {
        private const string SectorScenePath  = "Assets/Game/Scenes/SetorExterno.unity";
        private const string QuestConfigPath  = "Assets/Game/Quests/Data/quest_signal_config.json";
        private const string IntroDlgPath     = "Assets/Game/Dialogue/Data/dlg_vera_signal_intro.json";
        private const string ResultDlgPath    = "Assets/Game/Dialogue/Data/dlg_vera_signal_result.json";
        private const string CaptureSfxPath   = "Assets/Game/Audio/SFX/Sector/signal_antenna_capture_TEMP.wav";

        [MenuItem("Studio/Concerto/Quests/Setup Signal Quest in SetorExterno")]
        public static void BuildMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalQuestSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(SectorScenePath, OpenSceneMode.Single);

            // 1. Configura AntennaCaptureInteractable na POI_02_Antena_TripoSG ja existente!
            var antenaGo = GameObject.Find("POI_02_Antena_TripoSG");
            if (antenaGo == null) throw new Exception("POI_02_Antena_TripoSG nao encontrada em SetorExterno.unity!");

            var capType = Type.GetType("Studio.Game.Quests.AntennaCaptureInteractable, Assembly-CSharp");
            var oldCap = antenaGo.GetComponent(capType);
            if (oldCap != null) UnityEngine.Object.DestroyImmediate(oldCap);
            var oldBeam = antenaGo.transform.Find("Antenna_SkyBeam_Visual");
            if (oldBeam != null) UnityEngine.Object.DestroyImmediate(oldBeam.gameObject);
            var oldLight = antenaGo.transform.Find("Antenna_PulseLight");
            if (oldLight != null) UnityEngine.Object.DestroyImmediate(oldLight.gameObject);
            var oldTrig = antenaGo.transform.Find("Antenna_CaptureTrigger");
            if (oldTrig != null) UnityEngine.Object.DestroyImmediate(oldTrig.gameObject);

            var capComp = antenaGo.AddComponent(capType);

            var beamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamGo.name = "Antenna_SkyBeam_Visual";
            UnityEngine.Object.DestroyImmediate(beamGo.GetComponent<Collider>());
            beamGo.transform.SetParent(antenaGo.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, 16f, 0f);
            beamGo.transform.localScale = new Vector3(0.45f, 16f, 0.45f);
            var beamMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            beamMat.SetColor("_BaseColor", new Color(0.35f, 2.2f, 2.6f, 0.85f));
            beamGo.GetComponent<MeshRenderer>().sharedMaterial = beamMat;
            beamGo.SetActive(false);

            var lightGo = new GameObject("Antenna_PulseLight");
            lightGo.transform.SetParent(antenaGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.28f, 0.94f, 1.0f);
            pl.intensity = 3.0f;
            pl.range = 14f;
            pl.enabled = false;

            var trigGo = new GameObject("Antenna_CaptureTrigger");
            trigGo.transform.SetParent(antenaGo.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var sc = trigGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.8f;

            var sfx = antenaGo.GetComponent<AudioSource>();
            if (sfx == null) sfx = antenaGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            var capSo = new SerializedObject(capComp);
            capSo.FindProperty("captureDurationSec").floatValue = 2.8f;
            capSo.FindProperty("sfxSource").objectReferenceValue = sfx;
            capSo.FindProperty("captureClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CaptureSfxPath);
            capSo.FindProperty("skyBeamVisual").objectReferenceValue = beamGo;
            capSo.FindProperty("antennaPulseLight").objectReferenceValue = pl;
            capSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. Instala Portao Leste (EastGate_To_Epicenter) no perimetro leste do patio!
            var oldGate = GameObject.Find("EastGate_To_Epicenter");
            if (oldGate != null) UnityEngine.Object.DestroyImmediate(oldGate);

            var gateRoot = new GameObject("EastGate_To_Epicenter");
            gateRoot.transform.position = new Vector3(23.5f, 0.0f, 12.0f);

            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var frameMat = new Material(lit);
            frameMat.color = new Color(0.16f, 0.20f, 0.27f);
            frameMat.SetFloat("_Metallic", 0.72f);

            var leafMat = new Material(lit);
            leafMat.color = new Color(0.24f, 0.30f, 0.38f);
            leafMat.SetFloat("_Metallic", 0.65f);

            // Pilares e topo do Portao Leste
            CreatePart(gateRoot.transform, "Pillar_North", new Vector3(0f, 2.4f,  3.2f), new Vector3(1.1f, 4.8f, 1.0f), frameMat);
            CreatePart(gateRoot.transform, "Pillar_South", new Vector3(0f, 2.4f, -3.2f), new Vector3(1.1f, 4.8f, 1.0f), frameMat);
            CreatePart(gateRoot.transform, "Lintel_Top",   new Vector3(0f, 4.9f,  0.0f), new Vector3(1.3f, 0.8f, 7.6f), frameMat);

            var leftLeaf  = CreatePart(gateRoot.transform, "GateLeaf_South", new Vector3(0f, 2.2f, -1.35f), new Vector3(0.45f, 4.4f, 2.65f), leafMat);
            var rightLeaf = CreatePart(gateRoot.transform, "GateLeaf_North", new Vector3(0f, 2.2f,  1.35f), new Vector3(0.45f, 4.4f, 2.65f), leafMat);

            var lightBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lightBar.name = "EastGate_StatusLight";
            UnityEngine.Object.DestroyImmediate(lightBar.GetComponent<Collider>());
            lightBar.transform.SetParent(gateRoot.transform, false);
            lightBar.transform.localPosition = new Vector3(-0.72f, 4.55f, 0f);
            lightBar.transform.localScale = new Vector3(0.18f, 0.28f, 4.4f);
            var sMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            sMat.color = new Color(0.95f, 0.20f, 0.16f);
            lightBar.GetComponent<MeshRenderer>().sharedMaterial = sMat;

            var gateTrig = gateRoot.AddComponent<BoxCollider>();
            gateTrig.isTrigger = true;
            gateTrig.center = new Vector3(-1.4f, 1.8f, 0f);
            gateTrig.size = new Vector3(3.2f, 3.6f, 5.5f);

            var gateType = Type.GetType("Studio.Game.Quests.EastGateInteractable, Assembly-CSharp");
            var gateComp = gateRoot.AddComponent(gateType);
            var gSo = new SerializedObject(gateComp);
            gSo.FindProperty("leftDoorLeaf").objectReferenceValue = leftLeaf.transform;
            gSo.FindProperty("rightDoorLeaf").objectReferenceValue = rightLeaf.transform;
            gSo.FindProperty("statusLightRenderer").objectReferenceValue = lightBar.GetComponent<MeshRenderer>();
            gSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. Instala Quest_Signal_Manager (SignalQuestController)
            var oldMgr = GameObject.Find("Quest_Signal_Manager");
            if (oldMgr != null) UnityEngine.Object.DestroyImmediate(oldMgr);

            var mgrGo = new GameObject("Quest_Signal_Manager");
            var qType = Type.GetType("Studio.Game.Quests.SignalQuestController, Assembly-CSharp");
            var qComp = mgrGo.AddComponent(qType);

            var veraGo = GameObject.Find("NPC_Engenheira_Vera");
            var npcType = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var veraNpc = veraGo != null ? veraGo.GetComponent(npcType) : null;
            var amType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var amComp = UnityEngine.Object.FindAnyObjectByType(amType);

            var qSo = new SerializedObject(qComp);
            qSo.FindProperty("questConfigJson").objectReferenceValue        = AssetDatabase.LoadAssetAtPath<TextAsset>(QuestConfigPath);
            qSo.FindProperty("veraIntroDialogueJson").objectReferenceValue  = AssetDatabase.LoadAssetAtPath<TextAsset>(IntroDlgPath);
            qSo.FindProperty("veraResultDialogueJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(ResultDlgPath);
            qSo.FindProperty("veraNpc").objectReferenceValue                = veraNpc;
            qSo.FindProperty("anomalyManager").objectReferenceValue         = amComp;
            qSo.FindProperty("antennaCapture").objectReferenceValue         = capComp;
            qSo.FindProperty("eastGate").objectReferenceValue               = gateComp;
            qSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SectorScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[SignalQuestSetup] Missao 'Sinal' + AntennaCapture + EastGate instalados em SetorExterno.unity!");
        }

        private static GameObject CreatePart(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }
    }
}
