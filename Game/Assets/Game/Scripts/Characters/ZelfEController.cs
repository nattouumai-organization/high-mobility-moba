using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゼルフE(方向ダッシュ)を管理する。
/// TASKS.md「ゼルフEの方向ダッシュを実装する」「ゼルフE命中時のQ即時再使用を実装する」用のスクリプト。
/// Eキー(Input System)で、マウスカーソルが指すGround上の地点の方向へDash Distanceだけ、Dash Durationをかけてダッシュする
/// (ブリンクではない)。マウスがGroundを指していない場合・マウス地点が近すぎる場合・クールダウン中は発動しない。
/// ダッシュ経路(開始地点〜終点)と、終点からEnd Extensionぶん先までを、Hit RadiusのカプセルでTargetableLayerのみ判定し、
/// 有効なTargetableへ E Damage = Base Damage + Current Attack Damage × AD Ratio の通常ダメージをHealthController経由で与える
/// (同じTargetableにはE 1回につき1回だけ。Tower分類にも与える)。
/// Character分類のTargetable(Character分類のTrainingDummyを含む)へ1体以上命中した場合、
/// ZelfQController.ResetCooldown()でQの残りクールダウンを即時0にする(Same Target Lockoutは解除しない)。
/// ダッシュ中はCharacterControllerを無効化して位置を直接更新するため、対象のColliderに引っかかって止まり続けず、
/// 終了時に対象と重なったままの場合は安全な位置へ補正する。NavMesh / NavMeshAgentは使用しない。
/// 視点仕様に従い、ダッシュ開始時・終了時はダッシュ方向を向く(PlayerMouseFacingのpublic APIを使用)。
/// ダッシュ中は青いTrailRendererの残像を表示し、終了後短時間で消える(外部アセット不使用)。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class ZelfEController : MonoBehaviour
{
    [Header("References")]
    // 未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerClickMovement _clickMovement;
    [SerializeField] private PlayerBasicAttackController _basicAttackController;
    [SerializeField] private ZelfQController _qController;

    [Header("Dash")]
    // ダッシュ距離(Unity units)。
    [SerializeField, Min(0f)] private float _dashDistance = 4f;

    // ダッシュにかける時間(秒)。ブリンクではなく短時間の移動として実装する。
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.18f;

    // 経路命中判定の半径(Unity units)。
    [SerializeField, Min(0f)] private float _hitRadius = 0.6f;

    // ダッシュ終了地点からさらに先まで命中判定を延長する距離(Unity units)。
    [SerializeField, Min(0f)] private float _endExtension = 0.75f;

    // Playerとマウス地点がこの距離より近い場合は発動しない(Unity units)。
    [SerializeField, Min(0f)] private float _minCastDistance = 0.1f;

    [Header("Damage")]
    // E Damage = Base Damage + Current Attack Damage × AD Ratio。
    [SerializeField, Min(0f)] private float _baseDamage = 20f;
    [SerializeField, Range(0f, 2f)] private float _adRatio = 0.5f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 8f;

    [Header("Layers")]
    // 未設定(Nothing)の場合は、AwakeでZelfQControllerと同じLayerMask設定を自動使用する。
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    // ダッシュ残像(TrailRenderer)の色。
    [SerializeField] private Color _trailColor = new Color(0.25f, 0.6f, 1f, 0.85f);

    // 残像が消えるまでの時間(秒)。
    [SerializeField, Min(0.05f)] private float _trailTime = 0.25f;

    // 残像の幅(Unity units)。
    [SerializeField, Min(0.01f)] private float _trailWidth = 0.45f;

    [Header("Debug (Runtime)")]
    // ダッシュ中かどうか(Inspector確認用)。
    [SerializeField] private bool _isDashing;

    // Eの残りクールダウン秒数(Inspector確認用)。
    [SerializeField] private float _remainingCooldown;

    // 同じTargetableへE 1回につき1回だけ命中させるための記録。
    private readonly HashSet<Targetable> _hitTargets = new HashSet<Targetable>();

    private PlayerMouseFacing _mouseFacing;
    private ZelfPassiveHeal _passiveHeal;
    private HealthController _selfHealth;
    private Camera _mainCamera;
    private Vector3 _dashDirection;
    private float _remainingDashDistance;
    private float _dashSpeed;
    private float _cooldownEndTime;
    private bool _hitCharacterClassification;
    private bool _clickMovementWasEnabled;
    private bool _basicAttackWasEnabled;
    private bool _characterControllerWasEnabled;
    private TrailRenderer _trail;
    private Material _trailMaterial;

    /// <summary>ダッシュ中かどうか。</summary>
    public bool IsDashing => _isDashing;

    private void Awake()
    {
        _characterController = _characterController != null ? _characterController : GetComponent<CharacterController>();
        _characterStats = _characterStats != null ? _characterStats : GetComponent<CharacterStats>();
        _clickMovement = _clickMovement != null ? _clickMovement : GetComponent<PlayerClickMovement>();
        _basicAttackController = _basicAttackController != null ? _basicAttackController : GetComponent<PlayerBasicAttackController>();
        _qController = _qController != null ? _qController : GetComponent<ZelfQController>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _passiveHeal = GetComponent<ZelfPassiveHeal>();
        _selfHealth = GetComponent<HealthController>();
        _mainCamera = Camera.main;

        // LayerMaskが未設定(Nothing)の場合は、ZelfQControllerと同じInspector設定を共有する(Layer番号の固定値は使わない)。
        if (_qController != null)
        {
            if (_groundLayer.value == 0)
            {
                _groundLayer = _qController.GroundLayerMask;
            }

            if (_targetableLayer.value == 0)
            {
                _targetableLayer = _qController.TargetableLayerMask;
            }
        }

        CreateTrail();
    }

    private void OnDestroy()
    {
        if (_trail != null)
        {
            Destroy(_trail.gameObject);
        }

        if (_trailMaterial != null)
        {
            Destroy(_trailMaterial);
        }
    }

    private void Update()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        if (_isDashing)
        {
            // ダッシュ中に死亡した場合は安全に中断する(コンポーネントの復元はPlayerDeathHandler側の状態を尊重する)。
            if (_selfHealth != null && _selfHealth.IsDead)
            {
                AbortDashOnDeath();
                return;
            }

            UpdateDash();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            HandleEPressed();
        }
    }

    private void HandleEPressed()
    {
        if (Time.time < _cooldownEndTime)
        {
            Debug.Log("Zelf E: クールダウン中です。", this);
            return;
        }

        if (_selfHealth != null && _selfHealth.IsDead)
        {
            return;
        }

        // マウスカーソルがGround上の地点を指していない場合は発動しない。
        if (!TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Debug.Log("Zelf E: マウスカーソルがGroundを指していないため発動しません。", this);
            return;
        }

        Vector3 direction = groundPoint - transform.position;
        direction.y = 0f;

        // Playerとマウス地点が近すぎる場合は発動しない。
        if (direction.sqrMagnitude < _minCastDistance * _minCastDistance)
        {
            Debug.Log("Zelf E: マウス地点が近すぎるため発動しません。", this);
            return;
        }

        StartDash(direction.normalized);
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (Mouse.current == null || _groundLayer.value == 0)
        {
            return false;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                return false;
            }
        }

        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        point = hit.point;
        return true;
    }

    private void StartDash(Vector3 direction)
    {
        _dashDirection = direction;
        _remainingDashDistance = _dashDistance;
        _dashSpeed = _dashDistance / _dashDuration;
        _hitTargets.Clear();
        _hitCharacterClassification = false;
        _isDashing = true;
        _cooldownEndTime = Time.time + _cooldown;

        // 通常移動を停止し、ダッシュ中は右クリック移動・通常攻撃の自動接近で中断されないよう一時的に無効化する。
        if (_clickMovement != null)
        {
            _clickMovement.StopMovement();
            _clickMovementWasEnabled = _clickMovement.enabled;
            _clickMovement.enabled = false;
        }

        if (_basicAttackController != null)
        {
            _basicAttackWasEnabled = _basicAttackController.enabled;
            _basicAttackController.enabled = false;
        }

        // ゼルフQの自動接近中であれば中止する。
        if (_qController != null)
        {
            _qController.CancelPendingApproach();
        }

        // ダッシュ中は対象のColliderに引っかからないよう、CharacterControllerを無効化して位置を直接更新する。
        _characterControllerWasEnabled = _characterController.enabled;
        _characterController.enabled = false;

        // 視点仕様: ダッシュ方向を即時に向き、PlayerMouseFacingの目標回転も同じ方向へ更新する。
        FaceDashDirection();

        if (_trail != null)
        {
            _trail.Clear();
            _trail.emitting = true;
        }

        Debug.Log("Zelf E: ダッシュを発動しました。", this);
    }

    private void UpdateDash()
    {
        float step = Mathf.Min(_dashSpeed * Time.deltaTime, _remainingDashDistance);
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + _dashDirection * step;

        // ダッシュ中は地面へ傾かず(Y軸回転のみ)、Y座標はGround上の適切な高さを維持する。
        nextPosition.y = GetGroundedY(nextPosition);
        transform.position = nextPosition;
        _remainingDashDistance -= step;

        // このフレームで通過した経路ぶんの命中判定を行う。
        HitTargetsAlongSegment(previousPosition, nextPosition);

        if (_remainingDashDistance <= 0.0001f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        // ダッシュ終了地点からEnd Extensionぶん先までの命中判定を行う。
        Vector3 endPosition = transform.position;
        HitTargetsAlongSegment(endPosition, endPosition + _dashDirection * _endExtension);

        // Playerが対象へダッシュで重なったままになる場合は、安全な位置へ補正する。
        ResolveOverlapWithTargetables();

        // 一時的に無効化したコンポーネントを元へ戻す(E終了後は右クリック移動・通常攻撃・Q・Wが通常どおり動く)。
        _characterController.enabled = _characterControllerWasEnabled;

        if (_clickMovement != null)
        {
            _clickMovement.enabled = _clickMovementWasEnabled;
        }

        if (_basicAttackController != null)
        {
            _basicAttackController.enabled = _basicAttackWasEnabled;
        }

        _isDashing = false;

        // 視点仕様: ダッシュ後、Playerはダッシュ方向を向く。
        FaceDashDirection();

        if (_trail != null)
        {
            // 残像はダッシュ終了後、Trail Timeで短時間かけて自然に消える。
            _trail.emitting = false;
        }

        // EがCharacter分類(Character分類のTrainingDummyを含む)へ1体以上命中した場合のみ、
        // Qの残りクールダウンを即時0にする(Same Target Lockoutは解除しない)。
        if (_hitCharacterClassification && _qController != null)
        {
            _qController.ResetCooldown();
            Debug.Log("Zelf E: Character分類へ命中したため、Qが即時再使用可能になりました。", this);
        }
    }

    // ダッシュ中に死亡した場合の中断処理。PlayerDeathHandlerが操作系コンポーネントを無効化するため、ここでは復元しない。
    private void AbortDashOnDeath()
    {
        _isDashing = false;

        if (_trail != null)
        {
            _trail.emitting = false;
        }
    }

    private void FaceDashDirection()
    {
        if (_dashDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(_dashDirection, Vector3.up);

        // PlayerMouseFacingの目標回転もpublicメソッドで同じ方向へ更新し、
        // 次フレームに以前の右クリック方向へ不自然に戻らないようにする(ダッシュ後もPlayerMouseFacingは通常どおり動作する)。
        if (_mouseFacing != null)
        {
            _mouseFacing.SetLookDirection(_dashDirection);
        }
    }

    // 指定区間をHit Radiusのカプセル判定でTargetableLayerのみ判定し、経路上の有効なTargetableへダメージを与える。
    private void HitTargetsAlongSegment(Vector3 from, Vector3 to)
    {
        if (_targetableLayer.value == 0)
        {
            return;
        }

        Collider[] overlaps = Physics.OverlapCapsule(from, to, _hitRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            Targetable target = overlap.GetComponentInParent<Targetable>();

            // 同じTargetableには、E 1回につき1回だけダメージを与える。
            if (target == null || _hitTargets.Contains(target))
            {
                continue;
            }

            // 死亡・無効化・破棄されたTargetableにはダメージを与えない(Tower分類にはEダメージを与えてよい)。
            if (!target.isActiveAndEnabled || target.IsDead)
            {
                continue;
            }

            HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
            if (health == null || health.IsDead)
            {
                continue;
            }

            _hitTargets.Add(target);
            ApplyDamage(target, health);
        }
    }

    private void ApplyDamage(Targetable target, HealthController health)
    {
        // E Damage = Base Damage + Current Attack Damage × AD Ratio(通常ダメージ、攻撃者はPlayer)。
        float damage = _baseDamage + _characterStats.CurrentAttackDamage * _adRatio;
        float actualDamage = health.TakeDamage(damage, transform);

        if (actualDamage > 0f)
        {
            // 既存経路: 被弾フラッシュ・赤色の与ダメージ表示・ゼルフPの与ダメージ回復を、命中対象ごとに発生させる。
            // HPバー・死亡処理はHealthControllerのイベント経由で既存どおり発生する。
            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);

            if (_passiveHeal != null)
            {
                _passiveHeal.NotifyDamageDealt(actualDamage, target.Classification);
            }
        }

        // Character分類(Character分類のTrainingDummyを含む)への命中を記録する(Minion / Towerだけの命中ではQをリセットしない)。
        if (target.Classification == TargetClassification.Character ||
            target.Classification == TargetClassification.TrainingDummy)
        {
            _hitCharacterClassification = true;
        }
    }

    // ダッシュ中もPlayerのY座標がGround上の適切な高さを維持するよう、Groundへのレイキャストで高さを求める。
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

    // ダッシュ終了時、Playerが対象のColliderと重なったままの場合は、ダッシュ方向へ少しずつ押し出して補正する。
    private void ResolveOverlapWithTargetables()
    {
        if (_targetableLayer.value == 0)
        {
            return;
        }

        const int maxSteps = 10;
        const float stepDistance = 0.25f;

        for (int i = 0; i < maxSteps && IsOverlappingTargetable(); i++)
        {
            Vector3 position = transform.position + _dashDirection * stepDistance;
            position.y = GetGroundedY(position);
            transform.position = position;
        }
    }

    // PlayerのCharacterControllerと同じカプセル形状で、TargetableLayerのColliderと重なっているかを判定する。
    private bool IsOverlappingTargetable()
    {
        float radius = _characterController.radius + _characterController.skinWidth;
        Vector3 center = transform.position + _characterController.center;
        float halfHeight = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
        Vector3 point1 = center + Vector3.up * halfHeight;
        Vector3 point2 = center - Vector3.up * halfHeight;

        return Physics.OverlapCapsule(point1, point2, radius, _targetableLayer, QueryTriggerInteraction.Ignore).Length > 0;
    }

    // ダッシュ中の青い残像(TrailRenderer)を生成する(外部アセット・VFX Graph不使用)。
    private void CreateTrail()
    {
        GameObject trailObject = new GameObject("Zelf E Dash Trail");
        trailObject.transform.SetParent(transform, false);
        _trail = trailObject.AddComponent<TrailRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

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
