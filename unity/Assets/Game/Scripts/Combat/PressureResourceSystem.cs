using System;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// GATE 1 (Combate GDD 04 §2): Recurso PRESSAO — 100 pts, recarga passiva 25/s, +30 por golpe melee.
    /// Sustenta o loop "bater para poder dashar/atirar Faisca" (custo Faisca = 20 Pressao).
    /// </summary>
    public sealed class PressureResourceSystem : MonoBehaviour
    {
        public const float MaxPressure = 100.0f;
        public const float PassiveRegenPerSec = 25.0f;
        public const float MeleeHitPressureGain = 30.0f;
        public const float FaiscaPressureCost = 20.0f;

        public static PressureResourceSystem Instance { get; private set; }

        public float CurrentPressure { get; private set; } = MaxPressure;
        public int MeleeRegenTriggerCount { get; private set; }

        public event Action<float> OnPressureChanged;

        private void Awake()
        {
            Instance = this;
            CurrentPressure = MaxPressure;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            TickRegen(Time.deltaTime);
        }

        public void TickRegen(float dt)
        {
            if (dt <= 0f || CurrentPressure >= MaxPressure) return;
            CurrentPressure = Mathf.Min(MaxPressure, CurrentPressure + PassiveRegenPerSec * dt);
            OnPressureChanged?.Invoke(CurrentPressure);
        }

        public bool CanAfford(float cost) => CurrentPressure >= cost - 0.001f;

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost)) return false;
            CurrentPressure = Mathf.Max(0f, CurrentPressure - cost);
            OnPressureChanged?.Invoke(CurrentPressure);
            return true;
        }

        public void OnMeleeHitConnected()
        {
            CurrentPressure = Mathf.Min(MaxPressure, CurrentPressure + MeleeHitPressureGain);
            MeleeRegenTriggerCount++;
            OnPressureChanged?.Invoke(CurrentPressure);
        }

        public void SetPressureForTest(float value)
        {
            CurrentPressure = Mathf.Clamp(value, 0f, MaxPressure);
            OnPressureChanged?.Invoke(CurrentPressure);
        }
    }
}
