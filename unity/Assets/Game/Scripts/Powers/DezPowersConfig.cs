using UnityEngine;

namespace Studio.Game.Powers
{
    /// <summary>
    /// Configuracao 100% data-driven dos Poderes Temporais do Dez (#93 — Eco Temporal e Estase).
    /// Zero hardcode: todo valor tunavel vive neste ScriptableObject (DESIGN_BIBLE Secoes 2, 3 e 9).
    /// </summary>
    [CreateAssetMenu(fileName = "DezPowersConfig", menuName = "Studio/Concerto/Dez Powers Config")]
    public sealed class DezPowersConfig : ScriptableObject
    {
        [Header("Eco Temporal (Dodge com i-frames + Afterimage Ciano)")]
        public KeyCode ecoPrimaryKey = KeyCode.LeftAlt;
        public KeyCode ecoAltKey = KeyCode.C;
        [Tooltip("Duracao do deslocamento do Eco Temporal (s).")]
        public float ecoDurationSec = 0.24f;
        [Tooltip("Janela real de invulnerabilidade (i-frames) do Eco Temporal (~0.30s).")]
        public float ecoIFramesSec = 0.30f;
        [Tooltip("Velocidade do dash curto do Eco Temporal (m/s).")]
        public float ecoDashSpeed = 12.0f;
        [Tooltip("Cooldown do Eco Temporal (~3s).")]
        public float ecoCooldownSec = 3.0f;
        [Tooltip("Quantidade de afterimages cianos deixados no rastro.")]
        [Range(2, 6)] public int ecoAfterimageCount = 4;
        [Tooltip("Duracao do fade-out de cada afterimage (s).")]
        public float ecoAfterimageLifetimeSec = 0.36f;
        public Color ecoAfterimageColor = new Color(0.22f, 0.92f, 1.0f, 0.68f);

        [Header("Estase (Desacelera o Mundo Localmente, NUNCA o Dez)")]
        public KeyCode stasisKey = KeyCode.R;
        [Tooltip("Duracao da pulsacao de Estase (~3s).")]
        public float stasisDurationSec = 3.0f;
        [Tooltip("Fator de desaceleracao do mundo dentro da area de Estase (~0.5 = 50%).")]
        [Range(0.15f, 0.75f)] public float stasisWorldTimeScale = 0.50f;
        [Tooltip("Cooldown da Estase (~10s).")]
        public float stasisCooldownSec = 10.0f;
        [Tooltip("Raio da esfera/campo local de Estase (m).")]
        public float stasisRadius = 15.0f;
        public Color stasisPulseColor = new Color(0.28f, 0.85f, 1.0f, 0.42f);

        [Header("Micra (Reliquia Ponteiro Menor — 2s camera lenta, cooldown 8s; NAO substitui Estase)")]
        public KeyCode micraKey = KeyCode.T;
        [Tooltip("Duracao da camera lenta da Micra (2.0s exatos — GDD 03 §2.4).")]
        public float micraDurationSec = 2.0f;
        [Tooltip("Fator de camera lenta da Micra (0.35 = 35% velocidade).")]
        [Range(0.15f, 0.65f)] public float micraWorldTimeScale = 0.35f;
        [Tooltip("Cooldown da Micra (8.0s exatos — GDD 03 §2.4).")]
        public float micraCooldownSec = 8.0f;
        [Tooltip("Raio da bolha de Micra (m).")]
        public float micraRadius = 22.0f;

        [Header("Desbloqueio Pos-Cutscene 3 (Hint Curta — NAO explica a origem)")]
        public string firstUnlockHintText = "O tempo vazou pelos seus dedos...  [ALT/C] Eco Temporal  |  [R] Estase";
        public float firstUnlockHintDurationSec = 4.8f;
    }
}
