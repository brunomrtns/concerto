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
        public float walkSpeed = 1.6f;
        public float runSpeed = 3.6f;
        public float accelRate = 10.0f;
        public float decelRate = 12.0f;
        public float turnSpeedDeg = 720f;

        [Header("Fisica & Gravidade")]
        public float gravity = -18.0f;
        public float jumpHeight = 1.15f;

        [Header("Escalada de Borda (Ledge Grab / Mantle)")]
        public bool enableLedgeMantle = true;
        public float ledgeDetectDist = 0.62f;
        public float ledgeMinHeight = 0.42f;
        public float ledgeMaxHeight = 1.55f;
        public float mantleDuration = 0.38f;
        public float mantleCooldown = 0.22f;

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

        // Estado de escalada de borda (Ledge Grab / Mantle)
        private bool _isMantling;
        private float _mantleTimer;
        private float _mantleCooldownUntil;
        private Vector3 _mantleStartPos;
        private Vector3 _mantleMidPos;
        private Vector3 _mantleEndPos;
        private Quaternion _mantleStartRot;
        private Quaternion _mantleEndRot;

        public bool IsMantling => _isMantling;

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

            // Se esta escalando uma borda (Ledge Grab / Mantle), executa a subida suave com as maos
            if (_isMantling)
            {
                UpdateMantle(dt);
                return;
            }

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

                // Detecta borda ao alcance das maos/cabeca no ar e sobe automaticamente (Ledge Grab / Mantle)!
                if (enableLedgeMantle && Time.time >= _mantleCooldownUntil && _verticalVel <= 5.0f)
                {
                    Vector3 probeDir = (mag > 0.05f) ? desiredDir.normalized : transform.forward;
                    if (TryStartLedgeMantle(probeDir))
                        return;
                }
            }

            Vector3 horizMove = (mag > 0.05f ? desiredDir.normalized : transform.forward) * _currentSpeed;
            Vector3 totalMove = (horizMove + Vector3.up * _verticalVel) * dt;
            _cc.Move(totalMove);

            if (_anim != null)
            {
                _anim.SetFloat("Speed", _currentSpeed, 0.06f, dt);
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

        private bool TryStartLedgeMantle(Vector3 probeDir)
        {
            probeDir.y = 0f;
            if (probeDir.sqrMagnitude < 0.01f) probeDir = transform.forward;
            probeDir.Normalize();

            // 1. Sonda parede/borda na frente (testa altura do peito 0.70m e altura da cabeca 1.08m)
            bool hasWall =
                SphereCastIgnoreSelf(transform.position + Vector3.up * 0.70f, 0.16f, probeDir, ledgeDetectDist, out RaycastHit wallHit) ||
                SphereCastIgnoreSelf(transform.position + Vector3.up * 1.08f, 0.16f, probeDir, ledgeDetectDist, out wallHit);

            if (!hasWall) return false;
            // Precisa ser face quase vertical (nao rampa suave)
            if (Mathf.Abs(wallHit.normal.y) > 0.35f) return false;

            Vector3 wallNormal = new Vector3(wallHit.normal.x, 0f, wallHit.normal.z).normalized;
            if (wallNormal.sqrMagnitude < 0.01f) return false;

            // 2. Sonda topo da borda (0.28m para dentro da plataforma, descendo de cima da cabeca)
            float probeTopY = transform.position.y + ledgeMaxHeight + 0.18f;
            Vector3 topProbeOrigin = new Vector3(wallHit.point.x, probeTopY, wallHit.point.z) - wallNormal * 0.28f;
            float downDist = (ledgeMaxHeight - ledgeMinHeight) + 0.30f;

            if (!RaycastIgnoreSelf(topProbeOrigin, Vector3.down, downDist, out RaycastHit topHit))
                return false;

            // Topo precisa ser piso caminhavel
            if (topHit.normal.y < 0.70f) return false;

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < ledgeMinHeight || ledgeHeight > ledgeMaxHeight)
                return false;

            // 3. Verifica espaco livre para Dex ficar de pe em cima da borda (zero clipping em teto/parede)
            Vector3 standPos = new Vector3(topProbeOrigin.x, topHit.point.y + 0.02f, topProbeOrigin.z);
            Vector3 capBottom = standPos + Vector3.up * (_cc.radius + 0.06f);
            Vector3 capTop    = standPos + Vector3.up * Mathf.Max(_cc.radius + 0.12f, _cc.height - _cc.radius - 0.04f);
            var overlaps = Physics.OverlapCapsule(capBottom, capTop, _cc.radius * 0.85f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i].transform.root != transform.root)
                    return false;
            }

            // 4. Inicia escalada suave em 2 fases (puxa pra cima na beira -> sobe para o piso)
            _isMantling = true;
            _mantleTimer = 0f;
            _mantleStartPos = transform.position;
            Vector3 edgePos = new Vector3(wallHit.point.x, topHit.point.y + 0.02f, wallHit.point.z) + wallNormal * (_cc.radius * 0.65f);
            _mantleMidPos = edgePos;
            _mantleEndPos = standPos;
            _mantleStartRot = transform.rotation;
            _mantleEndRot = Quaternion.LookRotation(-wallNormal, Vector3.up);

            _verticalVel = 0f;
            _currentSpeed = 0f;
            _cc.enabled = false;

            if (_anim != null)
            {
                _anim.SetFloat("Speed", 0.5f);
                _anim.SetBool("IsGrounded", false);
                _anim.SetFloat("VerticalVel", 2.0f);
            }
            if (_proc != null) _proc.SetMantleState(true, 0f);
            return true;
        }

        private void UpdateMantle(float dt)
        {
            _mantleTimer += dt;
            float t = Mathf.Clamp01(_mantleTimer / Mathf.Max(0.15f, mantleDuration));

            // Fase 1 (0..0.55): puxa o corpo verticalmente com as maos ate a altura da borda
            // Fase 2 (0.55..1.0): projeta o corpo para cima da plataforma
            Vector3 pos;
            if (t < 0.55f)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                pos = Vector3.Lerp(_mantleStartPos, _mantleMidPos, u);
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f);
                pos = Vector3.Lerp(_mantleMidPos, _mantleEndPos, u);
            }

            transform.position = pos;
            transform.rotation = Quaternion.Slerp(_mantleStartRot, _mantleEndRot, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.2f)));

            if (_proc != null) _proc.SetMantleState(true, t);

            if (t >= 1f)
            {
                _isMantling = false;
                _mantleCooldownUntil = Time.time + mantleCooldown;
                transform.position = _mantleEndPos;
                _verticalVel = -1.0f;
                _wasGrounded = true;
                _cc.enabled = true;

                if (_anim != null)
                {
                    _anim.SetBool("IsGrounded", true);
                    _anim.SetFloat("VerticalVel", 0f);
                }
                if (_proc != null)
                {
                    _proc.SetMantleState(false, 0f);
                    _proc.NotifyLanding(-3.2f);
                }
            }
        }

        private bool SphereCastIgnoreSelf(Vector3 origin, float radius, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            var hits = Physics.SphereCastAll(origin, radius, dir, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform == null || hits[i].transform.root == transform.root) continue;
                if (hits[i].distance <= 0.001f) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        private bool RaycastIgnoreSelf(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            var hits = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform == null || hits[i].transform.root == transform.root) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
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
