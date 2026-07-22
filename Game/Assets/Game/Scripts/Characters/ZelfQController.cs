using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerTargetSelector))]
public sealed class ZelfQController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerTargetSelector _targetSelector;
    [SerializeField] private PlayerClickMovement _clickMovement;

    [Header("Targeting")]
    [SerializeField, Min(0f)] private float _targetRange = 4.5f;
    [SerializeField, Min(0f)] private float _blinkStopDistance = 0.75f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _baseDamage = 30f;
    [SerializeField, Min(0f)] private float _adRatio = 0.6f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 6f;
    [SerializeField, Min(0f)] private float _sameTargetLockout = 1.25f;
    [SerializeField, Range(0f, 1f)] private float _minionCooldownReductionPercent = 0.5f;

    [Header("Circles")]
    [SerializeField, Min(12)] private int _circleSegments = 64;
    [SerializeField, Min(0.005f)] private float _circleWidth = 0.035f;
    [SerializeField] private Color _rangeCircleColor = Color.white;
    [SerializeField] private Color _lockCircleColor = new Color(0.2f, 0.7f, 1f, 0.95f);

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isQAvailable = true;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private bool _isCurrentTargetLocked;
    [SerializeField] private bool _isApproachingQTarget;

    private readonly Dictionary<Targetable, float> _locks = new Dictionary<Targetable, float>();
    private readonly Dictionary<Targetable, LineRenderer> _lockCircles = new Dictionary<Targetable, LineRenderer>();
    private Targetable _pendingTarget;
    private float _cooldownEndTime;
    private bool _movementSuppressedUntilRightRelease;
    private LineRenderer _rangeCircle;
    private Material _rangeMaterial;
    private Material _lockMaterial;
    private PlayerMouseFacing _mouseFacing;
    private ZelfPassiveHeal _passiveHeal;
    private ZelfRController _rController;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;
    private Camera _mainCamera;

    public bool IsQAvailable => _isQAvailable;
    public float RemainingCooldown => _remainingCooldown;
    public bool IsCurrentTargetLocked => _isCurrentTargetLocked;
    public bool IsApproachingQTarget => _isApproachingQTarget;

    /// <summary>InspectorのGround用LayerMask。ゼルフEなど他スキルが同じレイヤー設定を共有するために公開する。</summary>
    public LayerMask GroundLayerMask => _groundLayer;

    /// <summary>InspectorのTargetable用LayerMask。ゼルフEなど他スキルが同じレイヤー設定を共有するために公開する。</summary>
    public LayerMask TargetableLayerMask => _targetableLayer;

    /// <summary>
    /// QのCDだけを即時0にする(ゼルフEのCharacter分類命中時などから呼び出す)。
    /// Same Target Lockoutは解除・削除せず、Q自動接近中の状態も変更しない。
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownEndTime = Time.time;
        _remainingCooldown = 0f;
        _isQAvailable = true;
    }

    /// <summary>
    /// 指定TargetableのSame Target Lockoutを即時解除する(ゼルフEのCharacter分類命中時などから呼び出す)。
    /// ロックリングも同時に破棄する。指定対象がロック中でなければ何もしない。
    /// </summary>
    public void ClearLockout(Targetable target)
    {
        if (target == null || !_locks.ContainsKey(target)) return;
        _locks.Remove(target);
        if (_lockCircles.TryGetValue(target, out LineRenderer circle) && circle != null)
        {
            Destroy(circle.gameObject);
        }
        _lockCircles.Remove(target);
    }

    /// <summary>
    /// Q射程外の自動接近中であれば中止する(ゼルフEの発動時などから呼び出す)。自動接近中でなければ何もしない。
    /// </summary>
    public void CancelPendingApproach()
    {
        if (_pendingTarget == null) return;
        CancelPendingCast(false);
    }

    private void Awake()
    {
        _characterController = _characterController != null ? _characterController : GetComponent<CharacterController>();
        _characterStats = _characterStats != null ? _characterStats : GetComponent<CharacterStats>();
        _targetSelector = _targetSelector != null ? _targetSelector : GetComponent<PlayerTargetSelector>();
        _clickMovement = _clickMovement != null ? _clickMovement : GetComponent<PlayerClickMovement>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _passiveHeal = GetComponent<ZelfPassiveHeal>();
        _rController = GetComponent<ZelfRController>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mainCamera = Camera.main;
        CreateRangeCircle();
    }

    private void OnDestroy()
    {
        if (_rangeCircle != null) Destroy(_rangeCircle.gameObject);
        foreach (LineRenderer circle in _lockCircles.Values) if (circle != null) Destroy(circle.gameObject);
        if (_rangeMaterial != null) Destroy(_rangeMaterial);
        if (_lockMaterial != null) Destroy(_lockMaterial);
    }

    private void Update()
    {
        RestoreMovementAfterRightClickRelease();
        UpdateCooldownAndLocks();
        UpdateLockCircles();

        // 行動ロック中(W発動中・Eダッシュ中・死亡中など)は入力を受け付けず、自動接近も中止する。
        // クールダウン・同一対象ロックの進行はロック中も継続する。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            if (_pendingTarget != null) CancelPendingCast(false);
            if (_rangeCircle != null) _rangeCircle.enabled = false;
            // 診断用: ロック中のQ押下は理由をログに出す。
            if (_inputHub != null && _inputHub.QPressedThisFrame)
            {
                Debug.Log("Zelf Q: 他の行動中のため発動できません。", this);
            }
            return;
        }

        UpdateRangeCircle();

        if (_inputHub != null && _inputHub.QPressedThisFrame)
        {
            HandleQPressed();
        }
        UpdatePendingCast();
    }

    private void HandleQPressed()
    {
        CancelPendingCast(false);
        if (!_isQAvailable)
        {
            Log("Zelf Q: クールダウン中です。");
            return;
        }

        Targetable target = GetQTarget();
        if (!CanCastAt(target, true)) return;

        // Rの自動接近と同時進行しないよう中止する(移動の二重制御を防ぐ)。
        if (_rController != null) _rController.CancelPendingApproach();

        if (IsInRange(target))
        {
            Cast(target);
            return;
        }

        _pendingTarget = target;
        _isApproachingQTarget = true;
        if (_clickMovement != null) _clickMovement.StopMovement();
        Log("Zelf Q: 射程外のため自動接近を開始します。");
    }

    private void UpdatePendingCast()
    {
        if (_pendingTarget == null) return;
        if (_inputHub != null && _inputHub.RightClickPressedThisFrame)
        {
            CancelPendingCast(false);
            Log("Zelf Q: 右クリック入力により自動接近を中止しました。");
            return;
        }
        if (!_isQAvailable || !CanCastAt(_pendingTarget, false))
        {
            CancelPendingCast(false);
            return;
        }
        if (!IsInRange(_pendingTarget))
        {
            Vector3 direction = _pendingTarget.GetClosestPoint(transform.position) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _characterController.Move(direction.normalized * _characterStats.CurrentMoveSpeed * Time.deltaTime);
                if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);
            }
            return;
        }

        Targetable target = _pendingTarget;
        CancelPendingCast(true);
        Cast(target);
    }

    private void Cast(Targetable target)
    {
        if (!CanCastAt(target, true) || !IsInRange(target)) return;
        BlinkTo(target);
        StopMovementAfterQCast();

        HealthController health = GetHealth(target);
        float actualDamage = health.TakeDamage(_baseDamage + _characterStats.CurrentAttackDamage * _adRatio, transform);
        if (actualDamage > 0f)
        {
            target.PlayHitFlash();
            NotifyCombatSystems(actualDamage, target);
        }

        _locks[target] = Time.time + _sameTargetLockout;
        CreateLockCircle(target);
        _cooldownEndTime = Time.time + _cooldown;
        if (target.Classification == TargetClassification.Character || target.Classification == TargetClassification.TrainingDummy)
        {
            _cooldownEndTime = Time.time;
        }
        else if (target.Classification == TargetClassification.Minion)
        {
            _cooldownEndTime = Time.time + Mathf.Max(0f, _cooldownEndTime - Time.time) * (1f - _minionCooldownReductionPercent);
        }
        Log("Zelf Q: 発動成功。");
    }

    private void StopMovementAfterQCast()
    {
        if (_clickMovement == null) return;
        _clickMovement.StopMovement();
        if (_inputHub != null && _inputHub.RightClickPressed)
        {
            _clickMovement.enabled = false;
            _movementSuppressedUntilRightRelease = true;
        }
    }

    private void RestoreMovementAfterRightClickRelease()
    {
        if (!_movementSuppressedUntilRightRelease) return;
        if (_inputHub != null && _inputHub.RightClickPressed) return;
        if (_clickMovement != null) _clickMovement.enabled = true;
        _movementSuppressedUntilRightRelease = false;
    }

    private void CancelPendingCast(bool stopMovement)
    {
        _pendingTarget = null;
        _isApproachingQTarget = false;
        if (stopMovement && _clickMovement != null) _clickMovement.StopMovement();
    }

    private Targetable GetQTarget()
    {
        return TryGetTargetUnderMouse(out Targetable mouseTarget) ? mouseTarget : null;
    }

    private bool TryGetTargetUnderMouse(out Targetable target)
    {
        target = null;
        if (_inputHub == null || _targetableLayer.value == 0) return false;
        // Camera.mainは毎フレーム呼ぶと検索コストがかかるため、Awakeでキャッシュし、破棄時のみ再取得する。
        if (_mainCamera == null) { _mainCamera = Camera.main; if (_mainCamera == null) return false; }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _targetableLayer, QueryTriggerInteraction.Ignore)) return false;
        target = hit.collider.GetComponentInParent<Targetable>();
        return target != null;
    }

    private bool CanCastAt(Targetable target, bool log)
    {
        if (target == null)
        {
            if (log) Log("Zelf Q: マウスを有効な敵に合わせてQを押してください。");
            return false;
        }
        HealthController health = GetHealth(target);
        if (!target.isActiveAndEnabled || target.IsDead || health == null || health.IsDead)
        {
            if (log) Log("Zelf Q: 対象が無効または死亡済みです。");
            return false;
        }
        if (target.Classification == TargetClassification.Tower)
        {
            if (log) Log("Zelf Q: Tower分類の対象には発動できません。");
            return false;
        }
        if (IsLocked(target))
        {
            if (log) Log("Zelf Q: この対象は同一対象ロック中です。");
            return false;
        }
        return true;
    }

    private static HealthController GetHealth(Targetable target)
    {
        return target == null ? null : target.Health != null ? target.Health : target.GetComponent<HealthController>();
    }

    private bool IsInRange(Targetable target)
    {
        Vector3 difference = target.GetClosestPoint(transform.position) - transform.position;
        difference.y = 0f;
        return difference.sqrMagnitude <= _targetRange * _targetRange;
    }

    private void BlinkTo(Targetable target)
    {
        Vector3 closest = target.GetClosestPoint(transform.position);
        Vector3 away = transform.position - closest;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        away.y = 0f;
        away.Normalize();

        Vector3 destination = closest + away * _blinkStopDistance;
        if (_groundLayer.value != 0 && Physics.Raycast(new Vector3(destination.x, transform.position.y + 20f, destination.z), Vector3.down, out RaycastHit hit, 50f, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            destination.y = hit.point.y + _characterController.height * 0.5f - _characterController.center.y + _characterController.skinWidth;
        }
        else destination.y = transform.position.y;

        Vector3 blinkStartPosition = transform.position;
        bool enabled = _characterController.enabled;
        _characterController.enabled = false;
        transform.position = destination;
        _characterController.enabled = enabled;
        FaceBlinkDirection(blinkStartPosition, target);
    }

    private void FaceBlinkDirection(Vector3 blinkStartPosition, Targetable target)
    {
        Vector3 direction = transform.position - blinkStartPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f && target != null)
        {
            direction = target.transform.position - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);
    }

    private void UpdateCooldownAndLocks()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);
        _isQAvailable = _remainingCooldown <= 0f;
        _isCurrentTargetLocked = IsLocked(GetQTarget());
        List<Targetable> remove = null;
        foreach (KeyValuePair<Targetable, float> pair in _locks)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.IsDead || Time.time >= pair.Value)
            {
                if (remove == null) remove = new List<Targetable>();
                remove.Add(pair.Key);
            }
        }
        if (remove == null) return;
        foreach (Targetable target in remove)
        {
            _locks.Remove(target);
            if (_lockCircles.TryGetValue(target, out LineRenderer circle) && circle != null) Destroy(circle.gameObject);
            _lockCircles.Remove(target);
        }
    }

    private bool IsLocked(Targetable target)
    {
        return target != null && _locks.TryGetValue(target, out float expiry) && Time.time < expiry;
    }

    private void CreateRangeCircle()
    {
        GameObject objectForCircle = new GameObject("Zelf Q Range Circle");
        objectForCircle.transform.SetParent(transform, false);
        _rangeCircle = objectForCircle.AddComponent<LineRenderer>();
        _rangeMaterial = CreateMaterial(_rangeCircleColor);
        ConfigureCircle(_rangeCircle, _rangeMaterial, _rangeCircleColor);
        _rangeCircle.enabled = false;
    }

    private void UpdateRangeCircle()
    {
        bool visible = _inputHub != null && _inputHub.QPressed;
        _rangeCircle.enabled = visible;
        if (!visible) return;
        _rangeCircle.transform.localPosition = new Vector3(0f, _characterController.center.y - _characterController.height * 0.5f + 0.025f, 0f);
        DrawCircle(_rangeCircle, _targetRange, 1f, true);
    }

    private void CreateLockCircle(Targetable target)
    {
        if (_lockCircles.TryGetValue(target, out LineRenderer oldCircle) && oldCircle != null) Destroy(oldCircle.gameObject);
        GameObject objectForCircle = new GameObject("Zelf Q Same Target Lock");
        objectForCircle.transform.SetParent(target.transform, false);
        LineRenderer circle = objectForCircle.AddComponent<LineRenderer>();
        if (_lockMaterial == null) _lockMaterial = CreateMaterial(_lockCircleColor);
        ConfigureCircle(circle, _lockMaterial, _lockCircleColor);
        _lockCircles[target] = circle;
    }

    private void UpdateLockCircles()
    {
        foreach (KeyValuePair<Targetable, LineRenderer> pair in _lockCircles)
        {
            if (pair.Key == null || pair.Value == null || !_locks.TryGetValue(pair.Key, out float expiry)) continue;
            Collider collider = pair.Key.GetComponent<Collider>();
            float y = collider == null ? 0f : collider.bounds.min.y - pair.Key.transform.position.y + 0.03f;
            float radius = collider == null ? 0.6f : Mathf.Max(0.45f, Mathf.Max(collider.bounds.size.x, collider.bounds.size.z) * 0.65f);
            pair.Value.transform.localPosition = new Vector3(0f, y, 0f);
            DrawCircle(pair.Value, radius, Mathf.Clamp01((expiry - Time.time) / _sameTargetLockout), false);
        }
    }

    private void ConfigureCircle(LineRenderer line, Material material, Color color)
    {
        line.useWorldSpace = false;
        line.material = material;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = _circleWidth;
        line.endWidth = _circleWidth;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void DrawCircle(LineRenderer line, float radius, float amount, bool loop)
    {
        int segments = Mathf.Max(12, _circleSegments);
        int count = loop ? segments + 1 : Mathf.Max(2, Mathf.CeilToInt(segments * Mathf.Clamp01(amount)) + 1);
        line.loop = loop;
        line.positionCount = count;
        float angleLimit = loop ? Mathf.PI * 2f : Mathf.PI * 2f * Mathf.Clamp01(amount);
        for (int i = 0; i < count; i++)
        {
            float angle = angleLimit * i / (count - 1);
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void NotifyCombatSystems(float damage, Targetable target)
    {
        CombatTextManager.ShowDamageDealt(target.transform.position, damage);
        if (_passiveHeal != null) _passiveHeal.NotifyDamageDealt(damage, target.Classification);
    }

    private void Log(string message)
    {
        Debug.Log(message, this);
    }
}
