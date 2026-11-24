using System;
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
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// Cutscene01ConcertoBuilder — Monta a Cutscene 1 ("Concerto", 72s, 5 Beats) DENTRO da cena real Caldeiral.unity:
    ///   - Props TripoSG + PBR 4K: Caixinha de Musica (bancada) + Maquina do Tempo coberta com lona (centro-fundo)
    ///   - Props 2D narrativos: Porta-retrato de Dez com a esposa + Quadro de Checklist ("PROJETO ECHO - CONFIDENCIAL")
    ///   - Prefab uGUI reutilizavel (CutsceneUI_Canvas.prefab): Letterbox, Legendas via JSON, Title Card, Skip Hold, Objetivo
    ///   - Timeline (CS01_Opening_Concerto.playable): CinemachineTrack (Cinemachine 3.1.4!), SignalTrack (7 SignalEmitters),
    ///     AnimationTracks (dolly suave das cameras) e ActivationTracks (Rig de Luz + URP Volume temporario)
    /// </summary>
    public static class Cutscene01ConcertoBuilder
    {
        private const string ScenePath     = "Assets/Game/Scenes/Caldeiral.unity";
        private const string CutscenesDir  = "Assets/Game/Cutscenes";
        private const string SignalsDir    = "Assets/Game/Cutscenes/Signals";
        private const string TimelinePath  = "Assets/Game/Cutscenes/CS01_Opening_Concerto.playable";
        private const string VolumeProf    = "Assets/Game/Cutscenes/CS01_PostProcessProfile.asset";
        private const string UiPrefabPath  = "Assets/Game/Prefabs/UI/CutsceneUI_Canvas.prefab";
        private const string SubJsonPath   = "Assets/Game/Cutscenes/Data/cs01_abertura_subtitles.json";
        private const string Cs01ArtDir    = "Assets/Game/Art/Generated/cs01";
        private const string Fab3dMatDir   = "Assets/Game/Materials/Fab3D";
        private const string CsTexDir      = "Assets/Game/Art/Textures/Cutscenes";
        private const string AudioDir      = "Assets/Game/Audio/Cutscenes/CS01";

        [MenuItem("Studio/Concerto/Cutscenes/Build Cutscene 01 (Concerto Opening) in Caldeiral")]
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
                Debug.LogError($"[Cutscene01Builder] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureDir(CutscenesDir);
            EnsureDir(SignalsDir);
            EnsureDir("Assets/Game/Prefabs/UI");
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Garante CinemachineBrain (Cinemachine 3.1.4!) na Main Camera (desligado fora da cutscene)
            var mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (mainCam == null) throw new Exception("Main Camera nao encontrada em Caldeiral.unity!");
            var cmBrain = mainCam.GetComponent<CinemachineBrain>();
            if (cmBrain == null) cmBrain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            cmBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.0f);
            cmBrain.enabled = false;

            // 2. Cria/atualiza Prefab uGUI reutilizavel de Cutscene
            var uiPrefab = BuildCutsceneUIPrefab();

            // 3. Instala Props narrativos da Cutscene 1 na Oficina Z1 (sem duplicar cena!)
            var oldProps = GameObject.Find("CS01_Narrative_Props");
            if (oldProps != null) UnityEngine.Object.DestroyImmediate(oldProps);
            var propsRoot = new GameObject("CS01_Narrative_Props").transform;
            var z1 = GameObject.Find("Z1_Oficina_da_Sucata")?.transform;
            if (z1 != null) propsRoot.SetParent(z1, false);

            PlaceNarrativeProps(propsRoot);

            // 4. Monta o root da Cutscene 1 (PlayableDirector + CutsceneManager + SignalReceiver + Rig de Luz + Volume + VCams)
            var oldCs = GameObject.Find("CS01_Opening_Concerto");
            if (oldCs != null) UnityEngine.Object.DestroyImmediate(oldCs);
            var csRoot = new GameObject("CS01_Opening_Concerto");

            var director = csRoot.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            var receiver = csRoot.AddComponent<SignalReceiver>();

            var managerType = Type.GetType("Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp");
            if (managerType == null) throw new Exception("CutsceneManager nao encontrado em Assembly-CSharp!");
            var managerComp = csRoot.AddComponent(managerType);

            // UI Instance
            var uiInst = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, csRoot.transform);
            uiInst.name = "CutsceneUI_Canvas";
            var uiOverlayType = Type.GetType("Studio.Game.Cutscenes.CutsceneUIOverlay, Assembly-CSharp");
            var uiOverlayComp = uiInst.GetComponent(uiOverlayType);

            // Rig de Luz proprio da Cutscene (ligado SO durante a cutscene!)
            var lightRig = BuildCutsceneLightRig(csRoot.transform);

            // URP Volume temporario (Color Grade quente + Vinheta + DoF leve — ligado SO durante a cutscene!)
            var ppVol = BuildCutscenePostProcessVolume(csRoot.transform);

            // 5 CinemachineCameras (Cinemachine 3.1.4!) para os 5 Beats
            var camsRoot = new GameObject("CS01_VirtualCameras");
            camsRoot.transform.SetParent(csRoot.transform, false);

            var vcamB1 = CreateVCam(camsRoot.transform, "VCam_B1_BenchClose",       new Vector3(-28.45f, 1.48f, -18.35f), new Vector3(-29.02f, 1.02f, -17.42f), fov: 36f, priority: 20);
            var vcamB2 = CreateVCam(camsRoot.transform, "VCam_B2_WorkshopReveal",   new Vector3(-29.8f,  1.85f, -19.2f),  new Vector3(-26.8f,  1.25f, -17.8f),  fov: 48f, priority: 20);
            var vcamB3 = CreateVCam(camsRoot.transform, "VCam_B3_ChecklistAndJuno", new Vector3(-30.5f,  1.65f, -16.9f),  new Vector3(-28.0f,  1.25f, -14.8f),  fov: 42f, priority: 20);
            var vcamB4 = CreateVCam(camsRoot.transform, "VCam_B4_PhotoEmotion",     new Vector3(-28.60f, 1.38f, -18.08f), new Vector3(-29.05f, 1.02f, -17.45f), fov: 32f, priority: 20);
            var vcamB5 = CreateVCam(camsRoot.transform, "VCam_B5_MachineTitle",     new Vector3(-31.8f,  1.95f, -17.2f),  new Vector3(-26.2f,  1.25f, -19.5f),  fov: 44f, priority: 20);

            // AudioSources TEMP da cutscene
            var audioRoot = new GameObject("CS01_Audio").transform;
            audioRoot.SetParent(csRoot.transform, false);
            var srcMusicBox = CreateAudioSource(audioRoot, "Audio_MusicBox_TEMP", $"{AudioDir}/cs01_musicbox_motif_TEMP.wav", loop: false, vol: 0.65f);
            var srcHum      = CreateAudioSource(audioRoot, "Audio_MachineHum_TEMP", $"{AudioDir}/cs01_machine_hum_TEMP.wav", loop: true,  vol: 0.42f);
            var srcSfx      = CreateAudioSource(audioRoot, "Audio_SFX_TEMP", null, loop: false, vol: 0.70f);

            var radioClip  = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/cs01_radio_chime_TEMP.wav");
            var memoryClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/cs01_echo_memory_TEMP.wav");

            // 5. Constroi o TimelineAsset (CS01_Opening_Concerto.playable)
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            // Track 1: CinemachineTrack (Cinemachine 3.1.4!) -> 5 Beats (0..72s)
            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Shot List");
            director.SetGenericBinding(cmTrack, cmBrain);

            AddCmShot(cmTrack, director, "Beat1_BenchClose",       vcamB1,  0.0, 10.5);
            AddCmShot(cmTrack, director, "Beat2_WorkshopReveal",   vcamB2, 10.0, 15.5);
            AddCmShot(cmTrack, director, "Beat3_ChecklistAndJuno", vcamB3, 25.0, 20.5);
            AddCmShot(cmTrack, director, "Beat4_PhotoEmotion",     vcamB4, 45.0, 15.5);
            AddCmShot(cmTrack, director, "Beat5_MachineTitle",     vcamB5, 60.0, 12.0);

            // Track 2: AnimationTrack para dolly lento de recuo no Beat 2 (revelando a oficina!)
            var dollyTrack = timeline.CreateTrack<AnimationTrack>(null, "Beat2_CameraDolly");
            director.SetGenericBinding(dollyTrack, vcamB2.gameObject);
            var dollyClip = CreateCameraDollyClip(
                "Assets/Game/Cutscenes/Anim_B2_WorkshopDolly.anim",
                new Vector3(-29.8f, 1.85f, -19.2f),
                new Vector3(-33.8f, 2.55f, -21.8f),
                duration: 15.5f
            );
            var dClip = dollyTrack.CreateClip(dollyClip);
            dClip.start = 10.0;
            dClip.duration = 15.5;

            // Track 3: ActivationTracks para Rig de Luz e URP Post-Processing Volume
            var actLight = timeline.CreateTrack<ActivationTrack>(null, "Cutscene_LightRig");
            director.SetGenericBinding(actLight, lightRig);
            var alClip = actLight.CreateDefaultClip();
            alClip.start = 0.0;
            alClip.duration = 71.8;

            var actVol = timeline.CreateTrack<ActivationTrack>(null, "Cutscene_PostProcessVolume");
            director.SetGenericBinding(actVol, ppVol.gameObject);
            var avClip = actVol.CreateDefaultClip();
            avClip.start = 0.0;
            avClip.duration = 71.8;

            // Track 4: SignalTrack com 7 SignalEmitters sincronizados aos beats!
            var sigTrack = timeline.CreateTrack<SignalTrack>(null, "Audio_And_Choreography_Signals");
            director.SetGenericBinding(sigTrack, receiver);

            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat1_MusicBox",      0.2,  "OnSignalMusicBox");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat1_RadioChime",    2.0,  "OnSignalRadioChime");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat2_MachineHum",   10.2,  "OnSignalMachineHum");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat3_DezWalkBoard", 25.0,  "OnSignalDezWalkToBoard");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat4_MemoryEcho",   47.5,  "OnSignalMemoryEcho");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Beat5_TitleMotif",   61.5,  "OnSignalTitleCardMotif");
            BindSignal(sigTrack, receiver, managerComp, "SIG_Cutscene_End",       71.8,  "OnSignalCutsceneEnd");

            director.playableAsset = timeline;
            EditorUtility.SetDirty(timeline);

            // 6. Liga todas as referencias serializadas do CutsceneManager
            var so = new SerializedObject(managerComp);
            so.FindProperty("cutsceneId").stringValue = "cs01_abertura_concerto";
            so.FindProperty("subtitleJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SubJsonPath);
            so.FindProperty("playOnFirstBoot").boolValue = true;
            so.FindProperty("cutsceneLightRig").objectReferenceValue = lightRig;
            so.FindProperty("cutscenePostProcessVolume").objectReferenceValue = ppVol;
            so.FindProperty("cutsceneCamerasRoot").objectReferenceValue = camsRoot;
            so.FindProperty("uiOverlay").objectReferenceValue = uiOverlayComp;
            so.FindProperty("musicBoxSource").objectReferenceValue = srcMusicBox;
            so.FindProperty("machineHumSource").objectReferenceValue = srcHum;
            so.FindProperty("sfxOneShotSource").objectReferenceValue = srcSfx;
            so.FindProperty("radioChimeClip").objectReferenceValue = radioClip;
            so.FindProperty("memoryEchoClip").objectReferenceValue = memoryClip;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Desliga rig no EditMode para nao alterar o visual padrao de gameplay
            lightRig.SetActive(false);
            ppVol.gameObject.SetActive(false);
            camsRoot.SetActive(false);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Cutscene01Builder] Cutscene 1 ('Concerto', 72s, 5 Beats, Cinemachine 3.1.4 + Timeline + Signals + URP Volume) montada com sucesso em Caldeiral.unity!");
        }

        private static void PlaceNarrativeProps(Transform parent)
        {
            // A) Caixinha de Musica (TripoSG + PBR 4K) sobre a Bancada I1 (-29.0, 0, -17.5)
            var musicBoxMat = EnsureFab3dMat("cs01_prop_caixinha_musica", new Color(0.2f, 2.6f, 3.0f));
            PlaceFbxProp($"{Cs01ArtDir}/cs01_prop_caixinha_musica.fbx", "CS01_Caixinha_Musica", parent,
                new Vector3(-28.72f, 0.96f, -17.42f), new Vector3(0f, 155f, 0f), targetMaxDim: 0.28f, musicBoxMat, addCollider: false);

            // B) Porta-retrato vintage com a foto de Dez e a esposa ao lado da caixinha na bancada!
            var photoFrame = new GameObject("CS01_Foto_Dez_Esposa");
            photoFrame.transform.SetParent(parent, false);
            photoFrame.transform.position = new Vector3(-29.18f, 1.09f, -17.36f);
            photoFrame.transform.rotation = Quaternion.Euler(10f, -22f, 0f); // Face -Z do Quad voltada para a camera B1/B4!

            var matBrass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/CaldeiralC4/Mat_C4_Brass_Accent.mat");
            var frameBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameBack.name = "BrassFrame";
            frameBack.transform.SetParent(photoFrame.transform, false);
            frameBack.transform.localScale = new Vector3(0.20f, 0.25f, 0.025f);
            UnityEngine.Object.DestroyImmediate(frameBack.GetComponent<Collider>());
            if (matBrass != null) frameBack.GetComponent<MeshRenderer>().sharedMaterial = matBrass;

            var photoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photoQuad.name = "PhotoSurface";
            photoQuad.transform.SetParent(photoFrame.transform, false);
            photoQuad.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            photoQuad.transform.localScale = new Vector3(0.17f, 0.22f, 1f);
            UnityEngine.Object.DestroyImmediate(photoQuad.GetComponent<Collider>());
            photoQuad.GetComponent<MeshRenderer>().sharedMaterial = EnsureTextureMat("Assets/Game/Materials/CaldeiralC4/Mat_CS01_FotoDezEsposa.mat", $"{CsTexDir}/cs01_foto_dez_esposa.png");

            // C) Estrutura da Maquina do Tempo coberta com lona (TripoSG + PBR 4K) no centro-leste da oficina
            var machineMat = EnsureFab3dMat("cs01_prop_maquina_coberta", new Color(0.18f, 3.2f, 3.6f));
            PlaceFbxProp($"{Cs01ArtDir}/cs01_prop_maquina_coberta.fbx", "CS01_Maquina_Coberta_Lona", parent,
                new Vector3(-25.8f, 0.0f, -15.4f), new Vector3(0f, -135f, 0f), targetMaxDim: 2.35f, machineMat, addCollider: true);

            // D) Quadro de Checklist de Pecas ("PROJETO ECHO - CONFIDENCIAL") na parede norte da oficina (perto da estante)
            var board = new GameObject("CS01_Quadro_Checklist_Pecas");
            board.transform.SetParent(parent, false);
            board.transform.position = new Vector3(-25.8f, 1.95f, -12.12f);
            board.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Face -Z do Quad voltada para dentro da oficina!

            var boardBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardBack.name = "BoardHousing";
            boardBack.transform.SetParent(board.transform, false);
            boardBack.transform.localScale = new Vector3(2.10f, 1.55f, 0.08f);
            UnityEngine.Object.DestroyImmediate(boardBack.GetComponent<Collider>());
            if (matBrass != null) boardBack.GetComponent<MeshRenderer>().sharedMaterial = matBrass;

            var boardFace = GameObject.CreatePrimitive(PrimitiveType.Quad);
            boardFace.name = "ChecklistFace";
            boardFace.transform.SetParent(board.transform, false);
            boardFace.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            boardFace.transform.localScale = new Vector3(2.00f, 1.46f, 1f);
            UnityEngine.Object.DestroyImmediate(boardFace.GetComponent<Collider>());
            boardFace.GetComponent<MeshRenderer>().sharedMaterial = EnsureTextureMat("Assets/Game/Materials/CaldeiralC4/Mat_CS01_ChecklistBoard.mat", $"{CsTexDir}/cs01_quadro_checklist.png");
        }

        private static GameObject BuildCutsceneLightRig(Transform parent)
        {
            var rig = new GameObject("CS01_CutsceneLightRig");
            rig.transform.SetParent(parent, false);

            // Key quente cinematografica destacando a foto e a caixinha de musica na bancada
            AddLight(rig.transform, "CS01_Key_WorkbenchWarm", LightType.Spot, new Vector3(-28.4f, 2.5f, -18.5f), Quaternion.LookRotation(new Vector3(-0.6f, -1.5f, 1.0f)), new Color(1.00f, 0.85f, 0.62f), 28f, 9f, 52f);
            // Rim light ciano misterioso saindo debaixo da lona da Maquina do Tempo
            AddLight(rig.transform, "CS01_Rim_MachineUnderglow", LightType.Point, new Vector3(-25.8f, 0.45f, -15.4f), Quaternion.identity, new Color(0.18f, 0.90f, 1.00f), 14f, 7.5f, 60f);
            // Fill suave no rosto do Dez e da Juno durante o Beat 3/4
            AddLight(rig.transform, "CS01_Fill_Actors", LightType.Point, new Vector3(-28.8f, 2.2f, -16.5f), Quaternion.identity, new Color(1.00f, 0.92f, 0.82f), 12f, 8f, 60f);

            return rig;
        }

        private static Volume BuildCutscenePostProcessVolume(Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProf);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProf);
            }
            profile.components.Clear();

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.15f);
            colorAdj.contrast.Override(14f);
            colorAdj.saturation.Override(8f);
            colorAdj.colorFilter.Override(new Color(1.00f, 0.96f, 0.90f));

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.05f, 0.04f, 0.04f));

            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(2.6f);
            dof.focalLength.Override(42f);
            dof.aperture.Override(8.0f); // DoF leve cinematografico sem borrar o rosto

            EditorUtility.SetDirty(profile);

            var go = new GameObject("CS01_PostProcessVolume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50f;
            vol.sharedProfile = profile;
            vol.weight = 1f;
            return vol;
        }

        private static GameObject BuildCutsceneUIPrefab()
        {
            var root = new GameObject("CutsceneUI_Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Letterbox Top & Bottom
            var topBar = CreateUiPanel(root.transform, "Letterbox_Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0), Color.black);
            var botBar = CreateUiPanel(root.transform, "Letterbox_Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0), Color.black);

            // Subtitles Container (acima da barra inferior)
            var subPanel = CreateUiPanel(root.transform, "Subtitle_Box", new Vector2(0.14f, 0), new Vector2(0.86f, 0), new Vector2(0.5f, 0), new Vector2(0, 122), new Vector2(0, 118), new Color(0.03f, 0.04f, 0.06f, 0.78f));
            var subGroup = subPanel.gameObject.AddComponent<CanvasGroup>();
            var speakerTxt = CreateUiText(subPanel, "SpeakerText", font, 24, TextAnchor.UpperCenter, new Color(1f, 0.80f, 0.42f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(-40, 34));
            speakerTxt.fontStyle = FontStyle.Bold;
            var bodyTxt = CreateUiText(subPanel, "BodyText", font, 30, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -14), new Vector2(-48, -36));

            // Title Card ("CONCERTO")
            var tcPanel = CreateUiPanel(root.transform, "TitleCard_Overlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.03f, 0.45f));
            var tcGroup = tcPanel.gameObject.AddComponent<CanvasGroup>();
            var tcTxt = CreateUiText(tcPanel, "TitleCardText", font, 108, TextAnchor.MiddleCenter, new Color(0.98f, 0.92f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tcTxt.fontStyle = FontStyle.Bold;
            tcTxt.text = "CONCERTO";

            // Skip Prompt (canto inferior direito sobre a barra)
            var skipPanel = CreateUiPanel(root.transform, "Skip_Prompt", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 28), new Vector2(410, 52), new Color(0.08f, 0.09f, 0.11f, 0.82f));
            var skipGroup = skipPanel.gameObject.AddComponent<CanvasGroup>();
            var skipTxt = CreateUiText(skipPanel, "SkipText", font, 20, TextAnchor.MiddleCenter, new Color(0.88f, 0.90f, 0.94f), new Vector2(0, 0.25f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var fillBg = CreateUiPanel(skipPanel, "SkipFillBg", new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.20f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.28f, 0.32f, 0.9f));
            var fillRect = CreateUiPanel(fillBg, "SkipProgressFill", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.88f, 0.98f, 1f));
            var fillImg = fillRect.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            // Objetivo Suave pos-cutscene (canto superior esquerdo)
            var objPanel = CreateUiPanel(root.transform, "PostObjective_Toast", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -36), new Vector2(420, 56), new Color(0.06f, 0.08f, 0.11f, 0.84f));
            var objGroup = objPanel.gameObject.AddComponent<CanvasGroup>();
            var objTxt = CreateUiText(objPanel, "ObjectiveText", font, 23, TextAnchor.MiddleLeft, new Color(0.88f, 0.96f, 1.0f), Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-24, 0));

            var uiType = Type.GetType("Studio.Game.Cutscenes.CutsceneUIOverlay, Assembly-CSharp");
            var overlay = root.AddComponent(uiType);
            var so = new SerializedObject(overlay);
            so.FindProperty("topBar").objectReferenceValue = topBar;
            so.FindProperty("bottomBar").objectReferenceValue = botBar;
            so.FindProperty("subtitleGroup").objectReferenceValue = subGroup;
            so.FindProperty("speakerText").objectReferenceValue = speakerTxt;
            so.FindProperty("bodyText").objectReferenceValue = bodyTxt;
            so.FindProperty("titleCardGroup").objectReferenceValue = tcGroup;
            so.FindProperty("titleCardText").objectReferenceValue = tcTxt;
            so.FindProperty("skipGroup").objectReferenceValue = skipGroup;
            so.FindProperty("skipHintText").objectReferenceValue = skipTxt;
            so.FindProperty("skipProgressFill").objectReferenceValue = fillImg;
            so.FindProperty("objectiveGroup").objectReferenceValue = objGroup;
            so.FindProperty("objectiveText").objectReferenceValue = objTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static CinemachineCamera CreateVCam(Transform parent, string name, Vector3 pos, Vector3 lookAt, float fov, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(lookAt);

            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = priority;
            var lens = vcam.Lens;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 120f;
            vcam.Lens = lens;
            return vcam;
        }

        private static void AddCmShot(CinemachineTrack track, PlayableDirector dir, string displayName, CinemachineCamera vcam, double start, double duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.displayName = displayName;
            clip.start = start;
            clip.duration = duration;
            var shot = clip.asset as CinemachineShot;
            shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = GUID.Generate().ToString() };
            dir.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
        }

        private static AnimationClip CreateCameraDollyClip(string assetPath, Vector3 fromPos, Vector3 toPos, float duration)
        {
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(assetPath) };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.EaseInOut(0f, fromPos.x, duration, toPos.x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.EaseInOut(0f, fromPos.y, duration, toPos.y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.EaseInOut(0f, fromPos.z, duration, toPos.z));

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(clip, assetPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static void BindSignal(SignalTrack track, SignalReceiver receiver, Component targetComp, string signalName, double timeSec, string methodName)
        {
            string sigPath = $"{SignalsDir}/{signalName}.signal";
            var sigAsset = AssetDatabase.LoadAssetAtPath<SignalAsset>(sigPath);
            if (sigAsset == null)
            {
                sigAsset = ScriptableObject.CreateInstance<SignalAsset>();
                AssetDatabase.CreateAsset(sigAsset, sigPath);
            }
            var emitter = track.CreateMarker<SignalEmitter>(timeSec);
            emitter.asset = sigAsset;
            emitter.emitOnce = true;

            var reaction = new UnityEvent();
            var mi = targetComp.GetType().GetMethod(methodName);
            if (mi != null)
            {
                var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), targetComp, mi);
                UnityEventTools.AddPersistentListener(reaction, action);
            }
            receiver.AddReaction(sigAsset, reaction);
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

        private static void AddLight(Transform parent, string name, LightType type, Vector3 pos, Quaternion rot, Color col, float intensity, float range, float spotAngle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = spotAngle;
            l.shadows = LightShadows.None;
        }

        private static void PlaceFbxProp(string fbxPath, string name, Transform parent, Vector3 pos, Vector3 euler, float targetMaxDim, Material mat, bool addCollider)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[Cutscene01Builder] FBX ainda nao encontrado: {fbxPath}");
                return;
            }
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.transform.position = pos;
            container.transform.rotation = Quaternion.Euler(euler);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, container.transform);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (mat != null) r.sharedMaterial = mat;

            Bounds b = ProceduralMeshLibrary.ComputeBounds(container);
            float maxD = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxD > 0.001f) container.transform.localScale = Vector3.one * (targetMaxDim / maxD);
            b = ProceduralMeshLibrary.ComputeBounds(container);
            container.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);

            if (addCollider)
            {
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }
        }

        private static Material EnsureFab3dMat(string slug, Color emissionHdr)
        {
            string albPath = $"{Fab3dMatDir}/{slug}_albedo.png";
            string msPath  = $"{Fab3dMatDir}/{slug}_metallicsmooth.png";
            string emPath  = $"{Fab3dMatDir}/{slug}_emission.png";
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(albPath);
            var ms  = AssetDatabase.LoadAssetAtPath<Texture2D>(msPath);
            var em  = AssetDatabase.LoadAssetAtPath<Texture2D>(emPath);
            mat.SetTexture("_BaseMap", alb);
            mat.SetColor("_BaseColor", Color.white);
            if (ms != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 0.65f);
            }
            if (em != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", em);
                mat.SetColor("_EmissionColor", emissionHdr);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTextureMat(string matPath, string texPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static RectTransform CreateUiPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return rt;
        }

        private static Text CreateUiText(Transform parent, string name, Font font, int fontSize, TextAnchor align, Color col, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void EnsureDir(string assetDir)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetDir);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
