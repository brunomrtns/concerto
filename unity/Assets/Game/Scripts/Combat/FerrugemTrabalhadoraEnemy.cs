namespace Studio.Game.Combat
{
    /// <summary>GATE 2: Ferrugem-Trabalhadora — telegraph 0,8s com brilho na juncao da marreta.</summary>
    public sealed class FerrugemTrabalhadoraEnemy : GddCanonEnemyBase
    {
        public override string CanonTypeId => "ferrugem_trabalhadora";
        public override float TelegraphDurationSec => 0.8f;

        public bool HammerSwingCommitted { get; private set; }

        public bool ExecuteHammerSwingAfterTelegraph()
        {
            if (!IsAlive || !IsTelegraphing) return false;
            HammerSwingCommitted = true;
            EndTelegraph();
            return true;
        }
    }
}
