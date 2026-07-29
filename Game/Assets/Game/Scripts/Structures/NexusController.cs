using UnityEngine;
using Core;

namespace Structures
{
    /// <summary>
    /// Phase 5 Nexus (本拠地). HP 6000 / AR 50.
    /// Accepts damage only AFTER its guard tower has been destroyed.
    /// Calls GameManager.OnNexusDestroyed when HP reaches 0.
    /// </summary>
    [RequireComponent(typeof(Combat.HealthController))]
    [RequireComponent(typeof(Characters.Targetable))]
    public class NexusController : MonoBehaviour, Combat.IIncomingDamageModifier
    {
        [Header("Stats")]
        [SerializeField] private float _maxHp = 6000f;
        [SerializeField] private float _armor = 50f;

        [Header("Team")]
        [SerializeField] private Team _team;

        [Header("Visuals")]
        [SerializeField] private Renderer _crystalRenderer;
        [SerializeField] private Color    _colorVulnerable = new Color(1f, 0.7f, 0f);   // orange when exposed
        [SerializeField] private Color    _colorBroken     = new Color(0.35f, 0.35f, 0.35f);

        private Combat.HealthController _health;
        private Characters.Targetable   _targetable;
        private bool                    _guardTowerDestroyed;
        private bool                    _nexusDestroyed;

        public Team Team          => _team;
        public bool IsVulnerable  => _guardTowerDestroyed;
        public bool IsDestroyed   => _nexusDestroyed;

        // ---- Called by TowerController when the guard tower dies ----
        public void OnGuardTowerDestroyed()
        {
            if (_guardTowerDestroyed) return;
            _guardTowerDestroyed = true;
            Debug.Log("[Nexus] Guard tower destroyed \u2013 " + _team + " nexus is now VULNERABLE.");
            ApplyCrystalColor(_colorVulnerable);
            // Re-enable the Targetable so players can select and attack this nexus
            _targetable.enabled = true;
            if (_targetable.TryGetComponent<Collider>(out var col))
                col.enabled = true;
        }

        // ---- Unity ----
        private void Awake()
        {
            _health     = GetComponent<Combat.HealthController>();
            _targetable = GetComponent<Characters.Targetable>();

            _health.SetMaxHealth(_maxHp);
            _targetable.InitializeRuntime(
                Characters.TargetClassification.Tower,
                null, null,
                _crystalRenderer != null ? _crystalRenderer : GetComponentInChildren<Renderer>());

            _health.OnDeath += HandleNexusDeath;

            // Nexus starts invulnerable: disable its Targetable so attacks cannot land
            _guardTowerDestroyed = false;
            _targetable.enabled = false;
            if (_targetable.TryGetComponent<Collider>(out var col))
                col.enabled = false;
        }

        // ---- IIncomingDamageModifier ----
        public float ModifyIncomingDamage(Combat.DamageContext ctx, float damage)
        {
            if (!_guardTowerDestroyed) return 0f;   // tower still standing – immune
            if (ctx.DamageType == Combat.DamageType.True) return damage;
            return damage * 100f / (100f + _armor); // AR reduction
        }

        // ---- Private ----
        private void HandleNexusDeath()
        {
            if (_nexusDestroyed) return;
            _nexusDestroyed = true;
            ApplyCrystalColor(_colorBroken);
            Debug.Log("[Nexus] DESTROYED \u2013 " + _team + " loses!");

            Team winner = _team == Team.Blue ? Team.Red : Team.Blue;
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.OnNexusDestroyed(winner);
            else
                Debug.Log(string.Format("[Nexus] Match over. Winner={0}  Loser={1}", winner, _team));
        }

        private void ApplyCrystalColor(Color color)
        {
            if (_crystalRenderer != null)
                _crystalRenderer.material.color = color;
        }
    }
}
