using System;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// RepairSystemSetup — Instala o Sistema de Reparo ("O Concerto" — Ato 2) em Caldeiral.unity:
    ///   - Importa dez_repair_tool.fbx (TripoSG + PBR 4K + emissivo ciano) -> prefab reutilizavel dez_repair_tool.prefab
    ///   - Anexa Repairable + SphereCollider trigger em todas as 5 Anomalias de Act2_Workshop_Anomalies
    ///   - Cria VCam_Repair_Focus (Cinemachine 3.1.4, Priority 15), Repair_ResolveFlash_Volume, Repair_SparksParticles e RepairHUD_Canvas
    ///   - Reusa cs01_musicbox_motif_TEMP.wav para o trecho musical de resolucao + 3 SFX TEMP de reparo/estalo
    /// </summary>
    public static class RepairSystemSetup
    {
        private const string ScenePath       = "Assets/Game/Scenes/Caldeiral.unity";
        private const string ToolFbxPath     = "Assets/Game/Art/Generated/repair/dez_repair_tool.fbx";
        private const string ToolPrefabPath  = "Assets/Game/Prefabs/Fab3D/dez_repair_tool.prefab";
        private const string Fab3dMatDir     = "Assets/Game/Materials/Fab3D";
        private const string FlashProfPath   = "Assets/Game/Cutscenes/Repair_ResolveFlashProfile.asset";
        private const string GlowMatPath     = "Assets/Game/Cutscenes/Mat_CS02_Particle_Glow.mat";
        private const string MusicBoxPath    = "Assets/Game/Audio/Cutscenes/CS01/cs01_musicbox_motif_TEMP.wav";
        private const string RepairAudioDir  = "Assets/Game/Audio/SFX/Repair";

        [MenuItem("Studio/Concerto/Act2/Setup Repair System (O Concerto) in Caldeiral")]
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
                Debug.LogError($"[RepairSystemSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();

            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ToolFbxPath)))
                throw new FileNotFoundException($"FBX da ferramenta de reparo nao encontrado (TripoSG obrigatorio!): {ToolFbxPath}");

            ConfigurePropFbx(ToolFbxPath);
            var toolMat = EnsureToolMaterial();
            var toolPrefab = CreateToolPrefab(toolMat);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo == null) throw new Exception("Player_Dez_Placeholder nao encontrado em Caldeiral.unity!");

            var anomRoot = GameObject.Find("Act2_Workshop_Anomalies");
            if (anomRoot == null) throw new Exception("Act2_Workshop_Anomalies nao encontrado em Caldeiral.unity (Cutscene 3 obrigatoria!)");

            // 1. Anexa componente generico Repairable + SphereCollider trigger em cada Anomaly da cena
            var anomType = Type.GetType("Studio.Game.Anomalies.Anomaly, Assembly-CSharp");
            var repType  = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var ctrlType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            if (anomType == null || repType == null || ctrlType == null)
                throw new Exception("Tipos de Studio.Game.Repair / Anomalies nao encontrados!");

            var anomalies = anomRoot.GetComponentsInChildren(anomType, true);
            foreach (var a in anomalies)
            {
                var go = a.gameObject;
                var rComp = go.GetComponent(repType);
                if (rComp == null) rComp = go.AddComponent(repType);

                var rSo = new SerializedObject(rComp);
                rSo.FindProperty("repairableId").stringValue = (string)anomType.GetField("anomalyId").GetValue(a);
                rSo.FindProperty("displayName").stringValue  = (string)anomType.GetField("displayName").GetValue(a);
                rSo.FindProperty("repairDurationSec").floatValue = 4.2f;
                rSo.FindProperty("linkedAnomaly").objectReferenceValue = a;
                rSo.ApplyModifiedPropertiesWithoutUndo();

                var sc = go.GetComponent<SphereCollider>();
                if (sc == null) sc = go.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 1.9f;
            }

            // 2. Instancia ferramenta TripoSG na mao direita do Dez (oculta fora do reparo)
            var oldHandTool = dezGo.transform.Find("Dez_RepairTool_InHand");
            if (oldHandTool != null) UnityEngine.Object.DestroyImmediate(oldHandTool.gameObject);
            var handTool = (GameObject)PrefabUtility.InstantiatePrefab(toolPrefab, dezGo.transform);
            handTool.name = "Dez_RepairTool_InHand";
            handTool.transform.localPosition = new Vector3(0.32f, 0.72f, 0.36f);
            handTool.transform.localRotation = Quaternion.Euler(18f, -15f, -35f);
            handTool.SetActive(false);

            // 3. Monta Repair_System_Manager (Controller + VCam Priority 15 + Flash Volume + Sparks + Audio + HUD)
            var oldMgr = GameObject.Find("Repair_System_Manager");
            if (oldMgr != null) UnityEngine.Object.DestroyImmediate(oldMgr);
            var mgrRoot = new GameObject("Repair_System_Manager");
            var ctrlComp = mgrRoot.AddComponent(ctrlType);

            // VCam_Repair_Focus (Priority 15: acima do Gameplay 10, abaixo das Cutscenes 20/25/30!)
            var vcamGo = new GameObject("VCam_Repair_Focus");
            vcamGo.transform.SetParent(mgrRoot.transform, false);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Priority = 15;
            vcam.Lens = new LensSettings { FieldOfView = 42f, NearClipPlane = 0.08f, FarClipPlane = 200f, Dutch = 0f };
            vcamGo.SetActive(false);

            // Volume de flash dourado suave ao resolver cada anomalia
            var flashVol = BuildResolveFlashVolume(mgrRoot.transform);

            // Faiscas de reparo
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            var sparks = BuildRepairSparks(mgrRoot.transform, glowMat);

            // AudioSources TEMP
            var chSrc = CreateAudio(mgrRoot.transform, "Audio_Repair_Channel", null, true, 0.55f);
            var sfxSrc = CreateAudio(mgrRoot.transform, "Audio_Repair_SFX", null, false, 0.85f);
            var mbSrc = CreateAudio(mgrRoot.transform, "Audio_Repair_MusicBoxSnippet", AssetDatabase.LoadAssetAtPath<AudioClip>(MusicBoxPath), false, 0.62f);

            // RepairHUD_Canvas (barra harmonica discreta coerente com o HUD)
            var (panelCg, titleTxt, fillImg, pulseImg, hintTxt) = BuildRepairHud(mgrRoot.transform);

            var trinket = GameObject.Find("CS03_Bugiganga_Quebrada");
            var lights  = GameObject.Find("Caldeiral_CeilingLights");
            var anomMgr = anomRoot.GetComponent(anomMgrType);

            var so = new SerializedObject(ctrlComp);
            so.FindProperty("repairFocusVcam").objectReferenceValue = vcam;
            so.FindProperty("resolveFlashVolume").objectReferenceValue = flashVol;
            so.FindProperty("dezRepairToolVisual").objectReferenceValue = handTool;
            so.FindProperty("repairSparksParticles").objectReferenceValue = sparks;
            so.FindProperty("workbenchTrinketProp").objectReferenceValue = trinket;
            so.FindProperty("ceilingLightsRoot").objectReferenceValue = lights;
            so.FindProperty("anomalyManager").objectReferenceValue = anomMgr;
            so.FindProperty("repairPanelGroup").objectReferenceValue = panelCg;
            so.FindProperty("repairTitleText").objectReferenceValue = titleTxt;
            so.FindProperty("repairProgressFill").objectReferenceValue = fillImg;
            so.FindProperty("harmonicPulseMarker").objectReferenceValue = pulseImg;
            so.FindProperty("repairHintText").objectReferenceValue = hintTxt;
            so.FindProperty("channelSource").objectReferenceValue = chSrc;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSrc;
            so.FindProperty("musicBoxSnippetSource").objectReferenceValue = mbSrc;
            so.FindProperty("toolChannelClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{RepairAudioDir}/repair_tool_channel_TEMP.wav");
            so.FindProperty("resolveChimeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{RepairAudioDir}/repair_resolve_chime_TEMP.wav");
            so.FindProperty("trinketCrackClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{RepairAudioDir}/act2_trinket_crack_TEMP.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[RepairSystemSetup] Sistema de Reparo ('O Concerto') montado em Caldeiral.unity com {anomalies.Length} anomalias reparaveis e ferramenta TripoSG!");
        }

        private static void ConfigurePropFbx(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }

        private static Material EnsureToolMaterial()
        {
            string slug = "dez_repair_tool";
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_albedo.png");
            var met = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_metallicsmooth.png");
            var emi = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_emission.png");
            if (alb != null) mat.SetTexture("_BaseMap", alb);
            if (met != null) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (emi != null)
            {
                mat.SetTexture("_EmissionMap", emi);
                mat.SetColor("_EmissionColor", new Color(0.25f, 1.35f, 1.85f) * 2.0f);
                mat.EnableKeyword("_EMISSION");
            }
            mat.SetFloat("_Smoothness", 0.62f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateToolPrefab(Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ToolFbxPath);
            var root = new GameObject("dez_repair_tool");
            var meshInst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            foreach (var r in meshInst.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            var rs = meshInst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                float maxDim = Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                meshInst.transform.localScale *= (0.52f / maxDim);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ToolPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Volume BuildResolveFlashVolume(Transform parent)
        {
            var go = new GameObject("Repair_ResolveFlash_Volume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 52f;
            vol.weight = 0f;

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(FlashProfPath) != null)
                AssetDatabase.DeleteAsset(FlashProfPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, FlashProfPath);

            var color = prof.Add<ColorAdjustments>(true);
            color.postExposure.Override(1.35f);
            color.colorFilter.Override(new Color(1.0f, 0.94f, 0.80f));
            var bloom = prof.Add<Bloom>(true);
            bloom.intensity.Override(2.1f);
            bloom.threshold.Override(0.65f);

            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            go.SetActive(false);
            return vol;
        }

        private static ParticleSystem BuildRepairSparks(Transform parent, Material glowMat)
        {
            var go = new GameObject("Repair_SparksParticles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.75f;
            main.startSpeed = 2.6f;
            main.startSize = 0.09f;
            main.startColor = new Color(1.0f, 0.86f, 0.45f, 0.95f);
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 45f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.25f;
            if (glowMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            return ps;
        }

        private static AudioSource CreateAudio(Transform parent, string name, AudioClip clip, bool loop, float vol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.volume = vol;
            src.spatialBlend = 0f;
            src.clip = clip;
            return src;
        }

        private static (CanvasGroup, Text, Image, Image, Text) BuildRepairHud(Transform parent)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("RepairHUD_Canvas");
            canvasGo.transform.SetParent(parent, false);
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 55;
            var sc = canvasGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("RepairPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 96f);
            rt.sizeDelta = new Vector2(680f, 132f);
            var cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            panel.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            var outl = panel.AddComponent<Outline>();
            outl.effectColor = new Color(0.76f, 0.54f, 0.28f, 0.95f);
            outl.effectDistance = new Vector2(2f, -2f);

            var titleTxt = MakeText(panel.transform, "Title", "CONSERTANDO ANOMALIA", font, 22, new Color(0.99f, 0.85f, 0.58f), FontStyle.Bold, new Vector2(0f, 38f), new Vector2(620f, 34f));

            var barBg = new GameObject("ProgressBg");
            barBg.transform.SetParent(panel.transform, false);
            var bRt = barBg.AddComponent<RectTransform>();
            bRt.sizeDelta = new Vector2(560f, 20f);
            bRt.anchoredPosition = new Vector2(0f, 2f);
            barBg.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f, 1f);

            var fillGo = new GameObject("ProgressFill");
            fillGo.transform.SetParent(barBg.transform, false);
            var fRt = fillGo.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.30f, 0.88f, 0.96f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            var pulseGo = new GameObject("HarmonicPulse");
            pulseGo.transform.SetParent(panel.transform, false);
            var pRt = pulseGo.AddComponent<RectTransform>();
            pRt.sizeDelta = new Vector2(22f, 22f);
            pRt.anchoredPosition = new Vector2(-300f, 2f);
            var pulseImg = pulseGo.AddComponent<Image>();
            pulseImg.color = new Color(0.99f, 0.85f, 0.58f, 0.95f);

            var hintTxt = MakeText(panel.transform, "Hint", "Segure [E] ou Clique para sintonizar  •  [Esc] / [Q] Cancelar", font, 17, new Color(0.85f, 0.90f, 0.95f), FontStyle.Normal, new Vector2(0f, -38f), new Vector2(620f, 28f));

            return (cg, titleTxt, fillImg, pulseImg, hintTxt);
        }

        private static Text MakeText(Transform parent, string name, string content, Font font, int size, Color color, FontStyle style, Vector2 pos, Vector2 dims)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            return txt;
        }
    }
}
