using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Combat
{
    [Serializable]
    public sealed class GddCombatWaveDef
    {
        public string waveName;
        public GddCanonEnemyBase[] enemies;
    }

    /// <summary>
    /// GATE 5 (Combate GDD 05 §7): Diretor de Ondas garantindo invariante de no maximo 2 tipos
    /// de inimigo simultaneos por onda, em arenas com 2-4 rotas de verticalidade.
    /// </summary>
    public sealed class GddCombatWaveDirector : MonoBehaviour
    {
        public const int MaxDistinctTypesPerWave = 2;

        public static GddCombatWaveDirector Instance { get; private set; }

        public GddCombatWaveDef[] waves = new GddCombatWaveDef[3];
        public int verticalityRouteCount = 3; // 2-4 rotas de verticalidade (GDD 05 §7)

        public int ActiveWaveIndex { get; private set; } = -1;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool ValidateAllWavesMaxTwoTypes()
        {
            if (waves == null || waves.Length == 0) return false;
            for (int w = 0; w < waves.Length; w++)
            {
                if (GetDistinctTypeCountInWave(w) > MaxDistinctTypesPerWave)
                    return false;
            }
            return true;
        }

        public int GetDistinctTypeCountInWave(int waveIdx)
        {
            if (waves == null || waveIdx < 0 || waveIdx >= waves.Length || waves[waveIdx].enemies == null)
                return 0;
            var types = new HashSet<string>();
            for (int i = 0; i < waves[waveIdx].enemies.Length; i++)
            {
                var e = waves[waveIdx].enemies[i];
                if (e != null) types.Add(e.CanonTypeId);
            }
            return types.Count;
        }

        public void ActivateWave(int waveIdx)
        {
            if (waves == null || waveIdx < 0 || waveIdx >= waves.Length) return;
            ActiveWaveIndex = waveIdx;
            for (int w = 0; w < waves.Length; w++)
            {
                bool active = (w == waveIdx);
                if (waves[w].enemies == null) continue;
                for (int i = 0; i < waves[w].enemies.Length; i++)
                {
                    if (waves[w].enemies[i] != null)
                        waves[w].enemies[i].gameObject.SetActive(active);
                }
            }
        }
    }
}
