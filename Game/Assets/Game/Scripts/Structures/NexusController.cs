using UnityEngine;
using Core;

namespace Structures
{
    [RequireComponent(typeof(HealthController))]
    [RequireComponent(typeof(Targetable))]
    public class NexusController : MonoBehaviour, IIncomingDamageModifier
    {
        [Header("Stats")]
        [SerializeField] private float _maxHp = 6000f;
        [SerializeField] private float _armor = 50f;

        [Header("Team")]
        [SerializeField] private Team _team;

        [Header("Visuals")]
        [SerializeField] private Renderer _crystalRenderer;
        [SerializeField] private Color _colorVulnerable = new Color(1f, 0.7f, 0f);
        [SerializeField] private Color _colorBroken     = new Color(0.35f, 0.35f, 0.35f);

        private HealthController _health;
        private Targetable       _targetable;
        private Collider         _col;
        private bool             _guardTowerDestroyed;
        private bool             _nexusDestroyed;

        public Team Team         => _team;
        public bool IsVulnerable => _guardTowerDestroyed;
        public bool IsDestroyed  => _nexusDestroyed;

        // --------------------------------------------------------
        public void OnGuardTowerDestroyed()
        {
            if (_guardTowerDestroyed) return;
            _guardTowerDestroyed = true;
            Debug.Log("[Nexus] Guard tower destroyed – " + _team + " nexus is VULNERABLE.");
            ApplyCrystalColor(_colorVulnerable);
            _targetable.enabled = true;
            if (_col != null) _col.enabled = true;
        }

        // --------------------------------------------------------
        private void Awake()
        {
            _health     = GetComponent<HealthController>();
            _targetable = GetComponent<Targetable>();
            _col        = GetComponent<Collider>();

            // SetMaxHealth は patch_cs.py で HealthController に追加されます。
            _health.SetMaxHealth(_maxHp);

            // InitializeRuntime は patch_cs.py で Targetable に追加されます。
            _targetable.InitializeRuntime(
                TargetClassification.Tower, null, null,
                _crystalRenderer != null ? _crystalRenderer : GetComponentInChildren<Renderer>());

            // 正しいイベント名: Died
            _health.Died += HandleNexusDeath;

            // タワー生存中は非ターゲット・非コリジョン
            _guardTowerDestroyed = false;
            _targetable.enabled = false;
            if (_col != null) _col.enabled = false;
        }

        // IIncomingDamageModifier: 正しいシグネチャ
        public float ModifyIncomingDamage(DamageContext context, float currentAmount)
        {
            if (!_guardTowerDestroyed) return 0f;      // タワー健在中は全ダメージ無効
            if (context.Type == DamageType.True) return currentAmount;  // 確定ダメージは素通し
            return currentAmount * 100f / (100f + _armor);
        }

        // --------------------------------------------------------
        private void HandleNexusDeath()
        {
            if (_nexusDestroyed) return;
            _nexusDestroyed = true;
            ApplyCrystalColor(_colorBroken);
            Debug.Log("[Nexus] DESTROYED – " + _team + " loses!");
            Team winner = _team == Team.Blue ? Team.Red : Team.Blue;
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.OnNexusDestroyed(winner);
            else Debug.Log(string.Format("[Nexus] Match over. Winner={0}", winner));
        }

        private void ApplyCrystalColor(Color c)
        {
            if (_crystalRenderer != null) _crystalRenderer.material.color = c;
        }
    }
}
