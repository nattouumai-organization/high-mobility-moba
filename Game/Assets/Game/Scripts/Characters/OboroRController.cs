using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 朧R。最大HPの20%以下にいる敵ヒーロー/TrainingDummyを、固定射程200(2 Unity units)で対象指定して処刑する。
/// 処刑ダメージは発動時現在HPと同量の確定ダメージとしてHealthControllerへ渡すため、ARは無視する一方、
/// シールドやIIncomingDamageModifierによる軽減/無効化があれば生存できる。対象の共通D成功時は完全不発だがCDを消費する。
/// WorldHealthBarは本クラスの静的照会APIを使い、対象のHPバーへ処刑閾値マーカーを表示する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class OboroRController : MonoBehaviour
{
    private static readonly HashSet<OboroRController> ActiveControllers = new HashSet<OboroRController>();

    [Header("Execute")]
    [SerializeField, Range(0.01f, 1f)] private float _executeHealthRatio = 0.2f;
    [SerializeField, Min(0f)] private float _castRange = 2f;
    [SerializeField, Min(0f)] private float _cooldown = 100f;
    [SerializeField, Range(0f, 1f)] private float _deathCooldownReduction = 0.6f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _rangeColor = new Color(0.85f, 0.15f, 0.25f, 0.8f);

    [Header("Debug (Runtime)")]
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private bool _isApproachingRTarget;

    private double _cooldownEndTime;
    private Targetable _pendingTarget;
    private CharacterStats _stats;
    private CharacterController _characterController;
    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private CrowdControlController _crowdControl;
    private PlayerClickMovement _clickMovement;
    private PlayerMouseFacing _mouseFacing;
    private HealthController _selfHealth;
    private OboroWController _wController;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;
    private int _castSequence;

    public float RemainingCooldown => _remainingCooldown;
    public bool IsApproachingRTarget => _isApproachingRTarget;
    public float ExecuteHealthRatio => _executeHealthRatio;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _characterController = GetComponent<CharacterController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _crowdControl = GetComponent<CrowdControlController>();
        _clickMovement = GetComponent<PlayerClickMovement>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _selfHealth = GetComponent<HealthController>();
        _wController = GetComponent<OboroWController>();
        _mainCamera = Camera.main;
        _targetableLayer = OboroCombatUtility.ResolveTargetableLayer(_targetableLayer);
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Oboro R Range Indicator");
        if (_selfHealth != null) _selfHealth.Died += HandleDied;
    }

    private void OnEnable()
    {
        ActiveControllers.Add(this);
    }

    private void OnDisable()
    {
        ActiveControllers.Remove(this);
        CancelPendingApproach();
        if (_rangeIndicator != null) _rangeIndicator.HideAll();
    }

    private void OnDestroy()
    {
        ActiveControllers.Remove(this);
        if (_selfHealth != null) _selfHealth.Died -= HandleDied;
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
        if (_crowdControl == null) _crowdControl = GetComponent<CrowdControlController>();

        if (OboroCombatUtility.IsMatchEnded)
        {
            CancelPendingApproach();
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            return;
        }

        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            CancelPendingApproach();
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            return;
        }

        UpdateRangeIndicator();
        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.RPressedThisFrame, _inputHub.RReleasedThisFrame))
        {
            HandleCastInput();
        }
        UpdatePendingApproach();
    }

    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.RPressed &&
                       (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }
        _rangeIndicator.ShowCircle(_castRange, _rangeColor, 0.05f);
    }

    private void HandleCastInput()
    {
        CancelPendingApproach();
        if (_selfHealth != null && _selfHealth.IsDead) return;
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("朧 R: クールダウン中です。", this);
            return;
        }
        if (!TryGetValidTargetUnderMouse(out Targetable target))
        {
            Debug.Log("朧 R: マウスを有効な敵ヒーローへ合わせてください。", this);
            return;
        }
        if (!IsInExecuteRange(target))
        {
            Debug.Log($"朧 R: {target.name}は処刑圏外です。", this);
            return;
        }

        if (IsInCastRange(target))
        {
            Execute(target);
            return;
        }

        _pendingTarget = target;
        _isApproachingRTarget = true;
        _clickMovement?.StopMovement();
        Debug.Log("朧 R: 射程外のため自動接近を開始しました。", this);
    }

    private void UpdatePendingApproach()
    {
        if (_pendingTarget == null) return;
        if (_inputHub != null && _inputHub.RightClickPressedThisFrame)
        {
            CancelPendingApproach();
            Debug.Log("朧 R: 右クリック入力により自動接近を中止しました。", this);
            return;
        }
        if ((_selfHealth != null && _selfHealth.IsDead) ||
            (_crowdControl != null && _crowdControl.IsMovementBlocked) ||
            Time.timeAsDouble < _cooldownEndTime || !IsValidTarget(_pendingTarget) || !IsInExecuteRange(_pendingTarget))
        {
            CancelPendingApproach();
            return;
        }

        if (!IsInCastRange(_pendingTarget))
        {
            Vector3 direction = OboroCombatUtility.Flatten(_pendingTarget.GetClosestPoint(transform.position) - transform.position);
            if (direction.sqrMagnitude > 0.0001f && _characterController != null && _characterController.enabled)
            {
                _characterController.Move(direction.normalized * _stats.CurrentMoveSpeed * Time.deltaTime);
                _mouseFacing?.SetLookDirection(direction);
            }
            return;
        }

        Targetable target = _pendingTarget;
        CancelPendingApproach();
        Execute(target);
    }

    public void CancelPendingApproach()
    {
        _pendingTarget = null;
        _isApproachingRTarget = false;
    }

    private bool TryGetValidTargetUnderMouse(out Targetable target)
    {
        target = null;
        if (!OboroCombatUtility.TryGetMouseTarget(_inputHub, ref _mainCamera, _targetableLayer, out Targetable candidate))
        {
            return false;
        }
        if (!IsValidTarget(candidate)) return false;
        target = candidate;
        return true;
    }

    private bool IsValidTarget(Targetable target)
    {
        if (!OboroCombatUtility.IsHeroOrTrainingDummy(target) || !OboroCombatUtility.IsEnemy(transform, target)) return false;
        return OboroWController.CanBeTargetSelected(target, transform);
    }

    private bool IsInCastRange(Targetable target)
    {
        if (target == null) return false;
        Vector3 difference = OboroCombatUtility.Flatten(target.GetClosestPoint(transform.position) - transform.position);
        return difference.sqrMagnitude <= _castRange * _castRange;
    }

    private bool IsInExecuteRange(Targetable target)
    {
        HealthController health = OboroCombatUtility.GetHealth(target);
        return health != null && health.MaxHealth > 0f &&
               health.CurrentHealth / health.MaxHealth <= _executeHealthRatio + 0.0001f;
    }

    private void Execute(Targetable target)
    {
        if (!IsValidTarget(target) || !IsInCastRange(target) || !IsInExecuteRange(target)) return;

        _wController?.BreakStealth("R発動");
        _cooldownEndTime = Time.timeAsDouble + _cooldown;
        _remainingCooldown = _cooldown;
        _castSequence++;

        CommonDController commonD = target.GetComponentInParent<CommonDController>();
        if (commonD != null && commonD.TryBlockHardCC(transform))
        {
            Debug.Log("朧 R: 対象の共通Dにより完全不発になりました(クールダウンは消費)。", this);
            return;
        }

        HealthController health = OboroCombatUtility.GetHealth(target);
        if (health == null || health.IsDead) return;
        Vector3 direction = OboroCombatUtility.Flatten(target.transform.position - transform.position);
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _mouseFacing?.SetLookDirection(direction);
        }

        // 現在HPと同量の確定ダメージ。シールド/軽減が0より大きく吸収すればHPが残り、仕様どおり生存できる。
        float executeDamage = health.CurrentHealth;
        float actualDamage = health.TakeDamage(executeDamage, transform, DamageType.True,
            sourceId: $"OboroR#{_castSequence}");
        if (actualDamage > 0f)
        {
            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
        }

        if (health.IsDead)
        {
            Debug.Log($"朧 R: {target.name}を処刑しました。", this);
        }
        else
        {
            Debug.Log($"朧 R: {target.name}はシールドまたはダメージ軽減により生存しました。", this);
        }
    }

    private void HandleDied()
    {
        CancelPendingApproach();
        double remaining = _cooldownEndTime - Time.timeAsDouble;
        if (remaining > 0.0)
        {
            _cooldownEndTime = Time.timeAsDouble + remaining * (1f - _deathCooldownReduction);
            _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
            Debug.Log($"朧 R: デスにより残りクールダウンを{_deathCooldownReduction * 100f:F0}%短縮しました。", this);
        }
    }

    /// <summary>
    /// WorldHealthBar用。いずれかの生存中の朧から見て対象が敵ヒーロー/TrainingDummyなら閾値を返す。
    /// </summary>
    public static bool TryGetExecuteThreshold(HealthController targetHealth, out float ratio, out bool isInExecuteRange)
    {
        ratio = 0f;
        isInExecuteRange = false;
        if (targetHealth == null || targetHealth.IsDead) return false;
        Targetable target = targetHealth.GetComponent<Targetable>();
        if (target == null) target = targetHealth.GetComponentInParent<Targetable>();
        if (target == null) return false;

        foreach (OboroRController controller in ActiveControllers)
        {
            if (controller == null || !controller.isActiveAndEnabled ||
                (controller._selfHealth != null && controller._selfHealth.IsDead)) continue;
            if (!OboroCombatUtility.IsHeroOrTrainingDummy(target) ||
                !OboroCombatUtility.IsEnemy(controller.transform, target)) continue;

            ratio = Mathf.Clamp01(controller._executeHealthRatio);
            isInExecuteRange = targetHealth.MaxHealth > 0f &&
                               targetHealth.CurrentHealth / targetHealth.MaxHealth <= ratio + 0.0001f;
            return true;
        }

        return false;
    }
}
