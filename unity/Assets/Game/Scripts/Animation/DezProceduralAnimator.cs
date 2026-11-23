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
        public float landingDipStrength = 0.026f;
        public float landingMaxDip = 0.16f;
        public float landingSpringK = 180f;
        public float landingDamping = 20f;

        [Header("Idle Vivo, Corrida, Passada & Maos")]
        public float breathCycleSec = 3.8f;
        public float breathChestDeg = 2.8f;
        public float maxBankDeg = 12f;
        public float runForwardLeanDeg = 8.5f;
        public float idleWeightSwayMeters = 0.012f;
        [Tooltip("Fecha as coxas andando/correndo em personagens estilizados/chibi (evita pernas abertas demais vistas por tras).")]
        public float legAdductionDeg = 14.0f;
        [Tooltip("Movimento dinamico e relaxamento natural dos pulsos/maos.")]
        public float handDynamicAmpDeg = 11.0f;

        [Header("Sombra de Contato (Peso & Gravidade)")]
        public bool enableContactShadow = true;
        public float shadowBaseRadius = 0.34f;

        [Header("Look-At")]
        public Transform lookTarget;
        [Range(0f, 1f)] public float lookWeight = 0.55f;

        private Animator _anim;
        private float _dipOffset;
        private float _dipVel;
        private float _currentBank;
        private float _targetBank;
        private float _currentLean;
        private float _targetLean;
        private float _currentSpeed;
        private float _currentTurnRate;
        private bool _isGrounded = true;
        private Transform _shadowQuad;
        private Material _shadowMat;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            if (enableContactShadow) CreateContactShadow();
        }

        private void OnDestroy()
        {
            if (_shadowQuad != null) Destroy(_shadowQuad.gameObject);
            if (_shadowMat != null) Destroy(_shadowMat);
        }

        private void CreateContactShadow()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Dez_ContactShadow";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * (shadowBaseRadius * 2f);

            // Textura radial Gaussian suave em runtime (zero dependencias externas)
            int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float nx = (x + 0.5f) / sz * 2f - 1f;
                    float ny = (y + 0.5f) / sz * 2f - 1f;
                    float r2 = nx * nx + ny * ny;
                    float a = Mathf.Exp(-r2 * 4.2f) * 0.58f;
                    if (r2 > 1f) a = 0f;
                    tex.SetPixel(x, y, new Color(0.02f, 0.015f, 0.01f, a));
                }
            }
            tex.Apply();

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            _shadowMat = new Material(sh);
            _shadowMat.mainTexture = tex;
            if (_shadowMat.HasProperty("_BaseMap")) _shadowMat.SetTexture("_BaseMap", tex);
            if (_shadowMat.HasProperty("_BaseColor")) _shadowMat.SetColor("_BaseColor", Color.white);
            if (_shadowMat.HasProperty("_Surface")) _shadowMat.SetFloat("_Surface", 1f); // Transparent
            _shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _shadowMat.SetInt("_ZWrite", 0);
            _shadowMat.renderQueue = 3000;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _shadowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _shadowQuad = go.transform;
        }

        public void SetLocomotionState(bool isGrounded, float speed, float turnRateDegPerSec)
        {
            _isGrounded = isGrounded;
            _currentSpeed = speed;
            _currentTurnRate = turnRateDegPerSec;
            float speedFactor = Mathf.Clamp01(speed / 3.2f);
            _targetBank = Mathf.Clamp(-turnRateDegPerSec * 0.09f * speedFactor, -maxBankDeg, maxBankDeg);
            // Inclinacao atletica para frente ao correr
            _targetLean = Mathf.Clamp01((speed - 1.2f) / 2.2f) * runForwardLeanDeg;
        }

        public void NotifyLanding(float verticalImpactSpeed)
        {
            float impulse = Mathf.Clamp(Mathf.Abs(verticalImpactSpeed) * landingDipStrength, 0.03f, landingMaxDip);
            _dipVel -= impulse * 18f;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            // Mola critica do Landing Dip
            float force = -_dipOffset * landingSpringK - _dipVel * landingDamping;
            _dipVel += force * dt;
            _dipOffset = Mathf.Clamp(_dipOffset + _dipVel * dt, -landingMaxDip, 0.03f);

            // Banking suave nas curvas + inclinacao atletica de corrida
            _currentBank = Mathf.Lerp(_currentBank, _targetBank, 1f - Mathf.Exp(-8f * dt));
            _currentLean = Mathf.Lerp(_currentLean, _targetLean, 1f - Mathf.Exp(-7f * dt));
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isHuman) return;

            // 1. Respiracao continua + inclinacao atletica + banking EM ESPACO DO PERSONAGEM (zero torcao diagonal!)
            float phase = (Time.time / Mathf.Max(0.5f, breathCycleSec)) * Mathf.PI * 2f;
            float breath = Mathf.Sin(phase);
            Vector3 charRight = transform.right;
            Vector3 charFwd = transform.forward;
            Vector3 charUp = transform.up;

            Transform chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
            {
                Quaternion qPitch = Quaternion.AngleAxis(breath * breathChestDeg + _currentLean * 0.55f, charRight);
                Quaternion qBank  = Quaternion.AngleAxis(_currentBank * 0.5f, charFwd);
                chest.rotation = qBank * qPitch * chest.rotation;
            }
            Transform spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null && (Mathf.Abs(_currentBank) > 0.05f || _currentLean > 0.05f))
            {
                Quaternion qPitch = Quaternion.AngleAxis(_currentLean * 0.45f, charRight);
                Quaternion qBank  = Quaternion.AngleAxis(_currentBank * 0.5f, charFwd);
                spine.rotation = qBank * qPitch * spine.rotation;
            }
            // Respiracao sutil nos ombros em repouso
            Transform shL = _anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            Transform shR = _anim.GetBoneTransform(HumanBodyBones.RightShoulder);
            if (shL != null) shL.rotation = Quaternion.AngleAxis(breath * 1.1f, charFwd) * shL.rotation;
            if (shR != null) shR.rotation = Quaternion.AngleAxis(-breath * 1.1f, charFwd) * shR.rotation;

            // 1B. Aducao suave das coxas ao andar/correr (passada fechada e agil sob o quadril vista por tras!)
            // Prova Unity left-handed (+Z frente, -Y perna para baixo):
            //   +deg em torno de +Z move (0,-1,0) para (+sin, -cos, 0) = DIREITA (+X) -> FECHA a perna ESQUERDA!
            //   -deg em torno de +Z move (0,-1,0) para (-sin, -cos, 0) = ESQUERDA (-X) -> FECHA a perna DIREITA!
            if (_isGrounded && _currentSpeed > 0.12f && legAdductionDeg > 0.1f)
            {
                float moveW = Mathf.Clamp01(_currentSpeed / 1.25f);
                float addDeg = legAdductionDeg * moveW;
                Transform lUp = _anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform rUp = _anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform lLow = _anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform rLow = _anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                if (lUp != null) lUp.rotation = Quaternion.AngleAxis( addDeg, charFwd) * lUp.rotation;
                if (rUp != null) rUp.rotation = Quaternion.AngleAxis(-addDeg, charFwd) * rUp.rotation;
                // Compensa 60% na canela para manter joelhos alinhados e botas 100% paralelas no piso
                if (lLow != null) lLow.rotation = Quaternion.AngleAxis(-addDeg * 0.60f, charFwd) * lLow.rotation;
                if (rLow != null) rLow.rotation = Quaternion.AngleAxis( addDeg * 0.60f, charFwd) * rLow.rotation;
            }

            // 1C. Movimento dinamico natural dos pulsos/maos (relaxamento em idle + follow-through elastico andando/correndo)
            Transform handL = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform handR = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (handL != null || handR != null)
            {
                float moveFactor = Mathf.Clamp01(_currentSpeed / 2.2f);
                float strideFreq = Mathf.Lerp(1.6f, 9.5f, moveFactor);
                float swing = Mathf.Sin(Time.time * strideFreq) * handDynamicAmpDeg * (0.28f + 0.72f * moveFactor);
                float relaxPitch = _isGrounded ? (7.5f + breath * 2.0f) : -12.0f; // relaxa no chao, abre leve no pulo
                float inwardCup = _isGrounded ? 8.0f : -6.0f;

                if (handL != null)
                {
                    Quaternion qH = Quaternion.AngleAxis(relaxPitch + swing, charRight)
                                  * Quaternion.AngleAxis(-inwardCup, charUp);
                    handL.rotation = qH * handL.rotation;
                }
                if (handR != null)
                {
                    Quaternion qH = Quaternion.AngleAxis(relaxPitch - swing, charRight)
                                  * Quaternion.AngleAxis(inwardCup, charUp);
                    handR.rotation = qH * handR.rotation;
                }
            }

            // 2. Micro-sway organico de peso no quadril em Idle + Absorcao de queda (Landing Dip)
            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                float idleFactor = _isGrounded ? Mathf.Clamp01(1f - _currentSpeed * 2f) : 0f;
                float swayX = Mathf.Sin(Time.time * 1.15f) * idleWeightSwayMeters * idleFactor;
                Vector3 offset = transform.right * swayX + new Vector3(0f, _dipOffset, 0f);
                if (offset.sqrMagnitude > 1e-8f)
                    hips.position += offset;
            }

            // 3. Atualiza Sombra de Contato no piso (diminui e suaviza proporcionalmente a altura do pulo!)
            if (_shadowQuad != null)
            {
                Vector3 origin = transform.position + Vector3.up * 0.6f;
                var hits = Physics.RaycastAll(origin, Vector3.down, 4.0f, groundMask, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                RaycastHit groundHit = default;
                bool hasGround = false;
                Transform selfRoot = transform.root;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].transform.root == selfRoot) continue;
                    if (hits[i].distance < bestDist)
                    {
                        bestDist = hits[i].distance;
                        groundHit = hits[i];
                        hasGround = true;
                    }
                }
                if (hasGround)
                {
                    float heightAboveFloor = Mathf.Max(0f, transform.position.y - groundHit.point.y);
                    float scaleFactor = Mathf.Clamp(1f - heightAboveFloor * 0.28f, 0.42f, 1.0f);
                    float alphaFactor = Mathf.Clamp(1f - heightAboveFloor * 0.45f, 0.18f, 1.0f);
                    _shadowQuad.position = groundHit.point + groundHit.normal * 0.015f;
                    _shadowQuad.rotation = Quaternion.FromToRotation(Vector3.up, groundHit.normal) * Quaternion.Euler(90f, 0f, 0f);
                    _shadowQuad.localScale = Vector3.one * (shadowBaseRadius * 2f * scaleFactor);
                    if (_shadowMat != null && _shadowMat.HasProperty("_BaseColor"))
                        _shadowMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, alphaFactor));
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
                // Olhar natural levemente vivo + Head Look-Ahead na direcao do giro (efeito AAA!)
                float sway = Mathf.Sin(Time.time * 0.9f) * 0.30f;
                float turnLook = Mathf.Clamp(_currentTurnRate / 120f, -1f, 1f) * 2.2f;
                Vector3 fwdTarget = transform.position + transform.forward * 4f + transform.right * (sway + turnLook) + Vector3.up * 1.10f;
                float wLook = Mathf.Abs(_currentTurnRate) > 10f ? 0.65f : 0.35f;
                _anim.SetLookAtWeight(wLook, 0.12f, 0.65f, 0.75f, 0.5f);
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
                float slopeAngle = Vector3.Angle(Vector3.up, bestHit.normal);
                // Em piso plano andando/correndo, reduz IK para ZERO para nunca torcer o tornozelo do ciclo profissional;
                // em Idle ou em rampas (slope >= 3 deg), aplica IK completo para plantar os pes no relevo!
                float flatMoveAtten = (slopeAngle < 3.0f)
                    ? Mathf.Lerp(0.85f, 0.0f, Mathf.Clamp01(_currentSpeed / 0.6f))
                    : 1.0f;
                float heightAboveGround = Mathf.Max(0f, footPos.y - targetPos.y);
                float w = Mathf.Clamp01(1f - (heightAboveGround / 0.24f)) * flatMoveAtten;
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, bestHit.normal) * _anim.GetIKRotation(goal);
                _anim.SetIKPositionWeight(goal, w);
                _anim.SetIKRotationWeight(goal, slopeAngle >= 3.0f ? w * 0.85f : 0f);
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
