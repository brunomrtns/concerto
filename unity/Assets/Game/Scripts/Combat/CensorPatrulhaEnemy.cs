namespace Studio.Game.Combat
{
    /// <summary>GATE 2: Censor-Patrulha — combo de 2 golpes + giro; contra-jogo: parry no 2o golpe atordoa.</summary>
    public sealed class CensorPatrulhaEnemy : GddCanonEnemyBase
    {
        public override string CanonTypeId => "censor_patrulha";
        public override float TelegraphDurationSec => 0.6f;

        public int CurrentComboStep { get; private set; } // 1 = golpe 1, 2 = golpe 2 (janela de parry!), 3 = giro
        public bool WasParriedOnSecondHit { get; private set; }

        public void AdvanceComboStep(int step)
        {
            CurrentComboStep = step;
            BeginTelegraph();
        }

        public bool ApplyParryOnSecondComboHit()
        {
            if (!IsAlive || CurrentComboStep != 2) return false;
            WasParriedOnSecondHit = true;
            CurrentComboStep = 0;
            ApplyStun();
            return true;
        }
    }
}
