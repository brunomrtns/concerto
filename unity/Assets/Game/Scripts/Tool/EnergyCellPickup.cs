using UnityEngine;

namespace Studio.Game.Tool
{
    /// <summary>
    /// Celula de Energia coletavel.
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
}
