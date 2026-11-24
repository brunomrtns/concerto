using UnityEngine;

namespace Studio.Game.Combat
{
    /// <summary>
    /// Espaco Seguro de TEACH do ABSORVER (DESIGN_BIBLE Secao 5 — teach -> test -> twist):
    ///   - Posicionado na oficina (Caldeiral) em area segura (dano = 0!)
    ///   - Dispara 1 pulso lento telegrafado para o jogador aprender o timing de [F] ABSORVER
    ///     antes de encontrar o inimigo Egide.
    /// </summary>
    public sealed class ParryTeachStation : MonoBehaviour
    {
        public float pulseIntervalSec = 3.2f;
        public float pulseSpeed = 5.5f;
        public bool TeachCompleted { get; private set; }
        public int SafePulsesEmitted { get; private set; }

        private float _timer = 1.5f;
        private Transform _player;

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null) _player = dez.transform;
            if (ParryAndLockOnController.Instance != null)
                ParryAndLockOnController.Instance.OnParrySucceeded += HandleTeachParry;
        }

        private void OnDestroy()
        {
            if (ParryAndLockOnController.Instance != null)
                ParryAndLockOnController.Instance.OnParrySucceeded -= HandleTeachParry;
        }

        private void HandleTeachParry(string kind)
        {
            if (!TeachCompleted && _player != null && Vector3.Distance(_player.position, transform.position) <= 12f)
            {
                TeachCompleted = true;
                Debug.Log("[ParryTeachStation] TEACH concluido: jogador executou ABSORVER em espaco seguro!");
            }
        }

        public ManifestationProjectile EmitSafeTrainingPulse()
        {
            if (_player == null) return null;
            SafePulsesEmitted++;
            Vector3 muzzle = transform.position + Vector3.up * 1.1f;
            Vector3 dir = ((_player.position + Vector3.up * 0.9f) - muzzle).normalized;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SafeTeach_ParryPulse";
            go.transform.position = muzzle;
            go.transform.localScale = Vector3.one * 0.42f;
            Destroy(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 0.95f, 1.0f, 1f));
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var proj = go.AddComponent<ManifestationProjectile>();
            proj.direction = dir;
            proj.speed = pulseSpeed;
            proj.lifetimeSec = 3.5f;
            proj.damageSegments = 0; // Espaco SEGURO: 0 dano!
            return proj;
        }

        private void Update()
        {
            if (TeachCompleted || _player == null) return;
            if (Vector3.Distance(_player.position, transform.position) > 10f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = pulseIntervalSec;
                EmitSafeTrainingPulse();
            }
        }
    }
}
