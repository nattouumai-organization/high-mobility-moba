using UnityEngine;

/// <summary>
/// 選択中のターゲットに対する通常攻撃を管理する。
/// TASKS.md「通常攻撃の射程判定を実装する」「攻撃速度と攻撃間隔を実装する」
/// 「ダメージと死亡処理を実装する」用の試作スクリプト。
/// 選択中のTargetableが攻撃射程内にいる場合のみ、攻撃間隔ごとに通常攻撃を実行し、
/// CharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与えて被弾フラッシュを発生させる。
/// 射程外のターゲットを選択した場合は、射程内に入るまでPlayerClickMovementで自動接近する。
/// ターゲットが死亡した場合は攻撃を停止する。
/// 攻撃アニメーション・弾丸・投射物・ヒットスキャンは今回実装しない。
/// 将来的にミニオンなども扱うBasicAttackControllerへ発展させる想定。
/// </summary>
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerTargetSelector))]
[RequireComponent(typeof(PlayerClickMovement))]
public class PlayerBasicAttackController : MonoBehaviour
{
    // 攻撃速度・攻撃射程の取得元。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private CharacterStats _characterStats;

    // 現在のターゲットの取得元。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private PlayerTargetSelector _targetSelector;

    // 射程外のターゲットへの自動接近に使用する移動処理。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private PlayerClickMovement _clickMovement;

    // 次に通常攻撃できる時刻(Time.time基準)。攻撃速度以上の頻度で攻撃しないための管理値。
    private float _nextAttackTime;

    // 射程外のターゲットへ自動接近中かどうか。射程内へ入った瞬間に停止するための管理値。
    private bool _isApproaching;

    /// <summary>現在のターゲットが攻撃射程内かどうか。ターゲット未選択・無効時はfalse。</summary>
    public bool IsCurrentTargetInRange { get; private set; }

    private void Awake()
    {
        if (_characterStats == null)
        {
            _characterStats = GetComponent<CharacterStats>();
        }

        if (_targetSelector == null)
        {
            _targetSelector = GetComponent<PlayerTargetSelector>();
        }

        if (_clickMovement == null)
        {
            _clickMovement = GetComponent<PlayerClickMovement>();
        }
    }

    private void Update()
    {
        Targetable target = GetValidTarget();
        IsCurrentTargetInRange = target != null && IsInAttackRange(target);

        if (target == null)
        {
            // ターゲットがいない・無効・死亡した場合は攻撃を停止する。
            // 自動接近の途中だった場合は、その場で移動も停止する。
            if (_isApproaching)
            {
                _isApproaching = false;

                if (_clickMovement != null)
                {
                    _clickMovement.StopMovement();
                }
            }

            return;
        }

        // 射程内外を選択リングの色へ反映する(射程内: 明るい緑 / 射程外: オレンジ)。
        target.SetInAttackRange(IsCurrentTargetInRange);

        if (!IsCurrentTargetInRange)
        {
            // 射程外の場合は攻撃せず、射程内に入るまでターゲットへ自動接近する。
            ApproachTarget(target);
            return;
        }

        // 自動接近中に射程内へ入ったら、その場で停止して攻撃を開始する。
        if (_isApproaching)
        {
            _isApproaching = false;

            if (_clickMovement != null)
            {
                _clickMovement.StopMovement();
            }
        }

        TryAttack(target);
    }

    /// <summary>
    /// 指定したTargetableが現在の攻撃射程内かどうかを判定する。
    /// TargetableのColliderのPlayerに最も近い点を使い、Y軸を除いた水平距離(XZ平面)だけで判定する。
    /// </summary>
    public bool IsInAttackRange(Targetable target)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 closestPoint = target.GetClosestPoint(transform.position);
        Vector3 toTarget = closestPoint - transform.position;
        toTarget.y = 0f;

        return toTarget.magnitude <= _characterStats.CurrentAttackRange;
    }

    private Targetable GetValidTarget()
    {
        if (_targetSelector == null)
        {
            return null;
        }

        Targetable target = _targetSelector.CurrentTarget;

        // 破棄・無効化・死亡した対象へは攻撃しない(選択の解除自体はPlayerTargetSelectorが行う)。
        if (target == null || !target.isActiveAndEnabled || target.IsDead)
        {
            return null;
        }

        return target;
    }

    /// <summary>
    /// 射程外のターゲットへ向けた移動を指示する。
    /// 射程判定と同じくColliderの最も近い点を目標にし、毎フレーム更新することで
    /// 将来ターゲットが移動する場合にも追従できるようにする。
    /// </summary>
    private void ApproachTarget(Targetable target)
    {
        if (_clickMovement == null)
        {
            return;
        }

        _isApproaching = true;
        _clickMovement.MoveToPosition(target.GetClosestPoint(transform.position));
    }

    private void TryAttack(Targetable target)
    {
        if (Time.time < _nextAttackTime)
        {
            return;
        }

        // 通常攻撃: CharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与える。
        // 弾丸・投射物は使わず、ダメージは即時に届く。
        HealthController targetHealth = target.Health;
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(_characterStats.CurrentAttackDamage);
        }

        // 既存の被弾フラッシュを発生させる。
        target.PlayHitFlash();

        // 攻撃間隔はCharacterStatsから毎回取得するため、Inspectorでの攻撃速度変更が次の攻撃から反映される。
        _nextAttackTime = Time.time + _characterStats.AttackInterval;
    }
}
