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

        private Animator _anim;
        private float _dipOffset;
        private float _dipVel;
        private float _currentSpeed;
        private bool _isGrounded = true;
        private bool _isMantling;
        private float _mantleT;
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
            // 2. Respiracao sutil no Chest SOMENTE quando parado no chao (zero interferencia andando/correndo)
            else if (_isGrounded && _currentSpeed < 0.08f)
            {
                float phase = (Time.time / Mathf.Max(0.5f, breathCycleSec)) * Mathf.PI * 2f;
                float breath = Mathf.Sin(phase);
                Transform chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(breath * breathChestDeg, transform.right) * chest.rotation;
                }
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

            // 3. Sombra de contato dinamica no piso
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
    }
}
