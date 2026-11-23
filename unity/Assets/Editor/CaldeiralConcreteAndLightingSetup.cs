using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Editor
{
    /// <summary>
    /// Aplica piso de cimento industrial (preservando os caixotes com a textura atual)
    /// e instala grandes luminarias de teto na Oficina (Z1) e demais zonas do Caldeiral.
    /// </summary>
    public static class CaldeiralConcreteAndLightingSetup
    {
        private const string ScenePath   = "Assets/Game/Scenes/Caldeiral.unity";
        private const string C4MatDir    = "Assets/Game/Materials/CaldeiralC4";
        private const string GenTexDir   = "Assets/Game/Art/Textures/Generated";
        private const string ConcreteAlb = GenTexDir + "/caldeiral_piso_cimento_albedo.png";
        private const string ConcreteNrm = GenTexDir + "/caldeiral_piso_cimento_normal.png";
        private const string ConcreteMat = C4MatDir + "/Mat_C4_Floor_Concrete.mat";
        private const string HousingMat  = C4MatDir + "/Mat_C4_CeilingLight_Housing.mat";
        private const string PanelMat    = C4MatDir + "/Mat_C4_CeilingLight_Emissive.mat";

        [MenuItem("Studio/Concerto/Caldeiral/Apply Concrete Floor + Ceiling Lights")]
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
                Debug.LogError($"[CaldeiralConcreteAndLights] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Configura texturas PBR do piso de cimento industrial
            ConfigureTexture(ConcreteAlb, isNormal: false);
            ConfigureTexture(ConcreteNrm, isNormal: true);

            var matConcrete = EnsureConcreteMaterial();
            var matHousing  = EnsureHousingMaterial();
            var matEmissive = EnsureEmissivePanelMaterial();

            // 2. Troca APENAS o chao/solo/piso para cimento (PRESERVANDO caixotes, bancadas, paredes e props!)
            int floorChanged = 0;
            int cratesPreserved = 0;
            var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "";

                // Preserva caixotes e props explicitamente!
                bool isCrateOrProp =
                    ContainsCI(n, "Crate") || ContainsCI(n, "Box") || ContainsCI(n, "Caixa") ||
                    ContainsCI(n, "Bancada") || ContainsCI(n, "Prop") || ContainsCI(n, "Medallion") ||
                    ContainsCI(n, "Pillar") || ContainsCI(n, "Wall") || ContainsCI(n, "Pipe") ||
                    ContainsCI(n, "Arch") || ContainsCI(n, "Rail") || ContainsCI(n, "Parapet") ||
                    ContainsCI(n, "Ceiling") || ContainsCI(n, "Beam") || ContainsCI(n, "Door") ||
                    ContainsCI(n, "Hero") || ContainsCI(n, "Player") || ContainsCI(n, "Dez") ||
                    ContainsCI(n, "Dex") || ContainsCI(n, "Porca") || ContainsCI(n, "CeilingLight");

                if (isCrateOrProp)
                {
                    if (ContainsCI(n, "Crate") || ContainsCI(n, "Box") || ContainsCI(n, "Caixa"))
                        cratesPreserved++;
                    continue;
                }

                bool isFloor =
                    ContainsCI(n, "Floor") || ContainsCI(n, "Deck") ||
                    ContainsCI(n, "Walkway") || ContainsCI(n, "Ramp") ||
                    ContainsCI(n, "Ground") || ContainsCI(n, "Piso") ||
                    matName == "Mat_C4_Floor_BrassIron" || matName == "Mat_Proto_Floor_1m";

                if (isFloor)
                {
                    r.sharedMaterial = matConcrete;
                    floorChanged++;
                }
            }

            // 3. Instala grandes luminarias industriais no teto (Oficina Z1 + Z2..Z5)
            var oldRoot = GameObject.Find("Caldeiral_CeilingLights");
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
            var lightsRoot = new GameObject("Caldeiral_CeilingLights");

            float z1CeilY = FindZoneCeilingY("Z1_Oficina_da_Sucata", 4.65f);
            float z2CeilY = FindZoneCeilingY("Z2_Passarela_da_Bigorna", 6.20f);
            float z3CeilY = FindZoneCeilingY("Z3_Patio_das_Sirenes", 9.60f);
            float z4CeilY = FindZoneCeilingY("Z4_Camara_Caldeiral_Central", 12.20f);
            float z5CeilY = FindZoneCeilingY("Z5_Galeria_Retorno_Sync", 8.20f);

            Debug.Log($"[CaldeiralConcreteAndLights] Ceiling heights: Z1={z1CeilY:F2} Z2={z2CeilY:F2} Z3={z3CeilY:F2} Z4={z4CeilY:F2} Z5={z5CeilY:F2}");

            Color warmWorkshop = new Color(1.00f, 0.94f, 0.85f);
            Color coolClean    = new Color(0.92f, 0.96f, 1.00f);

            // Z1 — OFICINA DA SUCATA: 3 grandes luminarias no teto iluminando bem todo o ambiente (sem estourar)!
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z1_West",   new Vector3(-33.0f, z1CeilY, -18.0f), new Vector3(2.8f, 0.14f, 1.05f), warmWorkshop, 24f, 22f, false, matHousing, matEmissive);
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z1_Center", new Vector3(-28.6f, z1CeilY, -18.0f), new Vector3(3.0f, 0.14f, 1.10f), warmWorkshop, 30f, 24f, true,  matHousing, matEmissive);
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z1_East",   new Vector3(-24.4f, z1CeilY, -18.0f), new Vector3(2.8f, 0.14f, 1.05f), warmWorkshop, 24f, 22f, false, matHousing, matEmissive);

            // Z2 — PASSARELA DA BIGORNA: 2 grandes luminarias no teto
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z2_West",   new Vector3(-13.2f, z2CeilY, -18.0f), new Vector3(3.0f, 0.15f, 1.10f), warmWorkshop, 32f, 26f, false, matHousing, matEmissive);
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z2_East",   new Vector3(-4.8f,  z2CeilY, -18.0f), new Vector3(3.0f, 0.15f, 1.10f), warmWorkshop, 32f, 26f, false, matHousing, matEmissive);

            // Z3 — PATIO DAS SIRENES: 2 grandes luminarias
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z3_South",  new Vector3(22.0f, z3CeilY, -15.0f),  new Vector3(3.4f, 0.16f, 1.25f), coolClean,    46f, 32f, false, matHousing, matEmissive);
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z3_North",  new Vector3(22.0f, z3CeilY,   4.0f),  new Vector3(3.4f, 0.16f, 1.25f), coolClean,    46f, 32f, false, matHousing, matEmissive);

            // Z4 — CAMARA CALDEIRAL CENTRAL: 2 grandes luminarias
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z4_Center", new Vector3(0.0f, z4CeilY,  7.0f),    new Vector3(4.0f, 0.18f, 1.45f), warmWorkshop, 65f, 38f, false, matHousing, matEmissive);
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z4_North",  new Vector3(0.0f, z4CeilY, 13.0f),    new Vector3(3.2f, 0.16f, 1.20f), warmWorkshop, 46f, 30f, false, matHousing, matEmissive);

            // Z5 — GALERIA DE RETORNO
            CreateCeilingFixture(lightsRoot.transform, "CeilingLight_Z5_Gallery", new Vector3(-19.0f, z5CeilY, 9.0f),  new Vector3(3.2f, 0.15f, 1.15f), warmWorkshop, 42f, 28f, false, matHousing, matEmissive);

            // 4. Calibra ambiente geral para ficar claro, equilibrado e legivel
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.36f, 0.33f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.24f, 0.21f);
            RenderSettings.ambientGroundColor  = new Color(0.16f, 0.15f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.14f, 0.12f, 0.11f);
            RenderSettings.fogDensity = 0.0055f;

            // Ajusta L1_Oficina_Warm para complementar sem estourar o cimento
            var l1 = GameObject.Find("L1_Oficina_Warm")?.GetComponent<Light>();
            if (l1 != null)
            {
                l1.color = new Color(1.00f, 0.88f, 0.72f);
                l1.intensity = 10f;
                l1.range = 18f;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 5. Captura foto da Oficina (Z1) mostrando chao de cimento, caixotes preservados e luzes no teto!
            CaptureOficinaPreview();

            Debug.Log($"[CaldeiralConcreteAndLights] OK! floors={floorChanged}, cratesPreserved={cratesPreserved}, ceilingFixtures=10");
        }

        private static float FindZoneCeilingY(string zoneName, float fallbackY)
        {
            var zGo = GameObject.Find(zoneName);
            if (zGo == null) return fallbackY;
            float bestY = float.MaxValue;
            foreach (var r in zGo.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (ContainsCI(r.gameObject.name, "Ceiling"))
                {
                    float bottomY = r.bounds.min.y;
                    if (bottomY > 2.5f && bottomY < bestY)
                        bestY = bottomY;
                }
            }
            return bestY < float.MaxValue ? (bestY - 0.08f) : fallbackY;
        }

        private static void CreateCeilingFixture(
            Transform parent, string name, Vector3 pos, Vector3 size,
            Color lightColor, float intensity, float range, bool castShadows,
            Material matHousing, Material matEmissive)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            // Carcaca metalica superior
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Housing";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            frame.transform.localScale = new Vector3(size.x + 0.18f, size.y, size.z + 0.18f);
            UnityEngine.Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.GetComponent<MeshRenderer>().sharedMaterial = matHousing;
            frame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Painel difusor luminoso emissivo voltado para baixo
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "EmissivePanel";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            panel.transform.localScale = new Vector3(size.x, size.y * 0.45f, size.z);
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<MeshRenderer>().sharedMaterial = matEmissive;
            panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Duas hastes metalicas de suspensao prendendo a luminaria ao teto (visual industrial real, sem flutuar!)
            for (int side = -1; side <= 1; side += 2)
            {
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rod.name = side < 0 ? "SuspendRod_L" : "SuspendRod_R";
                rod.transform.SetParent(root.transform, false);
                rod.transform.localPosition = new Vector3(side * (size.x * 0.36f), 0.75f, 0f);
                rod.transform.localScale = new Vector3(0.05f, 0.70f, 0.05f);
                UnityEngine.Object.DestroyImmediate(rod.GetComponent<Collider>());
                rod.GetComponent<MeshRenderer>().sharedMaterial = matHousing;
                rod.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Luz real URP posicionada logo abaixo do painel
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lightColor;
            l.intensity = intensity;
            l.range = range;
            l.shadows = castShadows ? LightShadows.Soft : LightShadows.None;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void ConfigureTexture(string path, bool isNormal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }

        private static Material EnsureConcreteMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ConcreteMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, ConcreteMat);
            }
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteAlb);
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteNrm);
            Vector2 tiling = new Vector2(0.40f, 0.40f); // 1 tile = 2.5m (placas de concreto ~1.25m)
            mat.SetTexture("_BaseMap", alb);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", new Color(0.76f, 0.75f, 0.73f));
            mat.SetFloat("_Metallic", 0.04f);
            mat.SetFloat("_Smoothness", 0.22f);
            if (nrm != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", nrm);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", 0.65f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureHousingMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(HousingMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, HousingMat);
            }
            mat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.19f));
            mat.SetFloat("_Metallic", 0.72f);
            mat.SetFloat("_Smoothness", 0.42f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureEmissivePanelMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PanelMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, PanelMat);
            }
            mat.SetColor("_BaseColor", new Color(1.0f, 0.97f, 0.90f));
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(3.2f, 2.9f, 2.4f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CaptureOficinaPreview()
        {
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            Vector3 oldPos = cam.transform.position;
            Quaternion oldRot = cam.transform.rotation;

            // Angulo amplo dentro da Oficina (Z1) pegando chao de cimento, caixotes, Dex e luzes no teto!
            cam.transform.position = new Vector3(-34.8f, 2.5f, -22.2f);
            cam.transform.LookAt(new Vector3(-27.2f, 2.0f, -17.4f));

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string outPath = Path.Combine(repoRoot, "evidence", "captures", "caldeiral_z1_concrete_lights.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            cam.transform.position = oldPos;
            cam.transform.rotation = oldRot;
        }

        private static bool ContainsCI(string src, string sub)
            => !string.IsNullOrEmpty(src) && src.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
