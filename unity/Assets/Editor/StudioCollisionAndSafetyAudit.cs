using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// StudioCollisionAndSafetyAudit — Ferramenta permanente da Fabrica de Jogos (ADR-012 / Qualidade AAA):
    ///   - Varre toda a cena ativa em busca de geometria solida (canos, corrimaos, cilindros, pilares,
    ///     paredes, bancadas, pisos, rampas, passarelas) que tenha MeshRenderer mas NAO tenha Collider.
    ///   - Adiciona BoxCollider ou MeshCollider automaticamente onde faltar (ignorando personagens, FX, sombras, tapetes).
    ///   - Salva relatorio JSON em evidence/validation/collision_audit_report.json.
    /// </summary>
    public static class StudioCollisionAndSafetyAudit
    {
        private const string DefaultScene = "Assets/Game/Scenes/Caldeiral.unity";

        private static readonly string[] IgnoreTokens =
        {
            "player_", "companion_", "npc_", "enemy_", "hero_",
            "shadow", "fx_", "vfx_", "decal", "tapete", "rug", "carpet",
            "poster", "banner", "light", "glow", "spark", "smoke", "fog", "trigger"
        };

        [MenuItem("Studio/Factory/Audit & Fix Scene Colliders")]
        public static void RunOnActiveSceneMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[CollisionAudit] Nenhuma cena ativa.");
                return;
            }
            int added = AuditAndFixScene(scene.path, saveScene: true);
            Debug.Log($"[CollisionAudit] Concluido em {scene.path}: +{added} colliders adicionados.");
        }

        public static void RunBatch()
        {
            try
            {
                int added = AuditAndFixScene(DefaultScene, saveScene: true);
                Debug.Log($"[CollisionAudit] BATCH OK: +{added} colliders adicionados em {DefaultScene}.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CollisionAudit] Erro fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static bool ShouldIgnore(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                string low = cur.name.ToLowerInvariant();
                for (int i = 0; i < IgnoreTokens.Length; i++)
                {
                    if (low.Contains(IgnoreTokens[i])) return true;
                }
                if (cur.GetComponent<Animator>() != null || cur.GetComponent<CharacterController>() != null)
                    return true;
                cur = cur.parent;
            }
            return false;
        }

        private static bool HasColliderInHierarchy(Transform t)
        {
            if (t.GetComponent<Collider>() != null) return true;
            if (t.GetComponentInChildren<Collider>(true) != null) return true;
            // Se o pai imediato ja tem um collider que envolve este visual, nao duplica
            if (t.parent != null && t.parent.GetComponent<Collider>() != null) return true;
            return false;
        }

        public static int AuditAndFixScene(string scenePath, bool saveScene)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            int checkedCount = 0;
            int alreadyHad = 0;
            int addedBox = 0;
            int addedMesh = 0;
            var addedNames = new List<string>();

            foreach (var mr in renderers)
            {
                if (mr == null || !mr.enabled) continue;
                var go = mr.gameObject;
                if (ShouldIgnore(go.transform)) continue;
                checkedCount++;

                var mf = go.GetComponent<MeshFilter>();
                string low = go.name.ToLowerInvariant();
                bool isHollowOrPortal =
                    low.Contains("arch") || low.Contains("portal") || low.Contains("tunnel") ||
                    low.Contains("door") || low.Contains("gate") || low.Contains("ring") ||
                    low.Contains("ceiling") || low.Contains("frame") || low.Contains("parapet") ||
                    low.Contains("railing") || low.Contains("corrimao");

                // Regra critica da fabrica: NUNCA deixar BoxCollider em portais/arcos/tuneis (cria barreira invisivel!)
                if (isHollowOrPortal)
                {
                    var existingBox = go.GetComponent<BoxCollider>();
                    if (existingBox != null)
                    {
                        UnityEngine.Object.DestroyImmediate(existingBox);
                        EditorUtility.SetDirty(go);
                    }
                    if (go.GetComponent<MeshCollider>() == null && mf != null && mf.sharedMesh != null)
                    {
                        var mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = false;
                        addedMesh++;
                        addedNames.Add(go.name + " (PortalFix->MeshCol)");
                        EditorUtility.SetDirty(go);
                    }
                    continue;
                }

                if (HasColliderInHierarchy(go.transform))
                {
                    alreadyHad++;
                    continue;
                }

                // Ignora planos extremamente finos decorativos no chao (ex.: decalques sem nome padrao)
                Bounds b = mr.bounds;
                if (b.size.x < 0.04f && b.size.y < 0.04f && b.size.z < 0.04f) continue;

                bool useMeshCollider = mf != null && mf.sharedMesh != null &&
                    (low.Contains("ramp") || low.Contains("pipe") || low.Contains("cano") ||
                     low.Contains("cylinder") || low.Contains("cilindro") || low.Contains("rail") ||
                     low.Contains("bridge") || low.Contains("ponte") ||
                     low.Contains("valve") || low.Contains("valvula"));

                if (useMeshCollider)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                    addedMesh++;
                }
                else
                {
                    go.AddComponent<BoxCollider>();
                    addedBox++;
                }

                addedNames.Add(go.name);
                EditorUtility.SetDirty(go);
            }

            if (saveScene && (addedBox + addedMesh) > 0)
            {
                EditorSceneManager.SaveScene(scene, scenePath);
            }

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string repPath = Path.Combine(repoRoot, "evidence", "validation", "collision_audit_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(repPath));
            string sampleList = string.Join(", ", addedNames.GetRange(0, Math.Min(25, addedNames.Count)).ConvertAll(n => $"\"{n.Replace("\"", "")}\""));
            string json = "{\n" +
                $"  \"scene\": \"{scenePath}\",\n" +
                $"  \"solid_renderers_checked\": {checkedCount},\n" +
                $"  \"already_had_collider\": {alreadyHad},\n" +
                $"  \"added_box_colliders\": {addedBox},\n" +
                $"  \"added_mesh_colliders\": {addedMesh},\n" +
                $"  \"total_added\": {addedBox + addedMesh},\n" +
                $"  \"sample_fixed_objects\": [{sampleList}]\n" +
                "}\n";
            File.WriteAllText(repPath, json);
            return addedBox + addedMesh;
        }
    }
}
