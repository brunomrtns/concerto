using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// DezProceduralAnimator — Camada biomecanica completa e legivel do Dez:
    ///   1. Abertura lateral real dos ombros/bracos (ZERO braco colado/arrastando no torso em Idle, Walk e Run)
    ///   2. Combate na mao direita: postura de empunhadura, arcos de combo melee (1/2/3/pesado) e coice de tiro (Bolt)
    ///   3. Escalada de borda (Ledge Mantle) em 3 fases: agarrar borda (bracos acima) -> puxar + joelho subindo -> erguer
    ///   4. Pouso correndo com absorcao/rolamento dinamico de momentum (ZERO deslize em pouso estatico!)
    ///   5. Sombra de contato dinamica no piso
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class DezProceduralAnimator : MonoBehaviour
    {
        [Header("Locomocao — Bracos Naturais (Afastados do Torso + Contra-Fase)")]
        [Tooltip("Angulo minimo de abertura lateral do braco em relacao ao torso (graus, sempre para FORA).")]
        [Range(10f, 36f)] public float minArmClearanceIdleDeg = 22f;
        [Range(12f, 38f)] public float minArmClearanceWalkDeg = 26f;
        [Range(12f, 38f)] public float minArmClearanceRunDeg  = 24f;
        [Tooltip("Abertura extra suave no osso do ombro (LeftShoulder/RightShoulder) para descolar o braco das costelas.")]
        [Range(0f, 14f)] public float shoulderOutwardLiftDeg = 7.5f;
        [Range(0f, 36f)] public float walkArmSwingTargetDeg = 22f;
        [Range(0f, 48f)] public float runArmSwingTargetDeg = 38f;
        [Range(0f, 25f)] public float walkForearmBendDeg = 12f;
        [Range(0.05f, 1.0f)] public float rightArmToolSwingFactor = 0.25f;

        [Header("Corrida com Vida — Inclinacao de Tronco, Ombros & Bob Sutil")]
        [Range(0f, 12f)] public float runForwardLeanDeg = 7.5f;
        public float walkForwardLeanDeg = 1.8f;
        public float shoulderTwistWalkDeg = 3.5f;
        public float shoulderTwistRunDeg = 6.5f;
        public float runVerticalBobMeters = 0.022f;

        [Header("Landing Dip (Pouso Parado) & Rolamento/Absorcao (Pouso Correndo)")]
        public float landingDipStrength = 0.022f;
        public float landingMaxDip = 0.14f;
        public float landingSpringK = 180f;
        public float landingDamping = 20f;
        public float runningRollDurationSec = 0.36f;

        [Header("Respiracao Sutil (Somente em Idle Parado)")]
        public float breathCycleSec = 3.8f;
        public float breathChestDeg = 1.8f;

        [Header("Sombra de Contato (Peso & Gravidade)")]
        public bool enableContactShadow = true;
        public float shadowBaseRadius = 0.34f;
        public LayerMask groundMask = ~0;

        public bool SimulateLegacyAnimMode { get; set; }
        public bool IsRightHandHoldingTool { get; set; }
        public float LastAppliedArmSwingDeg { get; private set; }
        public float LastAppliedTrunkLeanDeg { get; private set; }
        public bool IsPlayingCombatSwing => _combatSwingTimer > 0f;
        public bool IsPlayingBoltRecoil => _boltRecoilTimer > 0f;
        public bool IsPlayingRunningRoll => _runningRollTimer > 0f;

        private Animator _anim;
        private float _dipOffset;
        private float _dipVel;
        private float _currentSpeed;
        private bool _isGrounded = true;
        private bool _isMantling;
        private float _mantleT;

        // Combate: swing melee & coice de tiro na mao direita
        private float _combatSwingTimer;
        private float _combatSwingDuration = 0.22f;
        private int _combatSwingCombo = 1;
        private bool _combatSwingHeavy;
        private float _boltRecoilTimer;
        private const float BoltRecoilDuration = 0.24f;
        private float _toolHoldBlend;

        // Pouso correndo: absorcao/rolamento dinamico sem deslize
        private float _runningRollTimer;

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
                    float u = (x / (float)(sz - 1)) * 2f - 1f;
                    float v = (y / (float)(sz - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * 0.42f;
                    tex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.05f, a));
                }
            }
            tex.Apply();

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _shadowMat = new Material(sh);
            _shadowMat.mainTexture = tex;
            if (_shadowMat.HasProperty("_Surface")) _shadowMat.SetFloat("_Surface", 1f);
            if (_shadowMat.HasProperty("_SrcBlend")) _shadowMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_shadowMat.HasProperty("_DstBlend")) _shadowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_shadowMat.HasProperty("_ZWrite")) _shadowMat.SetFloat("_ZWrite", 0f);
            _shadowMat.renderQueue = 2990;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _shadowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _shadowQuad = go.transform;
        }

        public void SetLocomotionState(bool grounded, float horizontalSpeed, float turnRateDegPerSec)
        {
            _isGrounded = grounded;
            _currentSpeed = horizontalSpeed;
        }

        public void SetMantleState(bool isMantling, float normalizedT)
        {
            _isMantling = isMantling;
            _mantleT = Mathf.Clamp01(normalizedT);
        }

        public void NotifyLanding(float verticalImpactSpeed)
        {
            float impulse = Mathf.Clamp(Mathf.Abs(verticalImpactSpeed) * landingDipStrength, 0.03f, landingMaxDip);
            _dipVel -= impulse * 18f;
        }

        public void TriggerRunningRoll()
        {
            _runningRollTimer = Mathf.Max(0.22f, runningRollDurationSec);
        }

        public void TriggerCombatSwing(bool isHeavy, int comboStep)
        {
            _combatSwingHeavy = isHeavy;
            _combatSwingCombo = comboStep;
            _combatSwingDuration = isHeavy ? 0.34f : 0.24f;
            _combatSwingTimer = _combatSwingDuration;
        }

        public void TriggerBoltRecoil()
        {
            _boltRecoilTimer = BoltRecoilDuration;
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            float force = -_dipOffset * landingSpringK - _dipVel * landingDamping;
            _dipVel += force * dt;
            _dipOffset = Mathf.Clamp(_dipOffset + _dipVel * dt, -landingMaxDip, 0.02f);

            if (_combatSwingTimer > 0f) _combatSwingTimer = Mathf.Max(0f, _combatSwingTimer - dt);
            if (_boltRecoilTimer > 0f)  _boltRecoilTimer  = Mathf.Max(0f, _boltRecoilTimer - dt);
            if (_runningRollTimer > 0f) _runningRollTimer = Mathf.Max(0f, _runningRollTimer - dt);

            float targetToolHold = IsRightHandHoldingTool ? 1f : 0f;
            _toolHoldBlend = Mathf.MoveTowards(_toolHoldBlend, targetToolHold, 8.5f * dt);
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isHuman) return;

            Transform hips     = _anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine    = _anim.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest    = _anim.GetBoneTransform(HumanBodyBones.Chest);
            Transform lShldr   = _anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            Transform rShldr   = _anim.GetBoneTransform(HumanBodyBones.RightShoulder);
            Transform lArm     = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rArm     = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform lFore    = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform rFore    = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand    = _anim.GetBoneTransform(HumanBodyBones.RightHand);

            // =========================================================================
            // 1. ESCALADA DE BORDA (LEDGE MANTLE) — 3 FASES REAIS (AGARRAR -> PUXAR+JOELHO -> ERGUER)
            // =========================================================================
            if (_isMantling)
            {
                Transform lULeg = _anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform rULeg = _anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform lLLeg = _anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform rLLeg = _anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);

                float grabPhase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_mantleT / 0.35f));
                float pullPhase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_mantleT - 0.35f) / 0.45f));
                float standPhase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_mantleT - 0.80f) / 0.20f));
                float activeWeight = 1f - standPhase;

                // Em Unity left-handed, AngleAxis(+deg, -transform.right) ergue bracos/pernas para FRENTE e CIMA!
                float armRaiseDeg = Mathf.Lerp(112f, 34f, pullPhase) * grabPhase * activeWeight;
                float elbowBendDeg = Mathf.Lerp(18f, 58f, pullPhase) * grabPhase * activeWeight;
                float trunkLeanDeg = Mathf.Lerp(12f, 30f, pullPhase) * grabPhase * activeWeight;

                if (spine != null) spine.rotation = Quaternion.AngleAxis(trunkLeanDeg * 0.5f, transform.right) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(trunkLeanDeg * 0.5f, transform.right) * chest.rotation;

                ApplyShoulderClearance(lShldr, rShldr, 12f * activeWeight);
                if (lArm != null) lArm.rotation = Quaternion.AngleAxis(armRaiseDeg, -transform.right) * lArm.rotation;
                if (rArm != null) rArm.rotation = Quaternion.AngleAxis(armRaiseDeg, -transform.right) * rArm.rotation;
                if (lFore != null) lFore.rotation = Quaternion.AngleAxis(elbowBendDeg, -transform.right) * lFore.rotation;
                if (rFore != null) rFore.rotation = Quaternion.AngleAxis(elbowBendDeg, -transform.right) * rFore.rotation;

                EnsureArmOutwardClearance(lArm, lFore, -transform.right, 30f);
                EnsureArmOutwardClearance(rArm, rFore,  transform.right, 30f);

                // Joelho direito sobe na borda durante o pull-up, seguido pelo esquerdo
                float rKneeLift = Mathf.Sin(pullPhase * Mathf.PI) * activeWeight;
                float lKneeLift = Mathf.Sin(Mathf.Clamp01(pullPhase * 1.15f - 0.25f) * Mathf.PI) * activeWeight;
                if (rULeg != null) rULeg.rotation = Quaternion.AngleAxis(78f * rKneeLift, -transform.right) * rULeg.rotation;
                if (rLLeg != null) rLLeg.rotation = Quaternion.AngleAxis(88f * rKneeLift,  transform.right) * rLLeg.rotation;
                if (lULeg != null) lULeg.rotation = Quaternion.AngleAxis(55f * lKneeLift, -transform.right) * lULeg.rotation;
                if (lLLeg != null) lLLeg.rotation = Quaternion.AngleAxis(68f * lKneeLift,  transform.right) * lLLeg.rotation;
            }
            // =========================================================================
            // 2. IDLE NO CHAO — RESPIRACAO + BRACOS CLARAMENTE SEPARADOS DO TORSO
            // =========================================================================
            else if (_isGrounded && _currentSpeed < 0.08f)
            {
                float phase = (Time.time / Mathf.Max(0.5f, breathCycleSec)) * Mathf.PI * 2f;
                float breath = Mathf.Sin(phase);
                if (chest != null)
                    chest.rotation = Quaternion.AngleAxis(breath * breathChestDeg, transform.right) * chest.rotation;

                if (!SimulateLegacyAnimMode)
                {
                    ApplyShoulderClearance(lShldr, rShldr, shoulderOutwardLiftDeg);
                    EnsureArmOutwardClearance(lArm, lFore, -transform.right, minArmClearanceIdleDeg);
                    EnsureArmOutwardClearance(rArm, rFore,  transform.right, minArmClearanceIdleDeg);
                }
                LastAppliedArmSwingDeg = 0f;
                LastAppliedTrunkLeanDeg = 0f;
            }
            // =========================================================================
            // 3. WALK / RUN NO CHAO — BRACOS SEPARADOS + BALANCO NATURAL EM CONTRA-FASE
            // =========================================================================
            else if (_isGrounded && !SimulateLegacyAnimMode && _currentSpeed >= 0.08f)
            {
                float walkWeight = Mathf.Clamp01(_currentSpeed / 2.2f);
                float runWeight  = Mathf.Clamp01((_currentSpeed - 2.2f) / 3.2f);

                Transform lFoot = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rFoot = _anim.GetBoneTransform(HumanBodyBones.RightFoot);

                float strideBalance = 0f;
                if (hips != null && lFoot != null && rFoot != null)
                {
                    float rFootFwd = Vector3.Dot(rFoot.position - hips.position, transform.forward);
                    float lFootFwd = Vector3.Dot(lFoot.position - hips.position, transform.forward);
                    strideBalance = Mathf.Clamp((rFootFwd - lFootFwd) / 0.42f, -1f, 1f);
                }
                else
                {
                    var st = _anim.GetCurrentAnimatorStateInfo(0);
                    strideBalance = Mathf.Sin((st.normalizedTime % 1f) * Mathf.PI * 2f);
                }

                float minClearDeg = Mathf.Lerp(minArmClearanceWalkDeg, minArmClearanceRunDeg, runWeight);
                float targetSwing = Mathf.Lerp(walkArmSwingTargetDeg, runArmSwingTargetDeg, runWeight) * walkWeight;
                float leanDeg     = Mathf.Lerp(walkForwardLeanDeg, runForwardLeanDeg, runWeight) * walkWeight;
                float twistDeg    = Mathf.Lerp(shoulderTwistWalkDeg, shoulderTwistRunDeg, runWeight) * walkWeight * strideBalance;

                if (spine != null) spine.rotation = Quaternion.AngleAxis(leanDeg * 0.55f, transform.right) * spine.rotation;
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(leanDeg * 0.45f, transform.right) * chest.rotation;
                    chest.rotation = Quaternion.AngleAxis(-twistDeg, transform.up) * chest.rotation;
                }

                ApplyShoulderClearance(lShldr, rShldr, shoulderOutwardLiftDeg);

                // Braco ESQUERDO afastado e em contra-fase
                EnsureArmOutwardClearance(lArm, lFore, -transform.right, minClearDeg);
                ApplyNaturalArmSwing(lArm, lFore, strideBalance * targetSwing, walkWeight * (1f - runWeight * 0.7f));

                // Braco DIREITO afastado e em contra-fase (ou reduzido quando segurando a ferramenta)
                float rightFactor = IsRightHandHoldingTool ? rightArmToolSwingFactor : 1.0f;
                EnsureArmOutwardClearance(rArm, rFore, transform.right, minClearDeg);
                ApplyNaturalArmSwing(rArm, rFore, -strideBalance * targetSwing * rightFactor, walkWeight * (1f - runWeight * 0.7f));

                if (hips != null && runWeight > 0.05f)
                {
                    float bobWave = 1f - Mathf.Abs(strideBalance);
                    hips.position += new Vector3(0f, bobWave * runVerticalBobMeters * runWeight, 0f);
                }

                LastAppliedArmSwingDeg = targetSwing;
                LastAppliedTrunkLeanDeg = leanDeg;
            }
            // =========================================================================
            // 4. NO AR (PULO / QUEDA) — BRACOS AFASTADOS PARA EQUILIBRIO
            // =========================================================================
            else if (!_isGrounded && !SimulateLegacyAnimMode)
            {
                ApplyShoulderClearance(lShldr, rShldr, shoulderOutwardLiftDeg);
                EnsureArmOutwardClearance(lArm, lFore, -transform.right, 26f);
                EnsureArmOutwardClearance(rArm, rFore,  transform.right, 26f);
            }

            // =========================================================================
            // 5. POUSO CORRENDO — ROLAMENTO / MERGULHO DINAMICO DE MOMENTUM (ZERO DESLIZE)
            // =========================================================================
            if (_runningRollTimer > 0f && !_isMantling)
            {
                float u = 1f - (_runningRollTimer / Mathf.Max(0.15f, runningRollDurationSec));
                float rollEnv = Mathf.Sin(u * Mathf.PI); // 0 -> 1 -> 0 suave
                float pitchRoll = rollEnv * 48f;         // mergulho atletico de ombro absorvendo o momentum
                float twistRoll = rollEnv * 24f;
                if (hips != null) hips.position += new Vector3(0f, -0.20f * rollEnv, 0f);
                if (spine != null) spine.rotation = Quaternion.AngleAxis(pitchRoll * 0.55f, transform.right) * spine.rotation;
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(pitchRoll * 0.45f, transform.right) * chest.rotation;
                    chest.rotation = Quaternion.AngleAxis(twistRoll, transform.up) * chest.rotation;
                }
                if (lArm != null) lArm.rotation = Quaternion.AngleAxis(38f * rollEnv, -transform.right) * lArm.rotation;
                if (rArm != null) rArm.rotation = Quaternion.AngleAxis(28f * rollEnv, -transform.right) * rArm.rotation;
            }

            // =========================================================================
            // 6. MODO COMBATE NA MAO DIREITA — EMPUNHADURA + GOLPE MELEE + COICE DE TIRO
            // =========================================================================
            if (!_isMantling && (_toolHoldBlend > 0.01f || _combatSwingTimer > 0f || _boltRecoilTimer > 0f))
            {
                ApplyCombatRightArmLayer(spine, chest, rArm, rFore, rHand);
            }

            // =========================================================================
            // 7. LANDING DIP ELASTICO NO HIPS (POUSO PARADO)
            // =========================================================================
            if (Mathf.Abs(_dipOffset) > 0.001f && _runningRollTimer <= 0f)
            {
                if (hips != null) hips.position += new Vector3(0f, _dipOffset, 0f);
            }

            // =========================================================================
            // 8. SOMBRA DE CONTATO DINAMICA NO PISO
            // =========================================================================
            UpdateContactShadow();
        }

        private void ApplyCombatRightArmLayer(Transform spine, Transform chest, Transform rArm, Transform rFore, Transform rHand)
        {
            if (rArm == null) return;

            // Base de empunhadura pronta em Modo Combate (braco direito erguido a frente segurando a ferramenta!)
            float hold = _toolHoldBlend;
            float armPitchFwd = 46f * hold;
            float armYawIn    = -12f * hold;
            float elbowBend   = 44f * hold;
            float torsoTwist  = 0f;
            float wristKick   = 0f;

            // Arco de golpe melee (Combo 1 / 2 / 3 / Carregado)
            if (_combatSwingTimer > 0f)
            {
                float u = 1f - (_combatSwingTimer / Mathf.Max(0.08f, _combatSwingDuration));
                float windup = u < 0.25f ? (u / 0.25f) : 1f;
                float slash  = u >= 0.25f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.25f) / 0.45f)) : 0f;
                float recov  = u > 0.70f ? Mathf.SmoothStep(0f, 1f, (u - 0.70f) / 0.30f) : 0f;
                float env    = 1f - recov;

                if (_combatSwingHeavy || _combatSwingCombo == 3)
                {
                    // Golpe pesado / 3o golpe: ergue alto acima da cabeca e desce com forca!
                    armPitchFwd = Mathf.Lerp(46f, Mathf.Lerp(135f, 18f, slash), windup) * env + 46f * hold * recov;
                    armYawIn    = Mathf.Lerp(-24f, 38f, slash) * env;
                    elbowBend   = Mathf.Lerp(62f, 14f, slash) * env + 44f * hold * recov;
                    torsoTwist  = Mathf.Lerp(-22f, 28f, slash) * env;
                }
                else if (_combatSwingCombo == 2)
                {
                    // 2o golpe: corte reverso esquerda -> direita
                    armPitchFwd = Mathf.Lerp(58f, 76f, slash) * env + 46f * hold * recov;
                    armYawIn    = Mathf.Lerp(48f, -54f, slash) * env;
                    elbowBend   = Mathf.Lerp(48f, 16f, slash) * env + 44f * hold * recov;
                    torsoTwist  = Mathf.Lerp(24f, -26f, slash) * env;
                }
                else
                {
                    // 1o golpe: corte firme direita -> esquerda
                    armPitchFwd = Mathf.Lerp(54f, 74f, slash) * env + 46f * hold * recov;
                    armYawIn    = Mathf.Lerp(-52f, 50f, slash) * env;
                    elbowBend   = Mathf.Lerp(52f, 18f, slash) * env + 44f * hold * recov;
                    torsoTwist  = Mathf.Lerp(-20f, 26f, slash) * env;
                }
            }

            // Coice real na mao/braco ao disparar tiro de energia (Bolt)
            if (_boltRecoilTimer > 0f)
            {
                float u = 1f - (_boltRecoilTimer / BoltRecoilDuration);
                float kick = Mathf.Sin(Mathf.Clamp01(u / 0.35f) * Mathf.PI * 0.5f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.25f) / 0.75f)));
                // Estende o braco apontando + coice seco para cima/tras
                armPitchFwd = Mathf.Max(armPitchFwd, 84f) + 22f * kick;
                elbowBend   = Mathf.Lerp(12f, 34f, kick);
                wristKick   = 26f * kick;
                torsoTwist -= 12f * kick;
            }

            if (Mathf.Abs(torsoTwist) > 0.1f)
            {
                if (spine != null) spine.rotation = Quaternion.AngleAxis(torsoTwist * 0.45f, transform.up) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(torsoTwist * 0.55f, transform.up) * chest.rotation;
            }

            rArm.rotation = Quaternion.AngleAxis(armPitchFwd, -transform.right) * rArm.rotation;
            if (Mathf.Abs(armYawIn) > 0.1f)
                rArm.rotation = Quaternion.AngleAxis(armYawIn, transform.up) * rArm.rotation;
            if (rFore != null && Mathf.Abs(elbowBend) > 0.1f)
                rFore.rotation = Quaternion.AngleAxis(elbowBend, -transform.right) * rFore.rotation;
            if (rHand != null && Mathf.Abs(wristKick) > 0.1f)
                rHand.rotation = Quaternion.AngleAxis(wristKick, -transform.right) * rHand.rotation;
        }

        private void ApplyShoulderClearance(Transform lShoulder, Transform rShoulder, float deg)
        {
            if (deg <= 0.1f) return;
            // Leve abertura externa na clavicula/ombro para descolar o braco do torso largo do Dez
            if (lShoulder != null) lShoulder.rotation = Quaternion.AngleAxis(deg,  transform.forward) * lShoulder.rotation;
            if (rShoulder != null) rShoulder.rotation = Quaternion.AngleAxis(deg, -transform.forward) * rShoulder.rotation;
        }

        private void UpdateContactShadow()
        {
            if (_shadowQuad == null) return;
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

        /// <summary>
        /// Garante afastamento lateral real do braco em relacao ao torso usando o vetor ombro->cotovelo.
        /// </summary>
        private void EnsureArmOutwardClearance(Transform upperArm, Transform lowerArm, Vector3 outwardDir, float minClearanceDeg)
        {
            if (upperArm == null || lowerArm == null) return;
            Vector3 armVec = lowerArm.position - upperArm.position;
            if (armVec.sqrMagnitude < 0.0001f) return;

            Vector3 coronal = Vector3.ProjectOnPlane(armVec, transform.forward);
            if (coronal.sqrMagnitude < 0.0001f) return;
            coronal.Normalize();

            float curOutDeg = Mathf.Asin(Mathf.Clamp(Vector3.Dot(coronal, outwardDir), -1f, 1f)) * Mathf.Rad2Deg;
            if (curOutDeg < minClearanceDeg)
            {
                float addDeg = Mathf.Clamp(minClearanceDeg - curOutDeg, 0f, 42f);
                Vector3 axis = Vector3.Cross(coronal, outwardDir);
                if (axis.sqrMagnitude > 0.0001f)
                    upperArm.rotation = Quaternion.AngleAxis(addDeg, axis.normalized) * upperArm.rotation;
            }
        }

        private void ApplyNaturalArmSwing(Transform upperArm, Transform lowerArm, float targetFwdDeg, float walkBendWeight)
        {
            if (upperArm == null || lowerArm == null) return;
            Vector3 armVec = lowerArm.position - upperArm.position;
            if (armVec.sqrMagnitude < 0.0001f) return;

            Vector3 sagittal = Vector3.ProjectOnPlane(armVec, transform.right);
            if (sagittal.sqrMagnitude < 0.0001f) return;
            sagittal.Normalize();

            float curFwdDeg = Mathf.Asin(Mathf.Clamp(Vector3.Dot(sagittal, transform.forward), -1f, 1f)) * Mathf.Rad2Deg;
            float delta = Mathf.Clamp(targetFwdDeg - curFwdDeg, -22f, 22f);

            if (Mathf.Abs(delta) > 0.25f)
                upperArm.rotation = Quaternion.AngleAxis(delta, -transform.right) * upperArm.rotation;

            if (walkBendWeight > 0.05f && walkForearmBendDeg > 0.1f)
            {
                float forwardFactor = Mathf.Clamp01(0.35f + 0.65f * (targetFwdDeg / Mathf.Max(1f, walkArmSwingTargetDeg)));
                float bend = walkForearmBendDeg * walkBendWeight * forwardFactor;
                lowerArm.rotation = Quaternion.AngleAxis(bend, -transform.right) * lowerArm.rotation;
            }
        }
    }
}
