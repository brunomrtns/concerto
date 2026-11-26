using System;
using System.Collections.Generic;
using Studio.Game.Animation;
using Studio.Game.Combat;
using Studio.Game.Save;
using UnityEngine;

namespace Studio.Game.Economy
{
    public sealed class TioHorarioUpgradeDef
    {
        public string Id;
        public string DisplayName;
        public int CostSegundos;
        public string EffectSummary;
    }

    public sealed class RemendoCosmeticDef
    {
        public string Id;
        public string DisplayName;
        public int CostSegundos; // 0 = desbloqueio por conquista de coleta
        public bool IsAchievementUnlock;
        public Color TintColor;
    }

    /// <summary>
    /// Economia Canon do GDD 03 §4 + 04 §3:
    ///   - Moeda SEGUNDOS ("fios de tempo" dourados), migracao automatica de pecas (1 peca = 10 Segundos).
    ///   - Loja do Tio Horario: 12 melhorias com efeito REAL no gameplay (total = 1.130 Segundos).
    ///   - 21 Memorias-Eco (3 por fuso x 6 + 3 Corona) -> completar 21/21 libera Epilogo Estendido.
    ///   - 3 Remendos cosmeticos (2 comprados = 160 Segundos + 1 por conquista de coleta) — SO visual.
    ///   - Curva: 1.440 Segundos disponiveis no jogo vs 1.290 total de compras (margem +150s = +11,6%).
    /// </summary>
    public sealed class SegundosEconomySystem : MonoBehaviour
    {
        public const int LegacyPartToSegundosRate = 10;
        public const int TotalEchoMemoriesRequiredForEpilogue = 21;
        public const int TotalSegundosAvailableInWorld = 1440;

        public static SegundosEconomySystem Instance { get; private set; }

        public static readonly TioHorarioUpgradeDef[] CanonicalUpgrades =
        {
            new TioHorarioUpgradeDef { Id = "upg_fusivel_1",        DisplayName = "Fusivel +1 (I)",             CostSegundos = 60,  EffectSummary = "+1 Fusivel maximo" },
            new TioHorarioUpgradeDef { Id = "upg_fusivel_2",        DisplayName = "Fusivel +1 (II)",            CostSegundos = 100, EffectSummary = "+1 Fusivel maximo" },
            new TioHorarioUpgradeDef { Id = "upg_pressao_1",        DisplayName = "Pressao +25 (I)",            CostSegundos = 50,  EffectSummary = "+25 Pressao maxima" },
            new TioHorarioUpgradeDef { Id = "upg_pressao_2",        DisplayName = "Pressao +25 (II)",           CostSegundos = 80,  EffectSummary = "+25 Pressao maxima" },
            new TioHorarioUpgradeDef { Id = "upg_pressao_3",        DisplayName = "Pressao +25 (III)",          CostSegundos = 120, EffectSummary = "+25 Pressao maxima" },
            new TioHorarioUpgradeDef { Id = "upg_faisca_dupla",     DisplayName = "Faisca Dupla",               CostSegundos = 140, EffectSummary = "Dispara 2 Faiscas por tiro" },
            new TioHorarioUpgradeDef { Id = "upg_sopro_ar",         DisplayName = "Sopro no Ar",                CostSegundos = 90,  EffectSummary = "Libera Sopro aereo" },
            new TioHorarioUpgradeDef { Id = "upg_contratempo_largo",DisplayName = "Contratempo Largo (+0,06s)", CostSegundos = 110, EffectSummary = "+0,06s na janela de parry" },
            new TioHorarioUpgradeDef { Id = "upg_rebote_mola",      DisplayName = "Rebote Extra em Mola",       CostSegundos = 95,  EffectSummary = "Carga extra de Rebote em molas" },
            new TioHorarioUpgradeDef { Id = "upg_garra_alcance",    DisplayName = "Garra +4m",                  CostSegundos = 105, EffectSummary = "+4m de alcance na Garra (12m->16m)" },
            new TioHorarioUpgradeDef { Id = "upg_pressao_melee",    DisplayName = "Cadencia Melee (+10p)",      CostSegundos = 85,  EffectSummary = "+10 Pressao extra por golpe melee" },
            new TioHorarioUpgradeDef { Id = "upg_afinacao_rapida",  DisplayName = "Afinacao Rapida",            CostSegundos = 95,  EffectSummary = "Finalizador CONCERTO devolve +15 Pressao" }
        };

