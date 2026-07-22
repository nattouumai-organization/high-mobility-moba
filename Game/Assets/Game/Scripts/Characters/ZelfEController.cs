using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class ZelfEController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerClickMovement _clickMovement;
    [SerializeField] private ZelfQController _qController;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float _dashDistance = 4f;
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.18f;
    [SerializeField, Min(0f)] private float _hitRadius = 0.6f;
    [SerializeField, Min(0f)] private float _minCastDistance = 0.1f;

    [Header("Post-Dash Wave (Viego W style)")]
    [SerializeField, Min(0f)] private float _waveDistance = 3f;
    [SerializeField, Min(1f)] private float _waveSpeed = 10f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _baseDamage = 20f;
    [SerializeField, Range(0f, 2f)] private float _adRatio = 0.5f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 8f;

    [Header("Layers")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _trailColor = new Color(0.25f, 0.6f, 1f, 0.85f);
    [SerializeField, Min(0.05f)] private float _trailTime = 0.25f;
    [SerializeField, Min(0.01f)] private float _trailWidth = 0.45f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isDashing;
    [SerializeField] private float _remainingCooldown;

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
    private bool _characterControllerWasEnabled;
    // ダッシュがAbilityLockControllerへロックを追加済みか(二重解除・未解除の防止)。
    private bool _lockAdded;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;
    private TrailRenderer _trail;
    private Material _trailMaterial;
    private Coroutine _waveCoroutine;

    public bool IsDashing => _isDashing;

    private void Awake()
    {
        _characterController = _characterController != null ? _characterController : GetComponent<CharacterController>();
        _characterStats = _characterStats != null ? _characterStats : GetComponent<CharacterStats>();
        _clickMovement = _clickMovement != null ? _clickMovement : GetComponent<PlayerClickMovement>();
        _qController = _qController != null ? _qController : GetComponent<ZelfQController>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _passiveHeal = GetComponent<ZelfPassiveHeal>();
        _selfHealth = GetComponent<HealthController>();
        _mainCamera = Camera.main;


        if (_qController == null)
        {
            Debug.LogWarning("Zelf E: ZelfQControllerが見つかりません。EによるQのCD即時リセット機能が動作しません。", this);
        }

        if (_qController != null)
        {
            if (_groundLayer.value == 0) _groundLayer = _qController.GroundLayerMask;
            if (_targetableLayer.value == 0) _targetableLayer = _qController.TargetableLayerMask;
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
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        if (_isDashing)
        {
            if (_selfHealth != null && _selfHealth.IsDead)
            {
                AbortDashOnDeath();
                return;
            }
            // ダッシュ中のE再入力は受け付けない(診断用にログを出す)。
            if (_inputHub != null && _inputHub.EPressedThisFrame)
            {
                Debug.Log("ゼルフ E: ダッシュ中のため発動できません。", this);
            }
            UpdateDash();
            return;
        }

        if (_inputHub != null && _inputHub.EPressedThisFrame)
        {
            HandleEPressed();
        }
    }

    private void HandleEPressed()
    {
        // 他の行動ロック中(W発動中・死亡中など)は発動できない。
        // クールダウン判定より先に確認し、ロックが原因のときは必ずこのログを出す。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("ゼルフ E: 他の行動中のため発動できません。", this);
            return;
        }
        if (Time.time < _cooldownEndTime)
        {
            Debug.Log("ゼルフ E: クールダウン中です。", this);
            return;
        }
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            Debug.Log("ゼルフ E: 死亡中のため発動できません。", this);
            return;
        }

        if (!TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Debug.Log("ゼルフ E: マウスカーソルがGroundを指していないため発動しません。", this);
            return;
        }

        Vector3 direction = groundPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < _minCastDistance * _minCastDistance)
        {
            Debug.Log("ゼルフ E: マウス地点が近すぎるため発動しません。", this);
            return;
        }

        StartDash(direction.normalized);
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

    private void StartDash(Vector3 direction)
    {
        _dashDirection = direction;
        _remainingDashDistance = _dashDistance;
        _dashSpeed = _dashDistance / _dashDuration;
        _hitTargets.Clear();
        _hitCharacterClassification = false;
        _isDashing = true;
        _cooldownEndTime = Time.time + _cooldown;

        if (_waveCoroutine != null) { StopCoroutine(_waveCoroutine); _waveCoroutine = null; }

        if (_clickMovement != null)
        {
            _clickMovement.StopMovement();
            _clickMovementWasEnabled = _clickMovement.enabled;
            _clickMovement.enabled = false;
        }
        // ダッシュ中は通常攻撃・Q・W・Rを含む全スキルの入力をロックする
        // (各コントローラーがIsLockedを確認する。コンポーネント自体は無効化しない)。
        if (_qController != null) _qController.CancelPendingApproach();
        if (_abilityLock != null && !_lockAdded)
        {
            _abilityLock.AddLock(AbilityLockController.ReasonZelfEDash);
            _lockAdded = true;
        }

        _characterControllerWasEnabled = _characterController.enabled;
        _characterController.enabled = false;
        FaceDashDirection();

        if (_trail != null) { _trail.Clear(); _trail.emitting = true; }
        Debug.Log("Zelf E: ダッシュを発動しました。", this);
    }

    private void UpdateDash()
    {
        float step = Mathf.Min(_dashSpeed * Time.deltaTime, _remainingDashDistance);
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + _dashDirection * step;
        nextPosition.y = GetGroundedY(nextPosition);
        transform.position = nextPosition;
        _remainingDashDistance -= step;
        HitTargetsAlongSegment(previousPosition, nextPosition);
        if (_remainingDashDistance <= 0.0001f) EndDash();
    }

    private void EndDash()
    {
        ResolveOverlapWithTargetables();
        _characterController.enabled = _characterControllerWasEnabled;
        if (_clickMovement != null) _clickMovement.enabled = _clickMovementWasEnabled;
        // ダッシュが追加したロックを解除する。
        RemoveDashLock();

        _isDashing = false;
        FaceDashDirection();
        if (_trail != null) _trail.emitting = false;
        _waveCoroutine = StartCoroutine(PostDashWave(transform.position));
    }

    // ダッシュ終了後、ヴィエゴWのように前方へウェーブを飛ばしWave Distance先まで命中判定。
    private IEnumerator PostDashWave(Vector3 startPosition)
    {
        if (_waveDistance > 0f && _waveSpeed > 0f)
        {
            float traveledDistance = 0f;
            Vector3 wavePosition = startPosition;
            while (traveledDistance < _waveDistance)
            {
                float step = Mathf.Min(_waveSpeed * Time.deltaTime, _waveDistance - traveledDistance);
                Vector3 prevPos = wavePosition;
                wavePosition += _dashDirection * step;
                traveledDistance += step;
                HitTargetsAlongSegment(prevPos, wavePosition);
                yield return null;
            }
        }
        // Qリセット・ロック解除はApplyDamage()で命中瞬間に実行済み。
        _waveCoroutine = null;
    }

    private void AbortDashOnDeath()
    {
        _isDashing = false;
        if (_waveCoroutine != null) { StopCoroutine(_waveCoroutine); _waveCoroutine = null; }
        if (_trail != null) _trail.emitting = false;

        // 死亡でダッシュが中断された場合もロックを解除する。
        // (死亡中の行動禁止はPlayerDeathHandlerが追加する死亡ロックが担当する。
        //  移動・CharacterControllerはPlayerDeathHandlerが管理するため触らない)
        RemoveDashLock();
    }

    // ダッシュが追加したロックを解除する(未追加なら何もしない)。
    private void RemoveDashLock()
    {
        if (_abilityLock != null && _lockAdded)
        {
            _abilityLock.RemoveLock(AbilityLockController.ReasonZelfEDash);
            _lockAdded = false;
        }
    }

    private void FaceDashDirection()
    {
        if (_dashDirection.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(_dashDirection, Vector3.up);
        if (_mouseFacing != null) _mouseFacing.SetLookDirection(_dashDirection);
    }

    private void HitTargetsAlongSegment(Vector3 from, Vector3 to)
    {
        if (_targetableLayer.value == 0) return;
        Collider[] overlaps = Physics.OverlapCapsule(from, to, _hitRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            Targetable target = overlap.GetComponentInParent<Targetable>();
            if (target == null || _hitTargets.Contains(target)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;
            HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
            if (health == null || health.IsDead) continue;
            _hitTargets.Add(target);
            ApplyDamage(target, health);
        }
    }

    private void ApplyDamage(Targetable target, HealthController health)
    {
        float damage = _baseDamage + _characterStats.CurrentAttackDamage * _adRatio;
        float actualDamage = health.TakeDamage(damage, transform);
        if (actualDamage > 0f)
        {
            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
            if (_passiveHeal != null) _passiveHeal.NotifyDamageDealt(actualDamage, target.Classification);
        }
        if (target.Classification == TargetClassification.Character ||
            target.Classification == TargetClassification.TrainingDummy)
        {
            _hitCharacterClassification = true;
            // Character/TrainingDummyに命中した瞬間、QのCDを即時リセットし、
            // その対象のSame Target Lockoutを解除する。
            // ウェーブ終了を待たず即座に実行するため、E後に即座Qを再発動できる。
            if (_qController != null)
            {
                _qController.ResetCooldown();
                _qController.ClearLockout(target);
                Debug.Log("Zelf E: Character分類へ命中！QCDリセットと同一対象ロックを解除しました。", this);
            }
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
        GameObject trailObject = new GameObject("Zelf E Dash Trail");
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
