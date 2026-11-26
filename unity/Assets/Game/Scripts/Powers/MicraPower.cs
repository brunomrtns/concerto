using UnityEngine;

namespace Studio.Game.Powers
{
    /// <summary>
    /// GATE 3 (Ecodia — Reliquia Ponteiro Menor -> MICRA):
    ///   - 2.0s de camera lenta (escala 0.35x no mundo), cooldown 8.0s.
    ///   - Integrada ao menu de poderes (DezPowersController) SEM substituir a Estase existente (CANON.md).
    /// </summary>
    public sealed class MicraPower : DezPowerBase
    {
        private readonly DezPowersConfig _cfg;

        public MicraPower(DezPowersConfig cfg) => _cfg = cfg;

        public override string PowerId => "micra";
        public override string DisplayName => "Micra";
        public override KeyCode PrimaryKey => _cfg != null ? _cfg.micraKey : KeyCode.T;
        public override float CooldownDuration => _cfg != null ? _cfg.micraCooldownSec : 8.0f;

        protected override bool OnActivate(DezPowersController owner)
        {
            return owner.ExecuteMicraInternal();
        }
    }
}
