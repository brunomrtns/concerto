using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// GATE 5 — Movimento Secundario Mecanico + Sombra de Contato do Dez:
    ///   REGRA 3 OBEDECIDA INTEGRALMENTE: ZERO animacao procedural de corpo/bracos/pernas/tronco/hips em C#!
    ///   Todo movimento corporal vem exclusivamente dos clipes reais Humanoid + Blend Tree + Animation Rigging IK.
    ///   Este componente faz APENAS:
    ///     1. Sombra de contato dinamica no piso (nao toca em nenhum osso);
    ///     2. Movimento secundario puramente mecanico (mola amortecida sutil para antena/cabos/detalhes mecanicos).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class DezProceduralAnimator : MonoBehaviour
    {
        [Header("GATE 5 — Movimento Secundario Mecanico (Antena / Cabos — Sutil)")]
        [Tooltip("Transforms mecanicos secundarios (antenas/cabos/detalhes). NUNCA ossos de corpo!")]
        public Transform[] mechanicalSecondaryTransforms;
        [Range(0f, 14f)] public float maxMechanicalSwayDeg = 6.5f;
        public float mechanicalSpringK = 68f;
        public float mechanicalDamping = 10.5f;

        [Header("Sombra de Contato (Peso & Gravidade — Nao toca em ossos)")]
        public bool enableContactShadow = true;
        public float shadowBaseRadius = 0.34f;
        public LayerMask groundMask = ~0;

        // Propriedades de compatibilidade com callers existentes
        public bool SimulateLegacyAnimMode { get; set; }
        public bool IsRightHandHoldingTool { get; set; }
        public float LastAppliedArmSwingDeg => 0f;
        public float LastAppliedTrunkLeanDeg => 0f;
        public Vector2 CurrentMechanicalSwayDeg => _swayDeg;

        private float _currentSpeed;
        private float _prevSpeed;
        private float _turnRate;
        private bool _isGrounded = true;
        private Vector2 _swayDeg;
        private Vector2 _swayVel;
        private Quaternion[] _baseLocalRots;

        private Transform _shadowQuad;
        private Material _shadowMat;
        private readonly RaycastHit[] _shadowHitBuf = new RaycastHit[8];

        private void Awake()
        {
            if (enableContactShadow) CreateContactShadow();
            AutoDiscoverMechanicalDetails();
        }

        private void OnDestroy()
        {
            if (_shadowQuad != null) Destroy(_shadowQuad.gameObject);
            if (_shadowMat != null) Destroy(_shadowMat);
        }

        private void AutoDiscoverMechanicalDetails()
        {
            if (mechanicalSecondaryTransforms == null || mechanicalSecondaryTransforms.Length == 0)
            {
                var found = new System.Collections.Generic.List<Transform>(4);
                var all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string n = all[i].name.ToLowerInvariant();
                    if (n.Contains("antenna") || n.Contains("antena") || n.Contains("cable") || n.Contains("cabo"))
                        found.Add(all[i]);
                }
                mechanicalSecondaryTransforms = found.ToArray();
            }

            _baseLocalRots = new Quaternion[mechanicalSecondaryTransforms.Length];
            for (int i = 0; i < mechanicalSecondaryTransforms.Length; i++)
            {
                if (mechanicalSecondaryTransforms[i] != null)
                    _baseLocalRots[i] = mechanicalSecondaryTransforms[i].localRotation;
            }
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

        public void NotifyLanding(float impactSpeed) => TriggerLandingDip(impactSpeed);

        public void TriggerLandingDip(float impactSpeed)
        {
            // Impulso sutil somente no oscilador mecanico secundario (antena/cabos) — zero edicao de ossos do corpo!
            float impulse = Mathf.Clamp(impactSpeed * 4.5f, 0f, 28f);
            _swayVel.x += impulse;
        }

        public void SetLocomotionState(bool grounded, float horizSpeed, float turnRate)
        {
            _isGrounded = grounded;
            _currentSpeed = horizSpeed;
            _turnRate = turnRate;
        }

        public void SetMantleState(bool mantling, float normalizedTime)
        {
            // Mantido por compatibilidade de assinatura; animacao corporal controlada por Animator/IK.
        }

        private void LateUpdate()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            UpdateMechanicalSecondaryMotion(dt);
            UpdateContactShadow();
        }

        private void UpdateMechanicalSecondaryMotion(float dt)
        {
            float accel = (_currentSpeed - _prevSpeed) / dt;
            _prevSpeed = _currentSpeed;

            // Alvo sutil: leve atraso inercial ao acelerar/frear + inclinacao lateral suave ao girar
            float targetPitch = Mathf.Clamp(-accel * 0.45f - _currentSpeed * 0.65f, -maxMechanicalSwayDeg, maxMechanicalSwayDeg);
            float targetRoll  = Mathf.Clamp(-_turnRate * 0.015f, -maxMechanicalSwayDeg * 0.8f, maxMechanicalSwayDeg * 0.8f);
            if (!_isGrounded) targetPitch = Mathf.Clamp(targetPitch - 2.0f, -maxMechanicalSwayDeg, maxMechanicalSwayDeg);

            Vector2 target = new Vector2(targetPitch, targetRoll);
            Vector2 force = (target - _swayDeg) * mechanicalSpringK - _swayVel * mechanicalDamping;
            _swayVel += force * dt;
            _swayDeg += _swayVel * dt;
            _swayDeg.x = Mathf.Clamp(_swayDeg.x, -maxMechanicalSwayDeg, maxMechanicalSwayDeg);
            _swayDeg.y = Mathf.Clamp(_swayDeg.y, -maxMechanicalSwayDeg, maxMechanicalSwayDeg);

            if (mechanicalSecondaryTransforms == null) return;
            Quaternion swayRot = Quaternion.Euler(_swayDeg.x, 0f, _swayDeg.y);
            for (int i = 0; i < mechanicalSecondaryTransforms.Length; i++)
            {
                var tr = mechanicalSecondaryTransforms[i];
                if (tr != null)
                    tr.localRotation = _baseLocalRots[i] * swayRot;
            }
        }

        private void UpdateContactShadow()
        {
            if (_shadowQuad == null) return;
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _shadowHitBuf, 4.5f, groundMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            Vector3 bestPt = transform.position;
            bool found = false;
            for (int i = 0; i < hits; i++)
            {
                if (_shadowHitBuf[i].collider == null || _shadowHitBuf[i].transform.root == transform.root) continue;
                if (_shadowHitBuf[i].distance < bestDist)
                {
                    bestDist = _shadowHitBuf[i].distance;
                    bestPt = _shadowHitBuf[i].point;
                    found = true;
                }
            }
            if (!found)
            {
                _shadowQuad.gameObject.SetActive(false);
                return;
            }
            _shadowQuad.gameObject.SetActive(true);
            float h = Mathf.Max(0f, bestDist - 0.35f);
            _shadowQuad.position = bestPt + Vector3.up * 0.015f;
            float scale = shadowBaseRadius * 2f * Mathf.Clamp01(1f - h * 0.22f);
            _shadowQuad.localScale = new Vector3(Mathf.Max(0.25f, scale), Mathf.Max(0.25f, scale), 1f);
        }
    }
}
