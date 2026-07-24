using UnityEngine;

/// <summary>
/// F: フラッシュ(全キャラクター共通の場所指定スキル)。
/// Fを押した瞬間に、マウスカーソルが指すGround地点へ即座にブリンクする(着地地点プレビューなし)。
/// 仕様: 移動距離400(=4.0 Unity units。換算: 射程100 = 1 Unity unit) / クールダウン55秒 / 壁は越えられない。
/// カーソル地点が最大距離より遠い場合は、カーソル方向へ最大距離ぶんだけ移動する。
/// Wall Layerに設定した壁が経路上にある場合は壁の手前で停止する(壁拜け不可)。
/// デス時は残りクールダウンを60%短縮する(GAME_DESIGN.md 7章)。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FlashController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PlayerClickMovement _clickMovement;

    [Header("Flash")]
    // ステータス距離400 = 4.0 Unity units。
    [SerializeField, Min(0f)] private float _flashDistance = 4f;
    [SerializeField, Min(0f)] private float _cooldown = 55f;
    [SerializeField, Min(0f)] private float _minCastDistance = 0.1f;
    // デス時に残りクールダウンを短縮する割合(0.6 = 60%短縮)。GAME_DESIGN.md 7章準拠。
    [SerializeField, Range(0f, 1f)] private float _deathCooldownReduction = 0.6f;

    [Header("Layers")]
    // Ground/Targetableが未設定(=0)の場合、ZelfQControllerの設定を流用する。
    [SerializeField] private LayerMask _groundLayer;
    // 壁として扱うレイヤー。未設定(=0)の場合は壁判定を行わない(現在のプロトタイプマップに壁はない)。
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Debug (Runtime)")]
    [SerializeField] private float _remainingCooldown;

    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private HealthController _selfHealth;
    private PlayerMouseFacing _mouseFacing;
    private ZelfQController _qController;
    private ZelfRController _rController;
    private Camera _mainCamera;
    private float _cooldownEndTime;

    public float RemainingCooldown => Mathf.Max(0f, _cooldownEndTime - Time.time);

    private void Awake()
    {
        _characterController = _characterController != null ? _characterController : GetComponent<CharacterController>();
        _clickMovement = _clickMovement != null ? _clickMovement : GetComponent<PlayerClickMovement>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _selfHealth = GetComponent<HealthController>();
        if (_selfHealth != null) _selfHealth.Died += OnSelfDied;
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _qController = GetComponent<ZelfQController>();
        _rController = GetComponent<ZelfRController>();
        _mainCamera = Camera.main;

        if (_qController != null)
        {
            if (_groundLayer.value == 0) _groundLayer = _qController.GroundLayerMask;
            if (_targetableLayer.value == 0) _targetableLayer = _qController.TargetableLayerMask;
        }

        Debug.Log($"フラッシュ: 初期化しました(距離{_flashDistance} / CD{_cooldown}秒)。", this);
    }

    private void OnDestroy()
    {
        if (_selfHealth != null) _selfHealth.Died -= OnSelfDied;
    }

    private void Update()
    {
        _remainingCooldown = RemainingCooldown;

        // Fは押した瞬間に発動する(着地地点プレビューなし)。
        if (_inputHub != null && _inputHub.FPressedThisFrame)
        {
            HandleFPressed();
        }
    }

    // デス時: 残りクールダウンを60%短縮する(GAME_DESIGN.md 7章)。
    private void OnSelfDied()
    {
        float remaining = _cooldownEndTime - Time.time;
        if (remaining > 0f)
        {
            _cooldownEndTime = Time.time + remaining * (1f - _deathCooldownReduction);
            Debug.Log($"フラッシュ: デスにより残りクールダウンを{_deathCooldownReduction * 100f:F0}%短縮しました(残り{RemainingCooldown:F1}秒)。", this);
        }
    }

    private void HandleFPressed()
    {
        // 他の行動ロック中(W発動中・Eダッシュ中・死亡中など)は発動できない。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("フラッシュ: 他の行動中のため発動できません。", this);
            return;
        }
        if (Time.time < _cooldownEndTime)
        {
            Debug.Log($"フラッシュ: クールダウン中です(残り{RemainingCooldown:F1}秒)。", this);
            return;
        }
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            Debug.Log("フラッシュ: 死亡中のため発動できません。", this);
            return;
        }
        if (!TryGetFlashDestination(out Vector3 destination, out Vector3 direction))
        {
            Debug.Log("フラッシュ: マウスカーソルがGroundを指していないため発動しません。", this);
            return;
        }

        Vector3 delta = destination - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < _minCastDistance * _minCastDistance)
        {
            Debug.Log("フラッシュ: マウス地点が近すぎるため発動しません。", this);
            return;
        }

        PerformFlash(destination, direction);
    }

    // 発動時の着地地点を求める(最大距離クランプ → 壁クランプ → 接地Y補正)。
    private bool TryGetFlashDestination(out Vector3 destination, out Vector3 direction)
    {
        destination = transform.position;
        direction = transform.forward;
        if (!TryGetMouseGroundPoint(out Vector3 groundPoint)) return false;

        Vector3 delta = groundPoint - transform.position;
        delta.y = 0f;
        float distance = Mathf.Min(delta.magnitude, _flashDistance);
        if (delta.sqrMagnitude > 0.0001f) direction = delta.normalized;

        // 壁は越えられない: 経路上に壁がある場合、壁の手前まで距離を詰める。
        if (_wallLayer.value != 0 && distance > 0f)
        {
            Vector3 origin = transform.position + _characterController.center;
            float bodyRadius = _characterController.radius + _characterController.skinWidth;
            if (Physics.SphereCast(origin, bodyRadius, direction, out RaycastHit wallHit, distance, _wallLayer, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0f, wallHit.distance - _characterController.skinWidth);
            }
        }

        Vector3 target = transform.position + direction * distance;
        target.y = GetGroundedY(target);
        destination = target;
        return true;
    }

    private void PerformFlash(Vector3 destination, Vector3 direction)
    {
        _cooldownEndTime = Time.time + _cooldown;

        // 進行中の移動・Q/Rの射程外自動接近を中止してから瞬間移動する。
        if (_clickMovement != null) _clickMovement.StopMovement();
        if (_qController != null) _qController.CancelPendingApproach();
        if (_rController != null) _rController.CancelPendingApproach();

        bool controllerWasEnabled = _characterController.enabled;
        _characterController.enabled = false;
        transform.position = destination;
        ResolveOverlapBackward(direction);
        _characterController.enabled = controllerWasEnabled;

        // 視点仕様: ブリンクした場合はブリンクした方向を向く。
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);
        }

        Debug.Log("フラッシュ: 発動しました。", this);
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_inputHub == null || _groundLayer.value == 0) return false;
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return false;
        }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer, QueryTriggerInteraction.Ignore)) return false;
        point = hit.point;
        return true;
    }

    private float GetGroundedY(Vector3 position)
    {
        if (_groundLayer.value != 0 &&
            Physics.Raycast(new Vector3(position.x, transform.position.y + 20f, position.z), Vector3.down,
                out RaycastHit hit, 50f, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y + _characterController.height * 0.5f - _characterController.center.y + _characterController.skinWidth;
        }
        return transform.position.y;
    }

    // 着地地点がTargetableと重なった場合、発動元方向へ少しずつ戻して重なりを解消する。
    private void ResolveOverlapBackward(Vector3 direction)
    {
        if (_targetableLayer.value == 0) return;
        const int maxSteps = 10;
        const float stepDistance = 0.25f;
        for (int i = 0; i < maxSteps && IsOverlappingTargetable(); i++)
        {
            Vector3 position = transform.position - direction * stepDistance;
            position.y = GetGroundedY(position);
            transform.position = position;
        }
    }

    private bool IsOverlappingTargetable()
    {
        float radius = _characterController.radius + _characterController.skinWidth;
        Vector3 center = transform.position + _characterController.center;
        float halfHeight = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
        Vector3 point1 = center + Vector3.up * halfHeight;
        Vector3 point2 = center - Vector3.up * halfHeight;
        return Physics.OverlapCapsule(point1, point2, radius, _targetableLayer, QueryTriggerInteraction.Ignore).Length > 0;
    }
}
