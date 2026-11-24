using System;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Dialogue
{
    /// <summary>
    /// ObjectiveSystem — Sistema de objetivo minimo da fabrica (UM objetivo ativo por vez).
    /// API publica simples e reutilizavel pela proxima task das pecas:
    ///   ObjectiveSystem.Set("Encontre as pecas da maquina (0/3)");
    ///   ObjectiveSystem.Clear();
    /// </summary>
    public sealed class ObjectiveSystem : MonoBehaviour
    {
        public static ObjectiveSystem Instance { get; private set; }
        public static string CurrentObjective { get; private set; } = string.Empty;
        public static event Action<string> OnObjectiveChanged;

        [Header("HUD (Linha Discreta)")]
        public CanvasGroup hudGroup;
        public Text prefixLabel;
        public Text objectiveLabel;

        private void Awake()
        {
            Instance = this;
            RefreshView();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Set(string text)
        {
            CurrentObjective = text ?? string.Empty;
            if (Instance != null) Instance.RefreshView();
            OnObjectiveChanged?.Invoke(CurrentObjective);
            Debug.Log($"[ObjectiveSystem] Objetivo ativo: {CurrentObjective}");
        }

        public static void Clear()
        {
            Set(string.Empty);
        }

        public void SetPrefix(string prefix)
        {
            if (prefixLabel != null && !string.IsNullOrEmpty(prefix))
                prefixLabel.text = prefix;
        }

        private void RefreshView()
        {
            bool hasObj = !string.IsNullOrEmpty(CurrentObjective);
            if (hudGroup != null) hudGroup.alpha = hasObj ? 1f : 0f;
            if (objectiveLabel != null) objectiveLabel.text = CurrentObjective;
        }
    }
}
