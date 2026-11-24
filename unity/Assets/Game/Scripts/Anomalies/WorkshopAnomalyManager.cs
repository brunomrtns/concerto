using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Anomalies
{
    [Serializable]
    public sealed class AnomalyVec3
    {
        public float x;
        public float y;
        public float z;
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public sealed class AnomalyConfigEntry
    {
        public string id;
        public string displayName;
        public string kind;
        public AnomalyVec3 position;
        public string description;
    }

    [Serializable]
    public sealed class Act2AnomaliesConfigData
    {
        public List<AnomalyConfigEntry> anomalies = new List<AnomalyConfigEntry>();

        public static Act2AnomaliesConfigData FromJson(TextAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return new Act2AnomaliesConfigData();
            return JsonUtility.FromJson<Act2AnomaliesConfigData>(asset.text) ?? new Act2AnomaliesConfigData();
        }
    }

    /// <summary>
    /// Orquestrador das 5 Anomalias Ambientais da oficina (Ato 2):
    ///   - Antes da Cutscene 3: TODAS em AnomalyState.Normal (invisiveis/inaudiveis — Gate 3!)
    ///   - Apos a Cutscene 3: ActivateAllAnomalies() liga todas as 5 + persiste flag PlayerPrefs
    ///   - API limpa para a task de Reparo futura consultar/resolver qualquer anomalia
    /// </summary>
    public sealed class WorkshopAnomalyManager : MonoBehaviour
    {
        public const string Act2AnomaliesPrefKey = "CONCERTO_ACT2_ANOMALIES_ACTIVE";

        public static WorkshopAnomalyManager Instance { get; private set; }

        [Header("Config JSON (Posicoes & Tipos)")]
        public TextAsset anomaliesConfigJson;

        [Header("Anomalias da Cena")]
        public List<Anomaly> anomalies = new List<Anomaly>();

        [Header("Zumbido Persistente da Maquina Pos-CS03")]
        public AudioSource persistentMachineHum;

        public bool AreAnomaliesActive { get; private set; }
        public int ActiveAnomalyCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < anomalies.Count; i++)
                    if (anomalies[i] != null && anomalies[i].IsAnomalous) c++;
                return c;
            }
        }

        private void Awake()
        {
            Instance = this;
            ResetAllToNormal();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (PlayerPrefs.GetInt(Act2AnomaliesPrefKey, 0) == 1)
                ActivateAllAnomalies(persistFlag: false);
        }

        public void ActivateAllAnomalies(bool persistFlag = true)
        {
            AreAnomaliesActive = true;
            for (int i = 0; i < anomalies.Count; i++)
            {
                if (anomalies[i] != null)
                    anomalies[i].ActivateAnomaly();
            }

            if (persistentMachineHum != null && !persistentMachineHum.isPlaying)
                persistentMachineHum.Play();

            if (persistFlag)
            {
                PlayerPrefs.SetInt(Act2AnomaliesPrefKey, 1);
                PlayerPrefs.Save();
            }
            Debug.Log($"[WorkshopAnomalyManager] {ActiveAnomalyCount} anomalias ambientais ATIVADAS na oficina (Ato 2).");
        }

        public void ResetAllToNormal()
        {
            AreAnomaliesActive = false;
            for (int i = 0; i < anomalies.Count; i++)
            {
                if (anomalies[i] != null)
                    anomalies[i].Resolve();
            }
            if (persistentMachineHum != null && persistentMachineHum.isPlaying)
                persistentMachineHum.Stop();
        }

        public IReadOnlyList<Anomaly> GetAllAnomalies() => anomalies;
    }
}
