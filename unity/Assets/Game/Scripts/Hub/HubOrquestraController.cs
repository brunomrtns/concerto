using System;
using System.Collections.Generic;
using Studio.Game.Audio;
using UnityEngine;

namespace Studio.Game.Hub
{
    [Serializable]
    public sealed class HubNpcArrivalSlot
    {
        public string fusoId;
        public string npcName;
        public GameObject npcRoot;
    }

    /// <summary>
    /// Controlador do Hub "A Orquestra" (GDD 05 §1 + GDD 01 §5):
    ///   - Gerencia fusos restaurados e eventos de beneficio/custo da Linha do Equilibrio.
    ///   - Hub vivo: a cada fuso restaurado, o NPC correspondente chega ao hub e uma camada extra
    ///     da marchinha musical passa a tocar (integrada ao MusicGroup do AudioMixController).
    ///   - Persiste integralmente no SaveSystem JSON versionado (GATE 5).
    /// </summary>
    public sealed class HubOrquestraController : MonoBehaviour
    {
        public static HubOrquestraController Instance { get; private set; }

        [Header("Dados da Linha do Equilibrio (GATE 3)")]
        public BalanceLineDatabase balanceDatabase;

        [Header("Hub Vivo — Slots de Chegada de NPCs (GATE 4)")]
        public List<HubNpcArrivalSlot> npcArrivalSlots = new List<HubNpcArrivalSlot>();

        [Header("Referencia a Tela de Fusos (GATE 2)")]
        public TelaDeFusosUI telaDeFusos;

        public IReadOnlyList<FusoBalanceEntry> Entries => _entries;
        public int RestoredCount => _restoredFusos.Count;
        public int ActiveNpcCount { get; private set; }
        public int ActiveMarchinhaLayers { get; private set; }
        public IReadOnlyCollection<string> AppliedWorldEvents => _appliedWorldEvents;

        public event Action<FusoBalanceEntry> OnFusoRestored;
        public event Action<string> OnWorldEventApplied;

        private readonly List<FusoBalanceEntry> _entries = new List<FusoBalanceEntry>();
        private readonly HashSet<string> _restoredFusos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _appliedWorldEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioSource> _marchinhaLayerSources = new List<AudioSource>();

