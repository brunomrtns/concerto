using System;
using System.Collections;
using Studio.Game.Audio;
using Studio.Game.Menu;
using Studio.Game.Tool;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Combate v2 (#109) — ABSORVER (Parry preciso: 180ms janela, 3s cooldown, custo 0) + Lock-On Suave:
    ///   - Reflete projeteis de volta no atacante + atordoa atacantes corpo-a-corpo (incluindo quebra do Egide!)
    ///   - Juice proprio: freeze 150ms + flash ciano + som invertido/parry clang
    ///   - Lock-on suave: alvo automatico na frente, troca com [Q]/[V], sem travar camera dura
    /// </summary>
    public sealed class ParryAndLockOnController : MonoBehaviour
    {
        public static ParryAndLockOnController Instance { get; private set; }

        [Header("ABSORVER (Parry — DESIGN_BIBLE Secoes 2, 3, 5 e 9)")]
        public KeyCode parryKey = KeyCode.F;
        [Range(0.12f, 0.25f)] public float parryWindowSec = 0.18f;
        public float parryCooldownSec = 3.0f;
        [Range(0.10f, 0.18f)] public float parryFreezeSec = 0.15f;
        public float parryStaggerDurationSec = 2.4f;

        [Header("Lock-On Suave")]
        public KeyCode switchTargetKey = KeyCode.V;
        public float lockOnMaxRange = 18.0f;

        [Header("Audio")]
        public AudioClip parryClangClip;
        public AudioClip parryReverseClip;

        public bool IsParryWindowActive => _parryWindowTimer > 0f;
        public float ParryCooldownRemaining => _parryCooldownTimer;
        public ManifestationEnemyBase CurrentLockTarget { get; private set; }
        public int SuccessfulParryCount { get; private set; }
        public int ReflectedProjectileCount { get; private set; }
        public int LockSwitchCount { get; private set; }

        public event Action<string> OnParrySucceeded;

        private float _parryWindowTimer;
        private float _parryCooldownTimer;
        private AudioSource _audio;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            if (AudioMixController.Instance != null && AudioMixController.Instance.SfxGroup != null)
                _audio.outputAudioMixerGroup = AudioMixController.Instance.SfxGroup;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Time.timeScale > 0.001f && Time.timeScale < 0.99f) Time.timeScale = 1f;
        }

        public bool TryTriggerParry()
        {
            if (_parryCooldownTimer > 0f || _parryWindowTimer > 0f) return false;
            _parryWindowTimer = parryWindowSec;
            _parryCooldownTimer = parryCooldownSec;
            Debug.Log($"[CombatV2] ABSORVER ativado (janela {parryWindowSec * 1000f:F0}ms, cooldown {parryCooldownSec:F1}s).");
            return true;
        }

        public void SelectNextLockTarget()
        {
            var next = CombatGroupCoordinator.FindBestLockTarget(transform, exclude: CurrentLockTarget, maxRange: lockOnMaxRange);
            if (next == null)
                next = CombatGroupCoordinator.FindBestLockTarget(transform, exclude: null, maxRange: lockOnMaxRange);
            if (next != null && next != CurrentLockTarget)
            {
                CurrentLockTarget = next;
                LockSwitchCount++;
            }
        }

        /// <summary>
        /// Chamado quando um ataque melee tenta acertar o Dez: se ABSORVER ativo, anula dano e atordoa o atacante!
        /// </summary>
        public bool TryInterceptMeleeAttack(ManifestationEnemyBase attacker)
        {
            if (!IsParryWindowActive) return false;
            _parryWindowTimer = 0f;
            SuccessfulParryCount++;

            if (attacker != null)
                attacker.ApplyParryStagger(parryStaggerDurationSec);

            PlayParryJuice("melee_parry");
            Debug.Log($"[CombatV2] ABSORVER PERFEITO em ataque melee de {(attacker != null ? attacker.name : "desconhecido")} -> STAGGER {parryStaggerDurationSec:F1}s!");
            return true;
        }

        /// <summary>
        /// Chamado quando um projetil acerta o Dez: se ABSORVER ativo, reflete de volta no atacante!
        /// </summary>
        public bool TryReflectProjectile(ManifestationProjectile proj)
        {
            if (!IsParryWindowActive || proj == null) return false;
            _parryWindowTimer = 0f;
            SuccessfulParryCount++;
            ReflectedProjectileCount++;

            proj.ReflectBack(transform.position);
            PlayParryJuice("projectile_reflect");
            Debug.Log("[CombatV2] ABSORVER PERFEITO em projetil -> REFLETIDO de volta!");
            return true;
        }

        private void PlayParryJuice(string kind)
        {
            if (_audio != null)
            {
                float sc = _audio.outputAudioMixerGroup != null ? 1f : GameSettingsManager.SfxVolume;
                if (parryClangClip != null) _audio.PlayOneShot(parryClangClip, 0.92f * sc);
                if (parryReverseClip != null) _audio.PlayOneShot(parryReverseClip, 0.72f * sc);
            }
            StartCoroutine(RunParryFreeze());
            OnParrySucceeded?.Invoke(kind);
        }

        private IEnumerator RunParryFreeze()
        {
            float prev = Time.timeScale;
            Time.timeScale = 0.03f;
            yield return new WaitForSecondsRealtime(Mathf.Clamp(parryFreezeSec, 0.10f, 0.18f));
            Time.timeScale = prev > 0.05f ? prev : 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_parryWindowTimer > 0f) _parryWindowTimer = Mathf.Max(0f, _parryWindowTimer - dt);
            if (_parryCooldownTimer > 0f) _parryCooldownTimer = Mathf.Max(0f, _parryCooldownTimer - dt);

            if (Time.timeScale <= 0.01f) return;

            if (Input.GetKeyDown(parryKey) || Input.GetMouseButtonDown(2))
                TryTriggerParry();

            if (Input.GetKeyDown(switchTargetKey))
                SelectNextLockTarget();

            // Mantem lock-on suave atualizado automaticamente no inimigo vivo mais proximo na frente
            if (CurrentLockTarget == null || !CurrentLockTarget.IsAlive || Vector3.Distance(transform.position, CurrentLockTarget.transform.position) > lockOnMaxRange)
                CurrentLockTarget = CombatGroupCoordinator.FindBestLockTarget(transform, null, lockOnMaxRange);
        }
    }
}
