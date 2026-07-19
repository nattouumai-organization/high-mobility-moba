using UnityEngine;

/// <summary>
/// 選択中のターゲットに対する疑似通常攻撃を管理する。
/// TASKS.md「通常攻撃の射程判定を実装する」「攻撃速度と攻撃間隔を実装する」用の試作スクリプト。
/// 選択中のTargetableが攻撃射程内にいる場合のみ、攻撃間隔ごとに被弾フラッシュを呼び出す。
/// ダメージ・HP減少・死亡・攻撃アニメーション・弾丸・自動接近は今回実装しない。
/// 将来的にダメージ処理を持つBasicAttackControllerへ発展させる想定。
/// </summary>
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerTargetSelector))]
public class PlayerBasicAttackController : MonoBehaviour
{
    // 攻撃速度・攻撃射程の取得元。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private CharacterStats _characterStats;

    // 現在のターゲットの取得元。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private PlayerTargetSelector _targetSelector;

    // 次に疑似通常攻撃できる時刻(Time.time基準)。攻撃速度以上の頻度で攻撃しないための管理値。
    private float _nextAttackTime;

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
    }

    private void Update()
    {
        Targetable target = GetValidTarget();
        IsCurrentTargetInRange = target != null && IsInAttackRange(target);

        if (target != null)
        {
            // 射程内外を選択リングの色へ反映する(射程内: 明るい緑 / 射程外: オレンジ)。
            target.SetInAttackRange(IsCurrentTargetInRange);
        }

        if (!IsCurrentTargetInRange)
        {
            // ターゲットがいない・無効・射程外の場合は攻撃しない。
            // 射程外でも選択は保持し、自動接近もしない。
            return;
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

        // 破棄・無効化された対象へは攻撃しない(選択の解除自体はPlayerTargetSelectorが行う)。
        if (target == null || !target.isActiveAndEnabled)
        {
            return null;
        }

        return target;
    }

    private void TryAttack(Targetable target)
    {
        if (Time.time < _nextAttackTime)
        {
            return;
        }

        // 疑似通常攻撃: 被弾フラッシュのみ発生させる。ダメージ・HP減少はない。
        target.PlayHitFlash();

        // 攻撃間隔はCharacterStatsから毎回取得するため、Inspectorでの攻撃速度変更が次の攻撃から反映される。
        _nextAttackTime = Time.time + _characterStats.AttackInterval;
    }
}
