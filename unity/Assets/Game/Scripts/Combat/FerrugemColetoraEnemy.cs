using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>GATE 2: Ferrugem-Coletora — escuda aliados com bolha; contra-jogo: Crava-Mola quebra a bolha e atordoa.</summary>
    public sealed class FerrugemColetoraEnemy : GddCanonEnemyBase
    {
        public override string CanonTypeId => "ferrugem_coletora";
        public override float TelegraphDurationSec => 0.9f;

        public bool IsShieldBubbleActive { get; private set; } = true;
        public GameObject shieldBubbleVisual;

        public override bool ApplyFaiscaHit(float dmg)
        {
            if (IsShieldBubbleActive) return false; // bolha absorve tiros normais ate Crava-Mola quebrar
            return base.ApplyFaiscaHit(dmg);
        }

        public bool ApplyCravaMolaBubbleBreak()
        {
            if (!IsAlive || !IsShieldBubbleActive) return false;
            IsShieldBubbleActive = false;
            if (shieldBubbleVisual != null) shieldBubbleVisual.SetActive(false);
            ApplyStun();
            return true;
        }
    }
}
