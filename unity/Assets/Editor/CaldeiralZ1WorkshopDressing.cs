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
    /// Monta a baia de manutencao da nave espacial (TripoSG + PBR) com cabos/fios saindo,
    /// plataforma de manutencao e cristais energeticos alienigenas brilhantes (TripoSG + PBR)
    /// com sentido narrativo na Oficina Z1 (alimentando a nave, na prateleira e na bancada).
    /// </summary>
    public static class CaldeiralZ1WorkshopDressing
    {
        private const string ScenePath   = "Assets/Game/Scenes/Caldeiral.unity";
        private const string ArtDir      = "Assets/Game/Art/Generated/z1_workshop";
        private const string MatDir      = "Assets/Game/Materials/Fab3D";
        private const string PrefabDir   = "Assets/Game/Prefabs/Fab3D";
        private const string C4MatDir    = "Assets/Game/Materials/CaldeiralC4";

        private const string ShipSlug    = "z1_prop_nave_manutencao";
        private const string CrystalSlug = "z1_prop_cristal_energia";

        [MenuItem("Studio/Concerto/Caldeiral/Build Z1 Workshop Spaceship Bay + Alien Crystals")]
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
                Debug.LogError($"[CaldeiralZ1Workshop] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Cria materiais PBR + emissao ciano controlada (nucleo da nave + cristais!)
            var matShip    = EnsureFab3dPropMaterial(ShipSlug,    emissionHdr: new Color(0.20f, 2.80f, 3.20f), smoothnessMul: 0.72f);
            var matCrystal = EnsureFab3dPropMaterial(CrystalSlug, emissionHdr: new Color(0.15f, 3.40f, 3.80f), smoothnessMul: 0.85f);
            var matCable   = EnsureSimpleLitMaterial($"{C4MatDir}/Mat_Z1_PowerCable.mat", new Color(0.12f, 0.13f, 0.15f), 0.25f, 0.45f, Color.black);
            var matCopper  = EnsureSimpleLitMaterial($"{C4MatDir}/Mat_Z1_WireCopper.mat", new Color(0.75f, 0.42f, 0.20f), 0.78f, 0.55f, Color.black);
            var matHazard  = EnsureSimpleLitMaterial($"{C4MatDir}/Mat_Z1_HazardYellow.mat", new Color(0.86f, 0.64f, 0.12f), 0.18f, 0.32f, Color.black);
            var matSteel   = EnsureSimpleLitMaterial($"{C4MatDir}/Mat_Z1_CradleSteel.mat", new Color(0.24f, 0.25f, 0.27f), 0.68f, 0.40f, Color.black);
            var matSpark   = EnsureSimpleLitMaterial($"{C4MatDir}/Mat_Z1_CyanGlowTip.mat", new Color(0.4f, 0.95f, 1.0f), 0.0f, 0.2f, new Color(0.3f, 3.8f, 4.5f));

            // 2. Cria prefabs limpos dos 2 assets TripoSG
            var shipPrefab    = CreateOrUpdatePropPrefab(ShipSlug, matShip);
            var crystalPrefab = CreateOrUpdatePropPrefab(CrystalSlug, matCrystal);

            // 3. Monta container idempotente na Oficina Z1
            var z1 = GameObject.Find("Z1_Oficina_da_Sucata")?.transform;
            var oldBay = GameObject.Find("Z1_Spaceship_And_Crystals");
            if (oldBay != null) UnityEngine.Object.DestroyImmediate(oldBay);

            var root = new GameObject("Z1_Spaceship_And_Crystals");
            if (z1 != null) root.transform.SetParent(z1, false);

            // =========================================================================
            // A. BAIA DE MANUTENCAO DA NAVE ESPACIAL (Canto Sul-Leste da Oficina Z1)
            //    Posicao: (-26.0, 0, -21.2) — bem visivel do spawn e SEM bloquear o portal!
            // =========================================================================
            var bay = new GameObject("MaintenanceBay_Ship");
            bay.transform.SetParent(root.transform, false);
            bay.transform.position = new Vector3(-26.0f, 0.0f, -21.2f);
            bay.transform.rotation = Quaternion.Euler(0f, -32f, 0f); // Angulada pra mostrar a escotilha aberta pro jogador!

            // Plataforma / berco industrial de manutencao
            CreateBox("Bay_PlatformBase", bay.transform, new Vector3(0f, 0.05f, 0f), new Vector3(3.4f, 0.10f, 2.3f), matSteel, collider: true);
            CreateBox("Bay_HazardStrip_Front", bay.transform, new Vector3(0f, 0.055f, 1.12f), new Vector3(3.42f, 0.105f, 0.12f), matHazard, collider: false);
            CreateBox("Bay_HazardStrip_Back",  bay.transform, new Vector3(0f, 0.055f, -1.12f), new Vector3(3.42f, 0.105f, 0.12f), matHazard, collider: false);
            // 2 cavaletes hidraulicos apoiando a nave
            CreateBox("Bay_Jack_Front", bay.transform, new Vector3(-0.75f, 0.20f, 0f), new Vector3(0.28f, 0.22f, 1.35f), matSteel, collider: true);
            CreateBox("Bay_Jack_Rear",  bay.transform, new Vector3( 0.75f, 0.20f, 0f), new Vector3(0.28f, 0.22f, 1.35f), matSteel, collider: true);

            // Nave espacial TripoSG em cima dos cavaletes (2.85m de comprimento!)
            var shipInst = PlaceNormalizedProp(shipPrefab, "Z1_Nave_Em_Manutencao", bay.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0f, 180f, 0f), targetMaxDim: 2.85f, addCollider: true);

            // Caixa de diagnostico / gerador auxiliar ao lado da nave
            CreateBox("Bay_DiagnosticUnit", bay.transform, new Vector3(-1.55f, 0.42f, 0.85f), new Vector3(0.65f, 0.74f, 0.50f), matSteel, collider: true);
            CreateBox("Bay_DiagnosticScreen", bay.transform, new Vector3(-1.55f, 0.62f, 1.11f), new Vector3(0.42f, 0.22f, 0.03f), matSpark, collider: false);

            // Cristal Energetico Alienigena GRANDE ao lado da nave (alimentando o nucleo via cabo!)
            PlaceNormalizedProp(crystalPrefab, "Z1_Cristal_Bateria_Nave", bay.transform, new Vector3(1.15f, 0.10f, 0.85f), new Vector3(0f, 25f, 0f), targetMaxDim: 0.78f, addCollider: true);

            // CABOS / FIOS SAINDO DA ESCOTILHA ABERTA DA NAVE!
            // Cabo 1: Grosso de energia ligando o motor aberto da nave ao Cristal Bateria
            CreateCurvedCable(bay.transform, "Cable_ShipToCrystal",
                new Vector3(0.15f, 0.92f, 0.58f),
                new Vector3(0.65f, 0.40f, 0.95f),
                new Vector3(1.12f, 0.38f, 0.85f),
                radius: 0.032f, matCable);

            // Cabo 2: Cabo de diagnostico ligando a escotilha aberta a caixa de diagnostico
            CreateCurvedCable(bay.transform, "Cable_ShipToDiagnostic",
                new Vector3(-0.12f, 0.88f, 0.60f),
                new Vector3(-0.85f, 0.22f, 1.05f),
                new Vector3(-1.35f, 0.55f, 0.88f),
                radius: 0.026f, matCopper);

            // Cabo 3: Fio solto pendurado da escotilha ate o chao com ponta brilhante (faisca/energia)
            CreateCurvedCable(bay.transform, "Cable_LooseHangingWire",
                new Vector3(0.02f, 0.82f, 0.62f),
                new Vector3(0.18f, 0.35f, 0.98f),
                new Vector3(0.32f, 0.11f, 1.12f),
                radius: 0.018f, matCopper);
            var sparkTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sparkTip.name = "Wire_GlowTip";
            sparkTip.transform.SetParent(bay.transform, false);
            sparkTip.transform.localPosition = new Vector3(0.32f, 0.12f, 1.12f);
            sparkTip.transform.localScale = Vector3.one * 0.07f;
            UnityEngine.Object.DestroyImmediate(sparkTip.GetComponent<Collider>());
            sparkTip.GetComponent<MeshRenderer>().sharedMaterial = matSpark;

            // Luz ciano suave emanando do nucleo aberto da nave + cristal bateria
            CreateAccentLight(bay.transform, "Light_Bay_AlienCyan", new Vector3(0.65f, 0.85f, 0.95f), new Color(0.22f, 0.90f, 1.00f), intensity: 7.5f, range: 6.5f);

            // =========================================================================
            // B. CRISTAIS ENERGETICOS NA ESTANTE INDUSTRIAL DE 3 NIVEIS (X=-29, Z=-12.6)
            //    Prateleiras em Y=0.69, Y=1.49, Y=2.29 (CaldeiralC3LayoutRefiner.cs)
            // =========================================================================
            PlaceNormalizedProp(crystalPrefab, "Z1_Shelf_Cristal_TopL", root.transform, new Vector3(-29.85f, 2.29f, -12.60f), new Vector3(0f,  18f, 0f), 0.52f, addCollider: false);
            PlaceNormalizedProp(crystalPrefab, "Z1_Shelf_Cristal_TopR", root.transform, new Vector3(-28.15f, 2.29f, -12.60f), new Vector3(0f, -35f, 0f), 0.48f, addCollider: false);
            PlaceNormalizedProp(crystalPrefab, "Z1_Shelf_Cristal_MidC", root.transform, new Vector3(-29.05f, 1.49f, -12.55f), new Vector3(0f,  55f, 0f), 0.58f, addCollider: false);
            PlaceNormalizedProp(crystalPrefab, "Z1_Shelf_Cristal_MidL", root.transform, new Vector3(-30.05f, 1.49f, -12.62f), new Vector3(0f, 110f, 0f), 0.42f, addCollider: false);
            PlaceNormalizedProp(crystalPrefab, "Z1_Shelf_Cristal_LowR", root.transform, new Vector3(-28.25f, 0.69f, -12.58f), new Vector3(0f, -70f, 0f), 0.50f, addCollider: false);

            CreateAccentLight(root.transform, "Light_Shelf_AlienCyan", new Vector3(-29.0f, 1.95f, -13.35f), new Color(0.20f, 0.92f, 1.00f), intensity: 7.0f, range: 6.0f);

            // =========================================================================
            // C. CRISTAL EM ANALISE NA BANCADA (I1_Bancada_Relogio)
            // =========================================================================
            var bancada = GameObject.Find("I1_Bancada_Relogio");
            if (bancada != null)
            {
                Bounds bb = ComputeHierarchyBounds(bancada);
                PlaceNormalizedProp(crystalPrefab, "Z1_Bancada_Cristal_Amostra", root.transform, new Vector3(bb.center.x + 0.35f, bb.max.y - 0.12f, bb.center.z), new Vector3(0f, -20f, 0f), 0.32f, addCollider: false);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 4. Captura 2 fotos de verificacao (visao geral da oficina + close-up da nave com cabos e cristais!)
            CapturePreview(
                new Vector3(-34.6f, 2.6f, -22.2f),
                new Vector3(-26.8f, 1.2f, -19.2f),
                "caldeiral_z1_workshop_overview.png"
            );
            CapturePreview(
                new Vector3(-29.2f, 1.65f, -18.5f),
                new Vector3(-26.0f, 0.85f, -21.0f),
                "caldeiral_z1_spaceship_closeup.png"
            );

            Debug.Log("[CaldeiralZ1Workshop] OK! Spaceship maintenance bay + cables + glowing alien crystals installed.");
        }

        private static GameObject FindShelfInZ1()
        {
            var z1 = GameObject.Find("Z1_Oficina_da_Sucata");
            if (z1 == null) return null;
            foreach (var t in z1.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf("Shelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Estante", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Rack", StringComparison.OrdinalIgnoreCase) >= 0)
                    return t.gameObject;
            }
            return null;
        }

        private static Bounds ComputeHierarchyBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Material EnsureFab3dPropMaterial(string slug, Color emissionHdr, float smoothnessMul)
        {
            string albPath = $"{MatDir}/{slug}_albedo.png";
            string msPath  = $"{MatDir}/{slug}_metallicsmooth.png";
            string emPath  = $"{MatDir}/{slug}_emission.png";
            string matPath = $"{MatDir}/Mat_Fab3D_{slug}.mat";

            ConfigureTex(albPath);
            ConfigureTex(msPath);
            ConfigureTex(emPath);

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
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Smoothness", smoothnessMul);
            }
            if (em != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", em);
                mat.SetColor("_EmissionColor", emissionHdr);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.maxTextureSize = 2048;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }

        private static Material EnsureSimpleLitMaterial(string path, Color baseCol, float metallic, float smoothness, Color emission)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseCol);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrUpdatePropPrefab(string slug, Material mat)
        {
            string fbxPath = $"{ArtDir}/{slug}.fbx";
            string prefabPath = $"{PrefabDir}/{slug}.prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new FileNotFoundException($"FBX nao encontrado: {fbxPath}");

            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, prefabPath);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved;
        }

        private static GameObject PlaceNormalizedProp(GameObject prefab, string name, Transform parent, Vector3 localPos, Vector3 localEuler, float targetMaxDim, bool addCollider)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.transform.localPosition = localPos;
            container.transform.localRotation = Quaternion.Euler(localEuler);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            Bounds b = ComputeHierarchyBounds(container);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim > 0.0001f)
            {
                float s = targetMaxDim / maxDim;
                container.transform.localScale = Vector3.one * s;
            }
            // Re-ancora a base exatamente no localPos.y
            b = ComputeHierarchyBounds(container);
            float desiredWorldY = parent.TransformPoint(localPos).y;
            container.transform.position += new Vector3(0f, desiredWorldY - b.min.y, 0f);

            if (addCollider)
            {
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }
            return container;
        }

        private static void CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void CreateCurvedCable(Transform parent, string name, Vector3 p0, Vector3 p1, Vector3 p2, float radius, Material mat)
        {
            int segments = 14;
            int sides = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            Vector3 Eval(float t)
            {
                float u = 1f - t;
                return u * u * p0 + 2f * u * t * p1 + t * t * p2;
            }

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 center = Eval(t);
                Vector3 fwd = (Eval(Mathf.Min(1f, t + 0.02f)) - Eval(Mathf.Max(0f, t - 0.02f))).normalized;
                if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
                Vector3 right = Vector3.Cross(up, fwd).normalized;
                up = Vector3.Cross(fwd, right).normalized;

                for (int s = 0; s < sides; s++)
                {
                    float ang = (s / (float)sides) * Mathf.PI * 2f;
                    verts.Add(center + (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                int rowA = i * sides;
                int rowB = (i + 1) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int sn = (s + 1) % sides;
                    tris.Add(rowA + s); tris.Add(rowB + s); tris.Add(rowB + sn);
                    tris.Add(rowA + s); tris.Add(rowB + sn); tris.Add(rowA + sn);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        private static void CreateAccentLight(Transform parent, string name, Vector3 localPos, Color col, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static void CapturePreview(Vector3 camPos, Vector3 lookAt, string filename)
        {
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            Vector3 oldPos = cam.transform.position;
            Quaternion oldRot = cam.transform.rotation;

            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string outPath = Path.Combine(repoRoot, "evidence", "captures", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            cam.transform.position = oldPos;
            cam.transform.rotation = oldRot;
        }
    }
}
