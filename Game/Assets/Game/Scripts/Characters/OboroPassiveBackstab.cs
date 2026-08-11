using UnityEngine;

/// <summary>
/// 朧P。敵ヒーローの背後120度以内から行う通常攻撃へ、20 + AD×40%の通常ダメージを加算する。
/// 内部クールダウンはなく、Minion / Tower / TrainingDummyには発動しない。
/// 実ダメージ・ルーン命中回数・戦闘テキストを二重化しないよう、PlayerBasicAttackControllerと
/// OboroEControllerが通常攻撃の元ダメージへ加算してからHealthControllerへ1回だけ渡す。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class OboroPassiveBackstab : MonoBehaviour
{
    [Header("Passive Damage")]
    [SerializeField, Min(0f)] private float _bonusBaseDamage = 20f;
    [SerializeField, Min(0f)] private float _bonusAdRatio = 0.4f;
    [SerializeField, Range(0f, 360f)] private float _rearArcAngle = 120f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _lastAttackWasBackstab;
    [SerializeField] private float _lastBonusDamage;

    private CharacterStats _stats;

    public float BonusBaseDamage => _bonusBaseDamage;
    public float BonusAdRatio => _bonusAdRatio;
    public float RearArcAngle => _rearArcAngle;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
    }

    /// <summary>対象への次の通常攻撃でPが発動する場合、加算前の追加ダメージ量を返す。</summary>
    public bool TryGetBonusDamage(Targetable target, out float bonusDamage)
    {
        bonusDamage = 0f;
        _lastAttackWasBackstab = false;
        _lastBonusDamage = 0f;

        // ユーザー指定どおり発動対象は敵ヒーロー(Character + 敵TeamMember)だけ。
        if (!OboroCombatUtility.IsEnemyHero(transform, target)) return false;

        Vector3 targetBackward = OboroCombatUtility.Flatten(-target.transform.forward);
        Vector3 targetToAttacker = OboroCombatUtility.Flatten(transform.position - target.transform.position);
        if (targetBackward.sqrMagnitude <= 0.0001f || targetToAttacker.sqrMagnitude <= 0.0001f) return false;

        float angle = Vector3.Angle(targetBackward.normalized, targetToAttacker.normalized);
        if (angle > _rearArcAngle * 0.5f) return false;

        float attackDamage = _stats != null ? _stats.CurrentAttackDamage : 0f;
        bonusDamage = Mathf.Max(0f, _bonusBaseDamage + attackDamage * _bonusAdRatio);
        _lastAttackWasBackstab = bonusDamage > 0f;
        _lastBonusDamage = bonusDamage;
        return _lastAttackWasBackstab;
    }

    public void NotifyTriggered(Targetable target, float rawBonusDamage)
    {
        if (target == null || rawBonusDamage <= 0f) return;
        Debug.Log($"朧 P: {target.name}への背後通常攻撃に追加ダメージを加算しました(軽減前{rawBonusDamage:F1})。", this);
    }
}
