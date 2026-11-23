using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// CaldeiralC5PropsBatch1 — CONCERTO C5 (Substituicao in-place dos 4 Hero Props Batch-1 no Caldeiral).
    /// Props gerados pelo pipeline oficial da Fabrica 3D (TripoSG -> TripoSF 512 -> Hunyuan3D-Paint -> pack_glb, seed 42):
    ///   1. c5_prop_sirene_mestra    -> ENT_Sirene_Mestra_Placeholder (Z4, H=16m)
    ///   2. c5_prop_no_resincronia   -> I5_No_ReSincronia_Placeholder (Z4, H=2.0m)
    ///   3. c5_prop_valvula_ritmica  -> I2_Valvula_A, I3_Valvula_B, I4_Valvula_C (Z4, H=1.25m)
    ///   4. c5_prop_bancada_relogio  -> I1_Bancada_Relogio (Z1, H=1.10m)
    /// Preserva 100% da planta C3, vestimenta C4.1, InteractableBase, IkHandTarget, BoxCollider,
    /// budget de <= 5 luzes URP e caminhos congelados.
    /// </summary>
    public static partial class CaldeiralC2BlockoutBuilder
    {
        [MenuItem("Studio/Concerto/C5 Props Batch-1/Build Caldeiral C5 Props Batch-1 + Captures")]
        public static void BuildFullC5PropsBatch1Menu()
        {
            BuildFullC5PropsBatch1(captureAll: true);
        }

        public static void BuildFullC5PropsBatch1Batch()
        {
            try
            {
                BuildFullC5PropsBatch1(captureAll: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC5] Erro fatal no build C5 Props Batch-1: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFullC5PropsBatch1(bool captureAll = true)
        {
            AssetDatabase.Refresh();

            // 1. Garante importacao URP Lit + prefabs Fab3D dos novos props C5
            Fab3dImporter.ImportAll(autoCapture: false);
            AssetDatabase.Refresh();

            // 2. Constroi a cena C4.1 aprovada como base limpa
            BuildFullC41VisualDressing(captureConsolidated: false);

            // 3. Substitui in-place os 4 hero props Batch-1 (6 instancias) preservando mecanica/colliders
            var sirenePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/c5_prop_sirene_mestra.prefab");
            var noPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/c5_prop_no_resincronia.prefab");
            var valvulaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/c5_prop_valvula_ritmica.prefab");
            var bancadaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/c5_prop_bancada_relogio.prefab");

            int replaced = 0;
            if (ReplacePropInPlace("ENT_Sirene_Mestra_Placeholder", sirenePrefab,  targetHeight: 16.0f, yawOffset: 180f, maxWidth: 6.5f)) replaced++;
            if (ReplacePropInPlace("I5_No_ReSincronia_Placeholder", noPrefab,      targetHeight: 2.05f, yawOffset: 180f, maxWidth: 1.8f)) replaced++;
            if (ReplacePropInPlace("I2_Valvula_A",                  valvulaPrefab, targetHeight: 1.25f, yawOffset: 45f,  maxWidth: 1.2f)) replaced++;
            if (ReplacePropInPlace("I3_Valvula_B",                  valvulaPrefab, targetHeight: 1.25f, yawOffset: -45f, maxWidth: 1.2f)) replaced++;
            if (ReplacePropInPlace("I4_Valvula_C",                  valvulaPrefab, targetHeight: 1.25f, yawOffset: 180f, maxWidth: 1.2f)) replaced++;
            if (ReplacePropInPlace("I1_Bancada_Relogio",            bancadaPrefab, targetHeight: 1.12f, yawOffset: 180f, maxWidth: 2.6f)) replaced++;

            if (replaced < 6)
            {
                Debug.LogWarning($"[CaldeiralC5] Apenas {replaced}/6 instancias de props substituidas! Verifique se todos os prefabs existem.");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[CaldeiralC5] Caldeiral C5 Props Batch-1 salvo ({replaced}/6 instancias substituidas in-place).");

            if (captureAll)
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    CaptureC5Batch1Checkpoint(cam, replaced);
                    CaptureC5Batch1WalkthroughFrames(cam);
                }
            }
        }

        private static bool ReplacePropInPlace(string targetName, GameObject prefab, float targetHeight, float yawOffset, float maxWidth)
        {
            if (prefab == null)
            {
                Debug.LogError($"[CaldeiralC5] Prefab ausente para '{targetName}'.");
                return false;
            }
            var target = GameObject.Find(targetName);
            if (target == null)
            {
                Debug.LogError($"[CaldeiralC5] GameObject '{targetName}' nao encontrado na cena.");
                return false;
            }

            // Desativa apenas os renderers visuais do placeholder (preserva BoxCollider, InteractableBase e IkHandTarget!)
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }

            // Remove visual C5 anterior se houver
            var oldVis = target.transform.Find($"{targetName}_C5Visual");
            if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

            var container = new GameObject($"{targetName}_C5Visual");
            container.transform.SetParent(target.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            container.transform.localScale = Vector3.one;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(c);
            }

            // Normaliza escala uniforme preservando proporcao 3D original e apoia a base em target.position.y
            var b = ProceduralMeshLibrary.ComputeBounds(container);
            if (b.size.y > 0.001f)
            {
                float s = targetHeight / b.size.y;
                float horiz = Mathf.Max(b.size.x, b.size.z);
                if (horiz * s > maxWidth && horiz > 0.001f)
                {
                    s = maxWidth / horiz;
                }
                container.transform.localScale = Vector3.one * s;

                b = ProceduralMeshLibrary.ComputeBounds(container);
                Vector3 targetPos = target.transform.position;
                Vector3 delta = new Vector3(
                    targetPos.x - b.center.x,
                    targetPos.y - b.min.y,
                    targetPos.z - b.center.z
                );
                container.transform.position += delta;
            }

            foreach (var t in container.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic
                );
            }

            Debug.Log($"[CaldeiralC5] Substituido in-place: {targetName} -> {prefab.name} (H={targetHeight:F2}m)");
            return true;
        }

        private static void CaptureC5Batch1Checkpoint(Camera cam, int replacedCount)
        {
            string stamp = "2026-11-23";
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string reviewDir = Path.Combine(repoRoot, "director_review", $"{stamp}_c5_regen_vista_unica");
            string capDir = Path.Combine(repoRoot, "evidence", "captures", "c5_regen_vista_unica");
            Directory.CreateDirectory(reviewDir);
            Directory.CreateDirectory(capDir);

            (string slug, Vector3 pos, Vector3 look)[] shots =
            {
                ("01_z1_oficina_bancada_wide",        new Vector3(-34.8f, 3.0f, -22.2f), new Vector3(-28.8f, 1.0f, -17.5f)),
                ("02_z1_bancada_closeup",             new Vector3(-31.2f, 1.85f, -20.2f), new Vector3(-29.0f, 0.75f, -17.5f)),
                ("03_z4_camara_wide_sirene_valvulas", new Vector3(0.0f,   5.8f,  -4.5f), new Vector3(0.0f,   5.5f,  7.0f)),
                ("04_z4_sirene_mestra_closeup",       new Vector3(-7.8f,  5.4f,   0.5f), new Vector3(0.0f,   6.8f,  7.0f)),
                ("05_z4_no_resincronia_closeup",      new Vector3(2.8f,   5.2f,  -2.2f), new Vector3(0.0f,   4.5f,  0.5f)),
                ("06_z4_valvula_ritmica_closeup",     new Vector3(-3.3f,  1.65f,  1.6f), new Vector3(-5.5f,  0.75f, 4.0f)),
            };

            // Warmup URP render
            for (int w = 0; w < 2; w++)
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

                File.WriteAllBytes(Path.Combine(capDir, $"{s.slug}.png"), png);
                File.WriteAllBytes(Path.Combine(reviewDir, $"{s.slug}.png"), png);
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            string readme =
                $"O QUE É: Checkpoint CONCERTO C5 Props Batch-1 — 4 hero props gerados pelo pipeline oficial TripoSG -> TripoSF 512 -> Hunyuan3D-Paint -> pack_glb e importados in-place no Caldeiral ({replacedCount}/6 instâncias).\n" +
                "PROPS: c5_prop_sirene_mestra (Z4, 16m), c5_prop_no_resincronia (Z4, 2m), c5_prop_valvula_ritmica (Z4 x3), c5_prop_bancada_relogio (Z1).\n" +
                "O QUE VOCÊ DECIDE: Aprovar a substituição visual dos 4 props Batch-1 dentro das zonas Z1 e Z4 do Caldeiral.\n";
            File.WriteAllText(Path.Combine(reviewDir, "LEIA-ME.txt"), readme);
            Debug.Log($"[CaldeiralC5] Capturas C5 Batch-1 salvas em: {reviewDir}");
        }

        private static void CaptureC5Batch1WalkthroughFrames(Camera cam)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "evidence", "captures", "walkthrough_c5_batch1_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            (Vector3 pos, Vector3 look)[] stops =
            {
                (new Vector3(-35.0f, 2.8f, -22.2f), new Vector3(-28.8f, 1.0f, -17.5f)),
                (new Vector3(-31.0f, 1.9f, -20.0f), new Vector3(-29.0f, 0.8f, -17.5f)),
                (new Vector3(-20.0f, 2.2f, -18.0f), new Vector3(-10.0f, 2.2f, -18.0f)),
                (new Vector3(-10.0f, 3.5f, -18.5f), new Vector3(0.0f,   2.4f, -18.0f)),
                (new Vector3(11.0f,  3.6f, -21.8f), new Vector3(22.0f,  4.2f, -18.5f)),
                (new Vector3(25.5f,  5.4f, -16.5f), new Vector3(26.5f,  4.8f,  2.0f)),
                (new Vector3(20.0f,  5.4f,  8.8f),  new Vector3(11.0f,  5.2f,  8.5f)),
                (new Vector3(8.5f,   6.2f,  8.5f),  new Vector3(0.0f,   6.5f,  7.0f)),
                (new Vector3(2.8f,   5.2f, -2.2f),  new Vector3(0.0f,   4.5f,  0.5f)),
                (new Vector3(0.0f,   5.8f, -4.5f),  new Vector3(0.0f,   5.5f,  7.0f)),
                (new Vector3(-3.3f,  1.65f, 1.6f),  new Vector3(-5.5f,  0.75f, 4.0f)),
                (new Vector3(-9.5f,  7.2f, -1.5f),  new Vector3(0.0f,   7.5f,  7.0f)),
                (new Vector3(-14.0f, 6.0f,  9.0f),  new Vector3(-25.0f, 4.8f,  9.0f)),
                (new Vector3(-29.0f, 5.2f, -9.8f),  new Vector3(-29.0f, 1.0f, -17.5f)),
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
            Debug.Log($"[CaldeiralC5] {frameIdx} frames do walkthrough C5 Batch-1 gerados em {framesDir}");
        }
    }
}
