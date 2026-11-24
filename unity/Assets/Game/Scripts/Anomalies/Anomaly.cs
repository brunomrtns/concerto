using System;
using UnityEngine;

namespace Studio.Game.Anomalies
{
    public enum AnomalyState
    {
        Normal = 0,
        Anomalous = 1
    }

    public enum AnomalyKind
    {
        LightFlicker = 0,
        ReverseSpin = 1,
        FloatingTool = 2,
        WrongCornerSound = 3,
        MismatchedShadow = 4
    }

    /// <summary>
    /// Componente reutilizavel de Anomalia Ambiental (Ato 2 — Cutscene 3 em diante):
    ///   - Estados limpos: Normal vs Anomalous
    ///   - API pronta para a proxima task de Reparo: Resolve(), Rebreak(), SetAnomalous(bool), OnStateChanged
    ///   - Totalmente inativa (invisivel/inaudivel) antes da Cutscene 3 (Gate 3)
    /// </summary>
    public sealed class Anomaly : MonoBehaviour
    {
        [Header("Identidade & Config")]
        public string anomalyId;
        public string displayName;
        public AnomalyKind kind;

        [Header("Referencias Visuais & Sonoras (Opcionais por Tipo)")]
        public Transform targetVisual;
        public Light flickerLight;
        public Renderer emissiveRenderer;
        public GameObject shadowQuad;
        public ParticleSystem anomalyParticles;
        public AudioSource spatialAudio;

        [Header("Parametros Tunaveis")]
        public float floatAmplitude = 0.14f;
        public float floatSpeed = 1.85f;
        public float reverseSpinSpeedDeg = -48f;
        public float flickerBaseIntensity = 8.5f;

        private AnomalyState _state = AnomalyState.Normal;
        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private bool _restCached;
        private float _seed;

        public AnomalyState State => _state;
        public bool IsAnomalous => _state == AnomalyState.Anomalous;
        public event Action<Anomaly, AnomalyState> OnStateChanged;

        private void Awake()
        {
            CacheRestPose();
            _seed = Mathf.Abs(anomalyId != null ? anomalyId.GetHashCode() % 100 : 17) * 0.37f;
            ApplyStateImmediate(AnomalyState.Normal);
        }

        private void CacheRestPose()
        {
            if (_restCached || targetVisual == null) return;
            _restLocalPos = targetVisual.localPosition;
            _restLocalRot = targetVisual.localRotation;
            _restCached = true;
        }

        public void ActivateAnomaly() => SetAnomalous(true);
        public void Resolve() => SetAnomalous(false);
        public void Rebreak() => SetAnomalous(true);

        public void SetAnomalous(bool anomalous)
        {
            var next = anomalous ? AnomalyState.Anomalous : AnomalyState.Normal;
            if (_state == next) return;
            ApplyStateImmediate(next);
            OnStateChanged?.Invoke(this, _state);
        }

        private void ApplyStateImmediate(AnomalyState next)
        {
            CacheRestPose();
            _state = next;
            bool on = _state == AnomalyState.Anomalous;

            if (flickerLight != null) flickerLight.enabled = on;
            if (shadowQuad != null) shadowQuad.SetActive(on);

            if (anomalyParticles != null)
            {
                if (on) anomalyParticles.Play();
                else anomalyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (spatialAudio != null)
            {
                if (on)
                {
                    if (!spatialAudio.isPlaying) spatialAudio.Play();
                }
                else
                {
                    spatialAudio.Stop();
                }
            }

            if (!on && targetVisual != null && _restCached)
            {
                targetVisual.localPosition = _restLocalPos;
                targetVisual.localRotation = _restLocalRot;
            }
        }

        private void Update()
        {
            if (_state != AnomalyState.Anomalous) return;
            float t = Time.time + _seed;

            switch (kind)
            {
                case AnomalyKind.LightFlicker:
                    if (flickerLight != null)
                    {
                        float n = Mathf.Sin(t * 23f) * Mathf.Cos(t * 11.3f);
                        flickerLight.intensity = n > -0.25f ? flickerBaseIntensity : flickerBaseIntensity * 0.12f;
                    }
                    break;

                case AnomalyKind.ReverseSpin:
                    if (targetVisual != null)
                    {
                        targetVisual.Rotate(0f, reverseSpinSpeedDeg * Time.deltaTime, 0f, Space.Self);
                        float wobble = Mathf.Sin(t * 3.1f) * 8f;
                        var e = targetVisual.localEulerAngles;
                        targetVisual.localEulerAngles = new Vector3(wobble, e.y, -wobble * 0.6f);
                    }
                    break;

                case AnomalyKind.FloatingTool:
                    if (targetVisual != null && _restCached)
                    {
                        float yOff = 0.16f + Mathf.Sin(t * floatSpeed) * floatAmplitude * 0.5f;
                        targetVisual.localPosition = _restLocalPos + Vector3.up * yOff;
                        targetVisual.localRotation = _restLocalRot * Quaternion.Euler(Mathf.Sin(t * 1.4f) * 9f, t * 24f, Mathf.Cos(t * 1.2f) * 6f);
                    }
                    break;

                case AnomalyKind.WrongCornerSound:
                    if (targetVisual != null)
                    {
                        float s = 0.85f + 0.25f * Mathf.Sin(t * 5.2f);
                        targetVisual.localScale = Vector3.one * s;
                    }
                    break;

                case AnomalyKind.MismatchedShadow:
                    if (shadowQuad != null)
                    {
                        // Sombra gira e alonga em angulo impossivel, sem acompanhar a luz real!
                        shadowQuad.transform.localRotation = Quaternion.Euler(90f, t * -22f, 0f);
                        float sx = 1.4f + 0.35f * Mathf.Sin(t * 2.1f);
                        shadowQuad.transform.localScale = new Vector3(sx, 0.72f, 1f);
                    }
                    break;
            }
        }
    }
}
