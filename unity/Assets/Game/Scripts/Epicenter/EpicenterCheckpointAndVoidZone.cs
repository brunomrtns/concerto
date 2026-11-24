using System;
using Studio.Game.Animation;
using Studio.Game.Combat;
using UnityEngine;

namespace Studio.Game.Epicenter
{
    /// <summary>
    /// Gerencia checkpoints por secao do Epicentro (S1 Travessia / S2 Combate / S3 Arena),
    /// gravidade levemente instavel (tunavel em EpicenterConfig) e queda no vazio -> respawn limpo.
    /// </summary>
    public sealed class EpicenterCheckpointAndVoidZone : MonoBehaviour
    {
        public static EpicenterCheckpointAndVoidZone Instance { get; private set; }

        public EpicenterConfig config;
        public Transform[] sectionCheckpoints;

        public int ActiveSectionIndex { get; private set; }
        public Vector3 ActiveCheckpointPos { get; private set; }
        public int VoidRespawnCount { get; private set; }

        public event Action<int> OnSectionCheckpointReached;
        public event Action<Vector3> OnVoidRespawned;

        private Transform _player;
        private CharacterController _cc;
        private DezLocomotionController _loco;
        private float _origGravity;
        private bool _gravityApplied;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Restaura gravidade original do DezMovementConfig ao sair da cena
            if (_gravityApplied && _loco != null && _loco.config != null)
                _loco.config.gravity = _origGravity;
        }

        private void Start()
        {
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                _player = dez.transform;
                _cc = dez.GetComponent<CharacterController>();
                _loco = dez.GetComponent<DezLocomotionController>();
                ActiveCheckpointPos = _player.position;

                // Aplica gravidade levemente instavel do Epicentro (tunavel em config)
                if (_loco != null && _loco.config != null && config != null)
                {
                    _origGravity = _loco.config.gravity;
                    _loco.config.gravity = _origGravity * Mathf.Clamp(config.unstableGravityScale, 0.65f, 1.0f);
                    _gravityApplied = true;
                }
            }

            if (sectionCheckpoints != null && sectionCheckpoints.Length > 0 && sectionCheckpoints[0] != null)
                SetActiveCheckpoint(0);
        }

        private void Update()
        {
            if (_player == null) return;

            // Verifica avanco de secao por proximidade dos checkpoints
            if (sectionCheckpoints != null)
            {
                for (int i = ActiveSectionIndex + 1; i < sectionCheckpoints.Length; i++)
                {
                    var cp = sectionCheckpoints[i];
                    if (cp == null) continue;
                    Vector3 d = _player.position - cp.position;
                    d.y = 0f;
                    if (d.sqrMagnitude <= 16.0f)
                        SetActiveCheckpoint(i);
                }
            }

            // Vazio mata -> respawn limpo no checkpoint da secao
            float killY = config != null ? config.voidKillY : -7.5f;
            if (_player.position.y < killY)
                RespawnFromVoid();
        }

        public void SetActiveCheckpoint(int sectionIndex)
        {
            if (sectionCheckpoints == null || sectionIndex < 0 || sectionIndex >= sectionCheckpoints.Length) return;
            if (sectionCheckpoints[sectionIndex] == null) return;

            ActiveSectionIndex = sectionIndex;
            ActiveCheckpointPos = sectionCheckpoints[sectionIndex].position + Vector3.up * 0.15f;

            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.SetCheckpoint(ActiveCheckpointPos);

            OnSectionCheckpointReached?.Invoke(sectionIndex);
            Debug.Log($"[Epicenter] Checkpoint da Secao {sectionIndex + 1} ativado em {ActiveCheckpointPos}.");
        }

        public void RespawnFromVoid()
        {
            if (_player == null) return;
            if (_cc != null) _cc.enabled = false;
            _player.position = ActiveCheckpointPos;
            if (_cc != null) _cc.enabled = true;
            VoidRespawnCount++;
            OnVoidRespawned?.Invoke(ActiveCheckpointPos);
            Debug.Log($"[Epicenter] Queda no vazio detectada -> Respawn limpo no checkpoint da Secao {ActiveSectionIndex + 1} ({ActiveCheckpointPos}).");
        }
    }
}
