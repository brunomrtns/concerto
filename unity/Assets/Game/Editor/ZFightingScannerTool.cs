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
    /// Ferramenta de Editor (Tools/CONCERTO/Z-Fighting Scanner) para varrer TODAS as cenas
    /// do jogo e reportar:
    ///   (a) COPLANAR_OVERLAP — superficies coplanarmente sobrepostas (causa de z-fighting);
    ///   (b) STACKED_DUPLICATE — objetos duplicados empilhados no mesmo ponto;
    ///   (c) WALL_INTERNAL_SEAM — faces internas visiveis / juntas coplanares entre pecas de parede/piso.
    /// Tambem gera capturas determinísticas de 30s (Antes/Depois) e aplica correcoes geometricas minimas
    /// (offsets 0.005m-0.008m / remocao de duplicatas redundantes / recuo de faces internas) sem alterar gameplay.
    /// </summary>
    public static class ZFightingScannerTool
    {
        private const float CoplanarTolerance = 0.004f;      // 4mm
        private const float MinOverlapArea    = 0.04f;       // 20cm x 20cm
        private const float DuplicatePosTol   = 0.05f;       // 5cm
        private const float DuplicateRotTol   = 2.0f;        // 2 deg
        private const float DuplicateScaleTol = 0.05f;
        private const float FixOffsetMeters   = 0.006f;      // 6mm (dentro da faixa 0.001-0.01m)

        [Serializable]
        public sealed class ZFightIssue
        {
            public string scene;
            public string category; // COPLANAR_OVERLAP | STACKED_DUPLICATE | WALL_INTERNAL_SEAM
            public string objA;
            public string objB;
            public string axis;     // X | Y | Z | POS
            public float depthDiff;
            public float overlapArea;
            public Vector3 worldFocusPoint;
            public string details;
            public int idA;
            public int idB;
        }

        [Serializable]
        public sealed class ZFightFixRecord
        {
            public string scene;
            public string category;
            public string objA;
            public string objB;
            public string cause;
            public string correction;
        }

        [Serializable]
        private sealed class FixLogWrapper
        {
            public List<ZFightFixRecord> records = new List<ZFightFixRecord>();
        }

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string VisualDir => Path.Combine(RepoRoot, "captures", "visual");
        private static string ReportPath => Path.Combine(VisualDir, "zfight_relatorio.txt");
        private static string PostReportPath => Path.Combine(VisualDir, "zfight_relatorio_depois.txt");
        private static string FixLogPath => Path.Combine(VisualDir, "zfight_fixes_log.json");

        private static string[] GetGameScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        [MenuItem("Tools/CONCERTO/Z-Fighting Scanner (Todas as Cenas)")]
        public static void RunScanMenu()
        {
            var issues = ScanAllScenes(ReportPath);
            Debug.Log($"[ZFightScanner] Scan concluido: {issues.Count} ocorrencias. Relatorio: {ReportPath}");
        }

        public static void RunScanFromCommandLine()
        {
            string outPath = Environment.GetEnvironmentVariable("ZFIGHT_REPORT_OUT");
            if (string.IsNullOrEmpty(outPath)) outPath = ReportPath;
            var issues = ScanAllScenes(outPath);
            Debug.Log($"[ZFightScanner] CLI Scan concluido: TOTAL={issues.Count} -> {outPath}");
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/CONCERTO/Z-Fighting Auto-Fix & Save Scenes")]
        public static void RunAutoFixMenu()
        {
            ApplyFixesAcrossAllScenes();
        }

        public static void RunAutoFixFromCommandLine()
        {
            ApplyFixesAcrossAllScenes();
            EditorApplication.Exit(0);
        }

        public static void CaptureComparisonFramesFromCommandLine()
        {
            string phase = Environment.GetEnvironmentVariable("ZFIGHT_CAPTURE_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "antes";
            CaptureShowcaseFrames(phase);
            EditorApplication.Exit(0);
        }

        public static List<ZFightIssue> ScanAllScenes(string outputReportPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputReportPath));
            var allIssues = new List<ZFightIssue>();
            var sb = new StringBuilder();
            sb.AppendLine("=====================================================================");
            sb.AppendLine("CONCERTO — RELATORIO DE SCANNER Z-FIGHTING (TODAS AS CENAS)");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Tolerancia coplanar: {CoplanarTolerance:F4}m | Area min: {MinOverlapArea:F3}m2");
            sb.AppendLine("=====================================================================");

            string[] scenes = GetGameScenes();
            foreach (string scenePath in scenes)
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var sceneIssues = ScanActiveScene(sc.name);
                allIssues.AddRange(sceneIssues);

                int cCoplanar = sceneIssues.Count(i => i.category == "COPLANAR_OVERLAP");
                int cDup      = sceneIssues.Count(i => i.category == "STACKED_DUPLICATE");
                int cWall     = sceneIssues.Count(i => i.category == "WALL_INTERNAL_SEAM");

                sb.AppendLine();
                sb.AppendLine($"[CENA] {sc.name} ({scenePath}) — Total: {sceneIssues.Count} (Coplanar={cCoplanar}, Duplicatas={cDup}, JuntasParede={cWall})");
                foreach (var iss in sceneIssues)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  - [{0}] {1} <-> {2} | eixo={3} diff={4:F4}m area={5:F3}m2 pos=({6:F2},{7:F2},{8:F2}) | {9}",
                        iss.category, iss.objA, iss.objB, iss.axis, iss.depthDiff, iss.overlapArea,
                        iss.worldFocusPoint.x, iss.worldFocusPoint.y, iss.worldFocusPoint.z, iss.details));
                }
            }

            sb.AppendLine();
            sb.AppendLine("=====================================================================");
            sb.AppendLine($"RESUMO GLOBAL: TOTAL={allIssues.Count} | COPLANAR={allIssues.Count(i => i.category == "COPLANAR_OVERLAP")} | DUPLICATAS={allIssues.Count(i => i.category == "STACKED_DUPLICATE")} | JUNTAS_PAREDE={allIssues.Count(i => i.category == "WALL_INTERNAL_SEAM")}");
            sb.AppendLine("=====================================================================");

            File.WriteAllText(outputReportPath, sb.ToString(), Encoding.UTF8);
            return allIssues;
        }

        private static bool IsCandidateRenderer(MeshRenderer mr)
        {
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) return false;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var b = mr.bounds;
            if (b.size.sqrMagnitude < 0.0001f) return false;
            return true;
        }

        private static bool IsSameRigOrActor(Transform a, Transform b)
        {
            // Evita falsos positivos entre sub-partes de um mesmo personagem/NPC/inimigo/arma
            Transform ra = a.root;
            Transform rb = b.root;
            if (ra != rb) return false;
            string rn = ra.name.ToLowerInvariant();
            if (rn.Contains("player") || rn.Contains("dez") || rn.Contains("npc") ||
                rn.Contains("enemy") || rn.Contains("boss") || rn.Contains("guardiao") ||
                rn.Contains("juno") || rn.Contains("brito") || rn.Contains("luma") ||
                rn.Contains("tio") || rn.Contains("segunda") || rn.Contains("part_"))
                return true;
            return false;
        }

        private static bool IsStructuralWallOrFloor(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("wall") || n.Contains("parede") || n.Contains("panel") ||
                   n.Contains("floor") || n.Contains("piso") || n.Contains("pillar") ||
                   n.Contains("pilar") || n.Contains("plat") || n.Contains("ramp") ||
                   n.Contains("passarela") || n.Contains("corridor") || n.Contains("arch") ||
                   n.Contains("gate") || n.Contains("base") || n.Contains("pad") ||
                   n.Contains("roof") || n.Contains("teto") || n.Contains("ground") ||
                   n.Contains("deck") || n.Contains("bridge") || n.Contains("bridge") ||
                   n.Contains("step") || n.Contains("stair") || n.Contains("tile");
        }

        private static string GetHierarchyPath(Transform t)
        {
            string p = t.name;
            Transform cur = t.parent;
            int guard = 0;
            while (cur != null && guard++ < 8)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }

        public static List<ZFightIssue> ScanActiveScene(string sceneName)
        {
            var issues = new List<ZFightIssue>();
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(IsCandidateRenderer)
                .OrderBy(r => GetHierarchyPath(r.transform), StringComparer.Ordinal)
                .ToList();

            var filters = new MeshFilter[renderers.Count];
            var boundsArr = new Bounds[renderers.Count];
            var paths = new string[renderers.Count];

            for (int i = 0; i < renderers.Count; i++)
            {
                filters[i] = renderers[i].GetComponent<MeshFilter>();
                boundsArr[i] = renderers[i].bounds;
                paths[i] = GetHierarchyPath(renderers[i].transform);
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                var rA = renderers[i];
                var bA = boundsArr[i];
                var mA = filters[i].sharedMesh;

                for (int j = i + 1; j < renderers.Count; j++)
                {
                    var rB = renderers[j];
                    if (IsSameRigOrActor(rA.transform, rB.transform)) continue;

                    var bB = boundsArr[j];
                    // Quick AABB rejection com margem de tolerancia
                    if (bA.max.x + CoplanarTolerance < bB.min.x || bB.max.x + CoplanarTolerance < bA.min.x) continue;
                    if (bA.max.y + CoplanarTolerance < bB.min.y || bB.max.y + CoplanarTolerance < bA.min.y) continue;
                    if (bA.max.z + CoplanarTolerance < bB.min.z || bB.max.z + CoplanarTolerance < bA.min.z) continue;

                    var mB = filters[j].sharedMesh;

                    // (b) Objetos duplicados empilhados no mesmo ponto
                    if (mA == mB &&
                        Vector3.Distance(rA.transform.position, rB.transform.position) <= DuplicatePosTol &&
                        Quaternion.Angle(rA.transform.rotation, rB.transform.rotation) <= DuplicateRotTol &&
                        Vector3.Distance(rA.transform.lossyScale, rB.transform.lossyScale) <= DuplicateScaleTol)
                    {
                        issues.Add(new ZFightIssue
                        {
                            scene = sceneName,
                            category = "STACKED_DUPLICATE",
                            objA = paths[i],
                            objB = paths[j],
                            axis = "POS",
                            depthDiff = Vector3.Distance(rA.transform.position, rB.transform.position),
                            overlapArea = bA.size.x * bA.size.z,
                            worldFocusPoint = bA.center,
                            details = $"Mesma malha '{mA.name}' empilhada na mesma posicao/rotacao/escala",
                            idA = rA.GetInstanceID(),
                            idB = rB.GetInstanceID()
                        });
                        continue;
                    }

                    // Calcula sobreposicao 2D em cada plano ortogonal
                    float ovX = Mathf.Min(bA.max.x, bB.max.x) - Mathf.Max(bA.min.x, bB.min.x);
                    float ovY = Mathf.Min(bA.max.y, bB.max.y) - Mathf.Max(bA.min.y, bB.min.y);
                    float ovZ = Mathf.Min(bA.max.z, bB.max.z) - Mathf.Max(bA.min.z, bB.min.z);

                    // Verifica faces coplanares em Y (pisos/tetos/plataformas), X e Z (paredes/paineis)
                    CheckAxisCoplanar(sceneName, rA, rB, paths[i], paths[j], bA, bB, "Y",
                        bA.min.y, bA.max.y, bB.min.y, bB.max.y, ovX, ovZ, issues);
                    CheckAxisCoplanar(sceneName, rA, rB, paths[i], paths[j], bA, bB, "X",
                        bA.min.x, bA.max.x, bB.min.x, bB.max.x, ovY, ovZ, issues);
                    CheckAxisCoplanar(sceneName, rA, rB, paths[i], paths[j], bA, bB, "Z",
                        bA.min.z, bA.max.z, bB.min.z, bB.max.z, ovX, ovY, issues);
                }
            }

            return issues;
        }

        private static void CheckAxisCoplanar(
            string sceneName, MeshRenderer rA, MeshRenderer rB, string pathA, string pathB,
            Bounds bA, Bounds bB, string axis,
            float aMin, float aMax, float bMin, float bMax,
            float ov1, float ov2, List<ZFightIssue> issues)
        {
            if (ov1 <= 0.05f || ov2 <= 0.05f) return;
            float area = ov1 * ov2;
            if (area < MinOverlapArea) return;

            // Faces voltadas pro mesmo lado (min-min ou max-max) => COPLANAR_OVERLAP classico (pisca forte)
            float dMinMin = Mathf.Abs(aMin - bMin);
            float dMaxMax = Mathf.Abs(aMax - bMax);
            // Faces opostas encostando (max-min ou min-max) => WALL_INTERNAL_SEAM entre pecas estruturais
            float dMaxMin = Mathf.Abs(aMax - bMin);
            float dMinMax = Mathf.Abs(aMin - bMax);

            Vector3 focus = (bA.center + bB.center) * 0.5f;

            if (dMinMin <= CoplanarTolerance || dMaxMax <= CoplanarTolerance)
            {
                float d = Mathf.Min(dMinMin, dMaxMax);
                string faceTag = dMinMin <= dMaxMax ? "min-min" : "max-max";
                issues.Add(new ZFightIssue
                {
                    scene = sceneName,
                    category = "COPLANAR_OVERLAP",
                    objA = pathA,
                    objB = pathB,
                    axis = axis,
                    depthDiff = d,
                    overlapArea = area,
                    worldFocusPoint = focus,
                    details = $"Faces coplanares ({faceTag}) no eixo {axis} (diff={d * 1000f:F1}mm)",
                    idA = rA.GetInstanceID(),
                    idB = rB.GetInstanceID()
                });
                return;
            }

            if ((dMaxMin <= CoplanarTolerance || dMinMax <= CoplanarTolerance) &&
                area >= 0.25f &&
                IsStructuralWallOrFloor(rA.name) && IsStructuralWallOrFloor(rB.name) &&
                axis != "Y")
            {
                float d = Mathf.Min(dMaxMin, dMinMax);
                issues.Add(new ZFightIssue
                {
                    scene = sceneName,
                    category = "WALL_INTERNAL_SEAM",
                    objA = pathA,
                    objB = pathB,
                    axis = axis,
                    depthDiff = d,
                    overlapArea = area,
                    worldFocusPoint = focus,
                    details = $"Junta coplanar/face interna entre pecas estruturais no eixo {axis} (diff={d * 1000f:F1}mm)",
                    idA = rA.GetInstanceID(),
                    idB = rB.GetInstanceID()
                });
            }
        }

        public static void ApplyFixesAcrossAllScenes()
        {
            Directory.CreateDirectory(VisualDir);
            var fixRecords = new List<ZFightFixRecord>();
            string[] scenes = GetGameScenes();

            foreach (string scenePath in scenes)
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool dirty = false;

                // Ate 6 passadas por cena para garantir convergencia sem criar novas coplanaridades
                for (int pass = 0; pass < 6; pass++)
                {
                    var issues = ScanActiveScene(sc.name);
                    if (issues.Count == 0) break;

                    var byId = new Dictionary<int, MeshRenderer>();
                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        byId[mr.GetInstanceID()] = mr;

                    var touchedThisPass = new HashSet<int>();
                    int fixedThisPass = 0;

                    foreach (var iss in issues)
                    {
                        if (!byId.TryGetValue(iss.idA, out var rA) || !byId.TryGetValue(iss.idB, out var rB)) continue;
                        if (rA == null || rB == null || !rA.enabled || !rB.enabled) continue;
                        if (touchedThisPass.Contains(iss.idB)) continue;

                        if (iss.category == "STACKED_DUPLICATE")
                        {
                            // Remove duplicata visual redundante (se tiver script/collider de gameplay, apenas desabilita o MeshRenderer redundante)
                            bool hasGameplay = rB.GetComponent<Collider>() != null || rB.GetComponents<MonoBehaviour>().Length > 0;
                            if (hasGameplay)
                            {
                                rB.enabled = false;
                                fixRecords.Add(new ZFightFixRecord
                                {
                                    scene = sc.name, category = iss.category, objA = iss.objA, objB = iss.objB,
                                    cause = iss.details,
                                    correction = "MeshRenderer redundante desabilitado (preservando componente/collider)"
                                });
                            }
                            else
                            {
                                UnityEngine.Object.DestroyImmediate(rB.gameObject);
                                fixRecords.Add(new ZFightFixRecord
                                {
                                    scene = sc.name, category = iss.category, objA = iss.objA, objB = iss.objB,
                                    cause = iss.details,
                                    correction = "Duplicata visual redundante removida da cena"
                                });
                            }
                            touchedThisPass.Add(iss.idB);
                            fixedThisPass++;
                            dirty = true;
                            continue;
                        }

                        if (iss.category == "COPLANAR_OVERLAP")
                        {
                            // Escolhe o menor objeto (em volume) para receber o micro-offset (0.006m-0.008m), evitando deslocar pisos/paredes principais
                            float volA = rA.bounds.size.x * rA.bounds.size.y * rA.bounds.size.z;
                            float volB = rB.bounds.size.x * rB.bounds.size.y * rB.bounds.size.z;
                            MeshRenderer target = (volB <= volA) ? rB : rA;
                            int targetId = (target == rB) ? iss.idB : iss.idA;
                            if (touchedThisPass.Contains(targetId)) continue;

                            float step = FixOffsetMeters + (pass * 0.001f);
                            Vector3 delta = Vector3.zero;
                            if (iss.axis == "Y") delta = new Vector3(0f, step, 0f);
                            else if (iss.axis == "X") delta = new Vector3(step, 0f, 0f);
                            else if (iss.axis == "Z") delta = new Vector3(0f, 0f, step);

                            // Aplica micro-offset na posicao (e reduz 0.002m na escala se ambos min e max coincidirem exatamente)
                            bool bothFacesMatch = false;
                            if (iss.axis == "Y") bothFacesMatch = Mathf.Abs(rA.bounds.min.y - rB.bounds.min.y) <= CoplanarTolerance && Mathf.Abs(rA.bounds.max.y - rB.bounds.max.y) <= CoplanarTolerance;
                            if (iss.axis == "X") bothFacesMatch = Mathf.Abs(rA.bounds.min.x - rB.bounds.min.x) <= CoplanarTolerance && Mathf.Abs(rA.bounds.max.x - rB.bounds.max.x) <= CoplanarTolerance;
                            if (iss.axis == "Z") bothFacesMatch = Mathf.Abs(rA.bounds.min.z - rB.bounds.min.z) <= CoplanarTolerance && Mathf.Abs(rA.bounds.max.z - rB.bounds.max.z) <= CoplanarTolerance;

                            target.transform.position += delta;
                            string corr;
                            if (bothFacesMatch)
                            {
                                // Micro-encolhimento de 0.3% no eixo para descolar ambas as faces opostas simultaneamente
                                Vector3 s = target.transform.localScale;
                                target.transform.localScale = new Vector3(s.x * 0.997f, s.y * 0.997f, s.z * 0.997f);
                                corr = $"Micro-offset +{step * 1000f:F1}mm no eixo {iss.axis} + ajuste fino 0.3% em '{target.name}'";
                            }
                            else
                            {
                                corr = $"Micro-offset +{step * 1000f:F1}mm no eixo {iss.axis} em '{target.name}'";
                            }

                            fixRecords.Add(new ZFightFixRecord
                            {
                                scene = sc.name, category = iss.category, objA = iss.objA, objB = iss.objB,
                                cause = iss.details, correction = corr
                            });
                            touchedThisPass.Add(targetId);
                            fixedThisPass++;
                            dirty = true;
                            continue;
                        }

                        if (iss.category == "WALL_INTERNAL_SEAM")
                        {
                            // Recua 0.4% no eixo da junta (ambas as faces afastam-se sem empurrar a peca contra outro vizinho)
                            Vector3 s = rB.transform.localScale;
                            if (iss.axis == "X") s.x *= 0.995f;
                            else if (iss.axis == "Z") s.z *= 0.995f;
                            rB.transform.localScale = s;
                            fixRecords.Add(new ZFightFixRecord
                            {
                                scene = sc.name, category = iss.category, objA = iss.objA, objB = iss.objB,
                                cause = iss.details,
                                correction = $"Face interna recuada 0.5% no eixo {iss.axis} em '{rB.name}'"
                            });
                            touchedThisPass.Add(iss.idB);
                            fixedThisPass++;
                            dirty = true;
                        }
                    }

                    if (fixedThisPass == 0) break;
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                    Debug.Log($"[ZFightScanner] Cena salva com correcoes: {scenePath}");
                }
            }

            var wrapper = new FixLogWrapper { records = fixRecords };
            File.WriteAllText(FixLogPath, JsonUtility.ToJson(wrapper, true), Encoding.UTF8);

            // Re-scan final obrigatorio (GATE 4)
            var remaining = ScanAllScenes(PostReportPath);
            Debug.Log($"[ZFightScanner] Auto-Fix concluido: {fixRecords.Count} correcoes aplicadas. Restantes apos fix: {remaining.Count} (Relatorio: {PostReportPath})");
        }

        /// <summary>
        /// Captura 90 frames (30s @ 3fps interpolados ou 180 frames @ 6fps -> 30s MP4) orbitando os pontos
        /// de maior incidencia de z-fighting nas cenas, usando exatamente os mesmos pontos de camera
        /// para gerar zfight_antes.mp4 (GATE 2) e zfight_depois.mp4 (GATE 5).
        /// </summary>
        private static void CaptureShowcaseFrames(string phase)
        {
            string framesDir = Path.Combine(VisualDir, $"_tmp_frames_{phase}");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            // Pontos fixos de camera nas 3 cenas principais (10s cada = 30s total)
            var shots = new (string scenePath, Vector3 pivot, float radius, float height)[]
            {
                ("Assets/Game/Scenes/Caldeiral.unity",    new Vector3(-22.5f, 2.4f, -18.0f), 7.5f, 2.2f),
                ("Assets/Game/Scenes/SetorExterno.unity", new Vector3(0.0f, 1.8f, 14.0f),   11.0f, 4.8f),
                ("Assets/Game/Scenes/Epicentro.unity",    new Vector3(0.0f, 2.0f, 12.0f),   11.5f, 5.2f),
            };

            const int framesPerScene = 60; // 3 cenas * 60 frames = 180 frames @ 6 fps = 30.0s exatos!
            const int width = 1280;
            const int heightPx = 720;

            var rt = new RenderTexture(width, heightPx, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, heightPx, TextureFormat.RGB24, false);
            int globalIdx = 0;

            foreach (var shot in shots)
            {
                EditorSceneManager.OpenScene(shot.scenePath, OpenSceneMode.Single);
                var camGo = new GameObject("[ZFight_CaptureCam]");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 250f;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.targetTexture = rt;

                for (int f = 0; f < framesPerScene; f++)
                {
                    float t = f / (float)(framesPerScene - 1);
                    float angle = Mathf.Lerp(-0.65f, 0.65f, t);
                    Vector3 camPos = shot.pivot + new Vector3(Mathf.Sin(angle) * shot.radius, shot.height + Mathf.Sin(t * Mathf.PI * 2f) * 0.45f, -Mathf.Cos(angle) * shot.radius);
                    cam.transform.position = camPos;
                    cam.transform.LookAt(shot.pivot);
                    cam.Render();

                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, heightPx), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    string outFile = Path.Combine(framesDir, $"frame_{globalIdx:D4}.png");
                    File.WriteAllBytes(outFile, tex.EncodeToPNG());
                    globalIdx++;
                }

                UnityEngine.Object.DestroyImmediate(camGo);
            }

            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log($"[ZFightScanner] Captura '{phase}' concluida: {globalIdx} frames em {framesDir}");
        }
    }
}
#endif
