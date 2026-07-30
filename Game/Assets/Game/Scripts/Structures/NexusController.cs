using UnityEngine;

/// <summary>
/// 本拠地(GAME_DESIGN.md 4章)。MapBuilderが実行時に生成し、Initializeで所属チームを設定する。
/// - HP6,000 / AR50 / HPregなし。
/// - 同チームの1本目のタワーが破壊されるまで全ダメージ無効(IIncomingDamageModifierで0にする)。
/// - AR(50)はCharacterStatsを持たないためIIncomingDamageModifierとして自前で適用する。
/// - 破壊されるとGameManagerへ通知し、相手チームの勝利となる。
/// </summary>
public class NexusController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("戦闘(GAME_DESIGN.md 4章)")]
    [SerializeField] private Team _team = Team.Blue;
    [SerializeField, Min(0f)] private float _armor = 50f;

    private HealthController _health;
    private bool _isDestroyed;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>同チームのタワーが残っている間は無敵。</summary>
    public bool IsInvulnerable => !TowerController.IsTowerDestroyed(_team);

    /// <summary>MapBuilderが生成直後に呼び出す初期化。</summary>
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

    /// <summary>
    /// 受けるダメージの軽減(IIncomingDamageModifier)。
    /// タワーが破壊されるまで本拠地は攻撃できない(全ダメージ0)。その後はAR(50)で通常ダメージを軽減する。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (IsInvulnerable)
        {
            return 0f;
        }

        if (context.Type == DamageType.Normal && _armor > 0f)
        {
            currentAmount = currentAmount * 100f / (100f + _armor);
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
        else
        {
            Debug.Log($"NexusController: {_team}チームの本拠地が破壊されました({_team.Opponent()}チームの勝利)。", this);
        }
    }
}
