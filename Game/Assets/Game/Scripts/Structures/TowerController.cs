using UnityEngine;
using Core;

namespace Structures
{
    /// <summary>
    /// Phase 5 tower. Attacks the nearest enemy Targetable in range, prioritising heroes.
    /// Uses Core.TeamMember to distinguish friend from foe.
    /// Applies AR reduction + minion-absence reduction via IIncomingDamageModifier.
    /// </summary>
    [RequireComponent(typeof(Combat.HealthController))]
    [RequireComponent(typeof(Characters.Targetable))]
    public class TowerController : MonoBehaviour, Combat.IIncomingDamageModifier
    {
        [Header("Stats")]
        [SerializeField] private float _maxHp        = 5000f;
        [SerializeField] private float _armor        = 60f;
        [SerializeField] private float _attackDamage = 130f;
        [SerializeField] private float _attackSpeed  = 0.80f;   // attacks per second
        [SerializeField] private float _attackRange  = 8.0f;

        [Header("Consecutive Attack Bonus")]
        [SerializeField] private float _consecutiveBonusPerHit   = 0.25f;
        [SerializeField] private float _consecutiveMaxMultiplier = 3.0f;
        [SerializeField] private float _consecutiveResetDelay    = 2.0f;

        [Header("Minion Absence Reduction")]
        [SerializeField] private float _minionCheckRadius       = 9.0f;
        [Tooltip("Normal-damage multiplier when no friendly minion is nearby. 0.1 = 90% reduction.")]
        [SerializeField] private float _minionAbsenceMultiplier = 0.1f;

        [Header("Team")]
        [SerializeField] private Team _team;

        // ---- Runtime ----
        private Combat.HealthController _health;
        private Characters.Targetable   _targetable;
        private float                   _attackCooldown;
        private Transform               _currentTarget;
        private float                   _consecutiveMultiplier = 1f;
        private float                   _consecutiveResetTimer;
        private bool                    _hasMinionNearby;
        private bool                    _destroyed;

        public Team Team        => _team;
        public bool IsDestroyed => _destroyed;

        // Called by MapBuilder or scene setup to override team at runtime
        public void Initialize(Team team) { _team = team; }

        // ---- Unity ----
        private void Awake()
        {
            _health     = GetComponent<Combat.HealthController>();
            _targetable = GetComponent<Characters.Targetable>();

            _health.SetMaxHealth(_maxHp);
            _targetable.InitializeRuntime(
                Characters.TargetClassification.Tower,
                null, null,
                GetComponentInChildren<Renderer>());

            _health.OnDeath += HandleDeath;
        }

        private void Update()
        {
            if (_destroyed) return;

            _attackCooldown        -= Time.deltaTime;
            _consecutiveResetTimer -= Time.deltaTime;

            CheckMinionPresence();
            AcquireTarget();

            if (_currentTarget != null && _attackCooldown <= 0f)
                FireAtTarget();
        }

        // ---- IIncomingDamageModifier ----
        public float ModifyIncomingDamage(Combat.DamageContext ctx, float damage)
        {
            // True damage: always blocked (tower is immune to true damage)
            if (ctx.DamageType == Combat.DamageType.True) return 0f;

            // Normal damage: AR reduction first
            float reduced = damage * 100f / (100f + _armor);

            // Then minion-absence multiplier if no friendly minion nearby
            if (!_hasMinionNearby)
                reduced *= _minionAbsenceMultiplier;

            return reduced;
        }

        // ---- Helpers ----

        /// <summary>Checks whether at least one friendly minion is within check radius.</summary>
        private void CheckMinionPresence()
        {
            _hasMinionNearby = false;
            foreach (var m in FindObjectsByType<Minion.MinionController>(FindObjectsSortMode.None))
            {
                if (m.Team == _team &&
                    Vector3.Distance(transform.position, m.transform.position) <= _minionCheckRadius)
                {
                    _hasMinionNearby = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Finds the best enemy target within range.
        /// Hero (Character/TrainingDummy) is preferred over Minion.
        /// Uses TeamMember component for friend/foe determination.
        /// </summary>
        private void AcquireTarget()
        {
            Characters.Targetable best     = null;
            float                 bestDist = float.MaxValue;
            bool                  bestIsHero = false;

            foreach (var t in FindObjectsByType<Characters.Targetable>(FindObjectsSortMode.None))
            {
                if (t == _targetable) continue;
                if (!t.IsTargetable)  continue;

                // --- Team check: skip friendly targets ---
                // Check TeamMember on the same root first, then the Targetable's own GameObject.
                var tm = t.GetComponent<TeamMember>() ?? t.GetComponentInParent<TeamMember>();
                if (tm != null && tm.Team == _team) continue;   // same team → skip

                // Distance check
                float dist = Vector3.Distance(transform.position, t.transform.position);
                if (dist > _attackRange) continue;

                bool isHero = t.TargetClassification == Characters.TargetClassification.Character
                           || t.TargetClassification == Characters.TargetClassification.TrainingDummy;

                if (best == null
                    || (isHero && !bestIsHero)
                    || (isHero == bestIsHero && dist < bestDist))
                {
                    best = t;
                    bestDist   = dist;
                    bestIsHero = isHero;
                }
            }

            var newTarget = best != null ? best.transform : null;
            if (newTarget != _currentTarget)
            {
                // Reset consecutive bonus on target switch
                _consecutiveMultiplier = 1f;
                _consecutiveResetTimer = 0f;
            }
            _currentTarget = newTarget;
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null) return;

            if (_consecutiveResetTimer <= 0f && _consecutiveMultiplier > 1f)
                _consecutiveMultiplier = 1f;

            var hc = _currentTarget.GetComponent<Combat.HealthController>();
            if (hc != null)
                hc.TakeDamage(_attackDamage * _consecutiveMultiplier,
                              transform, Combat.DamageType.Normal);

            _consecutiveMultiplier = Mathf.Min(
                _consecutiveMultiplier + _consecutiveBonusPerHit,
                _consecutiveMaxMultiplier);
            _consecutiveResetTimer = _consecutiveResetDelay;
            _attackCooldown = 1f / _attackSpeed;

            Debug.Log(string.Format("[Tower-{0}] HP {1:F0}/{2:F0}  target={3}  minionNearby={4}",
                _team, _health.CurrentHealth, _maxHp,
                _currentTarget != null ? _currentTarget.name : "none",
                _hasMinionNearby));
        }

        private void HandleDeath()
        {
            _destroyed = true;
            Debug.Log("[Tower] Destroyed \u2013 " + _team);

            // Notify the enemy nexus that its guard tower is gone
            var mb = FindFirstObjectByType<Map.MapBuilder>();
            if (mb != null)
            {
                Team enemyTeam = _team == Team.Blue ? Team.Red : Team.Blue;
                var nexus = mb.GetNexus(enemyTeam);
                if (nexus != null) nexus.OnGuardTowerDestroyed();
            }
        }
    }
}
