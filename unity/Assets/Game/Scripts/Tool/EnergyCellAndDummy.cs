using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Tool
{
    [Serializable]
    public sealed class CellSpawnPoint
    {
        public string id;
        public float x;
        public float y;
        public float z;
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public sealed class EnergyCellsConfigData
    {
        public List<CellSpawnPoint> cells = new List<CellSpawnPoint>();

        public static EnergyCellsConfigData FromJson(TextAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return new EnergyCellsConfigData();
            return JsonUtility.FromJson<EnergyCellsConfigData>(asset.text) ?? new EnergyCellsConfigData();
        }
    }

    /// <summary>
    /// Celula de Energia coletavel (placeholder em pontos config ate existir drop de inimigo).
    /// </summary>
    public sealed class EnergyCellPickup : MonoBehaviour
    {
        public int cellAmount = 1;
        public bool IsCollected { get; private set; }

        private Vector3 _basePos;

        private void Awake() => _basePos = transform.position;

        private void Update()
        {
            if (IsCollected) return;
            transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);
            transform.position = _basePos + Vector3.up * (Mathf.Sin(Time.time * 2.8f) * 0.06f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsCollected || other == null) return;
            if (other.CompareTag("Player") || other.name.Contains("Player_Dez"))
                TryCollect();
        }

        public bool TryCollect()
        {
            if (IsCollected || MultiToolController.Instance == null) return false;
            if (!MultiToolController.Instance.AddEnergyCells(cellAmount)) return false;
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }

    /// <summary>
    /// Alvo de treino mecanico na oficina (NAO e inimigo — apenas prova hitbox, hitstop, shake e dano de bolt!).
    /// </summary>
    public sealed class WorkshopTrainingTarget : MonoBehaviour, IDamageable
    {
        public bool IsAlive => true;
        public int HitsReceived { get; private set; }
        public float TotalDamageReceived { get; private set; }

        private Renderer _rend;
        private Color _baseColor;
        private float _flashTimer;
        private Vector3 _basePos;

        private void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            if (_rend != null) _baseColor = _rend.material.color;
            _basePos = transform.position;
        }

        public void ApplyDamage(ToolHitInfo hit)
        {
            HitsReceived++;
            TotalDamageReceived += hit.Damage;
            _flashTimer = 0.14f;
            if (_rend != null) _rend.material.color = hit.IsHeavy ? Color.yellow : Color.white;
            transform.position = _basePos + hit.KnockbackDir * (hit.IsHeavy ? 0.16f : 0.08f);
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                if (_flashTimer <= 0f)
                {
                    if (_rend != null) _rend.material.color = _baseColor;
                    transform.position = _basePos;
                }
            }
        }
    }
}
