using Studio.Game.Collision;
using Studio.Game.Powers;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Projetil lento disparado pelo Flutuante (e reutilizado no Boss).
    /// Implementa ITimeScalable -> a Estase (#93) desacelera o projetil para 50%,
    /// e o Eco Temporal (#93) atravessa com i-frames limpos!
    /// </summary>
    public sealed class ManifestationProjectile : MonoBehaviour, ITimeScalable
    {
        public Vector3 direction = Vector3.forward;
        public float speed = 7.2f;
        public float lifetimeSec = 4.0f;
        public int damageSegments = 1;

        private float _age;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public Transform ScalableTransform => transform;

        public void SetLocalTimeScale(float scale) => LocalTimeScale = scale;

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
            // Raycast anti-tunneling contra Player e cenario
            int mask = (1 << CollisionMatrixBootstrap.LayerPlayer)
                     | (1 << CollisionMatrixBootstrap.LayerDefault)
                     | (1 << CollisionMatrixBootstrap.LayerEnvironment);
            if (Physics.SphereCast(transform.position, 0.24f, direction.normalized, out RaycastHit hit, step.magnitude + 0.08f, mask, QueryTriggerInteraction.Ignore))
            {
                var hp = hit.collider.GetComponentInParent<PlayerHealthController>();
                if (hp != null)
                {
                    hp.TryTakeDamage(damageSegments, transform.position);
                    Destroy(gameObject);
                    return;
                }
                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }
    }
}
