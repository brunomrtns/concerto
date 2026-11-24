using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// DezProceduralAnimator (Clean AAA) — Apenas camadas nao-conflitantes:
    ///   1. Sombra de contato dinamica no piso (nao toca em nenhum osso)
    ///   2. Landing Dip elastico no Hips somente ao pousar de um pulo
    ///   3. Respiracao sutil no Chest SOMENTE quando parado (Idle)
    /// ZERO rotacao de tronco/cabeca na corrida, ZERO LookAt forcado, ZERO IK/edicao manual em pernas, pes ou maos.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class DezProceduralAnimator : MonoBehaviour
    {
        [Header("Locomocao — Bracos Afastados & Balanco Acoplado a Passada (DESIGN_BIBLE Secao 2)")]
        [Tooltip("Abertura lateral dos bracos afastando do torso em Idle/Walk/Run (graus).")]
        public float armAbductionIdleDeg = 10f;
        public float armAbductionWalkDeg = 18f;
        public float armAbductionRunDeg = 14f;
        [Tooltip("Amplitude de balanco frente-tras dos bracos na caminhada e corrida (graus).")]
        public float armSwingWalkDeg = 28f;
        public float armSwingRunDeg = 52f;
        [Tooltip("Dobra natural do antebraco durante caminhada e corrida (graus).")]
        public float forearmBendWalkDeg = 16f;
        public float forearmBendRunDeg = 54f;
        [Tooltip("Multiplicador do balanco do braco direito quando segura a ferramenta (regra canonica).")]
        [Range(0.1f, 1.0f)] public float rightArmToolSwingFactor = 0.35f;

        [Header("Corrida com Vida — Inclinacao de Tronco, Ombros & Bob Sutil")]
        [Tooltip("Inclinacao frontal do tronco na corrida (spec: 5-10 graus).")]
        [Range(0f, 14f)] public float runForwardLeanDeg = 8.5f;
        public float walkForwardLeanDeg = 2.5f;
        [Tooltip("Contra-rotacao dos ombros/peito em fase com a passada (graus).")]
        public float shoulderTwistWalkDeg = 4.5f;
        public float shoulderTwistRunDeg = 9.0f;
        [Tooltip("Bob vertical sutil no Hips durante a corrida (m).")]
        public float runVerticalBobMeters = 0.028f;

        [Header("Landing Dip (Absorcao de Impacto no Pouso)")]
        public float landingDipStrength = 0.022f;
        public float landingMaxDip = 0.14f;
        public float landingSpringK = 180f;
        public float landingDamping = 20f;

        [Header("Respiracao Sutil (Somente em Idle Parado)")]
        public float breathCycleSec = 3.8f;
        public float breathChestDeg = 1.8f;

        [Header("Sombra de Contato (Peso & Gravidade)")]
        public bool enableContactShadow = true;
        public float shadowBaseRadius = 0.34f;
        public LayerMask groundMask = ~0;

        // Flag QA para gravacao ANTES vs DEPOIS na mesma rota/angulo
        public bool SimulateLegacyAnimMode { get; set; }
        public bool IsRightHandHoldingTool { get; set; }
        public float LastAppliedArmSwingDeg { get; private set; }
        public float LastAppliedTrunkLeanDeg { get; private set; }

        private Animator _anim;
        private float _dipOffset;
        private float _dipVel;
        private float _currentSpeed;
        private bool _isGrounded = true;
        private bool _isMantling;
        private float _mantleT;
        private Transform _shadowQuad;
        private Material _shadowMat;
        private readonly RaycastHit[] _shadowHitBuf = new RaycastHit[8];

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
            if (_shadowMat.HasProperty("_Surface")) _shadowMat.SetFloat("_Surface", 1f);
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
        }

        public void SetMantleState(bool isMantling, float normalizedTime)
        {
            _isMantling = isMantling;
            _mantleT = Mathf.Clamp01(normalizedTime);
        }

        public void NotifyLanding(float verticalImpactSpeed)
        {
            float impulse = Mathf.Clamp(Mathf.Abs(verticalImpactSpeed) * landingDipStrength, 0.03f, landingMaxDip);
            _dipVel -= impulse * 18f;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            float force = -_dipOffset * landingSpringK - _dipVel * landingDamping;
            _dipVel += force * dt;
            _dipOffset = Mathf.Clamp(_dipOffset + _dipVel * dt, -landingMaxDip, 0.02f);
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isHuman) return;

            // 1. Pose de escalada de borda (SOMENTE durante Ledge Grab / Mantle — zero efeito andando/correndo!)
            if (_isMantling)
            {
                // Fase 1 (0..0.55): estende ambos os bracos para frente/cima segurando a borda; Fase 2 (0.55..1): empurra pra baixo subindo
                float weight = Mathf.Sin(_mantleT * Mathf.PI);
                float armPitchDeg = Mathf.Lerp(-115f, -35f, _mantleT) * weight;
                Transform lArm = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rArm = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform chestM = _anim.GetBoneTransform(HumanBodyBones.Chest);
                if (lArm != null) lArm.rotation = Quaternion.AngleAxis(armPitchDeg, transform.right) * lArm.rotation;
                if (rArm != null) rArm.rotation = Quaternion.AngleAxis(armPitchDeg, transform.right) * rArm.rotation;
                if (chestM != null) chestM.rotation = Quaternion.AngleAxis(18f * weight, transform.right) * chestM.rotation;
            }
            // 2. Respiracao sutil no Chest SOMENTE quando parado no chao
            else if (_isGrounded && _currentSpeed < 0.08f)
            {
                float phase = (Time.time / Mathf.Max(0.5f, breathCycleSec)) * Mathf.PI * 2f;
                float breath = Mathf.Sin(phase);
                Transform chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null)
                    chest.rotation = Quaternion.AngleAxis(breath * breathChestDeg, transform.right) * chest.rotation;

                // Em Idle, mantem bracos levemente afastados do torso (sem colar nas costelas)
                if (!SimulateLegacyAnimMode)
                {
                    Transform lArm = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    Transform rArm = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    if (lArm != null) lArm.rotation = Quaternion.AngleAxis(armAbductionIdleDeg, transform.forward) * lArm.rotation;
                    if (rArm != null) rArm.rotation = Quaternion.AngleAxis(-armAbductionIdleDeg, transform.forward) * rArm.rotation;
                }
                LastAppliedArmSwingDeg = 0f;
                LastAppliedTrunkLeanDeg = 0f;
            }
            // 2B. Camada aditiva de locomocao (Walk/Run): bracos afastados do torso + balanco acoplado a passada + inclinacao de corrida!
            else if (_isGrounded && !SimulateLegacyAnimMode && _currentSpeed >= 0.08f)
            {
                float walkWeight = Mathf.Clamp01(_currentSpeed / 2.2f);
                float runWeight  = Mathf.Clamp01((_currentSpeed - 2.2f) / 3.2f);

                // Fase acoplada ao ciclo real do Animator (zero dessincronia com as pernas!)
                var st = _anim.GetCurrentAnimatorStateInfo(0);
                float stridePhase = (st.normalizedTime % 1f) * Mathf.PI * 2f;
                float swingSin = Mathf.Sin(stridePhase);
                float bobWave  = Mathf.Abs(Mathf.Sin(stridePhase));

                float abdDeg   = Mathf.Lerp(armAbductionWalkDeg, armAbductionRunDeg, runWeight) * walkWeight;
                float swingAmp = Mathf.Lerp(armSwingWalkDeg, armSwingRunDeg, runWeight) * walkWeight;
                float bendDeg  = Mathf.Lerp(forearmBendWalkDeg, forearmBendRunDeg, runWeight) * walkWeight;
                float leanDeg  = Mathf.Lerp(walkForwardLeanDeg, runForwardLeanDeg, runWeight) * walkWeight;
                float twistDeg = Mathf.Lerp(shoulderTwistWalkDeg, shoulderTwistRunDeg, runWeight) * walkWeight * swingSin;

                Transform spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
                Transform chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
                Transform lArm  = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rArm  = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform lFore = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Transform rFore = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform hips  = _anim.GetBoneTransform(HumanBodyBones.Hips);

                // Inclinacao de tronco (5-10 deg na corrida) + contra-rotacao de ombros
                if (spine != null) spine.rotation = Quaternion.AngleAxis(leanDeg * 0.55f, transform.right) * spine.rotation;
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(leanDeg * 0.45f, transform.right) * chest.rotation;
                    chest.rotation = Quaternion.AngleAxis(twistDeg, transform.up) * chest.rotation;
                }

                // Braco ESQUERDO: 100% livre, afastado do torso + balanco completo
                if (lArm != null)
                {
                    lArm.rotation = Quaternion.AngleAxis(abdDeg, transform.forward) * lArm.rotation;
                    lArm.rotation = Quaternion.AngleAxis(swingSin * swingAmp, transform.right) * lArm.rotation;
                }
                if (lFore != null)
                    lFore.rotation = Quaternion.AngleAxis(-bendDeg, transform.right) * lFore.rotation;

                // Braco DIREITO: afastado do torso; se segura ferramenta (COMBATE), balanco reduzido mantendo postura firme
                float rightFactor = IsRightHandHoldingTool ? rightArmToolSwingFactor : 1.0f;
                if (rArm != null)
                {
                    rArm.rotation = Quaternion.AngleAxis(-abdDeg, transform.forward) * rArm.rotation;
                    rArm.rotation = Quaternion.AngleAxis(-swingSin * swingAmp * rightFactor, transform.right) * rArm.rotation;
                }
                if (rFore != null)
                    rFore.rotation = Quaternion.AngleAxis(-bendDeg * (IsRightHandHoldingTool ? 1.15f : 1f), transform.right) * rFore.rotation;

                // Bob vertical sutil na corrida
                if (hips != null && runWeight > 0.05f)
                    hips.position += new Vector3(0f, bobWave * runVerticalBobMeters * runWeight, 0f);

                LastAppliedArmSwingDeg = swingAmp;
                LastAppliedTrunkLeanDeg = leanDeg;
            }

            // 2. Landing Dip elastico no Hips somente quando ha impacto ativo de pouso
            if (Mathf.Abs(_dipOffset) > 0.001f)
            {
                Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    hips.position += new Vector3(0f, _dipOffset, 0f);
                }
            }

            // 3. Sombra de contato dinamica no piso (zero-alloc via RaycastNonAlloc)
            if (_shadowQuad != null)
            {
                Vector3 origin = transform.position + Vector3.up * 0.6f;
                int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _shadowHitBuf, 4.0f, groundMask, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                RaycastHit groundHit = default;
                bool hasGround = false;
                Transform selfRoot = transform.root;
                for (int i = 0; i < hitCount; i++)
                {
                    if (_shadowHitBuf[i].transform.root == selfRoot) continue;
                    if (_shadowHitBuf[i].distance < bestDist)
                    {
                        bestDist = _shadowHitBuf[i].distance;
                        groundHit = _shadowHitBuf[i];
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
    }
}
