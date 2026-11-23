using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// SpringBone — Fisica secundaria 100% gratuita em C# (ADR-012 / Lei 9).
    /// Aplica inercia elastica com mola + amortecimento + gravidade em ossos secundarios
    /// (orelhas L/R, antenas, pingentes) em LateUpdate(), apos o Animator.
    /// </summary>
    public sealed class SpringBone : MonoBehaviour
    {
        [Tooltip("Direcao local da ponta do osso (ex.: +Y no Blender/Unity).")]
        public Vector3 boneAxis = Vector3.up;
        public float boneLength = 0.22f;
        [Range(5f, 120f)] public float stiffness = 65f;
        [Range(1f, 20f)]  public float damping = 9.0f;
        [Range(0f, 3f)]   public float gravityWeight = 0.28f;
        [Range(5f, 45f)]  public float maxAngleDeg = 22f;
        [Tooltip("Micro-oscilacao organica em repouso (idle vivo).")]
        public float idleBreezeAmp = 0.008f;

        private Quaternion _baseLocalRot;
        private Vector3 _tipWorldPos;
        private Vector3 _tipVelocity;
        private bool _initialized;

        private void OnEnable()
        {
            _baseLocalRot = transform.localRotation;
            _tipWorldPos = GetRestTipWorld();
            _tipVelocity = Vector3.zero;
            _initialized = true;
        }

        private Vector3 GetRestTipWorld()
        {
            Transform p = transform.parent;
            Quaternion restWorldRot = (p != null) ? (p.rotation * _baseLocalRot) : _baseLocalRot;
            return transform.position + restWorldRot * (boneAxis.normalized * boneLength);
        }

        private void LateUpdate()
        {
            if (!_initialized) OnEnable();
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);

            Vector3 restTip = GetRestTipWorld();
            // Micro-brisa / respiracao sutil para nunca ficar petrificado em idle
            float t = Time.time * 2.3f + transform.GetInstanceID() * 0.13f;
            restTip += new Vector3(Mathf.Sin(t) * idleBreezeAmp, Mathf.Cos(t * 1.3f) * idleBreezeAmp * 0.6f, 0f);

            // Mola + gravidade + amortecimento critico
            Vector3 force = (restTip - _tipWorldPos) * stiffness + Physics.gravity * (gravityWeight * 0.06f);
            _tipVelocity += force * dt;
            _tipVelocity *= Mathf.Exp(-damping * dt);
            _tipWorldPos += _tipVelocity * dt;

            // Projeta na esfera de raio boneLength
            Vector3 curDir = _tipWorldPos - transform.position;
            if (curDir.sqrMagnitude < 1e-6f) curDir = restTip - transform.position;
            curDir = curDir.normalized;
            _tipWorldPos = transform.position + curDir * boneLength;

            // Calcula rotacao relativa a partir do rest pose e aplica clamp angular
            Transform p = transform.parent;
            Quaternion restWorldRot = (p != null) ? (p.rotation * _baseLocalRot) : _baseLocalRot;
            Vector3 restDir = (restWorldRot * boneAxis.normalized).normalized;

            Quaternion delta = Quaternion.FromToRotation(restDir, curDir);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle, -maxAngleDeg, maxAngleDeg);
            if (axis.sqrMagnitude > 1e-5f)
                delta = Quaternion.AngleAxis(angle, axis);

            transform.rotation = delta * restWorldRot;
        }
    }
}
