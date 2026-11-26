using System;
using System.Collections.Generic;
using Studio.Game.Combat;
using UnityEngine;

namespace Studio.Game.Ecodia
{
    /// <summary>
    /// GATE 4 (Ecodia — Boss ECO DEZ):
    ///   - Um eco de Desiderio que repete seu erro: erra as piadas na ordem errada (punchline antes do setup — drama+humor).
    ///   - Padrao espelha o moveset do player (Sopro, Rebote, Crava-Mola).
    ///   - Fase 2 exige Contratempo (Parry) para quebrar a guarda espelhada.
    ///   - Posto de Reparo na arena com reinicio rapido <= 8s.
    /// </summary>
    public sealed class EcodiaBossEcoDez : MonoBehaviour
    {
        public static EcodiaBossEcoDez Instance { get; private set; }

        public Transform repairStationAnchor;

        public float MaxHealth { get; } = 12f;
        public float CurrentHealth { get; private set; } = 12f;
        public int CurrentPhase { get; private set; } = 1; // 1 = Espelho Sopro/Rebote, 2 = Guarda Contratempo, 3 = Espelho Final
        public bool IsParryGateActive { get; private set; }
        public bool IsDefeated => CurrentHealth <= 0f;
        public float LastRestartDurationSec { get; private set; }

        // Drama+Humor: Eco Dez conta as piadas do Desiderio na ordem errada (punchline antes do setup!)
        public string LastWrongOrderJokeLine { get; private set; } = string.Empty;
        public int WrongOrderJokeCount { get; private set; }
        public List<string> MirroredMovesetHistory { get; } = new List<string>();

        private static readonly string[] WrongOrderJokes =
        {
            "...e aí o ponteiro caiu na sopa! Espera — era pra eu ter falado do relógio primeiro, né?",
            "TOC-TOC! Ah não, eu já dei a resposta antes da pergunta de novo...",
            "Porque o tempo não espera ninguém! ...Calma, qual era a pergunta mesmo, Dez?"
        };

        public event Action OnEcoDezDefeated;

        private MeshRenderer _bodyRenderer;

        private void Awake()
        {
            Instance = this;
            CurrentHealth = MaxHealth;
            CurrentPhase = 1;
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void MirrorPlayerMove(string moveName)
        {
            if (string.IsNullOrEmpty(moveName)) return;
            MirroredMovesetHistory.Add($"Mirror_{moveName}");
            TriggerWrongOrderJoke();
        }

        public void TriggerWrongOrderJoke()
        {
            LastWrongOrderJokeLine = WrongOrderJokes[WrongOrderJokeCount % WrongOrderJokes.Length];
            WrongOrderJokeCount++;
        }

        public bool ApplyDamage(float dmg)
        {
            if (IsDefeated) return false;
            if (IsParryGateActive)
            {
                MirrorPlayerMove("Sopro");
                return false;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0.5f, dmg));
            if (CurrentPhase == 1 && CurrentHealth <= MaxHealth * 0.60f)
            {
                CurrentPhase = 2;
                IsParryGateActive = true;
                MirrorPlayerMove("Rebote");
                SetTint(new Color(1.0f, 0.56f, 0.22f, 1f));
            }
            else if (CurrentHealth <= 0f)
            {
                CurrentPhase = 3;
                SetTint(new Color(0.28f, 0.90f, 1.0f, 1f));
                OnEcoDezDefeated?.Invoke();
            }
            return true;
        }

        public bool ApplyContratempoParry()
        {
            if (IsDefeated || !IsParryGateActive) return false;
            IsParryGateActive = false;
            CurrentPhase = 3;
            MirrorPlayerMove("CravaMola");
            SetTint(new Color(0.95f, 0.80f, 0.26f, 1f));
            return true;
        }

        public void RestartEncounterAtRepairStation(GameObject player)
        {
            float t0 = Time.realtimeSinceStartup;
            CurrentHealth = MaxHealth;
            CurrentPhase = 1;
            IsParryGateActive = false;
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
