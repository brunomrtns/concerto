using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Configuracao 100% data-driven de balance das Manifestacoes e HP do Dez (#94).
    /// Todo dano, HP, velocidade, cooldown e drop rate vive aqui (DESIGN_BIBLE Secoes 2, 3, 7 e 9).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatBalanceConfig", menuName = "Studio/Concerto/Combat Balance Config")]
    public sealed class CombatBalanceConfig : ScriptableObject
    {
        [Header("Player (Dez) — HP em Segmentos & Knockdown (TEMP ate passada de dificuldade)")]
        [Range(2, 8)] public int playerMaxHpSegments = 4;
        public float playerPostHitInvulnSec = 0.75f;
        [Range(0.08f, 0.28f)] public float damageVignetteDecaySec = 0.26f;
        public float knockdownRespawnDelaySec = 1.6f;

        [Header("Inimigo 1 — Estilhaco (Rusher Melee: rapido, fraco — morre em 2 golpes)")]
        public float estilhacoMaxHp = 32f;
        public float estilhacoMoveSpeed = 5.4f;
        public float estilhacoPatrolSpeed = 2.0f;
        public float estilhacoAggroRange = 14.0f;
        public float estilhacoAttackRange = 2.1f;
        public float estilhacoTelegraphSec = 0.48f;
        public float estilhacoLungeSpeed = 10.5f;
        public float estilhacoLungeDurationSec = 0.26f;
        public float estilhacoAttackCooldownSec = 1.45f;
        public int estilhacoDamageSegments = 1;
        public float estilhacoStaggerSec = 0.35f;
        [Range(0f, 1f)] public float estilhacoCellDropChance = 0.45f;

        [Header("Inimigo 2 — Flutuante (Ranged Sentinel: paira, mantem distancia, 1 projetil lento)")]
        public float flutuanteMaxHp = 45f;
        public float flutuanteHoverHeight = 1.60f;
        public float flutuanteMoveSpeed = 3.2f;
        public float flutuanteAggroRange = 17.0f;
        public float flutuantePreferredDistance = 7.5f;
        public float flutuanteTelegraphSec = 0.65f;
        public float flutuanteProjectileSpeed = 7.2f;
        public float flutuanteProjectileLifetimeSec = 4.0f;
        public float flutuanteAttackCooldownSec = 2.4f;
        public int flutuanteDamageSegments = 1;
        public float flutuanteStaggerSec = 0.40f;
        [Range(0f, 1f)] public float flutuanteCellDropChance = 0.65f;

        [Header("Feedback Visual & Dissolve")]
        [Range(0.05f, 0.25f)] public float hitFlashDurationSec = 0.12f;
        public float deathDissolveSec = 0.55f;
    }
}
