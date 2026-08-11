using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 朧Q。カーソル方向へ貫通する手裏剣を投げ、飛翔終了時に最後に接触した敵ヒーロー/TrainingDummy/ミニオンへ
/// テレポートする。対象が死亡・無効化・破棄された場合は記録した最後の接触地点へ飛ぶ。
/// GAME_DESIGNにQダメージの記載がないため、この実装ではダメージを与えない。
/// 2ストックを個別ではなく順次リチャージし、発動済みの手裏剣が飛翔中でも残りストックを使用できる。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class OboroQController : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField, Min(0.1f)] private float _projectileRange = 7f;
    [SerializeField, Min(0.1f)] private float _projectileSpeed = 14f;
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.35f;
    [SerializeField, Min(0f)] private float _projectileHeight = 0.9f;
    [SerializeField, Min(0f)] private float _teleportStopDistance = 0.7f;

    [Header("Stocks")]
    [SerializeField, Min(1)] private int _maxCharges = 2;
    [SerializeField, Min(0.1f)] private float _stockRechargeTime = 8f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _projectileColor = new Color(0.55f, 0.45f, 0.85f, 1f);
    [SerializeField, Min(0.03f)] private float _projectileSize = 0.24f;

    [Header("Debug (Runtime)")]
    [SerializeField] private int _currentCharges = 2;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private int _activeProjectileCount;

    // HUDが既存スキルと同じReflection経路で次ストックまでの時間を読めるよう、同じフィールド名を使う。
    private double _cooldownEndTime;
    private CharacterController _characterController;
    private CharacterStats _stats;
    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private CrowdControlController _crowdControl;
    private PlayerClickMovement _clickMovement;
    private PlayerMouseFacing _mouseFacing;
    private OboroWController _wController;
    private HealthController _selfHealth;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;
    private readonly List<GameObject> _projectileObjects = new List<GameObject>();
    private int _castSequence;

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;
    public float RemainingCooldown => _remainingCooldown;
    public bool IsQAvailable => _currentCharges > 0;
    public bool IsApproachingQTarget => false;
    public LayerMask GroundLayerMask => _groundLayer;
    public LayerMask TargetableLayerMask => _targetableLayer;

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
        _wController = GetComponent<OboroWController>();
        _selfHealth = GetComponent<HealthController>();
        _mainCamera = Camera.main;
        _groundLayer = OboroCombatUtility.ResolveGroundLayer(_groundLayer);
        _targetableLayer = OboroCombatUtility.ResolveTargetableLayer(_targetableLayer);
        _currentCharges = Mathf.Clamp(_currentCharges, 0, Mathf.Max(1, _maxCharges));
        if (_currentCharges <= 0) _currentCharges = _maxCharges;
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Oboro Q Range Indicator");
    }

    private void OnDisable()
    {
        if (_rangeIndicator != null) _rangeIndicator.HideAll();
        // MatchResultControllerのStopAllCoroutinesで飛翔処理だけ止まり、見た目が残ることを防ぐ。
        foreach (GameObject projectile in _projectileObjects)
        {
            if (projectile != null) Destroy(projectile);
        }
        _projectileObjects.Clear();
        _activeProjectileCount = 0;
    }

    private void Update()
    {
        UpdateCharges();

        if (OboroCombatUtility.IsMatchEnded)
        {
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            return;
        }

        if (_crowdControl == null) _crowdControl = GetComponent<CrowdControlController>();
        UpdateRangeIndicator();

        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.QPressedThisFrame, _inputHub.QReleasedThisFrame))
        {
            TryCast();
        }
    }

    private void UpdateCharges()
    {
        _maxCharges = Mathf.Max(1, _maxCharges);
        _currentCharges = Mathf.Clamp(_currentCharges, 0, _maxCharges);

        if (_currentCharges >= _maxCharges)
        {
            _remainingCooldown = 0f;
            _cooldownEndTime = 0.0;
            return;
        }

        if (_cooldownEndTime <= 0.0)
        {
            _cooldownEndTime = Time.timeAsDouble + _stockRechargeTime;
        }

        while (_currentCharges < _maxCharges && Time.timeAsDouble >= _cooldownEndTime)
        {
            _currentCharges++;
            if (_currentCharges < _maxCharges)
            {
                _cooldownEndTime += _stockRechargeTime;
            }
            else
            {
                _cooldownEndTime = 0.0;
            }
        }

        _remainingCooldown = _currentCharges < _maxCharges
            ? (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble)
            : 0f;
    }

    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.QPressed &&
                       (_abilityLock == null || !_abilityLock.IsLocked) &&
                       (_crowdControl == null || !_crowdControl.IsMovementBlocked) &&
                       (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible || !OboroCombatUtility.TryGetMouseGroundPoint(_inputHub, ref _mainCamera, _groundLayer, out Vector3 groundPoint))
        {
            _rangeIndicator.HideAll();
            return;
        }

        Vector3 direction = OboroCombatUtility.Flatten(groundPoint - transform.position);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            _rangeIndicator.HideAll();
            return;
        }

        _rangeIndicator.ShowDirectionLine(transform.position + Vector3.up * 0.05f,
            direction.normalized, _projectileRange, _projectileColor);
    }

    private void TryCast()
    {
        if (_selfHealth != null && _selfHealth.IsDead) return;
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("朧 Q: 他の行動中のため発動できません。", this);
            return;
        }
        if (_crowdControl != null && _crowdControl.IsMovementBlocked)
        {
            Debug.Log("朧 Q: スタン/スネア中のためテレポートスキルを発動できません。", this);
            return;
        }
        if (_currentCharges <= 0)
        {
            Debug.Log("朧 Q: ストックがありません。", this);
            return;
        }
        if (!OboroCombatUtility.TryGetMouseGroundPoint(_inputHub, ref _mainCamera, _groundLayer, out Vector3 point))
        {
            Debug.Log("朧 Q: マウスカーソルがGroundを指していません。", this);
            return;
        }

        Vector3 direction = OboroCombatUtility.Flatten(point - transform.position);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Debug.Log("朧 Q: 方向を決定できません。", this);
            return;
        }

        _wController?.BreakStealth("Q発動");
        ConsumeCharge();
        _castSequence++;
        StartCoroutine(ThrowRoutine(direction.normalized, _castSequence));
    }

    private void ConsumeCharge()
    {
        bool wasFull = _currentCharges >= _maxCharges;
        _currentCharges = Mathf.Max(0, _currentCharges - 1);
        if (wasFull || _cooldownEndTime <= 0.0)
        {
            _cooldownEndTime = Time.timeAsDouble + _stockRechargeTime;
        }
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
    }

    private IEnumerator ThrowRoutine(Vector3 direction, int sequence)
    {
        _activeProjectileCount++;
        Vector3 castOrigin = transform.position;
        Vector3 projectilePosition = castOrigin + Vector3.up * _projectileHeight;
        GameObject projectile = CreateProjectile(sequence, projectilePosition, direction);
        _projectileObjects.Add(projectile);

        HashSet<Targetable> hitTargets = new HashSet<Targetable>();
        Targetable lastTarget = null;
        Vector3 lastHitPosition = Vector3.zero;
        bool hasHit = false;
        float traveled = 0f;

        while (traveled < _projectileRange)
        {
            if ((_selfHealth != null && _selfHealth.IsDead) || OboroCombatUtility.IsMatchEnded) break;

            float step = Mathf.Min(_projectileSpeed * Time.deltaTime, _projectileRange - traveled);
            Vector3 previous = projectilePosition;
            projectilePosition += direction * step;
            traveled += step;
            if (projectile != null) projectile.transform.position = projectilePosition;

            Collider[] overlaps = Physics.OverlapCapsule(previous, projectilePosition, _hitRadius,
                _targetableLayer, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                Targetable candidate = overlap.GetComponentInParent<Targetable>();
                if (candidate == null || hitTargets.Contains(candidate) || !IsValidHit(candidate)) continue;
                hitTargets.Add(candidate);
                lastTarget = candidate;
                lastHitPosition = candidate.transform.position;
                hasHit = true;
                candidate.PlayHitFlash();
                Debug.Log($"朧 Q: {candidate.name}へ接触し、最後の対象を更新しました。", this);
            }

            yield return null;
        }

        if (projectile != null)
        {
            _projectileObjects.Remove(projectile);
            Destroy(projectile);
        }
        _activeProjectileCount = Mathf.Max(0, _activeProjectileCount - 1);

        if (!hasHit || (_selfHealth != null && _selfHealth.IsDead) || OboroCombatUtility.IsMatchEnded) yield break;

        Vector3 destination;
        if (OboroCombatUtility.IsAlive(lastTarget))
        {
            Vector3 targetPosition = lastTarget.transform.position;
            Vector3 away = OboroCombatUtility.Flatten(transform.position - targetPosition);
            if (away.sqrMagnitude <= 0.0001f) away = -direction;
            destination = targetPosition + away.normalized * _teleportStopDistance;
        }
        else
        {
            // 対象が消えた場合は、接触時に保存した最後の地点へ飛ぶ。
            destination = lastHitPosition;
        }

        Vector3 before = transform.position;
        if (_clickMovement != null) _clickMovement.StopMovement();
        OboroCombatUtility.Teleport(transform, _characterController, destination, _groundLayer);
        Vector3 teleportDirection = OboroCombatUtility.Flatten(transform.position - before);
        if (teleportDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(teleportDirection.normalized, Vector3.up);
            _mouseFacing?.SetLookDirection(teleportDirection);
        }
        Debug.Log(lastTarget != null
            ? $"朧 Q: 最後に接触した{lastTarget.name}へテレポートしました。"
            : "朧 Q: 消失した対象の最後の地点へテレポートしました。", this);
    }

    private bool IsValidHit(Targetable target)
    {
        if (!OboroCombatUtility.IsEnemy(transform, target)) return false;
        // GAME_DESIGNは敵/ミニオンを対象とするため、Tower分類は明示的に除外する。
        if (target.Classification == TargetClassification.Tower) return false;
        return target.Classification == TargetClassification.Character ||
               target.Classification == TargetClassification.TrainingDummy ||
               target.Classification == TargetClassification.Minion;
    }

    private GameObject CreateProjectile(int sequence, Vector3 position, Vector3 direction)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = $"Oboro Q Shuriken #{sequence}";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * _projectileSize;
        projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        Collider generatedCollider = projectile.GetComponent<Collider>();
        if (generatedCollider != null) Destroy(generatedCollider);

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null) renderer.material = OboroCombatUtility.CreateUnlitMaterial(_projectileColor);

        TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
        trail.time = 0.2f;
        trail.startWidth = _projectileSize * 0.6f;
        trail.endWidth = 0f;
        trail.material = OboroCombatUtility.CreateUnlitMaterial(_projectileColor);
        trail.startColor = _projectileColor;
        trail.endColor = new Color(_projectileColor.r, _projectileColor.g, _projectileColor.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        return projectile;
    }

    /// <summary>MatchResultControllerの共通SendMessageと通常攻撃の競合防止用。Qは自動接近しないためno-op。</summary>
    public void CancelPendingApproach()
    {
    }
}
