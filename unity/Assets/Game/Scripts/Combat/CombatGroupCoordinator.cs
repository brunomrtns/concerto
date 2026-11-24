using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Coordenador de grupo da IA de Combate v2 (#109):
    ///   - Sistema de Tokens de Ataque (ref.: Batman Arkham / Doom Eternal / God of War):
    ///     maximo 2 inimigos atacando simultaneamente; demais circulam mantendo pressao legivel.
    ///   - Steering anti-aglomeracao: evita pile-up em grupos de 4+ inimigos.
    /// </summary>
    public static class CombatGroupCoordinator
    {
        public static int MaxConcurrentAttackers { get; set; } = 2;

        private static readonly List<ManifestationEnemyBase> Registered = new List<ManifestationEnemyBase>(16);
        private static readonly HashSet<ManifestationEnemyBase> ActiveTokens = new HashSet<ManifestationEnemyBase>();

        public static int ActiveEnemyCount => Registered.Count;
        public static int ActiveAttackTokensCount => ActiveTokens.Count;

        public static void Register(ManifestationEnemyBase enemy)
        {
            if (enemy != null && !Registered.Contains(enemy))
                Registered.Add(enemy);
        }

        public static void Unregister(ManifestationEnemyBase enemy)
        {
            if (enemy == null) return;
            Registered.Remove(enemy);
            ActiveTokens.Remove(enemy);
        }

        public static bool TryAcquireAttackToken(ManifestationEnemyBase enemy)
        {
            if (enemy == null || !enemy.IsAlive) return false;
            PruneDeadTokens();
            if (ActiveTokens.Contains(enemy)) return true;
            if (ActiveTokens.Count >= MaxConcurrentAttackers) return false;
            ActiveTokens.Add(enemy);
            return true;
        }

        public static void ReleaseAttackToken(ManifestationEnemyBase enemy)
        {
            if (enemy != null) ActiveTokens.Remove(enemy);
        }

        public static Vector3 ComputeSeparationSteering(ManifestationEnemyBase self, float desiredSpacing = 2.1f)
        {
            if (self == null) return Vector3.zero;
            Vector3 push = Vector3.zero;
            Vector3 myPos = self.transform.position;
            float sq = desiredSpacing * desiredSpacing;

            for (int i = 0; i < Registered.Count; i++)
            {
                var other = Registered[i];
                if (other == null || other == self || !other.IsAlive) continue;
                Vector3 diff = myPos - other.transform.position;
                diff.y = 0f;
                float d2 = diff.sqrMagnitude;
                if (d2 > 0.0001f && d2 < sq)
                {
                    float d = Mathf.Sqrt(d2);
                    push += (diff / d) * ((desiredSpacing - d) / desiredSpacing);
                }
            }
            return push;
        }

        public static ManifestationEnemyBase FindBestLockTarget(Transform player, ManifestationEnemyBase exclude = null, float maxRange = 18f)
        {
            if (player == null) return null;
            PruneDeadTokens();
            ManifestationEnemyBase best = null;
            float bestScore = float.MaxValue;
            Vector3 pPos = player.position;
            Vector3 pFwd = player.forward;
            pFwd.y = 0f;
            if (pFwd.sqrMagnitude > 0.001f) pFwd.Normalize();

            for (int i = 0; i < Registered.Count; i++)
            {
                var e = Registered[i];
                if (e == null || !e.IsAlive || e == exclude) continue;
                Vector3 toE = e.transform.position - pPos;
                toE.y = 0f;
                float dist = toE.magnitude;
                if (dist > maxRange || dist < 0.05f) continue;
                float dot = Vector3.Dot(pFwd, toE / dist);
                // Prefere inimigos a frente e mais proximos
                float score = dist * (1.45f - Mathf.Clamp01((dot + 1f) * 0.5f));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        private static void PruneDeadTokens()
        {
            ActiveTokens.RemoveWhere(e => e == null || !e.IsAlive);
            for (int i = Registered.Count - 1; i >= 0; i--)
                if (Registered[i] == null || !Registered[i].IsAlive)
                    Registered.RemoveAt(i);
        }
    }
}
