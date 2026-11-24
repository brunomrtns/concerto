using System;
using System.Collections;
using System.Collections.Generic;
using Studio.Game.Combat;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Menu;
using Studio.Game.Repair;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Tool
{
    /// <summary>
    /// MultiToolController — Core extensivel da Ferramenta Multi-Uso do Dez:
    ///   - Gerencia modulos plugaveis (ToolModuleBase): CONCERTAR (custo 0 sempre) e COMBATE (melee 0 / bolt 1 celula)
    ///   - Troca de modo com [Tab] (ou 1 / 2)
    ///   - HUD proprio (ToolHUD_Canvas): indicador de modo + barra de energia + contador de celulas (sem HP ainda!)
    ///   - Feedback de impacto: hitstop (65ms), camera shake sutil e som seco
    /// </summary>
    public sealed class MultiToolController : MonoBehaviour
    {
        public static MultiToolController Instance { get; private set; }

        [Header("Config & Modulos Plugaveis")]
        public MultiToolConfig config;
        public List<ToolModuleBase> modules = new List<ToolModuleBase>();

        [Header("Referencias de Cena")]
        public Transform playerTransform;
        public Transform cameraShakePivot;
        public Transform toolVisualTransform;
        public ParticleSystem hitSparksParticles;

        [Header("HUD da Ferramenta (ToolHUD_Canvas)")]
        public CanvasGroup hudGroup;
        public Text modeLabelText;
        public Image modeAccentBar;
        public Image energyBarFill;
        public Text cellsCountText;

        [Header("Audio TEMP")]
        public AudioSource sfxSource;
        public AudioClip modeSwitchClip;
        public AudioClip meleeSwingClip;
        public AudioClip meleeHitClip;
        public AudioClip boltFireClip;

        private MultiToolConfig _runtimeCfg;
        private int _activeIndex;
        private int _energyCells;
        private float _energyBar;
        private bool _inHitstop;
        private Vector3 _toolRestLocalPos;
        private Quaternion _toolRestLocalRot;
        private float _swingAnimTimer;
        private bool _swingHeavy;
        private int _swingCombo;

        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;

        public MultiToolConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_runtimeCfg == null) _runtimeCfg = ScriptableObject.CreateInstance<MultiToolConfig>();
                return _runtimeCfg;
            }
        }

        public Transform PlayerTransform => playerTransform != null ? playerTransform : transform;
        public ToolModuleBase ActiveModule => (modules.Count > 0 && _activeIndex >= 0 && _activeIndex < modules.Count) ? modules[_activeIndex] : null;
        public string ActiveModeId => ActiveModule != null ? ActiveModule.ModuleId : string.Empty;
        public bool IsRepairModeActive => ActiveModule == null || ActiveModule.AllowsRepairInteraction;
        public int EnergyCells => _energyCells;
        public float EnergyBar => _energyBar;
        public int HitstopTriggerCount { get; private set; }

        public event Action<ToolModuleBase> OnModeChanged;
        public event Action<int, float> OnEnergyChanged;

        private void Awake()
        {
            Instance = this;
            _energyCells = Cfg.startingCells;
            _energyBar = Cfg.maxEnergyBar;

            if (toolVisualTransform != null)
            {
                _toolRestLocalPos = toolVisualTransform.localPosition;
                _toolRestLocalRot = toolVisualTransform.localRotation;
            }

            if (modules.Count == 0)
                GetComponentsInChildren(true, modules);

            for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null) modules[i].Initialize(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_inHitstop) Time.timeScale = 1f;
        }

        private void Start()
        {
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();
            EquipModuleIndex(0, playSfx: false);
            RefreshHud();
        }

        public bool CanUseCombatActionsNow()
        {
            if (Time.timeScale <= 0.01f) return false;
            if (_cs01 != null && _cs01.IsPlaying) return false;
            if (_cs02 != null && _cs02.IsPlaying) return false;
            if (_cs03 != null && _cs03.IsPlaying) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) return false;
            if (RepairRitualController.Instance != null && (RepairRitualController.Instance.IsRepairing || RepairRitualController.Instance.IsPlayingFinalBeat)) return false;
            if (PlayerHealthController.Instance != null && PlayerHealthController.Instance.IsKnockedDown) return false;
            return true;
        }

        private void Update()
        {
            bool gameplayOpen = CanUseCombatActionsNow();
            if (hudGroup != null) hudGroup.alpha = gameplayOpen ? 1f : 0f;

            // Regeneracao passiva suave da barra de energia
            if (_energyBar < Cfg.maxEnergyBar)
            {
                _energyBar = Mathf.Min(Cfg.maxEnergyBar, _energyBar + Cfg.energyPassiveRegenPerSec * Time.deltaTime);
                RefreshHud();
            }

            // Animacao procedural curta de swing da ferramenta na mao
            if (_swingAnimTimer > 0f && toolVisualTransform != null)
            {
                _swingAnimTimer = Mathf.Max(0f, _swingAnimTimer - Time.deltaTime);
                float u = 1f - (_swingAnimTimer / (_swingHeavy ? 0.28f : 0.18f));
                float arc = Mathf.Sin(u * Mathf.PI);
                float yaw = (_swingCombo == 2 ? -85f : 85f) * arc;
                float pitch = (_swingHeavy ? 65f : 25f) * arc;
                toolVisualTransform.localRotation = _toolRestLocalRot * Quaternion.Euler(pitch, yaw, 0f);
                if (_swingAnimTimer <= 0f) toolVisualTransform.localRotation = _toolRestLocalRot;
            }

            if (!gameplayOpen) return;

            if (Input.GetKeyDown(Cfg.toggleModeKey))
                CycleNextMode();
            else if (Input.GetKeyDown(KeyCode.Alpha1))
                EquipModuleIndex(0, playSfx: true);
            else if (Input.GetKeyDown(KeyCode.Alpha2) && modules.Count > 1)
                EquipModuleIndex(1, playSfx: true);

            ActiveModule?.OnTick(Time.deltaTime);
        }

        public void CycleNextMode()
        {
            if (modules.Count <= 1) return;
            EquipModuleIndex((_activeIndex + 1) % modules.Count, playSfx: true);
        }

        public void EquipModuleIndex(int index, bool playSfx = true)
        {
            if (modules.Count == 0) return;
            index = Mathf.Clamp(index, 0, modules.Count - 1);
            if (ActiveModule != null) ActiveModule.OnUnequip();
            _activeIndex = index;
            if (ActiveModule != null) ActiveModule.OnEquip();

            // Em modo COMBATE, deixa a ferramenta visivel na mao do Dez!
            if (toolVisualTransform != null && (RepairRitualController.Instance == null || !RepairRitualController.Instance.IsRepairing))
                toolVisualTransform.gameObject.SetActive(!IsRepairModeActive);

            if (playSfx && sfxSource != null && modeSwitchClip != null)
                sfxSource.PlayOneShot(modeSwitchClip, 0.72f * GameSettingsManager.SfxVolume);

            RefreshHud();
            OnModeChanged?.Invoke(ActiveModule);
        }

        public bool TryConsumeBoltCost()
        {
            var cfg = Cfg;
            if (_energyCells < cfg.boltCellCost) return false;
            _energyCells -= cfg.boltCellCost;
            _energyBar = Mathf.Max(0f, _energyBar - cfg.boltEnergyCost);
            RefreshHud();
            OnEnergyChanged?.Invoke(_energyCells, _energyBar);
            return true;
        }

        public bool AddEnergyCells(int amount)
        {
            if (amount <= 0) return false;
            int prev = _energyCells;
            _energyCells = Mathf.Min(Cfg.maxCells, _energyCells + amount);
            _energyBar = Mathf.Min(Cfg.maxEnergyBar, _energyBar + amount * 30f);
            RefreshHud();
            OnEnergyChanged?.Invoke(_energyCells, _energyBar);
            return _energyCells > prev;
        }

        public void RestoreStateFromSave(int cells, float energy, string modeId)
        {
            _energyCells = Mathf.Clamp(cells, 0, Cfg.maxCells);
            _energyBar = Mathf.Clamp(energy, 0f, Cfg.maxEnergyBar);
            if (!string.IsNullOrEmpty(modeId))
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    if (modules[i] != null && modules[i].ModuleId == modeId)
                    {
                        EquipModuleIndex(i, playSfx: false);
                        break;
                    }
                }
            }
            RefreshHud();
            OnEnergyChanged?.Invoke(_energyCells, _energyBar);
        }

        public Vector3 GetMuzzleWorldPosition()
        {
            return PlayerTransform.position + Vector3.up * 0.92f + PlayerTransform.forward * 0.65f + PlayerTransform.right * 0.25f;
        }

        public Vector3 GetAimForward()
        {
            Vector3 fwd = PlayerTransform.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        }

        public void PlayToolSwingFeedback(bool isHeavy, int comboStep)
        {
            _swingHeavy = isHeavy;
            _swingCombo = comboStep;
            _swingAnimTimer = isHeavy ? 0.28f : 0.18f;
            if (toolVisualTransform != null) toolVisualTransform.gameObject.SetActive(true);
            if (sfxSource != null && meleeSwingClip != null)
            {
                sfxSource.pitch = isHeavy ? 0.78f : (comboStep == 2 ? 1.12f : 0.98f);
                sfxSource.PlayOneShot(meleeSwingClip, 0.78f * GameSettingsManager.SfxVolume);
            }
        }

        public void PlayBoltFireFeedback(Vector3 pos)
        {
            if (hitSparksParticles != null) { hitSparksParticles.transform.position = pos; hitSparksParticles.Play(); }
            if (sfxSource != null && boltFireClip != null)
            {
                sfxSource.pitch = 1.0f;
                sfxSource.PlayOneShot(boltFireClip, 0.85f * GameSettingsManager.SfxVolume);
            }
        }

        public void TriggerHitImpactFeedback(Vector3 hitPoint, bool isHeavy)
        {
            if (hitSparksParticles != null) { hitSparksParticles.transform.position = hitPoint; hitSparksParticles.Play(); }
            if (sfxSource != null && meleeHitClip != null)
            {
                sfxSource.pitch = isHeavy ? 0.82f : 1.05f;
                sfxSource.PlayOneShot(meleeHitClip, 0.90f * GameSettingsManager.SfxVolume);
            }
            HitstopTriggerCount++;
            StartCoroutine(RunHitstopAndSubtleShake(isHeavy));
        }

        private IEnumerator RunHitstopAndSubtleShake(bool isHeavy)
        {
            if (!_inHitstop)
            {
                _inHitstop = true;
                float prevScale = Time.timeScale;
                float hitstopDur = isHeavy ? Cfg.heavyHitstopDurationSec : Cfg.hitstopDurationSec;
                Time.timeScale = 0.02f;
                yield return new WaitForSecondsRealtime(Mathf.Clamp(hitstopDur, 0.05f, 0.10f));
                Time.timeScale = prevScale > 0.05f ? prevScale : 1f;
                _inHitstop = false;
            }

            if (cameraShakePivot != null)
            {
                Vector3 orig = cameraShakePivot.localPosition;
                float dur = Mathf.Min(0.28f, Cfg.cameraShakeDurationSec);
                float amp = Cfg.cameraShakeAmplitude * (isHeavy ? 1.35f : 1.0f) * GameSettingsManager.CameraShakeScale;
                float t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float falloff = 1f - (t / dur);
                    cameraShakePivot.localPosition = orig + new Vector3(
                        Mathf.Sin(t * 95f) * amp * falloff,
                        Mathf.Cos(t * 110f) * amp * falloff * 0.7f,
                        0f
                    );
                    yield return null;
                }
                cameraShakePivot.localPosition = orig;
            }
        }

        private int _lastHudCells = -1;
        private string _lastHudModeId = string.Empty;

        public void RefreshHud()
        {
            var mod = ActiveModule;
            if (mod != null && mod.ModuleId != _lastHudModeId)
            {
                _lastHudModeId = mod.ModuleId;
                if (modeLabelText != null)
                    modeLabelText.text = $"[TAB] MODO: {mod.DisplayName}";
                if (modeAccentBar != null)
                    modeAccentBar.color = mod.AccentColor;
            }
            if (energyBarFill != null)
                energyBarFill.fillAmount = Mathf.Clamp01(_energyBar / Mathf.Max(1f, Cfg.maxEnergyBar));
            if (cellsCountText != null && _energyCells != _lastHudCells)
            {
                _lastHudCells = _energyCells;
                cellsCountText.text = $"CÉLULAS: {_energyCells}/{Cfg.maxCells}";
            }
        }
    }
}
