using UnityEngine;

namespace Studio.Game.Collision
{
    /// <summary>
    /// CollisionMatrixBootstrap — Fonte unica de verdade das camadas fisicas de CONCERTO (DESIGN_BIBLE Secao 2 & 8.1):
    ///   - Layer 6 : NPC
    ///   - Layer 7 : Projectile
    ///   - Layer 9 : Player
    ///   - Layer 12: CutsceneActor
    ///   - Layer 13: CameraBlocker
    ///   - Layer 16: Environment
    ///   - Layer 23: Enemy
    ///   - Layer 25: Props
    /// Aplica automaticamente a matriz de colisao no boot antes de qualquer cena carregar.
    /// </summary>
    public static class CollisionMatrixBootstrap
    {
        public const int LayerDefault       = 0;
        public const int LayerNPC           = 6;
        public const int LayerProjectile    = 7;
        public const int LayerPlayer        = 9;
        public const int LayerCutsceneActor = 12;
        public const int LayerCameraBlocker = 13;
        public const int LayerEnvironment   = 16;
        public const int LayerEnemy         = 23;
        public const int LayerProps         = 25;

        public static int SolidWorldMask =>
            (1 << LayerDefault) | (1 << LayerEnvironment) | (1 << LayerProps) | (1 << LayerNPC) | (1 << LayerEnemy);

        public static int ProjectileHitMask =>
            (1 << LayerDefault) | (1 << LayerEnvironment) | (1 << LayerProps) | (1 << LayerEnemy);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyMatrix()
        {
            // 1. Player colide com cenario, props, NPCs e inimigos; ignora projeteis proprios, cutscene actors e camera blockers
            SetCollision(LayerPlayer, LayerDefault,       true);
            SetCollision(LayerPlayer, LayerEnvironment,   true);
            SetCollision(LayerPlayer, LayerProps,         true);
            SetCollision(LayerPlayer, LayerNPC,           true);
            SetCollision(LayerPlayer, LayerEnemy,         true);
            SetCollision(LayerPlayer, LayerProjectile,    false);
            SetCollision(LayerPlayer, LayerCutsceneActor, false);
            SetCollision(LayerPlayer, LayerCameraBlocker, false);

            // 2. NPC colide com mundo, player, outros NPCs e inimigos; ignora projeteis e cutscene actors
            SetCollision(LayerNPC, LayerDefault,       true);
            SetCollision(LayerNPC, LayerEnvironment,   true);
            SetCollision(LayerNPC, LayerProps,         true);
            SetCollision(LayerNPC, LayerNPC,           true);
            SetCollision(LayerNPC, LayerEnemy,         true);
            SetCollision(LayerNPC, LayerProjectile,    false);
            SetCollision(LayerNPC, LayerCutsceneActor, false);
            SetCollision(LayerNPC, LayerCameraBlocker, false);

            // 3. Enemy colide com mundo, player, NPC, outros Enemies e Projectile
            SetCollision(LayerEnemy, LayerDefault,       true);
            SetCollision(LayerEnemy, LayerEnvironment,   true);
            SetCollision(LayerEnemy, LayerProps,         true);
            SetCollision(LayerEnemy, LayerEnemy,         true);
            SetCollision(LayerEnemy, LayerProjectile,    true);
            SetCollision(LayerEnemy, LayerCutsceneActor, false);
            SetCollision(LayerEnemy, LayerCameraBlocker, false);

            // 4. Projectile colide com Environment, Props, Enemy, Default; ignora outros projeteis, Player, NPC, CutsceneActor
            SetCollision(LayerProjectile, LayerDefault,       true);
            SetCollision(LayerProjectile, LayerEnvironment,   true);
            SetCollision(LayerProjectile, LayerProps,         true);
            SetCollision(LayerProjectile, LayerProjectile,    false);
            SetCollision(LayerProjectile, LayerCutsceneActor, false);
            SetCollision(LayerProjectile, LayerCameraBlocker, false);

            // 5. CutsceneActor ignora tudo fisico (movimento 100% coreografado — DESIGN_BIBLE Secao 4.4)
            SetCollision(LayerCutsceneActor, LayerDefault,       false);
            SetCollision(LayerCutsceneActor, LayerEnvironment,   false);
            SetCollision(LayerCutsceneActor, LayerProps,         false);
            SetCollision(LayerCutsceneActor, LayerCutsceneActor, false);
            SetCollision(LayerCutsceneActor, LayerCameraBlocker, false);

            // 6. CameraBlocker bloqueia apenas a camera (nunca atores fisicos)
            SetCollision(LayerCameraBlocker, LayerDefault,       false);
            SetCollision(LayerCameraBlocker, LayerEnvironment,   false);
            SetCollision(LayerCameraBlocker, LayerProps,         false);
            SetCollision(LayerCameraBlocker, LayerCameraBlocker, false);
        }

        private static void SetCollision(int layerA, int layerB, bool collide)
        {
            Physics.IgnoreLayerCollision(layerA, layerB, !collide);
        }
    }
}
