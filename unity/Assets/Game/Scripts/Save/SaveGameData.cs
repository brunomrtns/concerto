using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Save
{
    [Serializable]
    public struct SaveVec3
    {
        public float x;
        public float y;
        public float z;

        public SaveVec3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    /// <summary>
    /// Schema versionado do Save real de CONCERTO (salvo como JSON em Application.persistentDataPath).
    /// Para adicionar campos novos no futuro sem quebrar saves antigos:
    ///   1. Adicione o novo campo com valor default seguro;
    ///   2. Incremente CurrentSchemaVersion se houver mudanca estrutural;
    ///   3. Trate a migracao em SaveGameData.MigrateIfNeeded().
    /// </summary>
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 3;

        [Header("Metadados & Integridade (GATE 2 & GATE 4)")]
        public int schemaVersion = CurrentSchemaVersion;
        public string savedAtUtc = string.Empty;
        public string payloadChecksum = string.Empty;

        [Header("Cena & Posicao do Jogador")]
        public string sceneName = "Caldeiral";
        public SaveVec3 playerPosition;
        public float playerYaw;

        [Header("Checkpoint Ativo (Respawn de Knockdown)")]
        public string currentCheckpointId = "cp_oficina_inicio";
        public SaveVec3 checkpointPosition = new SaveVec3(new Vector3(-28.5f, 0.05f, -18.5f));
        public float checkpointYaw = 45f;

        [Header("Objetivo & Progresso Ato 1")]
        public string currentObjective = string.Empty;
        public int partsCollectedMask;
        public bool canCollectParts;
        public bool act1Completed;

        [Header("Flags de Cutscenes Assistidas & Portas")]
        public bool cs01Played;
        public bool cs02Played;
        public bool cs03Played;
        public bool cs04Played;
        public bool sectorDoorUnlocked;
        public int signalQuestStep;
        public bool eastGateUnlocked;

        [Header("Ato 2 & Ato 3 — Anomalias, Reparo & Nucleo")]
        public bool act2AnomaliesActive;
        public List<string> resolvedAnomalyIds = new List<string>();
        public bool act2Completed;
        public bool act3Completed;

        [Header("Dialogos Concluidos (Prevencao de Reset de Missao — BL-001)")]
        public List<string> completedDialogueIds = new List<string>();

        [Header("Onboarding — Primeiros 15 Minutos")]
        public bool onboardingCompleted;
        public bool onboardingSkipped;
        public int onboardingStepIndex;
        public List<string> completedOnboardingSteps = new List<string>();

        [Header("Economia & Upgrades (Exclusivamente JSON — ZERO PlayerPrefs)")]
        public int partsCurrencyEarned;
        public int partsCurrencySpent;
        public List<string> purchasedUpgradeIds = new List<string>();

        [Header("Ferramenta Multi-Uso & Poderes")]
        public int energyCells = 3;
        public float energyBar = 100f;
        public string activeToolModeId = "mode_concertar";
        public bool powersUnlocked;
        public List<string> unlockedPowerIds = new List<string>();

        [Header("Re-Sincronia — Estados por Zona (GDD 04 §1)")]
        public List<string> resyncZoneStates = new List<string>();

        [Header("Hub A Orquestra & Linha do Equilibrio (GDD 01 §5)")]
        public List<string> restoredFusoIds = new List<string>();
        public List<string> appliedBalanceEvents = new List<string>();

        [Header("Fuso Vitralha — Progresso, Beats e Reliquia Ponteiro Maior")]
        public int vitralhaBeatIndex;
        public bool vitralhaBossDefeated;
        public bool relicPonteiroMaiorUnlocked;
        public bool vitralhaRiverRestored;

        [Header("Fuso Maresia — Progresso, Beats, Desvio Alagado e Reliquia Pendulo")]
        public int maresiaBeatIndex;
        public bool maresiaFloodDetourCleared;
        public bool maresiaBossDerusted;
        public bool relicPenduloUnlocked;
        public bool maresiaSeaThawed;

        [Header("Fuso Ecodia — Loop 6s, Eco Dez, Reliquia Ponteiro Menor (Micra) & Cidade Acordada")]
        public int ecodiaBeatIndex;
        public bool ecodiaBossDefeated;
        public bool relicPonteiroMenorUnlocked;
        public bool ecodiaCityAwake;
        public int ecodiaMemoryQuestsCleared;

        public bool MigrateIfNeeded()
        {
            if (schemaVersion <= 0 || schemaVersion > CurrentSchemaVersion)
                return false;

            if (string.IsNullOrEmpty(sceneName)) sceneName = "Caldeiral";
            if (resolvedAnomalyIds == null) resolvedAnomalyIds = new List<string>();
            if (unlockedPowerIds == null) unlockedPowerIds = new List<string>();
            if (completedDialogueIds == null) completedDialogueIds = new List<string>();
            if (completedOnboardingSteps == null) completedOnboardingSteps = new List<string>();
            if (purchasedUpgradeIds == null) purchasedUpgradeIds = new List<string>();
            if (resyncZoneStates == null) resyncZoneStates = new List<string>();
            if (restoredFusoIds == null) restoredFusoIds = new List<string>();
            if (appliedBalanceEvents == null) appliedBalanceEvents = new List<string>();
            if (string.IsNullOrEmpty(activeToolModeId)) activeToolModeId = "mode_concertar";

            // Migracao v1 -> v2 (FIX 3): traz flags legados de PlayerPrefs para o JSON versionado
            if (schemaVersion < 2)
            {
                if (!powersUnlocked && unlockedPowerIds.Count > 0)
                    powersUnlocked = true;
                if (!powersUnlocked && PlayerPrefs.GetInt("CONCERTO_POWERS_UNLOCKED", 0) == 1)
                    powersUnlocked = true;
                if (!cs03Played && PlayerPrefs.GetInt("CONCERTO_CS03_PLAYED", 0) == 1)
                    cs03Played = true;
            }

            // Migracao v2 -> v3 (Save a Prova de Corrupcao): traz flags restantes de PlayerPrefs e prepara checksum SHA-256
            if (schemaVersion < 3)
            {
                if (!cs04Played && PlayerPrefs.GetInt("CONCERTO_CS04_PLAYED", 0) == 1)
                    cs04Played = true;
                if (!sectorDoorUnlocked && PlayerPrefs.GetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 0) == 1)
                    sectorDoorUnlocked = true;
                if (!eastGateUnlocked && PlayerPrefs.GetInt("CONCERTO_EAST_GATE_UNLOCKED", 0) == 1)
                    eastGateUnlocked = true;
                if (!act2Completed && PlayerPrefs.GetInt("CONCERTO_ACT2_COMPLETED", 0) == 1)
                    act2Completed = true;
                if (!act3Completed && PlayerPrefs.GetInt("CONCERTO_ACT3_COMPLETED", 0) == 1)
                    act3Completed = true;
                if (payloadChecksum == null)
                    payloadChecksum = string.Empty;
            }

            schemaVersion = CurrentSchemaVersion;
            return true;
        }
    }
}
