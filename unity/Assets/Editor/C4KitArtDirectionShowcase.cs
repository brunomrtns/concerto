using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Studio.Editor
{
    /// <summary>
    /// Fase 4.0 — Importa os 4 assets Fab3D do Kit de Direcao de Arte do Caldeiral
    /// e gera capturas individuais + showcase conjunto em memoria (sem alterar Caldeiral.unity).
    /// </summary>
    public static class C4KitArtDirectionShowcase
    {
        private static readonly string[] KitAssets =
        {
            "caldeiral_env_floor_tile",
            "caldeiral_env_wall_panel",
            "caldeiral_env_portal_pillar",
            "caldeiral_env_vapor_pipe",
        };

        public static void ImportAndRenderShowcaseBatch()
        {
            try
            {
                AssetDatabase.Refresh();
                var rep = Fab3dImporter.ImportAll(autoCapture: true);
                Debug.Log($"[C4KitShowcase] Fab3dImporter: assets={rep.assetsProcessed} prefabs={rep.prefabs.Count} captures={rep.captures.Count} erros={rep.errors.Count}");
                RenderShowcase();
                EditorApplication.Exit(rep.errors.Count == 0 ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[C4KitShowcase] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void RenderShowcase()
        {
            // Cena temporaria em memoria — NAO altera Caldeiral.unity nem Calibration.unity!
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.31f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.20f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.11f, 0.09f);

            // Iluminacao quente/clockwork do Caldeiral (3 luzes)
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.88f, 0.72f);
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            keyGo.AddComponent<UniversalAdditionalLightData>();

            var warmGo = new GameObject("WarmAccent");
            warmGo.transform.position = new Vector3(-2.5f, 2.6f, -2.5f);
            var warm = warmGo.AddComponent<Light>();
            warm.type = LightType.Point;
            warm.color = new Color(1.0f, 0.72f, 0.34f);
            warm.range = 14f;
            warm.intensity = 10f;
            warmGo.AddComponent<UniversalAdditionalLightData>();

            var rimGo = new GameObject("CoolFill");
            rimGo.transform.position = new Vector3(3.5f, 2.4f, 2.5f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(0.58f, 0.78f, 0.92f);
            rim.range = 14f;
            rim.intensity = 6f;
            rimGo.AddComponent<UniversalAdditionalLightData>();

            // Base neutra escura de estudio (ProceduralMeshLibrary — zero CreatePrimitive)
            var stageMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            stageMat.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.14f));
            stageMat.SetFloat("_Metallic", 0.1f);
            stageMat.SetFloat("_Smoothness", 0.25f);
            ProceduralMeshLibrary.CreatePart("Showcase_Stage", ProceduralMeshLibrary.Box, null, new Vector3(0f, -0.1f, 0f), Quaternion.identity, new Vector3(14f, 0.2f, 8f), stageMat);

            var camGo = new GameObject("ShowcaseCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.12f, 0.11f);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            camGo.AddComponent<UniversalAdditionalCameraData>();

            string dateOnly = DateTime.Now.ToString("yyyy-MM-dd");
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string reviewDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "director_review", $"{dateOnly}_c4_0_kit_direcao_arte");
            string capDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(reviewDir);
            Directory.CreateDirectory(capDir);

            // Warm-up URP
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            // Posiciona os 4 prefabs reais Fab3D lado a lado (X = -3.0, -1.0, +1.0, +3.0)
            float[] xs = { -3.0f, -1.0f, 1.0f, 3.0f };
            Bounds[] boundsArr = new Bounds[KitAssets.Length];
            for (int i = 0; i < KitAssets.Length; i++)
            {
                string prefabPath = $"Assets/Game/Prefabs/Fab3D/{KitAssets[i]}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[C4KitShowcase] Prefab nao encontrado: {prefabPath}");
                    continue;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = KitAssets[i];
                inst.transform.position = new Vector3(xs[i], 0f, 0f);
                // Normaliza altura/escala visual de exibicao no showcase mantendo proporcoes reais
                var b = ProceduralMeshLibrary.ComputeBounds(inst);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.001f)
                {
                    float targetDim = (i == 2) ? 1.65f : 1.25f;
                    inst.transform.localScale *= (targetDim / maxDim);
                    b = ProceduralMeshLibrary.ComputeBounds(inst);
                    inst.transform.position += new Vector3(0f, -b.min.y, 0f);
                    b = ProceduralMeshLibrary.ComputeBounds(inst);
                }
                boundsArr[i] = b;

                // Captura individual 3/4 de cada peca
                Vector3 center = b.center;
                float dist = Mathf.Max(b.extents.magnitude * 2.4f, 1.8f);
                cam.transform.position = center + new Vector3(dist * 0.45f, dist * 0.42f, -dist * 0.95f);
                cam.transform.LookAt(center);
                SaveShot(cam, reviewDir, capDir, $"c4_kit_0{i + 1}_{KitAssets[i]}", stamp);
            }

            // Captura panoramica conjunta dos 4 assets lado a lado
            cam.fieldOfView = 38f;
            cam.transform.position = new Vector3(0.0f, 2.3f, -6.8f);
            cam.transform.LookAt(new Vector3(0.0f, 0.65f, 0.0f));
            SaveShot(cam, reviewDir, capDir, "c4_kit_00_showcase_conjunto", stamp);

            string readme =
                "O QUE É: Fase 4.0 — Kit de Direção de Arte do Caldeiral forjado 100% pela Fábrica 3D REAL (microsoft/TRELLIS.2-4B + fab3d_bridge + Fab3dImporter).\n" +
                "PEÇAS: 1 tile de piso (env_tile), 1 painel de parede (env_tile), 1 pilar/pórtico estrutural (env_structure), 1 duto/tubulação com válvula (env_structure).\n" +
                "O QUE VOCÊ DECIDE: Aprovar se ESTA é a linguagem visual oficial do Caldeiral ANTES de vestirmos qualquer zona (Z1->Z5) na Fase 4.1.\n";
            File.WriteAllText(Path.Combine(reviewDir, "LEIA-ME.txt"), readme);
            Debug.Log($"[C4KitShowcase] Showcase da Fase 4.0 salvo em: {reviewDir}");
        }

        private static void SaveShot(Camera cam, string reviewDir, string capDir, string slug, string stamp)
        {
            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(Path.Combine(reviewDir, $"{slug}_{stamp}.png"), png);
            File.WriteAllBytes(Path.Combine(capDir, $"{slug}_{stamp}.png"), png);
            File.WriteAllBytes(Path.Combine(capDir, $"{slug}.png"), png);
        }
    }
}
