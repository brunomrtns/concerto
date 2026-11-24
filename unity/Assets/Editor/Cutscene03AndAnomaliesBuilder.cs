using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace Studio.Editor
{
    /// <summary>
    /// Cutscene03AndAnomaliesBuilder — Monta a Cutscene 3 ("O Retorno", 32s, 4 Beats) +
    /// as 5 Anomalias Ambientais (config-driven via act2_anomalies_config.json) em Caldeiral.unity:
    ///   - Importa cs03_prop_bugiganga_quebrada.fbx (TripoSG + PBR 4K) na bancada
    ///   - Cria CS03_Return_Timeline.playable (4 VCams Cinemachine 3.1.4 Priority 30 + 5 SignalEmitters)
    ///   - Cria Act2_Workshop_Anomalies com 5 componentes Anomaly (inativos/Normal ate CS03 terminar!)
    ///   - Reusa CutsceneUI_Canvas.prefab e Mat_CS02_Particle_Glow.mat
    /// </summary>
    public static class Cutscene03AndAnomaliesBuilder
    {
        private const string ScenePath       = "Assets/Game/Scenes/Caldeiral.unity";
        private const string CutscenesDir    = "Assets/Game/Cutscenes";
        private const string SignalsDir      = "Assets/Game/Cutscenes/Signals";
        private const string TimelinePath    = "Assets/Game/Cutscenes/CS03_Return_Timeline.playable";
        private const string FlashVolProf    = "Assets/Game/Cutscenes/CS03_ReturnFlashProfile.asset";
        private const string UiPrefabPath    = "Assets/Game/Prefabs/UI/CutsceneUI_Canvas.prefab";
        private const string SubJsonPath     = "Assets/Game/Cutscenes/Data/cs03_retorno_subtitles.json";
        private const string AnomJsonPath    = "Assets/Game/Anomalies/Data/act2_anomalies_config.json";
        private const string TrinketFbxPath  = "Assets/Game/Art/Generated/cs03/cs03_prop_bugiganga_quebrada.fbx";
        private const string Fab3dMatDir     = "Assets/Game/Materials/Fab3D";
        private const string Cs03AudioDir    = "Assets/Game/Audio/Cutscenes/CS03";
        private const string GlowMatPath     = "Assets/Game/Cutscenes/Mat_CS02_Particle_Glow.mat";

        [MenuItem("Studio/Concerto/Cutscenes/Build Cutscene 03 (O Retorno) & 5 Act 2 Anomalies")]
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
                Debug.LogError($"[Cutscene03Builder] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureDir(CutscenesDir);
            EnsureDir(SignalsDir);
            AssetDatabase.Refresh();

            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), TrinketFbxPath)))
                throw new FileNotFoundException($"FBX da bugiganga quebrada nao encontrado (TripoSG obrigatorio!): {TrinketFbxPath}");

            ConfigurePropFbx(TrinketFbxPath);
            var trinketMat = EnsureTrinketMaterial();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (mainCam == null) throw new Exception("Main Camera nao encontrada em Caldeiral.unity!");
            var cmBrain = mainCam.GetComponent<CinemachineBrain>();
            if (cmBrain == null) cmBrain = mainCam.gameObject.AddComponent<CinemachineBrain>();

            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            if (uiPrefab == null) throw new FileNotFoundException($"CutsceneUI_Canvas.prefab nao encontrado: {UiPrefabPath}");

            var ceilingLights = GameObject.Find("Caldeiral_CeilingLights");
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            var wrongSoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs03AudioDir}/cs03_anomaly_wrong_sound_TEMP.wav");
            var humClip        = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs03AudioDir}/cs03_machine_hum_loop_TEMP.wav");
            var flashClip      = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs03AudioDir}/cs03_flash_return_TEMP.wav");

            // 1. Monta as 5 Anomalias Ambientais a partir de act2_anomalies_config.json (iniciam em AnomalyState.Normal!)
            var oldAnom = GameObject.Find("Act2_Workshop_Anomalies");
            if (oldAnom != null) UnityEngine.Object.DestroyImmediate(oldAnom);
            var anomRoot = new GameObject("Act2_Workshop_Anomalies");

            var mgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var anomType = Type.GetType("Studio.Game.Anomalies.Anomaly, Assembly-CSharp");
            var cfgType = Type.GetType("Studio.Game.Anomalies.Act2AnomaliesConfigData, Assembly-CSharp");
            if (mgrType == null || anomType == null || cfgType == null)
                throw new Exception("Tipos de Studio.Game.Anomalies nao encontrados em Assembly-CSharp!");

            var anomMgr = anomRoot.AddComponent(mgrType);
            var anomJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AnomJsonPath);
            var parsedCfg = cfgType.GetMethod("FromJson").Invoke(null, new object[] { anomJsonAsset });
            var entriesList = cfgType.GetField("anomalies").GetValue(parsedCfg) as System.Collections.IList;

            var builtAnomalies = new List<Component>();
            foreach (var entry in entriesList)
            {
                var et = entry.GetType();
                string id   = (string)et.GetField("id").GetValue(entry);
                string dName = (string)et.GetField("displayName").GetValue(entry);
                string kindStr = (string)et.GetField("kind").GetValue(entry);
                var posObj = et.GetField("position").GetValue(entry);
                Vector3 pos = (Vector3)posObj.GetType().GetMethod("ToVector3").Invoke(posObj, null);

                var aComp = CreateAnomalyInstance(anomRoot.transform, anomType, id, dName, kindStr, pos, glowMat, wrongSoundClip);
                builtAnomalies.Add(aComp);
            }

            // Zumbido persistente da maquina pos-CS03
            var persHum = CreateAudioSource(anomRoot.transform, "Audio_Act2_PersistentMachineHum_TEMP", humClip, loop: true, vol: 0.34f);

            var mgrSo = new SerializedObject(anomMgr);
            mgrSo.FindProperty("anomaliesConfigJson").objectReferenceValue = anomJsonAsset;
            mgrSo.FindProperty("persistentMachineHum").objectReferenceValue = persHum;
            var listProp = mgrSo.FindProperty("anomalies");
            listProp.ClearArray();
            for (int i = 0; i < builtAnomalies.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = builtAnomalies[i];
            }
            mgrSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. Monta Cutscene 3 ("O Retorno", 32s, 4 Beats)
            var oldCs3 = GameObject.Find("CS03_O_Retorno");
            if (oldCs3 != null) UnityEngine.Object.DestroyImmediate(oldCs3);
            var csRoot = new GameObject("CS03_O_Retorno");

            var director = csRoot.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            var receiver = csRoot.AddComponent<SignalReceiver>();

            var ctrlType = Type.GetType("Studio.Game.Cutscenes.Cutscene03ReturnController, Assembly-CSharp");
            if (ctrlType == null) throw new Exception("Cutscene03ReturnController nao encontrado!");
            var ctrlComp = csRoot.AddComponent(ctrlType);

            var uiInst = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, csRoot.transform);
            uiInst.name = "CS03_CutsceneUI_Canvas";
            var uiOverlayComp = uiInst.GetComponent(Type.GetType("Studio.Game.Cutscenes.CutsceneUIOverlay, Assembly-CSharp"));

            // Prop de continuidade: Bugiganga Quebrada (TripoSG + PBR 4K) na bancada ao lado da foto!
            var trinketGo = InstantiateNormalizedProp(csRoot.transform, "CS03_Bugiganga_Quebrada", TrinketFbxPath, trinketMat, targetHeight: 0.24f);
            trinketGo.transform.position = new Vector3(-28.88f, 0.96f, -17.36f);
            trinketGo.transform.rotation = Quaternion.Euler(0f, 32f, 12f); // Levemente tombada/quebrada!
            trinketGo.SetActive(false); // So aparece a partir da Cutscene 3!

            // URP Volume de Flash de retorno (Beat 1)
            var flashVol = BuildReturnFlashVolume(csRoot.transform);

            // 4 CinemachineCameras (Priority 30 > CS02 25 > CS01 20 > Gameplay 10!)
            var camsRoot = new GameObject("CS03_VirtualCameras");
            camsRoot.transform.SetParent(csRoot.transform, false);

            var vcamB1 = CreateVCam(camsRoot.transform, "VCam_CS03_B1_ReturnFlash",
                new Vector3(-31.2f, 1.65f, -19.8f), new Vector3(-28.4f, 1.05f, -17.3f), fov: 48f, priority: 30);
            var vcamB2 = CreateVCam(camsRoot.transform, "VCam_CS03_B2_PhotoPeace",
                new Vector3(-29.7f, 1.42f, -18.5f), new Vector3(-28.7f, 1.04f, -17.4f), fov: 36f, priority: 30);
            var vcamB3 = CreateVCam(camsRoot.transform, "VCam_CS03_B3_BrokenTrinket",
                new Vector3(-28.42f, 1.32f, -17.92f), new Vector3(-28.88f, 1.02f, -17.36f), fov: 30f, priority: 30);
            var vcamB4 = CreateVCam(camsRoot.transform, "VCam_CS03_B4_CutToBlack",
                new Vector3(0f, -60.0f, 0f), new Vector3(0f, -60.0f, 2.0f), fov: 40f, priority: 30);

            var blackQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blackQuad.name = "CS03_CutToBlack_Backdrop";
            UnityEngine.Object.DestroyImmediate(blackQuad.GetComponent<Collider>());
            blackQuad.transform.SetParent(vcamB4.transform, false);
            blackQuad.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            blackQuad.transform.localScale = new Vector3(6f, 4f, 1f);
            var blackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            blackMat.color = Color.black;
            blackQuad.GetComponent<MeshRenderer>().sharedMaterial = blackMat;

            // AudioSources TEMP CS03
            var audioRoot = new GameObject("CS03_Audio").transform;
            audioRoot.SetParent(csRoot.transform, false);
            var sfxSrc = CreateAudioSource(audioRoot, "Audio_CS03_SFX_TEMP", null, loop: false, vol: 0.72f);
            var humSrc = CreateAudioSource(audioRoot, "Audio_CS03_Hum_TEMP", humClip, loop: true, vol: 0.76f);

            // Timeline CS03 (32.0s)
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "CS03 Cinemachine Shot List");
            director.SetGenericBinding(cmTrack, cmBrain);

            AddCmShot(cmTrack, director, "Beat1_ReturnFlash",    vcamB1,  0.0,  8.2);
            AddCmShot(cmTrack, director, "Beat2_PhotoPeace",     vcamB2,  8.0, 10.2);
            AddCmShot(cmTrack, director, "Beat3_BrokenTrinket",  vcamB3, 18.0,  8.0);
            AddCmShot(cmTrack, director, "Beat3b_CutToBlackHum", vcamB4, 26.0,  6.0);

            var sigTrack = timeline.CreateTrack<SignalTrack>(null, "CS03_Choreography_And_Audio");
            director.SetGenericBinding(sigTrack, receiver);

            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS03_B1_Flash",       0.1,  "OnSignalCS03Beat1Flash");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS03_B2_PhotoPeace",  8.0,  "OnSignalCS03Beat2PhotoPeace");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS03_B3_BrokenHum",  18.2,  "OnSignalCS03Beat3BrokenAndHum");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS03_B3_Black",      26.0,  "OnSignalCS03Beat3CutToBlack");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS03_End",           31.8,  "OnSignalCS03End");

            director.playableAsset = timeline;
            EditorUtility.SetDirty(timeline);

            // Liga referencias no Cutscene03ReturnController
            var so = new SerializedObject(ctrlComp);
            so.FindProperty("cutsceneId").stringValue = "cs03_o_retorno";
            so.FindProperty("subtitleJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SubJsonPath);
            so.FindProperty("uiOverlay").objectReferenceValue = uiOverlayComp;
            so.FindProperty("camerasRoot").objectReferenceValue = camsRoot;
            so.FindProperty("returnFlashVolume").objectReferenceValue = flashVol;
            so.FindProperty("brokenTrinketProp").objectReferenceValue = trinketGo;
            so.FindProperty("ceilingLightsRoot").objectReferenceValue = ceilingLights;
            so.FindProperty("anomalyManager").objectReferenceValue = anomMgr;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSrc;
            so.FindProperty("humSource").objectReferenceValue = humSrc;
            so.FindProperty("flashReturnClip").objectReferenceValue = flashClip;
            so.FindProperty("machineHumClip").objectReferenceValue = humClip;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Estado inicial limpo (antes da CS03: tudo desligado!)
            camsRoot.SetActive(false);
            flashVol.gameObject.SetActive(false);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Cutscene03Builder] Cutscene 3 ('O Retorno', 32s, 4 Beats) + 5 Anomalias Ambientais (config-driven) montadas com sucesso em Caldeiral.unity!");
        }

        private static Component CreateAnomalyInstance(
            Transform parent, Type anomType, string id, string displayName, string kindStr,
            Vector3 pos, Material glowMat, AudioClip wrongSoundClip)
        {
            var go = new GameObject($"Anomaly_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var comp = go.AddComponent(anomType);
            var so = new SerializedObject(comp);
            so.FindProperty("anomalyId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;

            var kindEnumType = Type.GetType("Studio.Game.Anomalies.AnomalyKind, Assembly-CSharp");
            int kindInt = (int)Enum.Parse(kindEnumType, kindStr);
            so.FindProperty("kind").enumValueIndex = kindInt;

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (kindStr == "LightFlicker")
            {
                var lGo = new GameObject("FlickerPointLight");
                lGo.transform.SetParent(go.transform, false);
                var l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.75f, 0.96f, 1.0f);
                l.intensity = 9.0f;
                l.range = 8.5f;
                l.enabled = false;
                so.FindProperty("flickerLight").objectReferenceValue = l;

                // Particulas de poeira invertidas (subindo ao contrario!)
                var ps = BuildAnomalyParticles(go.transform, glowMat, new Color(0.70f, 0.92f, 1.0f, 0.7f), upwardGravity: -0.25f);
                so.FindProperty("anomalyParticles").objectReferenceValue = ps;
            }
            else if (kindStr == "ReverseSpin")
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "ReverseSpinGear";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
                var m = new Material(litShader);
                m.color = new Color(0.84f, 0.62f, 0.25f);
                m.SetFloat("_Metallic", 0.78f);
                m.SetFloat("_Smoothness", 0.65f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = m;
                so.FindProperty("targetVisual").objectReferenceValue = visual.transform;

                var ps = BuildAnomalyParticles(go.transform, glowMat, new Color(0.35f, 0.95f, 1.0f, 0.75f), upwardGravity: -0.12f);
                so.FindProperty("anomalyParticles").objectReferenceValue = ps;
            }
            else if (kindStr == "FloatingTool")
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "FloatingWrench";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = new Vector3(0.42f, 0.08f, 0.12f);
                var m = new Material(litShader);
                m.color = new Color(0.55f, 0.85f, 0.95f);
                m.SetFloat("_Metallic", 0.85f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = m;
                so.FindProperty("targetVisual").objectReferenceValue = visual.transform;

                var ps = BuildAnomalyParticles(go.transform, glowMat, new Color(0.35f, 0.95f, 1.0f, 0.85f), upwardGravity: -0.18f);
                so.FindProperty("anomalyParticles").objectReferenceValue = ps;
            }
            else if (kindStr == "WrongCornerSound")
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "SoundDistortionNode";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = Vector3.one * 0.18f;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? litShader);
                m.color = new Color(0.30f, 0.85f, 1.0f, 0.85f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = m;
                so.FindProperty("targetVisual").objectReferenceValue = visual.transform;

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 1.5f;
                src.maxDistance = 12.0f;
                src.volume = 0.82f;
                src.clip = wrongSoundClip;
                so.FindProperty("spatialAudio").objectReferenceValue = src;

                var ps = BuildAnomalyParticles(go.transform, glowMat, new Color(0.40f, 0.88f, 1.0f, 0.8f), upwardGravity: -0.15f);
                so.FindProperty("anomalyParticles").objectReferenceValue = ps;
            }
            else if (kindStr == "MismatchedShadow")
            {
                // Objeto pequeno acima + sombra escura no chao que gira em angulo impossivel!
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "SmallCrateRef";
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.transform.SetParent(go.transform, false);
                obj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
                obj.transform.localScale = new Vector3(0.44f, 0.44f, 0.44f);
                var mObj = new Material(litShader);
                mObj.color = new Color(0.62f, 0.44f, 0.26f);
                obj.GetComponent<MeshRenderer>().sharedMaterial = mObj;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "MismatchedShadowQuad";
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localPosition = new Vector3(0.35f, 0.02f, 0.22f);
                quad.transform.localRotation = Quaternion.Euler(90f, 35f, 0f);
                quad.transform.localScale = new Vector3(1.45f, 0.72f, 1f);
                var mSh = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? litShader);
                mSh.color = new Color(0.02f, 0.03f, 0.06f, 0.92f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = mSh;
                quad.SetActive(false);
                so.FindProperty("shadowQuad").objectReferenceValue = quad;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return comp;
        }

        private static ParticleSystem BuildAnomalyParticles(Transform parent, Material glowMat, Color color, float upwardGravity)
        {
            var go = new GameObject("AnomalyParticles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 2.0f;
            main.startSpeed = 0.25f;
            main.gravityModifier = upwardGravity; // Negativo = poeira subindo ao contrario!
            main.startSize = 0.09f;
            main.startColor = color;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 14f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.42f;
            if (glowMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
            return ps;
        }

        private static void ConfigurePropFbx(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }

        private static Material EnsureTrinketMaterial()
        {
            string slug = "cs03_prop_bugiganga_quebrada";
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_albedo.png");
            var met = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_metallicsmooth.png");
            if (alb != null) mat.SetTexture("_BaseMap", alb);
            if (met != null) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            mat.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject InstantiateNormalizedProp(Transform parent, string name, string fbxPath, Material mat, float targetHeight)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, container.transform);
            inst.name = $"{name}_Mesh";
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            var rs = inst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                float scale = targetHeight / Mathf.Max(0.01f, b.size.y);
                inst.transform.localScale *= scale;
                Bounds b2 = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b2.Encapsulate(rs[i].bounds);
                inst.transform.position += new Vector3(-b2.center.x, -b2.min.y, -b2.center.z);
            }
            return container;
        }

        private static Volume BuildReturnFlashVolume(Transform parent)
        {
            var go = new GameObject("CS03_ReturnFlash_Volume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 65f;
            vol.weight = 0f;

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(FlashVolProf) != null)
                AssetDatabase.DeleteAsset(FlashVolProf);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, FlashVolProf);

            var color = prof.Add<ColorAdjustments>(true);
            color.postExposure.Override(6.5f);
            var bloom = prof.Add<Bloom>(true);
            bloom.intensity.Override(3.8f);
            bloom.threshold.Override(0.5f);

            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            return vol;
        }

        private static CinemachineCamera CreateVCam(Transform parent, string name, Vector3 pos, Vector3 lookAt, float fov, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = priority;
            vcam.Lens = new LensSettings { FieldOfView = fov, NearClipPlane = 0.08f, FarClipPlane = 200f, Dutch = 0f };
            return vcam;
        }

        private static void AddCmShot(CinemachineTrack track, PlayableDirector dir, string displayName, CinemachineCamera vcam, double start, double duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.displayName = displayName;
            clip.start = start;
            clip.duration = duration;
            var shot = clip.asset as CinemachineShot;
            if (shot != null)
            {
                shot.VirtualCamera.exposedName = GUID.Generate().ToString();
                dir.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
            }
        }

        private static void BindSignal(SignalTrack track, SignalReceiver receiver, Component target, string signalName, double time, string methodName)
        {
            string path = $"{SignalsDir}/{signalName}.signal";
            var sig = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
            if (sig == null)
            {
                sig = ScriptableObject.CreateInstance<SignalAsset>();
                AssetDatabase.CreateAsset(sig, path);
            }
            var emitter = track.CreateMarker<SignalEmitter>(time);
            emitter.asset = sig;
            emitter.retroactive = false;
            emitter.emitOnce = true;

            var evt = new UnityEvent();
            var mi = target.GetType().GetMethod(methodName);
            if (mi != null)
            {
                var del = Delegate.CreateDelegate(typeof(UnityAction), target, mi) as UnityAction;
                if (del != null) UnityEventTools.AddPersistentListener(evt, del);
            }
            receiver.AddReaction(sig, evt);
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, AudioClip clip, bool loop, float vol)
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

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
