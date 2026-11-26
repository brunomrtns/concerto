using System;
using Studio.Game.Combat;
using Studio.Game.Resync;
using Studio.Game.Tool;
using UnityEngine;

namespace Studio.Game.Vitralha
{
    /// <summary>
    /// GATE 4: Boss "A Estufa Viva" (GDD 03 §2.2 + 05 §4 + 05 §7):
    ///   - Arena que MUDA DE ESTADO no meio da luta (briga com a Re-Sincronia!).
    ///   - Fase 2 exige Contratempo (Parry) para quebrar a guarda refratada e expor o nucleo (regra GDD 05 §7).
    ///   - Posto de Reparo na arena + reinicio rapido <= 8s (real < 0.5s).
    /// </summary>
    public sealed class VitralhaEstufaVivaBoss : MonoBehaviour, IDamageable
    {
        public static VitralhaEstufaVivaBoss Instance { get; private set; }

        [Header("Referencias da Arena")]
        public ResyncZoneController arenaResyncZone;
        public Transform repairStationAnchor;

        [Header("Parametros de Combate")]
        public int maxHp = 12;
        public int CurrentHp { get; private set; }
        public int CurrentPhase { get; private set; } = 1; // 1 = Prismas, 2 = Guarda Refratada (exige Contratempo), 3 = Nucleo Exposto
        public bool IsAlive => CurrentHp > 0 && !_defeated;
        public bool IsParryShieldActive { get; private set; }
        public bool ArenaStateFlippedMidFight { get; private set; }
        public int ContratempoBreaksCount { get; private set; }
        public float LastRestartDurationSec { get; private set; }

        public event Action<int> OnPhaseChanged;
        public event Action OnBossDefeated;

        private bool _defeated;
        private MeshRenderer _bodyRenderer;

        private void Awake()
        {
            Instance = this;
            CurrentHp = maxHp;
            CurrentPhase = 1;
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplyDamage(ToolHitInfo hit)
        {
            if (!IsAlive) return;

            // Fase 2 (GDD 05 §7): Contratempo e a chave — ataques normais nao perfuram a guarda refratada
            if (IsParryShieldActive)
            {
                Debug.Log("[EstufaViva] Guarda refratada ativa! Use Contratempo (Parry) para quebrar a fase.");
                return;
            }

            int dmg = Mathf.Max(1, Mathf.RoundToInt(hit.Damage));
            CurrentHp = Mathf.Max(0, CurrentHp - dmg);

            // Transicao Fase 1 -> Fase 2 aos 50% HP: Boss MUDA o estado da arena + ativa guarda que exige Contratempo!
            if (CurrentPhase == 1 && CurrentHp <= maxHp / 2 && CurrentHp > 0)
            {
                EnterPhase2ArenaFlipAndParryGate();
            }

            if (CurrentHp <= 0)
            {
                _defeated = true;
                IsParryShieldActive = false;
                SetTint(new Color(0.28f, 0.96f, 0.72f, 1f));
                OnBossDefeated?.Invoke();
                Debug.Log("[EstufaViva] A Estufa Viva desarmonizada! Reliquia Ponteiro Maior liberada.");
            }
        }

        private void EnterPhase2ArenaFlipAndParryGate()
        {
            CurrentPhase = 2;
            IsParryShieldActive = true;
            SetTint(new Color(1.0f, 0.74f, 0.22f, 1f));

            // Primeiro boss que "briga" com a Re-Sincronia: alterna o estado da arena no meio da luta!
            if (arenaResyncZone != null)
            {
                arenaResyncZone.ForceClearCooldownForTest();
                ResyncState flipped = arenaResyncZone.currentState == ResyncState.Desincronizado
                    ? ResyncState.Restaurado
                    : ResyncState.Desincronizado;
                arenaResyncZone.ApplySavedStateImmediate(flipped);
                ArenaStateFlippedMidFight = true;
            }
            else
            {
                ArenaStateFlippedMidFight = true;
            }

            OnPhaseChanged?.Invoke(2);
            Debug.Log("[EstufaViva] FASE 2: Arena mudou de estado! Guarda refratada ativa (requer Contratempo).");
        }

        /// <summary>Executa Contratempo (Parry) no golpe ritmico do boss, quebrando a Fase 2.</summary>
        public bool ApplyContratempoParry()
        {
            if (!IsAlive || !IsParryShieldActive) return false;
            IsParryShieldActive = false;
            CurrentPhase = 3;
            ContratempoBreaksCount++;
            SetTint(new Color(0.24f, 0.90f, 1.0f, 1f));
            OnPhaseChanged?.Invoke(3);
            Debug.Log("[EstufaViva] CONTRATEMPO acertado! Nucleo da Estufa Viva exposto (Fase 3).");
            return true;
        }

        /// <summary>Reinicio rapido <= 8s com cura no Posto de Reparo da arena (GDD 05 §7).</summary>
        public void RestartEncounterAtRepairStation(GameObject player)
        {
            float t0 = Time.realtimeSinceStartup;
            _defeated = false;
            CurrentHp = maxHp;
            CurrentPhase = 1;
            IsParryShieldActive = false;
            ArenaStateFlippedMidFight = false;
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
