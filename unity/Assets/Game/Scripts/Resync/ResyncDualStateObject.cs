using UnityEngine;

namespace Studio.Game.Resync
{
    public enum ResyncState
    {
        Desincronizado = 0,
        Restaurado = 1
    }

    public enum DualStateMode
    {
        /// <summary>Solido/caminhavel apenas em Desincronizado; atravessavel em Restaurado (ex: plataforma temporal).</summary>
        SolidOnlyInDesync = 0,
        /// <summary>Bloqueia em Desincronizado; atravessavel apenas em Restaurado (ex: feixe de luz).</summary>
        PassableOnlyInRestored = 1,
        /// <summary>Solido em ambos; troca apenas material/visual (fairness arquitetonica).</summary>
        VisualSwapOnly = 2
    }

    /// <summary>
    /// Componente de objeto de dois estados da Re-Sincronia (GDD 04 §1 + GDD 05 §2):
    ///   - REGRA DE OURO (Fairness): GEOMETRIA IDENTICA entre Desincronizado e Restaurado
    ///     (Mesh e Transform nunca mudam; somente material e propriedades de colisao mudam).
    ///   - Escrita Fantasma (0,8s): exibe wireframe do novo estado durante a transicao antes de trocar a colisao.
    /// </summary>
    public sealed class ResyncDualStateObject : MonoBehaviour
    {
        [Header("Configuracao de Estado (Geometria Identica)")]
        public DualStateMode mode = DualStateMode.SolidOnlyInDesync;
        public Material desyncMaterial;
        public Material restoredMaterial;
        public Material ghostPassableMaterial;

        public ResyncState CurrentState { get; private set; } = ResyncState.Desincronizado;
        public bool IsSolidNow { get; private set; } = true;
        public bool IsShowingGhostWireframe { get; private set; }
        public Mesh SharedGeometryMesh => _meshFilter != null ? _meshFilter.sharedMesh : null;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Collider _collider;
        private LineRenderer _wireframeLine;

        private void Awake()
        {
            CacheComponents();
            EnsureDefaultMaterials();
            BuildWireframeOverlay();
        }

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_collider == null) _collider = GetComponent<Collider>();
        }

        private void EnsureDefaultMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (desyncMaterial == null)
            {
                desyncMaterial = CreateSimpleMat(lit,
                    mode == DualStateMode.PassableOnlyInRestored ? new Color(0.88f, 0.34f, 0.18f, 1f) : new Color(0.42f, 0.24f, 0.58f, 1f),
                    mode == DualStateMode.PassableOnlyInRestored ? new Color(1.1f, 0.32f, 0.10f, 1f) : new Color(0.32f, 0.14f, 0.48f, 1f),
                    transparent: false);
            }
            if (restoredMaterial == null)
            {
                restoredMaterial = CreateSimpleMat(lit,
                    new Color(0.18f, 0.78f, 0.92f, 1f),
                    new Color(0.16f, 0.72f, 0.95f, 1f),
                    transparent: false);
            }
            if (ghostPassableMaterial == null)
            {
                ghostPassableMaterial = CreateSimpleMat(lit,
                    new Color(0.22f, 0.88f, 1.0f, 0.18f),
                    new Color(0.10f, 0.45f, 0.65f, 1f),
                    transparent: true);
            }
        }

        private static Material CreateSimpleMat(Shader sh, Color baseCol, Color emissive, bool transparent)
        {
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emissive);
            }
            if (transparent)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return m;
        }

        private void BuildWireframeOverlay()
        {
            if (_wireframeLine != null) return;
            var go = new GameObject("GhostWireframeOverlay");
            go.transform.SetParent(transform, false);
            _wireframeLine = go.AddComponent<LineRenderer>();
            _wireframeLine.useWorldSpace = false;
            _wireframeLine.loop = false;
            _wireframeLine.widthMultiplier = 0.045f;
            _wireframeLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _wireframeLine.receiveShadows = false;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var wfMat = new Material(unlit);
            if (wfMat.HasProperty("_BaseColor")) wfMat.SetColor("_BaseColor", Color.cyan);
            if (wfMat.HasProperty("_Color")) wfMat.SetColor("_Color", Color.cyan);
            _wireframeLine.sharedMaterial = wfMat;

            // Contorno wireframe completo das 12 arestas do cubo local [-0.5..0.5]
            Vector3 c000 = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 c100 = new Vector3( 0.5f, -0.5f, -0.5f);
            Vector3 c110 = new Vector3( 0.5f,  0.5f, -0.5f);
            Vector3 c010 = new Vector3(-0.5f,  0.5f, -0.5f);
            Vector3 c001 = new Vector3(-0.5f, -0.5f,  0.5f);
            Vector3 c101 = new Vector3( 0.5f, -0.5f,  0.5f);
            Vector3 c111 = new Vector3( 0.5f,  0.5f,  0.5f);
            Vector3 c011 = new Vector3(-0.5f,  0.5f,  0.5f);
            Vector3[] pts =
            {
                c000, c100, c110, c010, c000,
                c001, c101, c111, c011, c001,
                c101, c100, c110, c111, c011, c010
            };
            _wireframeLine.positionCount = pts.Length;
            _wireframeLine.SetPositions(pts);
            _wireframeLine.enabled = false;
        }

        public void BeginGhostPreview(ResyncState targetState)
        {
            CacheComponents();
            BuildWireframeOverlay();
            IsShowingGhostWireframe = true;
            if (_wireframeLine != null)
            {
                Color wfCol = targetState == ResyncState.Restaurado
                    ? new Color(0.20f, 0.95f, 1.0f, 1f)
                    : new Color(1.0f, 0.55f, 0.18f, 1f);
                _wireframeLine.startColor = wfCol;
                _wireframeLine.endColor = wfCol;
                if (_wireframeLine.sharedMaterial != null)
                {
                    if (_wireframeLine.sharedMaterial.HasProperty("_BaseColor"))
                        _wireframeLine.sharedMaterial.SetColor("_BaseColor", wfCol);
                    if (_wireframeLine.sharedMaterial.HasProperty("_Color"))
                        _wireframeLine.sharedMaterial.SetColor("_Color", wfCol);
                }
                _wireframeLine.enabled = true;
            }
        }

        public void ApplyStateImmediate(ResyncState state)
        {
            CacheComponents();
            EnsureDefaultMaterials();
            CurrentState = state;
            IsShowingGhostWireframe = false;
            if (_wireframeLine != null) _wireframeLine.enabled = false;

            bool shouldBeSolid;
            switch (mode)
            {
                case DualStateMode.SolidOnlyInDesync:
                    shouldBeSolid = (state == ResyncState.Desincronizado);
                    break;
                case DualStateMode.PassableOnlyInRestored:
                    shouldBeSolid = (state == ResyncState.Desincronizado);
                    break;
                default:
                    shouldBeSolid = true;
                    break;
            }

            IsSolidNow = shouldBeSolid;
            if (_collider != null) _collider.enabled = shouldBeSolid;

            if (_meshRenderer != null)
            {
                if (!shouldBeSolid)
                    _meshRenderer.sharedMaterial = ghostPassableMaterial;
                else
                    _meshRenderer.sharedMaterial = (state == ResyncState.Restaurado) ? restoredMaterial : desyncMaterial;
            }
        }
    }
}
