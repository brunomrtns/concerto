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
        public const int CurrentSchemaVersion = 1;

        [Header("Metadados")]
        public int schemaVersion = CurrentSchemaVersion;
        public string savedAtUtc = string.Empty;

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

        [Header("Ato 2 — Anomalias & Reparo")]
        public bool act2AnomaliesActive;
        public List<string> resolvedAnomalyIds = new List<string>();
        public bool act2Completed;

        [Header("Ferramenta Multi-Uso & Poderes")]
        public int energyCells = 3;
        public float energyBar = 100f;
        public string activeToolModeId = "mode_concertar";
        public List<string> unlockedPowerIds = new List<string>();

        public bool MigrateIfNeeded()
        {
            if (schemaVersion <= 0 || schemaVersion > CurrentSchemaVersion)
                return false;

            if (string.IsNullOrEmpty(sceneName)) sceneName = "Caldeiral";
            if (resolvedAnomalyIds == null) resolvedAnomalyIds = new List<string>();
            if (unlockedPowerIds == null) unlockedPowerIds = new List<string>();
            if (string.IsNullOrEmpty(activeToolModeId)) activeToolModeId = "mode_concertar";

            // Futuras migracoes (ex: v1 -> v2) entram aqui sem quebrar saves antigos!
            schemaVersion = CurrentSchemaVersion;
            return true;
        }
    }
}
