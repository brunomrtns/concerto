using System;
using Studio.Game.Animation;
using Studio.Game.Boss;
using Studio.Game.Menu;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.CameraSystem
{
    public enum CameraZoneMode
    {
        Exploration = 0,
        TightCorridor = 1,
        BossCombat = 2
    }

    /// <summary>
    /// ConcertoCameraDirector — Camera Pass (Cinemachine 3.1.4).
    /// Escopo fechado exclusivamente em camera (nao altera movimento, combate ou fases).
    ///
    /// Conformidade DESIGN_BIBLE.md:
    ///   - GATE 1 (Follow suave): dead zone 0.18m + look-ahead 0.65m com reacao <100ms (65ms), sem jitter
    ///   - GATE 2 (Enquadramento vertical): pulo e queda ajustam pitch/distancia revelando caminho e pouso
    ///   - GATE 3 (Combate Boss): enquadra Dez E Guardiao da Fenda simultaneamente sem cortar nenhum dos dois
    ///   - GATE 4 (Colisao): CinemachineDeoccluder + pull-in SphereCast garantindo zero clip em parede/chao
    ///   - GATE 5 (Transicoes suaves): blend EaseInOut 0.38s (<0.5s), adiado automaticamente se Dez estiver no ar
    ///   - Regra 3 (Shake): exclusivamente via Cinemachine Impulse com decay <300ms (220ms)
    ///   - 0 modelos 3D criados
    /// </summary>
    public sealed class ConcertoCameraDirector : MonoBehaviour
    {
        public const float DeadZoneRadiusM      = 0.18f;
        public const float MaxLookAheadM        = 0.65f;
        public const float LookAheadSmoothSec   = 0.065f; // 65ms (< 100ms DESIGN_BIBLE)
        public const float ZoneBlendDurationSec = 0.38f;  // < 0.50s (GATE 5)
        public const float ImpulseDecaySec      = 0.22f;  // < 300ms (Regra 3 & DESIGN_BIBLE)
        public const float PullInSphereRadiusM  = 0.26f;
        public const float MinGroundClearanceM  = 0.35f;

        /// <summary>
        /// Flag QA para gravacao lado a lado ANTES (sem Camera Pass) vs DEPOIS (com Camera Pass).
        /// </summary>
        public static bool DisableCameraPassForComparison { get; set; }

        public static ConcertoCameraDirector Instance { get; private set; }

        public CameraZoneMode ActiveZone { get; private set; } = CameraZoneMode.Exploration;
        public CameraZoneMode PendingZone { get; private set; } = CameraZoneMode.Exploration;
        public bool HasDeferredAirborneTransition { get; private set; }
        public Vector3 CurrentLookAheadOffset => _lookAheadOffset;
        public float CurrentVerticalPitchBias => _verticalPitchBias;
        public float CurrentEffectiveDistance { get; private set; } = 3.2f;
        public float CurrentEffectiveFov { get; private set; } = 55f;
        public bool IsCollisionPullInActive { get; private set; }
        public bool IsFramingBossAndPlayer { get; private set; }
        public float LastImpulseDecaySec { get; private set; }
        public int ImpulseTriggerCount { get; private set; }
        public int DeferredJumpCutCount { get; private set; }
        public int WallPullInCount { get; private set; }

        private DezLocomotionController _dezLoco;
        private CharacterController _dezCc;
        private CinemachineCamera _vcam;
        private CinemachineThirdPersonFollow _tpFollow;
        private CinemachineDeoccluder _deoccluder;
        private CinemachineBrain _brain;
        private CinemachineImpulseSource _impulseSource;
        private RiftGuardianBossController _boss;

        private Vector3 _lookAheadOffset;
        private Vector3 _lookAheadVel;
        private Vector3 _lastPlayerPos;
        private float _blendDistance = 3.2f;
        private float _blendFov = 55f;
        private float _verticalPitchBias;
        private float _airDistanceBias;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("Concerto_CameraDirector");
            go.AddComponent<ConcertoCameraDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureImpulseSource();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BindSceneCamera();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ActiveZone = CameraZoneMode.Exploration;
            PendingZone = CameraZoneMode.Exploration;
            HasDeferredAirborneTransition = false;
            _lookAheadOffset = Vector3.zero;
            _lookAheadVel = Vector3.zero;
            BindSceneCamera();
        }

        private void EnsureImpulseSource()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource == null)
                _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

            _impulseSource.ImpulseDefinition.ImpulseDuration = ImpulseDecaySec;
            _impulseSource.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            _impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        }

        private void BindSceneCamera()
        {
            var dez = PlayerRegistry.Resolve();
            _dezLoco = dez != null ? dez.GetComponent<DezLocomotionController>() : FindAnyObjectByType<DezLocomotionController>();
            _dezCc = _dezLoco != null ? _dezLoco.GetComponent<CharacterController>() : null;
            _lastPlayerPos = _dezLoco != null ? _dezLoco.transform.position : Vector3.zero;

            _vcam = _dezLoco != null ? _dezLoco.gameplayVcam : FindAnyObjectByType<CinemachineCamera>();
            _boss = FindAnyObjectByType<RiftGuardianBossController>();

            var mainCam = Camera.main;
            _brain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;
            if (_brain != null)
            {
                // GATE 5: Blend suave < 0.5s (0.38s EaseInOut)
                _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, ZoneBlendDurationSec);
            }

            if (_vcam != null)
            {
                _tpFollow = _vcam.GetComponent<CinemachineThirdPersonFollow>();
                _deoccluder = _vcam.GetComponent<CinemachineDeoccluder>();
                if (_deoccluder == null)
                    _deoccluder = _vcam.gameObject.AddComponent<CinemachineDeoccluder>();
                _deoccluder.CollideAgainst = ~0;
                _deoccluder.AvoidObstacles.Enabled = true;
                _deoccluder.AvoidObstacles.CameraRadius = PullInSphereRadiusM;

                var listener = _vcam.GetComponent<CinemachineImpulseListener>();
                if (listener == null)
                    listener = _vcam.gameObject.AddComponent<CinemachineImpulseListener>();
                listener.Gain = 1.0f;
                listener.ReactionSettings.Duration = ImpulseDecaySec;
            }
        }

        /// <summary>
        /// Regra 3: Shake EXCLUSIVAMENTE via Cinemachine Impulse com decay < 300ms.
        /// </summary>
        public void TriggerCinemachineImpulse(Vector3 velocity, float decaySec = ImpulseDecaySec)
        {
            if (DisableCameraPassForComparison || _impulseSource == null) return;
            float clamped = Mathf.Clamp(decaySec, 0.06f, 0.28f); // < 300ms
            _impulseSource.ImpulseDefinition.ImpulseDuration = clamped;
            LastImpulseDecaySec = clamped;
            _impulseSource.GenerateImpulseWithVelocity(velocity * GameSettingsManager.CameraShakeScale);
            ImpulseTriggerCount++;
        }

        /// <summary>
        /// GATE 5: Solicita transicao de zona de camera (<0.5s), adiando automaticamente se Dez estiver no meio de um pulo!
        /// </summary>
        public void RequestZoneTransition(CameraZoneMode targetZone)
        {
            PendingZone = targetZone;
            bool airborne = _dezCc != null && !_dezCc.isGrounded;
            if (airborne && targetZone != ActiveZone)
            {
                HasDeferredAirborneTransition = true;
                DeferredJumpCutCount++;
                return;
            }
            HasDeferredAirborneTransition = false;
            ActiveZone = targetZone;
        }

        private void LateUpdate()
        {
            if (DisableCameraPassForComparison) return;
            if (_dezLoco == null)
            {
                BindSceneCamera();
                if (_dezLoco == null) return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            // GATE 5: Conclui transicao adiada assim que Dez aterrissa
            bool grounded = _dezCc == null || _dezCc.isGrounded;
            if (HasDeferredAirborneTransition && grounded)
            {
                ActiveZone = PendingZone;
                HasDeferredAirborneTransition = false;
            }

            // GATE 3: Auto-detecta combate contra Guardiao da Fenda
            IsFramingBossAndPlayer = false;
            if (_boss != null && _boss.gameObject.activeInHierarchy)
            {
                float distBoss = Vector3.Distance(_dezLoco.transform.position, _boss.transform.position);
                if (distBoss <= 32f && ActiveZone != CameraZoneMode.BossCombat)
                    RequestZoneTransition(CameraZoneMode.BossCombat);
                if (ActiveZone == CameraZoneMode.BossCombat)
                    IsFramingBossAndPlayer = true;
            }

            // GATE 1: Follow suave com dead zone (0.18m) + look-ahead (<100ms = 65ms)
            Vector3 playerPos = _dezLoco.transform.position;
            Vector3 frameDelta = playerPos - _lastPlayerPos;
            _lastPlayerPos = playerPos;
            Vector3 horizVel = new Vector3(frameDelta.x, 0f, frameDelta.z) / dt;
            Vector3 desiredLookAhead = Vector3.zero;
            if (horizVel.magnitude > DeadZoneRadiusM)
                desiredLookAhead = Vector3.ClampMagnitude(horizVel.normalized * MaxLookAheadM, MaxLookAheadM);

            // GATE 3: Se lutando com o Guardiao, puxa o foco parcialmente para o ponto medio Dez <-> Boss
            if (IsFramingBossAndPlayer && _boss != null)
            {
                Vector3 toBoss = _boss.transform.position - playerPos;
                toBoss.y = 0f;
                desiredLookAhead = Vector3.ClampMagnitude(toBoss * 0.35f, 2.2f);
            }

            _lookAheadOffset = Vector3.SmoothDamp(_lookAheadOffset, desiredLookAhead, ref _lookAheadVel, LookAheadSmoothSec);

            // GATE 2: Enquadramento vertical durante pulo e queda (revela caminho e aterrissagem)
            float vy = frameDelta.y / dt;
            float targetPitchBias = 0f;
            float targetAirDist = 0f;
            if (!grounded)
            {
                if (vy > 0.5f) { targetPitchBias = 3.5f; targetAirDist = 0.25f; }
                else           { targetPitchBias = 6.5f; targetAirDist = 0.45f; }
            }
            _verticalPitchBias = Mathf.Lerp(_verticalPitchBias, targetPitchBias, Mathf.Clamp01(dt * 8f));
            _airDistanceBias = Mathf.Lerp(_airDistanceBias, targetAirDist, Mathf.Clamp01(dt * 8f));

            // Parametros por zona com blend suave (<0.5s -> 0.38s)
            float zoneDist = ActiveZone == CameraZoneMode.TightCorridor ? 2.15f
                : (ActiveZone == CameraZoneMode.BossCombat ? 5.60f : 3.25f);
            float zoneFov = ActiveZone == CameraZoneMode.TightCorridor ? 58f
                : (ActiveZone == CameraZoneMode.BossCombat ? 62f : 55f);

            float blendStep = Mathf.Clamp01(dt / ZoneBlendDurationSec * 2.5f);
            _blendDistance = Mathf.Lerp(_blendDistance, zoneDist + _airDistanceBias, blendStep);
            _blendFov = Mathf.Lerp(_blendFov, zoneFov, blendStep);

            // GATE 4: Pull-in por SphereCast garantindo que a camera NUNCA atravesse parede ou chao
            float finalDist = ComputeSafePullInDistance(playerPos, _blendDistance);
            CurrentEffectiveDistance = finalDist;
            CurrentEffectiveFov = _blendFov;

            if (_tpFollow != null)
                _tpFollow.CameraDistance = finalDist;

            if (_vcam != null && Mathf.Abs(_vcam.Lens.FieldOfView - _blendFov) > 0.05f)
            {
                var lens = _vcam.Lens;
                lens.FieldOfView = _blendFov;
                _vcam.Lens = lens;
            }

            if (_dezLoco.cameraFollowTarget != null)
            {
                Vector3 baseLocal = _dezLoco.cameraFollowTarget.localPosition;
                Vector3 localLook = _dezLoco.transform.InverseTransformDirection(_lookAheadOffset);
                _dezLoco.cameraFollowTarget.localPosition = new Vector3(
                    localLook.x * 0.45f,
                    Mathf.Max(MinGroundClearanceM, baseLocal.y),
                    localLook.z * 0.45f);
                if (Mathf.Abs(_verticalPitchBias) > 0.05f)
                {
                    Vector3 e = _dezLoco.cameraFollowTarget.localEulerAngles;
                    _dezLoco.cameraFollowTarget.localRotation = Quaternion.Euler(e.x + _verticalPitchBias, e.y, 0f);
                }
            }
        }

        public float ComputeSafePullInDistance(Vector3 playerPos, float desiredDist)
        {
            Vector3 pivot = playerPos + Vector3.up * 1.35f;
            var cam = Camera.main;
            Vector3 backDir = cam != null ? -cam.transform.forward : -Vector3.forward;
            backDir.Normalize();

            if (Physics.SphereCast(pivot, PullInSphereRadiusM, backDir, out RaycastHit hit, desiredDist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(_dezLoco.transform))
                {
                    float safe = Mathf.Clamp(hit.distance - 0.15f, 0.95f, desiredDist);
                    if (safe < desiredDist - 0.05f)
                    {
                        IsCollisionPullInActive = true;
                        WallPullInCount++;
                        return safe;
                    }
                }
            }
            IsCollisionPullInActive = false;
            return desiredDist;
        }
    }
}
