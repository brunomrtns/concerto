namespace Studio.Game.Combat
{
    /// <summary>GATE 2: Censor-Pesado — imune a Faisca; contra-jogo: Martelada Aerea na nuca quebra armadura e atordoa.</summary>
    public sealed class CensorPesadoEnemy : GddCanonEnemyBase
    {
        public override string CanonTypeId => "censor_pesado";
        public override float TelegraphDurationSec => 1.1f;

        public bool IsHeavyArmorIntact { get; private set; } = true;
        public int BlockedFaiscaShotsCount { get; private set; }

        public override bool ApplyFaiscaHit(float dmg)
        {
            // Regra canonica GDD 04 §2: Censor-Pesado e IMUNE a Faisca!
            BlockedFaiscaShotsCount++;
            return false;
        }

        public bool ApplyMarteladaAereaNapeHit(float damage = 3.0f)
        {
            if (!IsAlive) return false;
            IsHeavyArmorIntact = false;
            ApplyStun();
            TakeRawDamage(damage);
            return true;
        }
    }
}
