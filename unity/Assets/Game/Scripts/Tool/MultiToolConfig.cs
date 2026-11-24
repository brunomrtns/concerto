using UnityEngine;

namespace Studio.Game.Tool
{
    /// <summary>
    /// Configuracao data-driven (ScriptableObject) da Ferramenta Multi-Uso do Dez (CONCERTAR / COMBATE).
    /// </summary>
    [CreateAssetMenu(fileName = "MultiToolConfig", menuName = "Studio/Concerto/Multi-Tool Config")]
    public sealed class MultiToolConfig : ScriptableObject
    {
        [Header("Troca de Modo")]
        public KeyCode toggleModeKey = KeyCode.Tab;

        [Header("Energia & Celulas (Reparo e Melee SEMPRE custam 0!)")]
        public int startingCells = 3;
        public int maxCells = 6;
        public float maxEnergyBar = 100f;
        public int boltCellCost = 1;
        public float boltEnergyCost = 25f;
        public float energyPassiveRegenPerSec = 10f;

        [Header("Modo COMBATE — Corpo-a-Corpo (Custo 0)")]
        public float lightHit1Damage = 15f;
        public float lightHit2Damage = 22f;
        public float heavyChargedDamage = 45f;
        public float chargeHoldThresholdSec = 0.45f;
        public float comboWindowSec = 0.65f;
        public float meleeCooldownSec = 0.28f;
        public float meleeRange = 1.65f;
        public float meleeRadius = 0.72f;
        public float heavyRadius = 1.05f;

        [Header("Modo COMBATE — Tiro Carregado (Bolt de Energia)")]
        public float boltDamage = 30f;
        public float boltSpeed = 18.0f;
        public float boltLifetimeSec = 2.2f;
        public float boltCooldownSec = 0.45f;

        [Header("Game Feel Obrigatorio (Acerto)")]
        [Tooltip("Hitstop em segundos no acerto (spec: 50-80ms).")]
        [Range(0.05f, 0.08f)] public float hitstopDurationSec = 0.065f;
        [Tooltip("Amplitude sutil do camera shake no acerto.")]
        public float cameraShakeAmplitude = 0.065f;
        public float cameraShakeDurationSec = 0.12f;
    }
}
