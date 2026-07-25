using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ヴォルブラークE(突進とスタン)を管理する。
/// Eキーでマウスカーソル方向へ突進し、当たった敵へダメージとスタンを与える(GAME_DESIGN 12章)。
/// - 敵に当たると突進はそこで停止し、敵を突進方向へ少し押し出して、ヴォルブラークは敵の手前に止まる。
/// - 対象が共通Dの無効化ウィンドウ中の場合、ダメージとスタンの両方が不発になる。
///   ハードCCは必ずCrowdControlController.ApplyStun経由で適用し、戻り値がtrue(共通Dによる無効化)のときはダメージも適用しない。
/// - Tower分類は移動・行動しないためスタンは掛けない(ダメージのみ与える)。
/// - 移動スキルのためスネア中は発動できない(スタン中・死亡中などは行動ロックにより発動不可)。
/// - 突進中はAbilityLockControllerへロックを追加し、通常攻撃・他スキルの入力を禁止する。
/// - 自身が死亡した場合は突進を即時中断し、ロックを解除する。
/// - 突進の移動はZelfEControllerと同じ方式(CharacterControllerを一時無効化して直接移動・地面追従・終了時のめり込み解消)。
/// NormalCast: Eキーを押している間は方向線を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class VolbraakEController : MonoBehaviour
{
    // 突進中の行動ロック理由(文字列の打ち間違いを防ぐため定数を使用する)。
    private const string DashLockReason = "VolbraakEDash";

    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerClickMovement _clickMovement;

    [Header("Dash")]
    // 突進はゆっくり・長め(距離5.5を0.6秒かけて移動)。ゼルフEより重い突進として差別化する。
    [SerializeField, Min(0f)] private float _dashDistance = 5.5f;
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.6f;
    [SerializeField, Min(0f)] private float _hitRadius = 0.9f;
    [SerializeField, Min(0f)] private float _minCastDistance = 0.1f;
    // 敵に命中したとき、敵を突進方向へ押し出す距離(Unity units)。
    [SerializeField, Min(0f)] private float _hitPushDistance = 0.8f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _baseDamage = 40f;
    [SerializeField, Range(0f, 2f)] private float _adRatio = 0.7f;

    [Header("Stun")]
    // 命中した敵へ与えるスタンの持続時間(秒)。
    [SerializeField, Min(0f)] private float _stunDuration = 1f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 12f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;

    [Header("Layers")]
    // ZelfQControllerと同じレイヤーを設定する(Ground: マウス地点・地面高さ判定用 / Targetable: 命中判定用)。
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _trailColor = new Color(0.95f, 0.5f, 0.15f, 0.85f);
    [SerializeField, Min(0.05f)] private float _trailTime = 0.25f;
    [SerializeField, Min(0.01f)] private float _trailWidth = 0.5f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isDashing;
    [SerializeField] private float _remainingCooldown;

    private readonly HashSet<Targetable> _hitTargets = new HashSet<Targetable>();
    private PlayerMouseFacing _mouseFacing;
    private HealthController _selfHealth;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;
    private Vector3 _dashDirection;
    private float _remainingDashDistance;
    private float _dashSpeed;
    // クールダウン終了時刻。長時間起動でもfloat精度が落ちないよう、Time.timeAsDouble基準のdoubleで管理する。
    private double _cooldownEndTime;
    private bool _clickMovementWasEnabled;
    private bool _characterControllerWasEnabled;
    // 突進がAbilityLockControllerへロックを追加済みか(二重解除・未解除の防止)。
    private bool _lockAdded;
    private AbilityLockController _abilityLock;
    // CC(スタン・スネア)の参照。実行時に後から追加される場合があるため、未取得の間はUpdateで再取得する。
    private CrowdControlController _crowdControl;
    private PlayerInputHub _inputHub;
    private TrailRenderer _trail;
    private Material _trailMaterial;

    public bool IsDashing => _isDashing;

    private void Awake()
    {
        _characterController = _characterController != null ? _characterController : GetComponent<CharacterController>();
        _characterStats = _characterStats != null ? _characterStats : GetComponent<CharacterStats>();
        _clickMovement = _clickMovement != null ? _clickMovement : GetComponent<PlayerClickMovement>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _crowdControl = GetComponent<CrowdControlController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _selfHealth = GetComponent<HealthController>();
        _mainCamera = Camera.main;
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Volbraak E Range Indicator");

        if (_groundLayer.value == 0 || _targetableLayer.value == 0)
        {
            Debug.LogWarning("ヴォルブラーク E: Ground Layer / Targetable LayerをInspectorで設定してください(ZelfQControllerと同じ設定)。", this);
        }

        CreateTrail();
    }

    private void OnDestroy()
    {
        if (_trail != null) Destroy(_trail.gameObject);
        if (_trailMaterial != null) Destroy(_trailMaterial);
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);

        // CC参照の遅延取得(CrowdControlControllerが実行時に追加された場合に備える)。
        if (_crowdControl == null) _crowdControl = GetComponent<CrowdControlController>();

        if (_isDashing)
        {
            if (_selfHealth != null && _selfHealth.IsDead)
            {
                AbortDashOnDeath();
                return;
            }
            // 突進中のE再入力は受け付けない(診断用にログを出す)。
            if (_inputHub != null && _inputHub.EPressedThisFrame)
            {
                Debug.Log("ヴォルブラーク E: 突進中のため発動できません。", this);
            }
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            UpdateDash();
            return;
        }

        // NormalCast: 押している間は突進距離と方向を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
        UpdateRangeIndicator();

        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.EPressedThisFrame, _inputHub.EReleasedThisFrame))
        {
            HandleEPressed();
        }
    }

    // Eキーを押している間、本体→カーソル方向の直線(長さ=突進距離)のみを表示する(方向指定スキルの可視化)。
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.EPressed && !_isDashing
            && (_abilityLock == null || !_abilityLock.IsLocked)
            && (_crowdControl == null || !_crowdControl.IsMovementBlocked)
            && (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }

        float yOffset = _characterController != null
            ? _characterController.center.y - _characterController.height * 0.5f + 0.05f
            : 0.05f;

        // カーソルの地面位置から本体→カーソルのXZ方向を求め、突進距離ぶんの方向線のみを表示する。
        if (TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Vector3 direction = groundPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Vector3 origin = transform.position + new Vector3(0f, yOffset, 0f);
                _rangeIndicator.ShowDirectionLine(origin, direction.normalized, _dashDistance, new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0.9f));
                return;
            }
        }
        _rangeIndicator.HideAll();
    }

    private void HandleEPressed()
    {
        // 他の行動ロック中(スタン中・死亡中など)は発動できない。
        // クールダウン判定より先に確認し、ロックが原因のときは必ずこのログを出す。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("ヴォルブラーク E: 他の行動中のため発動できません。", this);
            return;
        }
        // スネア中は移動スキルのEを発動できない(スタン中は上の行動ロックで既に禁止済み)。
        if (_crowdControl != null && _crowdControl.IsMovementBlocked)
        {
            Debug.Log("ヴォルブラーク E: スネア中のため発動できません。", this);
            return;
        }
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("ヴォルブラーク E: クールダウン中です。", this);
            return;
        }
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            Debug.Log("ヴォルブラーク E: 死亡中のため発動できません。", this);
            return;
        }

        if (!TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Debug.Log("ヴォルブラーク E: マウスカーソルがGroundを指していないため発動しません。", this);
            return;
        }

        Vector3 direction = groundPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < _minCastDistance * _minCastDistance)
        {
            Debug.Log("ヴォルブラーク E: マウス地点が近すぎるため発動しません。", this);
            return;
        }

        StartDash(direction.normalized);
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_inputHub == null || _groundLayer.value == 0) return false;
        // Camera.mainは毎フレーム呼ぶと検索コストがかかるため、Awakeでキャッシュし、破棄時のみ再取得する。
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

    private void StartDash(Vector3 direction)
    {
        _dashDirection = direction;
        _remainingDashDistance = _dashDistance;
        _dashSpeed = _dashDistance / _dashDuration;
        _hitTargets.Clear();
        _isDashing = true;
        _cooldownEndTime = Time.timeAsDouble + _cooldown;

        if (_clickMovement != null)
        {
            _clickMovement.StopMovement();
            _clickMovementWasEnabled = _clickMovement.enabled;
            _clickMovement.enabled = false;
        }
        // 突進中は通常攻撃・Q・W・Rを含む全スキルの入力をロックする
        // (各コントローラーがIsLockedを確認する。コンポーネント自体は無効化しない)。
        if (_abilityLock != null && !_lockAdded)
        {
            _abilityLock.AddLock(DashLockReason);
            _lockAdded = true;
        }

        _characterControllerWasEnabled = _characterController.enabled;
        _characterController.enabled = false;
        FaceDashDirection();

        if (_trail != null) { _trail.Clear(); _trail.emitting = true; }
        Debug.Log("ヴォルブラーク E: 突進を発動しました。", this);
    }

    private void UpdateDash()
    {
        float step = Mathf.Min(_dashSpeed * Time.deltaTime, _remainingDashDistance);
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + _dashDirection * step;
        nextPosition.y = GetGroundedY(nextPosition);
        transform.position = nextPosition;
        _remainingDashDistance -= step;
        bool hitAny = HitTargetsAlongSegment(previousPosition, nextPosition);
        if (hitAny)
        {
            // 敵に当たったら突進はそこで停止する(ヴォルブラークは敵の手前に止まる)。
            EndDash(stoppedByHit: true);
            return;
        }
        if (_remainingDashDistance <= 0.0001f) EndDash(stoppedByHit: false);
    }

    private void EndDash(bool stoppedByHit)
    {
        // 命中停止時は「敵の手前」に止まるため後方(発動元方向)へ、通常終了時は従来どおり前方へ押し出して重なりを解消する。
        if (stoppedByHit) ResolveOverlapBackward();
        else ResolveOverlapWithTargetables();
        _characterController.enabled = _characterControllerWasEnabled;
        if (_clickMovement != null) _clickMovement.enabled = _clickMovementWasEnabled;
        // 突進が追加したロックを解除する。
        RemoveDashLock();

        _isDashing = false;
        FaceDashDirection();
        if (_trail != null) _trail.emitting = false;
    }

    private void AbortDashOnDeath()
    {
        _isDashing = false;
        if (_trail != null) _trail.emitting = false;

        // 死亡で突進が中断された場合もロックを解除する。
        // (死亡中の行動禁止はPlayerDeathHandlerが追加する死亡ロックが担当。
        //  移動・CharacterControllerはPlayerDeathHandlerが管理するため触らない)
        RemoveDashLock();
        Debug.Log("ヴォルブラーク E: 死亡により突進を中断しました。", this);
    }

    // 突進が追加したロックを解除する(未追加なら何もしない)。
    private void RemoveDashLock()
    {
        if (_abilityLock != null && _lockAdded)
        {
            _abilityLock.RemoveLock(DashLockReason);
            _lockAdded = false;
        }
    }

    private void FaceDashDirection()
    {
        if (_dashDirection.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(_dashDirection, Vector3.up);
        if (_mouseFacing != null) _mouseFacing.SetLookDirection(_dashDirection);
    }

    // 命中判定。1体でも命中した場合はtrueを返す(呼び元が突進を停止する)。
    private bool HitTargetsAlongSegment(Vector3 from, Vector3 to)
    {
        if (_targetableLayer.value == 0) return false;
        bool anyHit = false;
        Collider[] overlaps = Physics.OverlapCapsule(from, to, _hitRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            Targetable target = overlap.GetComponentInParent<Targetable>();
            if (target == null || _hitTargets.Contains(target)) continue;
            if (target.transform == transform || target.transform.IsChildOf(transform)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;
            HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
            if (health == null || health.IsDead) continue;
            _hitTargets.Add(target);
            anyHit = true;
            bool blockedByCommonD = ApplyHit(target, health);
            // 共通Dに弾かれた場合は攻撃自体が無効化されるため、押し出しは行わない(突進の停止のみ)。
            if (!blockedByCommonD) PushTarget(target);
        }
        return anyHit;
    }

    // 命中処理: スタン→ダメージの順で適用する。共通Dに弾かれた場合はtrueを返す。
    // 共通Dに無効化された場合(ApplyStunがtrue)は、ダメージとスタンの両方が不発になる(GAME_DESIGN 12章)。
    private bool ApplyHit(Targetable target, HealthController health)
    {
        // Tower分類は移動・行動しないためスタンは掛けない(ダメージのみ与える)。
        if (target.Classification != TargetClassification.Tower)
        {
            // CCを受け取る入口を取得(未追加でも動くようにget-or-add)。
            CrowdControlController cc = target.GetComponentInParent<CrowdControlController>();
            if (cc == null) cc = target.gameObject.AddComponent<CrowdControlController>();
            if (cc.ApplyStun(_stunDuration, transform))
            {
                Debug.Log($"ヴォルブラーク E: {target.name} の共通Dに弾かれたため、ダメージとスタンの両方が不発になりました。", this);
                return true;
            }
        }

        float damage = _baseDamage + (_characterStats != null ? _characterStats.CurrentAttackDamage : 0f) * _adRatio;
        float actualDamage = health.TakeDamage(damage, transform);
        if (actualDamage > 0f)
        {
            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
        }
        return false;
    }

    // 敵を突進方向へ少し押し出す(Tower分類は動かないため押し出さない)。
    // CharacterControllerを持つ相手はMoveで押し出し、それ以外はTransformを直接動かす。
    private void PushTarget(Targetable target)
    {
        if (_hitPushDistance <= 0f) return;
        if (target.Classification == TargetClassification.Tower) return;

        Vector3 push = _dashDirection * _hitPushDistance;
        CharacterController targetController = target.GetComponentInParent<CharacterController>();
        if (targetController != null && targetController.enabled)
        {
            targetController.Move(push);
        }
        else
        {
            target.transform.position += push;
        }
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

    // 突進終了時にTargetableへめり込んでいる場合、突進方向へ少しずつ押し出して重なりを解消する。
    private void ResolveOverlapWithTargetables()
    {
        if (_targetableLayer.value == 0) return;
        const int maxSteps = 10;
        const float stepDistance = 0.25f;
        for (int i = 0; i < maxSteps && IsOverlappingTargetable(); i++)
        {
            Vector3 position = transform.position + _dashDirection * stepDistance;
            position.y = GetGroundedY(position);
            transform.position = position;
        }
    }

    // 命中停止時: 敵と重なっている場合、発動元方向(後方)へ少しずつ戻して「敵の手前」で止まる。
    private void ResolveOverlapBackward()
    {
        if (_targetableLayer.value == 0) return;
        const int maxSteps = 10;
        const float stepDistance = 0.25f;
        for (int i = 0; i < maxSteps && IsOverlappingTargetable(); i++)
        {
            Vector3 position = transform.position - _dashDirection * stepDistance;
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

    private void CreateTrail()
    {
        GameObject trailObject = new GameObject("Volbraak E Dash Trail");
        trailObject.transform.SetParent(transform, false);
        _trail = trailObject.AddComponent<TrailRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _trailMaterial = new Material(shader);
        _trailMaterial.color = _trailColor;
        _trail.material = _trailMaterial;
        _trail.time = _trailTime;
        _trail.startWidth = _trailWidth;
        _trail.endWidth = _trailWidth * 0.1f;
        _trail.startColor = _trailColor;
        _trail.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
        _trail.numCornerVertices = 4;
        _trail.numCapVertices = 4;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.emitting = false;
    }
}
