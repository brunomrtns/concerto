using System;
using UnityEngine;

namespace Studio.Game.Repair
{
    /// <summary>
    /// Contrato generico e extensivel para qualquer objeto reparavel em CONCERTO
    /// (Anomalias, maquinas, portas, dispositivos, NPCs mecanicos).
    /// </summary>
    public interface IRepairable
    {
        string RepairableId { get; }
        string DisplayName { get; }
        bool CanBeRepairedNow { get; }
        bool IsRepaired { get; }
        float RepairDurationSec { get; }
        Transform FocusPoint { get; }
        void OnRepairCompleted();
        event Action<IRepairable> OnRepaired;
    }
}
