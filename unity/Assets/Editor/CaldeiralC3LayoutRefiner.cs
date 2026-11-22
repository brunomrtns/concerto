using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// CaldeiralC3LayoutRefiner — Estagio C3 (Refinamento Arquitetonico do Layout).
    /// Restricoes explicitas do diretor (Bruno):
    ///   - SEM arte final e SEM textura final (mantem materiais neutros/grid de leitura arquitetonica)
    ///   - Foco em arquitetura, proporcoes, leitura de espaco e composicao de visadas
    ///   - Zero GameObject.CreatePrimitive (malhas combinadas procedurais com UV world-scale)
    ///   - Zero alteracao na planta aprovada (dimensoes, rampas <= 18 deg, I1..I6, E1..E4 intocados)
    ///   - <= 5 luzes reais, static batching >= 90%, draw calls << 120, tris << 150k, tex_mb < 1 MB
    ///   - Checkpoint consolidado unico em director_review/YYYY-MM-DD_c3_caldeiral_consolidado/
    /// </summary>
    public static partial class CaldeiralC2BlockoutBuilder
    {
        private const string C3MeshDir = "Assets/Game/Art/Meshes/Greybox/C3";

        private static Material _matC3Trim;
        private static Material _matC3Pipe;

        [MenuItem("Studio/Concerto/C3 Layout Refinement/Build Full C3 Consolidated + Walkthrough")]
        public static void BuildFullC3Menu()
        {
            BuildFullC3RefinedLayout(captureConsolidated: true);
        }

        public static void BuildFullC3RefinedLayoutBatch()
        {
            try
            {
                BuildFullC3RefinedLayout(captureConsolidated: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC3] Erro fatal no build C3: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFullC3RefinedLayout(bool captureConsolidated = true)
        {
            EnsureFolder(C3MeshDir);

            // 1. Constroi a base deterministica C2 aprovada (Z1 -> Z5, sem bulkheads provisorios)
            BuildZ1OficinaSucata(closeZ1EndBulkhead: false, captureZ1: false);
            BuildZ2PassarelaBigorna(closeZ2EndBulkhead: false, captureZ2: false, addZ2GuideLight: false);
            BuildZ3PatioSirenes(closeZ3EndBulkhead: false, captureZ3: false);
            BuildZ4CamaraCaldeiral(closeZ4WestBulkhead: false, captureZ4: false);
            BuildZ5GaleriaRetorno(captureZ5: false);

            // 2. Materiais neutros de leitura estrutural (SEM textura nova — reusa proto_grid_ceiling_1m.png)
            _matC3Trim = EnsureProtoMaterial("Mat_C3_Trim_Neutral", $"{ProtoTexDir}/proto_grid_ceiling_1m.png", 0.12f, 0.20f, new Color(0.56f, 0.58f, 0.62f));
            _matC3Pipe = EnsureProtoMaterial("Mat_C3_Pipe_Neutral", $"{ProtoTexDir}/proto_grid_ceiling_1m.png", 0.25f, 0.35f, new Color(0.76f, 0.64f, 0.48f));

            // 3. Aplica refinamento arquitetonico C3 zona por zona (sem alterar planta nem mecanica)
            RefineZ1Architecture();
            RefineZ2Architecture();
            RefineZ3Architecture();
            RefineZ4Architecture();
            RefineZ5Architecture();

            // Restaura Main Camera na Z1 (spawn do jogador) para a cena salva ficar pronta pro PlayMode
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            cam.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
            cam.transform.LookAt(new Vector3(-27.5f, 1.3f, -17.4f));

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            if (captureConsolidated)
            {
                CaptureC3ConsolidatedCheckpoints(cam);
                CaptureC3WalkthroughFrames(cam);
                // Restaura camera de spawn apos capturas (sem sujar a cena)
                cam.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
                cam.transform.LookAt(new Vector3(-27.5f, 1.3f, -17.4f));
            }

            Debug.Log("[CaldeiralC3] Refinamento arquitetonico C3 completo (Z1..Z5) salvo e capturado.");
        }

        // =====================================================================
        // Z1 — OFICINA DA SUCATA (Refinamento de entrada, estante, bancada e janelao do Mirante Z5)
        // =====================================================================
        private static void RefineZ1Architecture()
        {
            var z1 = GameObject.Find("Z1_Oficina_da_Sucata")?.transform;
            if (z1 == null) return;

            // 1. Abre janelao panoramico no topo da parede Norte de Z1 (X: -32..-26, Y: 4.5..7.5)
            //    alinhado ao Mirante da Z5 (que fica fechado atras em Z=-12..4 -> zero void!).
            var oldNorth = z1.Find("Z1_Wall_North");
            if (oldNorth != null) UnityEngine.Object.DestroyImmediate(oldNorth.gameObject);
            CreateWorldBox("Z1_Wall_North_West", z1, new Vector3(-34.0f, 4.0f, -11.75f), new Vector3(4.0f, 8.0f, 0.5f), _matWall, true, true);
            CreateWorldBox("Z1_Wall_North_East", z1, new Vector3(-24.0f, 4.0f, -11.75f), new Vector3(4.0f, 8.0f, 0.5f), _matWall, true, true);
            CreateWorldBox("Z1_Wall_North_BelowMirante", z1, new Vector3(-29.0f, 1.75f, -11.75f), new Vector3(6.0f, 3.5f, 0.5f), _matWall, true, true);
            CreateWorldBox("Z1_Wall_North_AboveMirante", z1, new Vector3(-29.0f, 7.75f, -11.75f), new Vector3(6.0f, 0.5f, 0.5f), _matWall, true, true);

            // Substitui bloco solido da estante por prateleira estruturada em 3 niveis (mesmo footprint)
            var oldShelf = z1.Find("Z1_Props_Placeholders/Z1_Shelf_North_1");
            if (oldShelf != null) UnityEngine.Object.DestroyImmediate(oldShelf.gameObject);

            var trim = new C3MeshBatch();
            var pipes = new C3MeshBatch();

            // Pilastras de canto e ritmo em Z1 (H=8m, com base e capitel simples)
            Vector3[] pilPos =
            {
                new Vector3(-35.75f, 0f, -23.75f), new Vector3(-35.75f, 0f, -18.0f), new Vector3(-35.75f, 0f, -12.25f),
                new Vector3(-29.0f,  0f, -23.75f), new Vector3(-22.25f, 0f, -23.75f), new Vector3(-22.25f, 0f, -12.25f),
                new Vector3(-32.0f,  0f, -12.25f), new Vector3(-26.0f,  0f, -12.25f)
            };
            foreach (var p in pilPos)
            {
                AddSteppedColumn(trim, p, 8.0f, 0.55f);
            }

            // Rodapes continuos ancorando paredes ao piso
            trim.AddBox(new Vector3(-35.9f, 0.25f, -18.0f), new Vector3(0.25f, 0.5f, 12.0f));
            trim.AddBox(new Vector3(-29.0f, 0.25f, -23.9f), new Vector3(14.0f, 0.5f, 0.25f));
            trim.AddBox(new Vector3(-29.0f, 0.25f, -12.1f), new Vector3(14.0f, 0.5f, 0.25f));

            // Cornija superior + vigas longitudinais no teto Y=7.75m
            trim.AddBox(new Vector3(-29.0f, 7.75f, -21.0f), new Vector3(14.0f, 0.45f, 0.45f));
            trim.AddBox(new Vector3(-29.0f, 7.75f, -15.0f), new Vector3(14.0f, 0.45f, 0.45f));

            // Arquivolta dupla emoldurando o Arco Z1->Z2 (reforca visada para Z2)
            trim.AddBox(new Vector3(-22.35f, 2.5f, -20.55f), new Vector3(0.45f, 5.0f, 0.45f));
            trim.AddBox(new Vector3(-22.35f, 2.5f, -15.45f), new Vector3(0.45f, 5.0f, 0.45f));
            trim.AddBox(new Vector3(-22.35f, 5.15f, -18.0f), new Vector3(0.55f, 0.5f, 5.6f));

            // Estante industrial em 3 niveis (X=-29, Z=-12.6, 3.2m x 2.4m x 0.9m)
            for (int px = -1; px <= 1; px++)
                for (int pz = -1; pz <= 1; pz += 2)
                    trim.AddBox(new Vector3(-29.0f + px * 1.5f, 1.2f, -12.6f + pz * 0.38f), new Vector3(0.12f, 2.4f, 0.12f));
            trim.AddBox(new Vector3(-29.0f, 0.65f, -12.6f), new Vector3(3.15f, 0.08f, 0.85f));
            trim.AddBox(new Vector3(-29.0f, 1.45f, -12.6f), new Vector3(3.15f, 0.08f, 0.85f));
            trim.AddBox(new Vector3(-29.0f, 2.25f, -12.6f), new Vector3(3.15f, 0.08f, 0.85f));

            // Painel traseiro estrutural na Bancada I1 (enquadra ponto interagivel sem alterar transform)
            trim.AddBox(new Vector3(-29.0f, 1.55f, -16.95f), new Vector3(2.4f, 1.1f, 0.10f));
            trim.AddBox(new Vector3(-30.15f, 1.65f, -16.95f), new Vector3(0.14f, 1.3f, 0.18f));
            trim.AddBox(new Vector3(-27.85f, 1.65f, -16.95f), new Vector3(0.14f, 1.3f, 0.18f));

            // Moldura do janelao do Mirante Z5 visto de dentro de Z1
            trim.AddBox(new Vector3(-29.0f, 3.55f, -11.95f), new Vector3(6.4f, 0.22f, 0.45f));
            trim.AddBox(new Vector3(-29.0f, 7.45f, -11.95f), new Vector3(6.4f, 0.25f, 0.45f));

            // Dutos guia (leading lines): descem atras da Bancada I1, sobem ao teto e apontam pelo tunel para Z2
            pipes.AddPipe(new Vector3(-29.0f, 2.0f, -16.8f), new Vector3(-29.0f, 6.8f, -16.8f), 0.16f);
            pipes.AddPipe(new Vector3(-29.0f, 6.8f, -16.8f), new Vector3(-22.0f, 6.8f, -16.8f), 0.16f);
            pipes.AddPipe(new Vector3(-22.0f, 4.35f, -16.4f), new Vector3(-18.0f, 4.35f, -16.4f), 0.14f);
            pipes.AddPipe(new Vector3(-22.0f, 4.35f, -19.6f), new Vector3(-18.0f, 4.35f, -19.6f), 0.14f);

            trim.Commit("Z1_C3_Architecture_Trim", z1, _matC3Trim);
            pipes.Commit("Z1_C3_LeadingPipes", z1, _matC3Pipe);
        }

        // =====================================================================
        // Z2 — PASSARELA DA BIGORNA (Porticos ritmicos, guarda-corpos vazados, toldo Tio Horario)
        // =====================================================================
        private static void RefineZ2Architecture()
        {
            var z2 = GameObject.Find("Z2_Passarela_da_Bigorna")?.transform;
            if (z2 == null) return;

            // Remove placas solidas de guarda-corpo para substituir por guarda-corpos vazados legiveis
            var oldMezRail = z2.Find("Z2_Mezzanine_Rail");
            if (oldMezRail != null) UnityEngine.Object.DestroyImmediate(oldMezRail.gameObject);
            var oldRampRail = z2.Find("Z2_Mezzanine_Ramp_Rail");
            if (oldRampRail != null) UnityEngine.Object.DestroyImmediate(oldRampRail.gameObject);

            var trim = new C3MeshBatch();
            var pipes = new C3MeshBatch();
            var accentRails = new C3MeshBatch();

            // 4 porticos estruturais em U invertido guiando a perspectiva para o Arco Z2->Z3
            float[] portalXs = { -16.0f, -11.5f, -7.0f, -2.5f };
            foreach (float px in portalXs)
            {
                AddSteppedColumn(trim, new Vector3(px, 0f, -24.75f), 10.0f, 0.60f);
                AddSteppedColumn(trim, new Vector3(px, 0f, -11.25f), 10.0f, 0.60f);
                trim.AddBox(new Vector3(px, 9.55f, -18.0f), new Vector3(0.55f, 0.75f, 13.8f));
                // Maos-francesas nos cantos superiores do portico
                trim.AddBox(new Vector3(px, 8.6f, -23.8f), new Vector3(0.45f, 1.3f, 1.3f), Quaternion.Euler(45f, 0f, 0f));
                trim.AddBox(new Vector3(px, 8.6f, -12.2f), new Vector3(0.45f, 1.3f, 1.3f), Quaternion.Euler(45f, 0f, 0f));
            }

            // Rodapes Sul/Norte
            trim.AddBox(new Vector3(-9.0f, 0.25f, -24.9f), new Vector3(18.0f, 0.5f, 0.25f));
            trim.AddBox(new Vector3(-9.0f, 0.25f, -11.1f), new Vector3(18.0f, 0.5f, 0.25f));

            // Bases chanfradas nos pilares de cobertura (leitura imediata de cobertura tatica)
            trim.AddBox(new Vector3(-11.5f, 0.25f, -15.6f), new Vector3(1.4f, 0.5f, 1.4f));
            trim.AddBox(new Vector3(-11.5f, 3.25f, -15.6f), new Vector3(1.25f, 0.25f, 1.25f));
            trim.AddBox(new Vector3(-5.0f,  0.25f, -21.2f), new Vector3(1.4f, 0.5f, 1.4f));
            trim.AddBox(new Vector3(-5.0f,  3.25f, -21.2f), new Vector3(1.25f, 0.25f, 1.25f));

            // Maos-francesas sob o deck do Mezanino +3.5m
            trim.AddBox(new Vector3(-5.7f, 2.75f, -13.5f), new Vector3(0.35f, 1.1f, 1.1f), Quaternion.Euler(45f, 0f, 0f));
            trim.AddBox(new Vector3(-0.6f, 2.75f, -13.5f), new Vector3(0.35f, 1.1f, 1.1f), Quaternion.Euler(45f, 0f, 0f));

            // Toldo/cobertura arquitetonica enquadrando a banca do Tio Horario (-13.8, 0, -21.6)
            trim.AddBox(new Vector3(-15.1f, 1.35f, -20.7f), new Vector3(0.18f, 2.70f, 0.18f));
            trim.AddBox(new Vector3(-12.5f, 1.35f, -20.7f), new Vector3(0.18f, 2.70f, 0.18f));
            trim.AddBox(new Vector3(-15.1f, 1.50f, -23.4f), new Vector3(0.18f, 3.00f, 0.18f));
            trim.AddBox(new Vector3(-12.5f, 1.50f, -23.4f), new Vector3(0.18f, 3.00f, 0.18f));
            trim.AddBox(new Vector3(-13.8f, 2.98f, -22.05f), new Vector3(3.0f, 0.18f, 3.1f), Quaternion.Euler(-8f, 0f, 0f));

            // Guarda-corpo vazado industrial no Mezanino +3.5m (X: -6..0, Z=-14.4)
            AddOpenRailing(accentRails, new Vector3(-6.0f, 3.5f, -14.4f), new Vector3(0.0f, 3.5f, -14.4f), 1.0f, 5);
            // Guarda-corpo vazado inclinado ao longo da rampa 16.9 deg (X: -17.5 -> -6.0, Y: 0.0 -> 3.5, Z=-13.95)
            AddOpenRailing(accentRails, new Vector3(-17.5f, 0.0f, -13.95f), new Vector3(-6.0f, 3.5f, -13.95f), 0.95f, 8);

            // Duplo duto aereo de vapor correndo de Z1->Z2 ate o Arco Z2->Z3 (leading lines)
            pipes.AddPipe(new Vector3(-18.0f, 8.4f, -16.2f), new Vector3(0.0f, 8.4f, -16.2f), 0.24f);
            pipes.AddPipe(new Vector3(-18.0f, 8.4f, -19.8f), new Vector3(0.0f, 8.4f, -19.8f), 0.24f);
            pipes.AddPipe(new Vector3(0.0f, 4.35f, -16.3f), new Vector3(6.0f, 4.35f, -16.3f), 0.16f);
            pipes.AddPipe(new Vector3(0.0f, 4.35f, -19.7f), new Vector3(6.0f, 4.35f, -19.7f), 0.16f);

            trim.Commit("Z2_C3_Portals_And_Structure", z2, _matC3Trim);
            accentRails.Commit("Z2_C3_OpenRailings", z2, _matAccent);
            pipes.Commit("Z2_C3_OverheadPipes", z2, _matC3Pipe);
        }

        // =====================================================================
        // Z3 — PATIO DAS SIRENES + GALERIA DE FUMACA (Porticos 14m, fosso de vapor, guarda-corpos)
        // =====================================================================
        private static void RefineZ3Architecture()
        {
            var z3 = GameObject.Find("Z3_Patio_das_Sirenes")?.transform;
            if (z3 == null) return;

            var oldVoRail = z3.Find("Z3_VoSegunda_Rail");
            if (oldVoRail != null) UnityEngine.Object.DestroyImmediate(oldVoRail.gameObject);
            var oldWalkRail = z3.Find("Z3_Walkway_Rail_W");
            if (oldWalkRail != null) UnityEngine.Object.DestroyImmediate(oldWalkRail.gameObject);

            var trim = new C3MeshBatch();
            var pipes = new C3MeshBatch();
            var accentRails = new C3MeshBatch();

            // Porticos monumentais (H=14m) ao longo da Galeria de Fumaca (X: 16..30)
            float[] galZs = { -9.0f, -3.5f, 2.0f, 7.5f, 11.4f };
            foreach (float gz in galZs)
            {
                AddSteppedColumn(trim, new Vector3(29.7f, 0f, gz), 14.0f, 0.70f);
                AddSteppedColumn(trim, new Vector3(16.3f, 0f, gz), 14.0f, 0.70f);
                trim.AddBox(new Vector3(23.0f, 13.5f, gz), new Vector3(13.8f, 0.85f, 0.65f));
            }
            // Porticos no Patio Sul (X: 8, 14, 20)
            foreach (float px in new[] { 8.5f, 14.5f, 20.5f })
            {
                AddSteppedColumn(trim, new Vector3(px, 0f, -23.7f), 14.0f, 0.70f);
                trim.AddBox(new Vector3(px, 13.5f, -17.0f), new Vector3(0.65f, 0.85f, 13.6f));
            }

            // Pilares de sustentacao sob a Passarela Lateral + Patamar Vo Segunda + Ponte
            foreach (float pz in new[] { -16.5f, -10.5f, -4.5f, 1.5f, 9.5f })
            {
                trim.AddBox(new Vector3(24.7f, 1.6f, pz), new Vector3(0.45f, 3.2f, 0.45f));
            }
            trim.AddBox(new Vector3(19.3f, 1.6f, -23.0f), new Vector3(0.45f, 3.2f, 0.45f));
            trim.AddBox(new Vector3(19.3f, 1.6f, -16.8f), new Vector3(0.45f, 3.2f, 0.45f));

            // Vigas transversais sob a ponte do Fosso de Vapor (Z: +2.5..+9.0)
            foreach (float bz in new[] { 3.2f, 5.75f, 8.3f })
            {
                trim.AddBox(new Vector3(26.5f, 2.95f, bz), new Vector3(4.2f, 0.35f, 0.35f));
            }
            // Bordas estruturais do Fosso de Vapor em Y=0
            trim.AddBox(new Vector3(23.0f, -1.25f, 2.4f), new Vector3(14.0f, 2.5f, 0.35f));
            trim.AddBox(new Vector3(23.0f, -1.25f, 9.1f), new Vector3(14.0f, 2.5f, 0.35f));

            // Molduras horizontais superior e inferior nos paredoes de wall-run (8m)
            trim.AddBox(new Vector3(29.65f, 2.55f, -4.0f), new Vector3(0.45f, 0.28f, 12.2f));
            trim.AddBox(new Vector3(29.65f, 10.45f, -4.0f), new Vector3(0.45f, 0.28f, 12.2f));
            trim.AddBox(new Vector3(16.35f, 2.55f, 2.0f), new Vector3(0.45f, 0.28f, 12.2f));
            trim.AddBox(new Vector3(16.35f, 10.45f, 2.0f), new Vector3(0.45f, 0.28f, 12.2f));

            // Guarda-corpos vazados industriais na rampa, patamar Vo Segunda, passarela 4m e ponte
            AddOpenRailing(accentRails, new Vector3(7.5f, 0.0f, -20.55f), new Vector3(19.0f, 3.5f, -20.55f), 0.95f, 7);
            AddOpenRailing(accentRails, new Vector3(19.1f, 3.5f, -20.5f), new Vector3(19.1f, 3.5f, -16.5f), 1.0f, 4);
            AddOpenRailing(accentRails, new Vector3(24.6f, 3.5f, -16.5f), new Vector3(24.6f, 3.5f, 2.5f), 1.0f, 10);
            AddOpenRailing(accentRails, new Vector3(24.6f, 3.5f, 2.5f), new Vector3(24.6f, 3.5f, 9.0f), 1.0f, 5);
            AddOpenRailing(accentRails, new Vector3(28.4f, 3.5f, 2.5f), new Vector3(28.4f, 3.5f, 9.0f), 1.0f, 5);

            // Moldura ambar no Arco elevado Z3->Z4 (X=16, Z=6..11, Y=3.5..8.0)
            accentRails.AddBox(new Vector3(16.05f, 5.75f, 5.85f), new Vector3(0.6f, 4.5f, 0.45f));
            accentRails.AddBox(new Vector3(16.05f, 5.75f, 11.15f), new Vector3(0.6f, 4.5f, 0.45f));
            accentRails.AddBox(new Vector3(16.05f, 8.1f, 8.5f), new Vector3(0.65f, 0.45f, 5.7f));

            // 3 grandes tubulacoes de vapor no fundo do fosso (Y=-1.8m) + dutos aereos guiando para Z4
            pipes.AddPipe(new Vector3(16.2f, -1.8f, 4.0f), new Vector3(29.8f, -1.8f, 4.0f), 0.45f);
            pipes.AddPipe(new Vector3(16.2f, -1.8f, 5.75f), new Vector3(29.8f, -1.8f, 5.75f), 0.55f);
            pipes.AddPipe(new Vector3(16.2f, -1.8f, 7.5f), new Vector3(29.8f, -1.8f, 7.5f), 0.45f);
            pipes.AddPipe(new Vector3(28.8f, 11.8f, -18.0f), new Vector3(28.8f, 11.8f, 10.5f), 0.30f);
            pipes.AddPipe(new Vector3(28.8f, 11.8f, 10.5f), new Vector3(16.0f, 11.8f, 10.5f), 0.30f);

            trim.Commit("Z3_C3_Portals_And_Chasm", z3, _matC3Trim);
            accentRails.Commit("Z3_C3_OpenRailings_And_Arch", z3, _matAccent);
            pipes.Commit("Z3_C3_VaporPipes", z3, _matC3Pipe);
        }

        // =====================================================================
        // Z4 — CAMARA OCTOGONAL DO CALDEIRAL CENTRAL (Cupula 18m, 8 colunas, plinto + dutos I2..I4 -> Sirene)
        // =====================================================================
        private static void RefineZ4Architecture()
        {
            var z4 = GameObject.Find("Z4_Camara_Caldeiral_Central")?.transform;
            if (z4 == null) return;

            var trim = new C3MeshBatch();
            var pipes = new C3MeshBatch();
            var accentRails = new C3MeshBatch();

            Vector3 center = new Vector3(0f, 0f, 7f);

            // 8 colunas monumentais (H=18m) nos vertices do octogono + 8 nervuras radiais de cupula convergindo sobre a Sirene-Mestra!
            float radius = 10.4f;
            for (int i = 0; i < 8; i++)
            {
                float ang = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                Vector3 colBase = new Vector3(center.x + Mathf.Cos(ang) * radius, 0f, center.z + Mathf.Sin(ang) * radius);
                AddSteppedColumn(trim, colBase, 18.0f, 0.85f);

                // Nervura radial da cupula ligando topo da coluna (Y=16.2) ao anel oculus central (Y=17.7, r=2.6m)
                Vector3 ribStart = new Vector3(center.x + Mathf.Cos(ang) * (radius - 0.5f), 16.0f, center.z + Mathf.Sin(ang) * (radius - 0.5f));
                Vector3 ribEnd = new Vector3(center.x + Mathf.Cos(ang) * 2.4f, 17.65f, center.z + Mathf.Sin(ang) * 2.4f);
                AddBeamBetween(trim, ribStart, ribEnd, 0.55f, 0.70f);
            }

            // Anel estrutural superior (oculus fechado) em Y=17.5m + frisos horizontais nos 3 niveis (3.5m, 7.0m, 12.0m)
            for (int i = 0; i < 8; i++)
            {
                float a0 = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                float a1 = ((i + 1) * 45f + 22.5f) * Mathf.Deg2Rad;
                // Oculus superior (r=2.5m)
                AddBeamBetween(trim,
                    new Vector3(center.x + Mathf.Cos(a0) * 2.5f, 17.55f, center.z + Mathf.Sin(a0) * 2.5f),
                    new Vector3(center.x + Mathf.Cos(a1) * 2.5f, 17.55f, center.z + Mathf.Sin(a1) * 2.5f),
                    0.45f, 0.55f);
                // Friso nivel +7.0m (marca visual do 3o nivel vertical na camara)
                AddBeamBetween(trim,
                    new Vector3(center.x + Mathf.Cos(a0) * 10.1f, 7.0f, center.z + Mathf.Sin(a0) * 10.1f),
                    new Vector3(center.x + Mathf.Cos(a1) * 10.1f, 7.0f, center.z + Mathf.Sin(a1) * 10.1f),
                    0.40f, 0.45f);
                // Friso nivel +12.0m
                AddBeamBetween(trim,
                    new Vector3(center.x + Mathf.Cos(a0) * 10.1f, 12.0f, center.z + Mathf.Sin(a0) * 10.1f),
                    new Vector3(center.x + Mathf.Cos(a1) * 10.1f, 12.0f, center.z + Mathf.Sin(a1) * 10.1f),
                    0.40f, 0.45f);
            }

            // Plinto escalonado (2 degraus) na base da Sirene-Mestra (0, 0, 7) — eleva presenca do Focal Point
            trim.AddBox(new Vector3(0f, 0.12f, 7f), new Vector3(4.8f, 0.24f, 4.8f));
            trim.AddBox(new Vector3(0f, 0.12f, 7f), new Vector3(4.8f, 0.24f, 4.8f), Quaternion.Euler(0f, 45f, 0f));
            trim.AddBox(new Vector3(0f, 0.32f, 7f), new Vector3(3.6f, 0.20f, 3.6f));
            trim.AddBox(new Vector3(0f, 0.32f, 7f), new Vector3(3.6f, 0.20f, 3.6f), Quaternion.Euler(0f, 45f, 0f));

            // Pilares de sustentacao sob o Anel +3.5m (Leste, Sul, Oeste)
            Vector3[] deckCols =
            {
                new Vector3(7.2f, 1.6f, 1.8f), new Vector3(7.2f, 1.6f, -3.2f),
                new Vector3(3.2f, 1.6f, 1.4f), new Vector3(-3.2f, 1.6f, 1.4f),
                new Vector3(-7.2f, 1.6f, 1.8f), new Vector3(-7.2f, 1.6f, 8.5f)
            };
            foreach (var dc in deckCols) trim.AddBox(dc, new Vector3(0.5f, 3.2f, 0.5f));

            // Guarda-corpos vazados industriais no perimetro interno do Anel +3.5m
            // (deixa abertura limpa ao redor do No Re-Sincronia I5 em X=-1.5..+1.5 e na entrada da rampa Leste)
            AddOpenRailing(accentRails, new Vector3(-7.0f, 3.5f, 1.6f), new Vector3(-7.0f, 3.5f, 11.8f), 1.0f, 6);
            AddOpenRailing(accentRails, new Vector3(-7.0f, 3.5f, 1.6f), new Vector3(-1.8f, 3.5f, 1.6f), 1.0f, 4);
            AddOpenRailing(accentRails, new Vector3(1.8f,  3.5f, 1.6f), new Vector3(7.0f,  3.5f, 1.6f), 1.0f, 4);
            AddOpenRailing(accentRails, new Vector3(7.0f,  3.5f, 1.6f), new Vector3(7.0f,  3.5f, 4.2f), 1.0f, 2);

            // Moldura ambar no Arco de saida Oeste Z4->Z5 (X=-11, Z=6..11, Y=3.5..8.0)
            accentRails.AddBox(new Vector3(-10.95f, 5.75f, 5.85f), new Vector3(0.6f, 4.5f, 0.45f));
            accentRails.AddBox(new Vector3(-10.95f, 5.75f, 11.15f), new Vector3(0.6f, 4.5f, 0.45f));
            accentRails.AddBox(new Vector3(-10.95f, 8.1f, 8.5f), new Vector3(0.65f, 0.45f, 5.7f));

            // 3 dutos radiais no piso ligando fisicamente I2, I3 e I4 a base da Sirene-Mestra (leitura de puzzle!)
            pipes.AddPipe(new Vector3(-5.5f, 0.22f, 4.0f), new Vector3(-1.6f, 0.22f, 6.2f), 0.16f);
            pipes.AddPipe(new Vector3(5.5f,  0.22f, 4.0f), new Vector3(1.6f,  0.22f, 6.2f), 0.16f);
            pipes.AddPipe(new Vector3(0.0f,  0.22f, 12.8f), new Vector3(0.0f, 0.22f, 8.9f), 0.16f);
            // Duto de energia conectando o No de Re-Sincronia I5 (0, 3.5, 0.5) a coluna da Sirene-Mestra
            pipes.AddPipe(new Vector3(0.0f, 3.7f, 1.4f), new Vector3(0.0f, 4.8f, 5.6f), 0.14f);

            trim.Commit("Z4_C3_DomeRibs_And_Columns", z4, _matC3Trim);
            accentRails.Commit("Z4_C3_RingRailings_And_ExitArch", z4, _matAccent);
            pipes.Commit("Z4_C3_ValveConduits", z4, _matC3Pipe);
        }

        // =====================================================================
        // Z5 — GALERIA DE RETORNO SYNC + MIRANTE SOBRE Z1 (Porticos + janelao panoramico sobre Z1)
        // =====================================================================
        private static void RefineZ5Architecture()
        {
            var z5 = GameObject.Find("Z5_Galeria_Retorno_Sync")?.transform;
            if (z5 == null) return;

            var oldParapet = z5.Find("Z5_Mirante_Parapet_OverZ1");
            if (oldParapet != null) UnityEngine.Object.DestroyImmediate(oldParapet.gameObject);

            var trim = new C3MeshBatch();
            var pipes = new C3MeshBatch();
            var accentRails = new C3MeshBatch();

            // 4 porticos estruturais ao longo da Galeria Sync emoldurando a Catraca I6 (-24, 3.5, 9)
            foreach (float px in new[] { -14.0f, -19.0f, -24.0f, -29.0f })
            {
                AddSteppedColumn(trim, new Vector3(px, 3.5f, 13.75f), 9.0f, 0.55f);
                if (px > -26.0f)
                {
                    AddSteppedColumn(trim, new Vector3(px, 3.5f, 4.25f), 9.0f, 0.55f);
                }
                trim.AddBox(new Vector3(px, 12.1f, 9.0f), new Vector3(0.50f, 0.70f, 9.8f));
            }

            // Porticos ao longo do corredor do Mirante sobre Z1 (Z: 1, -4, -9)
            foreach (float mz in new[] { 1.0f, -4.0f, -9.0f })
            {
                AddSteppedColumn(trim, new Vector3(-31.75f, 3.5f, mz), 9.0f, 0.50f);
                AddSteppedColumn(trim, new Vector3(-26.25f, 3.5f, mz), 9.0f, 0.50f);
                trim.AddBox(new Vector3(-29.0f, 12.1f, mz), new Vector3(5.8f, 0.65f, 0.45f));
            }

            // Moldura do janelao panoramico do Mirante olhando para dentro de Z1 (Z=-11.8)
            trim.AddBox(new Vector3(-31.75f, 6.0f, -11.75f), new Vector3(0.55f, 5.0f, 0.55f));
            trim.AddBox(new Vector3(-26.25f, 6.0f, -11.75f), new Vector3(0.55f, 5.0f, 0.55f));
            trim.AddBox(new Vector3(-29.0f, 8.25f, -11.75f), new Vector3(6.0f, 0.5f, 0.55f));

            // Parapeito vazado industrial no Mirante sobre Z1 (permite ver Z1 e a Bancada I1 la embaixo!)
            AddOpenRailing(accentRails, new Vector3(-31.6f, 3.5f, -11.65f), new Vector3(-26.4f, 3.5f, -11.65f), 1.05f, 5);

            // Dutos guia aereos acompanhando a Galeria Sync ate o Mirante
            pipes.AddPipe(new Vector3(-11.0f, 11.2f, 13.2f), new Vector3(-31.2f, 11.2f, 13.2f), 0.22f);
            pipes.AddPipe(new Vector3(-31.2f, 11.2f, 13.2f), new Vector3(-31.2f, 11.2f, -11.2f), 0.22f);

            trim.Commit("Z5_C3_Portals_And_MiranteFrame", z5, _matC3Trim);
            accentRails.Commit("Z5_C3_Mirante_OpenParapet", z5, _matAccent);
            pipes.Commit("Z5_C3_SyncPipes", z5, _matC3Pipe);
        }

        // =====================================================================
        // CAPTURAS CONSOLIDADAS C3 + WALKTHROUGH C3
        // =====================================================================
        private static void CaptureC3ConsolidatedCheckpoints(Camera cam)
        {
            string dateOnly = DateTime.Now.ToString("yyyy-MM-dd");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string reviewDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "director_review", $"{dateOnly}_c3_caldeiral_consolidado");
            string capDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(reviewDir);
            Directory.CreateDirectory(capDir);

            (string slug, Vector3 pos, Vector3 look)[] shots =
            {
                ("c3_z1_oficina_sucata",     new Vector3(-35.2f, 3.2f, -22.6f), new Vector3(-27.5f, 1.4f, -17.4f)),
                ("c3_z2_passarela_bigorna",  new Vector3(-17.2f, 4.7f, -23.6f), new Vector3(-6.2f,  1.8f, -17.2f)),
                ("c3_z3_patio_sirenes",      new Vector3(17.5f,  7.2f, -22.5f), new Vector3(25.0f,  4.0f, -1.0f)),
                ("c3_z4_camara_caldeiral",   new Vector3(0.0f,   6.2f, -3.2f),  new Vector3(0.0f,   5.8f,  7.0f)),
                ("c3_z5_galeria_retorno",    new Vector3(-12.5f, 6.4f,  9.0f),  new Vector3(-26.0f, 4.8f,  8.8f)),
                ("c3_z5_mirante_visada_z1",  new Vector3(-29.0f, 5.2f, -9.8f),  new Vector3(-29.0f, 1.2f, -18.5f)),
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

            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            foreach (var s in shots)
            {
                cam.transform.position = s.pos;
                cam.transform.LookAt(s.look);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                byte[] png = tex.EncodeToPNG();

                File.WriteAllBytes(Path.Combine(capDir, $"{s.slug}_{stamp}.png"), png);
                File.WriteAllBytes(Path.Combine(capDir, $"{s.slug}.png"), png);
                File.WriteAllBytes(Path.Combine(reviewDir, $"{s.slug}_{stamp}.png"), png);
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            string readme =
                "O QUE É: Checkpoint consolidado do Estágio C3 do Caldeiral (Z1..Z5 + Mirante com visada real sobre Z1 + vídeo walkthrough regravado + folha 4x4).\n" +
                "RESTRIÇÕES RESPEITADAS: SEM arte final e SEM textura final — refinamento puro de arquitetura, proporções, leitura de espaço (dutos-guia, pórticos, nervuras da cúpula, guarda-corpos vazados, dutos das 3 válvulas à Sirene-Mestra) e composição de visadas.\n" +
                "O QUE VOCÊ DECIDE: Aprovar o conjunto arquitetônico C3 para liberar o C4 (Composição, visual e auditoria de câmeras) e deliberar sobre a Fila de Decisões do Diretor.\n";
            File.WriteAllText(Path.Combine(reviewDir, "LEIA-ME.txt"), readme);
            Debug.Log($"[CaldeiralC3] Capturas consolidadas salvas em: {reviewDir}");
        }

        private static void CaptureC3WalkthroughFrames(Camera cam)
        {
            string framesDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures", "walkthrough_c3_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            (Vector3 pos, Vector3 look)[] stops =
            {
                (new Vector3(-35.0f, 2.8f, -22.2f), new Vector3(-28.5f, 1.3f, -17.5f)), // 01 Z1 Spawn + Estante + Bancada
                (new Vector3(-30.5f, 2.2f, -19.8f), new Vector3(-22.0f, 2.2f, -18.0f)), // 02 Z1 Arquivolta + Duto -> Z2
                (new Vector3(-20.0f, 2.2f, -18.0f), new Vector3(-10.0f, 2.2f, -18.0f)), // 03 Tunel Z1->Z2
                (new Vector3(-16.8f, 3.2f, -23.5f), new Vector3(-7.0f,  2.0f, -17.5f)), // 04 Z2 Toldo Tio Horario + Porticos + Rampa
                (new Vector3(-10.0f, 3.5f, -18.5f), new Vector3(0.0f,   2.4f, -18.0f)), // 05 Z2 Nave Industrial -> Arco Z3
                (new Vector3(3.0f,   2.2f, -18.0f), new Vector3(14.0f,  2.8f, -18.0f)), // 06 Tunel Z2->Z3
                (new Vector3(11.0f,  3.6f, -21.8f), new Vector3(22.0f,  4.2f, -18.5f)), // 07 Z3 Rampa -> Vo Segunda
                (new Vector3(25.5f,  5.4f, -16.5f), new Vector3(26.5f,  4.8f,  2.0f)),  // 08 Z3 Passarela + Porticos 14m + Wall-run
                (new Vector3(26.5f,  5.4f,  1.5f),  new Vector3(26.5f,  3.2f,  8.5f)),  // 09 Z3 Ponte sobre Fosso + 3 Dutos Vapor
                (new Vector3(20.0f,  5.4f,  8.8f),  new Vector3(11.0f,  5.2f,  8.5f)),  // 10 Z3->Z4 Arco Elevado +3.5m
                (new Vector3(8.5f,   6.2f,  8.5f),  new Vector3(0.0f,   6.5f,  7.0f)),  // 11 Z4 Entrada -> Sirene-Mestra + Cupula 18m
                (new Vector3(0.0f,   6.2f, -3.0f),  new Vector3(0.0f,   5.8f,  7.0f)),  // 12 Z4 No Re-Sincronia I5 + Nervuras Cupula
                (new Vector3(-5.0f,  2.2f,  2.0f),  new Vector3(0.0f,   2.2f,  9.5f)),  // 13 Z4 Arena + Plinto + Dutos Valvulas I2..I4
                (new Vector3(-9.5f,  5.6f,  8.5f),  new Vector3(-20.0f, 5.0f,  9.0f)),  // 14 Z4->Z5 Arco Saida Oeste
                (new Vector3(-14.0f, 6.0f,  9.0f),  new Vector3(-25.0f, 4.8f,  9.0f)),  // 15 Z5 Galeria Sync + Porticos + Catraca I6
                (new Vector3(-29.0f, 5.2f, -9.8f),  new Vector3(-29.0f, 1.2f, -18.5f)), // 16 Z5 Mirante olhando direto para Z1!
            };

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
            Debug.Log($"[CaldeiralC3] {frameIdx} frames do walkthrough C3 gerados em {framesDir}");
        }

        // =====================================================================
        // HELPERS ARQUITETONICOS PROCEDURAIS (Zero CreatePrimitive, UV world-scale, static batch)
        // =====================================================================
        private static void AddSteppedColumn(C3MeshBatch batch, Vector3 baseCenter, float height, float width)
        {
            // Plinto base
            batch.AddBox(baseCenter + new Vector3(0f, 0.30f, 0f), new Vector3(width * 1.35f, 0.60f, width * 1.35f));
            // Fuste principal
            batch.AddBox(baseCenter + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width));
            // Capitel topo
            batch.AddBox(baseCenter + new Vector3(0f, height - 0.30f, 0f), new Vector3(width * 1.35f, 0.60f, width * 1.35f));
        }

        private static void AddOpenRailing(C3MeshBatch batch, Vector3 start, Vector3 end, float height, int segments)
        {
            Vector3 diff = end - start;
            float len = diff.magnitude;
            if (len < 0.05f) return;
            Quaternion rot = Quaternion.LookRotation(diff.normalized, Vector3.up);

            // Barra superior e barra intermediaria
            Vector3 mid = (start + end) * 0.5f;
            batch.AddBox(mid + new Vector3(0f, height, 0f), new Vector3(0.08f, 0.08f, len), rot);
            batch.AddBox(mid + new Vector3(0f, height * 0.52f, 0f), new Vector3(0.06f, 0.06f, len), rot);
            // Rodape de protecao na base
            batch.AddBox(mid + new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.14f, len), rot);

            // Montantes verticais
            int count = Mathf.Max(2, segments + 1);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                batch.AddBox(p + new Vector3(0f, height * 0.5f, 0f), new Vector3(0.09f, height, 0.09f));
            }
        }

        private static void AddBeamBetween(C3MeshBatch batch, Vector3 a, Vector3 b, float width, float height)
        {
            Vector3 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.05f) return;
            Quaternion rot = Quaternion.LookRotation(diff.normalized, Vector3.up);
            batch.AddBox((a + b) * 0.5f, new Vector3(width, height, len), rot);
        }

        private sealed class C3MeshBatch
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _norms = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _tris = new List<int>();

            public void AddBox(Vector3 center, Vector3 size) => AddBox(center, size, Quaternion.identity);

            public void AddBox(Vector3 center, Vector3 size, Quaternion rot)
            {
                float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
                float sx = size.x, sy = size.y, sz = size.z;

                Vector3[] lv =
                {
                    new Vector3(-hx,-hy,-hz), new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx,-hy,-hz),
                    new Vector3( hx,-hy, hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx,-hy, hz),
                    new Vector3(-hx,-hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy,-hz), new Vector3(-hx,-hy,-hz),
                    new Vector3( hx,-hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3( hx,-hy, hz),
                    new Vector3(-hx, hy,-hz), new Vector3(-hx, hy, hz), new Vector3( hx, hy, hz), new Vector3( hx, hy,-hz),
                    new Vector3(-hx,-hy, hz), new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz),
                };
                Vector3[] ln =
                {
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                    Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                    Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                    Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                };
                Vector2[] lu =
                {
                    new Vector2(0,0), new Vector2(0,sy), new Vector2(sx,sy), new Vector2(sx,0),
                    new Vector2(0,0), new Vector2(0,sy), new Vector2(sx,sy), new Vector2(sx,0),
                    new Vector2(0,0), new Vector2(0,sy), new Vector2(sz,sy), new Vector2(sz,0),
                    new Vector2(0,0), new Vector2(0,sy), new Vector2(sz,sy), new Vector2(sz,0),
                    new Vector2(0,0), new Vector2(0,sz), new Vector2(sx,sz), new Vector2(sx,0),
                    new Vector2(0,0), new Vector2(0,sz), new Vector2(sx,sz), new Vector2(sx,0),
                };
                int[] lt =
                {
                     0, 1, 2,  0, 2, 3,
                     4, 5, 6,  4, 6, 7,
                     8, 9,10,  8,10,11,
                    12,13,14, 12,14,15,
                    16,17,18, 16,18,19,
                    20,21,22, 20,22,23,
                };

                int baseIdx = _verts.Count;
                for (int i = 0; i < lv.Length; i++)
                {
                    _verts.Add(center + rot * lv[i]);
                    _norms.Add(rot * ln[i]);
                    _uvs.Add(lu[i]);
                }
                for (int i = 0; i < lt.Length; i++) _tris.Add(baseIdx + lt[i]);
            }

            public void AddPipe(Vector3 a, Vector3 b, float radius, int sides = 10)
            {
                Vector3 diff = b - a;
                float len = diff.magnitude;
                if (len < 0.05f) return;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, diff.normalized);
                Vector3 mid = (a + b) * 0.5f;
                float halfLen = len * 0.5f;
                int baseIdx = _verts.Count;

                for (int i = 0; i <= sides; i++)
                {
                    float ang = (i / (float)sides) * Mathf.PI * 2f;
                    float cx = Mathf.Cos(ang) * radius;
                    float cz = Mathf.Sin(ang) * radius;
                    Vector3 n = rot * new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    _verts.Add(mid + rot * new Vector3(cx, -halfLen, cz));
                    _verts.Add(mid + rot * new Vector3(cx, halfLen, cz));
                    _norms.Add(n);
                    _norms.Add(n);
                    float u = (i / (float)sides) * (2f * Mathf.PI * radius);
                    _uvs.Add(new Vector2(u, 0f));
                    _uvs.Add(new Vector2(u, len));
                }
                for (int i = 0; i < sides; i++)
                {
                    int s = baseIdx + i * 2;
                    _tris.Add(s); _tris.Add(s + 1); _tris.Add(s + 3);
                    _tris.Add(s); _tris.Add(s + 3); _tris.Add(s + 2);
                }
            }

            public GameObject Commit(string name, Transform parent, Material mat)
            {
                string assetPath = $"{C3MeshDir}/SM_C3_{name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (existing != null) AssetDatabase.DeleteAsset(assetPath);

                var mesh = new Mesh { name = $"SM_C3_{name}" };
                if (_verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_verts);
                mesh.SetNormals(_norms);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh, assetPath);

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(
                    go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic
                );
                return go;
            }
        }
    }
}
