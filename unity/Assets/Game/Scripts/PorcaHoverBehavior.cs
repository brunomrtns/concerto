using UnityEngine;

namespace Studio.Game
{
    /// <summary>
    /// Animacao procedural leve de flutuacao da Porca (S-char-01 / plano-producao-personagens.md §3).
    /// Hover senoidal + leve balanco organico, sem alterar a fisica ou caminhos congelados.
    /// </summary>
    public sealed class PorcaHoverBehavior : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.08f;
        [SerializeField] private float frequency = 2.1f;
        [SerializeField] private float tiltDegrees = 4.5f;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        private void Awake()
        {
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.time * frequency;
            transform.localPosition = _baseLocalPos + new Vector3(0f, Mathf.Sin(t) * amplitude, 0f);
            float pitch = Mathf.Sin(t * 0.7f) * tiltDegrees;
            float roll = Mathf.Cos(t * 0.55f) * (tiltDegrees * 0.75f);
            transform.localRotation = _baseLocalRot * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
