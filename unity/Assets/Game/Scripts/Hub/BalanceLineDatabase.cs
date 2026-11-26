using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Hub
{
    [Serializable]
    public sealed class FusoBalanceEntry
    {
        public string fusoId;
        public string displayName;
        public string relicName;
        public string npcArrivalName;
        public Vector2 graphNormalizedPos;
        public string[] connectedFusoIds;
        [TextArea(2, 3)] public string benefitDescription;
        [TextArea(2, 3)] public string costDescription;
        public string costTargetFusoId;
        public string worldBenefitEventId;
        public string worldCostEventId;
    }

    /// <summary>
    /// GATE 3: Data Asset canonico da Linha do Equilibrio (GDD 01 §5).
    /// Cada conserto de fuso define exatamente 1 beneficio + 1 custo em outro fuso.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceLineDatabase", menuName = "Studio/Concerto/Balance Line Database")]
    public sealed class BalanceLineDatabase : ScriptableObject
    {
        public List<FusoBalanceEntry> fusos = new List<FusoBalanceEntry>();

        public static List<FusoBalanceEntry> BuildCanonicalGddEntries()
        {
            return new List<FusoBalanceEntry>
            {
                new FusoBalanceEntry
                {
                    fusoId = "caldeiral",
                    displayName = "Caldeiral",
                    relicName = "Relíquia: Mola",
                    npcArrivalName = "Tio Horário",
                    graphNormalizedPos = new Vector2(0.20f, 0.72f),
                    connectedFusoIds = new[] { "vitralha", "retrogrado" },
                    benefitDescription = "Energia estável para A Orquestra (hub ganha NPCs e loja de melhorias).",
                    costDescription = "Fumaça cessa -> céu limpo congela o Retrógrado por 1 'dia'.",
                    costTargetFusoId = "retrogrado",
                    worldBenefitEventId = "evt_benefit_caldeiral_hub_power",
                    worldCostEventId = "evt_cost_caldeiral_freeze_retrogrado"
                },
                new FusoBalanceEntry
                {
                    fusoId = "vitralha",
                    displayName = "Vitralha",
                    relicName = "Relíquia: Ponteiro Maior",
                    npcArrivalName = "Vó Segunda",
                    graphNormalizedPos = new Vector2(0.22f, 0.30f),
                    connectedFusoIds = new[] { "caldeiral", "maresia" },
                    benefitDescription = "Rio volta a correr -> novo acesso aquático em Marésia.",
                    costDescription = "Enchente alaga cavernas-habitação de Marésia (quest de desvio).",
                    costTargetFusoId = "maresia",
                    worldBenefitEventId = "evt_benefit_vitralha_river_flow",
                    worldCostEventId = "evt_cost_vitralha_flood_maresia"
                },
                new FusoBalanceEntry
                {
                    fusoId = "maresia",
                    displayName = "Marésia",
                    relicName = "Relíquia: Pêndulo-Âncora",
                    npcArrivalName = "Mecânico Brito",
                    graphNormalizedPos = new Vector2(0.50f, 0.22f),
                    connectedFusoIds = new[] { "vitralha", "ecodia" },
                    benefitDescription = "Mar degela -> rotas de barco entre fusos desbloqueadas.",
                    costDescription = "Ondas soltas arrastam um pedaço da Ecodia (prédio-quebra-cabeça).",
                    costTargetFusoId = "ecodia",
                    worldBenefitEventId = "evt_benefit_maresia_boat_routes",
                    worldCostEventId = "evt_cost_maresia_drift_ecodia"
                },
                new FusoBalanceEntry
                {
                    fusoId = "ecodia",
                    displayName = "Ecodia",
                    relicName = "Relíquia: Diapasão",
                    npcArrivalName = "Analista Luma",
                    graphNormalizedPos = new Vector2(0.78f, 0.30f),
                    connectedFusoIds = new[] { "maresia", "retrogrado" },
                    benefitDescription = "Habitantes saem do loop de 6 s (quests de memória abertas).",
                    costDescription = "Sem o loop, os Censores passam a patrulhar em tempo real (dificuldade sobe).",
                    costTargetFusoId = "ecodia",
                    worldBenefitEventId = "evt_benefit_ecodia_free_loop",
                    worldCostEventId = "evt_cost_ecodia_realtime_censors"
                },
                new FusoBalanceEntry
                {
                    fusoId = "retrogrado",
                    displayName = "Retrógrado",
                    relicName = "Relíquia: Ampulheta Invertida",
                    npcArrivalName = "Engenheira Vera",
                    graphNormalizedPos = new Vector2(0.80f, 0.72f),
                    connectedFusoIds = new[] { "ecodia", "caldeiral" },
                    benefitDescription = "Areia-parada revela ruínas do pré-Fratura (lore + desafios).",
                    costDescription = "O tempo reverso 'desfaz' melhorias cosméticas em Caldeiral (humor).",
                    costTargetFusoId = "caldeiral",
                    worldBenefitEventId = "evt_benefit_retrogrado_ruins",
                    worldCostEventId = "evt_cost_retrogrado_undo_caldeiral"
                }
            };
        }

        public List<FusoBalanceEntry> GetEntriesOrDefault()
        {
            if (fusos != null && fusos.Count >= 5) return fusos;
            return BuildCanonicalGddEntries();
        }
    }
}
