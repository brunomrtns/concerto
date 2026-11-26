using UnityEngine;

namespace Studio.Game.Retrogrado
{
    /// <summary>
    /// GATE 1 (Retrogrado): Plataforma de janela INVERTIDA — ao contrario de plataformas que somem,
    /// esta comeca desmaterializada e EXISTE CADA VEZ MAIS conforme o jogador espera (tempo reverso).
    /// </summary>
    public sealed class ReverseWindowPlatform : MonoBehaviour
    {
        public float materializeDurationSec = 2.0f;
        public float solidThreshold01 = 0.50f;

        public float Stability01 { get; private set; }
        public bool IsSolid => Stability01 >= solidThreshold01;

        private Collider _col;
        private Vector3 _fullScale = Vector3.one;
        private bool _scaleCaptured;

        private void Awake()
        {
            _col = GetComponentInChildren<Collider>();
            CaptureBaseScale();
            ApplyStabilityVisual();
        }

        private void CaptureBaseScale()
        {
            if (_scaleCaptured) return;
            _fullScale = transform.localScale.sqrMagnitude > 0.001f ? transform.localScale : Vector3.one;
            _scaleCaptured = true;
        }

        public void AdvanceWait(float dt)
        {
            CaptureBaseScale();
            Stability01 = Mathf.Clamp01(Stability01 + Mathf.Max(0f, dt) / Mathf.Max(0.25f, materializeDurationSec));
            ApplyStabilityVisual();
        }

        public void ResetToZero()
        {
            CaptureBaseScale();
            Stability01 = 0f;
            ApplyStabilityVisual();
        }

        public void SetFullRestored()
        {
            CaptureBaseScale();
            Stability01 = 1f;
            ApplyStabilityVisual();
        }

        private void ApplyStabilityVisual()
        {
            if (_col != null) _col.enabled = IsSolid;
            float s = Mathf.Lerp(0.18f, 1.0f, Stability01);
            transform.localScale = _fullScale * s;
        }
    }
}
