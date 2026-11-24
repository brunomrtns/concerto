using UnityEngine;

namespace Studio.Game.Epicenter
{
    /// <summary>
    /// Configuracao data-driven da fase "O Epicentro" (#102).
    /// Todo valor tunavel (gravidade instavel, vazio, velocidades de plataformas e arena) vive aqui.
    /// </summary>
    [CreateAssetMenu(fileName = "EpicenterConfig", menuName = "Studio/Concerto/Epicenter Config")]
    public sealed class EpicenterConfig : ScriptableObject
    {
        [Header("Gravidade Instavel & Vazio")]
        [Tooltip("Fator aplicado a gravidade do Dez no Epicentro (<1 = pulo levemente flutuante/instavel).")]
        [Range(0.65f, 1.0f)] public float unstableGravityScale = 0.84f;
        [Tooltip("Altura Y abaixo da qual o vazio mata e respawna no checkpoint da secao.")]
        public float voidKillY = -7.5f;

        [Header("Secao 1 — Travessia & Puzzle de Estase")]
        public float normalMovingPlatformSpeed = 3.2f;
        [Tooltip("Velocidade da plataforma do puzzle de Estase (rapida demais sem Estase; 50% com Estase).")]
        public float fastStasisPlatformSpeed = 10.5f;
        public float rotatingPlatformDegPerSec = 42.0f;

        [Header("Secao 3 — Trigger Stub do Coracao da Fenda (onde #103 pluga o Boss)")]
        public float arenaTriggerRadius = 5.5f;
        public float omenTremorDurationSec = 0.28f;
        public float omenTremorAmplitude = 0.12f;
        public string surviveObjectiveText = "OBJETIVO: SOBREVIVA";
    }
}
