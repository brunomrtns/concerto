namespace Studio.Game.Combat
{
    /// <summary>GATE 2: Ferrugem-Espelho — carrega Faisca 1,2s; contra-jogo: Contratempo reflete a Faisca e atordoa.</summary>
    public sealed class FerrugemEspelhoEnemy : GddCanonEnemyBase
    {
        public override string CanonTypeId => "ferrugem_espelho";
        public override float TelegraphDurationSec => 1.2f;

        public bool IsChargingFaisca => IsTelegraphing;
        public bool WasHitByReflectedFaisca { get; private set; }

        public bool ApplyContratempoReflectedFaisca(float reflectedDamage = 3.0f)
        {
            if (!IsAlive) return false;
            WasHitByReflectedFaisca = true;
            ApplyStun();
            TakeRawDamage(reflectedDamage);
            return true;
        }
    }
}
