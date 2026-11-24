using System;
using System.Collections.Generic;
using Studio.Game.Repair;
using UnityEngine;

namespace Studio.Game.Combat
{
    public enum ManifestationType
    {
        Estilhaco,
        Flutuante
    }

    [Serializable]
    public sealed class EnemySpawnSlot
    {
        public ManifestationType enemyType = ManifestationType.Estilhaco;
        public Vector3 localOffset = new Vector3(4f, 0f, 0f);
    }

    /// <summary>
    /// Zona de combate config-driven (#94):
    ///   - Wave unica curta e tensa (2-4 inimigos — combate e Temperatura, nunca horda infinita)
    ///   - Na oficina (Caldeiral), ativa automaticamente apos o beat final do Ato 2 (ou via TriggerEncounter)
    ///   - Instancia exclusivamente prefabs TripoSG de Estilhaco e Flutuante
    /// </summary>
    public sealed class CombatEncounterZone : MonoBehaviour
    {
        [Header("Configuracao da Wave (Data-Driven)")]
        public string encounterId = "caldeiral_act2_first_manifestations";
        public bool requireAct2FinalBeat = true;
        public float triggerRadius = 9.0f;
        public GameObject estilhacoPrefab;
        public GameObject flutuantePrefab;
        public CombatBalanceConfig balanceConfig;
        public AudioClip telegraphClip;
        public AudioClip dissolveReverseClip;
        public List<EnemySpawnSlot> waveSlots = new List<EnemySpawnSlot>
        {
            new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3(3.5f, 0f,  1.5f) },
            new EnemySpawnSlot { enemyType = ManifestationType.Estilhaco, localOffset = new Vector3(4.5f, 0f, -1.8f) },
            new EnemySpawnSlot { enemyType = ManifestationType.Flutuante, localOffset = new Vector3(7.5f, 0f,  0.2f) }
        };

        public bool IsTriggered { get; private set; }
        public bool IsCleared { get; private set; }
        public int AliveCount => _aliveEnemies.Count;

        public event Action<CombatEncounterZone> OnEncounterCleared;

        private readonly List<ManifestationEnemyBase> _aliveEnemies = new List<ManifestationEnemyBase>(6);
        private Transform _player;

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null) _player = dez.transform;
        }

        private void Update()
        {
            if (IsTriggered || IsCleared || _player == null) return;
            if (requireAct2FinalBeat && RepairRitualController.Instance != null && !RepairRitualController.Instance.Act2Completed)
                return;

            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= triggerRadius * triggerRadius)
                TriggerEncounter();
        }

        public void TriggerEncounter()
        {
            if (IsTriggered) return;
            IsTriggered = true;
            _aliveEnemies.Clear();

            for (int i = 0; i < waveSlots.Count; i++)
            {
                var slot = waveSlots[i];
                Vector3 pos = transform.position + slot.localOffset;
                GameObject prefab = slot.enemyType == ManifestationType.Estilhaco ? estilhacoPrefab : flutuantePrefab;
                if (prefab == null)
                {
                    Debug.LogError($"[CombatEncounterZone] Prefab TripoSG ausente para {slot.enemyType}!");
                    continue;
                }

                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = $"{encounterId}_{slot.enemyType}_{i + 1}";
                var enemy = go.GetComponent<ManifestationEnemyBase>();
                if (enemy != null)
                {
                    if (balanceConfig != null) enemy.config = balanceConfig;
                    if (telegraphClip != null) enemy.telegraphClip = telegraphClip;
                    if (dissolveReverseClip != null) enemy.dissolveReverseClip = dissolveReverseClip;
                    enemy.OnEnemyDied += HandleEnemyDied;
                    _aliveEnemies.Add(enemy);
                }
            }

            Debug.Log($"[CombatEncounterZone] '{encounterId}' ativada: {_aliveEnemies.Count} manifestacoes spawnadas.");
        }

        private void HandleEnemyDied(ManifestationEnemyBase enemy)
        {
            if (enemy != null) enemy.OnEnemyDied -= HandleEnemyDied;
            _aliveEnemies.Remove(enemy);
            if (_aliveEnemies.Count == 0 && !IsCleared)
            {
                IsCleared = true;
                Debug.Log($"[CombatEncounterZone] '{encounterId}' CONCLUIDA!");
                OnEncounterCleared?.Invoke(this);
            }
        }
    }
}
