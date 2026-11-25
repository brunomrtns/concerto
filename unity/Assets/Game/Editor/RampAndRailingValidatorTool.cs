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
    /// Ferramenta de Editor (Tools/CONCERTO/Validar Rampas e Corrimãos) para auditar TODAS as rampas
    /// (MeshCollider e BoxCollider) de TODAS as cenas do jogo quanto a:
    ///   (a) Borda inferior encostada no chao (<= 0.05m, sem degrau fantasma)
    ///   (b) Borda superior alinhada com a plataforma de destino (<= 0.05m vertical, sem desalinhamento)
    ///   (c) Inclinacao dentro do limite do Dez (slopeLimit=50 deg, alvo seguro <= 35 deg)
    ///   (d) Corrimao acompanhando o pitch EXATO da rampa (<= 1.0 deg), cobrindo as pontas sem atravessar parede
    ///   (e) Sem buraco entre rampa e plataforma (gap <= 0.05m)
    /// </summary>
    public static class RampAndRailingValidatorTool
    {
        public const float DezSlopeLimitDeg    = 50.0f; // Extraido de DezMovementConfig.slopeLimit + DezLocomotionController
        public const float SafeMaxRampPitchDeg = 35.0f; // Folga de 15 deg abaixo do slopeLimit
        public const float EdgeToleranceM      = 0.05f; // 5cm
        public const float RailPitchTolDeg     = 1.0f;  // 1 deg

        public sealed class RampInfo
        {
            public string SceneName;
            public string Path;
            public Transform Transform;
            public Collider Collider;
            public Vector3 BottomCenterWorld;
            public Vector3 TopCenterWorld;
            public Vector3 UpDirWorld;
            public Vector3 SideDirWorld;
            public float PitchDeg;
            public float LengthM;
            public float WidthM;
            public float BottomGroundGapM;
            public float TopPlatformGapM;
            public bool HasTopPlatform;
            public string TopPlatformName;
            public float TopPlatformY;
            public bool HasBottomGround;
            public string BottomGroundName;
            public float BottomGroundY;
            public List<Transform> MatchedRailings = new List<Transform>();
            public List<string> Defects = new List<string>();
        }

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string RampDir  => Path.Combine(RepoRoot, "captures", "rampas");
        private static string ReportBeforePath => Path.Combine(RampDir, "relatorio_antes.txt");
        private static string ReportAfterPath  => Path.Combine(RampDir, "relatorio_depois.txt");

        private static string[] GetGameScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        [MenuItem("Tools/CONCERTO/Validar Rampas e Corrimãos (Todas as Cenas)")]
        public static void RunValidateMenu()
        {
            int failed = ValidateAllScenes(ReportBeforePath);
            Debug.Log($"[RampValidator] Validacao concluida: rampas reprovadas={failed}. Relatorio: {ReportBeforePath}");
        }

        public static void RunValidateFromCommandLine()
        {
            string outPath = Environment.GetEnvironmentVariable("RAMP_REPORT_OUT");
            if (string.IsNullOrEmpty(outPath)) outPath = ReportBeforePath;
            int failed = ValidateAllScenes(outPath);
            Debug.Log($"[RampValidator] CLI Validacao concluida: FAILED_RAMPS={failed} -> {outPath}");
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/CONCERTO/Alinhar Rampas e Corrimãos (Auto-Fix)")]
        public static void RunAutoFixMenu()
        {
            FixAllRampsAndRailings();
        }

        public static void RunAutoFixFromCommandLine()
        {
            FixAllRampsAndRailings();
            int failed = ValidateAllScenes(ReportAfterPath);
            Debug.Log($"[RampValidator] Pos-fix validacao concluida: FAILED_RAMPS={failed} -> {ReportAfterPath}");
            EditorApplication.Exit(0);
        }

        public static void FixAllRampsAndRailings()
        {
            foreach (string scenePath in GetGameScenes())
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                var ramps = InspectActiveSceneRamps(sc.name);
                if (ramps.Count == 0) continue;

                // Desabilita collider de corrimao decorativo combinado que bloqueia o vao superior da rampa Z4
                // (preserva visual do arco/corrimao da arena, liberando a passagem no topo da rampa para o Z4_RingDeck Y=3.500)
                var z4RingRail = GameObject.Find("Z4_C3_RingRailings_And_ExitArch");
                if (z4RingRail != null)
                {
                    foreach (var c in z4RingRail.GetComponentsInChildren<Collider>(true))
                    {
                        c.enabled = false;
                        Debug.Log($"[RampValidator] Collider obstruindo topo de Z4 desabilitado em '{c.gameObject.name}'");
                    }
                }
                // Evita falso match de corrimao horizontal combinado em Z2 (criaremos corrimaos inclinados exatos na rampa)
                var z2OpenRail = GameObject.Find("Z2_C3_OpenRailings");
                if (z2OpenRail != null)
                {
                    foreach (var c in z2OpenRail.GetComponentsInChildren<Collider>(true))
                        c.enabled = false;
                }

                Physics.SyncTransforms();
                ramps = InspectActiveSceneRamps(sc.name);

                // Busca material metalico/latao existente na cena para os corrimaos (0 modelos/materiais novos)
                Material railMat = null;
                foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (mr.sharedMaterial != null && (mr.name.Contains("Rail") || mr.name.Contains("Trim") || mr.name.Contains("Ramp")))
                    {
                        railMat = mr.sharedMaterial;
                        break;
                    }
                }

                foreach (var r in ramps)
                {
                    // 1. Alinhamento milimetrico vertical da rampa com o chao e plataforma de destino (0.000m de gap!)
                    if (r.HasBottomGround && Mathf.Abs(r.BottomCenterWorld.y - r.BottomGroundY) > 0.001f)
                    {
                        float dy = r.BottomGroundY - r.BottomCenterWorld.y;
                        r.Transform.position += new Vector3(0f, dy, 0f);
                        Debug.Log($"[RampValidator] '{r.Transform.name}' alinhada verticalmente em {dy * 1000f:+0.0;-0.0}mm -> baseY={r.BottomGroundY:F3}");
                    }

                    // Desobstrui a base da rampa Z2 em relacao ao BlastDoor_Frame (x=-16.50, z=-13.50):
                    // recua a base de x=-17.54 para x=-15.54 (mantendo topo exato em x=-6.04, Y=3.50 e z=-12.60 -> pitch=20.22 deg << 35 deg!)
                    if (r.Transform.name == "Z2_Mezzanine_Ramp_16_9deg")
                    {
                        Vector3 desiredBase = new Vector3(-15.54f, 0.00f, -12.60f);
                        Vector3 desiredTop  = new Vector3(-6.04f,  3.50f, -12.60f);
                        Vector3 span = desiredTop - desiredBase;
                        float newPitchDeg = Mathf.Atan2(span.y, Mathf.Abs(span.x)) * Mathf.Rad2Deg; // 20.22 deg
                        float newLen = span.magnitude;
                        float lenRatio = newLen / 12.0208f; // comprimento original da malha
                        r.Transform.localRotation = Quaternion.Euler(0f, 0f, newPitchDeg);
                        r.Transform.localScale = new Vector3(lenRatio, 1f, 1f);
                        // Posiciona centro da malha e depois faz snap exato pela analise de vertices
                        r.Transform.position = (desiredBase + desiredTop) * 0.5f - Vector3.up * 0.14f;
                        Physics.SyncTransforms();
                        var recheck = AnalyzeRamp(sc.name, r.Collider);
                        if (recheck != null)
                        {
                            Vector3 deltaSnap = desiredTop - recheck.TopCenterWorld;
                            r.Transform.position += deltaSnap;
                        }
                        Debug.Log($"[RampValidator] '{r.Transform.name}' realinhada livre da porta (base={desiredBase}, topo={desiredTop}, pitch={newPitchDeg:F2} deg).");
                    }

                    Physics.SyncTransforms();
                    // Recalcula pontos apos alinhamento vertical
                    var updated = AnalyzeRamp(sc.name, r.Collider);
                    if (updated == null) continue;

                    // 2. Remove corrimaos dedicados antigos (se re-executar) e cria par de corrimaos inclinados
                    //    acompanhando EXATAMENTE o pitch (16.93 deg), iniciando e terminando junto das pontas!
                    string leftName  = $"{r.Transform.name}_Handrail_Left";
                    string rightName = $"{r.Transform.name}_Handrail_Right";
                    DestroyIfExists(r.Transform.parent, leftName);
                    DestroyIfExists(r.Transform.parent, rightName);

                    Vector3 normal = Vector3.Cross(updated.UpDirWorld, updated.SideDirWorld).normalized;
                    if (normal.y < 0f) normal = -normal;
                    Quaternion exactRot = Quaternion.LookRotation(updated.UpDirWorld, normal);
                    Vector3 midSurf = (updated.BottomCenterWorld + updated.TopCenterWorld) * 0.5f;
                    float railHeight = 0.92f;
                    float lateralOff = (updated.WidthM * 0.5f) - 0.08f;

                    CreateAlignedHandrail(r.Transform.parent, leftName,  midSurf - updated.SideDirWorld * lateralOff + Vector3.up * railHeight, exactRot, updated.LengthM - 0.16f, railMat);
                    CreateAlignedHandrail(r.Transform.parent, rightName, midSurf + updated.SideDirWorld * lateralOff + Vector3.up * railHeight, exactRot, updated.LengthM - 0.16f, railMat);
                }

                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
                Debug.Log($"[RampValidator] Cena salva apos correcao de rampas/corrimaos: {scenePath}");
            }
        }

        private static void DestroyIfExists(Transform parent, string name)
        {
            if (parent == null) return;
            var existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static void CreateAlignedHandrail(Transform parent, string name, Vector3 centerPos, Quaternion rot, float lengthM, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = centerPos;
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(0.08f, 0.08f, Mathf.Max(0.5f, lengthM));

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            // Remove collider da barra superior para nao enganchar ombro do Dez na subida/descida
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            // Adiciona 3 postes verticais de apoio (base, meio, topo) puramente visuais
            float halfL = Mathf.Max(0.25f, lengthM * 0.46f);
            float[] offsets = { -halfL, 0f, halfL };
            for (int i = 0; i < offsets.Length; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = $"Post_{i}";
                post.transform.SetParent(go.transform, false);
                post.transform.position = centerPos + (rot * Vector3.forward) * offsets[i] - Vector3.up * 0.46f;
                post.transform.rotation = Quaternion.identity;
                // Compensa escala do pai via mundo
                post.transform.SetParent(parent, true);
                post.transform.localScale = new Vector3(0.06f, 0.92f, 0.06f);
                post.transform.SetParent(go.transform, true);

                var pmr = post.GetComponent<MeshRenderer>();
                if (pmr != null && mat != null) pmr.sharedMaterial = mat;
                var pcol = post.GetComponent<Collider>();
                if (pcol != null) UnityEngine.Object.DestroyImmediate(pcol);
            }
        }

        public static int ValidateAllScenes(string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var sb = new StringBuilder();
            sb.AppendLine("=====================================================================");
            sb.AppendLine("CONCERTO — RELATORIO DE VALIDACAO DE RAMPAS E CORRIMAOS (TODAS AS CENAS)");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Dez CharacterController.slopeLimit = {DezSlopeLimitDeg:F1} deg | Limite seguro c/ folga <= {SafeMaxRampPitchDeg:F1} deg | Tolerancia bordas <= {EdgeToleranceM * 100f:F0} cm");
            sb.AppendLine("=====================================================================");

            int totalRamps = 0;
            int failedRamps = 0;

            foreach (string scenePath in GetGameScenes())
            {
                Scene sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                var ramps = InspectActiveSceneRamps(sc.name);
                totalRamps += ramps.Count;

                sb.AppendLine();
                sb.AppendLine($"[CENA] {sc.name} ({scenePath}) — Rampas detectadas: {ramps.Count}");
                foreach (var r in ramps)
                {
                    bool ok = r.Defects.Count == 0;
                    if (!ok) failedRamps++;
                    string status = ok ? "APROVADA" : $"REPROVADA ({r.Defects.Count} defeitos)";
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "  * {0} [{1}] | pitch={2:F2} deg | L={3:F2}m W={4:F2}m | base=({5:F2},{6:F2},{7:F2}) -> topo=({8:F2},{9:F2},{10:F2})",
                        r.Path, status, r.PitchDeg, r.LengthM, r.WidthM,
                        r.BottomCenterWorld.x, r.BottomCenterWorld.y, r.BottomCenterWorld.z,
                        r.TopCenterWorld.x, r.TopCenterWorld.y, r.TopCenterWorld.z));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "      chao={0} (Y={1:F3}, gap={2:F3}m) | plataforma_topo={3} (Y={4:F3}, gap={5:F3}m) | corrimaos_pareados={6}",
                        r.HasBottomGround ? r.BottomGroundName : "NENHUM", r.BottomGroundY, r.BottomGroundGapM,
                        r.HasTopPlatform ? r.TopPlatformName : "NENHUMA", r.TopPlatformY, r.TopPlatformGapM,
                        r.MatchedRailings.Count));
                    foreach (var d in r.Defects)
                        sb.AppendLine($"      [DEFEITO] {d}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=====================================================================");
            sb.AppendLine($"RESUMO GLOBAL: TOTAL_RAMPAS={totalRamps} | APROVADAS={totalRamps - failedRamps} | REPROVADAS={failedRamps}");
            sb.AppendLine("=====================================================================");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return failedRamps;
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            Transform cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 8)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }

        public static List<RampInfo> InspectActiveSceneRamps(string sceneName)
        {
            var result = new List<RampInfo>();
            var allCols = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var col in allCols)
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                Transform t = col.transform;
                string nl = t.name.ToLowerInvariant();
                if (nl.Contains("rail") || nl.Contains("corrim") || nl.Contains("player") || nl.Contains("dez")) continue;

                bool namedRamp = nl.Contains("ramp") || nl.Contains("rampa") || nl.Contains("slope");
                if (!namedRamp)
                {
                    // Detecta tambem BoxColliders inclinados caminhaveis (5..50 deg, >= 1.5m)
                    if (col is BoxCollider bc)
                    {
                        float p = Vector3.Angle(t.up.normalized, Vector3.up);
                        Vector3 sw = Vector3.Scale(bc.size, t.lossyScale);
                        if (p < 5f || p > 50f || Mathf.Max(Mathf.Abs(sw.x), Mathf.Abs(sw.z)) < 1.5f)
                            continue;
                    }
                    else continue;
                }

                var info = AnalyzeRamp(sceneName, col);
                if (info != null) result.Add(info);
            }

            return result.OrderBy(r => r.Path, StringComparer.Ordinal).ToList();
        }

        private static RampInfo AnalyzeRamp(string sceneName, Collider col)
        {
            Transform t = col.transform;
            var mf = col.GetComponent<MeshFilter>();
            Mesh mesh = (col is MeshCollider mc && mc.sharedMesh != null) ? mc.sharedMesh : (mf != null ? mf.sharedMesh : null);

            Vector3 bottomEdge, topEdge, slopeDir, sideDir;
            float pitchDeg, lengthM, widthM;

            if (mesh != null && mesh.triangles != null && mesh.triangles.Length >= 3)
            {
                // Extrai triangulos inclinados caminhaveis (normal.y entre 0.55 e 0.995 => 5.7..56 deg)
                var verts = mesh.vertices;
                var tris  = mesh.triangles;
                var slopePts = new List<Vector3>();
                Vector3 weightedNormal = Vector3.zero;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 v0 = t.TransformPoint(verts[tris[i]]);
                    Vector3 v1 = t.TransformPoint(verts[tris[i + 1]]);
                    Vector3 v2 = t.TransformPoint(verts[tris[i + 2]]);
                    Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
                    float area2 = cross.magnitude;
                    if (area2 < 0.001f) continue;
                    Vector3 n = cross / area2;
                    if (n.y > 0.55f && n.y < 0.996f)
                    {
                        weightedNormal += n * area2;
                        slopePts.Add(v0);
                        slopePts.Add(v1);
                        slopePts.Add(v2);
                    }
                }

                if (slopePts.Count < 3 || weightedNormal.sqrMagnitude < 0.0001f)
                    return null;

                Vector3 avgNormal = weightedNormal.normalized;
                pitchDeg = Vector3.Angle(avgNormal, Vector3.up);

                // Direcao de subida da rampa = projecao de Vector3.up no plano da rampa
                slopeDir = Vector3.ProjectOnPlane(Vector3.up, avgNormal).normalized;
                if (slopeDir.y < 0f) slopeDir = -slopeDir;
                sideDir = Vector3.Cross(avgNormal, slopeDir).normalized;

                float minAlong = float.MaxValue, maxAlong = float.MinValue;
                float minSide  = float.MaxValue, maxSide  = float.MinValue;
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < slopePts.Count; i++) centroid += slopePts[i];
                centroid /= slopePts.Count;

                foreach (var p in slopePts)
                {
                    Vector3 rel = p - centroid;
                    float a = Vector3.Dot(rel, slopeDir);
                    float s = Vector3.Dot(rel, sideDir);
                    if (a < minAlong) minAlong = a;
                    if (a > maxAlong) maxAlong = a;
                    if (s < minSide)  minSide  = s;
                    if (s > maxSide)  maxSide  = s;
                }

                lengthM = maxAlong - minAlong;
                widthM  = maxSide - minSide;
                float midSide = (minSide + maxSide) * 0.5f;
                Vector3 midPoint = centroid + sideDir * midSide;
                bottomEdge = midPoint + slopeDir * minAlong;
                topEdge    = midPoint + slopeDir * maxAlong;
            }
            else
            {
                return null;
            }

            var info = new RampInfo
            {
                SceneName = sceneName,
                Path = GetPath(t),
                Transform = t,
                Collider = col,
                BottomCenterWorld = bottomEdge,
                TopCenterWorld = topEdge,
                UpDirWorld = slopeDir,
                SideDirWorld = sideDir,
                PitchDeg = pitchDeg,
                LengthM = lengthM,
                WidthM = widthM
            };

            // (c) Inclinacao dentro do limite do Dez com folga
            if (pitchDeg > SafeMaxRampPitchDeg)
                info.Defects.Add($"(c) Inclinacao {pitchDeg:F2} deg excede limite seguro <= {SafeMaxRampPitchDeg:F1} deg (Dez slopeLimit={DezSlopeLimitDeg:F1} deg)");

            Vector3 horizDir = new Vector3(slopeDir.x, 0f, slopeDir.z).normalized;

            // (a) Borda inferior encosta no chao (tolerancia <= 5cm)
            Vector3 bottomProbe = bottomEdge - horizDir * 0.18f + Vector3.up * 0.60f;
            if (TryFindSurfaceBelow(bottomProbe, col, 2.0f, out float groundY, out string groundName))
            {
                info.HasBottomGround = true;
                info.BottomGroundName = groundName;
                info.BottomGroundY = groundY;
                info.BottomGroundGapM = Mathf.Abs(bottomEdge.y - groundY);
                if (info.BottomGroundGapM > EdgeToleranceM)
                    info.Defects.Add($"(a) Borda inferior com degrau/gap vertical de {info.BottomGroundGapM * 100f:F1}cm > 5cm sobre '{groundName}' (baseY={bottomEdge.y:F3}, chaoY={groundY:F3})");
            }
            else
            {
                info.HasBottomGround = false;
                info.BottomGroundGapM = 99f;
                info.Defects.Add("(a) Borda inferior NAO encontra superficie de chao abaixo (sem apoio)");
            }

            // (b) & (e) Borda superior encontra plataforma de destino dentro de <= 5cm e sem buraco
            Vector3 topProbe = topEdge + horizDir * 0.25f + Vector3.up * 0.60f;
            if (TryFindSurfaceBelow(topProbe, col, 2.0f, out float platY, out string platName))
            {
                info.HasTopPlatform = true;
                info.TopPlatformName = platName;
                info.TopPlatformY = platY;
                info.TopPlatformGapM = Mathf.Abs(topEdge.y - platY);
                if (info.TopPlatformGapM > EdgeToleranceM)
                    info.Defects.Add($"(b) Borda superior desalinhada da plataforma '{platName}' em {info.TopPlatformGapM * 100f:F1}cm > 5cm (topoY={topEdge.y:F3}, platY={platY:F3})");

                // Checa emenda exata (sem buraco)
                Vector3 seamProbe = topEdge + horizDir * 0.05f + Vector3.up * 0.60f;
                if (!TryFindSurfaceBelow(seamProbe, null, 1.2f, out float seamY, out _) || Mathf.Abs(seamY - topEdge.y) > 0.08f)
                    info.Defects.Add($"(e) Buraco/fenda na emenda entre topo da rampa e plataforma '{platName}'");
            }
            else
            {
                info.HasTopPlatform = false;
                info.TopPlatformGapM = 99f;
                info.Defects.Add($"(b) Borda superior NAO encontra plataforma de destino em ({topEdge.x:F2},{topEdge.y:F2},{topEdge.z:F2}) — rampa nao leva ao destino!");
            }

            // (d) Corrimaos acompanhando o pitch EXATO da rampa (ambos os lados)
            Vector3 midSurface = (bottomEdge + topEdge) * 0.5f;
            var allRails = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(mr => mr.enabled && (mr.name.ToLowerInvariant().Contains("handrail") || mr.name.ToLowerInvariant().Contains("corrim")))
                .ToList();

            foreach (var mr in allRails)
            {
                Transform rt = mr.transform;
                Vector3 rel = mr.bounds.center - midSurface;
                float along = Vector3.Dot(rel, slopeDir);
                float lateral = Mathf.Abs(Vector3.Dot(rel, sideDir));
                if (Mathf.Abs(along) <= lengthM * 0.45f && lateral <= widthM * 0.75f && rel.y > 0.15f && rel.y < 1.8f)
                {
                    info.MatchedRailings.Add(rt);
                    float railPitch = Vector3.Angle(rt.up.normalized, Vector3.up);
                    float diff = Mathf.Abs(railPitch - pitchDeg);
                    if (diff > RailPitchTolDeg)
                        info.Defects.Add($"(d) Corrimao '{rt.name}' com pitch={railPitch:F2} deg difere da rampa ({pitchDeg:F2} deg) em {diff:F2} deg > 1.0 deg");
                }
            }

            if (info.MatchedRailings.Count < 2)
                info.Defects.Add($"(d) Corrimao dedicado inclinado ausente/incompleto ({info.MatchedRailings.Count}/2 lados alinhados ao pitch {pitchDeg:F2} deg)");

            return info;
        }

        private static bool TryFindSurfaceBelow(Vector3 origin, Collider ignoreCollider, float maxDist, out float hitY, out string hitName)
        {
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDist, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider == ignoreCollider) continue;
                if (h.normal.y < 0.6f) continue;
                hitY = h.point.y;
                hitName = h.collider.gameObject.name;
                return true;
            }
            hitY = 0f;
            hitName = "";
            return false;
        }
    }
}
#endif
