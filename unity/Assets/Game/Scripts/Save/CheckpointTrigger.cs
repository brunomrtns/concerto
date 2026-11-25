using UnityEngine;

namespace Studio.Game.Save
{
    /// <summary>
    /// Trigger discreto de Checkpoint por entrada de area na oficina (auto-save silencioso).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CheckpointTrigger : MonoBehaviour
    {
        public string checkpointId = "cp_oficina_area";
        public bool oneShotPerSession = false;

        private bool _triggered;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (!other.CompareTag("Player") && !other.name.Contains("Player_Dez")) return;
            if (oneShotPerSession && _triggered) return;

            _triggered = true;
            Vector3 groundPos = new Vector3(transform.position.x, other.transform.position.y, transform.position.z);
            SaveSystem.Save(checkpointId, groundPos, other.transform.eulerAngles.y);
        }
    }
}
