using UnityEngine;

namespace Studio.Game.Tool
{
    /// <summary>
    /// Modulo 1: Modo CONCERTAR.
    /// Reusa integralmente o sistema de reparo real (Repairable / RepairRitualController) com custo ZERO de energia/celulas SEMPRE!
    /// </summary>
    public sealed class RepairToolModule : ToolModuleBase
    {
        public override string ModuleId => "mode_concertar";
        public override string DisplayName => "CONCERTAR";
        public override Color AccentColor => new Color(0.99f, 0.85f, 0.58f, 1f);
        public override bool AllowsRepairInteraction => true;
    }
}
