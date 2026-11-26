using Studio.Game.Combat;
using Studio.Game.Hub;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Retrogrado
{
    /// <summary>
    /// Controlador do Fuso Retrogrado — O Deserto do Tempo Reverso (GDD 03 §2.5):
    ///   - 7 Beats com checkpoints e Save JSON.
    ///   - Desincronizado: areia cai para cima, pegadas se desmancham a frente, janelas invertidas.
    ///   - Boss O Arquivista-Mor (antecipa padroes repetidos; quebra com padroes ineditos + Contratempo).
    ///   - Reliquia Aro -> Aro-Escudo (parry reflete Faisca em area via ParryAndLockOnController).
    ///   - Pos-conserto: ruinas do pre-Fratura reveladas (lore dos Outros Relogios — gancho da serie)
    ///     e melhorias cosmeticas do Caldeiral "se desfazem" (custo comico leve, nada critico).
    /// </summary>
    public sealed class RetrogradoFusoController : MonoBehaviour
    {
        public static RetrogradoFusoController Instance { get; private set; }

        public static readonly string[] BeatIds =
        {
            "ret_b1_dunas_espiral",
            "ret_b2_areia_para_cima",
            "ret_b3_janelas_invertidas",
            "ret_b4_portal_ampulheta",
            "ret_b5_boss_arquivista_mor",
            "ret_b6_reliquia_aro_escudo",
            "ret_b7_ruinas_pre_fratura"
        };

        public static readonly string[] PreFractureLoreEntries =
        {
            "Inscricao I — Os Outros Relogios: Antes da Fratura, havia sete mostradores irmaos alem do Caldeiral.",
            "Inscricao II — A Assinatura do Relojoeiro: Cada Relogio guarda um fuso que nao pertence a este mapa."
        };

        [Header("Referencias")]
        public Transform[] beatCheckpoints = new Transform[7];
        public RetrogradoReverseTimeSystem reverseTime;
        public RetrogradoBossArquivistaMor bossArquivista;
        public GameObject relicAroObject;
        public GameObject preFractureRuinsRoot;

        public int CurrentBeatIndex { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool RelicAroEscudoCollected { get; private set; }
        public bool PreFractureRuinsRevealed { get; private set; }
        public bool CaldeiralCosmeticUndoApplied { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (bossArquivista != null)
                bossArquivista.OnArquivistaDefeated += HandleArquivistaDefeated;
        }

        private void OnDisable()
        {
            if (bossArquivista != null)
                bossArquivista.OnArquivistaDefeated -= HandleArquivistaDefeated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ReachBeat(int beatIndex, bool autoSave = true)
        {
            beatIndex = Mathf.Clamp(beatIndex, 0, BeatIds.Length - 1);
            if (beatIndex > CurrentBeatIndex) CurrentBeatIndex = beatIndex;
            string cpId = BeatIds[beatIndex];
            Vector3 cpPos = (beatCheckpoints != null && beatIndex < beatCheckpoints.Length && beatCheckpoints[beatIndex] != null)
                ? beatCheckpoints[beatIndex].position
                : transform.position;
            if (autoSave)
                SaveSystem.Save(cpId, cpPos, 0f);
        }

        private void HandleArquivistaDefeated()
        {
            BossDefeated = true;
            ReachBeat(4, autoSave: true);
            if (relicAroObject != null) relicAroObject.SetActive(true);
        }

        /// <summary>GATE 3: Coleta Reliquia Aro -> Aro-Escudo (parry reflete Faisca em area via Contratempo).</summary>
        public bool CollectRelicAroEscudo(ParryAndLockOnController parry = null)
        {
            if (!BossDefeated) return false;
            RelicAroEscudoCollected = true;
            if (relicAroObject != null) relicAroObject.SetActive(false);
            if (parry == null) parry = FindAnyObjectByType<ParryAndLockOnController>();
            if (parry != null) parry.HasRelicAroEscudo = true;
            ReachBeat(5, autoSave: true);
            return true;
        }

        /// <summary>GATE 2: Pos-conserto revela ruinas pre-Fratura (Outros Relogios) e aplica custo comico leve no Caldeiral.</summary>
        public bool RevealPreFractureRuinsAndApplyComicCost()
        {
            if (!RelicAroEscudoCollected) return false;
            PreFractureRuinsRevealed = true;
            CaldeiralCosmeticUndoApplied = true;
            if (preFractureRuinsRoot != null) preFractureRuinsRoot.SetActive(true);
            if (reverseTime != null) reverseTime.NormalizeTimePostRepair();
            if (HubOrquestraController.Instance != null)
                HubOrquestraController.Instance.ConfirmRestoreFuso("retrogrado");
            ReachBeat(6, autoSave: true);
            return true;
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.retrogradoBeatIndex = CurrentBeatIndex;
            data.retrogradoBossDefeated = BossDefeated;
            data.relicAroEscudoUnlocked = RelicAroEscudoCollected;
            data.retrogradoRuinsRevealed = PreFractureRuinsRevealed;
            data.caldeiralCosmeticsUndoneComic = CaldeiralCosmeticUndoApplied;
            if (PreFractureRuinsRevealed)
            {
                if (data.restoredFusoIds == null) data.restoredFusoIds = new System.Collections.Generic.List<string>();
                if (!data.restoredFusoIds.Contains("retrogrado")) data.restoredFusoIds.Add("retrogrado");
                if (data.appliedBalanceEvents == null) data.appliedBalanceEvents = new System.Collections.Generic.List<string>();
                if (!data.appliedBalanceEvents.Contains("evt_benefit_retrogrado_ruins")) data.appliedBalanceEvents.Add("evt_benefit_retrogrado_ruins");
                if (!data.appliedBalanceEvents.Contains("evt_cost_retrogrado_undo_caldeiral")) data.appliedBalanceEvents.Add("evt_cost_retrogrado_undo_caldeiral");
            }
        }

        public void RestoreFromSave(SaveGameData data)
        {
            if (data == null) return;
            CurrentBeatIndex = Mathf.Clamp(data.retrogradoBeatIndex, 0, BeatIds.Length - 1);
            BossDefeated = data.retrogradoBossDefeated;
            RelicAroEscudoCollected = data.relicAroEscudoUnlocked;
            PreFractureRuinsRevealed = data.retrogradoRuinsRevealed;
            CaldeiralCosmeticUndoApplied = data.caldeiralCosmeticsUndoneComic;
            if (preFractureRuinsRoot != null) preFractureRuinsRoot.SetActive(PreFractureRuinsRevealed);
            if (reverseTime != null) reverseTime.RestoreReverseState(!PreFractureRuinsRevealed);
            if (RelicAroEscudoCollected)
            {
                var parry = FindAnyObjectByType<ParryAndLockOnController>();
                if (parry != null) parry.HasRelicAroEscudo = true;
            }
        }
    }
}
