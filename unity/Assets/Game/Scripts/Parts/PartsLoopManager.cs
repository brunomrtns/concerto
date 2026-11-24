using System;
using System.Collections.Generic;
using Studio.Game.Dialogue;
using UnityEngine;

namespace Studio.Game.Parts
{
    /// <summary>
    /// PartsLoopManager — Orquestra o loop completo das pecas do Ato 1:
    ///   - Le N (requiredCount) de parts_loop_config.json (default 5; mudar pra 3 no JSON adapta tudo!)
    ///   - Apos dialogo intro da Juno -> libera coleta das N pecas + atualiza HUD & Quadro Diegetico
    ///   - Som no pickup + flash no contador do HUD ("Pecas (k/N)") + quadro fisico atualiza em tempo real
    ///   - Todas coletadas -> objetivo "Leve as pecas para Juno" + troca JSON da Juno para entrega
    ///   - Entrega -> dialogo curto final + stinger musical curto + Ato 1 concluido!
    /// </summary>
    public sealed class PartsLoopManager : MonoBehaviour
    {
        public static PartsLoopManager Instance { get; private set; }

        [Header("Configuracao Data-Driven (JSON)")]
        public TextAsset partsConfigJson;
        public TextAsset junoIntroDialogueJson;
        public TextAsset junoDeliverDialogueJson;

        [Header("Referencias de Cena")]
        public DiegeticChecklistBoard diegeticBoard;
        public NpcDialogueInteractable junoNpc;
        public List<MachinePartPickup> sceneParts = new List<MachinePartPickup>();

        [Header("Audio TEMP")]
        public AudioSource sfxSource;
        public AudioClip pickupChimeClip;
        public AudioClip act1CompleteStingerClip;

        private PartsLoopConfigData _config;
        private int _requiredCount = 5;
        private int _collectedCount;
        private bool _canCollectParts;
        private bool _allCollected;
        private bool _act1Completed;

        public int RequiredCount => _requiredCount;
        public int CollectedCount => _collectedCount;
        public bool CanCollectParts => _canCollectParts;
        public bool AllCollected => _allCollected;
        public bool Act1Completed => _act1Completed;

        public event Action<int, int> OnPartCountChanged;
        public event Action OnAct1Completed;

        private void Awake()
        {
            Instance = this;
            ApplyConfig(partsConfigJson);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (DialogueController.Instance != null)
                DialogueController.Instance.OnConversationFinished -= HandleConversationFinished;
        }

        private void Start()
        {
            if (DialogueController.Instance != null)
                DialogueController.Instance.OnConversationFinished += HandleConversationFinished;
        }

        /// <summary>
        /// Aplica (ou reaplica em runtime/teste) qualquer parts_loop_config.json — prova de config N=3 ou N=5!
        /// </summary>
        public void ApplyConfig(TextAsset configAsset)
        {
            _config = PartsLoopConfigData.FromJson(configAsset);
            int maxAvailable = Mathf.Min(_config.parts != null ? _config.parts.Count : 0, sceneParts.Count);
            _requiredCount = Mathf.Clamp(_config.requiredCount, 1, Mathf.Max(1, maxAvailable));
            _collectedCount = 0;
            _canCollectParts = false;
            _allCollected = false;
            _act1Completed = false;

            var activeDefs = new List<PartPlacementData>();
            for (int i = 0; i < sceneParts.Count; i++)
            {
                var p = sceneParts[i];
                if (p == null) continue;
                bool active = i < _requiredCount;
                p.ResetForReplay();
                p.slotIndex = i;
                if (active && _config.parts != null && i < _config.parts.Count)
                {
                    p.partId = _config.parts[i].id;
                    p.displayName = _config.parts[i].displayName;
                    activeDefs.Add(_config.parts[i]);
                }
                p.gameObject.SetActive(active);
            }

            if (diegeticBoard != null)
                diegeticBoard.BuildSlots(activeDefs);

            if (junoNpc != null && junoIntroDialogueJson != null)
                junoNpc.dialogueJson = junoIntroDialogueJson;
        }

        private void HandleConversationFinished(string dialogueId)
        {
            if (dialogueId == "dlg_juno_oficina_intro" && !_act1Completed)
            {
                _canCollectParts = true;
                UpdateProgressObjective(flash: true);
            }
            else if (dialogueId == "dlg_juno_entrega_pecas" && _allCollected && !_act1Completed)
            {
                _act1Completed = true;
                if (sfxSource != null && act1CompleteStingerClip != null)
                    sfxSource.PlayOneShot(act1CompleteStingerClip, 0.85f);

                string doneMsg = DialogueController.Instance != null
                    ? DialogueController.Instance.Localize("obj_act1_done")
                    : "Ato 1 concluído — Peças entregues!";
                ObjectiveSystem.Set(doneMsg);
                ObjectiveSystem.Flash();
                OnAct1Completed?.Invoke();
                Debug.Log("[PartsLoopManager] ATO 1 CONCLUIDO! Todas as pecas entregues para Juno.");
            }
        }

        public void NotifyPartCollected(MachinePartPickup part)
        {
            if (_allCollected) return;
            _collectedCount = Mathf.Min(_requiredCount, _collectedCount + 1);

            if (sfxSource != null && pickupChimeClip != null)
                sfxSource.PlayOneShot(pickupChimeClip, 0.78f);

            if (diegeticBoard != null)
                diegeticBoard.MarkSlotCollected(part.slotIndex, _collectedCount, _requiredCount);

            OnPartCountChanged?.Invoke(_collectedCount, _requiredCount);

            if (_collectedCount >= _requiredCount)
            {
                _allCollected = true;
                _canCollectParts = false;

                // Troca o JSON da Juno para a conversa curta de entrega (ZERO string hardcoded!)
                if (junoNpc != null && junoDeliverDialogueJson != null)
                    junoNpc.dialogueJson = junoDeliverDialogueJson;

                string retMsg = DialogueController.Instance != null
                    ? DialogueController.Instance.Localize("obj_return_juno")
                    : "Leve as peças para Juno";
                ObjectiveSystem.Set(retMsg);
                ObjectiveSystem.Flash();
            }
            else
            {
                UpdateProgressObjective(flash: true);
            }
        }

        private void UpdateProgressObjective(bool flash)
        {
            string fmt = DialogueController.Instance != null
                ? DialogueController.Instance.Localize("obj_find_parts_progress")
                : "Peças ({0}/{1}) — Encontre as peças da máquina";
            ObjectiveSystem.Set(string.Format(fmt, _collectedCount, _requiredCount));
            if (flash) ObjectiveSystem.Flash();
        }
    }
}
