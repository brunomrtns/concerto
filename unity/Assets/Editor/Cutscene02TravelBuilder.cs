using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
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
    /// Cutscene02TravelBuilder — Monta a Cutscene 2 ("A Viagem", 85s, 5 Beats) em Caldeiral.unity
    /// reusando 100% o sistema canonico da Cutscene 1 (Timeline + Cinemachine 3.1.4 + CutsceneUI_Canvas + JSON + Skip):
    ///   - Importa npc_esposa_lira_humanoid.fbx (TripoSG + PBR 4K + UniRig Humanoid) -> NPC_Lira_Passado
    ///   - Importa cs02_prop_maquina_completa.fbx (TripoSG + PBR 4K + emissivo ciano) -> CS02_Maquina_Completa
    ///   - Adiciona TimeMachineInteractable + SphereCollider trigger em CS01_Maquina_Coberta_Lona (ponte de gameplay!)
    ///   - Cria CS02_Travel_Timeline.playable (5 VCams Cinemachine 3.1.4 + 6 SignalEmitters)
    ///   - Cria 2 URP Volumes isolados (CS02_WarpExposureProfile + CS02_PastMemoryProfile) + 2 ParticleSystems economicos
    ///   - Reusa CutsceneUI_Canvas.prefab e cs01_musicbox_motif_TEMP.wav
    /// </summary>
    public static class Cutscene02TravelBuilder
    {
        private const string ScenePath       = "Assets/Game/Scenes/Caldeiral.unity";
        private const string CutscenesDir    = "Assets/Game/Cutscenes";
        private const string SignalsDir      = "Assets/Game/Cutscenes/Signals";
        private const string TimelinePath    = "Assets/Game/Cutscenes/CS02_Travel_Timeline.playable";
        private const string WarpVolProf     = "Assets/Game/Cutscenes/CS02_WarpExposureProfile.asset";
        private const string PastVolProf     = "Assets/Game/Cutscenes/CS02_PastMemoryProfile.asset";
        private const string UiPrefabPath    = "Assets/Game/Prefabs/UI/CutsceneUI_Canvas.prefab";
        private const string SubJsonPath     = "Assets/Game/Cutscenes/Data/cs02_viagem_subtitles.json";
        private const string LiraFbxPath     = "Assets/Game/Art/Generated/npcs/npc_esposa_lira/npc_esposa_lira_humanoid.fbx";
        private const string MachineFbxPath  = "Assets/Game/Art/Generated/cs02/cs02_prop_maquina_completa.fbx";
        private const string Fab3dMatDir     = "Assets/Game/Materials/Fab3D";
        private const string PrefabDir       = "Assets/Game/Prefabs/Fab3D";
        private const string CtrlPath        = "Assets/Game/Animations/HeroDezController.controller";
        private const string Cs01AudioDir    = "Assets/Game/Audio/Cutscenes/CS01";
        private const string Cs02AudioDir    = "Assets/Game/Audio/Cutscenes/CS02";

        [MenuItem("Studio/Concerto/Cutscenes/Build Cutscene 02 (A Viagem) in Caldeiral")]
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
                Debug.LogError($"[Cutscene02Builder] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureDir(CutscenesDir);
            EnsureDir(SignalsDir);
            EnsureDir(Fab3dMatDir);
            EnsureDir(PrefabDir);
            AssetDatabase.Refresh();

            // 1. Configura Humanoid Avatar + Material PBR 4K + Prefab da esposa Lira (TripoSG + UniRig)
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), LiraFbxPath)))
                throw new FileNotFoundException($"FBX da esposa Lira nao encontrado (TripoSG obrigatorio!): {LiraFbxPath}");
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), MachineFbxPath)))
                throw new FileNotFoundException($"FBX da maquina do tempo completa nao encontrado (TripoSG obrigatorio!): {MachineFbxPath}");

            var animCtrl   = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            var liraAvatar = ConfigureHumanoidFbx(LiraFbxPath);
            var liraMat    = EnsurePbrMaterial("npc_esposa_lira", withEmission: false);
            var liraPrefab = CreateNpcPrefab("npc_esposa_lira", LiraFbxPath, liraAvatar, liraMat, animCtrl);

            // 2. Configura Material PBR 4K (com emissivo ciano) da Maquina do Tempo Completa (TripoSG)
            ConfigurePropFbx(MachineFbxPath);
            var machineMat = EnsurePbrMaterial("cs02_prop_maquina_completa", withEmission: true);

            // 3. Abre Caldeiral.unity (reusa cena existente, sem duplicar!)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (mainCam == null) throw new Exception("Main Camera nao encontrada em Caldeiral.unity!");
            var cmBrain = mainCam.GetComponent<CinemachineBrain>();
            if (cmBrain == null) cmBrain = mainCam.gameObject.AddComponent<CinemachineBrain>();

            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            if (uiPrefab == null) throw new FileNotFoundException($"CutsceneUI_Canvas.prefab nao encontrado (CS01 obrigatorio!): {UiPrefabPath}");

            // Referencias existentes da cena (Oficina + CS01)
            var coveredMachine = GameObject.Find("CS01_Maquina_Coberta_Lona");
            var livingNpcs     = GameObject.Find("Caldeiral_Living_Alien_NPCs");
            var ceilingLights  = GameObject.Find("Caldeiral_CeilingLights");

            Vector3 machinePos = coveredMachine != null ? coveredMachine.transform.position : new Vector3(-25.8f, 0.0f, -15.4f);
            Quaternion machineRot = coveredMachine != null ? coveredMachine.transform.rotation : Quaternion.Euler(0f, -35f, 0f);

            // 4. Monta root limpo da Cutscene 2
            var oldCs2 = GameObject.Find("CS02_A_Viagem");
            if (oldCs2 != null) UnityEngine.Object.DestroyImmediate(oldCs2);
            var csRoot = new GameObject("CS02_A_Viagem");

            var director = csRoot.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            var receiver = csRoot.AddComponent<SignalReceiver>();

            var ctrlType = Type.GetType("Studio.Game.Cutscenes.Cutscene02TravelController, Assembly-CSharp");
            if (ctrlType == null) throw new Exception("Cutscene02TravelController nao encontrado em Assembly-CSharp!");
            var ctrlComp = csRoot.AddComponent(ctrlType);

            // Reusa prefab CutsceneUI_Canvas da Cutscene 1!
            var uiInst = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, csRoot.transform);
            uiInst.name = "CS02_CutsceneUI_Canvas";
            var uiOverlayType = Type.GetType("Studio.Game.Cutscenes.CutsceneUIOverlay, Assembly-CSharp");
            var uiOverlayComp = uiInst.GetComponent(uiOverlayType);

            // 5. Instancia Maquina do Tempo Completa (em pe na mesma posicao da lona!) e Esposa Lira (na bancada, livre de paredes!)
            var machineGo = InstantiateNormalizedProp(csRoot.transform, "CS02_Maquina_Completa", MachineFbxPath, machineMat, targetHeight: 2.35f, standUpright: true);
            machineGo.transform.position = new Vector3(machinePos.x, 0.02f, machinePos.z);
            machineGo.transform.rotation = machineRot;

            var liraGo = (GameObject)PrefabUtility.InstantiatePrefab(liraPrefab, csRoot.transform);
            liraGo.name = "NPC_Lira_Passado";
            NormalizeInstanceHeightAndFeet(liraGo, targetHeight: 1.24f);
            liraGo.transform.position = new Vector3(-30.05f, 0.02f, -17.55f);
            liraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.85f, 0f, -0.35f)); // Livre da divisoria, olhando bancada & Dez

            // 6. Rig de Luz quente do Passado + 2 URP Volumes (Warp Overexposure + Past Memory Grade)
            var pastLightRig = BuildPastMemoryLightRig(csRoot.transform);
            var warpVolume   = BuildWarpExposureVolume(csRoot.transform);
            var pastVolume   = BuildPastMemoryVolume(csRoot.transform);

            // 7. 2 ParticleSystems economicos com textura radial suave (Dobra Temporal no Beat 2 + Poeira do Teto no Beat 4)
            var softTex = EnsureSoftParticleTexture();
            var warpParticles = BuildWarpParticles(csRoot.transform, machinePos + Vector3.up * 1.15f, softTex);
            var dustParticles = BuildCeilingDustParticles(csRoot.transform, new Vector3(-29.2f, 3.35f, -17.6f), softTex);

            // 8. 5 CinemachineCameras (Cinemachine 3.1.4!) para os 5 Beats — enquadramento 100% desobstruido!
            var camsRoot = new GameObject("CS02_VirtualCameras");
            camsRoot.transform.SetParent(csRoot.transform, false);

            var vcamB1 = CreateVCam(camsRoot.transform, "VCam_CS02_B1_MachineActivate",
                machinePos + new Vector3(-1.35f, 1.55f, -3.35f), machinePos + Vector3.up * 1.15f, fov: 46f, priority: 25);
            var vcamB2 = CreateVCam(camsRoot.transform, "VCam_CS02_B2_TimeWarp",
                machinePos + new Vector3(-1.05f, 1.45f, -2.55f), machinePos + Vector3.up * 1.15f, fov: 56f, priority: 25);
            var vcamB3 = CreateVCam(camsRoot.transform, "VCam_CS02_B3_PastReunion",
                new Vector3(-31.8f, 1.45f, -19.6f), new Vector3(-28.7f, 1.00f, -17.3f), fov: 45f, priority: 25);
            var vcamB4 = CreateVCam(camsRoot.transform, "VCam_CS02_B4_FirstGlitch",
                new Vector3(-30.6f, 1.38f, -19.1f), new Vector3(-29.6f, 1.05f, -17.5f), fov: 36f, priority: 25);
            var vcamB5 = CreateVCam(camsRoot.transform, "VCam_CS02_B5_BlackEndCard",
                new Vector3(0f, -50.0f, 0f), new Vector3(0f, -50.0f, 2.0f), fov: 40f, priority: 25);

            // Quad preto absoluto na frente da VCam B5 para corte seco 100% preto em 78.5s -> Cartao "FIM DO ATO 1 / CONCERTO"
            var blackQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blackQuad.name = "CS02_CutToBlack_Backdrop";
            UnityEngine.Object.DestroyImmediate(blackQuad.GetComponent<Collider>());
            blackQuad.transform.SetParent(vcamB5.transform, false);
            blackQuad.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            blackQuad.transform.localScale = new Vector3(6f, 4f, 1f);
            var blackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            blackMat.color = Color.black;
            blackQuad.GetComponent<MeshRenderer>().sharedMaterial = blackMat;

            // 9. AudioSources TEMP (reusando cs01_musicbox_motif_TEMP.wav da Cutscene 1!)
            var audioRoot = new GameObject("CS02_Audio").transform;
            audioRoot.SetParent(csRoot.transform, false);
            var mainAudio     = CreateAudioSource(audioRoot, "Audio_CS02_Main_TEMP", null, loop: false, vol: 0.75f);
            var musicBoxAudio = CreateAudioSource(audioRoot, "Audio_CS02_MusicBox_Reused_TEMP", $"{Cs01AudioDir}/cs01_musicbox_motif_TEMP.wav", loop: false, vol: 0.68f);

            var chargeClip  = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs02AudioDir}/cs02_machine_charge_TEMP.wav");
            var warpClip    = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs02AudioDir}/cs02_timewarp_stretch_TEMP.wav");
            var glitchClip  = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Cs02AudioDir}/cs02_glitch_distort_TEMP.wav");

            // 10. Constroi o TimelineAsset (CS02_Travel_Timeline.playable)
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "CS02 Cinemachine Shot List");
            director.SetGenericBinding(cmTrack, cmBrain);

            AddCmShot(cmTrack, director, "Beat1_MachineActivate", vcamB1,  0.0, 15.2);
            AddCmShot(cmTrack, director, "Beat2_TimeWarp",        vcamB2, 15.0, 15.2);
            AddCmShot(cmTrack, director, "Beat3_PastReunion",     vcamB3, 30.0, 35.2);
            AddCmShot(cmTrack, director, "Beat4_FirstGlitch",     vcamB4, 65.0, 13.5);
            AddCmShot(cmTrack, director, "Beat5_BlackEndCard",    vcamB5, 78.5,  6.5);

            var sigTrack = timeline.CreateTrack<SignalTrack>(null, "CS02_Choreography_And_Audio");
            director.SetGenericBinding(sigTrack, receiver);

            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_B1_Charge",    1.5,  "OnSignalBeat1ChargeAndUncover");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_B2_Warp",     15.0,  "OnSignalBeat2TimeWarp");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_B3_Past",     30.0,  "OnSignalBeat3EnterPastMemory");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_B4_Glitch",   65.0,  "OnSignalBeat4FirstGlitch");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_B5_BlackCard",78.5,  "OnSignalBeat5CutToBlackCard");
            BindSignal(sigTrack, receiver, ctrlComp, "SIG_CS02_End",         84.8,  "OnSignalCutscene02End");

            director.playableAsset = timeline;
            EditorUtility.SetDirty(timeline);

            // 11. Liga referencias serializadas no Cutscene02TravelController
            var so = new SerializedObject(ctrlComp);
            so.FindProperty("cutsceneId").stringValue = "cs02_a_viagem";
            so.FindProperty("subtitleJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SubJsonPath);
            so.FindProperty("uiOverlay").objectReferenceValue = uiOverlayComp;
            so.FindProperty("camerasRoot").objectReferenceValue = camsRoot;
            so.FindProperty("pastMemoryLightRig").objectReferenceValue = pastLightRig;
            so.FindProperty("warpExposureVolume").objectReferenceValue = warpVolume;
            so.FindProperty("pastMemoryVolume").objectReferenceValue = pastVolume;
            so.FindProperty("coveredMachineProp").objectReferenceValue = coveredMachine;
            so.FindProperty("uncoveredMachineProp").objectReferenceValue = machineGo;
            so.FindProperty("wifeLiraActor").objectReferenceValue = liraGo;
            so.FindProperty("livingNpcsRoot").objectReferenceValue = livingNpcs;
            so.FindProperty("ceilingLightsRoot").objectReferenceValue = ceilingLights;
            so.FindProperty("warpParticles").objectReferenceValue = warpParticles;
            so.FindProperty("ceilingDustParticles").objectReferenceValue = dustParticles;
            so.FindProperty("mainAudioSource").objectReferenceValue = mainAudio;
            so.FindProperty("musicBoxSource").objectReferenceValue = musicBoxAudio;
            so.FindProperty("machineChargeClip").objectReferenceValue = chargeClip;
            so.FindProperty("timewarpStretchClip").objectReferenceValue = warpClip;
            so.FindProperty("glitchDistortClip").objectReferenceValue = glitchClip;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 12. Ponte de Gameplay: anexa TimeMachineInteractable + SphereCollider trigger em CS01_Maquina_Coberta_Lona
            if (coveredMachine != null)
            {
                var interactType = Type.GetType("Studio.Game.Cutscenes.TimeMachineInteractable, Assembly-CSharp");
                if (interactType == null) throw new Exception("TimeMachineInteractable nao encontrado!");
                var interactComp = coveredMachine.GetComponent(interactType);
                if (interactComp == null) interactComp = coveredMachine.AddComponent(interactType);

                var iso = new SerializedObject(interactComp);
                iso.FindProperty("cutscene02").objectReferenceValue = ctrlComp;
                iso.ApplyModifiedPropertiesWithoutUndo();

                var sc = coveredMachine.GetComponent<SphereCollider>();
                if (sc == null) sc = coveredMachine.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 2.0f;
                sc.center = Vector3.up * 1.0f;
            }

            // 13. Garante estado inicial limpo no EditMode (ZERO vazamento do passado no gameplay normal!)
            camsRoot.SetActive(false);
            pastLightRig.SetActive(false);
            warpVolume.gameObject.SetActive(false);
            pastVolume.gameObject.SetActive(false);
            machineGo.SetActive(false);
            liraGo.SetActive(false);
            if (coveredMachine != null) coveredMachine.SetActive(true);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Cutscene02Builder] Cutscene 2 ('A Viagem', 85s, 5 Beats, Lira + Maquina Completa via TripoSG, Cinemachine 3.1.4 + Timeline + Signals + URP Memory Volume) montada com sucesso em Caldeiral.unity!");
        }

        private static Avatar ConfigureHumanoidFbx(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) throw new Exception($"ModelImporter falhou em {fbxPath}");

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();

            var hd = importer.humanDescription;
            hd.human = hd.human.Where(h =>
                h.humanName != "UpperChest" &&
                h.humanName != "LeftEye" &&
                h.humanName != "RightEye"
            ).ToArray();
            importer.humanDescription = hd;
            importer.SaveAndReimport();

            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            return assets.OfType<Avatar>().FirstOrDefault();
        }

        private static void ConfigurePropFbx(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        private static Material EnsurePbrMaterial(string slug, bool withEmission)
        {
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_albedo.png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_normal.png");
            var met = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_metallicsmooth.png")
                   ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_metallic.png");
            var occ = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_ao.png");

            if (nrm != null)
            {
                var ni = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(nrm)) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
            }

            if (alb != null) mat.SetTexture("_BaseMap", alb);
            if (nrm != null) { mat.SetTexture("_BumpMap", nrm); mat.SetFloat("_BumpScale", 1.0f); mat.EnableKeyword("_NORMALMAP"); }
            if (met != null) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (occ != null) mat.SetTexture("_OcclusionMap", occ);
            mat.SetFloat("_Smoothness", withEmission ? 0.58f : 0.35f);

            if (withEmission)
            {
                var emi = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_emission.png");
                if (emi != null)
                {
                    mat.SetTexture("_EmissionMap", emi);
                    mat.SetColor("_EmissionColor", new Color(0.25f, 1.35f, 1.85f) * 2.2f);
                    mat.EnableKeyword("_EMISSION");
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateNpcPrefab(string slug, string fbxPath, Avatar avatar, Material mat, AnimatorController ctrl)
        {
            string prefabPath = $"{PrefabDir}/{slug}_humanoid.prefab";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            root.name = $"{slug}_humanoid";

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var procType = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            if (procType != null && root.GetComponent(procType) == null)
                root.AddComponent(procType);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject InstantiateNormalizedProp(Transform parent, string name, string fbxPath, Material mat, float targetHeight, bool standUpright = false)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, container.transform);
            inst.name = $"{name}_Mesh";
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            if (standUpright)
            {
                // Se o FBX veio deitado no eixo Y (Z-up do Blender), ergue +90 graus em X!
                var rs = inst.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    Bounds b0 = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b0.Encapsulate(rs[i].bounds);
                    if (b0.size.y < Mathf.Max(b0.size.x, b0.size.z) * 0.72f)
                        inst.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            NormalizeInstanceHeightAndFeet(inst, targetHeight);

            // Assenta a base exatamente em y=0 no container
            var rs2 = inst.GetComponentsInChildren<Renderer>(true);
            if (rs2.Length > 0)
            {
                Bounds b = rs2[0].bounds;
                for (int i = 1; i < rs2.Length; i++) b.Encapsulate(rs2[i].bounds);
                inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            }
            return container;
        }

        private static void NormalizeInstanceHeightAndFeet(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float h = Mathf.Max(0.01f, b.size.y);
            float scale = targetHeight / h;
            go.transform.localScale *= scale;
        }

        private static GameObject BuildPastMemoryLightRig(Transform parent)
        {
            var rig = new GameObject("CS02_PastMemory_LightRig");
            rig.transform.SetParent(parent, false);

            var key = new GameObject("Past_WarmSunKey").AddComponent<Light>();
            key.transform.SetParent(rig.transform, false);
            key.type = LightType.Spot;
            key.transform.position = new Vector3(-27.2f, 3.8f, -19.0f);
            key.transform.LookAt(new Vector3(-29.0f, 0.9f, -17.0f));
            key.color = new Color(1.00f, 0.86f, 0.62f);
            key.intensity = 18.0f;
            key.range = 16f;
            key.spotAngle = 72f;

            var fill = new GameObject("Past_WarmAmbientFill").AddComponent<Light>();
            fill.transform.SetParent(rig.transform, false);
            fill.type = LightType.Point;
            fill.transform.position = new Vector3(-29.2f, 2.2f, -17.5f);
            fill.color = new Color(1.00f, 0.90f, 0.72f);
            fill.intensity = 6.5f;
            fill.range = 10f;

            return rig;
        }

        private static Volume BuildWarpExposureVolume(Transform parent)
        {
            var go = new GameObject("CS02_WarpExposure_Volume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 60f;
            vol.weight = 0f;

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(WarpVolProf) != null)
                AssetDatabase.DeleteAsset(WarpVolProf);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, WarpVolProf);

            var color = prof.Add<ColorAdjustments>(true);
            color.postExposure.Override(6.2f);
            color.contrast.Override(-15f);
            color.colorFilter.Override(new Color(0.86f, 0.96f, 1.0f));

            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(0.45f);
            bloom.intensity.Override(4.2f);
            bloom.scatter.Override(0.82f);

            var chrom = prof.Add<ChromaticAberration>(true);
            chrom.intensity.Override(0.65f);

            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            return vol;
        }

        private static Volume BuildPastMemoryVolume(Transform parent)
        {
            var go = new GameObject("CS02_PastMemory_Volume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 55f;
            vol.weight = 0f;

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PastVolProf) != null)
                AssetDatabase.DeleteAsset(PastVolProf);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, PastVolProf);

            var color = prof.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.saturation.Override(-16f);
            color.colorFilter.Override(new Color(1.00f, 0.91f, 0.76f)); // Memoria quente/suave!

            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.95f);
            bloom.scatter.Override(0.70f);

            var vig = prof.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.48f);

            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;
            return vol;
        }

        private static Texture2D EnsureSoftParticleTexture()
        {
            string texPath = $"{CutscenesDir}/CS02_SoftParticle.png";
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), texPath);
            if (!File.Exists(fullPath))
            {
                int sz = 64;
                var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                float c = (sz - 1) * 0.5f;
                for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
            }
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && !ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        private static ParticleSystem BuildWarpParticles(Transform parent, Vector3 pos, Texture2D softTex)
        {
            var go = new GameObject("CS02_WarpParticles");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 1.6f;
            main.startSpeed = 4.2f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.55f, 0.94f, 1.0f, 0.85f);
            main.maxParticles = 260;
            var emission = ps.emission;
            emission.rateOverTime = 75f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.1f;
            ConfigureSoftParticleMaterial(ps, softTex, additive: true);
            return ps;
        }

        private static ParticleSystem BuildCeilingDustParticles(Transform parent, Vector3 pos, Texture2D softTex)
        {
            var go = new GameObject("CS02_CeilingDustParticles");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 3.2f;
            main.startSpeed = 0.35f;
            main.gravityModifier = 0.32f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.86f, 0.80f, 0.70f, 0.55f);
            main.maxParticles = 160;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.2f, 0.2f, 2.8f);
            ConfigureSoftParticleMaterial(ps, softTex, additive: false);
            return ps;
        }

        private static void ConfigureSoftParticleMaterial(ParticleSystem ps, Texture2D softTex, bool additive)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return;
            string matPath = $"{CutscenesDir}/Mat_CS02_Particle_{(additive ? "Glow" : "Dust")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", softTex);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", additive ? 2f : 0f); // Additive for warp, Alpha for dust
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(mat);
            r.sharedMaterial = mat;
        }

        private static CinemachineCamera CreateVCam(Transform parent, string name, Vector3 pos, Vector3 lookAt, float fov, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(lookAt);

            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = priority;
            vcam.Lens = new LensSettings
            {
                FieldOfView = fov,
                NearClipPlane = 0.1f,
                FarClipPlane = 200f,
                Dutch = 0f
            };
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

        private static AudioSource CreateAudioSource(Transform parent, string name, string clipPath, bool loop, float vol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.volume = vol;
            src.spatialBlend = 0f;
            if (!string.IsNullOrEmpty(clipPath))
                src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            return src;
        }

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
