using UnityEngine;

/// <summary>
/// 朧E。マウス下の敵ヒーロー/TrainingDummyを指定し、射程外なら自動接近する。
/// 発動時に開始地点を可視化して対象の真後ろへ移動し、「通常攻撃 + E追加ダメージ」を1回の通常ダメージとして与える。
/// 通常攻撃部分は朧Pの背後判定対象になる。帰還待機中はAbilityLockControllerで通常攻撃・全スキルを禁止し、
/// その間にスタンまたはスネアを受けた場合は開始地点へ戻らず、その場に残る。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class OboroEController : MonoBehaviour
{
    private const string AbilityLockReason = "OboroEReturn";

    [Header("Targeting")]
    [SerializeField, Min(0f)] private float _castRange = 4f;
    [SerializeField, Min(0f)] private float _behindOffset = 0.8f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _bonusBaseDamage = 20f;
    [SerializeField, Min(0f)] private float _bonusAdRatio = 0.4f;

    [Header("Return")]
    [SerializeField, Min(0f)] private float _returnDelay = 0.65f;
    [SerializeField, Min(0f)] private float _cooldown = 10f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _originMarkerColor = new Color(0.7f, 0.35f, 0.9f, 0.95f);
    [SerializeField, Min(0.1f)] private float _originMarkerRadius = 0.75f;
    [SerializeField, Min(0.005f)] private float _originMarkerWidth = 0.07f;
    [SerializeField, Min(12)] private int _originMarkerSegments = 48;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isExecuting;
    [SerializeField] private bool _isReturnPrevented;
    [SerializeField] private bool _isApproachingETarget;
    [SerializeField] private float _remainingCooldown;

    private double _cooldownEndTime;
    private float _returnTime;
    private Vector3 _originPosition;
    private Targetable _pendingTarget;
    private CharacterController _characterController;
    private CharacterStats _stats;
    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private CrowdControlController _crowdControl;
    private PlayerClickMovement _clickMovement;
    private PlayerMouseFacing _mouseFacing;
    private HealthController _selfHealth;
    private OboroPassiveBackstab _passive;
    private OboroWController _wController;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;
    private LineRenderer _originMarker;
    private Material _originMarkerMaterial;
    private bool _lockAdded;
    private int _castSequence;

    public bool IsExecuting => _isExecuting;
    public bool IsApproachingETarget => _isApproachingETarget;
    public float RemainingCooldown => _remainingCooldown;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _stats = GetComponent<CharacterStats>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _crowdControl = GetComponent<CrowdControlController>();
        _clickMovement = GetComponent<PlayerClickMovement>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _selfHealth = GetComponent<HealthController>();
        _passive = GetComponent<OboroPassiveBackstab>();
        _wController = GetComponent<OboroWController>();
        _mainCamera = Camera.main;
        _groundLayer = OboroCombatUtility.ResolveGroundLayer(_groundLayer);
        _targetableLayer = OboroCombatUtility.ResolveTargetableLayer(_targetableLayer);
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Oboro E Range Indicator");

        if (_selfHealth != null) _selfHealth.Died += HandleDied;
    }

    private void OnDestroy()
    {
        if (_selfHealth != null) _selfHealth.Died -= HandleDied;
        DestroyOriginMarker();
    }

    private void OnDisable()
    {
        CancelPendingApproach();
        if (_rangeIndicator != null) _rangeIndicator.HideAll();
        if (_isExecuting) FinishExecution(false, "コンポーネント停止");
        RemoveOwnLock();
        DestroyOriginMarker();
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
        if (_crowdControl == null) _crowdControl = GetComponent<CrowdControlController>();

        if (_isExecuting)
        {
            if (!_isReturnPrevented && _crowdControl != null && (_crowdControl.IsStunned || _crowdControl.IsSnared))
            {
                _isReturnPrevented = true;
                Debug.Log("朧 E: 帰還待機中にハードCCを受けたため、開始地点への帰還が阻害されました。", this);
            }

            if ((_selfHealth != null && _selfHealth.IsDead) || OboroCombatUtility.IsMatchEnded)
            {
                FinishExecution(false, _selfHealth != null && _selfHealth.IsDead ? "死亡" : "試合終了");
                return;
            }

            if (Time.time >= _returnTime)
            {
                FinishExecution(!_isReturnPrevented, _isReturnPrevented ? "CC帰還阻害" : "帰還完了");
            }
            return;
        }

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
            if (_inputHub != null && _inputHub.EReleasedThisFrame)
            {
                Debug.Log("朧 E: 他の行動中のため発動できません。", this);
            }
            return;
        }

        UpdateRangeIndicator();
        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.EPressedThisFrame, _inputHub.EReleasedThisFrame))
        {
            HandleCastInput();
        }
        UpdatePendingApproach();
    }

    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.EPressed && !_isExecuting &&
                       (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }
        _rangeIndicator.ShowCircle(_castRange, _originMarkerColor, 0.05f);
    }

    private void HandleCastInput()
    {
        CancelPendingApproach();
        if (_selfHealth != null && _selfHealth.IsDead) return;
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("朧 E: クールダウン中です。", this);
            return;
        }
        if (_crowdControl != null && _crowdControl.IsMovementBlocked)
        {
            Debug.Log("朧 E: スタン/スネア中のため発動できません。", this);
            return;
        }
        if (!TryGetValidTargetUnderMouse(out Targetable target))
        {
            Debug.Log("朧 E: マウスを有効な敵ヒーローへ合わせてください。", this);
            return;
        }

        if (IsInRange(target))
        {
            BeginExecution(target);
            return;
        }

        _pendingTarget = target;
        _isApproachingETarget = true;
        _clickMovement?.StopMovement();
        Debug.Log("朧 E: 射程外のため自動接近を開始しました。", this);
    }

    private void UpdatePendingApproach()
    {
        if (_pendingTarget == null) return;
        if (_inputHub != null && _inputHub.RightClickPressedThisFrame)
        {
            CancelPendingApproach();
            Debug.Log("朧 E: 右クリック入力により自動接近を中止しました。", this);
            return;
        }
        if ((_selfHealth != null && _selfHealth.IsDead) ||
            (_crowdControl != null && _crowdControl.IsMovementBlocked) ||
            Time.timeAsDouble < _cooldownEndTime || !IsValidTarget(_pendingTarget))
        {
            CancelPendingApproach();
            return;
        }

        if (!IsInRange(_pendingTarget))
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
        BeginExecution(target);
    }

    public void CancelPendingApproach()
    {
        _pendingTarget = null;
        _isApproachingETarget = false;
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

    private bool IsInRange(Targetable target)
    {
        if (target == null) return false;
        Vector3 difference = OboroCombatUtility.Flatten(target.GetClosestPoint(transform.position) - transform.position);
        return difference.sqrMagnitude <= _castRange * _castRange;
    }

    private void BeginExecution(Targetable target)
    {
        if (!IsValidTarget(target) || !IsInRange(target)) return;

        _wController?.BreakStealth("E発動");
        _cooldownEndTime = Time.timeAsDouble + _cooldown;
        _remainingCooldown = _cooldown;
        _castSequence++;
        _originPosition = transform.position;
        _isExecuting = true;
        _isReturnPrevented = false;
        _returnTime = Time.time + _returnDelay;
        _clickMovement?.StopMovement();
        CreateOriginMarker(_originPosition);

        if (_abilityLock != null && !_lockAdded)
        {
            _abilityLock.AddLock(AbilityLockReason);
            _lockAdded = true;
        }

        Vector3 targetForward = OboroCombatUtility.Flatten(target.transform.forward);
        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            targetForward = OboroCombatUtility.Flatten(target.transform.position - transform.position);
        }
        if (targetForward.sqrMagnitude <= 0.0001f) targetForward = Vector3.forward;

        Vector3 destination = target.transform.position - targetForward.normalized * _behindOffset;
        OboroCombatUtility.Teleport(transform, _characterController, destination, _groundLayer);
        FaceTarget(target);
        PerformAttack(target);
        Debug.Log($"朧 E: {target.name}の真後ろへ移動し、帰還待機を開始しました。", this);
    }

    private void PerformAttack(Targetable target)
    {
        if (!OboroCombatUtility.IsAlive(target)) return;
        HealthController targetHealth = OboroCombatUtility.GetHealth(target);
        if (targetHealth == null) return;

        float rawDamage = _stats.CurrentAttackDamage + _bonusBaseDamage + _stats.CurrentAttackDamage * _bonusAdRatio;
        float passiveBonus = 0f;
        bool passiveTriggered = _passive != null && _passive.TryGetBonusDamage(target, out passiveBonus);
        if (passiveTriggered) rawDamage += passiveBonus;

        float actualDamage = targetHealth.TakeDamage(rawDamage, transform, DamageType.Normal,
            isBasicAttack: true, sourceId: $"OboroE#{_castSequence}");
        if (actualDamage <= 0f) return;

        target.PlayHitFlash();
        CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
        if (passiveTriggered) _passive.NotifyTriggered(target, passiveBonus);
    }

    private void FaceTarget(Targetable target)
    {
        if (target == null) return;
        Vector3 direction = OboroCombatUtility.Flatten(target.transform.position - transform.position);
        if (direction.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _mouseFacing?.SetLookDirection(direction);
    }

    private void FinishExecution(bool shouldReturn, string reason)
    {
        if (!_isExecuting) return;
        if (shouldReturn && (_selfHealth == null || !_selfHealth.IsDead))
        {
            Vector3 before = transform.position;
            OboroCombatUtility.Teleport(transform, _characterController, _originPosition, _groundLayer);
            Vector3 direction = OboroCombatUtility.Flatten(transform.position - before);
            if (direction.sqrMagnitude > 0.0001f) _mouseFacing?.SetLookDirection(direction);
        }

        _isExecuting = false;
        _isReturnPrevented = false;
        RemoveOwnLock();
        DestroyOriginMarker();
        Debug.Log($"朧 E: {reason}。{(shouldReturn ? "開始地点へ帰還しました。" : "現在地点に残りました。")}", this);
    }

    private void HandleDied()
    {
        CancelPendingApproach();
        if (_isExecuting) FinishExecution(false, "死亡");
    }

    private void RemoveOwnLock()
    {
        if (_abilityLock != null && _lockAdded)
        {
            _abilityLock.RemoveLock(AbilityLockReason);
            _lockAdded = false;
        }
    }

    private void CreateOriginMarker(Vector3 position)
    {
        DestroyOriginMarker();
        GameObject markerObject = new GameObject("Oboro E Return Origin");
        markerObject.transform.position = position + Vector3.up * 0.04f;
        _originMarker = markerObject.AddComponent<LineRenderer>();
        _originMarkerMaterial = OboroCombatUtility.CreateUnlitMaterial(_originMarkerColor);
        _originMarker.material = _originMarkerMaterial;
        _originMarker.useWorldSpace = false;
        _originMarker.loop = true;
        _originMarker.alignment = LineAlignment.View;
        _originMarker.startWidth = _originMarkerWidth;
        _originMarker.endWidth = _originMarkerWidth;
        _originMarker.startColor = _originMarkerColor;
        _originMarker.endColor = _originMarkerColor;
        _originMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _originMarker.receiveShadows = false;
        int count = Mathf.Max(12, _originMarkerSegments);
        _originMarker.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            _originMarker.SetPosition(i, new Vector3(Mathf.Cos(angle) * _originMarkerRadius, 0f,
                Mathf.Sin(angle) * _originMarkerRadius));
        }
    }

    private void DestroyOriginMarker()
    {
        if (_originMarker != null) Destroy(_originMarker.gameObject);
        if (_originMarkerMaterial != null) Destroy(_originMarkerMaterial);
        _originMarker = null;
        _originMarkerMaterial = null;
    }
}
