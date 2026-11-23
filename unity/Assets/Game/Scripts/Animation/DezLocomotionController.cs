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

        [Header("Camera 3a Pessoa Inteligente AAA (Modo Jogavel)")]
        public bool enablePlayableCamera = true;
        public float camDistance = 3.2f;
        public float camHeight = 1.25f;
        public float camMouseSens = 2.6f;
        [Tooltip("Velocidade com que a camera se alinha suavemente atras do personagem quando o jogador nao gira o mouse.")]
        public float camAutoAlignSpeedDeg = 95f;
        public float camManualPauseSec = 1.6f;

        [Header("Anti-Limbo (Seguranca Padrao da Fabrica)")]
        public float killFloorY = -3.0f;

        private CharacterController _cc;
        private Animator _anim;
        private DezProceduralAnimator _proc;

        private float _currentSpeed;
        private float _verticalVel;
        private bool _wasGrounded = true;
        private float _lastAirVy;
        private float _camYaw = 70f;
        private float _camPitch = 18f;
        private float _lastManualCamTime = -10f;
        private Vector3 _camSmoothPos;
        private bool _camInit;
        private Vector3 _lastSafeGroundedPos;
        private bool _hasSafePos;

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
            _camYaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            bool grounded = _cc.isGrounded;

            if (grounded && !_wasGrounded && _lastAirVy < -2.0f)
            {
                if (_proc != null) _proc.NotifyLanding(_lastAirVy);
            }

            Vector3 desiredDir;
            bool wantRun;
            bool wantJump;

            if (UseScriptedInput)
            {
                desiredDir = ScriptedMoveDir;
                wantRun = ScriptedRun;
                wantJump = ScriptedJump;
                ScriptedJump = false;
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                // Direcao sempre 100% alinhada ao eixo horizontal real da camera (W = reto para frente na tela!)
                Vector3 camFwd = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.forward;
                Vector3 camRight = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.right;
                desiredDir = (camFwd * v + camRight * h);
                if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();
                wantRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                wantJump = Input.GetKeyDown(KeyCode.Space);

                // Rotacao de camera via mouse (segurando botao direito ou cursor travado) ou Q/E
                bool manualCam = false;
                if (Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked)
                {
                    float mx = Input.GetAxis("Mouse X");
                    float my = Input.GetAxis("Mouse Y");
                    if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) manualCam = true;
                    _camYaw += mx * camMouseSens;
                    _camPitch = Mathf.Clamp(_camPitch - my * camMouseSens * 0.8f, -10f, 55f);
                }
                if (Input.GetKey(KeyCode.Q)) { _camYaw -= 110f * dt; manualCam = true; }
                if (Input.GetKey(KeyCode.E)) { _camYaw += 110f * dt; manualCam = true; }
                if (manualCam) _lastManualCamTime = Time.time;

                // Smart Auto-Align AAA (padrao Zelda BotW / Mario 3D / Ratchet):
                // Quando o jogador anda/vira sem mexer na camera, ela orbita suavemente atras das costas da Dez!
                if (!manualCam && (Time.time - _lastManualCamTime) > camManualPauseSec && _currentSpeed > 0.35f)
                {
                    // Se o jogador apertou S (andando em direcao a camera), nao gira bruscamente; alinha suavemente em curvas/laterais
                    float dotFwd = Vector3.Dot(transform.forward, camFwd);
                    if (dotFwd > -0.55f)
                    {
                        float targetYaw = transform.eulerAngles.y;
                        float yawDelta = Mathf.DeltaAngle(_camYaw, targetYaw);
                        float alignStrength = Mathf.Clamp01(Mathf.Abs(h) * 0.85f + 0.35f);
                        _camYaw = Mathf.MoveTowardsAngle(_camYaw, targetYaw, camAutoAlignSpeedDeg * alignStrength * dt);
                    }
                }

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                    camDistance = Mathf.Clamp(camDistance - scroll * 2.0f, 1.8f, 6.0f);
                if (Input.GetKeyDown(KeyCode.Escape))
                    Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked) ? CursorLockMode.None : CursorLockMode.Locked;
            }

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

            // Seguranca Anti-Limbo (padrao da fabrica): grava ultimo ponto seguro no chao e resgata se cair fora do mapa
            if (_cc.isGrounded && transform.position.y > -0.5f)
            {
                _lastSafeGroundedPos = transform.position;
                _hasSafePos = true;
            }
            if (!UseScriptedInput && _hasSafePos && transform.position.y < killFloorY)
            {
                _cc.enabled = false;
                transform.position = _lastSafeGroundedPos + Vector3.up * 0.25f;
                _verticalVel = 0f;
                _cc.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (UseScriptedInput || !enablePlayableCamera) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 pivot = transform.position + Vector3.up * camHeight;
            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 dir = -(rot * Vector3.forward);
            Vector3 desiredPos = pivot + dir * camDistance;

            // Spring Arm com SphereCast (raio 0.22m) — nunca atravessa quinas de paredes ou canos!
            if (Physics.SphereCast(pivot, 0.22f, dir, out RaycastHit hit, camDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.root != transform.root)
                    desiredPos = pivot + dir * Mathf.Max(0.75f, hit.distance - 0.12f);
            }

            if (!_camInit)
            {
                _camSmoothPos = desiredPos;
                _camInit = true;
            }
            else
            {
                float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
                _camSmoothPos = Vector3.Lerp(_camSmoothPos, desiredPos, 1f - Mathf.Exp(-14f * dt));
            }
            cam.transform.position = _camSmoothPos;
            cam.transform.LookAt(pivot);
        }
    }
}
