using UnityEngine;

namespace Studio.Game.Sector
{
    /// <summary>
    /// VFX economico do "Ceu Errado" no Setor Externo:
    ///   - Pulsa a rachadura luminosa ciano/magenta no topo do ceu
    ///   - Ondula e pisca suavemente as faixas de aurora glitch (zero shader pesado!)
    ///   - Oscila sutilmente a luz fria vinda da fenda
    /// </summary>
    public sealed class SkyRiftAnimator : MonoBehaviour
    {
        [Header("Elementos do Ceu Errado")]
        public Transform[] riftSegments;
        public Transform[] auroraRibbons;
        public Light riftSkyLight;

        [Header("Parametros Tunaveis")]
        public float pulseSpeed = 1.6f;
        public float auroraWaveSpeed = 0.85f;
        public float baseLightIntensity = 1.85f;

        private Vector3[] _ribbonBasePos;

        private void Awake()
        {
            if (auroraRibbons != null)
            {
                _ribbonBasePos = new Vector3[auroraRibbons.Length];
                for (int i = 0; i < auroraRibbons.Length; i++)
                    if (auroraRibbons[i] != null) _ribbonBasePos[i] = auroraRibbons[i].localPosition;
            }
        }

        private void Update()
        {
            float t = Time.time;

            if (riftSegments != null)
            {
                for (int i = 0; i < riftSegments.Length; i++)
                {
                    var seg = riftSegments[i];
                    if (seg == null) continue;
                    float glitch = (Mathf.Sin(t * 13.7f + i * 2.1f) > 0.94f) ? 1.35f : 1.0f;
                    float pulse = (0.88f + 0.14f * Mathf.Sin(t * pulseSpeed + i * 0.9f)) * glitch;
                    var ls = seg.localScale;
                    seg.localScale = new Vector3(ls.x, Mathf.Clamp(pulse * 1.15f, 0.5f, 2.2f), ls.z);
                }
            }

            if (auroraRibbons != null && _ribbonBasePos != null)
            {
                for (int i = 0; i < auroraRibbons.Length; i++)
                {
                    var rib = auroraRibbons[i];
                    if (rib == null) continue;
                    float dx = Mathf.Sin(t * auroraWaveSpeed + i * 1.4f) * 1.8f;
                    float dy = Mathf.Cos(t * auroraWaveSpeed * 0.7f + i) * 0.65f;
                    rib.localPosition = _ribbonBasePos[i] + new Vector3(dx, dy, 0f);
                }
            }

            if (riftSkyLight != null)
            {
                float flicker = Mathf.Sin(t * 7.3f) * Mathf.Cos(t * 3.1f) > 0.82f ? 1.35f : 1.0f;
                riftSkyLight.intensity = baseLightIntensity * (0.92f + 0.12f * Mathf.Sin(t * 1.2f)) * flicker;
            }
        }
    }
}
