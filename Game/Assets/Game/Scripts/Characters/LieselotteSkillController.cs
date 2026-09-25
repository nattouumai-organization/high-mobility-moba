using System.Collections.Generic;
using UnityEngine;

/// <summary>リーゼロッテのP/Q/W/E/R。数値は専用SkillDataから取得する。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class LieselotteSkillController : MonoBehaviour
{
    private const string RLock = "LieselotteRDash";

    [SerializeField] private LieselotteSkillData _skillData;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    private CharacterStats _stats;
    private HealthController _health;
    private CharacterController _controller;
    private PlayerInputHub _input;
    private AbilityLockController _lock;
    private CrowdControlController _cc;
    private HeroSkillUpgrades _upgrades;
    private LieselotteSkillVisuals _visuals;
    private SkillRangeIndicator _qPreview;
    private SkillRangeIndicator _ePreview;
    private SkillRangeIndicator _rPreview;
    private LieselotteStatSteal _steal;
    private Camera _camera;
    private Targetable _stackTarget;
    private int _stacks;
    private float _stackExpiry;
    private float _empowerExpiry;
    private float _wExpiry;
    private float _wMoveBonus;
    private float _wAttackBonus;
    private bool _isWActive;
    private bool _isRActive;
    private Vector3 _rDirection;
    private float _rTraveled;
    private readonly List<Pool> _pools = new List<Pool>();
    private readonly HashSet<Targetable> _eHit = new HashSet<Targetable>();
    private int _sequence;
    private int _eSequence;
    private double _qCooldownEndTime;
    private double _eCooldownEndTime;
    private double _rCooldownEndTime;

    private sealed class Pool
    {
        public Vector3 Position;
        public float EndTime;
    }

    public int PassiveStacks => Time.time < _stackExpiry ? _stacks : 0;
    public bool IsRActive => _isRActive;
    public bool IsWActive => Time.time < _wExpiry;
    public bool IsStealing => _steal != null && _steal.IsActive;
    public float QRemainingCooldown => Remaining(_qCooldownEndTime);
    public float ERemainingCooldown => Remaining(_eCooldownEndTime);
    public float RRemainingCooldown => Remaining(_rCooldownEndTime);
    private static float Remaining(double end) => (float)System.Math.Max(0.0, end - Time.timeAsDouble);

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputHub>();
        _lock = GetComponent<AbilityLockController>();
        _cc = GetComponent<CrowdControlController>();
        _upgrades = GetComponent<HeroSkillUpgrades>();
        _visuals = GetComponent<LieselotteSkillVisuals>();
        if (_visuals == null) _visuals = gameObject.AddComponent<LieselotteSkillVisuals>();
        _steal = GetComponent<LieselotteStatSteal>();
        if (_steal == null) _steal = gameObject.AddComponent<LieselotteStatSteal>();
        _groundLayer = OboroCombatUtility.ResolveGroundLayer(_groundLayer);
        _targetableLayer = OboroCombatUtility.ResolveTargetableLayer(_targetableLayer);
        _camera = Camera.main;
        if (_health != null) _health.Died += OnDied;
        if (_skillData == null) Debug.LogError("LieselotteSkillData is required.", this);
    }

    private void Update()
    {
        if (_skillData == null) return;
        if (_cc == null) _cc = GetComponent<CrowdControlController>();
        if (_upgrades == null) _upgrades = GetComponent<HeroSkillUpgrades>();
        if (Time.time >= _wExpiry) EndW();
        UpdatePools();
        _visuals.SetStealActive(_steal != null && _steal.IsActive);
        if (OboroCombatUtility.IsMatchEnded || (_health != null && _health.IsDead))
        {
            HidePreviews();
            StopR();
            return;
        }
        if (_isRActive)
        {
            HidePreviews();
            if (_cc != null && _cc.IsMovementBlocked) StopR();
            else StepR();
            return;
        }
        if (_input == null || (_lock != null && _lock.IsLocked) ||
            (_cc != null && _cc.IsStunned))
        {
            HidePreviews();
            return;
        }
        UpdatePreviews();
        if (_input.QReleasedThisFrame) CastQ();
        if (_input.EReleasedThisFrame) CastE();
        if (_input.RReleasedThisFrame) CastR();
        // WはPの3打目で自動発動するパッシブ効果。
    }

    private void UpdatePreviews()
    {
        if (_input.QPressed && QRemainingCooldown <= 0f)
        {
            if (_qPreview == null) _qPreview = SkillRangeIndicator.Create(transform, "Lieselotte Q Range");
            _qPreview.ShowCircle(_skillData.QRange, new Color(1f, 0.45f, 0.55f), 0.05f);
        }
        else if (_qPreview != null) _qPreview.HideAll();

        UpdateDirectionPreview(ref _ePreview, _input.EPressed && ERemainingCooldown <= 0f,
            _skillData.EDistance, "Lieselotte E Range");
        UpdateDirectionPreview(ref _rPreview, _input.RPressed && RRemainingCooldown <= 0f,
            _skillData.RDistance, "Lieselotte R Range");
    }

    private void UpdateDirectionPreview(ref SkillRangeIndicator preview, bool visible,
        float distance, string name)
    {
        if (!visible || !TryAimPoint(out Vector3 point))
        {
            if (preview != null) preview.HideAll();
            return;
        }
        if (preview == null) preview = SkillRangeIndicator.Create(transform, name);
        Vector3 direction = OboroCombatUtility.Flatten(point - transform.position).normalized;
        preview.ShowDirectionLine(transform.position + Vector3.up * 0.05f, direction,
            distance, new Color(1f, 0.45f, 0.55f));
    }

    private void HidePreviews()
    {
        if (_qPreview != null) _qPreview.HideAll();
        if (_ePreview != null) _ePreview.HideAll();
        if (_rPreview != null) _rPreview.HideAll();
    }

    /// <summary>通常攻撃のダメージ計算前に呼ぶ。3打目のみ追加ダメージを返す。</summary>
    public float PassiveBonusFor(Targetable target)
    {
        if (_skillData == null || !OboroCombatUtility.IsEnemyChampion(transform, target)) return 0f;
        int next = target == _stackTarget && Time.time < _stackExpiry ? _stacks + 1 : 1;
        return next >= 3 ? _skillData.PassiveBaseDamage +
            _stats.CurrentAttackDamage * _skillData.PassiveAdRatio : 0f;
    }

    /// <summary>実際にHPを減らした通常攻撃だけをスタックへ数える。</summary>
    public void NotifyBasicAttackHit(Targetable target)
    {
        if (_skillData == null || !OboroCombatUtility.IsEnemyChampion(transform, target)) return;
        _stacks = target == _stackTarget && Time.time < _stackExpiry ? _stacks + 1 : 1;
        _stackTarget = target;
        _stackExpiry = Time.time + _skillData.PassiveStackSeconds;
        if (_stacks < 3) return;
        _stacks = 0;
        _stackTarget = null;
        _empowerExpiry = Time.time + _skillData.PassiveEmpowerSeconds;
        StartW();
        _visuals.ShowPassive(target.transform);
    }

    private bool IsQTarget(Targetable target)
    {
        return OboroCombatUtility.IsEnemy(transform, target, false) &&
            (target.Classification == TargetClassification.Character ||
             target.Classification == TargetClassification.Minion);
    }

    private void CastQ()
    {
        if (Time.timeAsDouble < _qCooldownEndTime) return;
        if (!OboroCombatUtility.TryGetMouseTarget(_input, ref _camera, _targetableLayer,
                out Targetable target) || !IsQTarget(target))
            target = GetComponent<PlayerTargetSelector>()?.CurrentTarget;
        if (!IsQTarget(target)) return;
        SkillApproachController.For(gameObject).CastOrApproach(target, _skillData.QRange,
            () => CastQTarget(target));
    }

    private void CastQTarget(Targetable target)
    {
        if (_skillData == null || Time.timeAsDouble < _qCooldownEndTime || !IsQTarget(target) ||
            (_health != null && _health.IsDead) || (_cc != null && _cc.IsStunned)) return;
        Vector3 delta = OboroCombatUtility.Flatten(target.GetClosestPoint(transform.position) -
            transform.position);
        if (delta.sqrMagnitude > _skillData.QRange * _skillData.QRange) return;
        HealthController targetHealth = OboroCombatUtility.GetHealth(target);
        if (targetHealth == null) return;
        _qCooldownEndTime = Time.timeAsDouble + _skillData.QCooldown;
        _eCooldownEndTime = System.Math.Max(Time.timeAsDouble,
            _eCooldownEndTime - _skillData.ECooldownReductionOnQ);
        _sequence++;
        float raw = Damage(_skillData.QBaseDamage, _skillData.QAdRatio, HeroSkillSlot.Q);
        float actual = targetHealth.TakeDamage(raw, transform, DamageType.Normal,
            sourceId: $"LieselotteQ#{_sequence}");
        _visuals.ShowQ(target.transform);
        if (actual <= 0f) return;
        float healPercent = target.Classification == TargetClassification.Minion
            ? _skillData.QMinionHealPercent : _skillData.QHeroHealPercent;
        if (Time.time < _empowerExpiry)
        {
            healPercent *= _skillData.QEmpoweredHealMultiplier;
            _empowerExpiry = 0f;
        }
        _health?.Heal(actual * healPercent / 100f);
        target.PlayHitFlash();
        CombatTextManager.ShowDamageDealt(target.transform.position, actual);
    }

    private void StartW()
    {
        EndW();
        _wMoveBonus = _stats.BaseMoveSpeed * _skillData.WMoveSpeedPercent / 100f;
        _wAttackBonus = _skillData.WAttackSpeedPercent;
        _stats.AddMoveSpeedBonus(_wMoveBonus);
        _stats.AddAttackSpeedPercentBonus(_wAttackBonus);
        _wExpiry = Time.time + _skillData.WDuration;
        _isWActive = true;
        _visuals.SetWActive(_skillData.WDuration);
    }

    private void EndW()
    {
        if (_wMoveBonus == 0f && _wAttackBonus == 0f) return;
        _stats.RemoveMoveSpeedBonus(_wMoveBonus);
        _stats.RemoveAttackSpeedPercentBonus(_wAttackBonus);
        _wMoveBonus = _wAttackBonus = 0f;
        _wExpiry = 0f;
        _isWActive = false;
    }

    private void CastE()
    {
        if (Time.timeAsDouble < _eCooldownEndTime ||
            (_cc != null && _cc.IsMovementBlocked) ||
            !TryAimPoint(out Vector3 point)) return;
        SkillApproachController.For(gameObject).CastOrApproach(point, _skillData.EDistance,
            () => CastEAt(point));
    }

    private void CastEAt(Vector3 point)
    {
        if (Time.timeAsDouble < _eCooldownEndTime ||
            (_cc != null && _cc.IsMovementBlocked) ||
            (_health != null && _health.IsDead) || (_lock != null && _lock.IsLocked)) return;
        Vector3 direction = OboroCombatUtility.Flatten(point - transform.position);
        if (direction.sqrMagnitude < 0.01f) return;
        direction.Normalize();
        _eCooldownEndTime = Time.timeAsDouble + _skillData.ECooldown;
        _sequence++;
        _eSequence = _sequence;
        Vector3 from = transform.position;
        Vector3 to = from + direction * _skillData.EDistance;
        OboroCombatUtility.Teleport(transform, _controller, to, _groundLayer);
        MovementSkillSignal.Report(gameObject);
        for (float distance = 0f; distance <= _skillData.EDistance; distance +=
            _skillData.EPoolRadius * 1.5f)
        {
            Vector3 position = from + direction * distance;
            if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down,
                out RaycastHit ground, 30f, _groundLayer, QueryTriggerInteraction.Ignore))
                position.y = ground.point.y;
            _pools.Add(new Pool { Position = position,
                EndTime = Time.time + _skillData.EPoolDuration });
            _visuals.ShowBloodPool(position, _skillData.EPoolRadius,
                _skillData.EPoolDuration);
        }
        _eHit.Clear();
        UpdatePools();
    }

    private void UpdatePools()
    {
        for (int i = _pools.Count - 1; i >= 0; i--)
        {
            Pool pool = _pools[i];
            if (Time.time >= pool.EndTime)
            {
                _pools.RemoveAt(i);
                continue;
            }
            foreach (Collider collider in Physics.OverlapSphere(pool.Position + Vector3.up,
                _skillData.EPoolRadius + 0.7f, _targetableLayer, QueryTriggerInteraction.Ignore))
            {
                Targetable target = collider.GetComponentInParent<Targetable>();
                if (!IsQTarget(target) || !_eHit.Add(target)) continue;
                Vector3 flat = OboroCombatUtility.Flatten(target.GetClosestPoint(pool.Position) -
                    pool.Position);
                if (flat.sqrMagnitude > _skillData.EPoolRadius * _skillData.EPoolRadius)
                {
                    _eHit.Remove(target);
                    continue;
                }
                CrowdControlController cc = target.GetComponent<CrowdControlController>();
                if (cc == null) cc = target.gameObject.AddComponent<CrowdControlController>();
                cc.ApplySlow(_skillData.ESlowPercent, _skillData.ESlowDuration);
                HealthController health = OboroCombatUtility.GetHealth(target);
                if (health == null) continue;
                float actual = health.TakeDamage(Damage(_skillData.EBaseDamage,
                    _skillData.EAdRatio, HeroSkillSlot.E), transform, DamageType.Normal,
                    sourceId: $"LieselotteE#{_eSequence}");
                if (actual > 0f) CombatTextManager.ShowDamageDealt(target.transform.position, actual);
                target.PlayHitFlash();
            }
        }
        if (_pools.Count == 0) _eHit.Clear();
    }

    private void CastR()
    {
        if (Time.timeAsDouble < _rCooldownEndTime ||
            (_cc != null && _cc.IsMovementBlocked) ||
            !TryAimPoint(out Vector3 point)) return;
        SkillApproachController.For(gameObject).CastOrApproach(point, _skillData.RDistance,
            () => CastRAt(point));
    }

    private void CastRAt(Vector3 point)
    {
        if (Time.timeAsDouble < _rCooldownEndTime ||
            (_cc != null && _cc.IsMovementBlocked) ||
            (_health != null && _health.IsDead) || (_lock != null && _lock.IsLocked)) return;
        _rDirection = OboroCombatUtility.Flatten(point - transform.position);
        if (_rDirection.sqrMagnitude < 0.01f) return;
        _rDirection.Normalize();
        _rCooldownEndTime = Time.timeAsDouble + _skillData.RCooldown;
        _isRActive = true;
        MovementSkillSignal.Report(gameObject);
        _rTraveled = 0f;
        GetComponent<PlayerClickMovement>()?.StopMovement();
        _lock?.AddLock(RLock);
    }

    private void StepR()
    {
        Vector3 before = transform.position;
        float step = Mathf.Min(_skillData.RDistance - _rTraveled,
            _skillData.RDistance / 0.3f * Time.deltaTime);
        if (_controller != null && _controller.enabled) _controller.Move(_rDirection * step);
        else transform.position += _rDirection * step;
        Vector3 after = transform.position;
        _rTraveled += OboroCombatUtility.Flatten(after - before).magnitude;
        _visuals.ShowRDash(before, after);
        foreach (Collider collider in Physics.OverlapSphere(after + Vector3.up,
            _skillData.RHitRadius + 0.7f, _targetableLayer, QueryTriggerInteraction.Ignore))
        {
            Targetable target = collider.GetComponentInParent<Targetable>();
            if (!OboroCombatUtility.IsEnemyChampion(transform, target)) continue;
            Vector3 flat = OboroCombatUtility.Flatten(target.GetClosestPoint(after) - after);
            if (flat.sqrMagnitude > _skillData.RHitRadius * _skillData.RHitRadius) continue;
            StopR();
            CommonDController d = target.GetComponent<CommonDController>();
            if (d != null && d.TryBlockHardCC(transform)) return;
            _visuals.ShowQ(target.transform);
            _steal.Apply(target.GetComponent<CharacterStats>(),
                OboroCombatUtility.GetHealth(target), _skillData.RStatStealPercent,
                _skillData.RMoveSpeedStealPercent, _skillData.RDuration);
            return;
        }
        if (_rTraveled >= _skillData.RDistance - 0.01f || step <= 0f) StopR();
    }

    private bool TryAimPoint(out Vector3 point)
    {
        return OboroCombatUtility.TryGetMouseGroundPoint(_input, ref _camera, _groundLayer,
            out point) && OboroCombatUtility.Flatten(point - transform.position).sqrMagnitude >= 0.01f;
    }

    private float Damage(float baseDamage, float ratio, HeroSkillSlot slot)
    {
        int rank = _upgrades != null ? _upgrades.GetRank(slot) : 0;
        return (baseDamage + _stats.CurrentAttackDamage * ratio) *
            (1f + rank * _skillData.RankDamageStep);
    }

    private void StopR()
    {
        if (!_isRActive) return;
        _isRActive = false;
        _lock?.RemoveLock(RLock);
    }

    private void OnDied()
    {
        HidePreviews();
        float remaining = RRemainingCooldown;
        if (remaining > 0f) _rCooldownEndTime = Time.timeAsDouble + remaining * 0.4f;
        StopR();
        EndW();
        _steal?.End();
        _visuals?.HideAll();
        _pools.Clear();
        _eHit.Clear();
        _stacks = 0;
        _stackTarget = null;
        _empowerExpiry = 0f;
    }

    private void OnDisable()
    {
        HidePreviews();
        StopR();
        EndW();
        _steal?.End();
        _visuals?.HideAll();
        _pools.Clear();
        _eHit.Clear();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
        OnDisable();
    }
}
