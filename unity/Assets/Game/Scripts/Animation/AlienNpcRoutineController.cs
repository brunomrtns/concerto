using System;
using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// AlienNpcRoutineController — Comportamento vivo e com proposito para NPCs alienigenas da Oficina e Caldeiral:
    ///   - Rotina de estacoes reais (inspecionar nave, checar cristais na estante, trabalhar na bancada, checar canos)
    ///   - Caminhada suave com gravidade (CharacterController) usando Clean_Walk / Clean_Idle do HeroDezController
    ///   - Micro-olhar natural durante inspecao (observa pecas/instrumentos sem parecer boneco duro)
    ///   - Reacao viva ao jogador (Dex): quando Dex se aproxima (< 2.8m), o NPC pausa/cumprimenta olhando para Dex
    ///     e retoma o trabalho naturalmente quando Dex se afasta.
    /// </summary>
    public sealed class AlienNpcRoutineController : MonoBehaviour
    {
        [Serializable]
        public struct RoutineStop
        {
            public string activityName;
            public Vector3 position;
            public Vector3 lookTarget;
            public float dwellSeconds;
        }

        [Header("Identidade & Rotina")]
        public string npcDisplayName = "Alien Technician";
        public RoutineStop[] stops = Array.Empty<RoutineStop>();
        public bool stationaryMode = false;

        [Header("Movimento")]
        public float walkSpeed = 1.35f;
        public float turnSpeedDeg = 240f;
        public float stopDistance = 0.25f;
        public float gravity = -18f;

        [Header("Reacao ao Jogador (Dex)")]
        public float playerGreetRadius = 2.8f;
        public float playerReleaseRadius = 3.6f;

        private CharacterController _cc;
        private Animator _anim;
        private Transform _player;

        private int _currentStopIdx;
        private float _dwellTimer;
        private float _currentSpeed;
        private float _verticalVel;
        private bool _isGreetingPlayer;
        private float _inspectPhaseOffset;
        private Vector3 _homePos;
        private Quaternion _homeRot;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.height = 1.25f;
                _cc.radius = 0.28f;
                _cc.center = new Vector3(0f, 0.625f, 0f);
                _cc.slopeLimit = 45f;
                _cc.stepOffset = 0.25f;
                _cc.skinWidth = 0.01f;
            }
            _anim = GetComponentInChildren<Animator>(true);
            _inspectPhaseOffset = UnityEngine.Random.Range(0f, 10f);
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

        private void Start()
        {
            var playerGo = PlayerRegistry.Resolve();
            if (playerGo != null) _player = playerGo.transform;
            if (stops != null && stops.Length > 0)
            {
                _currentStopIdx = 0;
                _dwellTimer = Mathf.Max(2f, stops[0].dwellSeconds);
            }
        }

        private void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);

            // Verifica proximidade do Dex para reacao viva
            float distToPlayer = _player != null ? Vector3.Distance(transform.position, _player.position) : 999f;
            if (!_isGreetingPlayer && distToPlayer < playerGreetRadius)
                _isGreetingPlayer = true;
            else if (_isGreetingPlayer && distToPlayer > playerReleaseRadius)
                _isGreetingPlayer = false;

            Vector3 moveDir = Vector3.zero;
            float targetSpeed = 0f;

            if (_isGreetingPlayer && _player != null)
            {
                // Pausa educadamente e olha para o Dex
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDeg * dt);
                }
            }
            else if (!stationaryMode && stops != null && stops.Length > 0)
            {
                RoutineStop cur = stops[_currentStopIdx];
                Vector3 toTarget = cur.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > stopDistance)
                {
                    // Caminhando ate a proxima estacao de trabalho
                    moveDir = toTarget / dist;
                    targetSpeed = walkSpeed;
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDeg * dt);
                }
                else
                {
                    // Chegou na estacao: vira para o ponto de interesse e trabalha/inspeciona
                    Vector3 toLook = cur.lookTarget - transform.position;
                    toLook.y = 0f;
                    if (toLook.sqrMagnitude > 0.01f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(toLook.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, turnSpeedDeg * 0.8f * dt);
                    }

                    _dwellTimer -= dt;
                    if (_dwellTimer <= 0f)
                    {
                        _currentStopIdx = (_currentStopIdx + 1) % stops.Length;
                        _dwellTimer = Mathf.Max(2.5f, stops[_currentStopIdx].dwellSeconds);
                    }
                }
            }
            else
            {
                // NPC estacionario: mantem orientacao natural no posto de trabalho
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _homeRot, turnSpeedDeg * 0.5f * dt);
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, 6.0f * dt);

            if (_cc.isGrounded)
            {
                if (_verticalVel < -1.0f) _verticalVel = -1.0f;
            }
            else
            {
                _verticalVel += gravity * dt;
            }

            Vector3 prevPos = transform.position;
            Vector3 motion = (moveDir * _currentSpeed + Vector3.up * _verticalVel) * dt;
            _cc.Move(motion);

            Vector3 moved = transform.position - prevPos;
            moved.y = 0f;
            float actualHorizSpeed = moved.magnitude / dt;

            if (_anim != null)
            {
                float animSpeedVal = actualHorizSpeed > 0.08f ? actualHorizSpeed : 0f;
                _anim.SetFloat("Speed", animSpeedVal, 0.08f, dt);
                _anim.SetBool("IsGrounded", _cc.isGrounded);
                // Casa cadencia dos passos com velocidade REAL percorrida no chao (ZERO moonwalk/deslize!)
                float mult = animSpeedVal > 0.08f ? Mathf.Clamp(actualHorizSpeed / 2.15f, 0.45f, 1.25f) : 1.0f;
                _anim.SetFloat("LocomotionSpeedMult", mult);
                _anim.SetFloat("VerticalVel", _verticalVel);
            }
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isHuman) return;
            Transform head = _anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            // Quando parado trabalhando ou cumprimentando Dex, adiciona micro-movimento vivo de cabeca
            if (_currentSpeed < 0.15f)
            {
                float t = Time.time + _inspectPhaseOffset;
                float yawDeg;
                float pitchDeg;
                if (_isGreetingPlayer)
                {
                    // Aceno sutil simpatico olhando pro Dex
                    yawDeg = Mathf.Sin(t * 1.4f) * 5.0f;
                    pitchDeg = Mathf.Sin(t * 2.2f) * 3.5f;
                }
                else
                {
                    // Inspecionando pecas/instrumentos/cristais (olhar curioso esquerda/direita/foco)
                    yawDeg = Mathf.Sin(t * 0.75f) * 14.0f;
                    pitchDeg = Mathf.Sin(t * 1.15f) * 5.5f;
                }
                head.rotation = Quaternion.AngleAxis(yawDeg, transform.up)
                              * Quaternion.AngleAxis(pitchDeg, transform.right)
                              * head.rotation;
            }
        }
    }
}
