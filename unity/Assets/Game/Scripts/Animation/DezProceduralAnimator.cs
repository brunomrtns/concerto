using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// DezProceduralAnimator — Camadas procedurais vivas AAA (ADR-012 / Lei 9):
    ///   1. Foot IK em tempo real (OnAnimatorIK + raycast em piso/rampa, sem flutuar/atravessar)
    ///   2. Landing Dip elastico (quadril absorve impacto do pulo proporcional a velocidade de queda)
    ///   3. Idle vivo (respiracao continua ~4s no torax/ombros + micro-olhar)
    ///   4. Head Look-At suave + Banking lateral nas curvas
    /// 100% gratuito, reutilizavel em qualquer personagem Humanoid do Studio Core.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class DezProceduralAnimator : MonoBehaviour
    {
        [Header("Foot IK (Chao e Rampas)")]
        public bool enableFootIK = true;
        public float footRaycastUp = 0.55f;
        public float footRaycastDown = 1.00f;
        public float footOffsetY = 0.04f;
        public LayerMask groundMask = ~0;

        [Header("Landing Dip (Absorcao de Impacto)")]
        public float landingDipStrength = 0.032f;
        public float landingMaxDip = 0.22f;
        public float landingSpringK = 140f;
        public float landingDamping = 16f;

        [Header("Idle Vivo & Banking")]
        public float breathCycleSec = 3.8f;
        public float breathChestDeg = 2.6f;
        public float maxBankDeg = 12f;

        [Header("Look-At")]
        public Transform lookTarget;
        [Range(0f, 1f)] public float lookWeight = 0.55f;

        private Animator _anim;
        private float _dipOffset;
        private float _dipVel;
        private float _currentBank;
        private float _targetBank;
        private bool _isGrounded = true;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
        }

        public void SetLocomotionState(bool isGrounded, float speed, float turnRateDegPerSec)
        {
            _isGrounded = isGrounded;
            float speedFactor = Mathf.Clamp01(speed / 3.5f);
            _targetBank = Mathf.Clamp(-turnRateDegPerSec * 0.08f * speedFactor, -maxBankDeg, maxBankDeg);
        }

        public void NotifyLanding(float verticalImpactSpeed)
        {
            float impulse = Mathf.Clamp(Mathf.Abs(verticalImpactSpeed) * landingDipStrength, 0.04f, landingMaxDip);
            _dipVel -= impulse * 16f;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            // Mola critica do Landing Dip
            float force = -_dipOffset * landingSpringK - _dipVel * landingDamping;
            _dipVel += force * dt;
            _dipOffset = Mathf.Clamp(_dipOffset + _dipVel * dt, -landingMaxDip, 0.04f);

            // Banking suave nas curvas
            _currentBank = Mathf.Lerp(_currentBank, _targetBank, 1f - Mathf.Exp(-8f * dt));
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isHuman) return;

            // 1. Respiracao continua no torax/ombros (ciclo ~3.8s)
            float phase = (Time.time / Mathf.Max(0.5f, breathCycleSec)) * Mathf.PI * 2f;
            float breath = Mathf.Sin(phase);
            Transform chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
            {
                chest.localRotation *= Quaternion.Euler(breath * breathChestDeg, 0f, _currentBank * 0.5f);
            }
            Transform spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null && Mathf.Abs(_currentBank) > 0.05f)
            {
                spine.localRotation *= Quaternion.Euler(0f, 0f, _currentBank * 0.5f);
            }

            // 2. Absorcao de queda (Landing Dip no Hips)
            if (Mathf.Abs(_dipOffset) > 0.0005f)
            {
                Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    hips.position += new Vector3(0f, _dipOffset, 0f);
                }
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_anim == null || !_anim.isHuman) return;

            // Look-At suave
            if (lookTarget != null)
            {
                _anim.SetLookAtWeight(lookWeight, 0.15f, 0.75f, 0.85f, 0.5f);
                _anim.SetLookAtPosition(lookTarget.position);
            }
            else
            {
                // Olhar natural levemente vivo para frente
                float sway = Mathf.Sin(Time.time * 0.9f) * 0.35f;
                Vector3 fwdTarget = transform.position + transform.forward * 4f + transform.right * sway + Vector3.up * 1.15f;
                _anim.SetLookAtWeight(0.35f, 0.08f, 0.55f, 0.7f, 0.5f);
                _anim.SetLookAtPosition(fwdTarget);
            }

            // Foot IK em piso e rampas quando no chao
            if (!enableFootIK || !_isGrounded)
            {
                _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }

            ApplyFootIK(AvatarIKGoal.LeftFoot);
            ApplyFootIK(AvatarIKGoal.RightFoot);
        }

        private void ApplyFootIK(AvatarIKGoal goal)
        {
            Vector3 footPos = _anim.GetIKPosition(goal);
            Vector3 rayOrigin = footPos + Vector3.up * footRaycastUp;
            var hits = Physics.RaycastAll(rayOrigin, Vector3.down, footRaycastUp + footRaycastDown, groundMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit bestHit = default;
            float bestDist = float.MaxValue;
            Transform selfRoot = transform.root;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.root == selfRoot) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            if (found)
            {
                Vector3 targetPos = bestHit.point + Vector3.up * footOffsetY;
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, bestHit.normal) * _anim.GetIKRotation(goal);
                // Peso suave (evita puxar o pe durante a fase alta da passada)
                float heightAboveGround = Mathf.Max(0f, footPos.y - targetPos.y);
                float w = Mathf.Clamp01(1f - (heightAboveGround / 0.28f));
                _anim.SetIKPositionWeight(goal, w);
                _anim.SetIKRotationWeight(goal, w * 0.85f);
                _anim.SetIKPosition(goal, targetPos);
                _anim.SetIKRotation(goal, targetRot);
            }
            else
            {
                _anim.SetIKPositionWeight(goal, 0f);
                _anim.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}
