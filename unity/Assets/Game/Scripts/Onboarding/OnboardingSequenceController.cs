using System;
using System.Collections.Generic;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Cutscenes;
using Studio.Game.Powers;
using Studio.Game.Save;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Onboarding
{
    public enum OnboardingStep
    {
        None = -1,
        Move = 0,
        Jump = 1,
        Interact = 2,
        EcoTemporal = 3,
        Estase = 4,
        Completed = 5
    }

    /// <summary>
    /// Terminal de energia diegetico usado no Passo 3 (Interagir) do Onboarding.
    /// Implementa IInteractable (Studio.Gameplay) para integracao com PlayerInteractor e tecla [E] / Gamepad [X].
    /// </summary>
    public sealed class OnboardingEnergyTerminal : InteractableBase
    {
        public Renderer coreRenderer;

        public override bool CanInteract(GameObject interactor)
        {
            return base.CanInteract(interactor);
        }

        public override void Interact(GameObject interactor)
        {
            base.Interact(interactor);
            if (coreRenderer != null && coreRenderer.material != null)
            {
                var mat = coreRenderer.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 1.0f, 0.72f, 1f));
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.2f, 2.2f, 1.4f, 1f));
            }
        }
    }

    /// <summary>
    /// OnboardingSequenceController — Primeiros 15 Minutos Diegeticos de CONCERTO.
    ///
    /// Conformidade DESIGN_BIBLE.md:
    ///   - Ordem canonica (GATE 1): Mover -> Pular (coyote ~100ms / buffer ~150ms) -> Interagir -> Eco Temporal -> Estase
    ///   - 100% diegetico: zero paredes de texto (maximo 1 linha curta por passo)
    ///   - Telegraph visual >= 300ms (450ms) em cada estacao antes de ativar o desafio
    ///   - Impossivel avancar sem aprender: barreira fisica diegetica bloqueia passagem ate completar ou pular
    ///   - Skippavel (GATE 2): pular passo atual apos 3s ([Tab]) e pular sequencia inteira ([Segure Esc/Backspace] apos 3s)
    ///   - Persistencia 100% no SaveSystem JSON versionado em persistentDataPath (ZERO PlayerPrefs)
    ///   - 0 modelos 3D novos criados (geometria diegetica procedural URP Lit)
    /// </summary>
    public sealed class OnboardingSequenceController : MonoBehaviour
    {
        public const float TelegraphDurationSec = 0.45f; // >= 300ms (DESIGN_BIBLE)
        public const float StepSkipDelaySec = 3.0f;      // skip disponivel apos 3s (DESIGN_BIBLE)
        public const float SkipAllHoldSec = 0.85f;
        public const float PlateauHeightM = 0.62f;       // > stepOffset (0.36m), exige pulo real (< jumpHeight 1.35m)

        public static OnboardingSequenceController Instance { get; private set; }

        public OnboardingStep CurrentStep { get; private set; } = OnboardingStep.None;
        public bool IsActive { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool WasSkipped { get; private set; }
        public bool IsWaitingForCutscene { get; private set; }
        public float CurrentStepElapsedSec => _stepElapsed;
        public float TotalElapsedSec => _totalElapsed;
        public bool TelegraphCompletedForCurrentStep => _telegraphTimer >= TelegraphDurationSec;
        public bool CanSkipCurrentStep => IsActive && !IsWaitingForCutscene && _stepElapsed >= StepSkipDelaySec;
        public bool CanSkipWholeSequence => IsActive && !IsWaitingForCutscene && _totalElapsed >= StepSkipDelaySec;
        public int CurrentPromptLineCount => string.IsNullOrEmpty(CurrentPromptText) ? 0 : CurrentPromptText.Split('\n').Length;
        public string CurrentPromptText { get; private set; } = string.Empty;
        public Transform JumpPlateauTransform => _jumpPlateauTransform;

        public IReadOnlyDictionary<OnboardingStep, float> StepDurationsSec => _stepDurations;
        public IReadOnlyList<string> CompletedStepIds => _completedStepIds;

        public event Action<OnboardingStep> OnStepStarted;
        public event Action<OnboardingStep> OnStepCompleted;
        public event Action<bool> OnOnboardingFinished; // bool = wasSkipped

        private readonly Dictionary<OnboardingStep, float> _stepDurations = new Dictionary<OnboardingStep, float>();
        private readonly List<string> _completedStepIds = new List<string>(5);
        private readonly List<Material> _ownedMaterials = new List<Material>(16);

        private Transform _worldRoot;
        private readonly GameObject[] _stationRoots = new GameObject[5];
        private readonly GameObject[] _barriers = new GameObject[5];
        private readonly Renderer[] _beaconRenderers = new Renderer[5];
        private readonly Transform[] _telegraphRings = new Transform[5];
        private readonly Vector3[] _stationPositions = new Vector3[5];

        private Transform _jumpPlateauTransform;
        private OnboardingEnergyTerminal _terminal;
        private Transform _stasisDoorBlade;
        private Vector3 _stasisDoorBaseLocalPos;
        private bool _stasisDoorLockedOpen;

        private DezLocomotionController _dezLoco;
        private DezPowersController _dezPowers;
        private CutsceneManager _cs01;

        private Canvas _hudCanvas;
        private CanvasGroup _hudGroup;
        private Text _promptLineText;
        private Text _skipHintText;
        private Image _accentBar;

        private float _stepElapsed;
        private float _totalElapsed;
        private float _telegraphTimer;
        private float _skipAllHoldTimer;
        private int _jumpCountAtStepStart;
        private bool _powersBound;
        private Vector3 _corridorOrigin;
        private Vector3 _corridorDir;

        private static bool _sceneHookInstalled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneBootstrap()
        {
            if (!_sceneHookInstalled)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _sceneHookInstalled = true;
            }
            EnsureControllerInCaldeiral(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureControllerInCaldeiral(scene);
        }

        private static void EnsureControllerInCaldeiral(Scene scene)
        {
            if (!scene.IsValid() || scene.name != SaveSystem.DefaultGameScene)
                return;

            if (Instance != null) return;
            var go = new GameObject("OnboardingSequence_Controller");
            go.AddComponent<OnboardingSequenceController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CacheReferences();

            // Ativa automaticamente somente ao iniciar Novo Jogo a partir do MainMenu
            if (SaveSystem.ConsumePendingNewGameOnboarding())
            {
                StartOnboarding(skipCutsceneIfPlaying: false);
                return;
            }

            // Se ha save existente, reflete o estado salvo sem interferir na cena
            var saved = SaveSystem.CachedState;
            if (saved != null)
            {
                RestoreFromSave(
                    saved.onboardingCompleted,
                    saved.onboardingSkipped,
                    saved.onboardingStepIndex,
                    saved.completedOnboardingSteps);
            }
        }

        private void OnDestroy()
        {
            UnbindPowers();
            CleanupWorldGeometry();
            if (_hudCanvas != null) Destroy(_hudCanvas.gameObject);
            if (Instance == this) Instance = null;
        }

        private void CacheReferences()
        {
            var dez = PlayerRegistry.Resolve();
            if (dez != null)
            {
                _dezLoco = dez.GetComponent<DezLocomotionController>();
                _dezPowers = dez.GetComponent<DezPowersController>();
            }
            if (_dezLoco == null) _dezLoco = FindAnyObjectByType<DezLocomotionController>();
            if (_dezPowers == null) _dezPowers = FindAnyObjectByType<DezPowersController>();
            if (_cs01 == null) _cs01 = FindAnyObjectByType<CutsceneManager>();
        }

        public void CopyCompletedStepsTo(List<string> destination)
        {
            if (destination == null) return;
            for (int i = 0; i < _completedStepIds.Count; i++)
            {
                if (!destination.Contains(_completedStepIds[i]))
                    destination.Add(_completedStepIds[i]);
            }
        }

        public void RestoreFromSave(bool completed, bool skipped, int stepIndex, List<string> completedSteps)
        {
            IsCompleted = completed;
            WasSkipped = skipped;
            _completedStepIds.Clear();
            if (completedSteps != null)
            {
                for (int i = 0; i < completedSteps.Count; i++)
                {
                    if (!string.IsNullOrEmpty(completedSteps[i]) && !_completedStepIds.Contains(completedSteps[i]))
                        _completedStepIds.Add(completedSteps[i]);
                }
            }

            if (completed)
            {
                CurrentStep = OnboardingStep.Completed;
                IsActive = false;
                IsWaitingForCutscene = false;
                CleanupWorldGeometry();
                SetHudVisible(false);
            }
            else if (stepIndex >= 0 && stepIndex < (int)OnboardingStep.Completed)
            {
                CurrentStep = (OnboardingStep)stepIndex;
            }
        }

        /// <summary>
        /// Inicia a sequencia diegetica de 5 passos do zero.
        /// </summary>
        public void StartOnboarding(bool skipCutsceneIfPlaying = false)
        {
            CacheReferences();

            if (skipCutsceneIfPlaying && _cs01 != null && _cs01.IsPlaying)
            {
                _cs01.SkipCutscene();
            }

            IsCompleted = false;
            WasSkipped = false;
            IsActive = true;
            _totalElapsed = 0f;
            _stepElapsed = 0f;
            _telegraphTimer = 0f;
            _skipAllHoldTimer = 0f;
            _stepDurations.Clear();
            _completedStepIds.Clear();
            _stasisDoorLockedOpen = false;

            BuildHudIfNeeded();

            if (_cs01 != null && _cs01.IsPlaying)
            {
                IsWaitingForCutscene = true;
                CurrentStep = OnboardingStep.None;
                SetHudVisible(false);
                return;
            }

            BeginCorridorAndFirstStep();
        }

        private void BeginCorridorAndFirstStep()
        {
            IsWaitingForCutscene = false;
            CacheReferences();
            BuildDiegeticCorridor();
            BindPowers();
            EnterStep(OnboardingStep.Move);
        }

        private void Update()
        {
            if (!IsActive) return;

            if (IsWaitingForCutscene)
            {
                if (_cs01 == null || !_cs01.IsPlaying)
                {
                    BeginCorridorAndFirstStep();
                }
                return;
            }

            float dt = Time.deltaTime;
            _totalElapsed += dt;
            _stepElapsed += dt;
            _telegraphTimer += dt;

            UpdateVisualTelegraphsAndDoor(dt);
            UpdateSkipInputs(dt);
            EvaluateCurrentStepCondition();
            UpdateHudPrompt();
        }

        private void UpdateSkipInputs(float dt)
        {
            // GATE 2: Pular passo atual apos 3s ([Tab] ou Gamepad Back)
            if (CanSkipCurrentStep && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6)))
            {
                SkipCurrentStep();
                return;
            }

            // GATE 2: Pular sequencia inteira apos 3s ([Segure Esc] ou [Segure Backspace])
            if (CanSkipWholeSequence && (Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Backspace)))
            {
                _skipAllHoldTimer += dt;
                if (_skipAllHoldTimer >= SkipAllHoldSec)
                {
                    SkipAllOnboarding();
                    return;
                }
            }
            else
            {
                _skipAllHoldTimer = 0f;
            }
        }

        private void EvaluateCurrentStepCondition()
        {
            if (_dezLoco == null) return;
            // Exige telegraph >= 300ms antes de aceitar conclusao do passo
            if (!TelegraphCompletedForCurrentStep) return;

            Vector3 dezPos = _dezLoco.transform.position;

            switch (CurrentStep)
            {
                case OnboardingStep.Move:
                {
                    Vector3 target = _stationPositions[0];
                    float distXZ = Vector2.Distance(new Vector2(dezPos.x, dezPos.z), new Vector2(target.x, target.z));
                    if (distXZ <= 1.35f)
                        CompleteCurrentStep(skipped: false);
                    break;
                }
                case OnboardingStep.Jump:
                {
                    Vector3 plateau = _stationPositions[1];
                    float distXZ = Vector2.Distance(new Vector2(dezPos.x, dezPos.z), new Vector2(plateau.x, plateau.z));
                    float topY = _corridorOrigin.y + PlateauHeightM;
                    bool jumped = _dezLoco.JumpSfxTriggerCount > _jumpCountAtStepStart;
                    bool onPlateau = distXZ <= 1.65f && dezPos.y >= topY - 0.24f;
                    if (onPlateau || (jumped && distXZ <= 2.25f))
                        CompleteCurrentStep(skipped: false);
                    break;
                }
                case OnboardingStep.Interact:
                {
                    if (_terminal != null)
                    {
                        float dist = Vector3.Distance(dezPos, _terminal.transform.position);
                        if (dist <= 2.6f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton2)))
                        {
                            _terminal.Interact(_dezLoco.gameObject);
                        }
                    }
                    break;
                }
                case OnboardingStep.EcoTemporal:
                {
                    // Conclusao disparada via evento OnEcoActivated do DezPowersController
                    break;
                }
                case OnboardingStep.Estase:
                {
                    // Conclusao disparada via evento OnStasisActivated do DezPowersController
                    break;
                }
            }
        }

        public Vector3 GetStationPosition(OnboardingStep step)
        {
            int idx = (int)step;
            if (idx < 0 || idx >= _stationPositions.Length) return _corridorOrigin;
            return _stationPositions[idx];
        }

        public bool IsBarrierActive(OnboardingStep step)
        {
            int idx = (int)step;
            if (idx < 0 || idx >= _barriers.Length) return false;
            return _barriers[idx] != null && _barriers[idx].activeSelf;
        }

        /// <summary>
        /// Permite acionar o terminal do Passo 3 via input/teste mantendo o contrato IInteractable real.
        /// </summary>
        public bool TryInteractCurrentTerminal()
        {
            if (!IsActive || CurrentStep != OnboardingStep.Interact || _terminal == null || _dezLoco == null)
                return false;
            if (!TelegraphCompletedForCurrentStep) return false;
            _terminal.Interact(_dezLoco.gameObject);
            return true;
        }

        /// <summary>
        /// GATE 2: Pula o passo atual (somente apos 3s, salvo bypass de debug explicitado).
        /// </summary>
        public bool SkipCurrentStep(bool forceIgnoreTimer = false)
        {
            if (!IsActive || IsWaitingForCutscene) return false;
            if (!forceIgnoreTimer && !CanSkipCurrentStep) return false;
            CompleteCurrentStep(skipped: true);
            return true;
        }

        /// <summary>
        /// GATE 2: Pula toda a sequencia de onboarding de forma limpa, removendo barreiras e salvando estado.
        /// </summary>
        public bool SkipAllOnboarding(bool forceIgnoreTimer = false)
        {
            if (!IsActive) return false;
            if (!forceIgnoreTimer && !IsWaitingForCutscene && !CanSkipWholeSequence) return false;

            if (CurrentStep >= OnboardingStep.Move && CurrentStep < OnboardingStep.Completed)
                _stepDurations[CurrentStep] = _stepElapsed;

            WasSkipped = true;
            FinishOnboarding(wasSkipped: true);
            return true;
        }

        private void EnterStep(OnboardingStep step)
        {
            CurrentStep = step;
            _stepElapsed = 0f;
            _telegraphTimer = 0f;

            if (step == OnboardingStep.Jump && _dezLoco != null)
            {
                _jumpCountAtStepStart = _dezLoco.JumpSfxTriggerCount;
            }
            else if (step == OnboardingStep.EcoTemporal && _dezPowers != null && !_dezPowers.IsUnlocked)
            {
                // Desbloqueia Eco Temporal + Estase no momento diegetico da estacao de Eco
                _dezPowers.UnlockPowers(showHint: false);
            }

            CurrentPromptText = GetSingleLinePrompt(step);
            SetHudVisible(true);
            UpdateHudPrompt();

            if (AudioMixController.Instance != null)
                AudioMixController.Instance.PlayUiConfirm();

            OnStepStarted?.Invoke(step);
            Debug.Log($"[Onboarding] Passo iniciado: {step} | Prompt: \"{CurrentPromptText}\"");
        }

        private void CompleteCurrentStep(bool skipped)
        {
            if (CurrentStep < OnboardingStep.Move || CurrentStep >= OnboardingStep.Completed)
                return;

            OnboardingStep finished = CurrentStep;
            _stepDurations[finished] = _stepElapsed;

            string stepId = GetStepId(finished);
            if (!_completedStepIds.Contains(stepId))
                _completedStepIds.Add(stepId);

            // Dissolve a barreira fisica deste passo
            int idx = (int)finished;
            if (idx >= 0 && idx < _barriers.Length && _barriers[idx] != null)
                _barriers[idx].SetActive(false);

            if (idx >= 0 && idx < _beaconRenderers.Length && _beaconRenderers[idx] != null)
                SetMaterialColor(_beaconRenderers[idx].material, new Color(0.22f, 0.95f, 0.62f, 0.9f), new Color(0.25f, 2.0f, 1.2f, 1f));

            if (finished == OnboardingStep.Estase)
                _stasisDoorLockedOpen = true;

            if (AudioMixController.Instance != null)
                AudioMixController.Instance.PlayUiConfirm();

            OnStepCompleted?.Invoke(finished);
            Debug.Log($"[Onboarding] Passo concluido: {finished} (skipped={skipped}, duracao={_stepElapsed:F2}s)");

            OnboardingStep next = finished + 1;
            if (next >= OnboardingStep.Completed)
            {
                FinishOnboarding(wasSkipped: false);
            }
            else
            {
                SaveSystem.Save();
                EnterStep(next);
            }
        }

        private void FinishOnboarding(bool wasSkipped)
        {
            IsActive = false;
            IsWaitingForCutscene = false;
            IsCompleted = true;
            WasSkipped = wasSkipped;
            CurrentStep = OnboardingStep.Completed;
            CurrentPromptText = string.Empty;

            // Garante todas as barreiras removidas (estado 100% limpo)
            for (int i = 0; i < _barriers.Length; i++)
            {
                if (_barriers[i] != null) _barriers[i].SetActive(false);
            }

            SetHudVisible(false);
            UnbindPowers();
            SaveSystem.Save();

            OnOnboardingFinished?.Invoke(wasSkipped);
            Debug.Log($"[Onboarding] Sequencia finalizada (skipped={wasSkipped}, total={_totalElapsed:F2}s, passos={_completedStepIds.Count}/5).");
        }

        private void BindPowers()
        {
            if (_powersBound || _dezPowers == null) return;
            _dezPowers.OnEcoActivated += HandleEcoActivated;
            _dezPowers.OnStasisActivated += HandleStasisActivated;
            _powersBound = true;
        }

        private void UnbindPowers()
        {
            if (!_powersBound || _dezPowers == null) return;
            _dezPowers.OnEcoActivated -= HandleEcoActivated;
            _dezPowers.OnStasisActivated -= HandleStasisActivated;
            _powersBound = false;
        }

        private void HandleEcoActivated()
        {
            if (!IsActive || CurrentStep != OnboardingStep.EcoTemporal || !TelegraphCompletedForCurrentStep)
                return;
            CompleteCurrentStep(skipped: false);
        }

        private void HandleStasisActivated()
        {
            if (!IsActive || CurrentStep != OnboardingStep.Estase || !TelegraphCompletedForCurrentStep)
                return;
            _stasisDoorLockedOpen = true;
            CompleteCurrentStep(skipped: false);
        }

        private void HandleTerminalInteracted(GameObject interactor)
        {
            if (!IsActive || CurrentStep != OnboardingStep.Interact || !TelegraphCompletedForCurrentStep)
                return;
            CompleteCurrentStep(skipped: false);
        }

        private void BuildDiegeticCorridor()
        {
            CleanupWorldGeometry();

            Vector3 origin = _dezLoco != null ? _dezLoco.transform.position : new Vector3(-28.5f, 0.05f, -18.5f);
            origin.y = Mathf.Max(0f, origin.y);
            _corridorOrigin = origin;
            _corridorDir = Vector3.right; // +X (corredor aberto da oficina do Caldeiral)
            Vector3 right = Vector3.Cross(Vector3.up, _corridorDir).normalized;

            var rootGo = new GameObject("Onboarding_DiegeticCorridor");
            _worldRoot = rootGo.transform;

            // 5 estacoes espacadas em passos curtos e claros no chao da oficina
            float[] distances = { 2.5f, 5.2f, 7.8f, 10.4f, 13.0f };
            for (int i = 0; i < 5; i++)
            {
                _stationPositions[i] = origin + _corridorDir * distances[i];
                var stRoot = new GameObject($"Station_{i}_{(OnboardingStep)i}");
                stRoot.transform.SetParent(_worldRoot, false);
                stRoot.transform.position = _stationPositions[i];
                _stationRoots[i] = stRoot;

                // Anel de telegraph visual no chao (>= 300ms)
                var ring = CreatePrimitive(PrimitiveType.Cylinder, "TelegraphRing", stRoot.transform,
                    new Vector3(0f, 0.04f, 0f), new Vector3(1.5f, 0.03f, 1.5f),
                    CreateEmissiveMat(new Color(0.18f, 0.85f, 1.0f, 0.85f), new Color(0.25f, 1.8f, 2.4f, 1f)),
                    withCollider: false);
                _telegraphRings[i] = ring.transform;
                _beaconRenderers[i] = ring.GetComponent<Renderer>();

                // Barreira fisica diegetica impedindo avancar antes de aprender o passo
                var barrier = CreatePrimitive(PrimitiveType.Cube, $"Barrier_{i}", stRoot.transform,
                    _corridorDir * 1.25f + Vector3.up * 1.25f, new Vector3(3.4f, 2.5f, 0.16f),
                    CreateTransparentMat(new Color(0.12f, 0.72f, 1.0f, 0.30f), new Color(0.1f, 0.9f, 1.5f, 1f)),
                    withCollider: true);
                barrier.transform.rotation = Quaternion.LookRotation(_corridorDir, Vector3.up);
                _barriers[i] = barrier;
            }

            // Passo 1 (Move): trilha luminosa guia no chao entre o Dez e a Estacao 0
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 4f;
                Vector3 pos = Vector3.Lerp(origin, _stationPositions[0], t) + Vector3.up * 0.03f;
                CreatePrimitive(PrimitiveType.Cube, $"MoveGuide_{k}", _worldRoot,
                    pos, new Vector3(0.45f, 0.03f, 0.25f),
                    CreateEmissiveMat(new Color(0.2f, 0.9f, 1f, 1f), new Color(0.2f, 1.6f, 2.2f, 1f)),
                    withCollider: false);
            }

            // Passo 2 (Jump): plato fisico elevado (0.62m > stepOffset 0.36m, exige pulo real!)
            var plateau = CreatePrimitive(PrimitiveType.Cube, "JumpPlateau", _stationRoots[1].transform,
                new Vector3(0f, PlateauHeightM * 0.5f, 0f), new Vector3(2.2f, PlateauHeightM, 1.8f),
                CreateEmissiveMat(new Color(0.24f, 0.32f, 0.42f, 1f), new Color(0.08f, 0.28f, 0.42f, 1f)),
                withCollider: true);
            _jumpPlateauTransform = plateau.transform;
            _telegraphRings[1].localPosition = new Vector3(0f, PlateauHeightM + 0.04f, 0f);

            // Passo 3 (Interact): terminal diegetico de energia (IInteractable)
            var pedestal = CreatePrimitive(PrimitiveType.Cube, "EnergyTerminal_Base", _stationRoots[2].transform,
                new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.7f),
                CreateEmissiveMat(new Color(0.28f, 0.30f, 0.35f, 1f), new Color(0.05f, 0.12f, 0.22f, 1f)),
                withCollider: true);
            var core = CreatePrimitive(PrimitiveType.Sphere, "EnergyTerminal_Core", pedestal.transform,
                new Vector3(0f, 0.78f, 0f), new Vector3(0.55f, 0.55f, 0.55f),
                CreateEmissiveMat(new Color(1.0f, 0.76f, 0.22f, 1f), new Color(2.2f, 1.4f, 0.25f, 1f)),
                withCollider: false);
            _terminal = pedestal.AddComponent<OnboardingEnergyTerminal>();
            _terminal.coreRenderer = core.GetComponent<Renderer>();
            _terminal.OnInteracted += HandleTerminalInteracted;

            // Passo 4 (Eco Temporal): arco de energia temporal pulsando
            CreatePrimitive(PrimitiveType.Cylinder, "EcoPylon_L", _stationRoots[3].transform,
                -right * 1.45f + Vector3.up * 1.1f, new Vector3(0.22f, 1.1f, 0.22f),
                CreateEmissiveMat(new Color(0.18f, 0.78f, 1.0f, 1f), new Color(0.2f, 1.6f, 2.4f, 1f)),
                withCollider: false);
            CreatePrimitive(PrimitiveType.Cylinder, "EcoPylon_R", _stationRoots[3].transform,
                right * 1.45f + Vector3.up * 1.1f, new Vector3(0.22f, 1.1f, 0.22f),
                CreateEmissiveMat(new Color(0.18f, 0.78f, 1.0f, 1f), new Color(0.2f, 1.6f, 2.4f, 1f)),
                withCollider: false);

            // Passo 5 (Estase): porta temporal oscilando rapido que desacelera com Estase
            var doorBlade = CreatePrimitive(PrimitiveType.Cube, "StasisGate_Blade", _stationRoots[4].transform,
                new Vector3(0f, 1.1f, 0f), new Vector3(2.8f, 1.8f, 0.24f),
                CreateEmissiveMat(new Color(0.95f, 0.52f, 0.16f, 1f), new Color(1.8f, 0.85f, 0.2f, 1f)),
                withCollider: false);
            _stasisDoorBlade = doorBlade.transform;
            _stasisDoorBaseLocalPos = _stasisDoorBlade.localPosition;
        }

        private void UpdateVisualTelegraphsAndDoor(float dt)
        {
            int idx = (int)CurrentStep;
            if (idx >= 0 && idx < _telegraphRings.Length && _telegraphRings[idx] != null)
            {
                bool inTelegraph = !TelegraphCompletedForCurrentStep;
                float pulseSpeed = inTelegraph ? 14f : 4.5f;
                float amp = inTelegraph ? 0.24f : 0.08f;
                float s = 1.5f + Mathf.Sin(_stepElapsed * pulseSpeed) * amp;
                _telegraphRings[idx].localScale = new Vector3(s, 0.03f, s);

                if (_beaconRenderers[idx] != null)
                {
                    Color em = inTelegraph
                        ? new Color(2.4f, 1.6f, 0.25f, 1f) // Ambar brilhante no telegraph >= 300ms
                        : new Color(0.22f, 1.8f, 2.5f, 1f); // Ciano ativo
                    SetMaterialColor(_beaconRenderers[idx].material, new Color(0.2f, 0.85f, 1f, 0.85f), em);
                }
            }

            if (_stasisDoorBlade != null)
            {
                if (_stasisDoorLockedOpen)
                {
                    Vector3 openPos = _stasisDoorBaseLocalPos + Vector3.up * 2.1f;
                    _stasisDoorBlade.localPosition = Vector3.Lerp(_stasisDoorBlade.localPosition, openPos, Mathf.Clamp01(dt * 6f));
                }
                else
                {
                    float speed = StasisFieldSystem.GetTimeScaleAt(_stasisDoorBlade.position) * 7.5f;
                    float offset = Mathf.Abs(Mathf.Sin(_totalElapsed * speed)) * 0.85f;
                    _stasisDoorBlade.localPosition = _stasisDoorBaseLocalPos + Vector3.up * offset;
                }
            }
        }

        private void BuildHudIfNeeded()
        {
            if (_hudCanvas != null) return;

            var canvasGo = new GameObject("Onboarding_DiegeticHUD");
            _hudCanvas = canvasGo.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hudCanvas.sortingOrder = 55;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _hudGroup = canvasGo.AddComponent<CanvasGroup>();
            _hudGroup.blocksRaycasts = false;
            _hudGroup.interactable = false;

            var panel = new GameObject("PromptPill");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.08f, 0.13f, 0.84f);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0f);
            pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.anchoredPosition = new Vector2(0f, 44f);
            pr.sizeDelta = new Vector2(780f, 76f);

            var accent = new GameObject("AccentBar");
            accent.transform.SetParent(panel.transform, false);
            _accentBar = accent.AddComponent<Image>();
            _accentBar.color = new Color(0.22f, 0.88f, 1f, 1f);
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0f, 0f);
            ar.anchorMax = new Vector2(0f, 1f);
            ar.pivot = new Vector2(0f, 0.5f);
            ar.sizeDelta = new Vector2(8f, 0f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var promptGo = new GameObject("SingleLinePrompt");
            promptGo.transform.SetParent(panel.transform, false);
            _promptLineText = promptGo.AddComponent<Text>();
            _promptLineText.font = font;
            _promptLineText.fontSize = 26;
            _promptLineText.fontStyle = FontStyle.Bold;
            _promptLineText.alignment = TextAnchor.MiddleCenter;
            _promptLineText.color = Color.white;
            _promptLineText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tr = promptGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0.28f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(18f, 0f);
            tr.offsetMax = new Vector2(-18f, -4f);

            var skipGo = new GameObject("SkipHint");
            skipGo.transform.SetParent(panel.transform, false);
            _skipHintText = skipGo.AddComponent<Text>();
            _skipHintText.font = font;
            _skipHintText.fontSize = 15;
            _skipHintText.alignment = TextAnchor.MiddleCenter;
            _skipHintText.color = new Color(0.72f, 0.86f, 0.94f, 0.9f);
            var sr = skipGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 0.34f);
            sr.offsetMin = new Vector2(12f, 2f);
            sr.offsetMax = new Vector2(-12f, 0f);

            SetHudVisible(false);
        }

        private void UpdateHudPrompt()
        {
            if (_promptLineText != null)
                _promptLineText.text = CurrentPromptText;

            if (_accentBar != null)
                _accentBar.color = TelegraphCompletedForCurrentStep
                    ? new Color(0.22f, 0.88f, 1f, 1f)
                    : new Color(1.0f, 0.76f, 0.22f, 1f);

            if (_skipHintText != null)
            {
                _skipHintText.text = CanSkipCurrentStep
                    ? "[Tab] Pular passo   •   [Segure Esc] Pular tudo"
                    : $"Passo {(int)CurrentStep + 1}/5";
            }
        }

        private void SetHudVisible(bool visible)
        {
            if (_hudGroup != null)
                _hudGroup.alpha = visible ? 1f : 0f;
        }

        private void CleanupWorldGeometry()
        {
            if (_terminal != null)
            {
                _terminal.OnInteracted -= HandleTerminalInteracted;
                _terminal = null;
            }
            if (_worldRoot != null)
            {
                Destroy(_worldRoot.gameObject);
                _worldRoot = null;
            }
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            Array.Clear(_stationRoots, 0, _stationRoots.Length);
            Array.Clear(_barriers, 0, _barriers.Length);
            Array.Clear(_beaconRenderers, 0, _beaconRenderers.Length);
            Array.Clear(_telegraphRings, 0, _telegraphRings.Length);
            _jumpPlateauTransform = null;
            _stasisDoorBlade = null;
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (!withCollider && col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            return go;
        }

        private Material CreateEmissiveMat(Color baseColor, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            SetMaterialColor(mat, baseColor, emission);
            _ownedMaterials.Add(mat);
            return mat;
        }

        private Material CreateTransparentMat(Color baseColor, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            SetMaterialColor(mat, baseColor, emission);
            _ownedMaterials.Add(mat);
            return mat;
        }

        private static void SetMaterialColor(Material mat, Color baseColor, Color emission)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
        }

        public static string GetStepId(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Move: return "onboarding_move";
                case OnboardingStep.Jump: return "onboarding_jump";
                case OnboardingStep.Interact: return "onboarding_interact";
                case OnboardingStep.EcoTemporal: return "onboarding_eco_temporal";
                case OnboardingStep.Estase: return "onboarding_estase";
                default: return "onboarding_none";
            }
        }

        public static string GetSingleLinePrompt(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Move: return "Caminhe ate o sinal luminoso  [WASD / Analogico]";
                case OnboardingStep.Jump: return "Pule sobre o plato elevado  [Espaco / A]";
                case OnboardingStep.Interact: return "Ative o terminal de energia  [E / X]";
                case OnboardingStep.EcoTemporal: return "Atravesse a energia com Eco Temporal  [Alt / C]";
                case OnboardingStep.Estase: return "Desacelere a porta temporal com Estase  [R / RB]";
                default: return string.Empty;
            }
        }
    }
}
