using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Studio.Editor
{
    /// <summary>
    /// SetorExternoBuilder — Constroi o novo cenario do Ato 3 ("SetorExterno.unity") + Porta de conexao em Caldeiral.unity:
    ///   - Importa 4 assets TripoSG + PBR 4K: NPC Engenheira Vera (UniRig Humanoid), Gerador, Antena, Modulo de Carga
    ///   - Atmosfera fria contrastante com a oficina quente (luz azulada/ciano, neblina fria, vento, URP Volume frio)
    ///   - Ceu Errado economico: rachadura luminosa ciano/magenta no ceu + faixas de aurora glitch animadas (SkyRiftAnimator)
    ///   - Reusa TODOS os sistemas existentes (Player_Dez, VCam_Gameplay_Dez, Dialogue_System, PauseMenu, Repair, MultiTool, Save)
    ///   - Instala 3 anomalias novas (Anomaly + Repairable) via act3_sector_anomalies_config.json (ativas ao entrar!)
    ///   - Instala Porta Oficina <-> Setor Externo (SectorDoorInteractable com flag CONCERTO_SECTOR_EXTERNO_UNLOCKED / F6)
    ///   - Registra [MainMenu, Caldeiral, SetorExterno] no EditorBuildSettings
    /// </summary>
    public static class SetorExternoBuilder
    {
        private const string MainMenuPath   = "Assets/Game/Scenes/MainMenu.unity";
        private const string CaldeiralPath  = "Assets/Game/Scenes/Caldeiral.unity";
        private const string SectorPath     = "Assets/Game/Scenes/SetorExterno.unity";
        private const string VeraFbxPath    = "Assets/Game/Art/Generated/npcs/npc_engenheira_vera/npc_engenheira_vera_humanoid.fbx";
        private const string GenFbxPath     = "Assets/Game/Art/Generated/sector/sector_prop_gerador.fbx";
        private const string AntFbxPath     = "Assets/Game/Art/Generated/sector/sector_prop_antena.fbx";
        private const string CargoFbxPath   = "Assets/Game/Art/Generated/sector/sector_prop_carga.fbx";
        private const string Fab3dMatDir    = "Assets/Game/Materials/Fab3D";
        private const string PrefabDir      = "Assets/Game/Prefabs/Fab3D";
        private const string CtrlPath       = "Assets/Game/Animations/HeroDezController.controller";
        private const string VeraDlgPath    = "Assets/Game/Dialogue/Data/dlg_vera_setor_externo.json";
        private const string AnomJsonPath   = "Assets/Game/Anomalies/Data/act3_sector_anomalies_config.json";
        private const string ColdVolProf    = "Assets/Game/Cutscenes/SectorExterno_ColdVolumeProfile.asset";
        private const string WindAudioPath  = "Assets/Game/Audio/SFX/Sector/sector_cold_wind_ambience_TEMP.wav";
        private const string GlowMatPath    = "Assets/Game/Cutscenes/Mat_CS02_Particle_Glow.mat";

        [MenuItem("Studio/Concerto/Act3/Build Setor Externo & Connecting Door")]
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
                Debug.LogError($"[SetorExternoBuilder] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            AssetDatabase.Refresh();

            foreach (var p in new[] { VeraFbxPath, GenFbxPath, AntFbxPath, CargoFbxPath })
            {
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), p)))
                    throw new FileNotFoundException($"Asset TripoSG obrigatorio ausente: {p}");
            }

            // 1. Configura Humanoid Avatar + Materiais PBR 4K + Prefab da Engenheira Vera
            var animCtrl   = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            var veraAvatar = ConfigureHumanoidFbx(VeraFbxPath);
            var veraMat    = EnsurePbrMaterial("npc_engenheira_vera", withEmission: false);
            var veraPrefab = CreateNpcPrefab("npc_engenheira_vera", VeraFbxPath, veraAvatar, veraMat, animCtrl);

            ConfigurePropFbx(GenFbxPath);
            ConfigurePropFbx(AntFbxPath);
            ConfigurePropFbx(CargoFbxPath);
            var genMat   = EnsurePbrMaterial("sector_prop_gerador", withEmission: true);
            var antMat   = EnsurePbrMaterial("sector_prop_antena",  withEmission: true);
            var cargoMat = EnsurePbrMaterial("sector_prop_carga",   withEmission: false);

            // 2. Instala Porta Oficina -> Setor Externo em Caldeiral.unity
            InstallCaldeiralExitDoor();

            // 3. Cria cena SetorExterno.unity e clona sistemas existentes de Caldeiral (ZERO duplicacao de logica!)
            var sectorScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var caldScene   = EditorSceneManager.OpenScene(CaldeiralPath, OpenSceneMode.Additive);

            string[] reusableNames =
            {
                "Main Camera",
                "EventSystem",
                "Player_Dez_Placeholder",
                "VCam_Gameplay_Dez",
                "Dialogue_And_Objective_System",
                "PauseMenu_Canvas",
                "Repair_System_Manager",
                "MultiTool_System",
                "Save_Checkpoints_Manager",
            };

            var clones = new Dictionary<string, GameObject>();
            foreach (string goName in reusableNames)
            {
                var src = FindInScene(caldScene, goName);
                if (src == null) continue;
                var copy = UnityEngine.Object.Instantiate(src);
                copy.name = goName;
                SceneManager.MoveGameObjectToScene(copy, sectorScene);
                clones[goName] = copy;
            }
            EditorSceneManager.CloseScene(caldScene, removeScene: true);
            SceneManager.SetActiveScene(sectorScene);

            // Remove dummy de treino da oficina caso tenha vindo no MultiTool_System
            if (clones.TryGetValue("MultiTool_System", out var mtRoot))
            {
                var dummy = mtRoot.transform.Find("Tool_TrainingTarget_Dummy");
                if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy.gameObject);
            }

            // Limpa initialObjectiveKey no DialogueController clonado para nao sobrescrever objetivo do Ato 2/3
            if (clones.TryGetValue("Dialogue_And_Objective_System", out var dlgRoot))
            {
                var dcType = Type.GetType("Studio.Game.Dialogue.DialogueController, Assembly-CSharp");
                var dc = dcType != null ? dlgRoot.GetComponent(dcType) : null;
                if (dc != null)
                {
                    var dlgSo = new SerializedObject(dc);
                    dlgSo.FindProperty("initialObjectiveKey").stringValue = "";
                    dlgSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Posiciona Dez na entrada do patio olhando pro horizonte e pra rachadura no ceu!
            if (clones.TryGetValue("Player_Dez_Placeholder", out var dezGo))
            {
                var cc = dezGo.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                dezGo.transform.position = new Vector3(0f, 0.05f, 3.0f);
                dezGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                if (cc != null) cc.enabled = true;

                // Re-vincula VCam_Gameplay_Dez e MultiTool_System aos novos clones na cena!
                var pivot = dezGo.transform.Find("Dez_CameraTarget");
                var handTool = dezGo.transform.Find("Dez_RepairTool_InHand");
                if (clones.TryGetValue("VCam_Gameplay_Dez", out var vcamGo) && pivot != null)
                {
                    var vcam = vcamGo.GetComponent<CinemachineCamera>();
                    if (vcam != null) vcam.Target.TrackingTarget = pivot;
                    var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
                    var loco = dezGo.GetComponent(locoType);
                    if (loco != null)
                    {
                        var lso = new SerializedObject(loco);
                        lso.FindProperty("cameraFollowTarget").objectReferenceValue = pivot;
                        lso.FindProperty("gameplayVcam").objectReferenceValue = vcam;
                        lso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                if (clones.TryGetValue("MultiTool_System", out var mtGo))
                {
                    var mtType = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
                    var mt = mtGo.GetComponent(mtType);
                    if (mt != null)
                    {
                        var mso = new SerializedObject(mt);
                        mso.FindProperty("playerTransform").objectReferenceValue = dezGo.transform;
                        mso.FindProperty("cameraShakePivot").objectReferenceValue = pivot;
                        mso.FindProperty("toolVisualTransform").objectReferenceValue = handTool;
                        mso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                if (clones.TryGetValue("Repair_System_Manager", out var repGo))
                {
                    var rcType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
                    var rc = repGo.GetComponent(rcType);
                    if (rc != null)
                    {
                        var rso = new SerializedObject(rc);
                        rso.FindProperty("dezRepairToolVisual").objectReferenceValue = handTool != null ? handTool.gameObject : null;
                        rso.FindProperty("workbenchTrinketProp").objectReferenceValue = null;
                        rso.FindProperty("ceilingLightsRoot").objectReferenceValue = null;
                        rso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // Configura Main Camera para o ceu frio escuro
            if (clones.TryGetValue("Main Camera", out var camGo))
            {
                var cam = camGo.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.03f, 0.055f, 0.105f, 1f);
                    cam.farClipPlane = 260f;
                }
                var brain = camGo.GetComponent<CinemachineBrain>();
                if (brain != null) brain.enabled = true;
            }

            // 4. Atmosfera Fria (Contraste com a Oficina Quente) + Ceu Errado (Rachadura Luminosa + Aurora Glitch)
            BuildColdAtmosphereAndWrongSky();

            // 5. Patio Caminhavel + Perimetro da Instalacao Fechando o Horizonte + 3 POIs TripoSG
            var envRoot = new GameObject("SetorExterno_Environment").transform;
            BuildCourtyardAndHorizon(envRoot);

            PlaceNormalizedProp(envRoot, "POI_01_Gerador_TripoSG", GenFbxPath,   genMat,   new Vector3(-12.0f, 0.0f, 12.0f), Quaternion.Euler(0f, 28f, 0f),  targetHeight: 4.4f);
            PlaceNormalizedProp(envRoot, "POI_02_Antena_TripoSG",  AntFbxPath,   antMat,   new Vector3( 13.0f, 0.0f, 16.0f), Quaternion.Euler(0f, -35f, 0f), targetHeight: 7.5f);
            PlaceNormalizedProp(envRoot, "POI_03_Carga_TripoSG",   CargoFbxPath, cargoMat, new Vector3(  1.5f, 0.0f, 25.0f), Quaternion.Euler(0f, 12f, 0f),  targetHeight: 3.6f);

            // 6. Porta de Retorno Setor Externo -> Oficina (Caldeiral)
            CreateDoorVisualAndInteractable(envRoot, "Door_Sector_To_Caldeiral", new Vector3(0f, 0f, -1.2f), Quaternion.identity,
                targetScene: "Caldeiral", requiresFlag: false);

            // 7. Novo NPC TripoSG: Engenheira Vera (conversa real data-driven -> objetivo Ato 3)
            var veraGo = (GameObject)PrefabUtility.InstantiatePrefab(veraPrefab);
            veraGo.name = "NPC_Engenheira_Vera";
            NormalizeHeight(veraGo, 1.28f);
            veraGo.transform.position = new Vector3(-2.6f, 0.02f, 8.2f);
            veraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, 0f, -0.89f));

            var dlgNpcType = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var dlgComp = veraGo.AddComponent(dlgNpcType);
            var dso = new SerializedObject(dlgComp);
            dso.FindProperty("dialogueJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(VeraDlgPath);
            dso.ApplyModifiedPropertiesWithoutUndo();
            var vc = veraGo.AddComponent<SphereCollider>();
            vc.isTrigger = true;
            vc.radius = 1.6f;
            vc.center = Vector3.up * 0.8f;

            // 8. 3 Anomalias Novas do Setor (Reusando Anomaly + Repairable + WorkshopAnomalyManager!)
            var anomRoot = BuildSectorAnomalies();
            if (clones.TryGetValue("Repair_System_Manager", out var repMgrGo))
            {
                var rcType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
                var amType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
                var rc = repMgrGo.GetComponent(rcType);
                var am = anomRoot.GetComponent(amType);
                if (rc != null && am != null)
                {
                    var rso = new SerializedObject(rc);
                    rso.FindProperty("anomalyManager").objectReferenceValue = am;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 9. Audio Ambiente Frio/Vento
            var windGo = new GameObject("Sector_ColdWind_Ambience");
            var windSrc = windGo.AddComponent<AudioSource>();
            windSrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WindAudioPath);
            windSrc.loop = true;
            windSrc.playOnAwake = true;
            windSrc.volume = 0.45f;
            windSrc.spatialBlend = 0f;

            // 10. Salva SetorExterno.unity e registra no EditorBuildSettings!
            EditorSceneManager.SaveScene(sectorScene, SectorPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(CaldeiralPath, true),
                new EditorBuildSettingsScene(SectorPath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SetorExternoBuilder] SetorExterno.unity + Porta Oficina<->Setor + Vera (TripoSG) + 3 POIs + 3 Anomalias + Ceu Errado construidos com sucesso!");
        }

        private static void InstallCaldeiralExitDoor()
        {
            var scene = EditorSceneManager.OpenScene(CaldeiralPath, OpenSceneMode.Single);
            var oldDoor = GameObject.Find("Door_Caldeiral_To_Sector");
            if (oldDoor != null) UnityEngine.Object.DestroyImmediate(oldDoor);

            // Posiciona a porta de saida no corredor leste da oficina (>12m longe da Juno em -27.3, -19.5!)
            CreateDoorVisualAndInteractable(null, "Door_Caldeiral_To_Sector", new Vector3(-16.5f, 0.0f, -13.5f), Quaternion.Euler(0f, -90f, 0f),
                targetScene: "SetorExterno", requiresFlag: true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CaldeiralPath);
        }

        private static void CreateDoorVisualAndInteractable(Transform parent, string name, Vector3 pos, Quaternion rot, string targetScene, bool requiresFlag)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = rot;

            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "BlastDoor_Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            frame.transform.localScale = new Vector3(2.8f, 3.2f, 0.45f);
            var fMat = new Material(lit);
            fMat.color = new Color(0.18f, 0.22f, 0.28f);
            fMat.SetFloat("_Metallic", 0.72f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = fMat;

            var statusLight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            statusLight.name = "DoorStatusStrip";
            UnityEngine.Object.DestroyImmediate(statusLight.GetComponent<Collider>());
            statusLight.transform.SetParent(root.transform, false);
            statusLight.transform.localPosition = new Vector3(0f, 3.05f, 0.26f);
            statusLight.transform.localScale = new Vector3(1.6f, 0.16f, 0.08f);
            var sMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? lit);
            sMat.color = requiresFlag ? new Color(0.95f, 0.22f, 0.18f) : new Color(0.28f, 0.94f, 1.0f);
            statusLight.GetComponent<MeshRenderer>().sharedMaterial = sMat;

            var doorType = Type.GetType("Studio.Game.Sector.SectorDoorInteractable, Assembly-CSharp");
            var doorComp = root.AddComponent(doorType);
            var so = new SerializedObject(doorComp);
            so.FindProperty("targetSceneName").stringValue = targetScene;
            so.FindProperty("requiresUnlockFlag").boolValue = requiresFlag;
            so.FindProperty("statusLightRenderer").objectReferenceValue = statusLight.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 2.4f;
            trig.center = Vector3.up * 1.2f;
        }

        private static void BuildColdAtmosphereAndWrongSky()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.10f, 0.18f, 1f);
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.14f, 0.24f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.14f, 0.22f);
            RenderSettings.ambientGroundColor  = new Color(0.04f, 0.06f, 0.10f);

            // Luz direcional fria (contraste intencional com a oficina quente!)
            var dirGo = new GameObject("Sector_ColdMoonLight");
            var dl = dirGo.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.transform.rotation = Quaternion.Euler(34f, -35f, 0f);
            dl.color = new Color(0.58f, 0.80f, 1.00f);
            dl.intensity = 1.65f;
            dl.shadows = LightShadows.Soft;

            // URP Volume frio + bloom para a rachadura no ceu brilhar sem shader caro
            var volGo = new GameObject("Sector_ColdVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 5f;
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ColdVolProf) != null)
                AssetDatabase.DeleteAsset(ColdVolProf);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, ColdVolProf);
            var col = prof.Add<ColorAdjustments>(true);
            col.colorFilter.Override(new Color(0.84f, 0.93f, 1.00f));
            col.saturation.Override(-8f);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.45f);
            bloom.scatter.Override(0.75f);
            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;

            // === O CEU ERRADO (Peca Central): Rachadura Luminosa + Aurora Glitch ===
            var skyRoot = new GameObject("SkyRift_Centerpiece");
            skyRoot.transform.position = new Vector3(0f, 17.5f, 44f);
            var animType = Type.GetType("Studio.Game.Sector.SkyRiftAnimator, Assembly-CSharp");
            var skyAnim = skyRoot.AddComponent(animType);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var riftMat = new Material(unlit);
            riftMat.SetColor("_BaseColor", new Color(0.65f, 2.5f, 2.8f, 1f));
            var auroraMat1 = new Material(unlit);
            auroraMat1.SetColor("_BaseColor", new Color(0.22f, 1.55f, 1.25f, 0.85f));
            var auroraMat2 = new Material(unlit);
            auroraMat2.SetColor("_BaseColor", new Color(1.15f, 0.35f, 1.85f, 0.80f));

            // 5 segmentos em zigue-zague formando a rachadura luminosa rasgando o ceu!
            var riftSegs = new Transform[5];
            Vector3[] segPos = {
                new Vector3(-16f,  5.5f, 0f),
                new Vector3( -8f,  2.0f, 0f),
                new Vector3(  0f,  0.0f, 0f),
                new Vector3(  9f, -2.5f, 0f),
                new Vector3( 18f, -6.0f, 0f),
            };
            float[] segTilt = { -22f, -15f, -26f, -14f, -24f };
            for (int i = 0; i < 5; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                q.name = $"SkyRift_Seg_{i}";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(skyRoot.transform, false);
                q.transform.localPosition = segPos[i];
                q.transform.localRotation = Quaternion.Euler(0f, 0f, segTilt[i]);
                q.transform.localScale = new Vector3(10.5f, i == 2 ? 1.55f : 0.95f, 0.3f);
                q.GetComponent<MeshRenderer>().sharedMaterial = riftMat;
                riftSegs[i] = q.transform;
            }

            // 4 faixas de aurora glitch atravessando o horizonte
            var ribbons = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Quad);
                r.name = $"GlitchAurora_Ribbon_{i}";
                UnityEngine.Object.DestroyImmediate(r.GetComponent<Collider>());
                r.transform.SetParent(skyRoot.transform, false);
                r.transform.localPosition = new Vector3((i - 1.5f) * 14f, 6f + (i % 2) * 3.5f, 4f + i * 1.5f);
                r.transform.localRotation = Quaternion.Euler(10f, 0f, -8f + i * 4f);
                r.transform.localScale = new Vector3(26f, 2.2f, 1f);
                r.GetComponent<MeshRenderer>().sharedMaterial = (i % 2 == 0) ? auroraMat1 : auroraMat2;
                ribbons[i] = r.transform;
            }

            var riftLightGo = new GameObject("SkyRift_GlowLight");
            riftLightGo.transform.SetParent(skyRoot.transform, false);
            var riftLight = riftLightGo.AddComponent<Light>();
            riftLight.type = LightType.Point;
            riftLight.color = new Color(0.35f, 0.95f, 1.0f);
            riftLight.intensity = 60f;
            riftLight.range = 80f;

            var sso = new SerializedObject(skyAnim);
            var segProp = sso.FindProperty("riftSegments");
            segProp.arraySize = riftSegs.Length;
            for (int i = 0; i < riftSegs.Length; i++) segProp.GetArrayElementAtIndex(i).objectReferenceValue = riftSegs[i];
            var ribProp = sso.FindProperty("auroraRibbons");
            ribProp.arraySize = ribbons.Length;
            for (int i = 0; i < ribbons.Length; i++) ribProp.GetArrayElementAtIndex(i).objectReferenceValue = ribbons[i];
            sso.FindProperty("riftSkyLight").objectReferenceValue = dl;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCourtyardAndHorizon(Transform parent)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Piso principal caminhavel do patio (44m x 44m concreto industrial frio)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Courtyard_ConcreteFloor";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(0f, -0.5f, 16f);
            floor.transform.localScale = new Vector3(44f, 1f, 44f);
            var floorMat = new Material(lit);
            floorMat.color = new Color(0.24f, 0.28f, 0.34f);
            floorMat.SetFloat("_Smoothness", 0.25f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Muros baixos + estruturas da instalacao fechando o horizonte sem tapar o ceu errado!
            var wallMat = new Material(lit);
            wallMat.color = new Color(0.14f, 0.17f, 0.22f);
            wallMat.SetFloat("_Metallic", 0.55f);

            CreateBox(parent, "Perimeter_SouthWall", new Vector3(0f, 3.0f, -4.0f),  new Vector3(44f, 6f, 1.5f), wallMat);
            CreateBox(parent, "Perimeter_WestWall",  new Vector3(-22f, 3.2f, 16f), new Vector3(1.5f, 6.4f, 44f), wallMat);
            CreateBox(parent, "Perimeter_EastWall",  new Vector3( 22f, 3.2f, 16f), new Vector3(1.5f, 6.4f, 44f), wallMat);
            CreateBox(parent, "Perimeter_NorthLow",  new Vector3(0f, 1.8f, 36.5f), new Vector3(44f, 3.6f, 1.5f), wallMat);

            // Torres laterais fechando a silhueta da instalacao no fundo
            CreateBox(parent, "Horizon_Tower_NW", new Vector3(-21f, 9f, 36f), new Vector3(7f, 18f, 7f), wallMat);
            CreateBox(parent, "Horizon_Tower_NE", new Vector3( 21f, 11f, 36f), new Vector3(7f, 22f, 7f), wallMat);

            // Postes de luz fria espalhados pelo patio
            Vector3[] lamps = { new Vector3(-8f, 4.5f, 6f), new Vector3(8f, 4.5f, 8f), new Vector3(0f, 4.5f, 20f) };
            for (int i = 0; i < lamps.Length; i++)
            {
                var lGo = new GameObject($"Courtyard_ColdLamp_{i}");
                lGo.transform.SetParent(parent, false);
                lGo.transform.position = lamps[i];
                var l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.62f, 0.88f, 1.0f);
                l.intensity = 14f;
                l.range = 18f;
            }
        }

        private static GameObject BuildSectorAnomalies()
        {
            var root = new GameObject("Act3_Sector_Anomalies");
            var mgrType  = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            var anomType = Type.GetType("Studio.Game.Anomalies.Anomaly, Assembly-CSharp");
            var cfgType  = Type.GetType("Studio.Game.Anomalies.Act2AnomaliesConfigData, Assembly-CSharp");
            var repType  = Type.GetType("Studio.Game.Repair.Repairable, Assembly-CSharp");
            var kindEnum = Type.GetType("Studio.Game.Anomalies.AnomalyKind, Assembly-CSharp");

            var mgr = root.AddComponent(mgrType);
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AnomJsonPath);
            var parsed = cfgType.GetMethod("FromJson").Invoke(null, new object[] { jsonAsset });
            var entries = cfgType.GetField("anomalies").GetValue(parsed) as System.Collections.IList;
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var built = new List<Component>();
            foreach (var e in entries)
            {
                string id = (string)e.GetType().GetField("id").GetValue(e);
                string dn = (string)e.GetType().GetField("displayName").GetValue(e);
                string ks = (string)e.GetType().GetField("kind").GetValue(e);
                var posObj = e.GetType().GetField("position").GetValue(e);
                Vector3 pos = (Vector3)posObj.GetType().GetMethod("ToVector3").Invoke(posObj, null);

                var go = new GameObject($"SectorAnomaly_{id}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = pos;

                var a = go.AddComponent(anomType);
                var aso = new SerializedObject(a);
                aso.FindProperty("anomalyId").stringValue = id;
                aso.FindProperty("displayName").stringValue = dn;
                aso.FindProperty("kind").enumValueIndex = (int)Enum.Parse(kindEnum, ks);

                var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vis.name = "AnomalyCore";
                UnityEngine.Object.DestroyImmediate(vis.GetComponent<Collider>());
                vis.transform.SetParent(go.transform, false);
                vis.transform.localScale = new Vector3(0.55f, 0.22f, 0.55f);
                var vm = new Material(lit);
                vm.color = new Color(0.35f, 0.90f, 1.0f);
                vis.GetComponent<MeshRenderer>().sharedMaterial = vm;
                aso.FindProperty("targetVisual").objectReferenceValue = vis.transform;

                var l = new GameObject("AnomLight").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.type = LightType.Point;
                l.color = new Color(0.35f, 0.92f, 1.0f);
                l.intensity = 10f;
                l.range = 9f;
                aso.FindProperty("flickerLight").objectReferenceValue = l;

                var psGo = new GameObject("AnomParticles");
                psGo.transform.SetParent(go.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                var pm = ps.main;
                pm.playOnAwake = false; pm.startLifetime = 2.0f; pm.startSpeed = 0.3f; pm.gravityModifier = -0.22f;
                pm.startSize = 0.11f; pm.startColor = new Color(0.35f, 0.94f, 1.0f, 0.85f); pm.maxParticles = 50;
                var pem = ps.emission; pem.rateOverTime = 16f;
                if (glowMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;
                aso.FindProperty("anomalyParticles").objectReferenceValue = ps;
                aso.ApplyModifiedPropertiesWithoutUndo();

                // Anexa o MESMO componente generico Repairable!
                var rep = go.AddComponent(repType);
                var rso = new SerializedObject(rep);
                rso.FindProperty("repairableId").stringValue = id;
                rso.FindProperty("displayName").stringValue = dn;
                rso.FindProperty("repairDurationSec").floatValue = 3.8f;
                rso.FindProperty("linkedAnomaly").objectReferenceValue = a;
                rso.ApplyModifiedPropertiesWithoutUndo();

                var sc = go.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 2.2f;

                built.Add(a);
            }

            var mso = new SerializedObject(mgr);
            mso.FindProperty("anomaliesConfigJson").objectReferenceValue = jsonAsset;
            var lp = mso.FindProperty("anomalies");
            lp.ClearArray();
            for (int i = 0; i < built.Count; i++)
            {
                lp.InsertArrayElementAtIndex(i);
                lp.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
            }
            mso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static GameObject FindInScene(Scene s, string name)
        {
            foreach (var r in s.GetRootGameObjects())
            {
                if (r.name == name) return r;
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        private static Avatar ConfigureHumanoidFbx(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.SaveAndReimport();

            var hd = imp.humanDescription;
            hd.human = hd.human.Where(h => h.humanName != "UpperChest" && h.humanName != "LeftEye" && h.humanName != "RightEye").ToArray();
            imp.humanDescription = hd;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
        }

        private static void ConfigurePropFbx(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) return;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }

        private static Material EnsurePbrMaterial(string slug, bool withEmission)
        {
            string matPath = $"{Fab3dMatDir}/Mat_Fab3D_{slug}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_albedo.png");
            var met = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_metallicsmooth.png");
            if (alb != null) mat.SetTexture("_BaseMap", alb);
            if (met != null) { mat.SetTexture("_MetallicGlossMap", met); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            mat.SetFloat("_Smoothness", withEmission ? 0.58f : 0.38f);
            if (withEmission)
            {
                var emi = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Fab3dMatDir}/{slug}_emission.png");
                if (emi != null) { mat.SetTexture("_EmissionMap", emi); mat.SetColor("_EmissionColor", new Color(0.25f, 1.35f, 1.85f) * 2.0f); mat.EnableKeyword("_EMISSION"); }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateNpcPrefab(string slug, string fbxPath, Avatar avatar, Material mat, AnimatorController ctrl)
        {
            string prefabPath = $"{PrefabDir}/{slug}_humanoid.prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.avatar = avatar; anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
            var procT = Type.GetType("Studio.Game.Animation.DezProceduralAnimator, Assembly-CSharp");
            if (procT != null && root.GetComponent(procT) == null) root.AddComponent(procT);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void PlaceNormalizedProp(Transform parent, string name, string fbxPath, Material mat, Vector3 pos, Quaternion rot, float targetHeight)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, container.transform);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            NormalizeHeight(inst, targetHeight);
            var rs = inst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                var bc = container.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                bc.size = new Vector3(Mathf.Max(1.2f, b.size.x * 0.8f), b.size.y, Mathf.Max(1.2f, b.size.z * 0.8f));
            }
            container.transform.position = pos;
            container.transform.rotation = rot;
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            go.transform.localScale *= (targetHeight / Mathf.Max(0.01f, b.size.y));
        }
    }
}
