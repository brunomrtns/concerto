using Studio.Game.Collision;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Inimigo 2 — "Flutuante" (#94):
    /// Paira no ar, mantem distancia segura e dispara 1 projetil lento (desviavel com Eco / travavel com Estase).
    /// FIX 7: verifica Line-of-Sight (LayerEnvironment|LayerDefault) antes de atirar.
    /// FIX 1: material URP/Unlit cacheado estaticamente para o projetil.
    /// </summary>
    public sealed class FlutuanteEnemy : ManifestationEnemyBase
    {
        private static Material _cachedProjectileMat;
        public override float MaxHp => Cfg.flutuanteMaxHp;
        public override float CellDropChance => Cfg.flutuanteCellDropChance;
        protected override float GetStaggerDuration() => Cfg.flutuanteStaggerSec;

        private float _hoverPhase;

        protected override void TickFsm(float dt)
        {
            if (PlayerTarget == null) return;

            _hoverPhase += dt * 2.4f;
            float baseGroundY = SpawnOrigin.y;
            Vector3 pos = transform.position;
            pos.y = baseGroundY + Cfg.flutuanteHoverHeight + Mathf.Sin(_hoverPhase) * 0.16f;
            transform.position = pos;

            Vector3 toPlayer = PlayerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 0.05f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 8f * dt);

            switch (State)
            {
                case EnemyFsmState.IdlePatrol:
                    if (dist <= Cfg.flutuanteAggroRange)
                        State = EnemyFsmState.Chase;
                    break;

                case EnemyFsmState.Chase:
                    if (dist > Cfg.flutuanteAggroRange * 1.4f)
                    {
                        State = EnemyFsmState.IdlePatrol;
                        break;
                    }
                    // Mantem distancia preferida + steering anti-aglomeracao + strafe lateral apos atirar
                    Vector3 sep = CombatGroupCoordinator.ComputeSeparationSteering(this, 2.4f);
                    Vector3 radial = Vector3.zero;
                    if (dist < Cfg.flutuantePreferredDistance - 1.5f) radial = -toPlayer.normalized;
                    else if (dist > Cfg.flutuantePreferredDistance + 1.5f) radial = toPlayer.normalized;
                    Vector3 strafe = Vector3.Cross(Vector3.up, toPlayer.normalized) * (Mathf.Sin(_hoverPhase * 0.45f) * 0.65f);
                    transform.position += (radial + strafe + sep * 0.8f) * (Cfg.flutuanteMoveSpeed * dt);

                    if (AttackCooldownTimer <= 0f && dist <= Cfg.flutuanteAggroRange && HasLineOfSightToPlayer() && CombatGroupCoordinator.TryAcquireAttackToken(this))
                    {
                        State = EnemyFsmState.Telegraph;
                        StateTimer = Cfg.flutuanteTelegraphSec;
                        SetVisualTint(new Color(0.75f, 0.25f, 1.0f, 1f)); // Brilho violeta-ciano de carga
                        PlayEnemySfx(telegraphClip, 0.78f, 0.92f);
                    }
                    break;

                case EnemyFsmState.Telegraph:
                    StateTimer -= dt;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 28f);
                    SetVisualTint(Color.Lerp(new Color(0.65f, 0.25f, 1f, 1f), Color.white, pulse));
                    if (StateTimer <= 0f)
                    {
                        RestoreVisualTint();
                        if (HasLineOfSightToPlayer())
                        {
                            State = EnemyFsmState.Attack;
                            FireSlowProjectile();
                        }
                        CombatGroupCoordinator.ReleaseAttackToken(this);
                        AttackCooldownTimer = Cfg.flutuanteAttackCooldownSec;
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

        /// <summary>FIX 7 (P2): So dispara com Line-of-Sight livre (nao atravessa parede).</summary>
        public bool HasLineOfSightToPlayer()
        {
            if (PlayerTarget == null) return false;
            Vector3 muzzle = transform.position + transform.forward * 0.75f;
            Vector3 aimTarget = PlayerTarget.position + Vector3.up * 0.95f;
            Vector3 delta = aimTarget - muzzle;
            float dist = delta.magnitude;
            if (dist < 0.1f) return true;
            int mask = (1 << CollisionMatrixBootstrap.LayerEnvironment) | (1 << CollisionMatrixBootstrap.LayerDefault);
            return !Physics.Raycast(muzzle, delta / dist, dist - 0.35f, mask, QueryTriggerInteraction.Ignore);
        }

        private void FireSlowProjectile()
        {
            if (PlayerTarget == null) return;
            Vector3 muzzle = transform.position + transform.forward * 0.75f;
            Vector3 aimTarget = PlayerTarget.position + Vector3.up * 0.95f;
            Vector3 dir = (aimTarget - muzzle).normalized;

            var projGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projGo.name = "Flutuante_GlitchProjectile";
            projGo.transform.position = muzzle;
            projGo.transform.localScale = Vector3.one * 0.38f;
            var col = projGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = projGo.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_cachedProjectileMat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (sh != null)
                    {
                        _cachedProjectileMat = new Material(sh);
                        _cachedProjectileMat.name = "Mat_FlutuanteProjectile_URP_Cached";
                        if (_cachedProjectileMat.HasProperty("_BaseColor"))
                            _cachedProjectileMat.SetColor("_BaseColor", new Color(0.85f, 0.22f, 1.0f, 1f));
                        else if (_cachedProjectileMat.HasProperty("_Color"))
                            _cachedProjectileMat.SetColor("_Color", new Color(0.85f, 0.22f, 1.0f, 1f));
                    }
                }
                if (_cachedProjectileMat != null)
                    rend.sharedMaterial = _cachedProjectileMat;
            }

            var proj = projGo.AddComponent<ManifestationProjectile>();
            proj.direction = dir;
            proj.speed = Cfg.flutuanteProjectileSpeed;
            proj.lifetimeSec = Cfg.flutuanteProjectileLifetimeSec;
            proj.damageSegments = Cfg.flutuanteDamageSegments;
        }
    }
}
