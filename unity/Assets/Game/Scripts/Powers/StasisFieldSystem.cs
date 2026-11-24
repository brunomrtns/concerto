using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Powers
{
    /// <summary>
    /// Contrato para qualquer entidade do mundo afetada pela Estase (inimigos, projeteis, plataformas/puzzles).
    /// Permite timeScale local por area/objeto sem travar Time.timeScale global nem desacelerar o Dez.
    /// </summary>
    public interface ITimeScalable
    {
        Transform ScalableTransform { get; }
        float LocalTimeScale { get; }
        void SetLocalTimeScale(float scale);
    }

    /// <summary>
    /// Gerenciador estatico de campos locais de Estase (por area e por objeto).
    /// Garante retorno EXATO a 1.000f quando todas as pulsacoes encerram (impossivel deixar timeScale preso).
    /// </summary>
    public static class StasisFieldSystem
    {
        public struct ActiveField
        {
            public Vector3 Center;
            public float Radius;
            public float TimeScale;
            public float RemainingSec;
        }

        private static readonly List<ActiveField> Fields = new List<ActiveField>(4);
        private static readonly List<ITimeScalable> Registered = new List<ITimeScalable>(32);

        public static bool IsAnyStasisActive => Fields.Count > 0;
        public static float WorldTimeScale { get; private set; } = 1.0f;
        public static float ActiveRemainingSec => Fields.Count > 0 ? Fields[0].RemainingSec : 0f;

        public static event Action<float> OnWorldTimeScaleChanged;

        public static void Register(ITimeScalable target)
        {
            if (target != null && !Registered.Contains(target))
                Registered.Add(target);
        }

        public static void Unregister(ITimeScalable target)
        {
            if (target != null)
                Registered.Remove(target);
        }

        public static void SpawnField(Vector3 center, float radius, float timeScale, float durationSec)
        {
            float clampedScale = Mathf.Clamp(timeScale, 0.1f, 0.95f);
            Fields.Add(new ActiveField
            {
                Center = center,
                Radius = Mathf.Max(1f, radius),
                TimeScale = clampedScale,
                RemainingSec = Mathf.Max(0.2f, durationSec)
            });
            RecomputeAndNotify();
            Debug.Log($"[StasisFieldSystem] Estase ativada: WorldTimeScale={WorldTimeScale:F3}, radius={radius:F1}m, dur={durationSec:F2}s (Dez permanece 1.000).");
        }

        public static void Tick(float unscaledDt)
        {
            if (Fields.Count == 0) return;

            bool changed = false;
            for (int i = Fields.Count - 1; i >= 0; i--)
            {
                var f = Fields[i];
                f.RemainingSec -= unscaledDt;
                if (f.RemainingSec <= 0f)
                {
                    Fields.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    Fields[i] = f;
                }
            }

            if (changed)
            {
                RecomputeAndNotify();
                if (Fields.Count == 0)
                    Debug.Log($"[StasisFieldSystem] Estase encerrada: WorldTimeScale restaurado EXATO para {WorldTimeScale:F3}.");
            }
            else
            {
                ApplyToRegistered();
            }
        }

        public static void ClearAll()
        {
            if (Fields.Count == 0 && Mathf.Approximately(WorldTimeScale, 1.0f)) return;
            Fields.Clear();
            RecomputeAndNotify();
            Debug.Log($"[StasisFieldSystem] ClearAll: WorldTimeScale restaurado EXATO para {WorldTimeScale:F3}.");
        }

        /// <summary>
        /// Retorna o timeScale local na posicao informada (1.0f fora de qualquer campo de Estase).
        /// </summary>
        public static float GetTimeScaleAt(Vector3 worldPos)
        {
            if (Fields.Count == 0) return 1.0f;
            float minScale = 1.0f;
            for (int i = 0; i < Fields.Count; i++)
            {
                var f = Fields[i];
                if ((worldPos - f.Center).sqrMagnitude <= f.Radius * f.Radius)
                {
                    if (f.TimeScale < minScale) minScale = f.TimeScale;
                }
            }
            return minScale;
        }

        private static void RecomputeAndNotify()
        {
            float next = 1.0f;
            for (int i = 0; i < Fields.Count; i++)
            {
                if (Fields[i].TimeScale < next) next = Fields[i].TimeScale;
            }
            // Garantia dura: sem campos ativos, SEMPRE 1.0f exato (sem drift de ponto flutuante!)
            if (Fields.Count == 0) next = 1.0f;

            WorldTimeScale = next;
            ApplyToRegistered();
            OnWorldTimeScaleChanged?.Invoke(WorldTimeScale);
        }

        private static void ApplyToRegistered()
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var item = Registered[i];
                if (item == null || item.ScalableTransform == null)
                {
                    Registered.RemoveAt(i);
                    continue;
                }
                item.SetLocalTimeScale(GetTimeScaleAt(item.ScalableTransform.position));
            }
        }
    }

    /// <summary>
    /// Componente pronto para objetos de puzzle/playground (pendulos, plataformas, engrenagens, projeteis)
    /// responderem automaticamente ao campo local de Estase (50% velocidade sem afetar o Dez).
    /// </summary>
    public sealed class StasisAffectedObject : MonoBehaviour, ITimeScalable
    {
        public Vector3 spinDegPerSec = new Vector3(0f, 180f, 0f);
        public Vector3 oscAmplitude = new Vector3(0f, 0.55f, 0f);
        public float oscFrequencyHz = 0.85f;

        private Vector3 _originPos;
        private float _localClock;
        public float LocalTimeScale { get; private set; } = 1.0f;
        public float LocalClock => _localClock;
        public Transform ScalableTransform => transform;

        private void OnEnable()
        {
            _originPos = transform.position;
            StasisFieldSystem.Register(this);
        }

        private void OnDisable()
        {
            StasisFieldSystem.Unregister(this);
        }

        public void SetLocalTimeScale(float scale)
        {
            LocalTimeScale = scale;
        }

        private void Update()
        {
            float dt = Time.deltaTime * LocalTimeScale;
            _localClock += dt;
            if (spinDegPerSec.sqrMagnitude > 0.001f)
                transform.Rotate(spinDegPerSec * dt, Space.World);
            if (oscAmplitude.sqrMagnitude > 0.0001f)
                transform.position = _originPos + oscAmplitude * Mathf.Sin(_localClock * Mathf.PI * 2f * oscFrequencyHz);
        }
    }
}
