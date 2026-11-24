using UnityEngine;

namespace Studio.Game.Tool
{
    public struct ToolHitInfo
    {
        public float Damage;
        public Vector3 HitPoint;
        public Vector3 KnockbackDir;
        public bool IsHeavy;
        public bool IsRangedBolt;
        public GameObject Instigator;
    }

    /// <summary>
    /// Contrato limpo para qualquer alvo atingivel pela ferramenta em modo COMBATE
    /// (dummies de treino agora, inimigos Manifestacoes na task futura).
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(ToolHitInfo hit);
    }

    /// <summary>
    /// Modulo extensivel de funcao da Ferramenta Multi-Uso.
    /// Para plugar uma nova funcao futura (ex: Scanner, Gancho), herde de ToolModuleBase e registre no MultiToolController!
    /// </summary>
    public abstract class ToolModuleBase : MonoBehaviour
    {
        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }
        public abstract Color AccentColor { get; }
        public virtual bool AllowsRepairInteraction => false;

        protected MultiToolController Controller { get; private set; }

        public virtual void Initialize(MultiToolController controller)
        {
            Controller = controller;
        }

        public virtual void OnEquip() { }
        public virtual void OnUnequip() { }
        public virtual void OnTick(float dt) { }
    }
}
