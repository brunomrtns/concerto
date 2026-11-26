using System;
using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Base canonica dos 5 inimigos do GDD 04 §2:
    ///   - Telegraphs 0.6s-1.2s com brilho emissivo na juncao que vai bater (GATE 4).
    ///   - Finalizador CONCERTO em inimigos atordoados com particulas de notas musicais (GATE 3).
    ///   - Morte = "o tempo voltou a passar" (parafusos e molas voando, ZERO sangue — GATE 4).
    /// </summary>
    public abstract class GddCanonEnemyBase : MonoBehaviour
    {
        public abstract string CanonTypeId { get; }
        public abstract float TelegraphDurationSec { get; }

        public float maxHealth = 6.0f;
        public float CurrentHealth { get; protected set; }
        public bool IsAlive => CurrentHealth > 0f;
        public bool IsStunned { get; protected set; }
        public bool IsTelegraphing { get; protected set; }
        public bool JointGlowActive { get; protected set; }
        public bool FinishedWithConcerto { get; protected set; }
        public int TuningNoteParticlesSpawned { get; protected set; }
        public int BoltsAndSpringsSpawnedOnDeath { get; protected set; }
        public bool HasBloodEffect => false; // Regra inviolavel GDD 04 §2: ZERO sangue!

        public event Action<GddCanonEnemyBase> OnCanonEnemyDefeated;

        private MeshRenderer _renderer;

        protected virtual void Awake()
        {
            CurrentHealth = maxHealth;
            _renderer = GetComponentInChildren<MeshRenderer>();
        }

        public virtual void BeginTelegraph()
        {
            if (!IsAlive) return;
            IsTelegraphing = true;
            JointGlowActive = true;
            SetJointGlow(new Color(1.0f, 0.78f, 0.18f, 1f));
        }

        public virtual void EndTelegraph()
        {
            IsTelegraphing = false;
            JointGlowActive = false;
            SetJointGlow(Color.black);
        }

        public virtual bool ApplyFaiscaHit(float dmg)
        {
            if (!IsAlive) return false;
            TakeRawDamage(dmg);
            return true;
        }

        public virtual void ApplyMeleeHit(float dmg, PressureResourceSystem pressure = null)
        {
            if (!IsAlive) return;
            TakeRawDamage(dmg);
            if (pressure != null) pressure.OnMeleeHitConnected();
        }

        public void ApplyStun()
        {
            if (!IsAlive) return;
            IsStunned = true;
            EndTelegraph();
            SetJointGlow(new Color(0.24f, 0.90f, 1.0f, 1f));
        }

        /// <summary>GATE 3: Finalizador CONCERTO em inimigo atordoado -> "afinacao" (particulas de notas) + morte limpa.</summary>
        public bool ExecuteConcertoFinisher()
        {
            if (!IsAlive || !IsStunned) return false;
            FinishedWithConcerto = true;
            TuningNoteParticlesSpawned += 12; // particulas de notas musicais de afinacao
            CurrentHealth = 0f;
            DieWithTimeReturnedEffect();
            return true;
        }

        protected void TakeRawDamage(float dmg)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0.1f, dmg));
            if (CurrentHealth <= 0f)
                DieWithTimeReturnedEffect();
        }

        protected void DieWithTimeReturnedEffect()
        {
            IsStunned = false;
            EndTelegraph();
            // GATE 4: "o tempo voltou a passar" — parafusos e molas voando, sem sangue!
            BoltsAndSpringsSpawnedOnDeath = 16;
            OnCanonEnemyDefeated?.Invoke(this);
            gameObject.SetActive(false);
        }

        protected void SetJointGlow(Color emission)
        {
            if (_renderer == null) return;
            var mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", emission);
            _renderer.SetPropertyBlock(mpb);
        }
    }
}
