using Studio.Game.Powers;
using UnityEngine;

namespace Studio.Game.Epicenter
{
    public enum EpicenterPlatformMode
    {
        Static,
        MovingLinear,
        FastStasisPuzzle,
        Rotating
    }

    /// <summary>
    /// Plataforma do Epicentro (#102) com suporte nativo a Estase (#93 via ITimeScalable).
    /// Na prova do Estase (FastStasisPuzzle), a plataforma oscila rapida demais ate o jogador ativar Estase (50%).
    /// </summary>
    public sealed class EpicenterPlatformController : MonoBehaviour, ITimeScalable
    {
        public EpicenterPlatformMode mode = EpicenterPlatformMode.Static;
        public EpicenterConfig config;
        public Vector3 moveAxis = new Vector3(0f, 0f, 1f);
        public float moveRange = 4.5f;

        private Vector3 _origin;
        private float _clock;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public bool IsSlowedByStasis => LocalTimeScale < 0.95f;
        public Transform ScalableTransform => transform;

        public void SetLocalTimeScale(float scale) => LocalTimeScale = scale;

        private void Awake()
        {
            _origin = transform.position;
        }

        private void OnEnable()
        {
            StasisFieldSystem.Register(this);
        }

        private void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
        }

        private void Update()
        {
            if (mode == EpicenterPlatformMode.Static) return;
            float dt = Time.deltaTime * LocalTimeScale;
            _clock += dt;

            if (mode == EpicenterPlatformMode.Rotating)
            {
                float degPerSec = config != null ? config.rotatingPlatformDegPerSec : 42f;
                transform.Rotate(0f, degPerSec * dt, 0f, Space.World);
                return;
            }

            float speed = mode == EpicenterPlatformMode.FastStasisPuzzle
                ? (config != null ? config.fastStasisPlatformSpeed : 10.5f)
                : (config != null ? config.normalMovingPlatformSpeed : 3.2f);

            float offset = Mathf.Sin(_clock * (speed / Mathf.Max(1f, moveRange))) * moveRange;
            transform.position = _origin + moveAxis.normalized * offset;
        }
    }
}
