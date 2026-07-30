using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1本目のタワー(GAME_DESIGN.md 4章)。MapBuilderが実行時に生成し、Initializeで所属チームを設定する。
/// - HP5,000 / AR60 / 射程8(設計800) / AD130 / AS0.80。
/// - 射程内の敵を自動攻撃する。敵ミニオンを優先し、いなければ敵ヒーローを攻撃する(構造物は攻撃しない)。
/// - 同じ敵ヒーローを連続攻撃するたびダメージが25%増加(最大+200%)。2秒間ヒーローを攻撃しなければリセット。
/// - 攻撃側チームのミニオンが近くにいない場合、通常ダメージを90%軽減し確定ダメージを無効化。
/// - AR(60)はCharacterStatsを持たないためIIncomingDamageModifierとして自前で適用する。
/// - 破壊されると同チームの本拠地が攻撃可能になる(NexusControllerがIsTowerDestroyedを参照)。
/// </summary>
public class TowerController : MonoBehaviour, IIncomingDamageModifier
{
    private static readonly List<TowerController> Towers = new List<TowerController>();

    [Header("戦闘(GAME_DESIGN.md 4章)")]
    [SerializeField] private Team _team = Team.Blue;
    [SerializeField, Min(0.5f)] private float _attackRange = 8f;
    [SerializeField, Min(1f)] private float _attackDamage = 130f;
    [SerializeField, Min(0.05f)] private float _attacksPerSecond = 0.8f;
    [SerializeField, Min(0f)] private float _armor = 60f;

    [Header("ミニオン不在時の保護")]
    [SerializeField, Min(0.5f)] private float _minionProtectRange = 8f;
    [SerializeField, Range(0f, 1f)] private float _noMinionDamageCut = 0.9f;

    [Header("連続攻撃ボーナス")]
    [SerializeField, Min(0f)] private float _consecutiveBonusPerHit = 0.25f;
    [SerializeField, Min(0f)] private float _consecutiveBonusMax = 2f;
    [SerializeField, Min(0.1f)] private float _consecutiveResetSeconds = 2f;

    private HealthController _health;
    private float _attackCooldown;
    private Transform _lastHeroTarget;
    private int _consecutiveHits;
    private float _lastHeroAttackTime = float.NegativeInfinity;
    private bool _isDestroyed;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>破壊済みかどうか。</summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>指定チームのタワーが1本も残っていないかどうか。本拠地の無敵解除判定に使用する。</summary>
    public static bool IsTowerDestroyed(Team team)
    {
        foreach (TowerController tower in Towers)
        {
            if (tower == null || tower.Team != team)
            {
                continue;
            }

            if (!tower.IsDestroyed)
            {
                return false;
            }
        }

        return true;
    }

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

    private void OnEnable()
    {
        Towers.Add(this);
    }

    private void OnDisable()
    {
        Towers.Remove(this);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (_isDestroyed || _health == null || _health.IsDead)
        {
            return;
        }

        if (_consecutiveHits > 0 && Time.time - _lastHeroAttackTime > _consecutiveResetSeconds)
        {
            _consecutiveHits = 0;
            _lastHeroTarget = null;
        }

        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f)
        {
            return;
        }

        HealthController target = AcquireTarget(out bool isHero);
        if (target == null)
        {
            _attackCooldown = 0.25f;
            return;
        }

        Attack(target, isHero);
        _attackCooldown = 1f / Mathf.Max(0.05f, _attacksPerSecond);
    }

    // 射程内の敵を探す。敵ミニオンを優先し、いなければ敵ヒーロー(構造物以外)を返す。
    private HealthController AcquireTarget(out bool isHero)
    {
        HealthController bestMinion = null;
        HealthController bestHero = null;
        float bestMinionDistance = float.MaxValue;
        float bestHeroDistance = float.MaxValue;

        foreach (TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (member.Team == _team)
            {
                continue;
            }

            if (member.GetComponent<TowerController>() != null || member.GetComponent<NexusController>() != null)
            {
                continue;
            }

            HealthController health = member.GetComponent<HealthController>();
            if (health == null || health.IsDead)
            {
                continue;
            }

            Vector3 closest = GetClosestPoint(member, transform.position);
            float distance = Vector3.Distance(transform.position, closest);
            if (distance > _attackRange)
            {
                continue;
            }

            if (member.GetComponent<MinionController>() != null)
            {
                if (distance < bestMinionDistance)
                {
                    bestMinionDistance = distance;
                    bestMinion = health;
                }
            }
            else if (distance < bestHeroDistance)
            {
                bestHeroDistance = distance;
                bestHero = health;
            }
        }

        isHero = bestMinion == null && bestHero != null;
        return bestMinion != null ? bestMinion : bestHero;
    }

    private void Attack(HealthController target, bool isHero)
    {
        float damage = _attackDamage;

        if (isHero)
        {
            _consecutiveHits = _lastHeroTarget == target.transform ? _consecutiveHits + 1 : 0;
            float bonus = Mathf.Min(_consecutiveHits * _consecutiveBonusPerHit, _consecutiveBonusMax);
            damage *= 1f + bonus;
            _lastHeroTarget = target.transform;
            _lastHeroAttackTime = Time.time;
        }

        float dealt = target.TakeDamage(damage, transform, DamageType.Normal);
        if (dealt > 0f)
        {
            Targetable targetable = target.GetComponent<Targetable>();
            if (targetable != null)
            {
                targetable.PlayHitFlash();
            }
        }
    }

    /// <summary>
    /// 受けるダメージの軽減(IIncomingDamageModifier)。
    /// 攻撃側チームのミニオンが近くにいない場合: 通常ダメージ90%軽減・確定ダメージ無効。
    /// その後、通常ダメージへAR(60)による軽減を適用する。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (!IsAttackerMinionNearby(context.Attacker))
        {
            if (context.Type == DamageType.True)
            {
                return 0f;
            }

            currentAmount *= 1f - _noMinionDamageCut;
        }

        if (context.Type == DamageType.Normal && _armor > 0f)
        {
            currentAmount = currentAmount * 100f / (100f + _armor);
        }

        return currentAmount;
    }

    // 攻撃側チームのミニオンがタワー近くにいるかどうか。攻撃者が不明な場合は相手チームとして扱う。
    private bool IsAttackerMinionNearby(Transform attacker)
    {
        Team attackerTeam = _team.Opponent();
        if (attacker != null)
        {
            TeamMember member = attacker.GetComponentInParent<TeamMember>();
            if (member != null)
            {
                attackerTeam = member.Team;
            }
        }

        if (attackerTeam == _team)
        {
            return true;
        }

        float sqrRange = _minionProtectRange * _minionProtectRange;
        foreach (MinionController minion in MinionController.ActiveMinions)
        {
            if (minion == null || minion.IsDead || minion.Team != attackerTeam)
            {
                continue;
            }

            if ((minion.transform.position - transform.position).sqrMagnitude <= sqrRange)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 GetClosestPoint(Component target, Vector3 from)
    {
        Collider collider = target.GetComponent<Collider>();
        return collider != null && collider.enabled ? collider.ClosestPoint(from) : target.transform.position;
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
            GameManager.Instance.NotifyTowerDestroyed(_team);
        }
        else
        {
            Debug.Log($"TowerController: {_team}チームの1本目のタワーが破壊されました。", this);
        }
    }
}
