using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// GATE 1 (Combate GDD 04 §2): FAISCA (ranged) — projetil de energia em arco curto 12m,
    /// recarga 0,4s, custo 20 Pressao.
    /// </summary>
    public sealed class FaiscaRangedController : MonoBehaviour
    {
        public const float ArcRangeM = 12.0f;
        public const float CooldownSec = 0.40f;
        public const float PressureCost = 20.0f;
        public const float BaseDamage = 1.5f;

        public static FaiscaRangedController Instance { get; private set; }

        public KeyCode fireKey = KeyCode.E;

        public float CooldownRemaining { get; private set; }
        public int ShotsFiredCount { get; private set; }
        public bool DoubleShotUnlocked { get; set; }
        public int ProjectilesPerShot => DoubleShotUnlocked ? 2 : 1;
        public Vector3 LastShotOrigin { get; private set; }
        public Vector3 LastShotEndPoint { get; private set; }

        private PressureResourceSystem _pressure;

        private void Awake()
        {
            Instance = this;
            _pressure = GetComponent<PressureResourceSystem>();
            if (_pressure == null) _pressure = gameObject.AddComponent<PressureResourceSystem>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);

            if (Input.GetKeyDown(fireKey))
                TryFireFaisca(transform.forward, null);
        }

        public void AdvanceCooldownForTest(float dt)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Mathf.Max(0f, dt));
        }

        public bool TryFireFaisca(Vector3 direction, GddCanonEnemyBase explicitTarget = null)
        {
            if (CooldownRemaining > 0.001f) return false;
            if (_pressure == null || !_pressure.TrySpend(PressureCost)) return false;

            Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            LastShotOrigin = transform.position + Vector3.up * 0.9f;
            LastShotEndPoint = LastShotOrigin + dir * ArcRangeM;
            CooldownRemaining = CooldownSec;
            ShotsFiredCount++;

            if (explicitTarget != null && Vector3.Distance(transform.position, explicitTarget.transform.position) <= ArcRangeM + 0.5f)
                explicitTarget.ApplyFaiscaHit(BaseDamage);

            return true;
        }
    }
}
