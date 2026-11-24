using Studio.Game.Audio;
using Studio.Game.Collision;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Unity.Cinemachine;
using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// DezLocomotionController — Base de Game Feel 3a Pessoa para combate de CONCERTO:
    ///   - 100% tunavel via DezMovementConfig (ScriptableObject) — ZERO numero magico hardcoded
    ///   - Aceleracao/desaceleracao responsivas (chao vs ar) + parada brusca + rotacao suave
    ///   - Pulo com Coyote Time + Input Buffer + Corte de Altura (soltar Espaco cedo) + Queda firme
    ///   - Impulso Mecanico (Dash dos propulsores): rajada curta, cooldown 2s, particulas + SFX, SEM i-frames (Eco e task futura!)
    ///   - Escalada de Borda (Ledge Grab / Mantle) preservada integralmente
    ///   - Camera 3a Pessoa via Cinemachine 3.1.4 (CinemachineThirdPersonFollow + CinemachineDeoccluder + leve shoulder offset)
    ///     com sensibilidade integrada a GameSettingsManager.CameraSensitivity
    /// </summary>
    public sealed class DezLocomotionController : MonoBehaviour
    {
        [Header("Config Data-Driven (ScriptableObject)")]
        public DezMovementConfig config;

        [Header("Camera 3a Pessoa (Cinemachine 3.1.4)")]
        public bool enablePlayableCamera = true;
        public Transform cameraFollowTarget;
        public CinemachineCamera gameplayVcam;

        [Header("Feedback do Impulso Mecanico (Propulsores)")]
        public ParticleSystem thrusterParticles;
        public AudioSource thrusterAudio;
        public AudioClip dashSfxClip;

        [Header("Feedback de Passos por Superficie & Pulo/Pouso (min. 3 variacoes/superficie)")]
        public AudioSource footstepAudio;
        public AudioClip footstepFloorClip;
        public AudioClip footstepMetalClip;
        public AudioClip footstepPuddleClip;
        public AudioClip[] footstepFloorClips;
        public AudioClip[] footstepMetalClips;
        public AudioClip[] footstepPuddleClips;
        public AudioClip jumpTakeoffClip;
        public AudioClip landingThudClip;

        private CharacterController _cc;
        private Animator _anim;
        private DezProceduralAnimator _proc;
        private CinemachineBrain _cmBrain;
        private CinemachineThirdPersonFollow _tpFollow;
        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;
        private Renderer[] _playerRenderers;
        private bool _playerHiddenByCamera;

        private DezMovementConfig _runtimeCfg;
        private Vector3 _horizVelocity;
        private float _currentSpeed;
        private float _verticalVel;
        private bool _wasGrounded = true;
        private float _lastAirVy;
        private float _stepTimer;
        private float _landingBobOffset;
        private float _baseFov = 55f;
        private float _dashFovKickTimer;
        private string _lastSurfaceType = "floor";
        private int _lastFootstepVarIdx = -1;
        private Vector3 _lastWallNormal;
        private float _lastWallHitTime = -10f;

        // Flag de comparacao QA (reproduz skinWidth=0.01 e sem wallSlide para video ANTES vs DEPOIS)
        public bool SimulateLegacySnagMode { get; set; }
        public string LastSurfaceType => _lastSurfaceType;
        public int LastFootstepVariationIndex => _lastFootstepVarIdx;
        public int FootstepTriggerCount { get; private set; }
        public int JumpSfxTriggerCount { get; private set; }
        public int WallSlideCount { get; private set; }
        public float CurrentFovKickDeg { get; private set; }

        // Coyote Time + Jump Buffer
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _jumpCutApplied;

        // Impulso Mecanico (Dash — SEM i-frames!)
        private bool _isDashing;
        private float _dashTimer;
        private float _dashCooldownUntil = -10f;
        private Vector3 _dashDir;

        // Camera Orbit
        private float _camYaw = 70f;
        private float _camPitch = 16f;
        private float _currentCamDist = 3.2f;
        private float _lastManualCamTime = -10f;
        private Vector3 _camSmoothPos;
        private bool _camInit;

        // Anti-Limbo
        private Vector3 _lastSafeGroundedPos;
        private bool _hasSafePos;

        // Escalada de Borda (Ledge Grab / Mantle)
        private bool _isMantling;
        private float _mantleTimer;
        private float _mantleCooldownUntil;
        private Vector3 _mantleStartPos;
        private Vector3 _mantleMidPos;
        private Vector3 _mantleEndPos;
        private Quaternion _mantleStartRot;
        private Quaternion _mantleEndRot;

        public bool IsMantling => _isMantling;
        public bool IsDashing => _isDashing;
        public bool HasIFrames => false; // Regra canonica: i-frames pertencem exclusivamente ao poder "Eco" (task futura)
        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownUntil - Time.time);
        public float CurrentSpeed => _currentSpeed;
        public float CameraYaw => _camYaw;
        public float CameraPitch => _camPitch;

        // Inputs programaticos opcionais (para testes PlayMode, showcase de feel e cutscenes)
        public Vector3 ScriptedMoveDir { get; set; }
        public bool ScriptedRun { get; set; }
        public bool ScriptedJump { get; set; }
        public bool ScriptedJumpHeld { get; set; } = true;
        public bool ScriptedDash { get; set; }
        public float ScriptedYawRate { get; set; }
        public float ScriptedCamYawDelta { get; set; }
        public float ScriptedCamPitchDelta { get; set; }
        public bool UseScriptedInput { get; set; }

        private DezMovementConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<DezMovementConfig>();
                return _runtimeCfg;
            }
        }

        private void Awake()
        {
            PlayerRegistry.Register(gameObject);
            _cc = GetComponent<CharacterController>();
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.height = 1.25f;
                _cc.radius = 0.28f;
                _cc.center = new Vector3(0f, 0.625f, 0f);
            }
            ApplyControllerAntiSnagTuning();

            _anim = GetComponentInChildren<Animator>(true);
            _proc = GetComponentInChildren<DezProceduralAnimator>(true);

            // Garante que o filho placeholder antigo "Model" (capsula azul) nunca seja exibido se houver HeroVisual
            Transform legacyModel = transform.Find("Model");
            if (legacyModel != null)
            {
                var mr = legacyModel.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                if (transform.Find("Player_Dez_Placeholder_HeroVisual") != null)
                    legacyModel.gameObject.SetActive(false);
            }

            var allRenderers = GetComponentsInChildren<Renderer>(true);
            var visibleList = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                // Nunca incluir renderers originalmente desligados (ex.: placeholder azul "Model" com enabled=false)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                    visibleList.Add(r);
            }
            _playerRenderers = visibleList.ToArray();
            _camYaw = transform.eulerAngles.y;
            _currentCamDist = Cfg.cameraDistance;

            if (gameplayVcam != null)
                _tpFollow = gameplayVcam.GetComponent<CinemachineThirdPersonFollow>();
        }

        public void ApplyControllerAntiSnagTuning()
        {
            if (_cc == null) return;
            var cfg = Cfg;
            if (SimulateLegacySnagMode)
            {
                _cc.skinWidth = 0.01f;
                _cc.stepOffset = 0.24f;
                _cc.slopeLimit = 45f;
                _cc.minMoveDistance = 0.001f;
            }
            else
            {
                _cc.skinWidth = Mathf.Clamp(cfg.capsuleSkinWidth, 0.02f, 0.08f);
                _cc.stepOffset = Mathf.Clamp(cfg.stepOffset, 0.20f, 0.45f);
                _cc.slopeLimit = Mathf.Clamp(cfg.slopeLimit, 35f, 60f);
                _cc.minMoveDistance = 0f;
            }
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) _cmBrain = cam.GetComponent<CinemachineBrain>();
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            EnsureAudioBankClipsBound();
            SyncCinemachineGameplayCamera();
        }

        private void EnsureAudioBankClipsBound()
        {
            var mix = AudioMixController.Instance;
            var bank = mix != null ? mix.bank : Resources.Load<ConcertoAudioBank>("Audio/ConcertoAudioBank");
            if (bank != null)
            {
                if ((footstepFloorClips == null || footstepFloorClips.Length < 3) && bank.footstepFloorClips != null && bank.footstepFloorClips.Length > 0)
                    footstepFloorClips = bank.footstepFloorClips;
                if ((footstepMetalClips == null || footstepMetalClips.Length < 3) && bank.footstepMetalClips != null && bank.footstepMetalClips.Length > 0)
                    footstepMetalClips = bank.footstepMetalClips;
                if ((footstepPuddleClips == null || footstepPuddleClips.Length < 3) && bank.footstepPuddleClips != null && bank.footstepPuddleClips.Length > 0)
                    footstepPuddleClips = bank.footstepPuddleClips;
                if (jumpTakeoffClip == null) jumpTakeoffClip = bank.jumpTakeoffClip;
                if (landingThudClip == null) landingThudClip = bank.landingThudClip;
                if (footstepAudio != null && footstepAudio.outputAudioMixerGroup == null && bank.sfxGroup != null)
                    footstepAudio.outputAudioMixerGroup = bank.sfxGroup;
                if (thrusterAudio != null && thrusterAudio.outputAudioMixerGroup == null && bank.sfxGroup != null)
                    thrusterAudio.outputAudioMixerGroup = bank.sfxGroup;
            }
        }

        private void OnEnable()
        {
            SyncCinemachineGameplayCamera();
        }

        private bool IsAnyCutscenePlaying()
        {
            return (_cs01 != null && _cs01.IsPlaying)
                || (_cs02 != null && _cs02.IsPlaying)
                || (_cs03 != null && _cs03.IsPlaying)
                || (Cutscene04SkyCrackedController.ActiveInstance != null && Cutscene04SkyCrackedController.ActiveInstance.IsPlaying);
        }

        private void SyncCinemachineGameplayCamera()
        {
            if (!enablePlayableCamera || gameplayVcam == null) return;
            if (_cmBrain == null && Camera.main != null) _cmBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (_tpFollow == null) _tpFollow = gameplayVcam.GetComponent<CinemachineThirdPersonFollow>();

            if (_tpFollow != null)
            {
                _tpFollow.ShoulderOffset = Cfg.shoulderOffset;
                _tpFollow.CameraDistance = _currentCamDist;
                _tpFollow.Damping = Cfg.followDamping;
            }

            if (!IsAnyCutscenePlaying() && _cmBrain != null)
                _cmBrain.enabled = true;
        }

        public bool TryTriggerDash(Vector3 preferredDir)
        {
            if (_isDashing || _isMantling || Time.time < _dashCooldownUntil)
                return false;

            preferredDir.y = 0f;
            _dashDir = preferredDir.sqrMagnitude > 0.01f ? preferredDir.normalized : transform.forward;
            _isDashing = true;
            _dashTimer = Cfg.dashDurationSec;
            _dashFovKickTimer = Mathf.Min(0.20f, Cfg.dashFovKickDurationSec);
            _dashCooldownUntil = Time.time + Cfg.dashCooldownSec;

            transform.rotation = Quaternion.LookRotation(_dashDir, Vector3.up);
            _horizVelocity = _dashDir * Cfg.dashSpeed;
            _currentSpeed = Cfg.dashSpeed;
            if (_verticalVel < 0f) _verticalVel = 0f;

            if (thrusterParticles != null)
            {
                thrusterParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                thrusterParticles.Play();
            }
            if (thrusterAudio != null && dashSfxClip != null)
            {
                thrusterAudio.PlayOneShot(dashSfxClip, 0.85f * GameSettingsManager.SfxVolume);
            }
            return true;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f) return;

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            var cfg = Cfg;

            if (_isMantling)
            {
                UpdateMantle(dt);
                return;
            }

            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                _coyoteTimer = cfg.coyoteTimeSec;
                _jumpCutApplied = false;
                if (!_wasGrounded && _lastAirVy < -2.0f)
                {
                    if (_proc != null) _proc.NotifyLanding(_lastAirVy);
                    float impact = Mathf.Clamp01(Mathf.Abs(_lastAirVy) / 14f);
                    _landingBobOffset = -cfg.landingBobAmplitude * Mathf.Max(0.35f, impact);
                    PlaySurfaceSfx(landingThudClip, 0.55f + 0.35f * impact, 0.92f, 1.05f);
                }
            }
            else
            {
                _coyoteTimer = Mathf.Max(0f, _coyoteTimer - dt);
            }

            // 1. Leitura de Input (Real ou Scriptado)
            Vector3 desiredDir;
            bool wantRun;
            bool jumpPressed;
            bool jumpHeld;
            bool dashPressed;

            if (UseScriptedInput)
            {
                desiredDir = ScriptedMoveDir;
                wantRun = ScriptedRun;
                jumpPressed = ScriptedJump;
                jumpHeld = ScriptedJumpHeld;
                dashPressed = ScriptedDash;
                ScriptedJump = false;
                ScriptedDash = false;

                if (Mathf.Abs(ScriptedCamYawDelta) > 0.001f || Mathf.Abs(ScriptedCamPitchDelta) > 0.001f)
                {
                    _camYaw += ScriptedCamYawDelta;
                    _camPitch = Mathf.Clamp(_camPitch + ScriptedCamPitchDelta, cfg.minPitchDeg, cfg.maxPitchDeg);
                    ScriptedCamYawDelta = 0f;
                    ScriptedCamPitchDelta = 0f;
                    _lastManualCamTime = Time.time;
                }
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                Vector3 camFwd = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.forward;
                Vector3 camRight = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.right;
                desiredDir = (camFwd * v + camRight * h);
                if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

                wantRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                jumpPressed = Input.GetKeyDown(KeyCode.Space);
                jumpHeld = Input.GetKey(KeyCode.Space);
                dashPressed = Input.GetKeyDown(cfg.dashPrimaryKey) || Input.GetKeyDown(cfg.dashAltKey);

                float sens = cfg.baseMouseSensitivity * GameSettingsManager.CameraSensitivity;
                bool manualCam = false;
                if (Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked)
                {
                    float mx = Input.GetAxis("Mouse X");
                    float my = Input.GetAxis("Mouse Y");
                    if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) manualCam = true;
                    _camYaw += mx * sens;
                    _camPitch = Mathf.Clamp(_camPitch - my * sens * 0.8f, cfg.minPitchDeg, cfg.maxPitchDeg);
                }
                if (Input.GetKey(KeyCode.Q)) { _camYaw -= 110f * dt; manualCam = true; }
                bool focusInteract = DialogueController.Instance != null && DialogueController.Instance.HasFocusedInteractable;
                if (!focusInteract && Input.GetKey(KeyCode.E)) { _camYaw += 110f * dt; manualCam = true; }
                if (manualCam) _lastManualCamTime = Time.time;

                if (!manualCam && (Time.time - _lastManualCamTime) > cfg.manualCamPauseSec && _currentSpeed > 0.35f)
                {
                    float dotFwd = Vector3.Dot(transform.forward, camFwd);
                    if (dotFwd > -0.55f)
                    {
                        float targetYaw = transform.eulerAngles.y;
                        float alignStrength = Mathf.Clamp01(Mathf.Abs(h) * 0.85f + 0.35f);
                        _camYaw = Mathf.MoveTowardsAngle(_camYaw, targetYaw, cfg.autoAlignSpeedDeg * alignStrength * dt);
                    }
                }

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                    _currentCamDist = Mathf.Clamp(_currentCamDist - scroll * 2.0f, cfg.minZoomDistance, cfg.maxZoomDistance);
            }

            // 2. Jump Input Buffer
            if (jumpPressed)
                _jumpBufferTimer = cfg.jumpBufferSec;
            else
                _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

            // 3. Impulso Mecanico (Dash dos Propulsores — SEM i-frames)
            if (dashPressed)
                TryTriggerDash(desiredDir);

            float turnRate = 0f;
            float mag = Mathf.Clamp01(desiredDir.magnitude);

            if (_isDashing)
            {
                _dashTimer -= dt;
                _horizVelocity = _dashDir * cfg.dashSpeed;
                _currentSpeed = cfg.dashSpeed;
                if (_dashTimer <= 0f)
                {
                    _isDashing = false;
                    _horizVelocity = _dashDir * (wantRun ? cfg.runSpeed : cfg.walkSpeed);
                    _currentSpeed = _horizVelocity.magnitude;
                }
            }
            else
            {
                // 4. Aceleracao / Desaceleracao responsivas (chao vs ar)
                Vector3 targetVel = desiredDir * (wantRun ? cfg.runSpeed : cfg.walkSpeed);
                bool accelerating = targetVel.sqrMagnitude > 0.001f;
                float rate = grounded
                    ? (accelerating ? cfg.groundAccel : cfg.groundDecel)
                    : (accelerating ? cfg.airAccel : cfg.airDecel);

                _horizVelocity = Vector3.MoveTowards(_horizVelocity, targetVel, rate * dt);
                _currentSpeed = _horizVelocity.magnitude;

                if (mag > 0.05f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(desiredDir.normalized, Vector3.up);
                    float prevYaw = transform.eulerAngles.y;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, cfg.turnSpeedDeg * dt);
                    turnRate = Mathf.DeltaAngle(prevYaw, transform.eulerAngles.y) / dt;
                }
                else if (UseScriptedInput && Mathf.Abs(ScriptedYawRate) > 0.1f)
                {
                    transform.Rotate(0f, ScriptedYawRate * dt, 0f);
                    turnRate = ScriptedYawRate;
                }
            }

            // 5. Pulo com Coyote Time + Input Buffer + Corte de Altura + Queda Firme
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && !_isDashing)
            {
                _verticalVel = Mathf.Sqrt(cfg.jumpHeight * -2f * cfg.gravity);
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                _jumpCutApplied = false;
                grounded = false;
                if (_anim != null) _anim.SetTrigger("Jump");
                if (jumpTakeoffClip != null)
                {
                    PlaySurfaceSfx(jumpTakeoffClip, 0.58f, 0.96f, 1.06f);
                    JumpSfxTriggerCount++;
                }
            }
            else if (grounded)
            {
                if (_verticalVel < -1.0f) _verticalVel = -1.0f;
            }
            else
            {
                // Corte de altura ao soltar Espaco cedo enquanto sobe
                if (!jumpHeld && _verticalVel > 0f && !_jumpCutApplied)
                {
                    _verticalVel *= cfg.jumpCutMultiplier;
                    _jumpCutApplied = true;
                }

                float g = (_verticalVel < 0f) ? cfg.gravity * cfg.fallGravityMultiplier : cfg.gravity;
                if (_isDashing) g *= 0.15f; // Mantem linha limpa durante a rajada curta de 0.18s
                _verticalVel += g * dt;
                _lastAirVy = _verticalVel;

                // Escalada de borda preservada!
                if (cfg.enableLedgeMantle && !_isDashing && Time.time >= _mantleCooldownUntil && _verticalVel <= 5.0f)
                {
                    Vector3 probeDir = (mag > 0.05f) ? desiredDir.normalized : transform.forward;
                    if (TryStartLedgeMantle(probeDir))
                        return;
                }
            }

            Vector3 totalMove = (_horizVelocity + Vector3.up * _verticalVel) * dt;

            // Anti-tunneling matematico no Dash / alta velocidade: CapsuleCast sweep antes de _cc.Move()
            // Garante que dash a 13.5m/s nunca atravessa parede fina mesmo em hitch de 50ms (0.675m/frame)!
            Vector3 horizStep = new Vector3(totalMove.x, 0f, totalMove.z);
            float horizDist = horizStep.magnitude;
            if (!SimulateLegacySnagMode && horizDist > _cc.radius * 0.55f)
            {
                Vector3 p1 = transform.position + _cc.center - Vector3.up * Mathf.Max(0.05f, (_cc.height * 0.5f) - _cc.radius);
                Vector3 p2 = transform.position + _cc.center + Vector3.up * Mathf.Max(0.05f, (_cc.height * 0.5f) - _cc.radius);
                if (Physics.CapsuleCast(p1, p2, _cc.radius * 0.92f, horizStep / horizDist, out RaycastHit sweepHit, horizDist + _cc.skinWidth, Studio.Game.Collision.CollisionMatrixBootstrap.SolidWorldMask, QueryTriggerInteraction.Ignore))
                {
                    if (sweepHit.transform.root != transform.root)
                    {
                        float allowed = Mathf.Max(0f, sweepHit.distance - _cc.skinWidth);
                        Vector3 clampedHoriz = (horizStep / horizDist) * allowed;
                        totalMove.x = clampedHoriz.x;
                        totalMove.z = clampedHoriz.z;
                    }
                }
            }

            _cc.Move(totalMove);

            // Recupera landing bob da camera + dispara passos por superficie
            _landingBobOffset = Mathf.MoveTowards(_landingBobOffset, 0f, cfg.landingBobRecoverSpeed * dt);
            UpdateSurfaceFootsteps(dt, wantRun);

            if (_anim != null)
            {
                _anim.SetFloat("Speed", _currentSpeed, 0.06f, dt);
                _anim.SetBool("IsGrounded", _cc.isGrounded);
                _anim.SetFloat("VerticalVel", _verticalVel);

                // Casa velocidade de playback do ciclo de passada com velocidade REAL do controller (ZERO foot slide!)
                float speedMult = 1.0f;
                if (_currentSpeed > 0.15f)
                {
                    float runBlend = Mathf.Clamp01((_currentSpeed - cfg.walkSpeed) / Mathf.Max(0.1f, cfg.runSpeed - cfg.walkSpeed));
                    float clipNominalSpeed = Mathf.Lerp(2.15f, 4.35f, runBlend);
                    speedMult = Mathf.Clamp(_currentSpeed / Mathf.Max(0.5f, clipNominalSpeed), 0.72f, 1.38f);
                }
                _anim.SetFloat("LocomotionSpeedMult", speedMult);
            }
            if (_proc != null)
            {
                _proc.SetLocomotionState(_cc.isGrounded, _currentSpeed, turnRate);
                _proc.IsRightHandHoldingTool = Studio.Game.Tool.MultiToolController.Instance != null
                    && !Studio.Game.Tool.MultiToolController.Instance.IsRepairModeActive;
            }
            _wasGrounded = _cc.isGrounded;

            // Seguranca Anti-Limbo
            if (_cc.isGrounded && transform.position.y > -0.5f)
            {
                _lastSafeGroundedPos = transform.position;
                _hasSafePos = true;
            }
            if (!UseScriptedInput && _hasSafePos && transform.position.y < cfg.killFloorY)
            {
                _cc.enabled = false;
                transform.position = _lastSafeGroundedPos + Vector3.up * 0.25f;
                _verticalVel = 0f;
                _horizVelocity = Vector3.zero;
                _cc.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (!enablePlayableCamera) return;
            if (IsAnyCutscenePlaying()) return;

            var cfg = Cfg;
            UpdateCameraProximityPlayerFade(cfg);
            UpdateDashFovKick(cfg);
            if (cameraFollowTarget != null && gameplayVcam != null)
            {
                // Atualiza pivot da CinemachineCamera 3.1.4 com landing bob sutil
                cameraFollowTarget.localPosition = new Vector3(0f, cfg.pivotHeight + _landingBobOffset, 0f);
                cameraFollowTarget.rotation = Quaternion.Euler(_camPitch, _camYaw, 0f);
                if (_tpFollow != null)
                {
                    _tpFollow.CameraDistance = _currentCamDist;
                    _tpFollow.ShoulderOffset = cfg.shoulderOffset;
                    _tpFollow.Damping = cfg.followDamping;
                }
                if (_cmBrain != null && !_cmBrain.enabled)
                    _cmBrain.enabled = true;
                return;
            }

            // Fallback seguro caso gameplayVcam nao esteja atribuida em cenas de teste isoladas
            if (UseScriptedInput) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 pivot = transform.position + Vector3.up * cfg.pivotHeight;
            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 dir = -(rot * Vector3.forward);
            Vector3 desiredPos = pivot + rot * cfg.shoulderOffset + dir * _currentCamDist;

            if (Physics.SphereCast(pivot, cfg.wallCollisionRadius, dir, out RaycastHit hit, _currentCamDist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.root != transform.root)
                    desiredPos = pivot + dir * Mathf.Max(0.75f, hit.distance - 0.12f);
            }

            if (!_camInit) { _camSmoothPos = desiredPos; _camInit = true; }
            else
            {
                float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
                _camSmoothPos = Vector3.Lerp(_camSmoothPos, desiredPos, 1f - Mathf.Exp(-14f * dt));
            }
            cam.transform.position = _camSmoothPos;
            cam.transform.LookAt(pivot);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (SimulateLegacySnagMode || !Cfg.wallSlideEnabled) return;
            if (hit.collider == null || hit.collider.isTrigger) return;

            // Apenas paredes/quinas laterais (normal quase horizontal)
            if (Mathf.Abs(hit.normal.y) > 0.38f) return;

            Vector3 wallN = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (wallN.sqrMagnitude < 0.001f) return;
            wallN.Normalize();
            _lastWallNormal = wallN;
            _lastWallHitTime = Time.time;

            if (_isDashing)
            {
                float dot = Vector3.Dot(_dashDir, wallN);
                if (dot < Cfg.dashWallStopDot)
                {
                    // Dash frontal contra parede: encerra limpo sem ficar preso empurrando!
                    _isDashing = false;
                    _dashTimer = 0f;
                    _horizVelocity = Vector3.zero;
                    _currentSpeed = 0f;
                    WallSlideCount++;
                }
                else
                {
                    // Dash obliquo: projeta ao longo da parede para deslizar com fluidez
                    Vector3 slide = Vector3.ProjectOnPlane(_dashDir, wallN);
                    if (slide.sqrMagnitude > 0.01f)
                    {
                        _dashDir = slide.normalized;
                        _horizVelocity = _dashDir * (Cfg.dashSpeed * 0.85f);
                        WallSlideCount++;
                    }
                }
            }
            else if (_horizVelocity.sqrMagnitude > 0.04f)
            {
                float intoWall = Vector3.Dot(_horizVelocity, wallN);
                if (intoWall < -0.05f)
                {
                    Vector3 projected = Vector3.ProjectOnPlane(_horizVelocity, wallN);
                    // Micro-desvio de quina quando a componente tangente e muito pequena mas ainda existe intencao
                    if (projected.magnitude < 0.35f && Cfg.cornerNudgeStrength > 0f)
                    {
                        Vector3 tangent = Vector3.Cross(Vector3.up, wallN);
                        float side = Vector3.Dot(transform.right, tangent) >= 0f ? 1f : -1f;
                        projected += tangent * (side * Cfg.cornerNudgeStrength);
                    }
                    _horizVelocity = projected;
                    WallSlideCount++;
                }
            }
        }

        private void UpdateSurfaceFootsteps(float dt, bool running)
        {
            if (!_cc.isGrounded || _isDashing || _currentSpeed < 0.45f)
            {
                _stepTimer = 0.08f;
                return;
            }

            _stepTimer -= dt;
            if (_stepTimer > 0f) return;

            var cfg = Cfg;
            _stepTimer = running ? cfg.runStepIntervalSec : cfg.walkStepIntervalSec;

            // Detecta superficie sob os pes usando as layers canonicas de colisao (Default / Environment / Props)
            _lastSurfaceType = "floor";
            int surfaceMask = (1 << CollisionMatrixBootstrap.LayerDefault)
                            | (1 << CollisionMatrixBootstrap.LayerEnvironment)
                            | (1 << CollisionMatrixBootstrap.LayerProps);
            if (Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.75f, surfaceMask, QueryTriggerInteraction.Collide))
            {
                int hitLayer = hit.collider != null ? hit.collider.gameObject.layer : 0;
                string n = hit.collider != null ? hit.collider.name : string.Empty;
                if (ContainsIgnoreCase(n, "puddle") || ContainsIgnoreCase(n, "poca") || ContainsIgnoreCase(n, "water") || ContainsIgnoreCase(n, "oil"))
                    _lastSurfaceType = "puddle";
                else if (ContainsIgnoreCase(n, "metal") || ContainsIgnoreCase(n, "grate") || ContainsIgnoreCase(n, "pipe") || ContainsIgnoreCase(n, "catwalk") || ContainsIgnoreCase(n, "door") || ContainsIgnoreCase(n, "ramp") || hitLayer == CollisionMatrixBootstrap.LayerProps)
                    _lastSurfaceType = "metal";
            }

            AudioClip clip = PickSurfaceFootstepClip(_lastSurfaceType);
            PlaySurfaceSfx(clip, running ? 0.54f : 0.36f, 0.94f, 1.06f);
            if (clip != null) FootstepTriggerCount++;
        }

        private AudioClip PickSurfaceFootstepClip(string surface)
        {
            AudioClip[] pool = surface switch
            {
                "metal"  => footstepMetalClips,
                "puddle" => footstepPuddleClips,
                _        => footstepFloorClips,
            };

            if (pool != null && pool.Length > 0)
            {
                int idx = Random.Range(0, pool.Length);
                if (pool.Length > 1 && idx == _lastFootstepVarIdx)
                    idx = (idx + 1) % pool.Length;
                _lastFootstepVarIdx = idx;
                if (pool[idx] != null) return pool[idx];
            }

            return surface switch
            {
                "metal"  => footstepMetalClip != null ? footstepMetalClip : footstepFloorClip,
                "puddle" => footstepPuddleClip != null ? footstepPuddleClip : footstepFloorClip,
                _        => footstepFloorClip,
            };
        }

        private void PlaySurfaceSfx(AudioClip clip, float vol, float minPitch, float maxPitch)
        {
            if (clip == null) return;
            var src = footstepAudio != null ? footstepAudio : thrusterAudio;
            if (src == null) return;
            src.pitch = Random.Range(minPitch, maxPitch);
            float scale = src.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
            src.PlayOneShot(clip, vol * scale);
        }

        private void UpdateDashFovKick(DezMovementConfig cfg)
        {
            if (gameplayVcam != null && _dashFovKickTimer <= 0f && CurrentFovKickDeg <= 0.001f)
                _baseFov = gameplayVcam.Lens.FieldOfView;
            else if (gameplayVcam == null && Camera.main != null && _dashFovKickTimer <= 0f && CurrentFovKickDeg <= 0.001f)
                _baseFov = Camera.main.fieldOfView;

            if (_dashFovKickTimer > 0f)
            {
                _dashFovKickTimer = Mathf.Max(0f, _dashFovKickTimer - Time.unscaledDeltaTime);
                float dur = Mathf.Max(0.05f, Mathf.Min(0.20f, cfg.dashFovKickDurationSec));
                float u = 1f - (_dashFovKickTimer / dur);
                CurrentFovKickDeg = Mathf.Sin(u * Mathf.PI) * Mathf.Clamp(cfg.dashFovKickDeg, 0f, 4.0f);
            }
            else
            {
                CurrentFovKickDeg = 0f;
            }

            if (gameplayVcam != null)
            {
                var lens = gameplayVcam.Lens;
                lens.FieldOfView = _baseFov + CurrentFovKickDeg;
                gameplayVcam.Lens = lens;
            }
            else if (Camera.main != null)
            {
                Camera.main.fieldOfView = _baseFov + CurrentFovKickDeg;
            }
        }

        private void UpdateCameraProximityPlayerFade(DezMovementConfig cfg)
        {
            var cam = Camera.main;
            if (cam == null || _playerRenderers == null || _playerRenderers.Length == 0) return;
            float dist = Vector3.Distance(cam.transform.position, transform.position + Vector3.up * cfg.pivotHeight);
            bool shouldHide = dist < cfg.playerHideDist;
            if (shouldHide == _playerHiddenByCamera) return;
            _playerHiddenByCamera = shouldHide;
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] != null)
                    _playerRenderers[i].enabled = !shouldHide;
            }
        }

        // === LEDGE GRAB / MANTLE (Preservado integralmente) ===
        private bool TryStartLedgeMantle(Vector3 probeDir)
        {
            var cfg = Cfg;
            probeDir.y = 0f;
            if (probeDir.sqrMagnitude < 0.01f) probeDir = transform.forward;
            probeDir.Normalize();

            bool hasWall =
                SphereCastIgnoreSelf(transform.position + Vector3.up * 0.70f, 0.16f, probeDir, cfg.ledgeDetectDist, out RaycastHit wallHit) ||
                SphereCastIgnoreSelf(transform.position + Vector3.up * 1.08f, 0.16f, probeDir, cfg.ledgeDetectDist, out wallHit);

            if (!hasWall || Mathf.Abs(wallHit.normal.y) > 0.35f) return false;
            Vector3 wallNormal = new Vector3(wallHit.normal.x, 0f, wallHit.normal.z).normalized;
            if (wallNormal.sqrMagnitude < 0.01f) return false;

            float probeTopY = transform.position.y + cfg.ledgeMaxHeight + 0.18f;
            Vector3 topProbeOrigin = new Vector3(wallHit.point.x, probeTopY, wallHit.point.z) - wallNormal * 0.28f;
            float downDist = (cfg.ledgeMaxHeight - cfg.ledgeMinHeight) + 0.30f;

            if (!RaycastIgnoreSelf(topProbeOrigin, Vector3.down, downDist, out RaycastHit topHit)) return false;
            if (topHit.normal.y < 0.70f) return false;

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < cfg.ledgeMinHeight || ledgeHeight > cfg.ledgeMaxHeight) return false;

            Vector3 standPos = new Vector3(topProbeOrigin.x, topHit.point.y + 0.02f, topProbeOrigin.z);
            Vector3 capBottom = standPos + Vector3.up * (_cc.radius + 0.06f);
            Vector3 capTop = standPos + Vector3.up * Mathf.Max(_cc.radius + 0.12f, _cc.height - _cc.radius - 0.04f);
            var overlaps = Physics.OverlapCapsule(capBottom, capTop, _cc.radius * 0.85f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i].transform.root != transform.root)
                    return false;
            }

            _isMantling = true;
            _mantleTimer = 0f;
            _mantleStartPos = transform.position;
            _mantleMidPos = new Vector3(wallHit.point.x, topHit.point.y + 0.02f, wallHit.point.z) + wallNormal * (_cc.radius * 0.65f);
            _mantleEndPos = standPos;
            _mantleStartRot = transform.rotation;
            _mantleEndRot = Quaternion.LookRotation(-wallNormal, Vector3.up);

            _verticalVel = 0f;
            _horizVelocity = Vector3.zero;
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
            var cfg = Cfg;
            _mantleTimer += dt;
            float t = Mathf.Clamp01(_mantleTimer / Mathf.Max(0.15f, cfg.mantleDuration));

            Vector3 pos = (t < 0.55f)
                ? Vector3.Lerp(_mantleStartPos, _mantleMidPos, Mathf.SmoothStep(0f, 1f, t / 0.55f))
                : Vector3.Lerp(_mantleMidPos, _mantleEndPos, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));

            transform.position = pos;
            transform.rotation = Quaternion.Slerp(_mantleStartRot, _mantleEndRot, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.2f)));

            if (_proc != null) _proc.SetMantleState(true, t);

            if (t >= 1f)
            {
                _isMantling = false;
                _mantleCooldownUntil = Time.time + cfg.mantleCooldown;
                transform.position = _mantleEndPos;
                _verticalVel = -1.0f;
                _wasGrounded = true;
                _cc.enabled = true;

                if (_anim != null) { _anim.SetBool("IsGrounded", true); _anim.SetFloat("VerticalVel", 0f); }
                if (_proc != null) { _proc.SetMantleState(false, 0f); _proc.NotifyLanding(-3.2f); }
            }
        }

        private readonly RaycastHit[] _probeBuf = new RaycastHit[12];

        private static bool ContainsIgnoreCase(string source, string token)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool SphereCastIgnoreSelf(Vector3 origin, float radius, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            int count = Physics.SphereCastNonAlloc(origin, radius, dir, _probeBuf, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_probeBuf[i].transform == null || _probeBuf[i].transform.root == transform.root) continue;
                if (_probeBuf[i].distance <= 0.001f) continue;
                if (_probeBuf[i].distance < bestDist) { bestDist = _probeBuf[i].distance; bestHit = _probeBuf[i]; found = true; }
            }
            return found;
        }

        private bool RaycastIgnoreSelf(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            int count = Physics.RaycastNonAlloc(origin, dir, _probeBuf, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_probeBuf[i].transform == null || _probeBuf[i].transform.root == transform.root) continue;
                if (_probeBuf[i].distance < bestDist) { bestDist = _probeBuf[i].distance; bestHit = _probeBuf[i]; found = true; }
            }
            return found;
        }
    }
}