        public static readonly RemendoCosmeticDef[] CanonicalRemendos =
        {
            new RemendoCosmeticDef { Id = "remendo_latao_dourado",     DisplayName = "Macacao Latao Dourado",  CostSegundos = 70, IsAchievementUnlock = false, TintColor = new Color(0.92f, 0.72f, 0.24f, 1f) },
            new RemendoCosmeticDef { Id = "remendo_azul_ecodia",       DisplayName = "Macacao Azul-Madrugada", CostSegundos = 90, IsAchievementUnlock = false, TintColor = new Color(0.22f, 0.42f, 0.78f, 1f) },
            new RemendoCosmeticDef { Id = "remendo_mestre_relojoeiro", DisplayName = "Remendo Mestre (Coleta)",CostSegundos = 0,  IsAchievementUnlock = true,  TintColor = new Color(0.96f, 0.88f, 0.56f, 1f) }
        };

        public static readonly string[] CanonicalEchoMemoryIds =
        {
            "mem_caldeiral_1", "mem_caldeiral_2", "mem_caldeiral_3",
            "mem_vitralha_1",  "mem_vitralha_2",  "mem_vitralha_3",
            "mem_maresia_1",   "mem_maresia_2",   "mem_maresia_3",
            "mem_ecodia_1",    "mem_ecodia_2",    "mem_ecodia_3",
            "mem_retrogrado_1","mem_retrogrado_2","mem_retrogrado_3",
            "mem_orquestra_1", "mem_orquestra_2", "mem_orquestra_3",
            "mem_corona_1",    "mem_corona_2",    "mem_corona_3"
        };

        public int SegundosEarned { get; private set; }
        public int SegundosSpent { get; private set; }
        public int SegundosBalance => Mathf.Max(0, SegundosEarned - SegundosSpent);

        public HashSet<string> PurchasedUpgradeIds { get; } = new HashSet<string>();
        public HashSet<string> CollectedMemoryIds { get; } = new HashSet<string>();
        public HashSet<string> UnlockedRemendoIds { get; } = new HashSet<string>();
        public string EquippedRemendoId { get; private set; } = string.Empty;
        public string LastPlayedMemoryMicroScene { get; private set; } = string.Empty;

        public int CollectedMemoriesCount => CollectedMemoryIds.Count;
        public bool ExtendedEpilogueUnlocked => CollectedMemoryIds.Count >= TotalEchoMemoriesRequiredForEpilogue;

        // Efeitos reais derivados das melhorias compradas
        public int BonusMaxFuses => (HasUpgrade("upg_fusivel_1") ? 1 : 0) + (HasUpgrade("upg_fusivel_2") ? 1 : 0);
        public float BonusMaxPressure => (HasUpgrade("upg_pressao_1") ? 25f : 0f) + (HasUpgrade("upg_pressao_2") ? 25f : 0f) + (HasUpgrade("upg_pressao_3") ? 25f : 0f);
        public bool FaiscaDoubleShotUnlocked => HasUpgrade("upg_faisca_dupla");
        public bool AirSoproUnlocked => HasUpgrade("upg_sopro_ar");
        public float ParryWindowBonusSec => HasUpgrade("upg_contratempo_largo") ? 0.06f : 0f;
        public bool ExtraSpringReboteUnlocked => HasUpgrade("upg_rebote_mola");
        public float GrappleRangeBonusM => HasUpgrade("upg_garra_alcance") ? 4.0f : 0f;
        public float BonusMeleePressureGain => HasUpgrade("upg_pressao_melee") ? 10f : 0f;
        public float FinisherPressureRefund => HasUpgrade("upg_afinacao_rapida") ? 15f : 0f;

        private readonly HashSet<string> _collectedPickups = new HashSet<string>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasUpgrade(string id) => PurchasedUpgradeIds.Contains(id);

        public bool CollectFioDeTempo(int amount, string pickupId = null)
        {
            if (amount <= 0) return false;
            if (!string.IsNullOrEmpty(pickupId) && !_collectedPickups.Add(pickupId))
                return false;
            SegundosEarned += amount;
            return true;
        }

        public bool TryPurchaseUpgrade(string upgradeId)
        {
            if (HasUpgrade(upgradeId)) return false;
            TioHorarioUpgradeDef def = null;
            for (int i = 0; i < CanonicalUpgrades.Length; i++)
                if (CanonicalUpgrades[i].Id == upgradeId) { def = CanonicalUpgrades[i]; break; }
            if (def == null || SegundosBalance < def.CostSegundos) return false;

            SegundosSpent += def.CostSegundos;
            PurchasedUpgradeIds.Add(def.Id);
            ApplyAllUpgradeEffectsToScene();
            return true;
        }

