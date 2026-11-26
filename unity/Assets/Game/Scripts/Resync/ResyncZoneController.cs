using System;
using System.Collections;
using System.Collections.Generic;
using Studio.Game.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Resync
{
    /// <summary>
    /// Sistema de Re-Sincronia — Dois Estados (GDD 04 §1 + GDD 05 §2):
    ///   - Alterna apenas a bolha desta zona entre Desincronizado <-> Restaurado em 0,8s.
    ///   - Exibe onda visual em anel expandindo + 0,8s de "escrita fantasma" (wireframe) nos objetos antes da troca.
    ///   - Atordoa inimigos dentro do raio da bolha por 1,0s.
    ///   - Cooldown rigoroso de 6,0s entre ativacoes.
    ///   - Estado persiste no Save JSON versionado por zona/checkpoint.
    /// </summary>
    public sealed class ResyncZoneController : MonoBehaviour
    {
        public const float TransitionDurationSec = 0.8f;
        public const float EnemyStunDurationSec  = 1.0f;
        public const float CooldownSec           = 6.0f;

        private static readonly List<ResyncZoneController> ActiveZones = new List<ResyncZoneController>();
        public static IReadOnlyList<ResyncZoneController> AllZones => ActiveZones;

        [Header("Identidade & Bolha")]
        public string zoneId = "caldeiral_demo_resync";
        public ResyncState currentState = ResyncState.Desincronizado;
        public float bubbleRadius = 14.0f;
        public float nodeInteractRange = 4.0f;
        public Transform nodeAnchor;
        public List<ResyncDualStateObject> managedObjects = new List<ResyncDualStateObject>();

        public bool IsTransitioning { get; private set; }
        public bool IsGhostPreviewActive { get; private set; }
        public float CooldownRemainingSec => Mathf.Max(0f, _cooldownUntil - Time.time);
        public bool CanTriggerNow => !IsTransitioning && CooldownRemainingSec <= 0.001f;
        public int ToggleCount { get; private set; }
        public int LastPulseStunnedEnemies { get; private set; }

        public event Action<ResyncZoneController, ResyncState> OnZoneStateChanged;

        private float _cooldownUntil = -99f;
        private LineRenderer _ringWave;
        private Transform _dezTransform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureDemoZoneInActiveScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene sc, LoadSceneMode mode)
        {
            EnsureDemoZoneInActiveScene(sc);
        }

        private static void EnsureDemoZoneInActiveScene(Scene sc)
        {
            if (sc.name != "Caldeiral") return;
            if (FindZoneById("caldeiral_demo_resync") != null) return;

            // Zona demo isolada no patio aberto Z3 (x=11.5, y=0.0, z=-14.5) — nao interfere em nenhuma rota existente
            var root = new GameObject("Resync_DemoZone_Caldeiral");
            root.transform.position = new Vector3(11.5f, 0.0f, -14.5f);
            var ctrl = root.AddComponent<ResyncZoneController>();
            ctrl.zoneId = "caldeiral_demo_resync";
            ctrl.bubbleRadius = 14.0f;
            ctrl.BuildDemoZoneGeometry();
        }

        public static ResyncZoneController FindZoneById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i] != null && ActiveZones[i].zoneId == id)
                    return ActiveZones[i];
            }
            return null;
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this)) ActiveZones.Add(this);
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        private void Awake()
        {
            EnsureRingWave();
            if (nodeAnchor == null) nodeAnchor = transform;
        }

        private void Start()
        {
            ApplyAllObjectsImmediate(currentState);
        }

        private void Update()
        {
            if (Time.timeScale <= 0.001f) return;

            // Input do jogador: tecla Y (ou Gamepad Y = JoystickButton3) dentro do alcance do No
            if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.JoystickButton3))
            {
                if (IsPlayerInNodeRange())
                    TryTriggerResync();
            }
        }

        private bool IsPlayerInNodeRange()
        {
            if (_dezTransform == null)
            {
                var dez = GameObject.Find("Player_Dez_Placeholder");
                if (dez != null) _dezTransform = dez.transform;
            }
            if (_dezTransform == null || nodeAnchor == null) return true;
            return Vector3.Distance(_dezTransform.position, nodeAnchor.position) <= nodeInteractRange;
        }

        public bool TryTriggerResync()
        {
            if (!CanTriggerNow) return false;
            StartCoroutine(RunResyncTransitionRoutine());
            return true;
        }

        /// <summary>Permite restaurar estado salvo instantaneamente ao carregar Save JSON.</summary>
        public void ApplySavedStateImmediate(ResyncState savedState)
        {
            StopAllCoroutines();
            IsTransitioning = false;
            IsGhostPreviewActive = false;
            if (_ringWave != null) _ringWave.enabled = false;
            currentState = savedState;
            ApplyAllObjectsImmediate(currentState);
        }

        /// <summary>Helper para testes unitarios/smoke resetarem cooldown entre ciclos controlados.</summary>
        public void ForceClearCooldownForTest()
        {
            _cooldownUntil = -99f;
        }

        private IEnumerator RunResyncTransitionRoutine()
        {
            IsTransitioning = true;
            IsGhostPreviewActive = true;
            _cooldownUntil = Time.time + CooldownSec;

            ResyncState nextState = (currentState == ResyncState.Desincronizado)
                ? ResyncState.Restaurado
                : ResyncState.Desincronizado;

            // 1. Atordoa todos os inimigos dentro do raio da bolha por 1,0s (GDD 04 §1)
            LastPulseStunnedEnemies = StunEnemiesInBubble();

            // 2. Inicia escrita fantasma (wireframe 0,8s) em TODOS os objetos gerenciados
            for (int i = 0; i < managedObjects.Count; i++)
            {
                if (managedObjects[i] != null)
                    managedObjects[i].BeginGhostPreview(nextState);
            }

            // 3. Onda visual em anel expandindo por 0,8s exatos
            EnsureRingWave();
            if (_ringWave != null)
            {
                Color ringCol = nextState == ResyncState.Restaurado
                    ? new Color(0.20f, 0.95f, 1.0f, 1f)
                    : new Color(1.0f, 0.55f, 0.18f, 1f);
                _ringWave.startColor = ringCol;
                _ringWave.endColor = ringCol;
                if (_ringWave.sharedMaterial != null)
                {
                    if (_ringWave.sharedMaterial.HasProperty("_BaseColor")) _ringWave.sharedMaterial.SetColor("_BaseColor", ringCol);
                    if (_ringWave.sharedMaterial.HasProperty("_Color")) _ringWave.sharedMaterial.SetColor("_Color", ringCol);
                }
                _ringWave.enabled = true;
            }

            float elapsed = 0f;
            while (elapsed < TransitionDurationSec)
            {
                float u = Mathf.Clamp01(elapsed / TransitionDurationSec);
                UpdateRingRadius(Mathf.Lerp(0.45f, bubbleRadius, u));
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (_ringWave != null) _ringWave.enabled = false;

            // 4. Comita a troca real de estado (material + colisao, geometria identica)
            currentState = nextState;
            IsGhostPreviewActive = false;
            IsTransitioning = false;
            ToggleCount++;
            ApplyAllObjectsImmediate(currentState);
            OnZoneStateChanged?.Invoke(this, currentState);
        }

        private int StunEnemiesInBubble()
        {
            Vector3 center = nodeAnchor != null ? nodeAnchor.position : transform.position;
            var enemies = FindObjectsByType<ManifestationEnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                if (Vector3.Distance(e.transform.position, center) <= bubbleRadius)
                {
                    e.ApplyParryStagger(EnemyStunDurationSec);
                    count++;
                }
            }
            return count;
        }

        private void ApplyAllObjectsImmediate(ResyncState state)
        {
            for (int i = 0; i < managedObjects.Count; i++)
            {
                if (managedObjects[i] != null)
                    managedObjects[i].ApplyStateImmediate(state);
            }
        }

        private void EnsureRingWave()
        {
            if (_ringWave != null) return;
            var go = new GameObject("Resync_RingWave");
            go.transform.SetParent(transform, false);
            _ringWave = go.AddComponent<LineRenderer>();
            _ringWave.useWorldSpace = false;
            _ringWave.loop = true;
            _ringWave.widthMultiplier = 0.14f;
            _ringWave.positionCount = 48;
            _ringWave.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringWave.receiveShadows = false;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _ringWave.sharedMaterial = new Material(unlit);
            _ringWave.enabled = false;
        }

        private void UpdateRingRadius(float r)
        {
            if (_ringWave == null) return;
            int n = _ringWave.positionCount;
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n) * Mathf.PI * 2f;
                _ringWave.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0.35f, Mathf.Sin(ang) * r));
            }
        }

        /// <summary>
        /// Monta a zona de demonstracao (GATE 3):
        ///   - No de Re-Sincronia (cristal de catraca — reutiliza c5_prop_no_resincronia se presente na cena/prefab)
        ///   - Uso 1: Feixe de luz atravessavel SO no Restaurado (ResyncDemo_LightBeamBarrier)
        ///   - Uso 2: Plataforma que SO existe/e solida no Desincronizado (ResyncDemo_DesyncPlatform)
        /// </summary>
        public void BuildDemoZoneGeometry()
        {
            managedObjects.Clear();

            // No de Re-Sincronia visual (cristal/catraca)
            var nodeGo = new GameObject("ResyncDemo_Node");
            nodeGo.transform.SetParent(transform, false);
            nodeGo.transform.localPosition = Vector3.zero;
            nodeAnchor = nodeGo.transform;

            var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ped.name = "NodePedestal";
            ped.transform.SetParent(nodeGo.transform, false);
            ped.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            ped.transform.localScale = new Vector3(0.75f, 0.55f, 0.75f);
            var pedMr = ped.GetComponent<MeshRenderer>();
            if (pedMr != null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var m = new Material(lit);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.18f, 0.75f, 0.92f, 1f));
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.25f, 0.95f, 1.15f, 1f)); }
                pedMr.sharedMaterial = m;
            }

            // Uso 1: Feixe de luz (bloqueia em Desincronizado, atravessavel em Restaurado — geometria identica!)
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "ResyncDemo_LightBeamBarrier";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(2.8f, 1.5f, 0f);
            beam.transform.localScale = new Vector3(0.45f, 3.0f, 3.2f);
            var beamDual = beam.AddComponent<ResyncDualStateObject>();
            beamDual.mode = DualStateMode.PassableOnlyInRestored;
            managedObjects.Add(beamDual);

            // Uso 2: Plataforma temporal (solida APENAS em Desincronizado, fantasma em Restaurado — geometria identica!)
            var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plat.name = "ResyncDemo_DesyncPlatform";
            plat.transform.SetParent(transform, false);
            plat.transform.localPosition = new Vector3(-2.8f, 1.1f, 0f);
            plat.transform.localScale = new Vector3(2.6f, 0.35f, 2.6f);
            var platDual = plat.AddComponent<ResyncDualStateObject>();
            platDual.mode = DualStateMode.SolidOnlyInDesync;
            managedObjects.Add(platDual);

            ApplyAllObjectsImmediate(currentState);
        }
    }
}
