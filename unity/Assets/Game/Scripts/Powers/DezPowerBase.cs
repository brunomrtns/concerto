using UnityEngine;

namespace Studio.Game.Powers
{
    /// <summary>
    /// Base extensivel para os poderes temporais do Dez.
    /// Para adicionar um 3o poder no futuro, basta herdar desta classe e registrar em DezPowersController.
    /// </summary>
    public abstract class DezPowerBase
    {
        public abstract string PowerId { get; }
        public abstract string DisplayName { get; }
        public abstract KeyCode PrimaryKey { get; }
        public abstract float CooldownDuration { get; }

        public float CooldownRemaining { get; protected set; }
        public bool IsReady => CooldownRemaining <= 0.001f;
        public float CooldownFill01 => CooldownDuration > 0.01f ? Mathf.Clamp01(1f - (CooldownRemaining / CooldownDuration)) : 1f;

        public virtual void TickCooldown(float dt)
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
        }

        public bool TryActivate(DezPowersController owner)
        {
            if (!IsReady || owner == null) return false;
            if (!OnActivate(owner)) return false;
            CooldownRemaining = CooldownDuration;
            return true;
        }

        protected abstract bool OnActivate(DezPowersController owner);
    }
}
