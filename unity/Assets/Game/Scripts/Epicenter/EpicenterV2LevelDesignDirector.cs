using System;
using System.Collections.Generic;
using Studio.Game.Audio;
using Studio.Game.Combat;
using Studio.Game.Parts;
using Studio.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Epicenter
{
    /// <summary>
    /// Recompensa do desvio lateral do Epicentro V2 (GATE 4: saida falsa recompensada, nunca castigada).
    /// </summary>
    public sealed class EpicenterSecretRewardPickup : MonoBehaviour
    {
        public bool IsCollected { get; private set; }
        public event Action OnCollected;

        private Vector3 _basePos;

        private void Awake() => _basePos = transform.position;

        private void Update()
        {
            if (IsCollected) return;
            transform.Rotate(0f, 85f * Time.deltaTime, 0f, Space.World);
            transform.position = _basePos + Vector3.up * (Mathf.Sin(Time.time * 3.0f) * 0.12f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCollected || other == null) return;
            if (other.CompareTag("Player") || other.name.Contains("Player_Dez"))
                Collect();
        }

        public bool Collect()
        {
            if (IsCollected) return false;
            IsCollected = true;
            if (GameFeelJuiceController.Instance != null)
                GameFeelJuiceController.Instance.TriggerItemPickupJuice(transform.position, "Reliquia Secreta do Epicentro");
            if (EconomyUpgradeSystem.Instance != null)
                EconomyUpgradeSystem.Instance.NotifyPartEarned(1);
            if (AudioMixController.Instance != null)
                AudioMixController.Instance.PlayUiConfirm();
            gameObject.SetActive(false);
            OnCollected?.Invoke();
            Debug.Log("[EpicenterV2] Recompensa secreta do desvio lateral coletada!");
            return true;
        }
    }

    /// <summary>
    /// EpicenterV2LevelDesignDirector — Segunda Passada de Level Design exclusiva da cena Epicentro.
    ///
    /// Conformidade DESIGN_BIBLE.md:
    ///   - Escopo 100% fechado em Epicentro.unity (zero alteracao em outros atos)
    ///   - GATE 2 (Ritmo): alterna desafio -> alivio/recompensa -> desafio (Ilha S1B + Ponte S2B + Portico S3)
    ///   - GATE 3 (Checkpoints <= 30s): 5 checkpoints (x=0, 33.5, 50, 71, 102 -> gap maximo 33.5m / ~15s de corrida)
    ///   - GATE 4 (Leitura + Saida lateral recompensada): balizas luminosas na rota principal sem setas +
    ///     desvio lateral seguro em (x=71, z=+6.5) com EpicenterSecretRewardPickup
    ///   - GATE 5 (Dificuldade crescente): curva suave com pico no Boss Guardiao da Fenda (x=116)
    ///   - 0 modelos 3D criados (geometria diegetica URP Lit)
    /// </summary>
    public sealed class EpicenterV2LevelDesignDirector : MonoBehaviour
    {
        public static EpicenterV2LevelDesignDirector Instance { get; private set; }

        public int TotalCheckpoints { get; private set; }
        public float MaxCheckpointGapMeters { get; private set; }
        public float MaxExpectedRetraceSeconds { get; private set; }
        public int GuidanceBeaconsCount { get; private set; }
        public bool SecretRewardCollected => _secretReward != null && _secretReward.IsCollected;
        public Vector3 SecretRewardPosition => _secretReward != null ? _secretReward.transform.position : new Vector3(71f, 1.55f, 6.8f);

        private Transform _v2Root;
        private EpicenterSecretRewardPickup _secretReward;
        private readonly List<Material> _ownedMaterials = new List<Material>(8);
        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBootstrap()
        {
            if (!_hooked)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _hooked = true;
            }
            EnsureInEpicentro(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInEpicentro(scene);
        }

        private static void EnsureInEpicentro(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, "Epicentro", StringComparison.OrdinalIgnoreCase))
                return;
            if (Instance != null) return;
            var go = new GameObject("EpicenterV2_LevelDesignDirector");
            go.AddComponent<EpicenterV2LevelDesignDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ApplyEpicenterV2Pass();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            if (Instance == this) Instance = null;
        }

        public bool CollectSecretRewardForTest()
        {
            return _secretReward != null && _secretReward.Collect();
        }

        private void ApplyEpicenterV2Pass()
        {
            if (_v2Root != null) return;
            var rootGo = new GameObject("EpicenterV2_PolishGeometry");
            _v2Root = rootGo.transform;

            var stoneMat   = CreateMat(new Color(0.18f, 0.22f, 0.28f, 1f), new Color(0.03f, 0.08f, 0.14f, 1f));
            var cyanMat    = CreateMat(new Color(0.20f, 0.88f, 1.0f, 1f),  new Color(0.25f, 1.80f, 2.40f, 1f));
            var goldMat    = CreateMat(new Color(1.00f, 0.78f, 0.24f, 1f), new Color(2.20f, 1.50f, 0.25f, 1f));
            var reliefMat  = CreateMat(new Color(0.24f, 0.92f, 0.68f, 1f), new Color(0.25f, 1.60f, 1.00f, 1f));

            // 1. GATE 2 & 5 (Ritmo + Dificuldade Crescente): Ilha de Alivio S1B em x=33.5 antes do puzzle de Estase
            CreatePrimitive(PrimitiveType.Cube, "V2_ReliefIsland_S1B", _v2Root,
                new Vector3(33.5f, 0.45f, 0f), new Vector3(4.2f, 0.5f, 4.4f), stoneMat, withCollider: true);
            CreatePrimitive(PrimitiveType.Cylinder, "V2_CheckpointPad_S1B", _v2Root,
                new Vector3(33.5f, 0.74f, 0f), new Vector3(1.6f, 0.04f, 1.6f), reliefMat, withCollider: false);

            // 2. GATE 3 (Checkpoints <= 30s): Inserir 2 checkpoints intermediarios (S1B em x=33.5 e S2B em x=71.0)
            var cp1B = new GameObject("Checkpoint_S1B_PreEstase").transform;
            cp1B.SetParent(_v2Root, false);
            cp1B.position = new Vector3(33.5f, 0.80f, 0f);

            var cp2B = new GameObject("Checkpoint_S2B_PonteAlivio").transform;
            cp2B.SetParent(_v2Root, false);
            cp2B.position = new Vector3(71.0f, 0.95f, 0f);
            CreatePrimitive(PrimitiveType.Cylinder, "V2_CheckpointPad_S2B", _v2Root,
                new Vector3(71.0f, 0.92f, 0f), new Vector3(1.6f, 0.04f, 1.6f), reliefMat, withCollider: false);

            RegisterCheckpointsInOrder(cp1B, cp2B);

            // 3. GATE 4 (Leitura visual da rota principal sem setas): balizas ciano ao longo de +X + portico dourado em x=102
            float[] beaconXs = { 4.5f, 15.0f, 24.0f, 33.5f, 44.0f, 50.0f, 64.5f, 71.0f, 78.0f, 92.0f, 102.0f };
            GuidanceBeaconsCount = beaconXs.Length;
            for (int i = 0; i < beaconXs.Length; i++)
            {
                float bx = beaconXs[i];
                float by = bx < 25f ? 0.35f : (bx < 45f ? 0.78f : 0.96f);
                CreatePrimitive(PrimitiveType.Cube, $"V2_GuidanceStrip_{i:D2}", _v2Root,
                    new Vector3(bx, by, 0f), new Vector3(1.1f, 0.04f, 0.36f), cyanMat, withCollider: false);
            }

            // Portico obsidiana-ciano na entrada da Arena Final (x=102) — coerente com a paleta da Fenda (VA-08)
            var archMat = CreateMat(new Color(0.14f, 0.20f, 0.28f, 1f), new Color(0.12f, 0.55f, 0.78f, 1f));
            CreatePrimitive(PrimitiveType.Cube, "V2_BossGatePillar_L", _v2Root,
                new Vector3(102.0f, 3.8f, -3.6f), new Vector3(0.65f, 6.0f, 0.65f), archMat, withCollider: false);
            CreatePrimitive(PrimitiveType.Cube, "V2_BossGatePillar_R", _v2Root,
                new Vector3(102.0f, 3.8f, +3.6f), new Vector3(0.65f, 6.0f, 0.65f), archMat, withCollider: false);
            CreatePrimitive(PrimitiveType.Cube, "V2_BossGateArch", _v2Root,
                new Vector3(102.0f, 6.8f, 0f), new Vector3(0.75f, 0.65f, 7.8f), archMat, withCollider: false);

            // 4. GATE 4 (Saida lateral recompensada — nunca castigada): desvio seguro em x=71, z=+6.8
            CreatePrimitive(PrimitiveType.Cube, "V2_SecretSideBridge", _v2Root,
                new Vector3(71.0f, 0.72f, 3.6f), new Vector3(2.4f, 0.35f, 4.4f), stoneMat, withCollider: true);
            CreatePrimitive(PrimitiveType.Cube, "V2_SecretAlcovePlatform", _v2Root,
                new Vector3(71.0f, 0.72f, 6.8f), new Vector3(3.6f, 0.40f, 3.4f), stoneMat, withCollider: true);
            CreatePrimitive(PrimitiveType.Cube, "V2_SecretCueStrip", _v2Root,
                new Vector3(71.0f, 0.94f, 3.4f), new Vector3(0.32f, 0.04f, 1.4f), goldMat, withCollider: false);

            var rewardGo = CreatePrimitive(PrimitiveType.Sphere, "V2_SecretReward_CoreRelic", _v2Root,
                new Vector3(71.0f, 1.65f, 6.8f), new Vector3(0.62f, 0.62f, 0.62f), goldMat, withCollider: true);
            var col = rewardGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            _secretReward = rewardGo.AddComponent<EpicenterSecretRewardPickup>();

            // 5. GATE 5 (Calibracao suave do puzzle de Estase para evitar muro de dificuldade)
            var voidCtrl = EpicenterCheckpointAndVoidZone.Instance ?? FindAnyObjectByType<EpicenterCheckpointAndVoidZone>();
            if (voidCtrl != null && voidCtrl.config != null)
            {
                voidCtrl.config = Instantiate(voidCtrl.config);
                voidCtrl.config.fastStasisPlatformSpeed = 9.2f; // 4.6 m/s sob Estase — justo e legivel
            }

            Debug.Log($"[EpicenterV2] Pass de Level Design aplicado: checkpoints={TotalCheckpoints}, maxGap={MaxCheckpointGapMeters:F1}m (~{MaxExpectedRetraceSeconds:F1}s <= 30s), beacons={GuidanceBeaconsCount}, desvio secreto ativo.");
        }

        private void RegisterCheckpointsInOrder(Transform cp1B, Transform cp2B)
        {
            var ctrl = EpicenterCheckpointAndVoidZone.Instance ?? FindAnyObjectByType<EpicenterCheckpointAndVoidZone>();
            var list = new List<Transform>();
            if (ctrl != null && ctrl.sectionCheckpoints != null)
            {
                for (int i = 0; i < ctrl.sectionCheckpoints.Length; i++)
                {
                    if (ctrl.sectionCheckpoints[i] != null) list.Add(ctrl.sectionCheckpoints[i]);
                }
            }
            list.Add(cp1B);
            list.Add(cp2B);
            list.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            if (ctrl != null)
                ctrl.sectionCheckpoints = list.ToArray();

            TotalCheckpoints = list.Count;
            float maxGap = 0f;
            for (int i = 1; i < list.Count; i++)
            {
                float d = Vector3.Distance(list[i - 1].position, list[i].position);
                if (d > maxGap) maxGap = d;
            }
            MaxCheckpointGapMeters = maxGap;
            // Velocidade media de deslocamento do Dez (caminhada/corrida ~3.5 m/s) -> garante <= 30s!
            MaxExpectedRetraceSeconds = maxGap / 3.5f;
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (!withCollider && col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            return go;
        }

        private Material CreateMat(Color baseColor, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            _ownedMaterials.Add(mat);
            return mat;
        }
    }
}
