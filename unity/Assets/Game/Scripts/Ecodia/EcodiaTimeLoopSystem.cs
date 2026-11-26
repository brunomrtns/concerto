using UnityEngine;

namespace Studio.Game.Ecodia
{
    /// <summary>
    /// GATE 1 (Ecodia — GDD 03 §2.4): Loop deterministico de 6.0s em todo o distrito Desincronizado.
    ///   - Bonde Art Deco repassa na mesma trajetoria a cada 6s (plataforma-puzzle previsivel).
    ///   - Porta relojoeira abre exatamente em t = 2.0s (janela 2.0s..4.5s).
    ///   - Anuncio neon e moradores repetem gestos no ciclo de 6s.
    ///   - O jogador e o unico fora do tempo; ao consertar Ecodia, o loop quebra e os moradores acordam.
    /// </summary>
    public sealed class EcodiaTimeLoopSystem : MonoBehaviour
    {
        public const float LoopDurationSec = 6.0f;
        public const float TimedDoorOpenAtSec = 2.0f;
        public const float TimedDoorCloseAtSec = 4.5f;

        public static EcodiaTimeLoopSystem Instance { get; private set; }

        [Header("Objetos Previsiveis do Loop de 6s")]
        public Transform tramTransform;
        public Vector3 tramStartPos = new Vector3(-18f, 0.8f, 64f);
        public Vector3 tramEndPos   = new Vector3( 18f, 0.8f, 64f);

        public Transform timedDoorTransform;
        public Vector3 timedDoorClosedPos = new Vector3(0f, 2.0f, 88f);
        public Vector3 timedDoorOpenPos   = new Vector3(0f, 5.8f, 88f);

        public Transform[] loopingNpcTransforms = new Transform[3];

        public bool IsLoopActive { get; private set; } = true;
        public float LoopTime { get; private set; }
        public int LoopCycleCount { get; private set; }
        public bool IsTimedDoorOpen => !IsLoopActive || (LoopTime >= TimedDoorOpenAtSec && LoopTime <= TimedDoorCloseAtSec);
        public string CurrentAnnouncementText => !IsLoopActive
            ? "Ecodia desperta: o relógio voltou a correr."
            : (LoopTime < 3.0f
                ? "[00:00-00:03] Bonde das Seis chegando à Praça Neon..."
                : "[00:03-00:06] Atenção passageiros: portas travando em 3, 2, 1...");

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
            if (!IsLoopActive) return;
            AdvanceLoop(Time.deltaTime);
        }

        public void AdvanceLoop(float dt)
        {
            if (!IsLoopActive) return;
            LoopTime += Mathf.Max(0f, dt);
            while (LoopTime >= LoopDurationSec)
            {
                LoopTime -= LoopDurationSec;
                LoopCycleCount++;
            }
            ApplyLoopStateAtTime(LoopTime);
        }

        public void SetLoopTimeForTest(float t)
        {
            LoopTime = Mathf.Repeat(t, LoopDurationSec);
            ApplyLoopStateAtTime(LoopTime);
        }

        public void ApplyLoopStateAtTime(float t)
        {
            float u = Mathf.Clamp01(t / LoopDurationSec);
            if (tramTransform != null)
                tramTransform.position = Vector3.Lerp(tramStartPos, tramEndPos, u);

            if (timedDoorTransform != null)
                timedDoorTransform.position = IsTimedDoorOpen ? timedDoorOpenPos : timedDoorClosedPos;

            if (loopingNpcTransforms != null)
            {
                float waveAngle = Mathf.Sin(u * Mathf.PI * 2f) * 32f;
                for (int i = 0; i < loopingNpcTransforms.Length; i++)
                {
                    if (loopingNpcTransforms[i] != null)
                        loopingNpcTransforms[i].localRotation = Quaternion.Euler(0f, waveAngle + i * 25f, 0f);
                }
            }
        }

        public void BreakLoopAndWakeDistrict()
        {
            IsLoopActive = false;
            if (timedDoorTransform != null)
                timedDoorTransform.position = timedDoorOpenPos;
            if (loopingNpcTransforms != null)
            {
                for (int i = 0; i < loopingNpcTransforms.Length; i++)
                {
                    if (loopingNpcTransforms[i] != null)
                        loopingNpcTransforms[i].localRotation = Quaternion.identity;
                }
            }
        }

        public void RestoreLoopState(bool loopActive)
        {
            IsLoopActive = loopActive;
            if (loopActive)
                ApplyLoopStateAtTime(LoopTime);
            else
                BreakLoopAndWakeDistrict();
        }
    }
}
