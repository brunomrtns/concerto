using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// DezLocomotionController — Locomocao fisica viva com gravidade real (ADR-012 / RUN mega_v6 TAREFA 3):
    ///   - CharacterController + gravidade (-18 m/s^2) + slopeLimit 45 deg
    ///   - Aceleracao e desaceleracao por curvas fisicas (zero corte seco)
    ///   - Pulo com altura real (~1.15m) + disparo de Landing Dip ao pousar
    ///   - Alimenta Animator (Speed, TurnRate, IsGrounded, VerticalVel) + DezProceduralAnimator
    /// </summary>
    public sealed class DezLocomotionController : MonoBehaviour
    {
        [Header("Movimento & Inercia")]
        public float walkSpeed = 1.45f;
        public float runSpeed = 3.45f;
        public float accelRate = 7.5f;
        public float decelRate = 9.0f;
        public float turnSpeedDeg = 420f;

        [Header("Fisica & Gravidade")]
        public float gravity = -18.0f;
        public float jumpHeight = 1.15f;

        private CharacterController _cc;
        private Animator _anim;
        private DezProceduralAnimator _proc;

        private float _currentSpeed;
        private float _verticalVel;
        private bool _wasGrounded = true;
        private float _lastAirVy;

        // Inputs programaticos opcionais (para testes PlayMode e cutscenes)
        public Vector3 ScriptedMoveDir { get; set; }
        public bool ScriptedRun { get; set; }
        public bool ScriptedJump { get; set; }
        public float ScriptedYawRate { get; set; }
        public bool UseScriptedInput { get; set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.height = 1.25f;
                _cc.radius = 0.28f;
                _cc.center = new Vector3(0f, 0.625f, 0f);
                _cc.slopeLimit = 45f;
                _cc.stepOffset = 0.28f;
                _cc.skinWidth = 0.01f;
            }
            else
            {
                _cc.skinWidth = 0.01f;
            }
            _anim = GetComponentInChildren<Animator>(true);
            _proc = GetComponentInChildren<DezProceduralAnimator>(true);
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            bool grounded = _cc.isGrounded;

            if (grounded && !_wasGrounded && _lastAirVy < -2.0f)
            {
                if (_proc != null) _proc.NotifyLanding(_lastAirVy);
            }

            Vector3 desiredDir = UseScriptedInput ? ScriptedMoveDir : Vector3.zero;
            bool wantRun = UseScriptedInput && ScriptedRun;
            bool wantJump = UseScriptedInput && ScriptedJump;
            if (UseScriptedInput) ScriptedJump = false;

            float mag = Mathf.Clamp01(desiredDir.magnitude);
            float targetSpeed = mag * (wantRun ? runSpeed : walkSpeed);
            float rate = (targetSpeed > _currentSpeed) ? accelRate : decelRate;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, rate * dt);

            float turnRate = 0f;
            if (mag > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(desiredDir.normalized, Vector3.up);
                float prevYaw = transform.eulerAngles.y;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDeg * dt);
                turnRate = Mathf.DeltaAngle(prevYaw, transform.eulerAngles.y) / dt;
            }
            else if (UseScriptedInput && Mathf.Abs(ScriptedYawRate) > 0.1f)
            {
                transform.Rotate(0f, ScriptedYawRate * dt, 0f);
                turnRate = ScriptedYawRate;
            }

            if (grounded)
            {
                if (_verticalVel < -1.0f) _verticalVel = -1.0f;
                if (wantJump)
                {
                    _verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    if (_anim != null) _anim.SetTrigger("Jump");
                    grounded = false;
                }
            }
            else
            {
                _verticalVel += gravity * dt;
                _lastAirVy = _verticalVel;
            }

            Vector3 horizMove = transform.forward * _currentSpeed;
            Vector3 totalMove = (horizMove + Vector3.up * _verticalVel) * dt;
            _cc.Move(totalMove);

            if (_anim != null)
            {
                _anim.SetFloat("Speed", _currentSpeed, 0.08f, dt);
                _anim.SetFloat("TurnRate", Mathf.Clamp(turnRate / 180f, -1f, 1f), 0.10f, dt);
                _anim.SetBool("IsGrounded", _cc.isGrounded);
                _anim.SetFloat("VerticalVel", _verticalVel);
            }
            if (_proc != null)
            {
                _proc.SetLocomotionState(_cc.isGrounded, _currentSpeed, turnRate);
            }
            _wasGrounded = _cc.isGrounded;
        }
    }
}