        public bool TryPurchaseRemendo(string remendoId)
        {
            if (UnlockedRemendoIds.Contains(remendoId)) return false;
            RemendoCosmeticDef def = null;
            for (int i = 0; i < CanonicalRemendos.Length; i++)
                if (CanonicalRemendos[i].Id == remendoId) { def = CanonicalRemendos[i]; break; }
            if (def == null || def.IsAchievementUnlock || SegundosBalance < def.CostSegundos) return false;

            SegundosSpent += def.CostSegundos;
            UnlockedRemendoIds.Add(def.Id);
            return true;
        }

        public bool EquipRemendo(string remendoId, GameObject playerVisualRoot = null)
        {
            if (!UnlockedRemendoIds.Contains(remendoId)) return false;
            EquippedRemendoId = remendoId;
            // So visual: aplica tinta cosmetica no macacao sem alterar nenhum stat de gameplay
            if (playerVisualRoot != null)
            {
                Color tint = Color.white;
                for (int i = 0; i < CanonicalRemendos.Length; i++)
                    if (CanonicalRemendos[i].Id == remendoId) { tint = CanonicalRemendos[i].TintColor; break; }
                var rend = playerVisualRoot.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", tint);
                    rend.SetPropertyBlock(mpb);
                }
            }
            return true;
        }

        public bool CollectEchoMemory(string memoryId)
        {
            if (string.IsNullOrEmpty(memoryId)) return false;
            bool valid = false;
            for (int i = 0; i < CanonicalEchoMemoryIds.Length; i++)
                if (CanonicalEchoMemoryIds[i] == memoryId) { valid = true; break; }
            if (!valid) return false;

            CollectedMemoryIds.Add(memoryId);
            LastPlayedMemoryMicroScene = $"[Memoria-Eco {CollectedMemoryIds.Count}/21] {memoryId}: Um fragmento antes da Fratura ecoa no relogio...";

            // Conquista de coleta: >= 10 Memorias-Eco libera o 3o Remendo cosmetico!
            if (CollectedMemoryIds.Count >= 10)
                UnlockedRemendoIds.Add("remendo_mestre_relojoeiro");

            return true;
        }

        public void ApplyAllUpgradeEffectsToScene()
        {
            var pres = PressureResourceSystem.Instance ?? FindAnyObjectByType<PressureResourceSystem>();
            if (pres != null)
                pres.ApplyEconomyBonuses(BonusMaxPressure, BonusMeleePressureGain);

            var faisca = FaiscaRangedController.Instance ?? FindAnyObjectByType<FaiscaRangedController>();
            if (faisca != null)
                faisca.DoubleShotUnlocked = FaiscaDoubleShotUnlocked;

            var parry = ParryAndLockOnController.Instance ?? FindAnyObjectByType<ParryAndLockOnController>();
            if (parry != null)
                parry.parryWindowSec = 0.18f + ParryWindowBonusSec;

            var loco = FindAnyObjectByType<DezLocomotionController>();
            if (loco != null && loco.config != null)
                loco.config.grappleRangeM = 12.0f + GrappleRangeBonusM;
        }

        public void CopyToSave(SaveGameData data)
        {
            if (data == null) return;
            data.segundosEarned = SegundosEarned;
            data.segundosSpent = SegundosSpent;
            data.tioHorarioUpgradeIds = new List<string>(PurchasedUpgradeIds);
            data.collectedEchoMemoryIds = new List<string>(CollectedMemoryIds);
            data.unlockedRemendoIds = new List<string>(UnlockedRemendoIds);
            data.equippedRemendoId = EquippedRemendoId;
            data.extendedEpilogueUnlocked = ExtendedEpilogueUnlocked;
        }

        public void RestoreFromSave(SaveGameData data)
        {
            if (data == null) return;
            SegundosEarned = Mathf.Max(0, data.segundosEarned);
            SegundosSpent = Mathf.Max(0, data.segundosSpent);
            PurchasedUpgradeIds.Clear();
            if (data.tioHorarioUpgradeIds != null)
                for (int i = 0; i < data.tioHorarioUpgradeIds.Count; i++) PurchasedUpgradeIds.Add(data.tioHorarioUpgradeIds[i]);
            CollectedMemoryIds.Clear();
            if (data.collectedEchoMemoryIds != null)
                for (int i = 0; i < data.collectedEchoMemoryIds.Count; i++) CollectedMemoryIds.Add(data.collectedEchoMemoryIds[i]);
            UnlockedRemendoIds.Clear();
            if (data.unlockedRemendoIds != null)
                for (int i = 0; i < data.unlockedRemendoIds.Count; i++) UnlockedRemendoIds.Add(data.unlockedRemendoIds[i]);
            EquippedRemendoId = data.equippedRemendoId ?? string.Empty;
            ApplyAllUpgradeEffectsToScene();
        }
    }
}
