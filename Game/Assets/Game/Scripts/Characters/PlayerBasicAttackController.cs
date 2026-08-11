using UnityEngine;

/// <summary>
/// 選択中Targetableへの通常攻撃と射程外自動接近を管理する共通コントローラー。
/// ゼルフPの回復に加え、朧ではPの背後追加ダメージを同じ1回の通常攻撃ダメージへ合算する。
/// これにより戦闘テキスト・ルーン命中回数・被弾イベントが二重化しない。
/// </summary>
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerTargetSelector))]
[RequireComponent(typeof(PlayerClickMovement))]
public class PlayerBasicAttackController : MonoBehaviour
{
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerTargetSelector _targetSelector;
    [SerializeField] private PlayerClickMovement _clickMovement;
    [SerializeField] private ZelfPassiveHeal _passiveHeal;

    private AbilityLockController _abilityLock;
    private ZelfQController _qController;
    private ZelfRController _rController;
    private OboroQController _oboroQController;
    private OboroEController _oboroEController;
    private OboroRController _oboroRController;
    private OboroPassiveBackstab _oboroPassive;
    private OboroWController _oboroWController;
    private float _nextAttackTime;
    private bool _isApproaching;
    private int _oboroAttackSequence;

    public bool IsCurrentTargetInRange { get; private set; }

    private void Awake()
    {
        if (_characterStats == null) _characterStats = GetComponent<CharacterStats>();
        if (_targetSelector == null) _targetSelector = GetComponent<PlayerTargetSelector>();
        if (_clickMovement == null) _clickMovement = GetComponent<PlayerClickMovement>();
        if (_passiveHeal == null) _passiveHeal = GetComponent<ZelfPassiveHeal>();

        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _qController = GetComponent<ZelfQController>();
        _rController = GetComponent<ZelfRController>();
        _oboroQController = GetComponent<OboroQController>();
        _oboroEController = GetComponent<OboroEController>();
        _oboroRController = GetComponent<OboroRController>();
        _oboroPassive = GetComponent<OboroPassiveBackstab>();
        _oboroWController = GetComponent<OboroWController>();
    }

    private void Update()
    {
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            IsCurrentTargetInRange = false;
            StopOwnApproach(true);
            return;
        }

        Targetable target = GetValidTarget();
        IsCurrentTargetInRange = target != null && IsInAttackRange(target);
        if (target == null)
        {
            StopOwnApproach(true);
            return;
        }

        target.SetInAttackRange(IsCurrentTargetInRange);
        if (!IsCurrentTargetInRange)
        {
            if (IsSkillApproachActive())
            {
                StopOwnApproach(false);
                return;
            }
            ApproachTarget(target);
            return;
        }

        StopOwnApproach(true);
        TryAttack(target);
    }

    private void StopOwnApproach(bool stopMovement)
    {
        if (!_isApproaching) return;
        _isApproaching = false;
        if (stopMovement && _clickMovement != null) _clickMovement.StopMovement();
    }

    private bool IsSkillApproachActive()
    {
        if (_qController != null && _qController.IsApproachingQTarget) return true;
        if (_rController != null && _rController.IsApproachingRTarget) return true;
        if (_oboroQController != null && _oboroQController.IsApproachingQTarget) return true;
        if (_oboroEController != null && _oboroEController.IsApproachingETarget) return true;
        if (_oboroRController != null && _oboroRController.IsApproachingRTarget) return true;
        return false;
    }

    public bool IsInAttackRange(Targetable target)
    {
        if (target == null) return false;
        Vector3 closestPoint = target.GetClosestPoint(transform.position);
        Vector3 toTarget = closestPoint - transform.position;
        toTarget.y = 0f;
        return toTarget.magnitude <= _characterStats.CurrentAttackRange;
    }

    private Targetable GetValidTarget()
    {
        if (_targetSelector == null) return null;
        Targetable target = _targetSelector.CurrentTarget;
        if (target == null || !target.isActiveAndEnabled || target.IsDead) return null;
        if (!OboroWController.CanBeTargetSelected(target, transform)) return null;

        TeamMember myTeam = GetComponent<TeamMember>();
        TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
        if (myTeam != null && targetTeam != null && myTeam.Team == targetTeam.Team) return null;
        return target;
    }

    private void ApproachTarget(Targetable target)
    {
        if (_clickMovement == null) return;
        _isApproaching = true;
        _clickMovement.MoveToPosition(target.GetClosestPoint(transform.position));
    }

    private void TryAttack(Targetable target)
    {
        if (Time.time < _nextAttackTime) return;

        HealthController targetHealth = target.Health;
        if (targetHealth != null)
        {
            // 攻撃が成立する時点で朧Wを解除する。対象が無敵でも「攻撃」入力自体で解除される。
            _oboroWController?.BreakStealth("通常攻撃");

            float rawDamage = _characterStats.CurrentAttackDamage;
            float passiveBonus = 0f;
            bool passiveTriggered = _oboroPassive != null &&
                                    _oboroPassive.TryGetBonusDamage(target, out passiveBonus);
            if (passiveTriggered) rawDamage += passiveBonus;

            string sourceId = null;
            if (_oboroPassive != null)
            {
                _oboroAttackSequence++;
                sourceId = $"OboroAA#{_oboroAttackSequence}";
            }

            float actualDamage = targetHealth.TakeDamage(rawDamage, transform, DamageType.Normal,
                isBasicAttack: true, sourceId: sourceId);
            if (actualDamage > 0f)
            {
                CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
                if (_passiveHeal != null) _passiveHeal.NotifyDamageDealt(actualDamage, target.Classification);
                if (passiveTriggered) _oboroPassive.NotifyTriggered(target, passiveBonus);
                target.PlayHitFlash();
            }
        }

        _nextAttackTime = Time.time + _characterStats.AttackInterval;
    }
}
