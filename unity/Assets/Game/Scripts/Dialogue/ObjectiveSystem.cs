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

        private float _flashTimer;
        private Color _baseTextColor = Color.white;

        private void Awake()
        {
            Instance = this;
            if (objectiveLabel != null) _baseTextColor = objectiveLabel.color;
            RefreshView();
        }

        private void Update()
        {
            if (_flashTimer > 0f && objectiveLabel != null)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - Time.unscaledDeltaTime * 2.2f);
                objectiveLabel.color = Color.Lerp(_baseTextColor, new Color(0.30f, 0.98f, 1.0f, 1f), _flashTimer);
                objectiveLabel.transform.localScale = Vector3.one * (1f + 0.08f * _flashTimer);
            }
        }

        public static void Flash()
        {
            if (Instance != null) Instance._flashTimer = 1f;
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
