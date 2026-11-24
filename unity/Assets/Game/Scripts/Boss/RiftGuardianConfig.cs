using UnityEngine;

namespace Studio.Game.Boss
{
    /// <summary>
    /// Configuracao 100% data-driven do mini-boss "Guardiao da Fenda" (#103).
    /// Todo HP, dano, telegraph, cooldown, fase 2 e duracao do flash de holograma (0.5s) vive aqui.
    /// </summary>
    [CreateAssetMenu(fileName = "RiftGuardianConfig", menuName = "Studio/Concerto/Rift Guardian Boss Config")]
    public sealed class RiftGuardianConfig : ScriptableObject
    {
        [Header("Vida & Transicao de Fases")]
        public float maxHp = 320f;
        [Range(0.25f, 0.75f)] public float phase2Threshold01 = 0.50f;
        [Tooltip("Multiplicador de dano quando o NUCLEO esta exposto apos o slam (janela de punicao).")]
        public float exposedCoreDamageMultiplier = 1.65f;
        public float armoredDamageMultiplier = 0.45f;

        [Header("Fase 1 — Investida + Slam com Onda Expansiva + Nucleo Exposto")]
        public float moveSpeed = 3.8f;
        public float chargeTelegraphSec = 0.72f;
        public float chargeSpeed = 12.0f;
        public float chargeDurationSec = 0.55f;
        public float slamTelegraphSec = 0.80f;
        public float shockwaveMaxRadius = 11.5f;
        public float shockwaveExpandSpeed = 13.5f;
        [Tooltip("Janela de stagger com nucleo exposto apos slam (~3s).")]
        public float coreExposedStaggerSec = 3.0f;
        public float phase1CooldownSec = 1.6f;
        public int attackDamageSegments = 1;

        [Header("Fase 2 (<= 50% HP) — 2 Estilhacos + Spray de Projeteis Lentos")]
        public int phase2SummonEstilhacosCount = 2;
        public int phase2SprayProjectileCount = 7;
        public float phase2SpraySpreadDeg = 84f;
        public float phase2SprayProjectileSpeed = 6.8f;
        public float phase2CooldownSec = 1.3f;

        [Header("Vitoria — Fragmento de Nucleo, Flash de Holograma (0.5s, SEM dialogo) & Cartao Fim do Ato 3")]
        [Tooltip("Duracao canonica do flash de holograma da oficina no passado + esposa (meio segundo, sem explicacao).")]
        public float hologramFlashDurationSec = 0.52f;
        public string act3EndCardTitle = "FIM DO ATO 3";
        public string act3EndCardSubtitle = "O Coração da Fenda silenciou... mas a imagem permanece.";
    }
}
