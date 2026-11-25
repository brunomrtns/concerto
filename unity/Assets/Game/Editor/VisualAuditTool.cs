#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Ferramenta de Auditoria Visual por Ato (GATE 1 & GATE 3):
    /// Detecta em todas as cenas de gameplay (Caldeiral=ato1, SetorExterno=ato2, Epicentro=ato3):
    ///   - FLOATING_PROP: prop de chao flutuando acima da superficie de apoio (> 0.06m e < 1.2m)
    ///   - BURIED_PROP: prop parcialmente enterrado abaixo do chao (bottomY < groundY - 0.08m)
    ///   - BROKEN_MATERIAL: material nulo, Hidden/InternalErrorShader ou shader ausente
    /// Tambem aplica correcoes rapidas (apoiar prop flutuante no chao, desenterrar prop, corrigir material nulo).
    /// </summary>
    public static class VisualAuditTool
    {
        [Serializable]
        public sealed class AuditFinding
        {
            public string act;
            public string scene;
            public string path;
            public string objName;
            public string kind; // FLOATING_PROP | BURIED_PROP | BROKEN_MATERIAL
            public float deltaY;
            public float groundY;
            public Vector3 worldPos;
            public string details;
        }

        [Serializable]
        private sealed class FindingsWrapper
        {
            public List<AuditFinding> findings = new List<AuditFinding>();
        }

        private static readonly (string act, string scenePath)[] ActScenes =
        {
            ("ato1", "Assets/Game/Scenes/Caldeiral.unity"),
            ("ato2", "Assets/Game/Scenes/SetorExterno.unity"),
            ("ato3", "Assets/Game/Scenes/Epicentro.unity"),
        };

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string AuditDir => Path.Combine(RepoRoot, "captures", "visual_auditoria");
        private static string FindingsJsonPath => Path.Combine(AuditDir, "audit_findings_antes.json");

        public static void RunAuditScanFromCommandLine()
        {
            Directory.CreateDirectory(AuditDir);
            var all = new List<AuditFinding>();
            foreach (var (act, scenePath) in ActScenes)
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                all.AddRange(ScanActiveScene(act, sc.name));
            }
            File.WriteAllText(FindingsJsonPath, JsonUtility.ToJson(new FindingsWrapper { findings = all }, true), Encoding.UTF8);
            Debug.Log($"[VisualAudit] Scan concluido: {all.Count} achados -> {FindingsJsonPath}");
            EditorApplication.Exit(0);
        }

        public static void RunQuickFixesFromCommandLine()
        {
            Directory.CreateDirectory(AuditDir);
            int totalFixed = 0;
            var fixLog = new StringBuilder();
            fixLog.AppendLine("VisualAudit Quick Fixes:");

            foreach (var (act, scenePath) in ActScenes)
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                var findings = ScanActiveScene(act, sc.name);
                if (findings.Count == 0 && act != "ato2") continue;

                bool dirty = false;
                Material fallbackMat = FindSafeUrpLitMaterial();

                foreach (var f in findings)
                {
                    var go = FindByHierarchyPath(f.path);
                    if (go == null) continue;

                    if (f.kind == "FLOATING_PROP" || f.kind == "BURIED_PROP")
                    {
                        // Move verticalmente para apoiar exatamente sobre o chao (delta = -f.deltaY)
                        go.transform.position += new Vector3(0f, -f.deltaY, 0f);
                        fixLog.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "  [{0}] {1}: {2} -> ajustado Y em {3:+0.000;-0.000}m (chao Y={4:F3})",
                            act, f.path, f.kind, -f.deltaY, f.groundY));
                        totalFixed++;
                        dirty = true;
                    }
                    else if (f.kind == "BROKEN_MATERIAL" && fallbackMat != null)
                    {
                        var mr = go.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = fallbackMat;
                            fixLog.AppendLine($"  [{act}] {f.path}: BROKEN_MATERIAL -> atribuido '{fallbackMat.name}'");
                            totalFixed++;
                            dirty = true;
                        }
                    }
                }

                if (act == "ato2")
                {
                    // VA-07: aplica materiais PBR existentes (piso latao e painel parede) nos blocos greybox do SetorExterno
                    Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Fab3D/Mat_Fab3D_caldeiral_env_floor_tile.mat");
                    Material wallMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Fab3D/Mat_Fab3D_caldeiral_env_wall_panel.mat");
                    if (floorMat != null && wallMat != null)
                    {
                        foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        {
                            if (mr == null || !mr.enabled) continue;
                            string n = mr.name.ToLowerInvariant();
                            string matName = mr.sharedMaterial != null ? mr.sharedMaterial.name.ToLowerInvariant() : "";
                            if (!matName.Contains("greybox") && !matName.Contains("default") && !matName.Contains("lit") && !matName.Contains("sector")) continue;
                            if (n.Contains("floor") || n.Contains("ground") || n.Contains("piso") || n.Contains("patio"))
                            {
                                mr.sharedMaterial = floorMat;
                                totalFixed++;
                                dirty = true;
                            }
                            else if (n.Contains("wall") || n.Contains("parede") || n.Contains("tower") || n.Contains("pillar") || n.Contains("block") || n.Contains("barrier"))
                            {
                                mr.sharedMaterial = wallMat;
                                totalFixed++;
                                dirty = true;
                            }
                        }
                        fixLog.AppendLine("  [ato2] SetorExterno: materiais PBR existentes (floor_tile / wall_panel) aplicados em pisos e paredes greybox.");
                    }
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                }
            }

            File.WriteAllText(Path.Combine(AuditDir, "audit_fixes_applied.txt"), fixLog.ToString(), Encoding.UTF8);
            Debug.Log($"[VisualAudit] Quick fixes concluidos: {totalFixed} correcoes aplicadas.");
            EditorApplication.Exit(0);
        }

        private static bool IsGroundedPropCandidate(MeshRenderer mr)
        {
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) return false;
            string n = mr.name.ToLowerInvariant();
            string rootN = mr.transform.root.name.ToLowerInvariant();
            // Ignora arquitetura estrutural, tetos, luzes, particulas, efeitos aereos, flutuantes intencionais, personagens e UI
            if (n.Contains("wall") || n.Contains("parede") || n.Contains("floor") || n.Contains("piso") ||
                n.Contains("ceiling") || n.Contains("teto") || n.Contains("beam") || n.Contains("pipe") ||
                n.Contains("arch") || n.Contains("rail") || n.Contains("corrim") || n.Contains("ramp") ||
                n.Contains("plat") || n.Contains("deck") || n.Contains("bridge") || n.Contains("portal") ||
                n.Contains("light") || n.Contains("lamp") || n.Contains("sirene") || n.Contains("glow") ||
                n.Contains("sky") || n.Contains("rift") || n.Contains("fenda") || n.Contains("crystal") ||
                n.Contains("floating") || n.Contains("flutuante") || n.Contains("trim") || n.Contains("window") ||
                n.Contains("frame") || n.Contains("door") || n.Contains("post_") || n.Contains("handrail") ||
                n.Contains("pillar") || n.Contains("pilar") || n.Contains("column") || n.Contains("dome") ||
                n.Contains("monolith") || n.Contains("ring") || n.Contains("stair") || n.Contains("step") ||
                rootN.Contains("player") || rootN.Contains("dez") || rootN.Contains("enemy") ||
                rootN.Contains("boss") || rootN.Contains("guardiao") || rootN.Contains("npc") ||
                rootN.Contains("juno") || rootN.Contains("brito") || rootN.Contains("luma") ||
                rootN.Contains("tio") || rootN.Contains("segunda") || rootN.Contains("part_") ||
                rootN.Contains("pickup") || rootN.Contains("celula") || rootN.Contains("energy"))
                return false;

            // Foca especificamente em props de cenario (caixas, bancadas, maquinas, consoles, geradores, barris, mesas, antenas, props)
            return n.Contains("crate") || n.Contains("caixa") || n.Contains("prop") || n.Contains("bancada") ||
                   n.Contains("table") || n.Contains("mesa") || n.Contains("gerador") || n.Contains("console") ||
                   n.Contains("barrel") || n.Contains("barril") || n.Contains("bench") || n.Contains("box") ||
                   n.Contains("maquina") || n.Contains("machine") || n.Contains("dummy") || n.Contains("rack") ||
                   n.Contains("shelf") || n.Contains("estante") || n.Contains("workstation") || n.Contains("antena") ||
                   n.Contains("valvula") || n.Contains("estacao");
        }

        private static List<AuditFinding> ScanActiveScene(string act, string sceneName)
        {
            var list = new List<AuditFinding>();
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var mr in renderers)
            {
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                string path = GetPath(mr.transform);

                // 1. Checa material quebrado/nulo em QUALQUER MeshRenderer ativo
                var mats = mr.sharedMaterials;
                bool brokenMat = mats == null || mats.Length == 0 || mats.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader");
                if (brokenMat)
                {
                    list.Add(new AuditFinding
                    {
                        act = act, scene = sceneName, path = path, objName = mr.name,
                        kind = "BROKEN_MATERIAL", deltaY = 0f, groundY = 0f, worldPos = mr.bounds.center,
                        details = "Material nulo ou Hidden/InternalErrorShader (rosa)"
                    });
                }

                // 2. Checa props apoiados no chao (flutuando ou enterrados)
                if (!IsGroundedPropCandidate(mr)) continue;

                Bounds b = mr.bounds;
                if (b.size.y < 0.15f || b.size.y > 6.0f) continue;

                // Sonda superficie caminhavel logo abaixo do centro do prop
                Vector3 probeOrigin = new Vector3(b.center.x, b.max.y + 0.35f, b.center.z);
                var hits = Physics.RaycastAll(probeOrigin, Vector3.down, b.size.y + 1.8f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

                float? groundY = null;
                string groundName = "";
                foreach (var h in hits)
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    if (h.collider.transform == mr.transform || h.collider.transform.IsChildOf(mr.transform.root)) continue;
                    if (h.normal.y < 0.85f) continue;
                    // Aceita apenas superficies que estejam perto da base do prop (nao tetos acima)
                    if (h.point.y <= b.min.y + b.size.y * 0.45f)
                    {
                        groundY = h.point.y;
                        groundName = h.collider.name;
                        break;
                    }
                }

                if (!groundY.HasValue) continue;
                float deltaY = b.min.y - groundY.Value; // > 0 => flutuando; < 0 => enterrado

                if (deltaY > 0.06f && deltaY < 0.85f)
                {
                    list.Add(new AuditFinding
                    {
                        act = act, scene = sceneName, path = path, objName = mr.name,
                        kind = "FLOATING_PROP", deltaY = deltaY, groundY = groundY.Value, worldPos = b.center,
                        details = string.Format(CultureInfo.InvariantCulture, "Prop flutuando +{0:F1}cm acima de '{1}' (minY={2:F3}, chaoY={3:F3})", deltaY * 100f, groundName, b.min.y, groundY.Value)
                    });
                }
                else if (deltaY < -0.08f && deltaY > -0.85f)
                {
                    list.Add(new AuditFinding
                    {
                        act = act, scene = sceneName, path = path, objName = mr.name,
                        kind = "BURIED_PROP", deltaY = deltaY, groundY = groundY.Value, worldPos = b.center,
                        details = string.Format(CultureInfo.InvariantCulture, "Prop enterrado {0:F1}cm em '{1}' (minY={2:F3}, chaoY={3:F3})", deltaY * 100f, groundName, b.min.y, groundY.Value)
                    });
                }
            }

            return list.OrderBy(f => f.act).ThenBy(f => f.path, StringComparer.Ordinal).ToList();
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            Transform cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 10)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }

        private static GameObject FindByHierarchyPath(string path)
        {
            var parts = path.Split('/');
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform cur = root.transform;
                bool ok = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    cur = cur.Find(parts[i]);
                    if (cur == null) { ok = false; break; }
                }
                if (ok && cur != null) return cur.gameObject;
            }
            return null;
        }

        private static Material FindSafeUrpLitMaterial()
        {
            foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mr.sharedMaterial != null && mr.sharedMaterial.shader != null &&
                    mr.sharedMaterial.shader.name.Contains("Universal Render Pipeline"))
                    return mr.sharedMaterial;
            }
            return null;
        }
    }
}
#endif
