using System;
using System.Collections;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Combat
{
    /// <summary>
    /// GameFeelJuiceController — Game Feel Pass ("O Juice do CONCERTO").
    ///
    /// Conformidade exata DESIGN_BIBLE.md:
    ///   - Hitstop: 65ms (golpe leve) / 85ms (golpe pesado) / 95ms (morte inimigo) -> 100% na faixa 50-100ms
    ///   - Camera shake: decay 180ms (golpe) / 220ms (dano direcional) / 240ms (morte) -> 100% < 300ms (respeita ReduceCameraShake)
    ///   - Zero mudanca de balanceamento ou dano; 0 modelos 3D criados
    ///   - GATE 1: Golpe do Dez (hitstop + shake + particula + som sincronizados no MESMO frame)
    ///   - GATE 2: Receber dano (flash no Dez + shake direcional + pausa de input curta 90ms, distinta da morte)
    ///   - GATE 3: Inimigo morto (knockback + hitstop maior 95ms sem travar fluxo)
    ///   - GATE 4: Pulo/aterrissagem (squash & stretch sutil no rig com retorno exato + poeira no chao)
    ///   - GATE 5: Coletar item (3 camadas: visual + som + "clim" animado no HUD)
    /// </summary>
    public sealed class GameFeelJuiceController : MonoBehaviour
    {
        public const float LightHitstopSec      = 0.065f; // 65ms (50-100ms)
        public const float HeavyHitstopSec      = 0.085f; // 85ms (50-100ms)
        public const float EnemyDeathHitstopSec = 0.095f; // 95ms (50-100ms)
        public const float HitShakeDecaySec     = 0.180f; // 180ms (<300ms)
        public const float DamageShakeDecaySec  = 0.220f; // 220ms (<300ms)
        public const float DeathShakeDecaySec   = 0.240f; // 240ms (<300ms)
        public const float DamageInputPauseSec  = 0.090f; // 90ms pausa curta de input (nao confunde com morte)
        public const float DezDamageFlashSec    = 0.140f;
        public const float SquashStretchDurSec  = 0.170f;

        /// <summary>
        /// Flag QA para gravacao lado-a-lado ANTES (sem juice) vs DEPOIS (com juice).
        /// </summary>
        public static bool DisableJuiceForComparison { get; set; }

        public static GameFeelJuiceController Instance { get; private set; }

        // Telemetria para testes Smoke & QA
        public int HitFeedbackCount { get; private set; }
        public int LastHitParticleFrame { get; private set; } = -1;
        public int LastHitSfxFrame { get; private set; } = -1;
        public int LastHitHitstopFrame { get; private set; } = -1;
        public int PlayerDamageFeedbackCount { get; private set; }
        public int EnemyDeathFeedbackCount { get; private set; }
        public int JumpStretchCount { get; private set; }
        public int LandingSquashDustCount { get; private set; }
        public int Pickup3LayerCount { get; private set; }
        public float LastHitstopAppliedSec { get; private set; }
        public float LastShakeDecayAppliedSec { get; private set; }
        public bool IsInputPausedForHit => _inputPauseTimer > 0f;
        public bool IsHudClimVisible => _hudClimGroup != null && _hudClimGroup.alpha > 0.05f;

        private AudioSource _sfxSource;
        private AudioClip _hitPunchClip;
        private AudioClip _pickupChimeClip;
        private AudioClip _landDustClip;

        private ParticleSystem _impactParticles;
        private ParticleSystem _dustParticles;
        private ParticleSystem _pickupParticles;

        private Canvas _hudClimCanvas;
        private CanvasGroup _hudClimGroup;
        private RectTransform _hudClimPill;
        private Text _hudClimText;
        private float _hudClimTimer;

        private float _hitstopRemaining;
        private float _shakeRemaining;
        private float _shakeDuration;
        private float _shakeAmplitude;
        private Vector3 _shakeBiasDir;
        private Vector3 _lastShakeOffset;

        private float _inputPauseTimer;
        private DezLocomotionController _dezLoco;
        private CharacterController _dezCc;
        private Transform _dezRigTarget;
        private Vector3 _dezRigBaseScale = Vector3.one;
        private bool _dezBaseScaleCaptured;
        private bool _dezWasGrounded = true;
        private int _lastJumpSfxCount;
        private float _squashTimer;
        private Vector3 _squashStartScale = Vector3.one;

        private Coroutine _dezFlashRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("Concerto_GameFeelJuice");
            go.AddComponent<GameFeelJuiceController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildProceduralAudio();
            BuildPooledParticles();
            BuildHudClimCanvas();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BindDezInScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RestoreTimeScaleIfHitstop();
            RestoreDezBaseScale();
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RestoreTimeScaleIfHitstop();
            _dezBaseScaleCaptured = false;
            _dezLoco = null;
            _dezCc = null;
            _dezRigTarget = null;
            BindDezInScene();
        }

        private void BindDezInScene()
        {
            var dez = PlayerRegistry.Resolve();
            if (dez == null) return;
            _dezLoco = dez.GetComponent<DezLocomotionController>();
            _dezCc = dez.GetComponent<CharacterController>();
            _dezWasGrounded = _dezCc == null || _dezCc.isGrounded;
            _lastJumpSfxCount = _dezLoco != null ? _dezLoco.JumpSfxTriggerCount : 0;

            var anim = dez.GetComponentInChildren<Animator>();
            _dezRigTarget = anim != null ? anim.transform : dez.transform;
            if (_dezRigTarget != null && !_dezBaseScaleCaptured)
            {
                _dezRigBaseScale = _dezRigTarget.localScale;
                _dezBaseScaleCaptured = true;
            }
        }

        // GATE 1: Golpe do Dez — hitstop 50-100ms + micro shake (<300ms) + particula + som no MESMO frame
        public void TriggerDezAttackHit(Vector3 hitPoint, bool isHeavy)
        {
            if (DisableJuiceForComparison) return;

            float hitstop = isHeavy ? HeavyHitstopSec : LightHitstopSec;
            StartHitstop(hitstop);
            StartCameraShake(isHeavy ? 0.14f : 0.085f, HitShakeDecaySec, Vector3.zero);
            PlayPooledParticles(_impactParticles, hitPoint, isHeavy ? 18 : 12);
            PlayOneShot(_hitPunchClip, isHeavy ? 0.90f : 0.72f, isHeavy ? 0.88f : 1.08f);

            int frame = Time.frameCount;
            LastHitHitstopFrame = frame;
            LastHitParticleFrame = frame;
            LastHitSfxFrame = frame;
            HitFeedbackCount++;
        }

        // GATE 2: Receber dano — flash no Dez + shake direcional (<300ms) + pausa de input curta (90ms, != morte)
        public void TriggerPlayerDamaged(Transform dezTransform, Vector3 hitFromWorldPos, bool isFatal)
        {
            if (DisableJuiceForComparison || isFatal) return;

            Vector3 dir = dezTransform != null ? (dezTransform.position - hitFromWorldPos) : Vector3.back;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.back;
            dir.Normalize();

            StartCameraShake(0.19f, DamageShakeDecaySec, dir);
            _inputPauseTimer = DamageInputPauseSec;
            if (dezTransform != null)
            {
                if (_dezFlashRoutine != null) StopCoroutine(_dezFlashRoutine);
                _dezFlashRoutine = StartCoroutine(RunDezDamageFlash(dezTransform));
            }
            PlayerDamageFeedbackCount++;
        }

        // GATE 3: Inimigo morto — knockback + hitstop maior (95ms, dentro de 50-100ms) sem travar fluxo
        public void TriggerEnemyKilled(Transform enemyTransform, Vector3 knockbackDir)
        {
            if (DisableJuiceForComparison) return;

            StartHitstop(EnemyDeathHitstopSec);
            StartCameraShake(0.16f, DeathShakeDecaySec, Vector3.zero);
            if (enemyTransform != null)
            {
                Vector3 kb = knockbackDir.sqrMagnitude > 0.01f ? knockbackDir.normalized : Vector3.forward;
                kb.y = 0f;
                enemyTransform.position += kb * 0.55f;
                PlayPooledParticles(_impactParticles, enemyTransform.position + Vector3.up * 0.85f, 24);
            }
            PlayOneShot(_hitPunchClip, 0.95f, 0.72f);
            EnemyDeathFeedbackCount++;
        }

        // GATE 5: Coletar item — 3 camadas: visual + som + "clim" animado do HUD
        public void TriggerItemPickupJuice(Vector3 worldPos, string itemLabel = "PECA COLETADA")
        {
            if (DisableJuiceForComparison) return;

            // Camada 1: Visual (particulas douradas/ciano no ponto do item)
            PlayPooledParticles(_pickupParticles, worldPos + Vector3.up * 0.4f, 22);

            // Camada 2: Som (chime harmonico)
            PlayOneShot(_pickupChimeClip, 0.85f, 1.0f);

            // Camada 3: "Clim" animado do HUD
            if (_hudClimGroup != null && _hudClimText != null)
            {
                _hudClimText.text = $"+1  {itemLabel.ToUpperInvariant()}!";
                _hudClimGroup.alpha = 1f;
                _hudClimTimer = 1.45f;
                if (_hudClimPill != null) _hudClimPill.localScale = Vector3.one * 1.18f;
            }
            Pickup3LayerCount++;
        }

        // GATE 4: Pulo/aterrissagem — squash & stretch sutil + poeira no chao
        public void TriggerJumpStretch()
        {
            if (DisableJuiceForComparison || _dezRigTarget == null) return;
            _squashStartScale = new Vector3(_dezRigBaseScale.x * 0.91f, _dezRigBaseScale.y * 1.12f, _dezRigBaseScale.z * 0.91f);
            _dezRigTarget.localScale = _squashStartScale;
            _squashTimer = SquashStretchDurSec;
            JumpStretchCount++;
        }

        public void TriggerLandingSquashAndDust(Vector3 feetPos)
        {
            if (DisableJuiceForComparison) return;
            if (_dezRigTarget != null)
            {
                _squashStartScale = new Vector3(_dezRigBaseScale.x * 1.13f, _dezRigBaseScale.y * 0.86f, _dezRigBaseScale.z * 1.13f);
                _dezRigTarget.localScale = _squashStartScale;
                _squashTimer = SquashStretchDurSec;
            }
            PlayPooledParticles(_dustParticles, feetPos + Vector3.up * 0.05f, 14);
            PlayOneShot(_landDustClip, 0.55f, 1.0f);
            LandingSquashDustCount++;
        }

        private void Update()
        {
            float udt = Time.unscaledDeltaTime;

            // Hitstop controlado sem travar fluxo
            if (_hitstopRemaining > 0f)
            {
                _hitstopRemaining -= udt;
                if (_hitstopRemaining <= 0f)
                    RestoreTimeScaleIfHitstop();
            }

            // Pausa curta de input ao receber dano (90ms)
            if (_inputPauseTimer > 0f)
                _inputPauseTimer = Mathf.Max(0f, _inputPauseTimer - udt);

            // Monitora pulo e aterrissagem do Dez (GATE 4)
            if (_dezLoco == null) BindDezInScene();
            if (_dezLoco != null && _dezCc != null)
            {
                if (_dezLoco.JumpSfxTriggerCount > _lastJumpSfxCount)
                {
                    _lastJumpSfxCount = _dezLoco.JumpSfxTriggerCount;
                    TriggerJumpStretch();
                }

                bool grounded = _dezCc.isGrounded;
                if (grounded && !_dezWasGrounded)
                    TriggerLandingSquashAndDust(_dezLoco.transform.position);
                _dezWasGrounded = grounded;
            }

            // Retorno suave e exato do squash & stretch para _dezRigBaseScale (nunca deforma malha!)
            if (_squashTimer > 0f && _dezRigTarget != null)
            {
                _squashTimer -= udt;
                float t = 1f - Mathf.Clamp01(_squashTimer / SquashStretchDurSec);
                _dezRigTarget.localScale = Vector3.Lerp(_squashStartScale, _dezRigBaseScale, t);
                if (_squashTimer <= 0f) _dezRigTarget.localScale = _dezRigBaseScale;
            }

            // Animacao do "clim" do HUD (GATE 5)
            if (_hudClimTimer > 0f && _hudClimGroup != null)
            {
                _hudClimTimer -= udt;
                if (_hudClimPill != null)
                    _hudClimPill.localScale = Vector3.Lerp(_hudClimPill.localScale, Vector3.one, Mathf.Clamp01(udt * 10f));
                if (_hudClimTimer < 0.35f)
                    _hudClimGroup.alpha = Mathf.Clamp01(_hudClimTimer / 0.35f);
                if (_hudClimTimer <= 0f)
                    _hudClimGroup.alpha = 0f;
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Remove offset do frame anterior antes de aplicar novo micro-shake
            if (_lastShakeOffset.sqrMagnitude > 0f)
            {
                cam.transform.localPosition -= _lastShakeOffset;
                _lastShakeOffset = Vector3.zero;
            }

            if (_shakeRemaining > 0f)
            {
                _shakeRemaining -= Time.unscaledDeltaTime;
                float decay = Mathf.Clamp01(_shakeRemaining / Mathf.Max(0.01f, _shakeDuration));
                float amp = _shakeAmplitude * decay * GameSettingsManager.CameraShakeScale;
                Vector3 rand = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-0.7f, 0.7f),
                    0f) * amp;
                Vector3 dirKick = _shakeBiasDir * (amp * 0.65f);
                _lastShakeOffset = rand + dirKick;
                cam.transform.localPosition += _lastShakeOffset;
            }
        }

        private void StartHitstop(float durationSec)
        {
            float clamped = Mathf.Clamp(durationSec, 0.050f, 0.100f); // DESIGN_BIBLE: 50-100ms
            LastHitstopAppliedSec = clamped;
            if (Time.timeScale > 0.05f)
                Time.timeScale = 0.04f;
            _hitstopRemaining = Mathf.Max(_hitstopRemaining, clamped);
        }

        private void RestoreTimeScaleIfHitstop()
        {
            if (_hitstopRemaining <= 0f && Time.timeScale > 0f && Time.timeScale < 0.1f)
                Time.timeScale = 1f;
            _hitstopRemaining = 0f;
        }

        private void RestoreDezBaseScale()
        {
            if (_dezBaseScaleCaptured && _dezRigTarget != null)
                _dezRigTarget.localScale = _dezRigBaseScale;
        }

        private void StartCameraShake(float amplitude, float decaySec, Vector3 biasDir)
        {
            float clampedDecay = Mathf.Min(decaySec, 0.28f); // DESIGN_BIBLE: < 300ms
            LastShakeDecayAppliedSec = clampedDecay;
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
            _shakeDuration = clampedDecay;
            _shakeRemaining = clampedDecay;
            _shakeBiasDir = biasDir;
        }

        private IEnumerator RunDezDamageFlash(Transform dezTransform)
        {
            var renderers = dezTransform.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            var mpb = new MaterialPropertyBlock();
            Color flashColor = new Color(1.0f, 0.55f, 0.25f, 1f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", flashColor * 2.2f);
                renderers[i].SetPropertyBlock(mpb);
            }

            yield return new WaitForSecondsRealtime(DezDamageFlashSec);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].SetPropertyBlock(null);
            }
        }

        private void PlayPooledParticles(ParticleSystem ps, Vector3 pos, int count)
        {
            if (ps == null) return;
            ps.transform.position = pos;
            ps.Emit(count);
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (_sfxSource == null || clip == null) return;
            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, volume * GameSettingsManager.SfxVolume);
        }

        private void BuildProceduralAudio()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            _hitPunchClip   = CreateSynthClip("Juice_HitPunch", 0.12f, 150f, 55f, true);
            _pickupChimeClip = CreateSynthClip("Juice_PickupChime", 0.24f, 660f, 990f, false);
            _landDustClip   = CreateSynthClip("Juice_LandDust", 0.10f, 110f, 60f, true);
        }

        private static AudioClip CreateSynthClip(string name, float durSec, float f0, float f1, bool noisy)
        {
            int sr = 22050;
            int n = Mathf.Max(64, Mathf.RoundToInt(sr * durSec));
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = (1f - t) * (1f - t);
                float freq = Mathf.Lerp(f0, f1, t);
                float sample = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sr));
                if (noisy) sample = sample * 0.65f + (((i * 1103515245 + 12345) & 0x7fff) / 32767f - 0.5f) * 0.35f;
                data[i] = sample * env * 0.55f;
            }
            var clip = AudioClip.Create(name, n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void BuildPooledParticles()
        {
            _impactParticles = CreateParticlePool("Juice_ImpactSparks", new Color(1.0f, 0.82f, 0.28f, 1f), 4.5f, 0.24f, 0.12f);
            _dustParticles   = CreateParticlePool("Juice_LandingDust",  new Color(0.82f, 0.80f, 0.74f, 0.7f), 2.2f, 0.35f, 0.20f);
            _pickupParticles = CreateParticlePool("Juice_PickupBurst",  new Color(0.28f, 0.95f, 1.0f, 1f), 3.6f, 0.42f, 0.16f);
        }

        private ParticleSystem CreateParticlePool(string name, Color color, float speed, float lifetime, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;
            return ps;
        }

        private void BuildHudClimCanvas()
        {
            var go = new GameObject("GameFeel_HudClimCanvas");
            go.transform.SetParent(transform, false);
            _hudClimCanvas = go.AddComponent<Canvas>();
            _hudClimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hudClimCanvas.sortingOrder = 165;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var pill = new GameObject("PickupClimPill");
            pill.transform.SetParent(go.transform, false);
            var img = pill.AddComponent<Image>();
            img.color = new Color(0.06f, 0.14f, 0.20f, 0.90f);
            img.raycastTarget = false;
            _hudClimGroup = pill.AddComponent<CanvasGroup>();
            _hudClimGroup.alpha = 0f;
            _hudClimGroup.blocksRaycasts = false;
            _hudClimPill = pill.GetComponent<RectTransform>();
            _hudClimPill.anchorMin = new Vector2(0.5f, 1f);
            _hudClimPill.anchorMax = new Vector2(0.5f, 1f);
            _hudClimPill.pivot = new Vector2(0.5f, 1f);
            _hudClimPill.anchoredPosition = new Vector2(0f, -84f);
            _hudClimPill.sizeDelta = new Vector2(440f, 52f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(pill.transform, false);
            _hudClimText = txtGo.AddComponent<Text>();
            _hudClimText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hudClimText.fontSize = 24;
            _hudClimText.fontStyle = FontStyle.Bold;
            _hudClimText.alignment = TextAnchor.MiddleCenter;
            _hudClimText.color = new Color(0.32f, 0.98f, 0.80f, 1f);
            _hudClimText.raycastTarget = false;
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }
    }
}
