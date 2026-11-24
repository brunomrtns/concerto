using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// Configuracao data-driven (ScriptableObject) de TODO o Game Feel de movimento e camera do Dez.
    /// ZERO numero magico hardcoded no controller — ajuste qualquer parametro aqui no Inspector!
    /// </summary>
    [CreateAssetMenu(fileName = "DezMovementConfig", menuName = "Studio/Concerto/Dez Movement Config")]
    public sealed class DezMovementConfig : ScriptableObject
    {
        [Header("Locomocao (Responsiva + Parada Brusca)")]
        [Tooltip("Velocidade de caminhada (m/s).")]
        public float walkSpeed = 2.2f;
        [Tooltip("Velocidade de corrida com Shift (m/s).")]
        public float runSpeed = 5.4f;
        [Tooltip("Aceleracao no chao (m/s^2) — resposta rapida ao toque.")]
        public float groundAccel = 26.0f;
        [Tooltip("Desaceleracao no chao (m/s^2) — parada brusca e precisa sem escorregar.")]
        public float groundDecel = 34.0f;
        [Tooltip("Controle horizontal no ar (aceleracao em m/s^2).")]
        public float airAccel = 12.0f;
        [Tooltip("Desaceleracao horizontal no ar (m/s^2).")]
        public float airDecel = 4.0f;
        [Tooltip("Velocidade de rotacao do corpo do Dez (graus/s).")]
        public float turnSpeedDeg = 780f;

        [Header("Pulo + Coyote Time + Input Buffer + Corte de Altura")]
        [Tooltip("Gravidade base (m/s^2).")]
        public float gravity = -24.0f;
        [Tooltip("Multiplicador de gravidade na queda (deixa o pouso firme e responsivo).")]
        public float fallGravityMultiplier = 1.35f;
        [Tooltip("Altura maxima do pulo segurando Espaco (m).")]
        public float jumpHeight = 1.35f;
        [Tooltip("Multiplicador da velocidade vertical ao soltar Espaco cedo (pulo curto).")]
        [Range(0.2f, 0.9f)] public float jumpCutMultiplier = 0.45f;
        [Tooltip("Janela de tolerancia apos sair de uma borda para ainda permitir o pulo (s).")]
        public float coyoteTimeSec = 0.14f;
        [Tooltip("Janela de buffer se o jogador apertar Espaco logo antes de tocar o chao (s).")]
        public float jumpBufferSec = 0.14f;

        [Header("Impulso Mecanico (Dash dos Propulsores — SEM i-frames)")]
        [Tooltip("Velocidade da rajada de impulso (m/s).")]
        public float dashSpeed = 13.5f;
        [Tooltip("Duracao da rajada do propulsor (s).")]
        public float dashDurationSec = 0.18f;
        [Tooltip("Cooldown entre impulsos (s).")]
        public float dashCooldownSec = 2.0f;
        [Tooltip("Tecla principal do impulso mecanico (Ctrl esquerdo / F).")]
        public KeyCode dashPrimaryKey = KeyCode.LeftControl;
        public KeyCode dashAltKey = KeyCode.F;

        [Header("Escalada de Borda (Ledge Grab / Mantle)")]
        public bool enableLedgeMantle = true;
        public float ledgeDetectDist = 0.62f;
        public float ledgeMinHeight = 0.42f;
        public float ledgeMaxHeight = 1.55f;
        public float mantleDuration = 0.38f;
        public float mantleCooldown = 0.22f;

        [Header("Camera 3a Pessoa (Cinemachine ThirdPersonFollow + Deoccluder)")]
        [Tooltip("Sensibilidade base do mouse (multiplicada pela opcao CameraSensitivity do Menu de Opcoes).")]
        public float baseMouseSensitivity = 2.6f;
        public float minPitchDeg = -18f;
        public float maxPitchDeg = 52f;
        public float pivotHeight = 1.25f;
        [Tooltip("Leve deslocamento sobre o ombro direito (sem mira ADS ainda).")]
        public Vector3 shoulderOffset = new Vector3(0.38f, 0.12f, 0f);
        public float cameraDistance = 3.2f;
        public float minZoomDistance = 2.0f;
        public float maxZoomDistance = 5.2f;
        public Vector3 followDamping = new Vector3(0.12f, 0.16f, 0.14f);
        [Tooltip("Velocidade de alinhamento automatico atras das costas quando o mouse esta parado (graus/s).")]
        public float autoAlignSpeedDeg = 95f;
        public float manualCamPauseSec = 1.6f;
        [Tooltip("Raio de colisao da camera com paredes (CinemachineDeoccluder).")]
        public float wallCollisionRadius = 0.24f;

        [Header("Seguranca Anti-Limbo")]
        public float killFloorY = -3.0f;
    }
}
