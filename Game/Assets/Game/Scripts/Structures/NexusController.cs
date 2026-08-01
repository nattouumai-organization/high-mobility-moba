using UnityEngine;

/// <summary>
/// 本拠地(GAME_DESIGN.md 3章)。MapBuilderが実行時に生成し、Initializeで所属チームを設定する。
/// - 自チームの1本目のタワーが破壊されるまで完全無敵(すべてのダメージを0にする)。
/// - 受けるダメージはタワーと同じく通常攻撃のみ有効。同一チームからのダメージは0。
/// - 通常ダメージはAR50で軽減(CharacterStatsを持たないため自前で適用)。
/// - 破壊されるとGameManagerへ通知し、相手チームの勝利となる。
/// </summary>
public class NexusController : MonoBehaviour, IIncomingDamageModifier
{
    private const float Armor = 50f;

    private Team _team = Team.Blue;
    private HealthController _health;
    private bool _isDestroyed;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>自チームの1本目のタワーが破壊されるまでは無敵。</summary>
    public bool IsInvulnerable => !TowerController.IsTowerDestroyed(_team);

    /// <summary>生成直後の初期化(MapBuilderから呼び出す)。</summary>
    public void Initialize(Team team)
    {
        _team = team;
        _health = GetComponent<HealthController>();
        if (_health != null)
        {
            // HealthController.AwakeのキャッシュはこのAddComponentより先に実行済みのため再取得させる。
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

    /// <summary>受けるダメージの変更(IIncomingDamageModifier)。クラスコメントのルールを適用する。</summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        // 自チームのタワー破壊前は完全無敵。
        if (IsInvulnerable)
        {
            return 0f;
        }

        // 同一チームからのダメージは受けない。
        if (context.Attacker != null)
        {
            TeamMember attackerTeam = context.Attacker.GetComponent<TeamMember>();
            if (attackerTeam != null && attackerTeam.Team == _team)
            {
                return 0f;
            }
        }

        // 本拠地も通常攻撃でのみダメージを受ける(スキル・反射は無効)。
        if (!context.IsBasicAttack)
        {
            return 0f;
        }

        // ARによる通常ダメージの軽減。
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyNexusDestroyed(_team);
        }
    }
}
