using UnityEngine;

namespace Studio.Game.Dialogue
{
    /// <summary>
    /// Highlight sutil no interagivel focado usando MaterialPropertyBlock (zero vazamento de material).
    /// </summary>
    public sealed class InteractionHighlight : MonoBehaviour
    {
        public Color highlightTint = new Color(1.18f, 1.24f, 1.28f, 1f);

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _isHighlighted;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        public void SetHighlighted(bool active)
        {
            if (_isHighlighted == active) return;
            _isHighlighted = active;
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                if (active)
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColorId, highlightTint);
                    r.SetPropertyBlock(_mpb);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        private void OnDisable()
        {
            if (_isHighlighted) SetHighlighted(false);
        }
    }
}
