using UnityEngine;
using Core;

namespace Structures
{
    [RequireComponent(typeof(HealthController))]
    [RequireComponent(typeof(Targetable))]
    public class TowerController : MonoBehaviour, IIncomingDamageModifier
    {
        [Header("Stats")]
        [SerializeField] private float _maxHp        = 5000f;
        [SerializeField] private float _armor        = 60f;
        [SerializeField] private float _attackDamage = 130f;
        [SerializeField] private float _attackSpeed  = 0.80f;
        [SerializeField] private float _attackRange  = 8.0f;

        [Header("Consecutive Attack Bonus")]
        [SerializeField] private float _consecutiveBonusPerHit   = 0.25f;
        [SerializeField] private float _consecutiveMaxMultiplier = 3.0f;
        [SerializeField] private float _consecutiveResetDelay    = 2.0f;

        [Header("Minion Absence Damage Reduction")]
        [SerializeField] private float _minionCheckRadius = 9.0f;
        [Tooltip("Normal damage multiplier when no friendly minion is nearby. 0.1 = 90 % reduction.")]
        [SerializeField] private float _minionAbsenceMultiplier = 0.1f;

        [Header("Team")]
        [SerializeField] private Team _team;

        private HealthController _health;
        private Targetable       _targetable;
        private float            _attackCooldown;
        private Transform        _currentTarget;
        private float            _consecutiveMultiplier = 1f;
        private float            _consecutiveResetTimer;
        private bool             _hasMinionNearby;
        private bool             _destroyed;

        public Team Team        => _team;
        public bool IsDestroyed => _destroyed;

        public void Initialize(Team team) { _team = team; }

        // --------------------------------------------------------
        private void Awake()
        {
            _health     = GetComponent<HealthController>();
            _targetable = GetComponent<Targetable>();

            // SetMaxHealth は patch_cs.py で HealthController に追加されます。
            _health.SetMaxHealth(_maxHp);

            // InitializeRuntime は patch_cs.py で Targetable に追加されます。
            _targetable.InitializeRuntime(
                TargetClassification.Tower, null, null,
                GetComponentInChildren<Renderer>());

            // 正しいイベント名: Died
            _health.Died += HandleDeath;
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

        // IIncomingDamageModifier: 正しいシグネチャ
        public float ModifyIncomingDamage(DamageContext context, float currentAmount)
        {
            // 確定ダメージは 0 で完全無効
            if (context.Type == DamageType.True) return 0f;
            float reduced = currentAmount * 100f / (100f + _armor);
            if (!_hasMinionNearby) reduced *= _minionAbsenceMultiplier;
            return reduced;
        }

        // --------------------------------------------------------
        private void CheckMinionPresence()
        {
            _hasMinionNearby = false;
            foreach (var m in FindObjectsByType<Minion.MinionController>(FindObjectsSortMode.None))
            {
                if (m.Team == _team &&
                    Vector3.Distance(transform.position, m.transform.position) <= _minionCheckRadius)
                { _hasMinionNearby = true; return; }
            }
        }

        private void AcquireTarget()
        {
            Targetable best     = null;
            float      bestDist = float.MaxValue;
            bool       bestHero = false;

            foreach (var t in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
            {
                if (t == _targetable) continue;
                if (t.IsDead)         continue;

                // TeamMember でチーム判定
                var tm = t.GetComponent<TeamMember>() ?? t.GetComponentInParent<TeamMember>();
                if (tm != null && tm.Team == _team) continue;

                float dist = Vector3.Distance(transform.position, t.transform.position);
                if (dist > _attackRange) continue;

                // 正しいプロパティ名: Classification (TargetClassification は存在しない)
                bool isHero = t.Classification == TargetClassification.Character
                           || t.Classification == TargetClassification.TrainingDummy;

                if (best == null
                    || (isHero && !bestHero)
                    || (isHero == bestHero && dist < bestDist))
                { best = t; bestDist = dist; bestHero = isHero; }
            }

            var newTarget = best != null ? best.transform : null;
            if (newTarget != _currentTarget)
            { _consecutiveMultiplier = 1f; _consecutiveResetTimer = 0f; }
            _currentTarget = newTarget;
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null) return;
            if (_consecutiveResetTimer <= 0f && _consecutiveMultiplier > 1f)
                _consecutiveMultiplier = 1f;

            var hc = _currentTarget.GetComponent<HealthController>();
            if (hc != null)
                hc.TakeDamage(_attackDamage * _consecutiveMultiplier, transform, DamageType.Normal);

            _consecutiveMultiplier = Mathf.Min(
                _consecutiveMultiplier + _consecutiveBonusPerHit, _consecutiveMaxMultiplier);
            _consecutiveResetTimer = _consecutiveResetDelay;
            _attackCooldown = 1f / _attackSpeed;

            Debug.Log(string.Format("[Tower-{0}] HP {1:F0}/{2:F0} target={3} minion={4}",
                _team, _health.CurrentHealth, _maxHp,
                _currentTarget != null ? _currentTarget.name : "none", _hasMinionNearby));
        }

        private void HandleDeath()
        {
            _destroyed = true;
            Debug.Log("[Tower] Destroyed – " + _team);
            var mb = FindFirstObjectByType<Map.MapBuilder>();
            if (mb != null)
            {
                Team enemy = _team == Team.Blue ? Team.Red : Team.Blue;
                var nexus  = mb.GetNexus(enemy);
                if (nexus != null) nexus.OnGuardTowerDestroyed();
            }
        }
    }
}
