using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Editor
{
    /// <summary>
    /// CaldeiralC4VisualDressing — Fase 4.1 (Vestimenta Visual Z1->Z5 com o Kit 4.0 Aprovado).
    /// Linguagem visual oficial (aprovada por Bruno em 2026-11-22):
    ///   latao / cobre / ferro escuro, rebitado, clockwork-industrial (TripoSG + Hunyuan3D-Paint).
    /// Regras inegociaveis:
    ///   - Tiling world-scale (1 tile = 1.25 - 2.0 m) sobre malhas UV 1m
    ///   - <= 5 luzes reais simultaneas (budget Android URP)
    ///   - Todas as pecas de vestimenta estaticas (BatchingStatic) -> draw calls << 120, tris << 150k
    ///   - Zero alteracao na planta, mecanica ou caminhos congelados
    ///   - Checkpoint consolidado unico em director_review/YYYY-MM-DD_c4_1_caldeiral_vestido/
    /// </summary>
    public static partial class CaldeiralC2BlockoutBuilder
    {
        private const string C4MatDir = "Assets/Game/Materials/CaldeiralC4";
        private const string GenTexDir = "Assets/Game/Art/Textures/Generated";
        private const string Fab3dPrefabDir = "Assets/Game/Prefabs/Fab3D";

        [MenuItem("Studio/Concerto/C4.1 Visual Dressing/Build Full C4.1 Dressed Caldeiral + Walkthrough")]
        public static void BuildFullC41Menu()
        {
            BuildFullC41VisualDressing(captureConsolidated: true);
        }

        public static void BuildFullC41VisualDressingBatch()
        {
            try
            {
                BuildFullC41VisualDressing(captureConsolidated: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC4.1] Erro fatal no build C4.1: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFullC41VisualDressing(bool captureConsolidated = true)
        {
            EnsureFolder(C4MatDir);
            AssetDatabase.Refresh();

            // 1. Constroi toda a geometria arquitetonica C3 aprovada (sem alteracoes na planta!)
            BuildFullC3RefinedLayout(captureConsolidated: false);

            // 2. Cria materiais PBR world-scale oficiais do Caldeiral (1 tile = 1.25 - 2.0 m)
            var matFloor = EnsureC4PbrMaterial(
                "Mat_C4_Floor_BrassIron",
                $"{GenTexDir}/caldeiral_piso_latao_albedo.png",
                $"{GenTexDir}/caldeiral_piso_latao_normal.png",
                new Vector2(0.667f, 0.667f), // 1 tile = 1.5 m
                new Color(0.74f, 0.65f, 0.54f),
                metallic: 0.30f, smoothness: 0.32f, bumpScale: 0.90f
            );
            var matWall = EnsureC4PbrMaterial(
                "Mat_C4_Wall_CopperBoiler",
                $"{GenTexDir}/caldeiral_parede_caldeira_albedo.png",
                $"{GenTexDir}/caldeiral_parede_caldeira_normal.png",
                new Vector2(0.50f, 0.50f), // 1 tile = 2.0 m
                new Color(0.78f, 0.64f, 0.54f),
                metallic: 0.36f, smoothness: 0.38f, bumpScale: 0.90f
            );
            var matCeiling = EnsureC4PbrMaterial(
                "Mat_C4_Ceiling_DarkIron",
                $"{GenTexDir}/caldeiral_teto_ferro_albedo.png",
                $"{GenTexDir}/caldeiral_teto_ferro_normal.png",
                new Vector2(0.50f, 0.50f), // 1 tile = 2.0 m
                new Color(0.54f, 0.52f, 0.50f),
                metallic: 0.55f, smoothness: 0.30f, bumpScale: 0.75f
            );
            var matTrim = EnsureC4PbrMaterial(
                "Mat_C4_Trim_DarkIron",
                $"{GenTexDir}/caldeiral_teto_ferro_albedo.png",
                $"{GenTexDir}/caldeiral_teto_ferro_normal.png",
                new Vector2(0.75f, 0.75f), // 1 tile = 1.33 m
                new Color(0.42f, 0.40f, 0.39f),
                metallic: 0.68f, smoothness: 0.44f, bumpScale: 0.85f
            );
            var matBrass = EnsureC4PbrMaterial(
                "Mat_C4_Brass_Accent",
                $"{GenTexDir}/caldeiral_piso_latao_albedo.png",
                $"{GenTexDir}/caldeiral_piso_latao_normal.png",
                new Vector2(0.80f, 0.80f), // 1 tile = 1.25 m
                new Color(0.95f, 0.70f, 0.32f),
                metallic: 0.74f, smoothness: 0.60f, bumpScale: 0.70f
            );
            var matCopperPipe = EnsureC4PbrMaterial(
                "Mat_C4_Copper_Pipe",
                $"{GenTexDir}/caldeiral_parede_caldeira_albedo.png",
                $"{GenTexDir}/caldeiral_parede_caldeira_normal.png",
                new Vector2(0.80f, 0.80f),
                new Color(0.88f, 0.52f, 0.30f),
                metallic: 0.72f, smoothness: 0.56f, bumpScale: 0.65f
            );
            var matWallRunSteel = EnsureC4PbrMaterial(
                "Mat_C4_WallRun_Steel",
                $"{GenTexDir}/caldeiral_teto_ferro_albedo.png",
                $"{GenTexDir}/caldeiral_teto_ferro_normal.png",
                new Vector2(0.50f, 0.50f),
                new Color(0.38f, 0.64f, 0.74f),
                metallic: 0.58f, smoothness: 0.50f, bumpScale: 0.80f
            );

            // 3. Aplica a linguagem visual PBR world-scale nas 5 zonas (substituindo greybox)
            ApplyPbrMaterialsToZones(matFloor, matWall, matCeiling, matTrim, matBrass, matCopperPipe, matWallRunSteel);

            // 4. Distribui instancias reais dos 4 prefabs do Kit 4.0 (TripoSG + Hunyuan PBR) em Z1..Z5
            int kitInstances = DressZonesWithKit40Prefabs();

            // 5. Calibra a iluminacao real do Caldeiral (estritamente 5/5 luzes URP) + atmosfera
            CalibrateCaldeiralLighting();

            // Restaura Main Camera na Z1 (spawn) antes de salvar
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            cam.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
            cam.transform.LookAt(new Vector3(-27.5f, 1.4f, -17.4f));

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            if (captureConsolidated)
            {
                CaptureC41ConsolidatedCheckpoints(cam, kitInstances);
                CaptureC41WalkthroughFrames(cam);
                cam.transform.position = new Vector3(-35.2f, 3.2f, -22.6f);
                cam.transform.LookAt(new Vector3(-27.5f, 1.4f, -17.4f));
            }

            Debug.Log($"[CaldeiralC4.1] Vestimenta visual Z1..Z5 concluida ({kitInstances} instancias do Kit 4.0).");
        }

        private static void ApplyPbrMaterialsToZones(
            Material matFloor, Material matWall, Material matCeiling,
            Material matTrim, Material matBrass, Material matCopperPipe, Material matWallRunSteel)
        {
            string[] zoneRoots =
            {
                "Z1_Oficina_da_Sucata",
                "Z2_Passarela_da_Bigorna",
                "Z3_Patio_das_Sirenes",
                "Z4_Camara_Caldeiral_Central",
                "Z5_Galeria_Retorno_Sync",
            };

            foreach (string zName in zoneRoots)
            {
                var zGo = GameObject.Find(zName);
                if (zGo == null) continue;
                foreach (var r in zGo.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name;
                    var cur = r.sharedMaterial;
                    string matName = cur != null ? cur.name : "";

                    if (n.Contains("WallRun_Panel"))
                    {
                        r.sharedMaterial = matWallRunSteel;
                    }
                    else if (matName == "Mat_C3_Trim_Neutral" || n.Contains("Trim") || n.Contains("Portals") || n.Contains("DomeRibs"))
                    {
                        r.sharedMaterial = matTrim;
                    }
                    else if (matName == "Mat_C3_Pipe_Neutral" || n.Contains("Pipes") || n.Contains("Conduits"))
                    {
                        r.sharedMaterial = matCopperPipe;
                    }
                    else if (matName == "Mat_Proto_Accent_1m" || n.Contains("Arch") || n.Contains("Rail") || n.Contains("Parapet") || n.Contains("VaporBridge") || n.Contains("Bancada_Tampo") || n.Contains("I6_"))
                    {
                        r.sharedMaterial = matBrass;
                    }
                    else if (matName == "Mat_Proto_Floor_1m" || n.Contains("Floor") || n.Contains("Deck") || n.Contains("Ramp") || n.Contains("Walkway"))
                    {
                        r.sharedMaterial = matFloor;
                    }
                    else if (matName == "Mat_Proto_Ceiling_1m" || n.Contains("Ceiling") || n.Contains("Beam") || n.Contains("Chasm_Bottom"))
                    {
                        r.sharedMaterial = matCeiling;
                    }
                    else if (matName == "Mat_Proto_Wall_1m" || n.Contains("Wall") || n.Contains("Octagon") || n.Contains("Pillar") || n.Contains("Crate") || n.Contains("Bancada_Base"))
                    {
                        r.sharedMaterial = matWall;
                    }
                }
            }
        }

        private static int DressZonesWithKit40Prefabs()
        {
            var pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/caldeiral_env_portal_pillar.prefab");
            var pipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/caldeiral_env_vapor_pipe.prefab");
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/caldeiral_env_wall_panel.prefab");
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/caldeiral_env_floor_tile.prefab");

            int count = 0;

            GameObject Place(GameObject prefab, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 targetMeters)
            {
                if (prefab == null || parent == null) return null;
                // Container pai preserva localRotation (-90X) e localScale (100,100,100) nativos do prefab FBX!
                var container = new GameObject(name);
                container.transform.SetParent(parent, false);
                container.transform.position = pos;
                container.transform.rotation = Quaternion.Euler(euler);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);

                // Normaliza dimensao real em metros preservando proporcao ou targetMeters e apoia a base em pos.y
                var b = ProceduralMeshLibrary.ComputeBounds(container);
                if (b.size.x > 0.001f && b.size.y > 0.001f && b.size.z > 0.001f)
                {
                    container.transform.localScale = new Vector3(
                        targetMeters.x / b.size.x,
                        targetMeters.y / b.size.y,
                        targetMeters.z / b.size.z
                    );
                    b = ProceduralMeshLibrary.ComputeBounds(container);
                    container.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
                }

                foreach (var t in container.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(
                        t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic
                    );
                }
                count++;
                return container;
            }

            // === Z1 — OFICINA DA SUCATA (6 pecas Kit 4.0) ===
            var z1 = GameObject.Find("Z1_Oficina_da_Sucata")?.transform;
            if (z1 != null)
            {
                var d1 = new GameObject("Z1_C4_Kit40_Dressing").transform;
                d1.SetParent(z1, false);
                Place(pillarPrefab, "Z1_Kit_Pillar_ArchS", d1, new Vector3(-22.7f, 0.0f, -20.5f), new Vector3(0, 0, 0), new Vector3(0.85f, 3.6f, 0.85f));
                Place(pillarPrefab, "Z1_Kit_Pillar_ArchN", d1, new Vector3(-22.7f, 0.0f, -15.5f), new Vector3(0, 180, 0), new Vector3(0.85f, 3.6f, 0.85f));
                Place(pipePrefab,   "Z1_Kit_VaporPipe_Bancada", d1, new Vector3(-29.0f, 3.2f, -12.8f), new Vector3(0, 0, 0), new Vector3(1.1f, 0.9f, 0.9f));
                Place(wallPrefab,   "Z1_Kit_WallPanel_West", d1, new Vector3(-35.6f, 1.3f, -18.0f), new Vector3(0, 90, 0), new Vector3(1.3f, 1.3f, 1.0f));
                Place(wallPrefab,   "Z1_Kit_WallPanel_South", d1, new Vector3(-29.0f, 1.3f, -23.6f), new Vector3(0, 0, 0), new Vector3(1.3f, 1.3f, 1.0f));
                Place(floorPrefab,  "Z1_Kit_FloorMedallion_I1", d1, new Vector3(-29.0f, 0.02f, -17.5f), new Vector3(0, 0, 0), new Vector3(1.5f, 0.03f, 1.5f));
            }

            // === Z2 — PASSARELA DA BIGORNA (8 pecas Kit 4.0) ===
            var z2 = GameObject.Find("Z2_Passarela_da_Bigorna")?.transform;
            if (z2 != null)
            {
                var d2 = new GameObject("Z2_C4_Kit40_Dressing").transform;
                d2.SetParent(z2, false);
                Place(pillarPrefab, "Z2_Kit_Pillar_SW", d2, new Vector3(-16.0f, 0.0f, -24.1f), new Vector3(0, 0, 0), new Vector3(0.95f, 4.4f, 0.95f));
                Place(pillarPrefab, "Z2_Kit_Pillar_SE", d2, new Vector3(-2.5f,  0.0f, -24.1f), new Vector3(0, 90, 0), new Vector3(0.95f, 4.4f, 0.95f));
                Place(pillarPrefab, "Z2_Kit_Pillar_ArchZ3_S", d2, new Vector3(-0.7f, 0.0f, -20.5f), new Vector3(0, 0, 0), new Vector3(0.85f, 3.8f, 0.85f));
                Place(pillarPrefab, "Z2_Kit_Pillar_ArchZ3_N", d2, new Vector3(-0.7f, 0.0f, -15.5f), new Vector3(0, 180, 0), new Vector3(0.85f, 3.8f, 0.85f));
                Place(pipePrefab,   "Z2_Kit_VaporPipe_TopW", d2, new Vector3(-12.5f, 5.4f, -11.9f), new Vector3(0, 0, 0), new Vector3(1.25f, 1.0f, 1.0f));
                Place(pipePrefab,   "Z2_Kit_VaporPipe_TopE", d2, new Vector3(-4.5f,  5.4f, -11.9f), new Vector3(0, 0, 0), new Vector3(1.25f, 1.0f, 1.0f));
                Place(wallPrefab,   "Z2_Kit_WallPanel_South", d2, new Vector3(-9.0f, 1.5f, -24.6f), new Vector3(0, 0, 0), new Vector3(1.5f, 1.5f, 1.0f));
                Place(floorPrefab,  "Z2_Kit_FloorMedallion_Tio", d2, new Vector3(-13.8f, 0.02f, -20.5f), new Vector3(0, 0, 0), new Vector3(1.2f, 0.03f, 1.2f));
            }

            // === Z3 — PATIO DAS SIRENES + GALERIA DE FUMACA (8 pecas Kit 4.0) ===
            var z3 = GameObject.Find("Z3_Patio_das_Sirenes")?.transform;
            if (z3 != null)
            {
                var d3 = new GameObject("Z3_C4_Kit40_Dressing").transform;
                d3.SetParent(z3, false);
                Place(pillarPrefab, "Z3_Kit_Pillar_PatioE", d3, new Vector3(29.0f, 3.5f, -16.8f), new Vector3(0, 0, 0), new Vector3(0.95f, 4.6f, 0.95f));
                Place(pillarPrefab, "Z3_Kit_Pillar_BridgeS", d3, new Vector3(29.0f, 3.5f, 2.5f),  new Vector3(0, 90, 0), new Vector3(0.95f, 4.6f, 0.95f));
                Place(pillarPrefab, "Z3_Kit_Pillar_BridgeN", d3, new Vector3(29.0f, 3.5f, 9.2f),  new Vector3(0, 180, 0), new Vector3(0.95f, 4.6f, 0.95f));
                Place(pillarPrefab, "Z3_Kit_Pillar_ArchZ4", d3, new Vector3(16.7f, 3.5f, 5.7f),   new Vector3(0, 0, 0), new Vector3(0.85f, 3.8f, 0.85f));
                Place(pipePrefab,   "Z3_Kit_VaporPipe_ChasmA", d3, new Vector3(21.0f, -2.3f, 5.75f), new Vector3(0, 0, 0), new Vector3(1.6f, 1.3f, 1.3f));
                Place(pipePrefab,   "Z3_Kit_VaporPipe_Gallery", d3, new Vector3(17.2f, 4.8f, -5.0f), new Vector3(0, 90, 0), new Vector3(1.4f, 1.1f, 1.1f));
                Place(wallPrefab,   "Z3_Kit_WallPanel_Vo", d3, new Vector3(24.5f, 4.9f, -23.5f), new Vector3(0, 0, 0), new Vector3(1.6f, 1.6f, 1.0f));
                Place(floorPrefab,  "Z3_Kit_FloorMedallion_Vo", d3, new Vector3(22.0f, 3.52f, -18.5f), new Vector3(0, 0, 0), new Vector3(1.3f, 0.03f, 1.3f));
            }

            // === Z4 — CAMARA OCTOGONAL DO CALDEIRAL CENTRAL (9 pecas Kit 4.0) ===
            var z4 = GameObject.Find("Z4_Camara_Caldeiral_Central")?.transform;
            if (z4 != null)
            {
                var d4 = new GameObject("Z4_C4_Kit40_Dressing").transform;
                d4.SetParent(z4, false);
                Place(pillarPrefab, "Z4_Kit_Pillar_NoL", d4, new Vector3(-3.2f, 3.5f, 0.6f),  new Vector3(0, 0, 0), new Vector3(0.9f, 4.2f, 0.9f));
                Place(pillarPrefab, "Z4_Kit_Pillar_NoR", d4, new Vector3(3.2f,  3.5f, 0.6f),  new Vector3(0, 180, 0), new Vector3(0.9f, 4.2f, 0.9f));
                Place(pillarPrefab, "Z4_Kit_Pillar_ExitS", d4, new Vector3(-10.2f, 3.5f, 5.6f), new Vector3(0, 90, 0), new Vector3(0.9f, 4.0f, 0.9f));
                Place(pillarPrefab, "Z4_Kit_Pillar_ExitN", d4, new Vector3(-10.2f, 3.5f, 11.4f), new Vector3(0, -90, 0), new Vector3(0.9f, 4.0f, 0.9f));
                Place(pipePrefab,   "Z4_Kit_VaporPipe_ValvA", d4, new Vector3(-4.2f, 0.0f, 5.5f), new Vector3(0, -35, 0), new Vector3(0.95f, 0.85f, 0.85f));
                Place(pipePrefab,   "Z4_Kit_VaporPipe_ValvB", d4, new Vector3(4.2f,  0.0f, 5.5f), new Vector3(0, 35, 0), new Vector3(0.95f, 0.85f, 0.85f));
                Place(pipePrefab,   "Z4_Kit_VaporPipe_ValvC", d4, new Vector3(0.0f,  0.0f, 11.1f), new Vector3(0, 90, 0), new Vector3(0.95f, 0.85f, 0.85f));
                Place(wallPrefab,   "Z4_Kit_WallPanel_North", d4, new Vector3(0.0f, 2.2f, 17.5f), new Vector3(0, 180, 0), new Vector3(2.0f, 2.0f, 1.0f));
                Place(floorPrefab,  "Z4_Kit_FloorMedallion_I5", d4, new Vector3(0.0f, 3.52f, 0.5f), new Vector3(0, 0, 0), new Vector3(1.5f, 0.03f, 1.5f));
            }

            // === Z5 — GALERIA DE RETORNO SYNC + MIRANTE (5 pecas Kit 4.0) ===
            var z5 = GameObject.Find("Z5_Galeria_Retorno_Sync")?.transform;
            if (z5 != null)
            {
                var d5 = new GameObject("Z5_C4_Kit40_Dressing").transform;
                d5.SetParent(z5, false);
                Place(pillarPrefab, "Z5_Kit_Pillar_GateS", d5, new Vector3(-23.2f, 3.5f, 5.6f),  new Vector3(0, 0, 0), new Vector3(0.85f, 3.8f, 0.85f));
                Place(pillarPrefab, "Z5_Kit_Pillar_GateN", d5, new Vector3(-23.2f, 3.5f, 12.4f), new Vector3(0, 180, 0), new Vector3(0.85f, 3.8f, 0.85f));
                Place(pipePrefab,   "Z5_Kit_VaporPipe_Sync", d5, new Vector3(-18.0f, 7.2f, 13.1f), new Vector3(0, 0, 0), new Vector3(1.25f, 1.0f, 1.0f));
                Place(wallPrefab,   "Z5_Kit_WallPanel_MiranteW", d5, new Vector3(-31.6f, 5.0f, -4.0f), new Vector3(0, 90, 0), new Vector3(1.5f, 1.5f, 1.0f));
                Place(floorPrefab,  "Z5_Kit_FloorMedallion_I6", d5, new Vector3(-24.0f, 3.52f, 9.0f), new Vector3(0, 0, 0), new Vector3(1.4f, 0.03f, 1.4f));
            }

            return count;
        }

        private static void CalibrateCaldeiralLighting()
        {
            // Atmosfera clockwork-industrial quente (sem adicionar 1 luz extra — mantem 5/5 luzes reais URP!)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.21f, 0.15f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.15f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.08f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.13f, 0.10f, 0.08f);
            RenderSettings.fogDensity = 0.010f;

            void Tune(string name, Color col, float intensity, float range = -1f)
            {
                var go = GameObject.Find(name);
                if (go == null) return;
                var l = go.GetComponent<Light>();
                if (l == null) return;
                l.color = col;
                l.intensity = intensity;
                if (range > 0f) l.range = range;
            }

            Tune("L0_Directional_Desync",      new Color(1.00f, 0.84f, 0.62f), 0.95f);
            Tune("L1_Oficina_Warm",            new Color(1.00f, 0.71f, 0.34f), 26.0f, 18.0f);
            Tune("L2_Z2_Passarela_Key",        new Color(0.96f, 0.68f, 0.38f), 34.0f, 26.0f);
            Tune("L3_Z3_Patio_Sirenes",        new Color(0.48f, 0.76f, 0.88f), 46.0f, 34.0f);
            Tune("L4_Z4_SireneMestra_Zenital", new Color(1.00f, 0.66f, 0.24f), 68.0f, 44.0f);
        }

        private static Material EnsureC4PbrMaterial(
            string matName, string albedoPath, string normalPath,
            Vector2 tiling, Color tint, float metallic, float smoothness, float bumpScale)
        {
            var albImp = AssetImporter.GetAtPath(albedoPath) as TextureImporter;
            if (albImp != null)
            {
                albImp.wrapMode = TextureWrapMode.Repeat;
                albImp.filterMode = FilterMode.Trilinear;
                albImp.anisoLevel = 8;
                albImp.SaveAndReimport();
            }
            var nrmImp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (nrmImp != null)
            {
                nrmImp.textureType = TextureImporterType.NormalMap;
                nrmImp.wrapMode = TextureWrapMode.Repeat;
                nrmImp.filterMode = FilterMode.Trilinear;
                nrmImp.anisoLevel = 8;
                nrmImp.SaveAndReimport();
            }

            var albTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var nrmTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string matPath = $"{C4MatDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetTexture("_BaseMap", albTex);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (nrmTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", nrmTex);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", bumpScale);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CaptureC41ConsolidatedCheckpoints(Camera cam, int kitInstances)
        {
            string dateOnly = DateTime.Now.ToString("yyyy-MM-dd");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string reviewDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "director_review", $"{dateOnly}_c4_1_caldeiral_vestido");
            string capDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(reviewDir);
            Directory.CreateDirectory(capDir);

            (string slug, Vector3 pos, Vector3 look)[] shots =
            {
                ("c4_1_z1_oficina_sucata",     new Vector3(-35.2f, 3.2f, -22.6f), new Vector3(-27.5f, 1.4f, -17.4f)),
                ("c4_1_z2_passarela_bigorna",  new Vector3(-17.2f, 4.7f, -23.6f), new Vector3(-6.2f,  1.8f, -17.2f)),
                ("c4_1_z3_patio_sirenes",      new Vector3(17.5f,  7.2f, -22.5f), new Vector3(25.0f,  4.0f, -1.0f)),
                ("c4_1_z4_camara_caldeiral",   new Vector3(0.0f,   6.2f, -3.2f),  new Vector3(0.0f,   5.8f,  7.0f)),
                ("c4_1_z5_galeria_retorno",    new Vector3(-12.5f, 6.4f,  9.0f),  new Vector3(-26.0f, 4.8f,  8.8f)),
                ("c4_1_z5_mirante_visada_z1",  new Vector3(-29.0f, 5.2f, -9.8f),  new Vector3(-29.0f, 1.2f, -18.5f)),
            };

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
                $"O QUE É: Checkpoint consolidado da Fase 4.1 — Caldeiral vestido (Z1..Z5 + Mirante sobre Z1 + walkthrough MP4 + folha 4x4) usando o Kit 4.0 aprovado ({kitInstances} instâncias TripoSG+Hunyuan PBR + materiais PBR world-scale 1 tile = 1.25-2.0m + iluminação real 5/5 luzes URP).\n" +
                "O QUE VOCÊ DECIDE: Aprovar a vestimenta visual e iluminação do conjunto Z1..Z5 para liberar o estágio C5 (substituição in-place dos heróis/props pela Fábrica 3D) e ajustar a densidade de peças/m² se desejar.\n" +
                "O QUE ACONTECE DEPOIS: Com C4 aprovado em PROGRESS.md, iniciamos o C5 substituindo Porca, Dez, Sirene-Mestra, Válvulas, Nó e inimigos no mesmo transform.\n";
            File.WriteAllText(Path.Combine(reviewDir, "LEIA-ME.txt"), readme);
            Debug.Log($"[CaldeiralC4.1] Capturas consolidadas salvas em: {reviewDir}");
        }

        private static void CaptureC41WalkthroughFrames(Camera cam)
        {
            string framesDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures", "walkthrough_c4_1_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            (Vector3 pos, Vector3 look)[] stops =
            {
                (new Vector3(-35.0f, 2.8f, -22.2f), new Vector3(-28.5f, 1.3f, -17.5f)),
                (new Vector3(-30.5f, 2.2f, -19.8f), new Vector3(-22.0f, 2.2f, -18.0f)),
                (new Vector3(-20.0f, 2.2f, -18.0f), new Vector3(-10.0f, 2.2f, -18.0f)),
                (new Vector3(-16.8f, 3.2f, -23.5f), new Vector3(-7.0f,  2.0f, -17.5f)),
                (new Vector3(-10.0f, 3.5f, -18.5f), new Vector3(0.0f,   2.4f, -18.0f)),
                (new Vector3(3.0f,   2.2f, -18.0f), new Vector3(14.0f,  2.8f, -18.0f)),
                (new Vector3(11.0f,  3.6f, -21.8f), new Vector3(22.0f,  4.2f, -18.5f)),
                (new Vector3(25.5f,  5.4f, -16.5f), new Vector3(26.5f,  4.8f,  2.0f)),
                (new Vector3(26.5f,  5.4f,  1.5f),  new Vector3(26.5f,  3.2f,  8.5f)),
                (new Vector3(20.0f,  5.4f,  8.8f),  new Vector3(11.0f,  5.2f,  8.5f)),
                (new Vector3(8.5f,   6.2f,  8.5f),  new Vector3(0.0f,   6.5f,  7.0f)),
                (new Vector3(0.0f,   6.2f, -3.0f),  new Vector3(0.0f,   5.8f,  7.0f)),
                (new Vector3(-5.0f,  2.2f,  2.0f),  new Vector3(0.0f,   2.2f,  9.5f)),
                (new Vector3(-9.5f,  5.6f,  8.5f),  new Vector3(-20.0f, 5.0f,  9.0f)),
                (new Vector3(-14.0f, 6.0f,  9.0f),  new Vector3(-25.0f, 4.8f,  9.0f)),
                (new Vector3(-29.0f, 5.2f, -9.8f),  new Vector3(-29.0f, 1.2f, -18.5f)),
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
            Debug.Log($"[CaldeiralC4.1] {frameIdx} frames do walkthrough C4.1 gerados em {framesDir}");
        }
    }
}
