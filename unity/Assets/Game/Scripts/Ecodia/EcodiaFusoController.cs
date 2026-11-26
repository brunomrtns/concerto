using Studio.Game.Hub;
using Studio.Game.Powers;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Ecodia
{
    public enum CensorPatrolMode
    {
        FrozenLoop6s = 0,
        RealTimePatrol = 1
    }

    /// <summary>
    /// Controlador do Fuso Ecodia — A Cidade que Repete 6 Segundos (GDD 03 §2.4):
    ///   - 7 Beats com checkpoints e Save JSON.
    ///   - Desincronizado: loop deterministico de 6s (bonde + porta no t=2s + anuncio + moradores).
    ///   - Boss Eco Dez (espelha moveset + piadas na ordem errada + Contratempo na Fase 2 + reinicio <= 8s).
    ///   - Reliquia Ponteiro Menor -> Micra (2s camera lenta, cooldown 8s; NAO substitui Estase).
    ///   - Pos-conserto: moradores acordam com 3 micro-quests de memoria (devolver o segundo perdido)
    ///     e Censores passam a patrulhar em tempo real (dificuldade sobe = custo do conserto).
    /// </summary>
    public sealed class EcodiaFusoController : MonoBehaviour
    {
        public static EcodiaFusoController Instance { get; private set; }

        public static readonly string[] BeatIds =
        {
            "eco_b1_estacao_6s",
            "eco_b2_bonde_porta_2s",
            "eco_b3_praca_anuncio_neon",
            "eco_b4_torre_espelho",
            "eco_b5_boss_eco_dez",
            "eco_b6_reliquia_ponteiro_menor",
            "eco_b7_cidade_acordada_censores"
        };

        [Header("Referencias")]
        public Transform[] beatCheckpoints = new Transform[7];
        public EcodiaTimeLoopSystem timeLoop;
        public EcodiaBossEcoDez bossEcoDez;
        public GameObject relicPonteiroMenorObject;
        public Transform[] censorTransforms = new Transform[2];

        public int CurrentBeatIndex { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool RelicPonteiroMenorCollected { get; private set; }
        public bool CityAwakePostRepair { get; private set; }
        public CensorPatrolMode CensorMode { get; private set; } = CensorPatrolMode.FrozenLoop6s;
        public float CensorAlertSpeedMultiplier => CensorMode == CensorPatrolMode.RealTimePatrol ? 1.65f : 0.60f;
        public int MemoryQuestsCompletedCount { get; private set; }
        public bool AllMemoryQuestsCompleted => MemoryQuestsCompletedCount >= 3;

        private readonly bool[] _memoryQuestDone = new bool[3];

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (bossEcoDez != null)
                bossEcoDez.OnEcoDezDefeated += HandleEcoDezDefeated;
        }

        private void OnDisable()
        {
            if (bossEcoDez != null)
                bossEcoDez.OnEcoDezDefeated -= HandleEcoDezDefeated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (censorTransforms == null) return;
            for (int i = 0; i < censorTransforms.Length; i++)
            {
                var c = censorTransforms[i];
                if (c == null) continue;
                if (CensorMode == CensorPatrolMode.RealTimePatrol)
                {
                    float offset = Mathf.Sin(Time.time * 1.8f * CensorAlertSpeedMultiplier + i * 2.1f) * 8.0f;
                    var p = c.localPosition;
                    c.localPosition = new Vector3(offset, p.y, p.z);
                }
            }
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

        private void HandleEcoDezDefeated()
        {
            BossDefeated = true;
            ReachBeat(4, autoSave: true);
            if (relicPonteiroMenorObject != null) relicPonteiroMenorObject.SetActive(true);
        }

        /// <summary>GATE 3: Coleta Reliquia Ponteiro Menor -> libera Micra (2s slow-mo, 8s CD) SEM substituir Estase.</summary>
        public bool CollectRelicPonteiroMenor(DezPowersController powers = null)
        {
            if (!BossDefeated) return false;
            RelicPonteiroMenorCollected = true;
            if (relicPonteiroMenorObject != null) relicPonteiroMenorObject.SetActive(false);
            if (powers == null) powers = FindAnyObjectByType<DezPowersController>();
            if (powers != null) powers.UnlockMicraRelic();
            ReachBeat(5, autoSave: true);
            return true;
        }

        /// <summary>GATE 2: Pos-conserto — quebra loop 6s, acorda moradores (3 micro-quests) e Censores patrulham em tempo real.</summary>
        public bool WakeCityAndActivateRealTimeCensors()
        {
            if (!RelicPonteiroMenorCollected) return false;
            CityAwakePostRepair = true;
            CensorMode = CensorPatrolMode.RealTimePatrol;
            if (timeLoop != null) timeLoop.BreakLoopAndWakeDistrict();
            if (HubOrquestraController.Instance != null)
                HubOrquestraController.Instance.ConfirmRestoreFuso("ecodia");
            ReachBeat(6, autoSave: true);
            return true;
        }

        /// <summary>GATE 2: Devolve a um morador acordado o segundo que ele perdeu (3 micro-quests de memoria).</summary>
        public bool CompleteMemoryMicroQuest(int residentIndex)
        {
            if (!CityAwakePostRepair || residentIndex < 0 || residentIndex >= _memoryQuestDone.Length)
                return false;
            if (!_memoryQuestDone[residentIndex])
            {
                _memoryQuestDone[residentIndex] = true;
                MemoryQuestsCompletedCount++;
            }
            SaveSystem.Save(BeatIds[6], transform.position, 0f);
            return true;
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.ecodiaBeatIndex = CurrentBeatIndex;
            data.ecodiaBossDefeated = BossDefeated;
            data.relicPonteiroMenorUnlocked = RelicPonteiroMenorCollected;
            data.ecodiaCityAwake = CityAwakePostRepair;
            data.ecodiaMemoryQuestsCleared = MemoryQuestsCompletedCount;
            if (CityAwakePostRepair)
            {
                if (data.restoredFusoIds == null) data.restoredFusoIds = new System.Collections.Generic.List<string>();
                if (!data.restoredFusoIds.Contains("ecodia")) data.restoredFusoIds.Add("ecodia");
                if (data.appliedBalanceEvents == null) data.appliedBalanceEvents = new System.Collections.Generic.List<string>();
                if (!data.appliedBalanceEvents.Contains("evt_benefit_ecodia_free_loop")) data.appliedBalanceEvents.Add("evt_benefit_ecodia_free_loop");
                if (!data.appliedBalanceEvents.Contains("evt_cost_ecodia_realtime_censors")) data.appliedBalanceEvents.Add("evt_cost_ecodia_realtime_censors");
            }
        }

        public void RestoreFromSave(SaveGameData data)
        {
            if (data == null) return;
            CurrentBeatIndex = Mathf.Clamp(data.ecodiaBeatIndex, 0, BeatIds.Length - 1);
            BossDefeated = data.ecodiaBossDefeated;
            RelicPonteiroMenorCollected = data.relicPonteiroMenorUnlocked;
            CityAwakePostRepair = data.ecodiaCityAwake;
            CensorMode = CityAwakePostRepair ? CensorPatrolMode.RealTimePatrol : CensorPatrolMode.FrozenLoop6s;
            MemoryQuestsCompletedCount = Mathf.Clamp(data.ecodiaMemoryQuestsCleared, 0, 3);
            for (int i = 0; i < 3; i++) _memoryQuestDone[i] = i < MemoryQuestsCompletedCount;
            if (timeLoop != null) timeLoop.RestoreLoopState(!CityAwakePostRepair);
            if (RelicPonteiroMenorCollected)
            {
                var powers = FindAnyObjectByType<DezPowersController>();
                if (powers != null) powers.UnlockMicraRelic();
            }
        }
    }
}
