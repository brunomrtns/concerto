using System;
using System.Collections.Generic;
using Studio.Game.Combat;
using UnityEngine;

namespace Studio.Game.Retrogrado
{
    /// <summary>
    /// GATE 4 (Retrogrado — Boss O ARQUIVISTA-MOR):
    ///   - Inimigo que "ja sabe" seus padroes: registra cada padrao usado pelo jogador e ANTECIPA/BLOQUEIA
    ///     qualquer repeticao do combo favorito.
    ///   - Quebra-se ele usando padroes que ele AINDA NAO VIU (variar combo/habilidades + Contratempo).
    ///   - Posto de Reparo na arena com reinicio rapido <= 8s.
    /// </summary>
    public sealed class RetrogradoBossArquivistaMor : MonoBehaviour
    {
        public static RetrogradoBossArquivistaMor Instance { get; private set; }

        public Transform repairStationAnchor;

        public int RequiredNovelPatterns { get; } = 3;
        public int CurrentPhase { get; private set; } = 1; // 1 = Leitura, 2 = Antecipacao Total (exige Contratempo), 3 = Quebra Inedita
        public bool IsParryGateActive { get; private set; }
        public bool IsDefeated { get; private set; }
        public float LastRestartDurationSec { get; private set; }

        public HashSet<string> KnownPatterns { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int NovelPatternsLandedCount { get; private set; }
        public int AnticipatedBlockCount { get; private set; }
        public string LastAnticipatedPattern { get; private set; } = string.Empty;

        public event Action OnArquivistaDefeated;

        private MeshRenderer _bodyRenderer;

        private void Awake()
        {
            Instance = this;
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Tenta atingir o Arquivista-Mor com um padrao nomeado (ex: "Sopro", "Rebote", "CravaMola", "MarteladaAerea").
        /// Se ele JA conhece o padrao, antecipa e bloqueia (retorna false). Se for INEDITO, quebra a previsao!
        /// </summary>
        public bool TryStrikeWithPattern(string patternName)
        {
            if (IsDefeated || string.IsNullOrEmpty(patternName)) return false;

            if (IsParryGateActive)
            {
                AnticipatedBlockCount++;
                LastAnticipatedPattern = patternName;
                return false;
            }

            if (KnownPatterns.Contains(patternName))
            {
                AnticipatedBlockCount++;
                LastAnticipatedPattern = patternName;
                Debug.Log($"[ArquivistaMor] Padrao '{patternName}' JA CONHECIDO — antecipado e bloqueado!");
                return false;
            }

            KnownPatterns.Add(patternName);
            NovelPatternsLandedCount++;

            if (NovelPatternsLandedCount == 1)
            {
                CurrentPhase = 2;
                IsParryGateActive = true;
                SetTint(new Color(0.96f, 0.64f, 0.18f, 1f));
            }
            else if (NovelPatternsLandedCount >= RequiredNovelPatterns)
            {
                CurrentPhase = 3;
                IsDefeated = true;
                SetTint(new Color(0.28f, 0.92f, 0.84f, 1f));
                OnArquivistaDefeated?.Invoke();
                Debug.Log("[ArquivistaMor] Previsao quebrada com 3 padroes ineditos + Contratempo!");
            }
            return true;
        }

        public bool ApplyContratempoParry()
        {
            if (IsDefeated || !IsParryGateActive) return false;
            IsParryGateActive = false;
            CurrentPhase = 3;
            KnownPatterns.Add("Contratempo");
            SetTint(new Color(0.95f, 0.85f, 0.30f, 1f));
            return true;
        }

        public void RestartEncounterAtRepairStation(GameObject player)
        {
            float t0 = Time.realtimeSinceStartup;
            KnownPatterns.Clear();
            NovelPatternsLandedCount = 0;
            AnticipatedBlockCount = 0;
            LastAnticipatedPattern = string.Empty;
            CurrentPhase = 1;
            IsParryGateActive = false;
            IsDefeated = false;
            SetTint(Color.white);

            if (player != null && repairStationAnchor != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = repairStationAnchor.position + Vector3.up * 0.1f;
                if (cc != null) cc.enabled = true;
            }
            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.RestoreFullHealth();
            LastRestartDurationSec = Time.realtimeSinceStartup - t0;
        }

        private void SetTint(Color c)
        {
            if (_bodyRenderer == null) return;
            var mpb = new MaterialPropertyBlock();
            _bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            _bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}
