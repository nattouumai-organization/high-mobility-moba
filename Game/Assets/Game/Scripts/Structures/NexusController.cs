using UnityEngine;

/// <summary>
/// 旧仕様との互換用。本拠地はMapBuilderから生成されず、現在の正式な勝利条件にも使用しない。
/// 古いシーンやPrefabに残っていても、第2タワー勝利条件へ影響を与えない。
/// </summary>
public class NexusController : MonoBehaviour, IIncomingDamageModifier
{
    private const float Armor = 50f;

    private Team _team = Team.Blue;
    private HealthController _health;
    private bool _isDestroyed;

    public Team Team => _team;

    /// <summary>互換オブジェクトの被ダメージ条件。現在のマッチ進行では参照しない。</summary>
    public bool IsInvulnerable => !TowerController.IsTowerDestroyed(_team);

    public void Initialize(Team team)
    {
        _team = team;
        _health = GetComponent<HealthController>();
        if (_health != null)
        {
            _health.RefreshDamageModifiers();
            _health.Died += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
        }
    }

    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (IsInvulnerable)
        {
            return 0f;
        }

        if (context.Attacker != null)
        {
            TeamMember attackerTeam = context.Attacker.GetComponent<TeamMember>();
            if (attackerTeam != null && attackerTeam.Team == _team)
            {
                return 0f;
            }
        }

        if (!context.IsBasicAttack)
        {
            return 0f;
        }

        if (context.Type == DamageType.Normal)
        {
            currentAmount = currentAmount * 100f / (100f + Armor);
        }

        return currentAmount;
    }

    private void HandleDied()
    {
        if (_isDestroyed)
        {
            return;
        }

        _isDestroyed = true;
        Debug.LogWarning(
            $"NexusController: 旧互換用の{_team}本拠地が破壊されましたが、勝敗処理は行いません。正式な勝利条件は第2タワーの破壊です。",
            this);
    }
}
