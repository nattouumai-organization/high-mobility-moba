using System.Collections.Generic;
using UnityEngine;

/// <summary>Prototype Rines P/Q/W/E/R. Only the selected Rines prefab owns this component.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class RinesSkillController : MonoBehaviour
{
    [SerializeField] private RinesSkillData _skillData;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    private CharacterStats _stats;
    private HealthController _health;
    private CrowdControlController _cc;
    private AbilityLockController _lock;
    private PlayerInputHub _input;
    private HeroSkillUpgrades _upgrades;
    private RinesSkillVisuals _visuals;
    private SkillRangeIndicator _qPreview;
    private SkillRangeIndicator _rPreview;
    private Camera _camera;
    private double _qCooldownEndTime;
    private double _wCooldownEndTime;
    private double _eCooldownEndTime;
    private double _rCooldownEndTime;
    private bool _qPending;
    private Vector3 _qPoint;
    private double _qStarted;
    private GameObject _qMarker;
    private bool _rPending;
    private Vector3 _rPoint;
    private double _rStarted;
    private GameObject _rMarker;
    private bool _isEActive;
    private bool _eHit;
    private double _eStarted;
    private double _eEnds;
    private float _eSpeedBonus;
    private int _sequence;

    public bool IsEActive => _isEActive;
    public float QRemainingCooldown => Remaining(_qCooldownEndTime);
    public float WRemainingCooldown => Remaining(_wCooldownEndTime);
    public float ERemainingCooldown => Remaining(_eCooldownEndTime);
    public float RRemainingCooldown => Remaining(_rCooldownEndTime);

    private static float Remaining(double end) => (float)System.Math.Max(0.0, end - Time.timeAsDouble);

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        _cc = GetComponent<CrowdControlController>();
        _lock = GetComponent<AbilityLockController>();
        _input = GetComponent<PlayerInputHub>();
        _upgrades = GetComponent<HeroSkillUpgrades>();
        _visuals = GetComponent<RinesSkillVisuals>();
        if (_visuals == null) _visuals = gameObject.AddComponent<RinesSkillVisuals>();
        _groundLayer = OboroCombatUtility.ResolveGroundLayer(_groundLayer);
        _targetableLayer = OboroCombatUtility.ResolveTargetableLayer(_targetableLayer);
        _camera = Camera.main;
        if (_skillData == null) Debug.LogError("RinesSkillController requires RinesSkillData.", this);
        if (_health != null) _health.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
        CancelPending();
        EndE();
        if (_visuals != null) _visuals.HideAll();
    }

    private void OnDisable()
    {
        HidePreviews();
        CancelPending();
        EndE();
        if (_visuals != null) _visuals.HideAll();
    }

    private void Update()
    {
        if (_skillData == null) return;
        if (_cc == null) _cc = GetComponent<CrowdControlController>();
        if (_upgrades == null) _upgrades = GetComponent<HeroSkillUpgrades>();

        if (OboroCombatUtility.IsMatchEnded || (_health != null && _health.IsDead) ||
            (_cc != null && _cc.IsStunned))
        {
            HidePreviews();
            CancelPending();
            EndE();
            return;
        }

        double now = Time.timeAsDouble;
        if (_qPending && now - _qStarted > _skillData.QWindow) CancelQ();
        if (_rPending && now - _rStarted >= _skillData.RWarning) ResolveR();

        if (_isEActive)
        {
            if ((_cc != null && _cc.IsMovementBlocked) || (_input != null && _input.FPressedThisFrame))
                EndE();
            else
            {
                if (!_eHit && now - _eStarted <= _skillData.EDuration) ScanE();
                if (now >= _eEnds) EndE();
            }
        }

        if (_input == null || (_lock != null && _lock.IsLocked))
        {
            HidePreviews();
            return;
        }
        UpdatePreviews();
        if (_input.QReleasedThisFrame) CastQ();
        if (_input.WReleasedThisFrame) CastW();
        if (_input.EReleasedThisFrame) CastE();
        if (_input.RReleasedThisFrame) CastR();
    }

    private void UpdatePreviews()
    {
        if (_input.QPressed && !_isEActive && (_qPending || QRemainingCooldown <= 0f))
        {
            if (_qPreview == null) _qPreview = SkillRangeIndicator.Create(transform, "Rines Q Range");
            _qPreview.ShowCircle(_skillData.QRange, new Color(0.45f, 0.85f, 1f), 0.05f);
            if (TryAimPoint(out Vector3 point))
                _qPreview.ShowPointMarker(point, _skillData.QRadius, new Color(0.45f, 0.85f, 1f));
            else _qPreview.HidePointMarker();
        }
        else if (_qPreview != null) _qPreview.HideAll();

        if (_input.RPressed && !_isEActive && !_rPending && RRemainingCooldown <= 0f)
        {
            if (_rPreview == null) _rPreview = SkillRangeIndicator.Create(transform, "Rines R Range");
            _rPreview.ShowCircle(_skillData.RRange, new Color(1f, 0.7f, 0.25f), 0.05f);
            if (TryAimPoint(out Vector3 point))
                _rPreview.ShowPointMarker(point, _skillData.RRadius, new Color(1f, 0.7f, 0.25f));
            else _rPreview.HidePointMarker();
        }
        else if (_rPreview != null) _rPreview.HideAll();
    }

    private void HidePreviews()
    {
        if (_qPreview != null) _qPreview.HideAll();
        if (_rPreview != null) _rPreview.HideAll();
    }

    private void OnDied()
    {
        float remaining = RRemainingCooldown;
        if (remaining > 0f) _rCooldownEndTime = Time.timeAsDouble + remaining * 0.4f;
        CancelPending();
        EndE();
        if (_visuals != null) _visuals.HideAll();
    }

    private void CancelPending()
    {
        CancelQ();
        CancelR();
    }

    private void CancelQ()
    {
        _qPending = false;
        ClearMarker(ref _qMarker);
    }

    private void CancelR()
    {
        _rPending = false;
        ClearMarker(ref _rMarker);
    }

    private bool TryAimPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (!OboroCombatUtility.TryGetMouseGroundPoint(_input, ref _camera, _groundLayer, out point))
            return false;
        return true;
    }

    private void CastQ()
    {
        if (_isEActive) return;
        double now = Time.timeAsDouble;
        if (_qPending)
        {
            if (!RinesRules.CanDetonateQ(now - _qStarted, _skillData.QDelay, _skillData.QWindow)) return;
            Vector3 point = _qPoint;
            CancelQ();
            _sequence++;
            Strike(point, _skillData.QRadius, _skillData.QBaseDamage, _skillData.QAdRatio,
                HeroSkillSlot.Q, HardCcType.Snare, _skillData.QSnare, "RinesQ");
            return;
        }
        if (now < _qCooldownEndTime || !TryAimPoint(out Vector3 castPoint)) return;
        SkillApproachController.For(gameObject).CastOrApproach(castPoint, _skillData.QRange,
            () => PlaceQ(castPoint));
    }

    private void PlaceQ(Vector3 castPoint)
    {
        if (_skillData == null || _isEActive || Time.timeAsDouble < _qCooldownEndTime || _qPending ||
            (_health != null && _health.IsDead) || (_cc != null && _cc.IsStunned)) return;
        _qCooldownEndTime = Time.timeAsDouble + _skillData.QCooldown;
        _qPoint = castPoint;
        _qStarted = Time.timeAsDouble;
        _qPending = true;
        _qMarker = MakeMarker("Rines Q warning", castPoint, _skillData.QRadius);
    }

    private void CastW()
    {
        if (_isEActive || Time.timeAsDouble < _wCooldownEndTime) return;
        _wCooldownEndTime = Time.timeAsDouble + _skillData.WCooldown;
        _sequence++;
        _visuals?.ShowW(_skillData.WRadius);
        foreach (Targetable target in TargetsAt(transform.position, _skillData.WRadius, true))
        {
            CrowdControlController targetCc = target.GetComponent<CrowdControlController>();
            if (targetCc == null) targetCc = target.gameObject.AddComponent<CrowdControlController>();
            bool hardControlled = RinesRules.IsHardControlled(targetCc);
            if (hardControlled)
            {
                if (Hit(target, _skillData.WBaseDamage, _skillData.WAdRatio, HeroSkillSlot.W,
                    HardCcType.Snare, _skillData.WSnare, "RinesW"))
                    _visuals?.ShowHit(target.transform, true);
            }
            else
            {
                targetCc?.ApplySlow(_skillData.WSlowPercent, _skillData.WSlowDuration);
                DealDamage(target, _skillData.WBaseDamage, _skillData.WAdRatio, HeroSkillSlot.W,
                    false, "RinesW");
                _visuals?.ShowHit(target.transform, false);
            }
        }
    }

    private void CastE()
    {
        if (_isEActive || Time.timeAsDouble < _eCooldownEndTime ||
            (_cc != null && _cc.IsMovementBlocked)) return;
        _eCooldownEndTime = Time.timeAsDouble + _skillData.ECooldown;
        _sequence++;
        _isEActive = true;
        _eHit = false;
        _eStarted = Time.timeAsDouble;
        _eEnds = _eStarted + _skillData.EDuration;
        _eSpeedBonus = _stats.BaseMoveSpeed * _skillData.ESpeedPercent / 100f;
        _stats.AddMoveSpeedBonus(_eSpeedBonus);
        _visuals?.SetEActive(true);
        ScanE();
    }

    private void ScanE()
    {
        foreach (Targetable target in TargetsAt(transform.position, _skillData.ERadius))
        {
            _eHit = true;
            _eEnds = RinesRules.EEndAfterHit(Time.timeAsDouble, _skillData.EHitTail);
            Hit(target, _skillData.EBaseDamage, _skillData.EAdRatio, HeroSkillSlot.E,
                HardCcType.Snare, _skillData.ESnare, "RinesE");
            break;
        }
    }

    private void EndE()
    {
        if (_visuals != null) _visuals.SetEActive(false);
        if (!_isEActive) return;
        _isEActive = false;
        if (_stats != null) _stats.RemoveMoveSpeedBonus(_eSpeedBonus);
        _eSpeedBonus = 0f;
    }

    private void CastR()
    {
        if (_isEActive || _rPending || Time.timeAsDouble < _rCooldownEndTime ||
            !TryAimPoint(out Vector3 point)) return;
        SkillApproachController.For(gameObject).CastOrApproach(point, _skillData.RRange,
            () => PlaceR(point));
    }

    private void PlaceR(Vector3 point)
    {
        if (_skillData == null || _isEActive || _rPending ||
            Time.timeAsDouble < _rCooldownEndTime ||
            (_health != null && _health.IsDead) || (_cc != null && _cc.IsStunned)) return;
        _rCooldownEndTime = Time.timeAsDouble + _skillData.RCooldown;
        _sequence++;
        _rPoint = point;
        _rStarted = Time.timeAsDouble;
        _rPending = true;
        _rMarker = MakeMarker("Rines R warning", point, _skillData.RRadius);
    }

    private void ResolveR()
    {
        Vector3 point = _rPoint;
        CancelR();
        foreach (Targetable target in TargetsAt(point, _skillData.RRadius))
        {
            Vector3 delta = OboroCombatUtility.Flatten(target.transform.position - point);
            bool center = delta.sqrMagnitude <= _skillData.RCenterRadius * _skillData.RCenterRadius;
            if (center)
                Hit(target, _skillData.RBaseDamage, _skillData.RAdRatio, HeroSkillSlot.R,
                    HardCcType.Stun, _skillData.RStun, "RinesR");
            else
            {
                DealDamage(target, _skillData.RBaseDamage, _skillData.RAdRatio, HeroSkillSlot.R,
                    false, "RinesR");
            }
        }
    }

    private void Strike(Vector3 point, float radius, float baseDamage, float adRatio,
        HeroSkillSlot slot, HardCcType cc, float duration, string source)
    {
        foreach (Targetable target in TargetsAt(point, radius))
            Hit(target, baseDamage, adRatio, slot, cc, duration, source);
    }

    private bool Hit(Targetable target, float baseDamage, float adRatio, HeroSkillSlot slot,
        HardCcType type, float duration, string source)
    {
        CrowdControlController targetCc = target.GetComponent<CrowdControlController>();
        if (targetCc == null) targetCc = target.gameObject.AddComponent<CrowdControlController>();
        bool passive = RinesRules.IsHardControlled(targetCc);
        if (targetCc != null && targetCc.ApplyHardCC(type, duration, transform)) return false;
        DealDamage(target, baseDamage, adRatio, slot, passive, source);
        return true;
    }

    private void DealDamage(Targetable target, float baseDamage, float adRatio,
        HeroSkillSlot slot, bool passive, string source)
    {
        HealthController targetHealth = OboroCombatUtility.GetHealth(target);
        if (targetHealth == null || targetHealth.IsDead) return;
        int rank = _upgrades != null ? _upgrades.GetRank(slot) : 0;
        float raw = RinesRules.Damage(baseDamage, adRatio, _stats.CurrentAttackDamage, rank,
            passive, _skillData.PassiveMultiplier, _skillData.RankDamageStep);
        float actual = targetHealth.TakeDamage(raw, transform, DamageType.Normal,
            sourceId: $"{source}#{_sequence}");
        if (actual <= 0f) return;
        target.PlayHitFlash();
        CombatTextManager.ShowDamageDealt(target.transform.position, actual);
    }

    private List<Targetable> TargetsAt(Vector3 center, float radius, bool onlyEnemyPlayers = false)
    {
        var result = new List<Targetable>();
        var seen = new HashSet<Targetable>();
        Collider[] overlaps = Physics.OverlapSphere(center + Vector3.up, radius + 1.5f,
            _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            Targetable target = overlap.GetComponentInParent<Targetable>();
            if (!(onlyEnemyPlayers ? RinesRules.IsValidWTarget(transform, target) :
                RinesRules.IsValidTarget(transform, target)) || !seen.Add(target)) continue;
            Vector3 delta = OboroCombatUtility.Flatten(target.GetClosestPoint(center) - center);
            if (delta.sqrMagnitude <= radius * radius) result.Add(target);
        }
        return result;
    }

    private static GameObject MakeMarker(string label, Vector3 point, float radius)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = label;
        marker.transform.position = point + Vector3.up * 0.05f;
        marker.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null) renderer.material = OboroCombatUtility.CreateUnlitMaterial(new Color(1f, 0.9f, 0.2f));
        return marker;
    }

    private static void ClearMarker(ref GameObject marker)
    {
        if (marker == null) return;
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null) Destroy(renderer.material);
        Destroy(marker);
        marker = null;
    }
}
