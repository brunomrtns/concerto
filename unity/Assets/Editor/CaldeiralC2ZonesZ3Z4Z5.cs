using System;
using System.IO;
using Studio.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    public static partial class CaldeiralC2BlockoutBuilder
    {
        public static void BuildUpToZ3Batch()
        {
            try
            {
                BuildZ1OficinaSucata(closeZ1EndBulkhead: false, captureZ1: false);
                BuildZ2PassarelaBigorna(closeZ2EndBulkhead: false, captureZ2: false, addZ2GuideLight: false);
                BuildZ3PatioSirenes(closeZ3EndBulkhead: true, captureZ3: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z3: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildUpToZ4Batch()
        {
            try
            {
                BuildZ1OficinaSucata(closeZ1EndBulkhead: false, captureZ1: false);
                BuildZ2PassarelaBigorna(closeZ2EndBulkhead: false, captureZ2: false, addZ2GuideLight: false);
                BuildZ3PatioSirenes(closeZ3EndBulkhead: false, captureZ3: false);
                BuildZ4CamaraCaldeiral(closeZ4WestBulkhead: true, captureZ4: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z4: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFullC2LoopAndWalkthroughBatch()
        {
            try
            {
                BuildZ1OficinaSucata(closeZ1EndBulkhead: false, captureZ1: false);
                BuildZ2PassarelaBigorna(closeZ2EndBulkhead: false, captureZ2: false, addZ2GuideLight: false);
                BuildZ3PatioSirenes(closeZ3EndBulkhead: false, captureZ3: false);
                BuildZ4CamaraCaldeiral(closeZ4WestBulkhead: false, captureZ4: false);
                BuildZ5GaleriaRetorno(captureZ5: true);
                CaptureWalkthroughFrames();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z5+Walkthrough: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Z3: Patio das Sirenes (24m x 14m, X: 6..30, Z: -24..-10) + Galeria de Fumaca (14m x 22m, X: 16..30, Z: -10..+12).
        /// Teto 14m, 100% fechado. Aplica Deltas Z3-A (passarela lateral 4m ao longo dos paredoes 8m),
        /// Z3-B (ponte transitavel sobre o fosso de vapor 6.5m) e VERT-A (rampa 16.9 deg ate Vo Segunda +3.5m).
        /// </summary>
        public static void BuildZ3PatioSirenes(bool closeZ3EndBulkhead = true, bool captureZ3 = true)
        {
            var lightsRoot = GameObject.Find("Lighting")?.transform ?? new GameObject("Lighting").transform;
            // Luz 4/5 do orcamento Android URP (cobre Patio + Galeria de Fumaca)
            CreatePointLight(lightsRoot, "L3_Z3_Patio_Sirenes", new Vector3(22.0f, 10.5f, -6.0f), new Color(0.66f, 0.90f, 0.96f), 32f, 28f);

            var z3Root = new GameObject("Z3_Patio_das_Sirenes").transform;

            // Piso Sul Patio (24m x 14m, X: 6..30, Z: -24..-10) + Teto 14m
            CreateWorldBox("Z3_Patio_Floor", z3Root, new Vector3(18f, -0.25f, -17f), new Vector3(24f, 0.5f, 14f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Patio_Ceiling", z3Root, new Vector3(18f, 14.25f, -17f), new Vector3(24f, 0.5f, 14f), _matCeiling, isStatic: true, addCollider: false);
            // Paredes Patio Sul
            CreateWorldBox("Z3_Patio_Wall_South", z3Root, new Vector3(18f, 7f, -24.25f), new Vector3(24f, 14f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Patio_Wall_North_W", z3Root, new Vector3(11f, 7f, -9.75f), new Vector3(10f, 14f, 0.5f), _matWall, isStatic: true, addCollider: true);
            // Parede Oeste Patio aberta apenas no tunel Z2->Z3 (Z: -20..-16, H: 0..4.5)
            CreateWorldBox("Z3_Patio_Wall_West_S", z3Root, new Vector3(5.75f, 7f, -22f), new Vector3(0.5f, 14f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Patio_Wall_West_N", z3Root, new Vector3(5.75f, 7f, -13f), new Vector3(0.5f, 14f, 6f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Patio_Wall_West_Header", z3Root, new Vector3(5.75f, 9.25f, -18f), new Vector3(0.5f, 9.5f, 4f), _matWall, isStatic: true, addCollider: true);

            // Galeria Norte de Fumaca (14m x 22m, X: 16..30, Z: -10..+12, teto 14m)
            // Piso sul da galeria (Z: -10..+2.5), fosso de vapor rebaixado (Z: +2.5..+9.0 em Y=-2.5m), e plataforma norte (Z: +9.0..+12)
            CreateWorldBox("Z3_Galeria_Floor_South", z3Root, new Vector3(23f, -0.25f, -3.75f), new Vector3(14f, 0.5f, 12.5f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_VaporChasm_Bottom", z3Root, new Vector3(23f, -2.75f, 5.75f), new Vector3(14f, 0.5f, 6.5f), _matCeiling, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Floor_North", z3Root, new Vector3(23f, -0.25f, 10.5f), new Vector3(14f, 0.5f, 3.0f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Ceiling", z3Root, new Vector3(23f, 14.25f, 1f), new Vector3(14f, 0.5f, 22f), _matCeiling, isStatic: true, addCollider: false);

            // Paredes Galeria Norte (Leste contínua X=30 de Z=-24..+12; Norte Z=+12; Oeste X=16 aberta na ponte Z3->Z4 em Z=6..11, Y=3.5..8.0)
            CreateWorldBox("Z3_Wall_East_Full", z3Root, new Vector3(30.25f, 7f, -6f), new Vector3(0.5f, 14f, 36f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Wall_North_End", z3Root, new Vector3(23f, 7f, 12.25f), new Vector3(14f, 14f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Wall_West_S", z3Root, new Vector3(15.75f, 7f, -2f), new Vector3(0.5f, 14f, 16f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Wall_West_BelowBridge", z3Root, new Vector3(15.75f, 1.75f, 8.5f), new Vector3(0.5f, 3.5f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Wall_West_AboveBridge", z3Root, new Vector3(15.75f, 11.0f, 8.5f), new Vector3(0.5f, 6.0f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Galeria_Wall_West_N", z3Root, new Vector3(15.75f, 7f, 11.5f), new Vector3(0.5f, 14f, 1f), _matWall, isStatic: true, addCollider: true);

            // DELTA VERT-A: Rampa 16.9 deg (run=11.5m, rise=3.5m) subindo de X=7.5 ate Patamar +3.5m da Vo Segunda (X: 19..29.5, Z: -23.5..-16.5)
            float run = 11.5f, rise = 3.5f;
            float rampLen = Mathf.Sqrt(run * run + rise * rise);
            float rampAng = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var z3Ramp = CreateWorldBox("Z3_VoSegunda_Ramp_16_9deg", z3Root, new Vector3(13.25f, 1.60f, -22.0f), new Vector3(rampLen, 0.30f, 3.0f), _matFloor, isStatic: true, addCollider: true);
            z3Ramp.transform.localRotation = Quaternion.Euler(0f, 0f, rampAng);

            // Patamar +3.5m da Vo Segunda
            CreateWorldBox("Z3_VoSegunda_Deck", z3Root, new Vector3(24.25f, 3.30f, -20.0f), new Vector3(10.5f, 0.40f, 7.0f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_VoSegunda_Rail", z3Root, new Vector3(19.1f, 4.0f, -18.5f), new Vector3(0.16f, 1.0f, 4.0f), _matAccent, isStatic: true, addCollider: true);

            // DELTA Z3-A: Passarela lateral transitavel a pe (largura 4m, Y=+3.5m) ao longo dos paredoes de wall-run (8m)
            CreateWorldBox("Z3_Walkway_Lateral_4m", z3Root, new Vector3(26.5f, 3.30f, -7.0f), new Vector3(4.0f, 0.40f, 19.0f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_Walkway_Rail_W", z3Root, new Vector3(24.6f, 4.0f, -7.0f), new Vector3(0.16f, 1.0f, 19.0f), _matAccent, isStatic: true, addCollider: false);
            // Paredoes de wall-run (8m altura, destaque azul/player na parede Leste e Oeste como arquitetura pronta p/ futuro wall-run)
            CreateWorldBox("Z3_WallRun_Panel_East", z3Root, new Vector3(29.85f, 6.5f, -4.0f), new Vector3(0.3f, 8.0f, 12.0f), _matPlayer, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_WallRun_Panel_West", z3Root, new Vector3(16.15f, 6.5f, 2.0f), new Vector3(0.3f, 8.0f, 12.0f), _matPlayer, isStatic: true, addCollider: true);

            // DELTA Z3-B: Ponte industrial transitavel (4m) cruzando o fosso de vapor 6.5m (Z: +2.5..+9.0) em Y=+3.5m -> Landing Norte
            CreateWorldBox("Z3_VaporBridge_4m", z3Root, new Vector3(26.5f, 3.30f, 5.75f), new Vector3(4.0f, 0.40f, 6.5f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_NorthLanding_Deck", z3Root, new Vector3(22.5f, 3.30f, 9.0f), new Vector3(13.0f, 0.40f, 4.0f), _matFloor, isStatic: true, addCollider: true);

            // Ponte Elevada Z3->Z4 (X: 11..16, Z: 6..11, Y=+3.5m) fechada em tunel
            CreateWorldBox("Z3_BridgeZ4_Floor", z3Root, new Vector3(13.5f, 3.30f, 8.5f), new Vector3(5f, 0.40f, 5f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_BridgeZ4_Ceiling", z3Root, new Vector3(13.5f, 8.25f, 8.5f), new Vector3(5f, 0.50f, 5f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z3_BridgeZ4_WallS", z3Root, new Vector3(13.5f, 5.75f, 5.75f), new Vector3(5f, 5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z3_BridgeZ4_WallN", z3Root, new Vector3(13.5f, 5.75f, 11.25f), new Vector3(5f, 5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            if (closeZ3EndBulkhead)
            {
                CreateWorldBox("Z3_BridgeZ4_Bulkhead", z3Root, new Vector3(10.75f, 5.75f, 8.5f), new Vector3(0.5f, 5f, 5.5f), _matWall, isStatic: true, addCollider: true);
            }

            // NPC Vo Segunda (+3.5m) + InteractableBase
            var voAnchor = new GameObject("NPC_Vo_Segunda_Anchor");
            voAnchor.transform.SetParent(z3Root);
            voAnchor.transform.position = new Vector3(22.0f, 3.5f, -18.5f);
            var voCol = voAnchor.AddComponent<BoxCollider>();
            voCol.size = new Vector3(1.2f, 1.8f, 1.2f);
            voCol.center = new Vector3(0f, 0.9f, 0f);
            var voInteract = voAnchor.AddComponent<InteractableBase>();
            var soVo = new SerializedObject(voInteract);
            soVo.FindProperty("promptText").stringValue = "Ouvir Vó Segunda [E/B]";
            soVo.ApplyModifiedPropertiesWithoutUndo();

            var entitiesRoot = GameObject.Find("Entities_Placeholders")?.transform ?? new GameObject("Entities_Placeholders").transform;
            InstantiatePlaceholderFbx("vo_segunda_placeholder", "NPC_Vo_Segunda_Placeholder", entitiesRoot, new Vector3(22.0f, 3.5f, -18.5f), Quaternion.Euler(0f, -45f, 0f), _matPlayer);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            if (captureZ3)
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                cam.transform.position = new Vector3(17.5f, 7.2f, -22.5f);
                cam.transform.LookAt(new Vector3(25.0f, 4.0f, -1.0f));
                CaptureZoneScreenshot(
                    cam,
                    "c2_z3_patio_sirenes",
                    "O QUE É: Captura do blockout greybox C2 da Zona Z3 (Pátio das Sirenes + Galeria de Fumaça — teto 14m, Vó Segunda no patamar +3.5m, passarela lateral 4m ao longo dos paredões 8m e ponte âmbar sobre o fosso de vapor 6.5m).\n" +
                    "O QUE VOCÊ DECIDE: Aprovar a legibilidade da travessia Z3 com os deltas mecânicos (passarela + ponte transitável a pé) e escala dos paredões de 8m.\n" +
                    "O QUE ACONTECE DEPOIS: Com seu OK no consolidado C2, Z3 segue para vestimenta modular CC0 + PBR no C3.\n"
                );
            }
            Debug.Log("[CaldeiralC2] Z3 Patio das Sirenes construida.");
        }

        /// <summary>
        /// Z4: Camara do Caldeiral Central (22m x 22m octogonal/chanfrada, X: -11..+11, Z: -4..+18, teto 18m).
        /// Focal Point Unico: Sirene-Mestra (H=16m) em (0,0,7), 3 Valvulas Ritmicas (I2,I3,I4) em Y=0,
        /// No de Re-Sincronia (I5) em (0,3.5,0.5), rampa <=18 deg conectando anel +3.5m ao piso 0m, e E3/E4 nao-bloqueantes.
        /// </summary>
        public static void BuildZ4CamaraCaldeiral(bool closeZ4WestBulkhead = true, bool captureZ4 = true)
        {
            var lightsRoot = GameObject.Find("Lighting")?.transform ?? new GameObject("Lighting").transform;
            // Luz 5/5 do orcamento Android URP (Zenital da Cupula 18m destacando o Focal Point)
            CreatePointLight(lightsRoot, "L4_Z4_SireneMestra_Zenital", new Vector3(0.0f, 16.5f, 7.0f), new Color(1.0f, 0.72f, 0.34f), 36f, 42f);

            var z4Root = new GameObject("Z4_Camara_Caldeiral_Central").transform;

            // Piso 22m x 22m (Y=0) e Teto Cupula 18m (100% fechado)
            CreateWorldBox("Z4_Floor_22x22", z4Root, new Vector3(0f, -0.25f, 7f), new Vector3(22f, 0.5f, 22f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Ceiling_22x22", z4Root, new Vector3(0f, 18.25f, 7f), new Vector3(22f, 0.5f, 22f), _matCeiling, isStatic: true, addCollider: false);

            // Paredes principais (Norte Z=18, Sul Z=-4, Leste X=11 aberta na ponte Z3->Z4 Z=6..11 Y=3.5..8.0, Oeste X=-11 aberta para Z5 Z=6..11 Y=3.5..8.0)
            CreateWorldBox("Z4_Wall_North", z4Root, new Vector3(0f, 9f, 18.25f), new Vector3(22f, 18f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_South", z4Root, new Vector3(0f, 9f, -4.25f), new Vector3(22f, 18f, 0.5f), _matWall, isStatic: true, addCollider: true);

            // Parede Leste X=11
            CreateWorldBox("Z4_Wall_East_S", z4Root, new Vector3(11.25f, 9f, 1f), new Vector3(0.5f, 18f, 10f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_East_N", z4Root, new Vector3(11.25f, 9f, 14.5f), new Vector3(0.5f, 18f, 7f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_East_Below", z4Root, new Vector3(11.25f, 1.75f, 8.5f), new Vector3(0.5f, 3.5f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_East_Above", z4Root, new Vector3(11.25f, 13.0f, 8.5f), new Vector3(0.5f, 10.0f, 5f), _matWall, isStatic: true, addCollider: true);

            // Parede Oeste X=-11
            CreateWorldBox("Z4_Wall_West_S", z4Root, new Vector3(-11.25f, 9f, 1f), new Vector3(0.5f, 18f, 10f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_West_N", z4Root, new Vector3(-11.25f, 9f, 14.5f), new Vector3(0.5f, 18f, 7f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_West_Below", z4Root, new Vector3(-11.25f, 1.75f, 8.5f), new Vector3(0.5f, 3.5f, 5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_Wall_West_Above", z4Root, new Vector3(-11.25f, 13.0f, 8.5f), new Vector3(0.5f, 10.0f, 5f), _matWall, isStatic: true, addCollider: true);
            if (closeZ4WestBulkhead)
            {
                CreateWorldBox("Z4_WestExit_Bulkhead", z4Root, new Vector3(-11.25f, 5.75f, 8.5f), new Vector3(0.5f, 4.5f, 5f), _matWall, isStatic: true, addCollider: true);
            }

            // Chanfros octogonais nos 4 cantos (quebram a caixa quadrada e criam silhueta octogonal de caldeira)
            Vector3[] cornerPos = { new Vector3(8.8f, 9f, 15.8f), new Vector3(-8.8f, 9f, 15.8f), new Vector3(8.8f, 9f, -1.8f), new Vector3(-8.8f, 9f, -1.8f) };
            float[] cornerYaw = { 45f, -45f, -45f, 45f };
            for (int i = 0; i < 4; i++)
            {
                var c = CreateWorldBox($"Z4_Octagon_Corner_{i}", z4Root, cornerPos[i], new Vector3(6.2f, 18f, 0.8f), _matWall, isStatic: true, addCollider: true);
                c.transform.localRotation = Quaternion.Euler(0f, cornerYaw[i], 0f);
            }

            // Anel Elevado +3.5m (Leste, Sul com No Re-Sincronia I5, e Oeste para Z5)
            CreateWorldBox("Z4_RingDeck_East", z4Root, new Vector3(9.0f, 3.30f, 4.0f), new Vector3(4.0f, 0.40f, 16.0f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_RingDeck_South", z4Root, new Vector3(0.0f, 3.30f, -1.2f), new Vector3(14.0f, 0.40f, 5.6f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z4_RingDeck_West", z4Root, new Vector3(-9.0f, 3.30f, 4.0f), new Vector3(4.0f, 0.40f, 16.0f), _matFloor, isStatic: true, addCollider: true);

            // Rampa 16.9 deg descendo do Anel Leste (+3.5m) para o piso 0m da arena das 3 Valvulas
            float run = 11.5f, rise = 3.5f;
            float rampLen = Mathf.Sqrt(run * run + rise * rise);
            float rampAng = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var z4Ramp = CreateWorldBox("Z4_Arena_Ramp_16_9deg", z4Root, new Vector3(7.0f, 1.60f, 10.0f), new Vector3(2.8f, 0.30f, rampLen), _matFloor, isStatic: true, addCollider: true);
            z4Ramp.transform.localRotation = Quaternion.Euler(rampAng, 0f, 0f);

            // === ENTIDADES §5.1 DA Z4 ===
            var entitiesRoot = GameObject.Find("Entities_Placeholders")?.transform ?? new GameObject("Entities_Placeholders").transform;

            // FOCAL POINT UNICO: Sirene-Mestra (H=16m) em (0, 0, 7)
            InstantiatePlaceholderFbx("sirene_mestra_placeholder", "ENT_Sirene_Mestra_Placeholder", entitiesRoot, new Vector3(0.0f, 0.0f, 7.0f), Quaternion.identity, _matAccent);

            // 3 Valvulas Ritmicas (I2, I3, I4) com InteractableBase + IkHandTarget
            (string id, Vector3 pos)[] valves =
            {
                ("I2_Valvula_A", new Vector3(-5.5f, 0.0f, 4.0f)),
                ("I3_Valvula_B", new Vector3(5.5f, 0.0f, 4.0f)),
                ("I4_Valvula_C", new Vector3(0.0f, 0.0f, 12.8f)),
            };
            foreach (var v in valves)
            {
                var vGo = InstantiatePlaceholderFbx("valvula_ritmica_placeholder", v.id, entitiesRoot, v.pos, Quaternion.identity, _matAccent);
                var col = vGo.AddComponent<BoxCollider>();
                col.size = new Vector3(1.0f, 1.2f, 1.0f);
                col.center = new Vector3(0f, 0.6f, 0f);
                var ik = new GameObject("IkHandTarget").transform;
                ik.SetParent(vGo.transform, false);
                ik.localPosition = new Vector3(0f, 0.95f, 0.25f);
                var inter = vGo.AddComponent<InteractableBase>();
                var so = new SerializedObject(inter);
                so.FindProperty("promptText").stringValue = $"Girar {v.id} [E/B]";
                so.FindProperty("ikHandTarget").objectReferenceValue = ik;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // No de Re-Sincronia (I5) em (0, 3.5, 0.5)
            var noGo = InstantiatePlaceholderFbx("no_resincronia_placeholder", "I5_No_ReSincronia_Placeholder", entitiesRoot, new Vector3(0.0f, 3.5f, 0.5f), Quaternion.identity, _matAccent);
            var noCol = noGo.AddComponent<BoxCollider>();
            noCol.size = new Vector3(1.4f, 2.0f, 1.4f);
            noCol.center = new Vector3(0f, 1.0f, 0f);
            var noInter = noGo.AddComponent<InteractableBase>();
            var soNo = new SerializedObject(noInter);
            soNo.FindProperty("promptText").stringValue = "Ativar Re-Sincronia (Bis) [Y/E]";
            soNo.ApplyModifiedPropertiesWithoutUndo();

            // Inimigos E3 (Ferrugem-Coletora) e E4 (Ferrugem-Trabalhadora) nao-bloqueantes
            InstantiatePlaceholderFbx("ferrugem_coletora_placeholder", "E3_Ferrugem_Coletora_Placeholder", entitiesRoot, new Vector3(-6.2f, 0.0f, 9.5f), Quaternion.Euler(0f, 125f, 0f), _matEnemy);
            InstantiatePlaceholderFbx("ferrugem_trabalhadora_placeholder", "E4_Ferrugem_Trabalhadora_Placeholder", entitiesRoot, new Vector3(6.2f, 0.0f, 9.5f), Quaternion.Euler(0f, -125f, 0f), _matEnemy);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            if (captureZ4)
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                cam.transform.position = new Vector3(0.0f, 6.2f, -3.2f);
                cam.transform.LookAt(new Vector3(0.0f, 5.5f, 7.0f));
                CaptureZoneScreenshot(
                    cam,
                    "c2_z4_camara_caldeiral",
                    "O QUE É: Captura do blockout greybox C2 da Zona Z4 (Câmara Octogonal do Caldeiral Central — 22m x 22m, teto 18m, Focal Point Sirene-Mestra H=16m, Nó de Re-Sincronia I5 em +3.5m, 3 Válvulas I2..I4 e E3/E4).\n" +
                    "O QUE VOCÊ DECIDE: Aprovar a monumentalidade do Focal Point único, leitura vertical da câmara e acesso às 3 válvulas + Nó de Re-Sincronia.\n" +
                    "O QUE ACONTECE DEPOIS: Com seu OK no consolidado C2, Z4 segue para kit CC0 + PBR e substituição da Sirene-Mestra/Válvulas no C5.\n"
                );
            }
            Debug.Log("[CaldeiralC2] Z4 Camara do Caldeiral Central construida.");
        }

        /// <summary>
        /// Z5: Galeria de Retorno Sync (21m x 10m, X: -32..-11, Z: 4..14, Y=+3.5m) + Mirante descendo sobre Z1 (X: -32..-26, Z: -12..4).
        /// Fecha o loop arquitetonico completo sem adicionar luz real extra (mantem 5/5 luzes Android URP).
        /// </summary>
        public static void BuildZ5GaleriaRetorno(bool captureZ5 = true)
        {
            var z5Root = new GameObject("Z5_Galeria_Retorno_Sync").transform;

            // Galeria principal Y=+3.5m (X: -32..-11, Z: 4..14, teto Y=12.5m)
            CreateWorldBox("Z5_Floor_Main", z5Root, new Vector3(-21.5f, 3.30f, 9f), new Vector3(21f, 0.40f, 10f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Ceiling_Main", z5Root, new Vector3(-21.5f, 12.75f, 9f), new Vector3(21f, 0.50f, 10f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z5_Wall_North", z5Root, new Vector3(-21.5f, 8.0f, 14.25f), new Vector3(21f, 9.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Wall_South_E", z5Root, new Vector3(-18.5f, 8.0f, 3.75f), new Vector3(15f, 9.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Wall_West", z5Root, new Vector3(-32.25f, 8.0f, 9f), new Vector3(0.5f, 9.5f, 10f), _matWall, isStatic: true, addCollider: true);

            // I6: Catraca de Saida Sync em (-24, 3.5, 9) com moldura ambar + InteractableBase
            var gate = new GameObject("I6_Catraca_Saida_Sync");
            gate.transform.SetParent(z5Root);
            gate.transform.position = new Vector3(-24.0f, 3.5f, 9.0f);
            CreateWorldBox("I6_Pillar_S", gate.transform, new Vector3(0f, 2.0f, -2.2f), new Vector3(0.6f, 4.0f, 0.6f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("I6_Pillar_N", gate.transform, new Vector3(0f, 2.0f, 2.2f), new Vector3(0.6f, 4.0f, 0.6f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("I6_Lintel", gate.transform, new Vector3(0f, 4.2f, 0f), new Vector3(0.7f, 0.5f, 5.0f), _matAccent, isStatic: true, addCollider: false);
            var gCol = gate.AddComponent<BoxCollider>();
            gCol.size = new Vector3(1.5f, 3.0f, 4.0f);
            gCol.center = new Vector3(0f, 1.5f, 0f);
            gCol.isTrigger = true;
            var gInter = gate.AddComponent<InteractableBase>();
            var soG = new SerializedObject(gInter);
            soG.FindProperty("promptText").stringValue = "Abrir Passagem ao Mercado [E/B]";
            soG.ApplyModifiedPropertiesWithoutUndo();

            // Mirante de Retorno sobre Z1 (X: -32..-26, Z: -12..4, teto 12.5m, descida suave ate Z=-12)
            CreateWorldBox("Z5_Mirante_Floor", z5Root, new Vector3(-29f, 3.30f, -4f), new Vector3(6f, 0.40f, 16f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Mirante_Ceiling", z5Root, new Vector3(-29f, 12.75f, -4f), new Vector3(6f, 0.50f, 16f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z5_Mirante_Wall_W", z5Root, new Vector3(-32.25f, 8.0f, -4f), new Vector3(0.5f, 9.5f, 16f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Mirante_Wall_E", z5Root, new Vector3(-25.75f, 8.0f, -4f), new Vector3(0.5f, 9.5f, 16f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Mirante_Parapet_OverZ1", z5Root, new Vector3(-29f, 4.0f, -11.8f), new Vector3(6f, 1.0f, 0.3f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z5_Mirante_WindowFrame_Top", z5Root, new Vector3(-29f, 10.5f, -11.8f), new Vector3(6f, 4.5f, 0.4f), _matWall, isStatic: true, addCollider: true);

            // Restaura camera principal na Z1 (spawn do jogador) para a cena salva ficar pronta pro PlayMode
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            cam.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
            cam.transform.LookAt(new Vector3(-27.5f, 1.3f, -17.4f));

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            if (captureZ5)
            {
                cam.transform.position = new Vector3(-12.5f, 6.4f, 9.0f);
                cam.transform.LookAt(new Vector3(-26.0f, 4.8f, 8.8f));
                CaptureZoneScreenshot(
                    cam,
                    "c2_z5_galeria_retorno",
                    "O QUE É: Captura do blockout greybox C2 da Zona Z5 (Galeria de Retorno Sync — 21m x 10m em +3.5m, Catraca de Saída I6 emoldurada em âmbar e corredor do Mirante fechando o loop sobre Z1).\n" +
                    "O QUE VOCÊ DECIDE: Aprovar o fechamento do loop arquitetônico Z4 -> Z5 -> Mirante sobre Z1.\n" +
                    "O QUE ACONTECE DEPOIS: Com o consolidado C2 (Z1..Z5) aprovado, avançamos para C3 (vestimenta CC0 + PBR world-scale).\n"
                );
            }
            Debug.Log("[CaldeiralC2] Z5 Galeria de Retorno Sync construida.");
        }

        private static void CaptureWalkthroughFrames()
        {
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            string framesDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures", "walkthrough_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            // 16 keyframes ao longo de todo o loop: Spawn Z1 -> Z2 -> Z3 -> Z4 -> Z5 -> Mirante sobre Z1
            (Vector3 pos, Vector3 look)[] stops =
            {
                (new Vector3(-35.0f, 2.8f, -22.2f), new Vector3(-28.5f, 1.2f, -17.5f)), // 01 Z1 Spawn + Dez + Porca
                (new Vector3(-30.5f, 2.2f, -19.8f), new Vector3(-22.0f, 2.0f, -18.0f)), // 02 Z1 Bancada I1 -> Arco Z1-Z2
                (new Vector3(-20.0f, 2.2f, -18.0f), new Vector3(-10.0f, 2.0f, -18.0f)), // 03 Tunel Z1->Z2
                (new Vector3(-16.8f, 3.2f, -23.5f), new Vector3(-7.0f, 1.8f, -17.5f)),  // 04 Z2 Tio Horario + Rampa
                (new Vector3(-10.0f, 3.5f, -18.5f), new Vector3(0.0f, 2.2f, -18.0f)),   // 05 Z2 Passarela -> Arco Z3
                (new Vector3(3.0f, 2.2f, -18.0f),   new Vector3(14.0f, 2.8f, -18.0f)),  // 06 Tunel Z2->Z3
                (new Vector3(11.0f, 3.6f, -21.8f),  new Vector3(22.0f, 4.2f, -18.5f)),  // 07 Z3 Rampa -> Vo Segunda
                (new Vector3(25.5f, 5.4f, -16.5f),  new Vector3(26.5f, 4.8f, 2.0f)),    // 08 Z3 Passarela Lateral + Paredoes 8m
                (new Vector3(26.5f, 5.4f, 1.5f),    new Vector3(26.5f, 3.6f, 8.5f)),    // 09 Z3 Ponte sobre Fosso Vapor 6.5m
                (new Vector3(20.0f, 5.4f, 8.8f),    new Vector3(11.0f, 5.0f, 8.5f)),    // 10 Z3->Z4 Ponte Elevada +3.5m
                (new Vector3(8.5f, 6.2f, 8.5f),     new Vector3(0.0f, 6.5f, 7.0f)),     // 11 Z4 Entrada -> Sirene-Mestra H=16m
                (new Vector3(0.0f, 6.2f, -3.0f),    new Vector3(0.0f, 5.5f, 7.0f)),     // 12 Z4 No Re-Sincronia I5 + Sirene
                (new Vector3(-5.0f, 2.2f, 2.0f),    new Vector3(0.0f, 2.0f, 10.0f)),    // 13 Z4 Piso Arena + 3 Valvulas
                (new Vector3(-9.5f, 5.6f, 8.5f),    new Vector3(-20.0f, 5.0f, 9.0f)),   // 14 Z4->Z5 Saida Oeste
                (new Vector3(-14.0f, 6.0f, 9.0f),   new Vector3(-25.0f, 4.8f, 9.0f)),   // 15 Z5 Galeria Sync + Catraca I6
                (new Vector3(-29.0f, 5.5f, 2.0f),   new Vector3(-29.0f, 3.5f, -12.0f)), // 16 Z5 Mirante de Retorno sobre Z1
            };

            // Warm-up URP
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            int width = 960, height = 540;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            int frameIdx = 0;
            for (int i = 0; i < stops.Length; i++)
            {
                int subSteps = (i < stops.Length - 1) ? 3 : 1;
                for (int s = 0; s < subSteps; s++)
                {
                    float t = subSteps > 1 ? (float)s / subSteps : 0f;
                    Vector3 p = (i < stops.Length - 1) ? Vector3.Lerp(stops[i].pos, stops[i + 1].pos, t) : stops[i].pos;
                    Vector3 l = (i < stops.Length - 1) ? Vector3.Lerp(stops[i].look, stops[i + 1].look, t) : stops[i].look;
                    cam.transform.position = p;
                    cam.transform.LookAt(l);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D3}.png"), tex.EncodeToPNG());
                    frameIdx++;
                }
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[CaldeiralC2] Walkthrough gravado: {frameIdx} frames em {framesDir}");
        }
    }
}
