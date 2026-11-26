using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Animation
{
    /// <summary>
    /// Crista fragil quebravel exclusivamente pelo Crava-Mola (GDD 04 §1 — GATE 4).
    /// </summary>
    public sealed class FragileCrestTarget : MonoBehaviour
    {
        public bool IsBroken { get; private set; }

        public void BreakCrest()
        {
            if (IsBroken) return;
            IsBroken = true;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            Debug.Log($"[CravaMola] Crista fragil '{name}' quebrada pela onda de choque (3m)!");
        }

        public void ResetCrest()
        {
            IsBroken = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = true;
        }
    }

    /// <summary>
    /// GATE 6 (Movimentacao GDD): Zona de treino diegetica na oficina (Caldeiral),
    /// sem parede de texto — ensina visualmente Corrimao, Rebote, Crava-Mola e Martelada Aerea.
    /// </summary>
    public sealed class GddMovementTrainingZone : MonoBehaviour
    {
        public static GddMovementTrainingZone Instance { get; private set; }
        public FragileCrestTarget TrainingFragileCrest { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInActiveScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene sc, LoadSceneMode mode)
        {
            EnsureInActiveScene(sc);
        }

        private static void EnsureInActiveScene(Scene sc)
        {
            if (sc.name != "Caldeiral") return;
            if (Instance != null) return;

            var root = new GameObject("GddMovement_TrainingZone");
            root.transform.position = new Vector3(15.0f, 0.0f, -16.5f);
            Instance = root.AddComponent<GddMovementTrainingZone>();
            Instance.BuildDiegeticStations();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildDiegeticStations()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wallMat  = MakeMat(lit, new Color(0.18f, 0.24f, 0.30f, 1f), new Color(0.05f, 0.14f, 0.20f, 1f));
            Material cyanMat  = MakeMat(lit, new Color(0.18f, 0.86f, 1.00f, 1f), new Color(0.20f, 1.15f, 1.40f, 1f));
            Material crestMat = MakeMat(lit, new Color(0.95f, 0.42f, 0.16f, 1f), new Color(0.85f, 0.28f, 0.08f, 1f));
            Material padMat   = MakeMat(lit, new Color(0.96f, 0.78f, 0.22f, 1f), new Color(0.60f, 0.44f, 0.08f, 1f));

            // 1. Estacao Corrimao (Muro de wall-run 10m com trilho luminoso ciano)
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Training_WallRunPanel";
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = new Vector3(0f, 2.2f, 2.8f);
            wall.transform.localScale = new Vector3(10.0f, 4.4f, 0.45f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            var railStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            railStrip.name = "Training_WallRunCueStrip";
            railStrip.transform.SetParent(wall.transform, false);
            railStrip.transform.localPosition = new Vector3(0f, 0.0f, -0.55f);
            railStrip.transform.localScale = new Vector3(0.96f, 0.06f, 0.12f);
            railStrip.GetComponent<MeshRenderer>().sharedMaterial = cyanMat;
            Destroy(railStrip.GetComponent<Collider>());

            // 2. Estacao Rebote (Plataforma elevada +3,8m com borda luminosa)
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Training_ReboteLedge";
            ledge.transform.SetParent(transform, false);
            ledge.transform.localPosition = new Vector3(6.2f, 3.6f, 0.0f);
            ledge.transform.localScale = new Vector3(2.8f, 0.4f, 2.8f);
            ledge.GetComponent<MeshRenderer>().sharedMaterial = cyanMat;

            // 3. Estacao Crava-Mola (Crista fragil laranja-ambar quebravel)
            var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Training_FragileCrest";
            crest.transform.SetParent(transform, false);
            crest.transform.localPosition = new Vector3(-3.0f, 0.45f, -1.6f);
            crest.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
            crest.GetComponent<MeshRenderer>().sharedMaterial = crestMat;
            TrainingFragileCrest = crest.AddComponent<FragileCrestTarget>();

            // 4. Estacao Martelada Aerea (Pad dourado de impacto 3m)
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Training_AirHammerPad";
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = new Vector3(2.2f, 0.06f, -1.6f);
            pad.transform.localScale = new Vector3(2.4f, 0.06f, 2.4f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = padMat;
        }

        private static Material MakeMat(Shader sh, Color baseCol, Color emissive)
        {
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emissive);
            }
            return m;
        }
    }
}
