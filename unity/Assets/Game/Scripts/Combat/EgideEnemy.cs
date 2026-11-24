using Studio.Game.Tool;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Inimigo Novo (#109) — "Egide" (TripoSG):
    ///   - Constructo blindado frontal: golpes frontais ricocheteiam (dano 0 + som metalico + feedback claro)
    ///   - Quebra por ABSORVER (parry) no ataque dele (guarda quebrada durante stagger) OU por flanqueio pelas costas!
    ///   - Prova viva do teach -> test -> twist (DESIGN_BIBLE Secao 5).
    /// </summary>
    public sealed class EgideEnemy : ManifestationEnemyBase
    {
        public float egideMaxHp = 65f;
        public float egideMoveSpeed = 3.4f;
        public float egideTelegraphSec = 0.62f;
        public float egideStrikeCooldownSec = 1.85f;
        public AudioClip ricochetClangClip;

        public bool IsGuardBroken => State == EnemyFsmState.Stagger;
        public int FrontRicochetCount { get; private set; }
        public int FlankHitsReceived { get; private set; }
        public int ParryBreakHitsReceived { get; private set; }

        public override float MaxHp => egideMaxHp;
        public override float CellDropChance => 0.75f;
        protected override float GetStaggerDuration() => 0.45f;

        private Vector3 _strikeDir;
        private bool _strikeDealt;

        public override void ApplyDamage(ToolHitInfo hit)
        {
            if (!IsAlive) return;

            // Verifica se o golpe veio pela FRENTE com a guarda ativa (nao quebrada por parry)
            Vector3 incomingFrom = -hit.KnockbackDir;
            incomingFrom.y = 0f;
            float frontDot = incomingFrom.sqrMagnitude > 0.001f
                ? Vector3.Dot(transform.forward, incomingFrom.normalized)
                : 1f;

            if (!IsGuardBroken && frontDot > 0.25f)
            {
                FrontRicochetCount++;
                SetVisualTint(new Color(0.55f, 0.85f, 1.0f, 1f));
                PlayEnemySfx(ricochetClangClip != null ? ricochetClangClip : telegraphClip, 0.88f, 1.35f);
                Debug.Log("[Egide] Golpe frontal RICOCHETEOU no escudo! Use ABSORVER [F] no ataque dele ou flanqueie pelas costas.");
                return;
            }

            if (IsGuardBroken)
            {
                ParryBreakHitsReceived++;
                hit.Damage *= 1.35f;
            }
            else
            {
                FlankHitsReceived++;
                hit.Damage *= 1.25f;
                Debug.Log("[Egide] Flanqueio pelas costas acertou o nucleo vulneravel!");
            }

            base.ApplyDamage(hit);
        }

        protected override void TickFsm(float dt)
        {
            if (PlayerTarget == null) return;
            Vector3 toPlayer = PlayerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            switch (State)
            {
                case EnemyFsmState.IdlePatrol:
                    if (dist <= 15f) State = EnemyFsmState.Chase;
                    break;

                case EnemyFsmState.Chase:
                    Vector3 sep = CombatGroupCoordinator.ComputeSeparationSteering(this, 2.3f);
                    if (dist > 2.3f)
                    {
                        Vector3 move = (toPlayer.normalized + sep * 0.7f).normalized;
                        transform.position += move * (egideMoveSpeed * dt);
                    }
                    if (dist > 0.05f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 6f * dt);

                    if (dist <= 2.6f && AttackCooldownTimer <= 0f && CombatGroupCoordinator.TryAcquireAttackToken(this))
                    {
                        State = EnemyFsmState.Telegraph;
                        StateTimer = egideTelegraphSec;
                        _strikeDir = dist > 0.01f ? toPlayer.normalized : transform.forward;
                        SetVisualTint(new Color(1.0f, 0.48f, 0.16f, 1f));
                        PlayEnemySfx(telegraphClip, 0.84f, 0.88f);
                    }
                    break;

                case EnemyFsmState.Telegraph:
                    StateTimer -= dt;
                    if (StateTimer <= 0f)
                    {
                        RestoreVisualTint();
                        State = EnemyFsmState.Attack;
                        StateTimer = 0.28f;
                        _strikeDealt = false;
                    }
                    break;

                case EnemyFsmState.Attack:
                    StateTimer -= dt;
                    transform.position += _strikeDir * (7.2f * dt);
                    if (!_strikeDealt && dist <= 2.0f)
                    {
                        _strikeDealt = true;
                        if (ParryAndLockOnController.Instance != null && ParryAndLockOnController.Instance.TryInterceptMeleeAttack(this))
                        {
                            CombatGroupCoordinator.ReleaseAttackToken(this);
                            return;
                        }
                        if (PlayerHealthController.Instance != null)
                            PlayerHealthController.Instance.TryTakeDamage(1, transform.position);
                    }
                    if (StateTimer <= 0f)
                    {
                        CombatGroupCoordinator.ReleaseAttackToken(this);
                        AttackCooldownTimer = egideStrikeCooldownSec;
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
    }
}
