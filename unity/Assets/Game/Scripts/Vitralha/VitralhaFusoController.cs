using System;
using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Hub;
using Studio.Game.Resync;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Vitralha
{
    /// <summary>
    /// Controlador dos 7 Beats do Fuso Vitralha (GDD 03 §2.2 + 05 §4):
    ///   1. Entrada pela Estacao de Luz (plataformas de feixes duplos via Re-Sincronia)
    ///   2. Bosque dos Passaros-Vidro (plataformas vivas + Rebote)
    ///   3. Mirante do Rio Parado (vista narrativa)
    ///   4. Cachoeira de Vidro (rota de Rebote + Sopro)
    ///   5. Boss: A Estufa Viva (arena dual-state + Contratempo + Posto de Reparo <= 8s)
    ///   6. Reliquia: Ponteiro Maior -> desbloqueia Sopro Estendido (14m em 0,30s)
    ///   7. Consequencia: o rio volta (cutscene in-engine curta + evento 'alaga Maresia' no grafo)
    /// </summary>
    public sealed class VitralhaFusoController : MonoBehaviour
    {
        public static VitralhaFusoController Instance { get; private set; }

        public static readonly string[] BeatIds =
        {
            "vit_b1_estacao_luz",
            "vit_b2_bosque_passaros",
            "vit_b3_mirante_rio",
            "vit_b4_cachoeira_vidro",
            "vit_b5_boss_estufa_viva",
            "vit_b6_reliquia_ponteiro",
            "vit_b7_consequencia_rio"
        };

        [Header("Referencias da Cena Vitralha")]
        public Transform[] beatCheckpoints = new Transform[7];
        public ResyncZoneController entryLightStationZone;
        public VitralhaEstufaVivaBoss bossEstufaViva;
        public GameObject relicPonteiroMaiorObject;
        public GameObject riverFlowVisual;

        public int CurrentBeatIndex { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool RelicCollected { get; private set; }
        public bool ConsequenceCutscenePlayed { get; private set; }
        public bool MaresiaFloodEventRegistered { get; private set; }

        public event Action<int, string> OnBeatReached;

        private void Awake()
        {
            Instance = this;
            if (riverFlowVisual != null) riverFlowVisual.SetActive(false);
        }

        private void OnEnable()
        {
            if (bossEstufaViva != null)
                bossEstufaViva.OnBossDefeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            if (bossEstufaViva != null)
                bossEstufaViva.OnBossDefeated -= HandleBossDefeated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ReachBeat(int beatIndex, bool autoSave = true)
        {
            beatIndex = Mathf.Clamp(beatIndex, 0, BeatIds.Length - 1);
            if (beatIndex > CurrentBeatIndex) CurrentBeatIndex = beatIndex;
            string cpId = BeatIds[beatIndex];
            Vector3 cpPos = (beatCheckpoints != null && beatIndex < beatCheckpoints.Length && beatCheckpoints[beatIndex] != null)
                ? beatCheckpoints[beatIndex].position
                : transform.position;

            OnBeatReached?.Invoke(beatIndex, cpId);
            if (autoSave)
                SaveSystem.Save(cpId, cpPos, 0f);
        }

        private void HandleBossDefeated()
        {
            BossDefeated = true;
            ReachBeat(4, autoSave: true);
            if (relicPonteiroMaiorObject != null)
                relicPonteiroMaiorObject.SetActive(true);
        }

        /// <summary>GATE 5: Coleta Reliquia Ponteiro Maior -> desbloqueia Sopro Estendido (14m em 0,30s).</summary>
        public bool CollectRelicPonteiroMaior(DezLocomotionController dez = null)
        {
            if (!BossDefeated) return false;
            RelicCollected = true;
            if (relicPonteiroMaiorObject != null) relicPonteiroMaiorObject.SetActive(false);

            if (dez == null) dez = FindAnyObjectByType<DezLocomotionController>();
            if (dez != null) dez.HasRelicPonteiroMaior = true;

            ReachBeat(5, autoSave: true);
            Debug.Log("[Vitralha] Reliquia PONTEIRO MAIOR coletada! Sopro Estendido (14m / 0.30s) desbloqueado.");
            return true;
        }

        /// <summary>GATE 6: Beat 7 — cutscene in-engine curta do rio voltando + registra 'alaga Maresia' no grafo/save.</summary>
        public IEnumerator PlayConsequenceRiverCutsceneAndRegisterEvent()
        {
            if (riverFlowVisual != null) riverFlowVisual.SetActive(true);

            // Animacao curta in-engine do desague do rio (~0.6s)
            float t = 0f;
            Vector3 baseScale = riverFlowVisual != null ? riverFlowVisual.transform.localScale : Vector3.one;
            while (t < 0.6f)
            {
                if (riverFlowVisual != null)
                {
                    float u = Mathf.Clamp01(t / 0.6f);
                    riverFlowVisual.transform.localScale = new Vector3(baseScale.x, baseScale.y, Mathf.Max(0.2f, baseScale.z * u));
                }
                yield return null;
                t += Time.deltaTime;
            }
            if (riverFlowVisual != null) riverFlowVisual.transform.localScale = baseScale;

            ConsequenceCutscenePlayed = true;
            MaresiaFloodEventRegistered = true;

            if (HubOrquestraController.Instance != null)
                HubOrquestraController.Instance.ConfirmRestoreFuso("vitralha");

            ReachBeat(6, autoSave: true);
            Debug.Log("[Vitralha] Consequencia concluida: rio voltou! Evento 'evt_cost_vitralha_flood_maresia' registrado.");
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.vitralhaBeatIndex = CurrentBeatIndex;
            data.vitralhaBossDefeated = BossDefeated;
            data.relicPonteiroMaiorUnlocked = RelicCollected;
            data.vitralhaRiverRestored = ConsequenceCutscenePlayed;

            if (MaresiaFloodEventRegistered)
            {
                if (data.restoredFusoIds == null) data.restoredFusoIds = new System.Collections.Generic.List<string>();
                if (!data.restoredFusoIds.Contains("vitralha")) data.restoredFusoIds.Add("vitralha");
                if (data.appliedBalanceEvents == null) data.appliedBalanceEvents = new System.Collections.Generic.List<string>();
                if (!data.appliedBalanceEvents.Contains("evt_benefit_vitralha_river_flow"))
                    data.appliedBalanceEvents.Add("evt_benefit_vitralha_river_flow");
                if (!data.appliedBalanceEvents.Contains("evt_cost_vitralha_flood_maresia"))
                    data.appliedBalanceEvents.Add("evt_cost_vitralha_flood_maresia");
            }
        }

        public void RestoreFromSave(SaveGameData data, DezLocomotionController dez = null)
        {
            if (data == null) return;
            CurrentBeatIndex = data.vitralhaBeatIndex;
            BossDefeated = data.vitralhaBossDefeated;
            RelicCollected = data.relicPonteiroMaiorUnlocked;
            ConsequenceCutscenePlayed = data.vitralhaRiverRestored;
            MaresiaFloodEventRegistered = data.vitralhaRiverRestored
                || (data.appliedBalanceEvents != null && data.appliedBalanceEvents.Contains("evt_cost_vitralha_flood_maresia"));

            if (riverFlowVisual != null) riverFlowVisual.SetActive(ConsequenceCutscenePlayed);
            if (dez == null) dez = FindAnyObjectByType<DezLocomotionController>();
            if (dez != null && RelicCollected) dez.HasRelicPonteiroMaior = true;
        }
    }
}
