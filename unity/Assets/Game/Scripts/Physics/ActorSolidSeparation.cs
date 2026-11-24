using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Collision
{
    public enum ActorBodyKind
    {
        Player = 0,
        NPC = 1,
        Enemy = 2,
    }

    /// <summary>
    /// ActorSolidSeparation — Resolve sobreposicao entre personagens de forma 100% deterministica (SEM jitter, SEM empurrao aleatorio!):
    ///   - Causa raiz corrigida: dois CharacterControllers nao colidem/separam entre si por padrao na Unity.
    ///   - Garante CapsuleCollider solido dedicado em NPCs/Enemies (radius=0.30m << interactRadius=2.5m — nunca quebra conversa!).
    ///   - Regras de separacao:
    ///       Player <-> NPC   : Player desliza ao redor (100% no Player, NPC permanece estavel na marca).
    ///       Player <-> Enemy : 30% Player / 70% Enemy (regra de justica: inimigo NUNCA prende player em canto!).
    ///       Enemy  <-> Enemy : 50% / 50% mutuo (nunca empilham no mesmo ponto).
    /// </summary>
    public sealed class ActorSolidSeparation : MonoBehaviour
    {
        private static readonly List<ActorSolidSeparation> ActiveActors = new List<ActorSolidSeparation>();

        [Header("Identidade & Corpo Solido")]
        public ActorBodyKind bodyKind = ActorBodyKind.NPC;
        public float solidRadius = 0.30f;
        public float solidHeight = 1.25f;
        public bool createSolidCapsuleIfMissing = true;

        private CharacterController _cc;
        private CapsuleCollider _solidCapsule;

        public bool IsSeparationActive => isActiveAndEnabled && gameObject.activeInHierarchy && (_cc == null || _cc.enabled);
        public int ResolutionsApplied { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            EnsureSolidCapsule();
        }

        private void OnEnable()
        {
            if (!ActiveActors.Contains(this))
                ActiveActors.Add(this);
        }

        private void OnDisable()
        {
            ActiveActors.Remove(this);
        }

        private void EnsureSolidCapsule()
        {
            if (!createSolidCapsuleIfMissing || bodyKind == ActorBodyKind.Player) return;

            // Capsule solida dedicada em filho para nao conflitar com triggers de interacao nem com CharacterController
            Transform child = transform.Find("SolidBody_Capsule");
            if (child == null)
            {
                var go = new GameObject("SolidBody_Capsule");
                go.transform.SetParent(transform, false);
                go.layer = bodyKind == ActorBodyKind.Enemy
                    ? CollisionMatrixBootstrap.LayerEnemy
                    : CollisionMatrixBootstrap.LayerNPC;
                child = go.transform;
            }

            _solidCapsule = child.GetComponent<CapsuleCollider>();
            if (_solidCapsule == null) _solidCapsule = child.gameObject.AddComponent<CapsuleCollider>();
            _solidCapsule.isTrigger = false;
            _solidCapsule.radius = Mathf.Clamp(solidRadius, 0.22f, 0.45f);
            _solidCapsule.height = Mathf.Max(0.8f, solidHeight);
            _solidCapsule.center = new Vector3(0f, _solidCapsule.height * 0.5f, 0f);
        }

        private void LateUpdate()
        {
            // Apenas o Player e os Enemies executam o passe deterministico de separacao (evita trabalho duplicado)
            if (bodyKind == ActorBodyKind.NPC || !IsSeparationActive) return;

            Vector3 myPos = transform.position;
            for (int i = 0; i < ActiveActors.Count; i++)
            {
                var other = ActiveActors[i];
                if (other == null || other == this || !other.IsSeparationActive) continue;

                // Ignora pares em alturas totalmente distintas (ex.: pisos diferentes)
                if (Mathf.Abs(myPos.y - other.transform.position.y) > Mathf.Max(solidHeight, other.solidHeight))
                    continue;

                Vector3 delta = myPos - other.transform.position;
                delta.y = 0f;
                float minDist = solidRadius + other.solidRadius;
                float distSq = delta.sqrMagnitude;

                if (distSq >= minDist * minDist) continue;

                float dist = Mathf.Sqrt(distSq);
                Vector3 pushDir;
                if (dist > 0.001f)
                {
                    pushDir = delta / dist;
                }
                else
                {
                    // Desempate 100% deterministico por InstanceID (NUNCA aleatorio!)
                    float sign = GetInstanceID() >= other.GetInstanceID() ? 1f : -1f;
                    pushDir = new Vector3(sign, 0f, 0f);
                }

                float overlap = (minDist - dist) + 0.005f;
                float myWeight = GetPushWeightAgainst(bodyKind, other.bodyKind);
                if (myWeight <= 0f) continue;

                Vector3 push = pushDir * (overlap * myWeight);
                if (_cc != null && _cc.enabled)
                    _cc.Move(push);
                else
                    transform.position += push;

                myPos = transform.position;
                ResolutionsApplied++;
            }
        }

        private static float GetPushWeightAgainst(ActorBodyKind self, ActorBodyKind other)
        {
            if (self == ActorBodyKind.Player && other == ActorBodyKind.NPC)   return 1.0f; // Player desliza ao redor do NPC
            if (self == ActorBodyKind.Player && other == ActorBodyKind.Enemy) return 0.3f; // Regra de justica: inimigo nao prende player
            if (self == ActorBodyKind.Enemy  && other == ActorBodyKind.Player) return 0.7f;
            if (self == ActorBodyKind.Enemy  && other == ActorBodyKind.Enemy)  return 0.5f;
            if (self == ActorBodyKind.Enemy  && other == ActorBodyKind.NPC)    return 1.0f;
            return 0f;
        }
    }
}
