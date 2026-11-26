using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Retrogrado
{
    /// <summary>
    /// GATE 1 (Retrogrado — GDD 03 §2.5): Sistema de Tempo Reverso do deserto Desincronizado:
    ///   - Areia cai PARA CIMA (+Y velocity).
    ///   - Pegadas se desmancham A FRENTE do jogador.
    ///   - Artefatos voltam a ser novos conforme o tempo passa.
    ///   - Plataformas de janelas invertidas ganham solidez conforme o jogador espera.
    /// </summary>
    public sealed class RetrogradoReverseTimeSystem : MonoBehaviour
    {
        public static RetrogradoReverseTimeSystem Instance { get; private set; }

        [Header("Tempo Reverso")]
        public float risingSandVelocityY = 3.2f; // Positivo = areia cai PARA CIMA!
        public Transform[] risingSandMotes = new Transform[6];
        public Transform[] forwardFootprints = new Transform[3];
        public List<ReverseWindowPlatform> reversePlatforms = new List<ReverseWindowPlatform>();

        public bool IsReverseTimeActive { get; private set; } = true;
        public float TotalWaitElapsedSec { get; private set; }
        public float ArtifactRestorationProgress01 => Mathf.Clamp01(TotalWaitElapsedSec / 3.0f);
        public bool FootprintsFadeAheadOfPlayer => IsReverseTimeActive;
        public float RisingSandDeltaYAccumulated { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsReverseTimeActive) return;
            TickReverseTime(Time.deltaTime);
        }

        public void TickReverseTime(float dt)
        {
            if (!IsReverseTimeActive || dt <= 0f) return;
            TotalWaitElapsedSec += dt;
            float dy = risingSandVelocityY * dt;
            RisingSandDeltaYAccumulated += dy;

            if (risingSandMotes != null)
            {
                for (int i = 0; i < risingSandMotes.Length; i++)
                {
                    var m = risingSandMotes[i];
                    if (m == null) continue;
                    var p = m.position;
                    p.y += dy;
                    if (p.y > 14f) p.y = 0.5f;
                    m.position = p;
                }
            }

            for (int i = 0; i < reversePlatforms.Count; i++)
            {
                if (reversePlatforms[i] != null)
                    reversePlatforms[i].AdvanceWait(dt);
            }
        }

        public void NormalizeTimePostRepair()
        {
            IsReverseTimeActive = false;
            for (int i = 0; i < reversePlatforms.Count; i++)
            {
                if (reversePlatforms[i] != null)
                    reversePlatforms[i].SetFullRestored();
            }
        }

        public void RestoreReverseState(bool reverseActive)
        {
            IsReverseTimeActive = reverseActive;
            if (!reverseActive) NormalizeTimePostRepair();
        }
    }
}
