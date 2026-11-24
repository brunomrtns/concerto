using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Tool
{
    /// <summary>
    /// Modulo 2: Modo COMBATE.
    ///   - Combo leve de 2 golpes (toque rapido Botao Esquerdo / J — custo 0)
    ///   - Golpe carregado (segurar Botao Esquerdo / J >= 0.45s e soltar — custo 0)
    ///   - Tiro carregado (Bolt de energia — Botao Direito / K — consome 1 celula!)
    ///   - Hitbox honesta (OverlapSphere) + hitstop 65ms + camera shake sutil + som seco no acerto
    /// </summary>
    public sealed class CombatToolModule : ToolModuleBase
    {
        public override string ModuleId => "mode_combate";
        public override string DisplayName => "COMBATE";
        public override Color AccentColor => new Color(0.30f, 0.88f, 0.96f, 1f);
        public override bool AllowsRepairInteraction => false;

        private float _meleeCooldownUntil;
        private float _boltCooldownUntil;
        private float _comboExpireAt;
        private float _attackBufferTimer;
        private float _ecoCancelUntil;
        private int _comboStep;
        private bool _isChargingHeavy;
        private float _chargeTimer;

        public int LastComboStep { get; private set; }
        public int TotalLightHitsLanded { get; private set; }
        public int TotalHeavyHitsLanded { get; private set; }
        public int TotalBoltsFired { get; private set; }
        public int CancelIntoEcoCount { get; private set; }
        public bool IsInEcoCancelWindow => Time.time <= _ecoCancelUntil;
        public bool IsChargingHeavy => _isChargingHeavy;

        public bool TryCancelRecoveryWithEco()
        {
            if (!IsInEcoCancelWindow) return false;
            _meleeCooldownUntil = Time.time;
            _isChargingHeavy = false;
            _chargeTimer = 0f;
            _ecoCancelUntil = 0f;
            CancelIntoEcoCount++;
            return true;
        }
        public float ChargeProgress => Controller != null && Controller.Cfg != null
            ? Mathf.Clamp01(_chargeTimer / Mathf.Max(0.1f, Controller.Cfg.chargeHoldThresholdSec))
            : 0f;

        public override void OnUnequip()
        {
            _isChargingHeavy = false;
            _chargeTimer = 0f;
            _comboStep = 0;
        }

        public override void OnTick(float dt)
        {
            if (Controller == null || !Controller.CanUseCombatActionsNow()) return;
            var cfg = Controller.Cfg;

            bool attackDown = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
            bool attackHeld = Input.GetMouseButton(0) || Input.GetKey(KeyCode.J);
            bool attackUp   = Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.J);
            bool boltDown   = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.K);

            if (_attackBufferTimer > 0f)
            {
                _attackBufferTimer = Mathf.Max(0f, _attackBufferTimer - dt);
                if (Time.time >= _meleeCooldownUntil)
                {
                    _attackBufferTimer = 0f;
                    ExecuteLightComboStep();
                }
            }

            if (attackDown)
            {
                if (Time.time >= _meleeCooldownUntil)
                {
                    _isChargingHeavy = true;
                    _chargeTimer = 0f;
                }
                else
                {
                    _attackBufferTimer = cfg.attackInputBufferSec;
                }
            }

            if (_isChargingHeavy && attackHeld)
            {
                _chargeTimer += dt;
            }

            if (_isChargingHeavy && attackUp)
            {
                bool heavy = _chargeTimer >= cfg.chargeHoldThresholdSec;
                _isChargingHeavy = false;
                _chargeTimer = 0f;
                if (heavy) ExecuteHeavyStrike();
                else ExecuteLightComboStep();
            }

            if (boltDown && Time.time >= _boltCooldownUntil)
            {
                TryFireEnergyBolt();
            }
        }

        public bool ExecuteLightComboStep()
        {
            if (Controller == null || Time.time < _meleeCooldownUntil) return false;
            var cfg = Controller.Cfg;

            if (Time.time > _comboExpireAt) _comboStep = 0;
            _comboStep = (_comboStep % 3) + 1; // Combo de 3 golpes (1-2 rapidos, 3o punicao com hitstop maior e knockback!)
            LastComboStep = _comboStep;

            bool isFinisher = (_comboStep == 3);
            _meleeCooldownUntil = Time.time + (isFinisher ? cfg.meleeCooldownSec * 1.25f : cfg.meleeCooldownSec);
            _comboExpireAt = Time.time + cfg.comboWindowSec;
            _ecoCancelUntil = Time.time + cfg.ecoCancelWindowSec;

            float dmg = _comboStep == 1 ? cfg.lightHit1Damage : (_comboStep == 2 ? cfg.lightHit2Damage : cfg.lightHit3Damage);
            float radius = isFinisher ? cfg.heavyRadius : cfg.meleeRadius;
            Controller.PlayToolSwingFeedback(isHeavy: isFinisher, comboStep: _comboStep);
            int hits = PerformMeleeHitbox(dmg, isFinisher ? cfg.meleeRange * 1.12f : cfg.meleeRange, radius, isHeavy: isFinisher);
            TotalLightHitsLanded += hits;
            return true;
        }

        public bool ExecuteHeavyStrike()
        {
            if (Controller == null || Time.time < _meleeCooldownUntil) return false;
            var cfg = Controller.Cfg;
            _comboStep = 0;
            _meleeCooldownUntil = Time.time + cfg.meleeCooldownSec * 1.45f;
            _ecoCancelUntil = Time.time + cfg.ecoCancelWindowSec;

            Controller.PlayToolSwingFeedback(isHeavy: true, comboStep: 0);
            int hits = PerformMeleeHitbox(cfg.heavyChargedDamage, cfg.meleeRange * 1.15f, cfg.heavyRadius, isHeavy: true);
            TotalHeavyHitsLanded += hits;
            return true;
        }

        public bool TryFireEnergyBolt()
        {
            if (Controller == null || Time.time < _boltCooldownUntil) return false;
            var cfg = Controller.Cfg;

            if (!Controller.TryConsumeBoltCost())
                return false;

            _boltCooldownUntil = Time.time + cfg.boltCooldownSec;
            TotalBoltsFired++;

            Vector3 spawnPos = Controller.GetMuzzleWorldPosition();
            Vector3 dir = Controller.GetAimForward();

            var boltGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltGo.name = "Tool_EnergyBolt";
            boltGo.transform.position = spawnPos;
            boltGo.transform.localScale = Vector3.one * 0.24f;
            var col = boltGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            var mr = boltGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = new Color(0.32f, 0.94f, 1.0f, 1f);
                mr.sharedMaterial = mat;
            }

            var proj = boltGo.AddComponent<EnergyBoltProjectile>();
            proj.Launch(Controller, dir, cfg.boltSpeed, cfg.boltDamage, cfg.boltLifetimeSec);
            Controller.PlayBoltFireFeedback(spawnPos);
            return true;
        }

        private int PerformMeleeHitbox(float damage, float range, float radius, bool isHeavy)
        {
            Transform owner = Controller.PlayerTransform;
            Vector3 center = owner.position + Vector3.up * 0.78f + owner.forward * (range * 0.65f);
            var cols = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            var hitSet = new HashSet<IDamageable>();

            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].transform.root == owner.root) continue;
                var dmgTarget = cols[i].GetComponentInParent<IDamageable>();
                if (dmgTarget == null || !dmgTarget.IsAlive || hitSet.Contains(dmgTarget)) continue;

                hitSet.Add(dmgTarget);
                dmgTarget.ApplyDamage(new ToolHitInfo
                {
                    Damage = damage,
                    HitPoint = cols[i].ClosestPoint(center),
                    KnockbackDir = owner.forward,
                    IsHeavy = isHeavy,
                    IsRangedBolt = false,
                    Instigator = owner.gameObject
                });
            }

            if (hitSet.Count > 0)
                Controller.TriggerHitImpactFeedback(center, isHeavy);
            return hitSet.Count;
        }
    }

    public sealed class EnergyBoltProjectile : MonoBehaviour
    {
        private MultiToolController _owner;
        private Vector3 _dir;
        private float _speed;
        private float _damage;
        private float _lifeLeft;
        private readonly RaycastHit[] _hitBuf = new RaycastHit[12];

        public void Launch(MultiToolController owner, Vector3 dir, float speed, float damage, float lifetime)
        {
            _owner = owner;
            _dir = dir.normalized;
            _speed = speed;
            _damage = damage;
            _lifeLeft = lifetime;
            gameObject.layer = Studio.Game.Collision.CollisionMatrixBootstrap.LayerProjectile;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _lifeLeft -= dt;
            if (_lifeLeft <= 0f) { Destroy(gameObject); return; }

            float step = _speed * dt;
            int count = Physics.SphereCastNonAlloc(transform.position, 0.14f, _dir, _hitBuf, step + 0.05f, Studio.Game.Collision.CollisionMatrixBootstrap.ProjectileHitMask, QueryTriggerInteraction.Collide);
            if (count > 0)
            {
                // Ordenacao simples in-place (zero alocacao!) por distancia crescente
                for (int a = 0; a < count - 1; a++)
                {
                    for (int b = a + 1; b < count; b++)
                    {
                        if (_hitBuf[b].distance < _hitBuf[a].distance)
                        {
                            var tmp = _hitBuf[a]; _hitBuf[a] = _hitBuf[b]; _hitBuf[b] = tmp;
                        }
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    var hit = _hitBuf[i];
                    if (hit.collider == null) continue;
                    if (_owner != null && hit.transform.root == _owner.PlayerTransform.root) continue;

                    var dmg = hit.transform.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsAlive)
                    {
                        dmg.ApplyDamage(new ToolHitInfo
                        {
                            Damage = _damage,
                            HitPoint = hit.point,
                            KnockbackDir = _dir,
                            IsHeavy = false,
                            IsRangedBolt = true,
                            Instigator = _owner != null ? _owner.PlayerTransform.gameObject : null
                        });
                        if (_owner != null) _owner.TriggerHitImpactFeedback(hit.point, isHeavy: false);
                        Destroy(gameObject);
                        return;
                    }

                    if (!hit.collider.isTrigger)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            transform.position += _dir * step;
        }
    }
}