        private void Awake()
        {
            Instance = this;
            _entries.Clear();
            var src = balanceDatabase != null ? balanceDatabase.GetEntriesOrDefault() : BalanceLineDatabase.BuildCanonicalGddEntries();
            _entries.AddRange(src);
            BuildMarchinhaAudioLayers();
            RefreshHubVisualsAndAudio();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.001f) return;
            // Atalho M ou Tab para abrir/fechar Tela de Fusos no Hub
            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.F))
            {
                if (telaDeFusos != null)
                {
                    if (telaDeFusos.IsOpen) telaDeFusos.Close();
                    else telaDeFusos.Open();
                }
            }
        }

        public bool IsFusoRestored(string fusoId)
        {
            return !string.IsNullOrEmpty(fusoId) && _restoredFusos.Contains(fusoId);
        }

        public bool HasWorldEvent(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && _appliedWorldEvents.Contains(eventId);
        }

        public FusoBalanceEntry GetEntry(string fusoId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].fusoId, fusoId, StringComparison.OrdinalIgnoreCase))
                    return _entries[i];
            }
            return null;
        }

        public bool ConfirmRestoreFuso(string fusoId)
        {
            var entry = GetEntry(fusoId);
            if (entry == null || _restoredFusos.Contains(entry.fusoId))
                return false;

            _restoredFusos.Add(entry.fusoId);

            // Aplica beneficio e custo como eventos de mundo (GATE 3)
            if (!string.IsNullOrEmpty(entry.worldBenefitEventId) && _appliedWorldEvents.Add(entry.worldBenefitEventId))
                OnWorldEventApplied?.Invoke(entry.worldBenefitEventId);
            if (!string.IsNullOrEmpty(entry.worldCostEventId) && _appliedWorldEvents.Add(entry.worldCostEventId))
                OnWorldEventApplied?.Invoke(entry.worldCostEventId);

            RefreshHubVisualsAndAudio();
            OnFusoRestored?.Invoke(entry);
            Debug.Log($"[HubOrquestra] Fuso '{entry.displayName}' restaurado! NPC '{entry.npcArrivalName}' chegou; marchinha={ActiveMarchinhaLayers} camadas; eventos=[{entry.worldBenefitEventId}, {entry.worldCostEventId}]");
            return true;
        }

        public void CopyStateToSave(List<string> outRestoredFusos, List<string> outAppliedEvents)
        {
            outRestoredFusos.Clear();
            foreach (var id in _restoredFusos) outRestoredFusos.Add(id);

            outAppliedEvents.Clear();
            foreach (var ev in _appliedWorldEvents) outAppliedEvents.Add(ev);
        }

        public void RestoreStateFromSave(List<string> savedRestoredFusos, List<string> savedAppliedEvents)
        {
            _restoredFusos.Clear();
            if (savedRestoredFusos != null)
            {
                for (int i = 0; i < savedRestoredFusos.Count; i++)
                {
                    if (!string.IsNullOrEmpty(savedRestoredFusos[i]))
                        _restoredFusos.Add(savedRestoredFusos[i]);
                }
            }

            _appliedWorldEvents.Clear();
            if (savedAppliedEvents != null)
            {
                for (int i = 0; i < savedAppliedEvents.Count; i++)
                {
                    if (!string.IsNullOrEmpty(savedAppliedEvents[i]))
                        _appliedWorldEvents.Add(savedAppliedEvents[i]);
                }
            }

            RefreshHubVisualsAndAudio();
            if (telaDeFusos != null && telaDeFusos.IsOpen)
                telaDeFusos.RefreshVisuals();
        }

        private void RefreshHubVisualsAndAudio()
        {
            int activeNpcs = 0;
            for (int i = 0; i < npcArrivalSlots.Count; i++)
            {
                var slot = npcArrivalSlots[i];
                if (slot == null || slot.npcRoot == null) continue;
                bool arrived = IsFusoRestored(slot.fusoId);
                slot.npcRoot.SetActive(arrived);
                if (arrived) activeNpcs++;
            }
            ActiveNpcCount = activeNpcs;

            // Medidor auditivo de progresso: camada base (1) + 1 camada extra por fuso restaurado (ate 6)
            int targetLayers = Mathf.Min(_marchinhaLayerSources.Count, 1 + _restoredFusos.Count);
            ActiveMarchinhaLayers = targetLayers;
            for (int i = 0; i < _marchinhaLayerSources.Count; i++)
            {
                var src = _marchinhaLayerSources[i];
                if (src == null) continue;
                bool active = i < targetLayers;
                src.volume = active ? (i == 0 ? 0.16f : 0.10f) : 0f;
                if (active && !src.isPlaying) src.Play();
            }
        }

        private void BuildMarchinhaAudioLayers()
        {
            if (_marchinhaLayerSources.Count > 0) return;
            // 6 camadas harmonicas sintetizadas (base + 5 instrumentos da marchinha de A Orquestra)
            float[] freqs = { 220f, 277.18f, 329.63f, 440f, 554.37f, 659.25f };
            var mix = AudioMixController.Instance;

            for (int i = 0; i < freqs.Length; i++)
            {
                var go = new GameObject($"Marchinha_Layer_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = 0f;
                if (mix != null && mix.MusicGroup != null)
                    src.outputAudioMixerGroup = mix.MusicGroup;
                src.clip = CreateMarchinhaPulseClip($"MarchinhaLayer_{i}", freqs[i], 2.0f);
                _marchinhaLayerSources.Add(src);
            }
        }

        private static AudioClip CreateMarchinhaPulseClip(string name, float freqHz, float durSec)
        {
            const int sr = 22050;
            int samples = Mathf.Max(1024, Mathf.RoundToInt(sr * durSec));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sr;
                float beatEnv = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 4f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freqHz * t) * 0.22f * beatEnv;
            }
            var clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
