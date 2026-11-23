using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// CaldeiralC5HeroisImport — S-char-01 (Porca D) + S-char-02 (Dez A) in-place na Oficina Z1 do Caldeiral.
    /// Preserva 100% da cena C4.1 + C5 Props FLUX, PlayerInteractor, DialoguePresenter e caminhos congelados.
    /// </summary>
    public static partial class CaldeiralC2BlockoutBuilder
    {
        [MenuItem("Studio/Concerto/C5 Herois/Build Caldeiral C5 + Herois (Porca D & Dez A)")]
        public static void BuildFullC5HeroisMenu()
        {
            BuildFullC5Herois(captureAll: true);
        }

        public static void BuildFullC5HeroisBatch()
        {
            try
            {
                BuildFullC5Herois(captureAll: true);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaldeiralC5-Herois] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFullC5Herois(bool captureAll = true)
        {
            AssetDatabase.Refresh();
            Fab3dImporter.ImportAll(autoCapture: false);
            AssetDatabase.Refresh();

            // 1. Constroi C4.1 + C5 Props FLUX intactos
            BuildFullC5PropsBatch1(captureAll: false);

            var porcaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/hero_porca.prefab");
            var dezPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>($"{Fab3dPrefabDir}/hero_dez.prefab");

            int replaced = 0;

            // 2. Substitui Companion_Porca_Placeholder in-place (H = 0.45 m, flutuando em y = 1.35 m + hover procedural, olhando para a Bancada)
            if (ReplaceHeroInPlace("Companion_Porca_Placeholder", porcaPrefab, targetHeight: 0.45f, yawOffset: 0f, alignBaseToTargetY: false, addPorcaHover: true))
                replaced++;

            // 3. Substitui Player_Dez_Placeholder in-place (H = 1.25 m, pes apoiados em y = 0.0 m, olhando para a Bancada)
            if (ReplaceHeroInPlace("Player_Dez_Placeholder", dezPrefab, targetHeight: 1.25f, yawOffset: 0f, alignBaseToTargetY: true, addPorcaHover: false))
                replaced++;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[CaldeiralC5-Herois] Caldeiral salvo com {replaced}/2 herois reais (Porca D + Dez A).");

            if (captureAll)
            {
                var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    CaptureC5HeroisCheckpoint(cam);
                }
            }
        }

        private static bool ReplaceHeroInPlace(string targetName, GameObject prefab, float targetHeight, float yawOffset, bool alignBaseToTargetY, bool addPorcaHover)
        {
            if (prefab == null)
            {
                Debug.LogError($"[CaldeiralC5-Herois] Prefab ausente para '{targetName}'.");
                return false;
            }
            var target = GameObject.Find(targetName);
            if (target == null)
            {
                Debug.LogError($"[CaldeiralC5-Herois] GameObject '{targetName}' nao encontrado.");
                return false;
            }

            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            var oldVis = target.transform.Find($"{targetName}_HeroVisual");
            if (oldVis != null) UnityEngine.Object.DestroyImmediate(oldVis.gameObject);

            var container = new GameObject($"{targetName}_HeroVisual");
            container.transform.SetParent(target.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            container.transform.localScale = Vector3.one;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);

            var b = ProceduralMeshLibrary.ComputeBounds(container);
            if (b.size.y > 0.001f)
            {
                float s = targetHeight / b.size.y;
                container.transform.localScale = Vector3.one * s;
                b = ProceduralMeshLibrary.ComputeBounds(container);

                Vector3 tPos = target.transform.position;
                float dy = alignBaseToTargetY ? (tPos.y - b.min.y) : (tPos.y - b.center.y);
                container.transform.position += new Vector3(tPos.x - b.center.x, dy, tPos.z - b.center.z);
            }

            if (addPorcaHover)
            {
                var hoverType = Type.GetType("Studio.Game.PorcaHoverBehavior, Assembly-CSharp");
                if (hoverType != null && container.GetComponent(hoverType) == null)
                    container.AddComponent(hoverType);
            }

            Debug.Log($"[CaldeiralC5-Herois] Substituido in-place: {targetName} -> {prefab.name} (H={targetHeight:F2}m)");
            return true;
        }

        private static void CaptureC5HeroisCheckpoint(Camera cam)
        {
            string stamp = "2026-11-23";
            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string reviewDir = Path.Combine(repoRoot, "director_review", $"{stamp}_c5_herois_porca_dez");
            string capDir = Path.Combine(repoRoot, "evidence", "captures", "c5_herois_porca_dez");
            Directory.CreateDirectory(reviewDir);
            Directory.CreateDirectory(capDir);

            // Player_Dez esta em (-33.0, 0, -20.0) rot Y=65 (olhando para a Bancada em (-29, 0, -17.5))
            // Companion_Porca esta em (-31.8, 1.35, -19.0) rot Y=60
            (string slug, Vector3 pos, Vector3 look)[] shots =
            {
                ("01_z1_herois_oficina_wide",   new Vector3(-35.2f, 2.4f, -22.2f), new Vector3(-30.5f, 1.0f, -18.5f)),
                ("02_herois_dez_porca_frente",  new Vector3(-30.3f, 1.30f, -18.3f), new Vector3(-32.6f, 0.85f, -19.7f)),
                ("03_hero_dez_closeup",         new Vector3(-31.5f, 0.92f, -19.2f), new Vector3(-33.0f, 0.72f, -20.0f)),
                ("04_hero_porca_closeup",       new Vector3(-30.8f, 1.42f, -18.4f), new Vector3(-31.8f, 1.35f, -19.0f)),
            };

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

            string html = @"<!doctype html><html lang='pt-BR'><head><meta charset='utf-8'>
<title>CONCERTO C5 — Herois Porca D &amp; Dez A no Caldeiral (2026-11-23)</title>
<style>body{background:#141210;color:#ece3d5;font-family:system-ui,sans-serif;margin:24px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(460px,1fr));gap:16px}
.card{background:#1e1a16;border:1px solid #3a3026;border-radius:10px;padding:12px}
img{width:100%;border-radius:6px}h1,h3{color:#e8a13c}</style></head><body>
<h1>CONCERTO C5 — Heróis Oficiais: Porca D &amp; Dez A (Aguardando Aprovação do Bruno)</h1>
<p>Conceitos aprovados (FLUX.1-dev FP8, Dez A com mãos simétricas de 4 dedos + Porca D) -> TripoSG -> TripoSF 512 -> Hunyuan3D-Paint -> 05_trellis_pbr -> pack_glb -> importados in-place na Oficina Z1.</p>
<div class='grid'>
  <div class='card'><h3>Z1 Oficina — Dez A + Porca D + Bancada FLUX</h3><img src='01_z1_herois_oficina_wide.png'></div>
  <div class='card'><h3>Dez A &amp; Porca D — Frente 3/4</h3><img src='02_herois_dez_porca_frente.png'></div>
  <div class='card'><h3>Dez A — Close-up (H = 1,25 m)</h3><img src='03_hero_dez_closeup.png'></div>
  <div class='card'><h3>Porca D — Close-up (H = 0,45 m + hover procedural)</h3><img src='04_hero_porca_closeup.png'></div>
</div></body></html>";
            File.WriteAllText(Path.Combine(reviewDir, "review_c5_herois.html"), html);
            Debug.Log($"[CaldeiralC5-Herois] Capturas salvas em: {reviewDir}");
        }
    }
}
