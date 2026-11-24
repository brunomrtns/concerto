using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Inimigo 1 — "Estilhaco" (#94):
    /// Rusher melee, rapido, fraco (morre em 2 golpes), ataque com telegraph claro (brilho antes de avancar).
    /// </summary>
    public sealed class EstilhacoEnemy : ManifestationEnemyBase
    {
        public override float MaxHp => Cfg.estilhacoMaxHp;
        public override float CellDropChance => Cfg.estilhacoCellDropChance;
        protected override float GetStaggerDuration() => Cfg.estilhacoStaggerSec;

        private Vector3 _lungeDir;
        private bool _lungeDealtDamage;
        private float _patrolAngle;

        protected override void TickFsm(float dt)
        {
            if (PlayerTarget == null) return;

            Vector3 toPlayer = PlayerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            switch (State)
            {
                case EnemyFsmState.IdlePatrol:
                    _patrolAngle += dt * 1.1f;
                    Vector3 patrolTarget = SpawnOrigin + new Vector3(Mathf.Cos(_patrolAngle) * 1.6f, 0f, Mathf.Sin(_patrolAngle) * 1.6f);
                    MoveHorizToward(patrolTarget, Cfg.estilhacoPatrolSpeed, dt);
                    if (dist <= Cfg.estilhacoAggroRange)
                        State = EnemyFsmState.Chase;
                    break;

                case EnemyFsmState.Chase:
                    if (dist > Cfg.estilhacoAggroRange * 1.4f)
                    {
                        State = EnemyFsmState.IdlePatrol;
                        break;
                    }
                    if (dist <= Cfg.estilhacoAttackRange && AttackCooldownTimer <= 0f && CombatGroupCoordinator.TryAcquireAttackToken(this))
                    {
                        State = EnemyFsmState.Telegraph;
                        StateTimer = Cfg.estilhacoTelegraphSec;
                        _lungeDir = dist > 0.01f ? toPlayer.normalized : transform.forward;
                        transform.rotation = Quaternion.LookRotation(_lungeDir, Vector3.up);
                        SetVisualTint(new Color(1.0f, 0.32f, 0.20f, 1f)); // Brilho de telegraph claro!
                        PlayEnemySfx(telegraphClip, 0.78f, 1.12f);
                        break;
                    }
                    // Flanqueio leve (aproximacao angular +/- 28 deg + steering anti-aglomeracao)
                    Vector3 sep = CombatGroupCoordinator.ComputeSeparationSteering(this, 2.0f);
                    float flankSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;
                    Vector3 flankDir = Quaternion.Euler(0f, flankSign * (dist > 3.2f ? 28f : 10f), 0f) * (dist > 0.05f ? toPlayer.normalized : transform.forward);
                    Vector3 desired = (flankDir + sep * 0.85f).normalized;
                    MoveHorizToward(transform.position + desired * 2.0f, Cfg.estilhacoMoveSpeed, dt);
                    break;

                case EnemyFsmState.Telegraph:
                    StateTimer -= dt;
                    // Pulsacao visual de aviso durante telegraph
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 32f);
                    SetVisualTint(Color.Lerp(new Color(1f, 0.25f, 0.15f, 1f), Color.white, pulse));
                    if (StateTimer <= 0f)
                    {
                        RestoreVisualTint();
                        State = EnemyFsmState.Attack;
                        StateTimer = Cfg.estilhacoLungeDurationSec;
                        _lungeDealtDamage = false;
                    }
                    break;

                case EnemyFsmState.Attack:
                    StateTimer -= dt;
                    transform.position += _lungeDir * (Cfg.estilhacoLungeSpeed * dt);
                    if (!_lungeDealtDamage && dist <= 1.55f)
                    {
                        _lungeDealtDamage = true;
                        if (ParryAndLockOnController.Instance != null && ParryAndLockOnController.Instance.TryInterceptMeleeAttack(this))
                        {
                            CombatGroupCoordinator.ReleaseAttackToken(this);
                            return;
                        }
                        if (PlayerHealthController.Instance != null)
                            PlayerHealthController.Instance.TryTakeDamage(Cfg.estilhacoDamageSegments, transform.position);
                    }
                    if (StateTimer <= 0f)
                    {
                        CombatGroupCoordinator.ReleaseAttackToken(this);
                        AttackCooldownTimer = Cfg.estilhacoAttackCooldownSec;
                        State = EnemyFsmState.Chase;
                    }
                    break;

                case EnemyFsmState.Stagger:
                    StateTimer -= dt;
                    if (StateTimer <= 0f)
                    {
                        RestoreVisualTint();
                        State = EnemyFsmState.Chase;
                    }
                    break;
            }
        }

        private void MoveHorizToward(Vector3 target, float speed, float dt)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.02f) return;
            Vector3 dir = d.normalized;
            transform.position += dir * (speed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 10f * dt);
        }
    }
}
