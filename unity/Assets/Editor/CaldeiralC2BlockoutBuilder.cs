using System;
using System.Collections.Generic;
using System.IO;
using Studio.Gameplay;
using Studio.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Studio.Editor
{
    /// <summary>
    /// CaldeiralC2BlockoutBuilder — Construtor deterministico do Greybox Fechado C2
    /// seguindo docs/level-design/caldeiral_c1.md (zona por zona: Z1 -> Z5).
    /// Regras inegociaveis:
    ///   - Zero GameObject.CreatePrimitive (usa ProceduralMeshLibrary + malhas world-scale UV 1 tile = 1 m)
    ///   - Espaco 100% fechado (piso + paredes + teto; zero void)
    ///   - Batching estatico em toda arquitetura
    ///   - <= 5 luzes reais simultaneas (budget Android URP)
    ///   - Captura datada por zona salva em evidence/captures/ e director_review/
    /// </summary>
    public static class CaldeiralC2BlockoutBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Caldeiral.unity";
        private const string GreyboxMeshDir = "Assets/Game/Art/Meshes/Greybox";
        private const string GreyboxMatDir = "Assets/Game/Materials/Greybox";
        private const string ProtoTexDir = "Assets/Game/Art/Textures/Prototype";
        private const string PlaceholderDir = "Assets/Game/Art/Greybox/Placeholders";

        private static Material _matFloor;
        private static Material _matWall;
        private static Material _matCeiling;
        private static Material _matAccent;
        private static Material _matPlayer;
        private static Material _matEnemy;

        public static void BuildZ1AndCaptureBatch()
        {
            try
            {
                BuildZ1OficinaSucata(closeZ1EndBulkhead: true, captureZ1: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z1: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildZ1AndZ2AndCaptureBatch()
        {
            try
            {
                BuildZ1OficinaSucata(closeZ1EndBulkhead: false, captureZ1: false);
                BuildZ2PassarelaBigorna();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z1+Z2: {ex}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Studio/Concerto/C2 Blockout/Build Z1 - Oficina da Sucata")]
        public static void BuildZ1OficinaSucataMenu()
        {
            BuildZ1OficinaSucata(closeZ1EndBulkhead: true, captureZ1: true);
        }

        public static void BuildZ1OficinaSucata(bool closeZ1EndBulkhead = true, bool captureZ1 = true)
        {
            EnsureFolder("Assets/Game/Art/Meshes");
            EnsureFolder(GreyboxMeshDir);
            EnsureFolder("Assets/Game/Materials");
            EnsureFolder(GreyboxMatDir);
            EnsureFolder("Assets/Game/Scenes");
            AssetDatabase.Refresh();

            _matFloor = EnsureProtoMaterial("Mat_Proto_Floor_1m", $"{ProtoTexDir}/proto_grid_floor_1m.png", 0.08f, 0.28f);
            _matWall = EnsureProtoMaterial("Mat_Proto_Wall_1m", $"{ProtoTexDir}/proto_grid_wall_1m.png", 0.05f, 0.22f);
            _matCeiling = EnsureProtoMaterial("Mat_Proto_Ceiling_1m", $"{ProtoTexDir}/proto_grid_ceiling_1m.png", 0.05f, 0.15f);
            _matAccent = EnsureProtoMaterial("Mat_Proto_Accent_1m", $"{ProtoTexDir}/proto_grid_accent_1m.png", 0.35f, 0.55f);
            _matPlayer = EnsureProtoMaterial("Mat_Proto_Player_1m", $"{ProtoTexDir}/proto_grid_player_1m.png", 0.15f, 0.45f);
            _matEnemy = EnsureProtoMaterial("Mat_Proto_Enemy_1m", $"{ProtoTexDir}/proto_grid_wall_1m.png", 0.20f, 0.35f, new Color(0.92f, 0.42f, 0.34f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.18f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.14f, 0.12f);
            RenderSettings.fogDensity = 0.012f;

            // === LIGHTING (Budget Android URP <= 5 real lights; Z1+Z2 usa 4/5 luzes) ===
            var lightsRoot = new GameObject("Lighting").transform;
            CreateDirectionalLight(lightsRoot, "L0_Directional_Desync", Quaternion.Euler(52f, -35f, 0f), new Color(0.75f, 0.68f, 0.60f), 0.75f);
            CreatePointLight(lightsRoot, "L1_Oficina_Warm", new Vector3(-29.0f, 5.2f, -18.0f), new Color(1.0f, 0.74f, 0.42f), 18f, 14f);
            if (closeZ1EndBulkhead)
            {
                CreatePointLight(lightsRoot, "L2_Arco_Guide", new Vector3(-20.0f, 4.2f, -18.0f), new Color(0.78f, 0.88f, 0.96f), 12f, 12f);
            }

            // === Z1: OFICINA DA SUCATA (14m x 12m, X: -36..-22, Z: -24..-12, Teto Y=8.0m, 100% FECHADO) ===
            var z1Root = new GameObject("Z1_Oficina_da_Sucata").transform;

            CreateWorldBox("Z1_Floor_14x12", z1Root, new Vector3(-29f, -0.25f, -18f), new Vector3(14f, 0.5f, 12f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Ceiling_14x12", z1Root, new Vector3(-29f, 8.25f, -18f), new Vector3(14f, 0.5f, 12f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z1_CeilingBeam_A", z1Root, new Vector3(-32.5f, 7.65f, -18f), new Vector3(0.6f, 0.7f, 12f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z1_CeilingBeam_B", z1Root, new Vector3(-25.5f, 7.65f, -18f), new Vector3(0.6f, 0.7f, 12f), _matCeiling, isStatic: true, addCollider: false);

            CreateWorldBox("Z1_Wall_West", z1Root, new Vector3(-36.25f, 4f, -18f), new Vector3(0.5f, 8f, 12f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_North", z1Root, new Vector3(-29f, 4f, -11.75f), new Vector3(14f, 8f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_South", z1Root, new Vector3(-29f, 4f, -24.25f), new Vector3(14f, 8f, 0.5f), _matWall, isStatic: true, addCollider: true);

            CreateWorldBox("Z1_Wall_East_South", z1Root, new Vector3(-21.75f, 4f, -22f), new Vector3(0.5f, 8f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_East_North", z1Root, new Vector3(-21.75f, 4f, -14f), new Vector3(0.5f, 8f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_East_Header", z1Root, new Vector3(-21.75f, 6.25f, -18f), new Vector3(0.5f, 3.5f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Arch_Pillar_S", z1Root, new Vector3(-22.0f, 2.25f, -20.15f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Arch_Pillar_N", z1Root, new Vector3(-22.0f, 2.25f, -15.85f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Arch_Lintel", z1Root, new Vector3(-22.0f, 4.7f, -18f), new Vector3(0.7f, 0.45f, 4.7f), _matAccent, isStatic: true, addCollider: true);

            CreateWorldBox("Z1_ArchTunnel_Floor", z1Root, new Vector3(-20f, -0.25f, -18f), new Vector3(4f, 0.5f, 4f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_ArchTunnel_Ceiling", z1Root, new Vector3(-20f, 4.75f, -18f), new Vector3(4f, 0.5f, 4f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z1_ArchTunnel_WallS", z1Root, new Vector3(-20f, 2.25f, -20.25f), new Vector3(4f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_ArchTunnel_WallN", z1Root, new Vector3(-20f, 2.25f, -15.75f), new Vector3(4f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            if (closeZ1EndBulkhead)
            {
                CreateWorldBox("Z1_ArchTunnel_EndBulkhead", z1Root, new Vector3(-17.75f, 2.25f, -18f), new Vector3(0.5f, 4.5f, 4.5f), _matWall, isStatic: true, addCollider: true);
            }

            // Props Z1
            var propsRoot = new GameObject("Z1_Props_Placeholders").transform;
            propsRoot.SetParent(z1Root);
            CreateWorldBox("Z1_Crate_NW_1", propsRoot, new Vector3(-34.6f, 0.55f, -13.2f), new Vector3(1.1f, 1.1f, 1.1f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_NW_2", propsRoot, new Vector3(-33.3f, 0.45f, -13.0f), new Vector3(0.9f, 0.9f, 0.9f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Shelf_North_1", propsRoot, new Vector3(-29.0f, 1.2f, -12.6f), new Vector3(3.2f, 2.4f, 0.9f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_NE_1", propsRoot, new Vector3(-24.2f, 0.6f, -13.3f), new Vector3(1.2f, 1.2f, 1.2f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_SW_1", propsRoot, new Vector3(-34.5f, 0.5f, -22.8f), new Vector3(1.2f, 1.0f, 1.0f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_SE_1", propsRoot, new Vector3(-24.0f, 0.5f, -22.6f), new Vector3(1.4f, 1.0f, 1.0f), _matWall, isStatic: true, addCollider: true);

            // I1: Bancada de Relogio
            var bancada = new GameObject("I1_Bancada_Relogio");
            bancada.transform.SetParent(z1Root);
            bancada.transform.position = new Vector3(-29.0f, 0.0f, -17.5f);
            CreateWorldBox("Bancada_Base", bancada.transform, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 1.1f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Bancada_Tampo", bancada.transform, new Vector3(0f, 0.96f, 0f), new Vector3(2.6f, 0.12f, 1.25f), _matAccent, isStatic: true, addCollider: false);
            var ikTarget = new GameObject("IkHandTarget");
            ikTarget.transform.SetParent(bancada.transform);
            ikTarget.transform.localPosition = new Vector3(0f, 1.08f, -0.35f);
            var interactable = bancada.AddComponent<InteractableBase>();
            var soInteract = new SerializedObject(interactable);
            soInteract.FindProperty("promptText").stringValue = "Examinar Bancada [E/B]";
            soInteract.FindProperty("ikHandTarget").objectReferenceValue = ikTarget.transform;
            soInteract.ApplyModifiedPropertiesWithoutUndo();

            // Entidades Z1 (instanciadas a partir dos FBX A-pose validados pelo validate_visual)
            var entitiesRoot = new GameObject("Entities_Placeholders").transform;
            var dez = InstantiatePlaceholderFbx("dez_placeholder", "Player_Dez_Placeholder", entitiesRoot, new Vector3(-33.0f, 0.0f, -20.0f), Quaternion.Euler(0f, 65f, 0f), _matPlayer);
            dez.AddComponent<PlayerInteractor>();
            dez.AddComponent<DialoguePresenter>();
            InstantiatePlaceholderFbx("porca_placeholder", "Companion_Porca_Placeholder", entitiesRoot, new Vector3(-31.8f, 1.35f, -19.0f), Quaternion.Euler(0f, 60f, 0f), _matAccent);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.10f, 0.09f);
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 120f;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
            camGo.transform.LookAt(new Vector3(-27.5f, 1.3f, -17.4f));

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (captureZ1)
            {
                CaptureZoneScreenshot(
                    cam,
                    "c2_z1_oficina_sucata",
                    "O QUE É: Captura do blockout greybox C2 da Zona Z1 (Oficina da Sucata — 14m x 12m, teto 8m, grid 1 tile = 1m, Dez + Porca placeholder, Bancada I1 e Arco 4m).\n" +
                    "O QUE VOCÊ DECIDE: Aprovar escala, proporção, fechamento espacial (zero void) e leitura da Z1 antes de construir a Zona Z2 (Passarela da Bigorna).\n" +
                    "O QUE ACONTECE DEPOIS: Com seu OK, avançamos para o blockout C2 da Z2 mantendo o baseline PlayMode e validate_scene.py verdes.\n"
                );
            }
        }

        /// <summary>
        /// Constrói Z2: Passarela da Bigorna (18m x 14m, X: -18..0, Z: -25..-11, teto 10m, 100% fechada).
        /// Rampa industrial <= 18 deg (16.9 deg) ate Mezanino +3.5m, Tio Horario + InteractableBase,
        /// E1/E2 A-pose nao-bloqueantes e Arco Leste 4m apontando para Z3.
        /// </summary>
        public static void BuildZ2PassarelaBigorna()
        {
            var lightsRoot = GameObject.Find("Lighting")?.transform ?? new GameObject("Lighting").transform;
            // Luzes Z2: total acumulado na cena = 4/5 luzes reais (dentro do limite Android URP)
            CreatePointLight(lightsRoot, "L2_Z2_Passarela_Key", new Vector3(-9.0f, 7.2f, -18.0f), new Color(0.95f, 0.82f, 0.68f), 24f, 22f);
            CreatePointLight(lightsRoot, "L3_Z2_Arco_Z3_Guide", new Vector3(2.2f, 4.2f, -18.0f), new Color(0.62f, 0.90f, 0.96f), 14f, 16f);

            var z2Root = new GameObject("Z2_Passarela_da_Bigorna").transform;

            // Piso 18m x 14m (topo em Y=0.0) e Teto 18m x 14m (base em Y=10.0)
            CreateWorldBox("Z2_Floor_18x14", z2Root, new Vector3(-9f, -0.25f, -18f), new Vector3(18f, 0.5f, 14f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Ceiling_18x14", z2Root, new Vector3(-9f, 10.25f, -18f), new Vector3(18f, 0.5f, 14f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z2_CeilingBeam_A", z2Root, new Vector3(-13.5f, 9.6f, -18f), new Vector3(0.7f, 0.8f, 14f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z2_CeilingBeam_B", z2Root, new Vector3(-4.5f, 9.6f, -18f), new Vector3(0.7f, 0.8f, 14f), _matCeiling, isStatic: true, addCollider: false);

            // Parede Norte (Z=-11) e Parede Sul (Z=-25) fechadas (H=10m)
            CreateWorldBox("Z2_Wall_North", z2Root, new Vector3(-9f, 5f, -10.75f), new Vector3(18f, 10f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Wall_South", z2Root, new Vector3(-9f, 5f, -25.25f), new Vector3(18f, 10f, 0.5f), _matWall, isStatic: true, addCollider: true);

            // Parede Oeste (X=-18) aberta apenas no Arco Z1->Z2 (Z: -20..-16, H: 0..4.5m)
            CreateWorldBox("Z2_Wall_West_South", z2Root, new Vector3(-18.25f, 5f, -22.5f), new Vector3(0.5f, 10f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Wall_West_North", z2Root, new Vector3(-18.25f, 5f, -13.5f), new Vector3(0.5f, 10f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Wall_West_Header", z2Root, new Vector3(-18.25f, 7.25f, -18f), new Vector3(0.5f, 5.5f, 4f), _matWall, isStatic: true, addCollider: true);

            // Parede Leste (X=0) aberta no Arco Z2->Z3 (Z: -20..-16, H: 0..4.5m) -> composicao apontando para Z3
            CreateWorldBox("Z2_Wall_East_South", z2Root, new Vector3(0.25f, 5f, -22.5f), new Vector3(0.5f, 10f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Wall_East_North", z2Root, new Vector3(0.25f, 5f, -13.5f), new Vector3(0.5f, 10f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Wall_East_Header", z2Root, new Vector3(0.25f, 7.25f, -18f), new Vector3(0.5f, 5.5f, 4f), _matWall, isStatic: true, addCollider: true);
            // Moldura ambar do Arco Z2->Z3
            CreateWorldBox("Z2_ArchZ3_Pillar_S", z2Root, new Vector3(0.0f, 2.25f, -20.15f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_ArchZ3_Pillar_N", z2Root, new Vector3(0.0f, 2.25f, -15.85f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_ArchZ3_Lintel", z2Root, new Vector3(0.0f, 4.7f, -18f), new Vector3(0.7f, 0.45f, 4.7f), _matAccent, isStatic: true, addCollider: true);

            // Tunel provisorio Z2->Z3 (X: 0..6, Z: -20..-16) fechado ao fundo (zero void!)
            CreateWorldBox("Z2_TunnelZ3_Floor", z2Root, new Vector3(3f, -0.25f, -18f), new Vector3(6f, 0.5f, 4f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_TunnelZ3_Ceiling", z2Root, new Vector3(3f, 4.75f, -18f), new Vector3(6f, 0.5f, 4f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z2_TunnelZ3_WallS", z2Root, new Vector3(3f, 2.25f, -20.25f), new Vector3(6f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_TunnelZ3_WallN", z2Root, new Vector3(3f, 2.25f, -15.75f), new Vector3(6f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_TunnelZ3_Bulkhead", z2Root, new Vector3(6.25f, 2.25f, -18f), new Vector3(0.5f, 4.5f, 4.5f), _matWall, isStatic: true, addCollider: true);

            // === MEZANINO +3.5m (X: -6.0..0.0, Z: -14.5..-11.0, topo em Y=+3.5m) + RAMPA <= 18 deg (16.9 deg) ===
            CreateWorldBox("Z2_Mezzanine_Deck", z2Root, new Vector3(-3.0f, 3.3f, -12.75f), new Vector3(6.0f, 0.4f, 3.5f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Mezzanine_Pillar_W", z2Root, new Vector3(-5.7f, 1.55f, -14.2f), new Vector3(0.5f, 3.1f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Mezzanine_Pillar_E", z2Root, new Vector3(-0.6f, 1.55f, -14.2f), new Vector3(0.5f, 3.1f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_Mezzanine_Rail", z2Root, new Vector3(-3.0f, 4.0f, -14.4f), new Vector3(6.0f, 1.0f, 0.16f), _matAccent, isStatic: true, addCollider: true);

            // Rampa transitavel a pe: sobe de X=-17.5 (Y=0.0) ate X=-6.0 (Y=3.5) -> run=11.5m, rise=3.5m, angulo=16.93 deg <= 18 deg
            float run = 11.5f, rise = 3.5f;
            float rampLen = Mathf.Sqrt(run * run + rise * rise);
            float rampAngleDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg; // 16.93 deg
            var rampGo = CreateWorldBox("Z2_Mezzanine_Ramp_16_9deg", z2Root, new Vector3(-11.75f, 1.60f, -12.6f), new Vector3(rampLen, 0.30f, 2.8f), _matFloor, isStatic: true, addCollider: true);
            rampGo.transform.localRotation = Quaternion.Euler(0f, 0f, rampAngleDeg);
            var rampRail = CreateWorldBox("Z2_Mezzanine_Ramp_Rail", z2Root, new Vector3(-11.75f, 2.15f, -13.95f), new Vector3(rampLen, 0.85f, 0.14f), _matAccent, isStatic: true, addCollider: false);
            rampRail.transform.localRotation = Quaternion.Euler(0f, 0f, rampAngleDeg);

            // Pilares de cobertura da Passarela da Bigorna (guiam o olhar ao Arco Z3 sem obstruir o corredor central)
            CreateWorldBox("Z2_CoverPillar_NW", z2Root, new Vector3(-11.5f, 1.6f, -15.6f), new Vector3(1.0f, 3.2f, 1.0f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z2_CoverPillar_SE", z2Root, new Vector3(-5.0f, 1.6f, -21.2f), new Vector3(1.0f, 3.2f, 1.0f), _matWall, isStatic: true, addCollider: true);

            // === NPC TIO HORARIO + BANCA + ANCORA DE DIALOGO (InteractableBase em nicho Sul-Oeste) ===
            var tioBooth = new GameObject("NPC_Tio_Horario_Anchor");
            tioBooth.transform.SetParent(z2Root);
            tioBooth.transform.position = new Vector3(-13.8f, 0.0f, -21.6f);
            CreateWorldBox("Tio_Banca_Base", tioBooth.transform, new Vector3(0.0f, 0.5f, 0.8f), new Vector3(2.2f, 1.0f, 0.9f), _matAccent, isStatic: true, addCollider: true);
            var tioIk = new GameObject("DialogueAnchor");
            tioIk.transform.SetParent(tioBooth.transform);
            tioIk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var tioInteract = tioBooth.AddComponent<InteractableBase>();
            var soTio = new SerializedObject(tioInteract);
            soTio.FindProperty("promptText").stringValue = "Conversar com Tio Horário [E/B]";
            soTio.FindProperty("ikHandTarget").objectReferenceValue = tioIk.transform;
            soTio.ApplyModifiedPropertiesWithoutUndo();

            var entitiesRoot = GameObject.Find("Entities_Placeholders")?.transform ?? new GameObject("Entities_Placeholders").transform;
            InstantiatePlaceholderFbx("tio_horario_placeholder", "NPC_Tio_Horario_Placeholder", entitiesRoot, new Vector3(-13.8f, 0.0f, -22.2f), Quaternion.Euler(0f, 20f, 0f), _matAccent);

            // E1 & E2: Inimigos em A-pose simetrica, NAO-BLOQUEANTES (fora da passagem central Z=-18)
            InstantiatePlaceholderFbx("ferrugem_trabalhadora_placeholder", "E1_Ferrugem_Trabalhadora_Placeholder", entitiesRoot, new Vector3(-8.5f, 0.0f, -21.8f), Quaternion.Euler(0f, -35f, 0f), _matEnemy);
            InstantiatePlaceholderFbx("ferrugem_espelho_placeholder", "E2_Ferrugem_Espelho_Placeholder", entitiesRoot, new Vector3(-3.0f, 3.5f, -12.8f), Quaternion.Euler(0f, -135f, 0f), _matEnemy);

            // Camera de composicao Z2 (enquadra Tio Horario + banca, rampa 16.9 deg, Mezanino +3.5m com E2, E1 em pe e Arco Z3!)
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            cam.transform.position = new Vector3(-17.4f, 3.8f, -24.4f);
            cam.transform.LookAt(new Vector3(-5.5f, 1.9f, -17.0f));

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            CaptureZoneScreenshot(
                cam,
                "c2_z2_passarela_bigorna",
                "O QUE É: Captura do blockout greybox C2 da Zona Z2 (Passarela da Bigorna — 18m x 14m, teto 10m, rampa 16.9° até Mezanino +3.5m, Tio Horário + banca, E1/E2 A-pose não-bloqueantes e Arco Z3).\n" +
                "O QUE VOCÊ DECIDE: Aprovar escala, inclinação da rampa (<=18°), posicionamento dos placeholders A-pose e composição guiando o olhar para Z3.\n" +
                "O QUE ACONTECE DEPOIS: Com seu OK, avançamos para o blockout C2 da Zona Z3 (Pátio das Sirenes + Galeria de Fumaça) mantendo todos os gates verdes.\n"
            );
            Debug.Log("[CaldeiralC2] Z2 Passarela da Bigorna construida, salva e capturada com sucesso.");
        }

        private static GameObject InstantiatePlaceholderFbx(string assetName, string instanceName, Transform parent, Vector3 pos, Quaternion rot, Material mat)
        {
            string fbxPath = $"{PlaceholderDir}/{assetName}/{assetName}.fbx";
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp != null && imp.bakeAxisConversion)
            {
                imp.bakeAxisConversion = false;
                imp.SaveAndReimport();
            }
            var container = new GameObject(instanceName);
            container.transform.SetParent(parent, false);
            container.transform.position = pos;
            container.transform.rotation = rot;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab != null)
            {
                // Instancia como filho preservando o localRotation (-90 X) nativo do FBX exportado pelo Blender!
                var model = (GameObject)UnityEngine.Object.Instantiate(prefab, container.transform);
                model.name = "Model";
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                }
            }
            else
            {
                Debug.LogWarning($"[CaldeiralC2] FBX nao encontrado em {fbxPath}, usando fallback procedural.");
                ProceduralMeshLibrary.CreatePart("Model", ProceduralMeshLibrary.Capsule, container.transform, new Vector3(0f, 0.8f, 0f), Quaternion.identity, new Vector3(0.5f, 0.8f, 0.5f), mat);
            }
            return container;
        }

        private static void CaptureZoneScreenshot(Camera cam, string zoneSlug, string readmeText)
        {
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            byte[] pngBytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string dateOnly = DateTime.Now.ToString("yyyy-MM-dd");

            string capDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(capDir);
            string datedPath = Path.Combine(capDir, $"{zoneSlug}_{stamp}.png");
            string latestPath = Path.Combine(capDir, $"{zoneSlug}.png");
            File.WriteAllBytes(datedPath, pngBytes);
            File.WriteAllBytes(latestPath, pngBytes);

            string reviewDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "director_review", $"{dateOnly}_{zoneSlug}");
            Directory.CreateDirectory(reviewDir);
            string reviewImg = Path.Combine(reviewDir, $"{zoneSlug}_{stamp}.png");
            File.WriteAllBytes(reviewImg, pngBytes);
            File.WriteAllText(Path.Combine(reviewDir, "LEIA-ME.txt"), readmeText);

            Debug.Log($"[CaldeiralC2] Captura salva em: {datedPath} e {reviewImg}");
        }

        private static GameObject CreateWorldBox(string name, Transform parent, Vector3 localPos, Vector3 sizeMeters, Material mat, bool isStatic, bool addCollider)
        {
            Mesh mesh = GetOrCreateWorldUvBoxMesh(sizeMeters);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = sizeMeters;
            }
            if (isStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            }
            return go;
        }

        private static GameObject CreatePartScaled(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            return ProceduralMeshLibrary.CreatePart(name, mesh, parent, localPos, Quaternion.identity, localScale, mat);
        }

        private static Mesh GetOrCreateWorldUvBoxMesh(Vector3 size)
        {
            string key = $"SM_GB_Box_{size.x:F2}x{size.y:F2}x{size.z:F2}".Replace('.', '_');
            string assetPath = $"{GreyboxMeshDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null) return existing;

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float sx = size.x, sy = size.y, sz = size.z;

            Vector3[] verts =
            {
                // Front (-Z)
                new Vector3(-hx,-hy,-hz), new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx,-hy,-hz),
                // Back (+Z)
                new Vector3( hx,-hy, hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx,-hy, hz),
                // Left (-X)
                new Vector3(-hx,-hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy,-hz), new Vector3(-hx,-hy,-hz),
                // Right (+X)
                new Vector3( hx,-hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3( hx,-hy, hz),
                // Top (+Y)
                new Vector3(-hx, hy,-hz), new Vector3(-hx, hy, hz), new Vector3( hx, hy, hz), new Vector3( hx, hy,-hz),
                // Bottom (-Y)
                new Vector3(-hx,-hy, hz), new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz),
            };
            Vector3[] normals =
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            };
            // Exact 1 tile = 1 meter UVs on every face!
            Vector2[] uvs =
            {
                new Vector2(0,0), new Vector2(0,sy), new Vector2(sx,sy), new Vector2(sx,0),
                new Vector2(0,0), new Vector2(0,sy), new Vector2(sx,sy), new Vector2(sx,0),
                new Vector2(0,0), new Vector2(0,sy), new Vector2(sz,sy), new Vector2(sz,0),
                new Vector2(0,0), new Vector2(0,sy), new Vector2(sz,sy), new Vector2(sz,0),
                new Vector2(0,0), new Vector2(0,sz), new Vector2(sx,sz), new Vector2(sx,0),
                new Vector2(0,0), new Vector2(0,sz), new Vector2(sx,sz), new Vector2(sx,0),
            };
            int[] tris =
            {
                 0, 1, 2,  0, 2, 3,
                 4, 5, 6,  4, 6, 7,
                 8, 9,10,  8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
            };

            var m = new Mesh { name = key, vertices = verts, normals = normals, uv = uvs, triangles = tris };
            m.RecalculateBounds();
            m.RecalculateTangents();
            AssetDatabase.CreateAsset(m, assetPath);
            return m;
        }

        private static Material EnsureProtoMaterial(string matName, string texPath, float metallic, float smoothness, Color? tint = null)
        {
            var texImp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (texImp != null)
            {
                texImp.wrapMode = TextureWrapMode.Repeat;
                texImp.filterMode = FilterMode.Trilinear;
                texImp.anisoLevel = 8;
                texImp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = $"{GreyboxMatDir}/{matName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                existing.SetTexture("_BaseMap", tex);
                existing.SetColor("_BaseColor", tint ?? Color.white);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint ?? Color.white);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void CreateDirectionalLight(Transform parent, string name, Quaternion rot, Color col, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = col;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            go.AddComponent<UniversalAdditionalLightData>();
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 pos, Color col, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<UniversalAdditionalLightData>();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
