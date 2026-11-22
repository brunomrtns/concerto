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

        private static Material _matFloor;
        private static Material _matWall;
        private static Material _matCeiling;
        private static Material _matAccent;
        private static Material _matPlayer;

        public static void BuildZ1AndCaptureBatch()
        {
            try
            {
                BuildZ1OficinaSucata();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC2] Erro fatal no build Z1: {ex}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Studio/Concerto/C2 Blockout/Build Z1 - Oficina da Sucata")]
        public static void BuildZ1OficinaSucata()
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.18f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.14f, 0.12f);
            RenderSettings.fogDensity = 0.014f;

            // === LIGHTING (Budget Android URP <= 5 real lights; Z1 usa 3 luzes) ===
            var lightsRoot = new GameObject("Lighting").transform;
            CreateDirectionalLight(lightsRoot, "L0_Directional_Desync", Quaternion.Euler(52f, -35f, 0f), new Color(0.75f, 0.68f, 0.60f), 0.75f);
            CreatePointLight(lightsRoot, "L1_Oficina_Warm", new Vector3(-29.0f, 5.2f, -18.0f), new Color(1.0f, 0.74f, 0.42f), 18f, 14f);
            CreatePointLight(lightsRoot, "L2_Arco_Guide", new Vector3(-20.0f, 4.2f, -18.0f), new Color(0.78f, 0.88f, 0.96f), 12f, 12f);

            // === Z1: OFICINA DA SUCATA (14m x 12m, X: -36..-22, Z: -24..-12, Teto Y=8.0m, 100% FECHADO) ===
            var z1Root = new GameObject("Z1_Oficina_da_Sucata").transform;

            // Piso 14m x 12m (topo exatamente em Y=0.0)
            CreateWorldBox("Z1_Floor_14x12", z1Root, new Vector3(-29f, -0.25f, -18f), new Vector3(14f, 0.5f, 12f), _matFloor, isStatic: true, addCollider: true);
            // Teto 14m x 12m (base em Y=8.0)
            CreateWorldBox("Z1_Ceiling_14x12", z1Root, new Vector3(-29f, 8.25f, -18f), new Vector3(14f, 0.5f, 12f), _matCeiling, isStatic: true, addCollider: false);

            // Vigas estruturais no teto (Y=7.6) para leitura arquitetonica imediata
            CreateWorldBox("Z1_CeilingBeam_A", z1Root, new Vector3(-32.5f, 7.65f, -18f), new Vector3(0.6f, 0.7f, 12f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z1_CeilingBeam_B", z1Root, new Vector3(-25.5f, 7.65f, -18f), new Vector3(0.6f, 0.7f, 12f), _matCeiling, isStatic: true, addCollider: false);

            // Paredes fechadas (H=8.0m)
            // Parede Oeste (X=-36)
            CreateWorldBox("Z1_Wall_West", z1Root, new Vector3(-36.25f, 4f, -18f), new Vector3(0.5f, 8f, 12f), _matWall, isStatic: true, addCollider: true);
            // Parede Norte (Z=-12)
            CreateWorldBox("Z1_Wall_North", z1Root, new Vector3(-29f, 4f, -11.75f), new Vector3(14f, 8f, 0.5f), _matWall, isStatic: true, addCollider: true);
            // Parede Sul (Z=-24)
            CreateWorldBox("Z1_Wall_South", z1Root, new Vector3(-29f, 4f, -24.25f), new Vector3(14f, 8f, 0.5f), _matWall, isStatic: true, addCollider: true);

            // Parede Leste (X=-22) com Arco 4m (Z: -20..-16, altura 4.5m) -> fechada acima e nas laterais
            CreateWorldBox("Z1_Wall_East_South", z1Root, new Vector3(-21.75f, 4f, -22f), new Vector3(0.5f, 8f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_East_North", z1Root, new Vector3(-21.75f, 4f, -14f), new Vector3(0.5f, 8f, 4f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Wall_East_Header", z1Root, new Vector3(-21.75f, 6.25f, -18f), new Vector3(0.5f, 3.5f, 4f), _matWall, isStatic: true, addCollider: true);
            // Moldura do Arco 4m (destaque ambar)
            CreateWorldBox("Z1_Arch_Pillar_S", z1Root, new Vector3(-22.0f, 2.25f, -20.15f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Arch_Pillar_N", z1Root, new Vector3(-22.0f, 2.25f, -15.85f), new Vector3(0.7f, 4.5f, 0.4f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Arch_Lintel", z1Root, new Vector3(-22.0f, 4.7f, -18f), new Vector3(0.7f, 0.45f, 4.7f), _matAccent, isStatic: true, addCollider: true);

            // Corredor Z1->Z2 fechado (4m x 4m, X: -22..-18, Z: -20..-16) com parede provisoria ao fundo (zero void!)
            CreateWorldBox("Z1_ArchTunnel_Floor", z1Root, new Vector3(-20f, -0.25f, -18f), new Vector3(4f, 0.5f, 4f), _matFloor, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_ArchTunnel_Ceiling", z1Root, new Vector3(-20f, 4.75f, -18f), new Vector3(4f, 0.5f, 4f), _matCeiling, isStatic: true, addCollider: false);
            CreateWorldBox("Z1_ArchTunnel_WallS", z1Root, new Vector3(-20f, 2.25f, -20.25f), new Vector3(4f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_ArchTunnel_WallN", z1Root, new Vector3(-20f, 2.25f, -15.75f), new Vector3(4f, 4.5f, 0.5f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_ArchTunnel_EndBulkhead", z1Root, new Vector3(-17.75f, 2.25f, -18f), new Vector3(0.5f, 4.5f, 4.5f), _matWall, isStatic: true, addCollider: true);

            // === PROPS DA OFICINA Z1 (6 caixotes / prateleiras apoiados no piso Y=0.0, static batching) ===
            var propsRoot = new GameObject("Z1_Props_Placeholders").transform;
            propsRoot.SetParent(z1Root);
            CreateWorldBox("Z1_Crate_NW_1", propsRoot, new Vector3(-34.6f, 0.55f, -13.2f), new Vector3(1.1f, 1.1f, 1.1f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_NW_2", propsRoot, new Vector3(-33.3f, 0.45f, -13.0f), new Vector3(0.9f, 0.9f, 0.9f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Shelf_North_1", propsRoot, new Vector3(-29.0f, 1.2f, -12.6f), new Vector3(3.2f, 2.4f, 0.9f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_NE_1", propsRoot, new Vector3(-24.2f, 0.6f, -13.3f), new Vector3(1.2f, 1.2f, 1.2f), _matAccent, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_SW_1", propsRoot, new Vector3(-34.5f, 0.5f, -22.8f), new Vector3(1.2f, 1.0f, 1.0f), _matWall, isStatic: true, addCollider: true);
            CreateWorldBox("Z1_Crate_SE_1", propsRoot, new Vector3(-24.0f, 0.5f, -22.6f), new Vector3(1.4f, 1.0f, 1.0f), _matWall, isStatic: true, addCollider: true);

            // === I1: BANCADA DE RELOGIO (InteractableBase + IkHandTarget em (-29, 0, -17.5)) ===
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

            // === ENTIDADES PLACEHOLDER CONFORME §5.1 (Dez + Porca em pe Y-up, +Z frente) ===
            var entitiesRoot = new GameObject("Entities_Placeholders").transform;

            // Player Dez Placeholder (H=1.55m, pes em Y=0.0, orientado para a bancada/arco)
            var dez = new GameObject("Player_Dez_Placeholder");
            dez.transform.SetParent(entitiesRoot);
            dez.transform.position = new Vector3(-33.0f, 0.0f, -20.0f);
            dez.transform.rotation = Quaternion.Euler(0f, 65f, 0f);
            CreatePartScaled("Dez_Torso", ProceduralMeshLibrary.Capsule, dez.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.46f, 0.62f, 0.34f), _matPlayer);
            CreatePartScaled("Dez_Head", ProceduralMeshLibrary.Sphere, dez.transform, new Vector3(0f, 1.38f, 0f), new Vector3(0.34f, 0.34f, 0.34f), _matPlayer);
            CreatePartScaled("Dez_AuxArm_L", ProceduralMeshLibrary.Box, dez.transform, new Vector3(-0.32f, 0.95f, -0.18f), new Vector3(0.10f, 0.42f, 0.10f), _matAccent);
            CreatePartScaled("Dez_AuxArm_R", ProceduralMeshLibrary.Box, dez.transform, new Vector3(0.32f, 0.95f, -0.18f), new Vector3(0.10f, 0.42f, 0.10f), _matAccent);
            dez.AddComponent<PlayerInteractor>();
            dez.AddComponent<DialoguePresenter>();

            // Companion Porca Placeholder (Drone esfera Ø 0.45m, classe drone, hover a Y=1.55m)
            var porca = new GameObject("Companion_Porca_Placeholder");
            porca.transform.SetParent(entitiesRoot);
            porca.transform.position = new Vector3(-31.8f, 1.55f, -19.0f);
            porca.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
            CreatePartScaled("Porca_Shell", ProceduralMeshLibrary.Sphere, porca.transform, Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), _matAccent);
            CreatePartScaled("Porca_Lens", ProceduralMeshLibrary.Cylinder, porca.transform, new Vector3(0f, 0f, 0.22f), new Vector3(0.18f, 0.05f, 0.18f), _matPlayer);
            porca.transform.Find("Porca_Lens").localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePartScaled("Porca_Fin_L", ProceduralMeshLibrary.Box, porca.transform, new Vector3(-0.28f, 0.04f, -0.05f), new Vector3(0.16f, 0.04f, 0.14f), _matWall);
            CreatePartScaled("Porca_Fin_R", ProceduralMeshLibrary.Box, porca.transform, new Vector3(0.28f, 0.04f, -0.05f), new Vector3(0.16f, 0.04f, 0.14f), _matWall);

            // === CAMERA PRINCIPAL DE BLOCKOUT Z1 (enquadra Dez, Porca, I1 Bancada, grid 1m e Arco 4m) ===
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

            // Captura datada de Z1 (vai para evidence/captures/ e director_review/)
            CaptureZoneScreenshot(cam, "c2_z1_oficina_sucata");
            Debug.Log("[CaldeiralC2] Z1 Oficina da Sucata construida, salva e capturada com sucesso.");
        }

        private static void CaptureZoneScreenshot(Camera cam, string zoneSlug)
        {
            // Warm-up URP (3 frames)
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

            // Pasta canonica director_review/<data>_<nome>/ + LEIA-ME.txt de 3 linhas (Lei 7)
            string reviewDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "director_review", $"{dateOnly}_{zoneSlug}");
            Directory.CreateDirectory(reviewDir);
            string reviewImg = Path.Combine(reviewDir, $"{zoneSlug}_{stamp}.png");
            File.WriteAllBytes(reviewImg, pngBytes);
            File.WriteAllText(
                Path.Combine(reviewDir, "LEIA-ME.txt"),
                "O QUE É: Captura do blockout greybox C2 da Zona Z1 (Oficina da Sucata — 14m x 12m, teto 8m, grid 1 tile = 1m, Dez + Porca placeholder, Bancada I1 e Arco 4m).\n" +
                "O QUE VOCÊ DECIDE: Aprovar escala, proporção, fechamento espacial (zero void) e leitura da Z1 antes de construir a Zona Z2 (Passarela da Bigorna).\n" +
                "O QUE ACONTECE DEPOIS: Com seu OK, avançamos para o blockout C2 da Z2 mantendo o baseline PlayMode e validate_scene.py verdes.\n"
            );

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

        private static Material EnsureProtoMaterial(string matName, string texPath, float metallic, float smoothness)
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
                EditorUtility.SetDirty(existing);
                return existing;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
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
