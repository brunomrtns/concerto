using System;
using Studio.Game.Combat;
using UnityEngine;

namespace Studio.Game.Maresia
{
    /// <summary>
    /// Ponto marcado de Garra ("olho-de-lente dourado" — GDD 04 §1 / Maresia GATE 4).
    /// </summary>
    public sealed class GrappleLensPoint : MonoBehaviour
    {
        public float snapRadius = 12.0f;
    }

    /// <summary>
    /// GATE 3: Boss "A Baleia de Ancora" (GDD 03 §2.3 + 05 §7):
    ///   - NAO e combate para matar: e DESFERRUJAR em 3 fases (3 travas de ferrugem).
    ///   - Fase 2 exige Contratempo (Parry) para destravar a ancora central.
    ///   - Posto de Reparo na arena com reinicio rapido <= 8s.
    /// </summary>
    public sealed class MaresiaBaleiaAncoraBoss : MonoBehaviour
    {
        public static MaresiaBaleiaAncoraBoss Instance { get; private set; }

        public Transform repairStationAnchor;
        public int RustLocksRemaining { get; private set; } = 3;
        public int CurrentPhase { get; private set; } = 1; // 1 = Trava 1, 2 = Trava 2 (exige Contratempo), 3 = Trava 3 final
        public bool IsParryGateActive { get; private set; }
        public bool IsDerusted => RustLocksRemaining <= 0;
        public float LastRestartDurationSec { get; private set; }

        public event Action OnWhaleDerusted;

        private MeshRenderer _bodyRenderer;

        private void Awake()
        {
            Instance = this;
            RustLocksRemaining = 3;
            CurrentPhase = 1;
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Desferruja 1 trava mecanica da Baleia de Ancora (NAO mata o leviata).</summary>
        public bool ApplyDerustAction()
        {
            if (IsDerusted) return false;
            if (IsParryGateActive)
            {
                Debug.Log("[BaleiaAncora] Trava da Fase 2 exige Contratempo (Parry) no golpe da ancora!");
                return false;
            }

            RustLocksRemaining = Mathf.Max(0, RustLocksRemaining - 1);
            if (RustLocksRemaining == 2)
            {
                CurrentPhase = 2;
                IsParryGateActive = true;
                SetTint(new Color(0.95f, 0.58f, 0.20f, 1f));
            }
            else if (RustLocksRemaining == 0)
            {
                CurrentPhase = 3;
                SetTint(new Color(0.24f, 0.88f, 0.92f, 1f));
                OnWhaleDerusted?.Invoke();
                Debug.Log("[BaleiaAncora] Baleia de Ancora 100% DESFERRUJADA! Mar pronto para degelar.");
            }
            return true;
        }

        public bool ApplyContratempoParry()
        {
            if (IsDerusted || !IsParryGateActive) return false;
            IsParryGateActive = false;
            RustLocksRemaining = Mathf.Max(1, RustLocksRemaining - 1);
            CurrentPhase = 3;
            SetTint(new Color(0.32f, 0.92f, 0.72f, 1f));
            Debug.Log("[BaleiaAncora] Contratempo acertado! Trava central solta (falta 1 trava final).");
            return true;
        }

        public void RestartEncounterAtRepairStation(GameObject player)
        {
            float t0 = Time.realtimeSinceStartup;
            RustLocksRemaining = 3;
            CurrentPhase = 1;
            IsParryGateActive = false;
            SetTint(Color.white);
            if (player != null && repairStationAnchor != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = repairStationAnchor.position + Vector3.up * 0.1f;
                if (cc != null) cc.enabled = true;
            }
            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.RestoreFullHealth();
            LastRestartDurationSec = Time.realtimeSinceStartup - t0;
        }

        private void SetTint(Color c)
        {
            if (_bodyRenderer == null) return;
            var mpb = new MaterialPropertyBlock();
            _bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            _bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}
