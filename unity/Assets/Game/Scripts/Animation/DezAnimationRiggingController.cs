using Studio.Game.Collision;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Studio.Game.Animation
{
    /// <summary>
    /// GATE 4 — Unity Animation Rigging (com.unity.animation.rigging 1.3.0):
    ///   1. Right Hand Interact IK (TwoBoneIKConstraint oficial): estende o braco direito de forma natural
    ///      para agarrar/interagir com objetos da fase (bancada, pecas, paineis, NPCs).
    ///   2. Foot Grounding IK (OnAnimatorIK + raycast em terreno plano): planta os pes do Dez no piso
    ///      sem deslizar ou flutuar.
    /// ZERO animacao manual de corpo em C# — apenas IK oficial sobre os clipes Humanoid reais!
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class DezAnimationRiggingController : MonoBehaviour
    {
        [Header("Hand Interact IK (Unity Animation Rigging — TwoBoneIKConstraint)")]
        [Range(0f, 1f)] public float maxHandIkWeight = 0.92f;
        public float handIkBlendSpeed = 6.5f;

        [Header("Foot Grounding IK (Pes plantados no chao)")]
        public bool enableFootGrounding = true;
        public float footRayUpOffset = 0.45f;
        public float footRayDistance = 0.85f;
        public float footSoleOffset = 0.065f;
        [Range(0f, 1f)] public float footIkWeight = 0.85f;

        public bool IsRigBuilt { get; private set; }
        public float CurrentHandIkWeight => _rightHandConstraint != null ? _rightHandConstraint.weight : 0f;
        public bool HasActiveInteractTarget => _hasManualTarget || _autoInteractActive;

        private Animator _anim;
        private RigBuilder _rigBuilder;
        private Rig _interactRig;
        private TwoBoneIKConstraint _rightHandConstraint;
        private Transform _handTarget;
        private Transform _handHint;

        private bool _hasManualTarget;
        private bool _autoInteractActive;
        private Vector3 _interactWorldPos;
        private int _groundMask;
        private readonly Collider[] _interactProbeBuf = new Collider[16];

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _groundMask = (1 << CollisionMatrixBootstrap.LayerEnvironment) | (1 << CollisionMatrixBootstrap.LayerDefault);
            BuildAnimationRiggingSetup();
        }

        private void BuildAnimationRiggingSetup()
        {
            if (_anim == null || !_anim.isHuman) return;

            Transform rUpper = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLower = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand  = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (rUpper == null || rLower == null || rHand == null) return;

            _rigBuilder = GetComponent<RigBuilder>();
            if (_rigBuilder == null) _rigBuilder = gameObject.AddComponent<RigBuilder>();

            var rigGo = new GameObject("Dez_InteractIK_Rig");
            rigGo.transform.SetParent(transform, false);
            _interactRig = rigGo.AddComponent<Rig>();
            _interactRig.weight = 1f;

            var ikGo = new GameObject("RightHand_TwoBoneIK");
            ikGo.transform.SetParent(rigGo.transform, false);
            _rightHandConstraint = ikGo.AddComponent<TwoBoneIKConstraint>();

            var targetGo = new GameObject("RightHand_Target");
            targetGo.transform.SetParent(ikGo.transform, false);
            targetGo.transform.position = rHand.position;
            targetGo.transform.rotation = rHand.rotation;
            _handTarget = targetGo.transform;

            var hintGo = new GameObject("RightHand_ElbowHint");
            hintGo.transform.SetParent(ikGo.transform, false);
            hintGo.transform.position = rLower.position - transform.forward * 0.35f + transform.right * 0.20f;
            _handHint = hintGo.transform;

            var data = _rightHandConstraint.data;
            data.root = rUpper;
            data.mid = rLower;
            data.tip = rHand;
            data.target = _handTarget;
            data.hint = _handHint;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 0.65f;
            data.hintWeight = 0.55f;
            _rightHandConstraint.data = data;
            _rightHandConstraint.weight = 0f;

            _rigBuilder.layers.Clear();
            _rigBuilder.layers.Add(new RigLayer(_interactRig, true));
            _rigBuilder.Build();
            IsRigBuilt = true;
        }

        public void SetInteractTarget(Vector3 worldPos)
        {
            _hasManualTarget = true;
            _interactWorldPos = worldPos;
        }

        public void ClearInteractTarget()
        {
            _hasManualTarget = false;
        }

        private void LateUpdate()
        {
            if (_rightHandConstraint == null || _handTarget == null) return;

            // Detecta automaticamente interacao proxima na fase 1 quando o jogador aperta [E] perto de objeto/bancada
            _autoInteractActive = false;
            Vector3 desiredTarget = _interactWorldPos;

            if (!_hasManualTarget && Input.GetKey(KeyCode.E))
            {
                int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.9f, 2.2f, _interactProbeBuf, ~0, QueryTriggerInteraction.Collide);
                float bestSq = float.MaxValue;
                Transform bestTr = null;
                for (int i = 0; i < count; i++)
                {
                    var col = _interactProbeBuf[i];
                    if (col == null || col.transform.root == transform.root) continue;
                    var interactable = col.GetComponentInParent<IInteractable>();
                    if (interactable == null) continue;
                    float dSq = (col.transform.position - transform.position).sqrMagnitude;
                    if (dSq < bestSq) { bestSq = dSq; bestTr = col.transform; }
                }
                if (bestTr != null)
                {
                    _autoInteractActive = true;
                    Vector3 toTarget = bestTr.position - transform.position;
                    float dist = Mathf.Min(0.68f, toTarget.magnitude);
                    Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
                    if (flatDir.sqrMagnitude < 0.01f) flatDir = transform.forward;
                    desiredTarget = transform.position + flatDir.normalized * dist + Vector3.up * 1.02f;
                }
            }

            float targetW = (_hasManualTarget || _autoInteractActive) ? maxHandIkWeight : 0f;
            _rightHandConstraint.weight = Mathf.MoveTowards(_rightHandConstraint.weight, targetW, handIkBlendSpeed * Time.deltaTime);

            if (_rightHandConstraint.weight > 0.005f)
            {
                _handTarget.position = Vector3.Lerp(_handTarget.position, desiredTarget, 14f * Time.deltaTime);
                Vector3 reachDir = (desiredTarget - transform.position);
                reachDir.y = 0f;
                if (reachDir.sqrMagnitude > 0.001f)
                    _handTarget.rotation = Quaternion.LookRotation(reachDir.normalized, Vector3.up);
                if (_handHint != null)
                    _handHint.position = transform.position + transform.right * 0.45f - transform.forward * 0.25f + Vector3.up * 0.90f;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableFootGrounding || _anim == null || !_anim.isHuman) return;
            if (!_anim.GetBool("Grounded"))
            {
                _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }

            SolveFootGoal(AvatarIKGoal.LeftFoot);
            SolveFootGoal(AvatarIKGoal.RightFoot);
        }

        private void SolveFootGoal(AvatarIKGoal goal)
        {
            Vector3 footPos = _anim.GetIKPosition(goal);
            Vector3 rayOrigin = footPos + Vector3.up * footRayUpOffset;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, footRayDistance, _groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 targetPos = hit.point + Vector3.up * footSoleOffset;
                // Apenas planta no piso quando o pe esta proximo da altura de apoio (nao puxa pe levantado no passo!)
                float heightAboveGround = footPos.y - targetPos.y;
                float dynamicWeight = heightAboveGround <= 0.14f ? footIkWeight : 0f;

                _anim.SetIKPositionWeight(goal, dynamicWeight);
                _anim.SetIKRotationWeight(goal, dynamicWeight * 0.75f);
                if (dynamicWeight > 0.01f)
                {
                    _anim.SetIKPosition(goal, targetPos);
                    Quaternion footRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                    _anim.SetIKRotation(goal, footRot);
                }
            }
            else
            {
                _anim.SetIKPositionWeight(goal, 0f);
                _anim.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}
