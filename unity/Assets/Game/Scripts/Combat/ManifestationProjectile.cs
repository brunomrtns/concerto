using Studio.Game.Collision;
using Studio.Game.Powers;
using Studio.Game.Tool;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Projetil lento disparado pelo Flutuante / Guardiao / Estacao de Treino.
    ///   - ITimeScalable -> Estase (#93) desacelera para 50%
    ///   - ABSORVER (#109) -> ReflectBack() devolve o projetil em alta velocidade causando dano ao atacante!
    /// </summary>
    public sealed class ManifestationProjectile : MonoBehaviour, ITimeScalable
    {
        public Vector3 direction = Vector3.forward;
        public float speed = 7.2f;
        public float lifetimeSec = 4.0f;
        public int damageSegments = 1;
        public bool IsReflected { get; private set; }

        private float _age;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public Transform ScalableTransform => transform;

        public void SetLocalTimeScale(float scale) => LocalTimeScale = scale;

        public void ReflectBack(Vector3 playerPos)
        {
            IsReflected = true;
            direction = (-direction).normalized;
            speed *= 1.65f;
            _age = 0f;
            var r = GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.90f, 1f));
                else r.material.color = new Color(0.25f, 1.0f, 0.90f, 1f);
            }
        }

        private void OnEnable()
        {
            gameObject.layer = CollisionMatrixBootstrap.LayerProjectile;
            StasisFieldSystem.Register(this);
        }

        private void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime * LocalTimeScale;
            _age += dt;
            if (_age >= lifetimeSec)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 step = direction.normalized * (speed * dt);
            int mask = IsReflected
                ? ((1 << CollisionMatrixBootstrap.LayerEnemy) | (1 << CollisionMatrixBootstrap.LayerDefault) | (1 << CollisionMatrixBootstrap.LayerEnvironment))
                : ((1 << CollisionMatrixBootstrap.LayerPlayer) | (1 << CollisionMatrixBootstrap.LayerDefault) | (1 << CollisionMatrixBootstrap.LayerEnvironment));

            if (Physics.SphereCast(transform.position, 0.28f, direction.normalized, out RaycastHit hit, step.magnitude + 0.12f, mask, QueryTriggerInteraction.Ignore))
            {
                if (IsReflected)
                {
                    var dmgTarget = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmgTarget != null && dmgTarget.IsAlive)
                    {
                        dmgTarget.ApplyDamage(new ToolHitInfo { Damage = 55f, IsHeavy = true, KnockbackDir = direction.normalized });
                        Destroy(gameObject);
                        return;
                    }
                }
                else
                {
                    var parry = hit.collider.GetComponentInParent<ParryAndLockOnController>();
                    if (parry != null && parry.TryReflectProjectile(this))
                        return;

                    var hp = hit.collider.GetComponentInParent<PlayerHealthController>();
                    if (hp != null)
                    {
                        if (damageSegments > 0) hp.TryTakeDamage(damageSegments, transform.position);
                        Destroy(gameObject);
                        return;
                    }
                }
                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }
    }
}
