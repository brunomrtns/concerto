using System;
using Studio.Game.Animation;
using Studio.Game.Hub;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Maresia
{
    /// <summary>
    /// Controlador do Fuso Maresia — O Mar Parado (GDD 03 §2.3 + 05 §4):
    ///   - 7 Beats com checkpoints e save JSON.
    ///   - Beat 4 com rota alternativa Rebote+Sopro quando Maresia for primeiro (sem Garra).
    ///   - Consequencia da Vitralha: se 'evt_cost_vitralha_flood_maresia' ativo, cavernas-habitacao alagam -> quest de desvio jogavel (3 marcos).
    ///   - Boss A Baleia de Ancora (desferrujar em 3 fases, Contratempo na Fase 2, reinicio <= 8s).
    ///   - Reliquia Pendulo -> Garra (12m, 8 m/s, swing com momentum circular).
    ///   - Pos-conserto: mar degela -> navegacao de barco-drift com ondas como elevadores verticais.
    /// </summary>
    public sealed class MaresiaFusoController : MonoBehaviour
    {
        public static MaresiaFusoController Instance { get; private set; }

        public static readonly string[] BeatIds =
        {
            "mar_b1_cais_congelado",
            "mar_b2_cristas_10m",
            "mar_b3_cavernas_alagadas_desvio",
            "mar_b4_travessia_sem_garra_alt",
            "mar_b5_boss_baleia_ancora",
            "mar_b6_reliquia_pendulo",
            "mar_b7_mar_degelado_barco_drift"
        };

        [Header("Referencias")]
        public Transform[] beatCheckpoints = new Transform[7];
        public MaresiaBaleiaAncoraBoss bossBaleia;
        public GameObject relicPenduloObject;
        public GameObject floodWaterVisual;
        public Transform driftBoatRoot;
        public Transform waveElevatorPlatform;

        public int CurrentBeatIndex { get; private set; }
        public bool VitralhaFloodConsequenceActive { get; private set; }
        public int FloodDetourWaypointsCleared { get; private set; }
        public bool FloodDetourQuestCompleted => FloodDetourWaypointsCleared >= 3;
        public bool BossDerusted { get; private set; }
        public bool RelicPenduloCollected { get; private set; }
        public bool SeaThawedBoatDriftActive { get; private set; }
        public bool UsedNoGrappleAlternateRoute { get; private set; }

        private Vector3 _waveElevatorBasePos;

        private void Awake()
        {
            Instance = this;
            if (waveElevatorPlatform != null)
                _waveElevatorBasePos = waveElevatorPlatform.position;
        }

        private void OnEnable()
        {
            if (bossBaleia != null)
                bossBaleia.OnWhaleDerusted += HandleWhaleDerusted;
        }

        private void OnDisable()
        {
            if (bossBaleia != null)
                bossBaleia.OnWhaleDerusted -= HandleWhaleDerusted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Checa se a consequencia da Vitralha ('alaga Maresia') ja esta ativa no Save/Hub
            var cached = SaveSystem.CachedState;
            bool vitralhaFlooded = (cached != null && cached.appliedBalanceEvents != null &&
                                    cached.appliedBalanceEvents.Contains("evt_cost_vitralha_flood_maresia"))
                                || (HubOrquestraController.Instance != null &&
                                    HubOrquestraController.Instance.HasWorldEvent("evt_cost_vitralha_flood_maresia"));
            SetVitralhaFloodConsequence(vitralhaFlooded);
        }

        private void Update()
        {
            // GATE 6: Pos-conserto — ondas funcionam como elevadores verticais quando o mar degela
            if (SeaThawedBoatDriftActive && waveElevatorPlatform != null)
            {
                float lift = Mathf.Sin(Time.time * 1.4f) * 3.5f + 3.5f;
                waveElevatorPlatform.position = _waveElevatorBasePos + new Vector3(0f, lift, 0f);
            }
        }

        public void SetVitralhaFloodConsequence(bool active)
        {
            VitralhaFloodConsequenceActive = active;
            if (floodWaterVisual != null) floodWaterVisual.SetActive(active);
        }

        /// <summary>GATE 5: Quest de desvio jogavel quando cavernas-habitacao estao alagadas pela Vitralha.</summary>
        public bool ClearFloodDetourWaypoint(int idx)
        {
            if (!VitralhaFloodConsequenceActive) return false;
            if (idx >= FloodDetourWaypointsCleared && FloodDetourWaypointsCleared < 3)
                FloodDetourWaypointsCleared++;
            if (FloodDetourQuestCompleted)
                ReachBeat(2, autoSave: true);
            return true;
        }

        /// <summary>GATE 2: Registra travessia do Beat 4 pela rota alternativa Rebote+Sopro (quando Maresia vem primeiro).</summary>
        public void MarkNoGrappleAlternateRouteCompleted()
        {
            UsedNoGrappleAlternateRoute = true;
            ReachBeat(3, autoSave: true);
        }

        public void ReachBeat(int beatIndex, bool autoSave = true)
        {
            beatIndex = Mathf.Clamp(beatIndex, 0, BeatIds.Length - 1);
            if (beatIndex > CurrentBeatIndex) CurrentBeatIndex = beatIndex;
            string cpId = BeatIds[beatIndex];
            Vector3 cpPos = (beatCheckpoints != null && beatIndex < beatCheckpoints.Length && beatCheckpoints[beatIndex] != null)
                ? beatCheckpoints[beatIndex].position
                : transform.position;
            if (autoSave)
                SaveSystem.Save(cpId, cpPos, 0f);
        }

        private void HandleWhaleDerusted()
        {
            BossDerusted = true;
            ReachBeat(4, autoSave: true);
            if (relicPenduloObject != null) relicPenduloObject.SetActive(true);
        }

        /// <summary>GATE 4: Coleta Reliquia Pendulo -> libera Garra (12m, 8m/s, swing com momentum circular).</summary>
        public bool CollectRelicPendulo(DezLocomotionController dez = null)
        {
            if (!BossDerusted) return false;
            RelicPenduloCollected = true;
            if (relicPenduloObject != null) relicPenduloObject.SetActive(false);
            if (dez == null) dez = FindAnyObjectByType<DezLocomotionController>();
            if (dez != null) dez.HasRelicPendulo = true;
            ReachBeat(5, autoSave: true);
            return true;
        }

        /// <summary>GATE 6: Pos-conserto — mar degela, barco-drift navega entre pontos e ondas viram elevadores.</summary>
        public bool ActivateSeaThawAndBoatDrift(Vector3 driftTargetPos)
        {
            if (!RelicPenduloCollected) return false;
            SeaThawedBoatDriftActive = true;
            if (driftBoatRoot != null)
                driftBoatRoot.position = Vector3.MoveTowards(driftBoatRoot.position, driftTargetPos, 18f);
            if (HubOrquestraController.Instance != null)
                HubOrquestraController.Instance.ConfirmRestoreFuso("maresia");
            ReachBeat(6, autoSave: true);
            return true;
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.maresiaBeatIndex = CurrentBeatIndex;
            data.maresiaFloodDetourCleared = FloodDetourQuestCompleted;
            data.maresiaBossDerusted = BossDerusted;
            data.relicPenduloUnlocked = RelicPenduloCollected;
            data.maresiaSeaThawed = SeaThawedBoatDriftActive;
            if (SeaThawedBoatDriftActive)
            {
                if (data.restoredFusoIds == null) data.restoredFusoIds = new System.Collections.Generic.List<string>();
                if (!data.restoredFusoIds.Contains("maresia")) data.restoredFusoIds.Add("maresia");
                if (data.appliedBalanceEvents == null) data.appliedBalanceEvents = new System.Collections.Generic.List<string>();
                if (!data.appliedBalanceEvents.Contains("evt_benefit_maresia_boat_routes")) data.appliedBalanceEvents.Add("evt_benefit_maresia_boat_routes");
                if (!data.appliedBalanceEvents.Contains("evt_cost_maresia_drift_ecodia")) data.appliedBalanceEvents.Add("evt_cost_maresia_drift_ecodia");
            }
        }

        public void RestoreFromSave(SaveGameData data, DezLocomotionController dez = null)
        {
            if (data == null) return;
            CurrentBeatIndex = data.maresiaBeatIndex;
            if (data.maresiaFloodDetourCleared) FloodDetourWaypointsCleared = 3;
            BossDerusted = data.maresiaBossDerusted;
            RelicPenduloCollected = data.relicPenduloUnlocked;
            SeaThawedBoatDriftActive = data.maresiaSeaThawed;
            if (dez == null) dez = FindAnyObjectByType<DezLocomotionController>();
            if (dez != null && RelicPenduloCollected) dez.HasRelicPendulo = true;
        }
    }
}
