using System;
using UnityEngine;

/// <summary>地点・対象指定スキルを、射程に入るまで通常移動で接近させる共通処理。</summary>
public sealed class SkillApproachController : MonoBehaviour, ICancelableSkillApproach
{
    private CharacterController _controller;
    private CharacterStats _stats;
    private PlayerInputHub _input;
    private PlayerTargetSelector _selector;
    private PlayerClickMovement _clickMovement;
    private HealthController _health;
    private CrowdControlController _cc;
    private AbilityLockController _abilityLock;
    private PlayerMouseFacing _facing;
    private Targetable _target;
    private bool _isTargetApproach;
    private Vector3 _point;
    private float _range;
    private Action _cast;

    public bool IsApproaching => _cast != null;

    public static SkillApproachController For(GameObject owner)
    {
        SkillApproachController approach = owner.GetComponent<SkillApproachController>();
        return approach != null ? approach : owner.AddComponent<SkillApproachController>();
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<CharacterStats>();
        _input = GetComponent<PlayerInputHub>();
        _selector = GetComponent<PlayerTargetSelector>();
        _clickMovement = GetComponent<PlayerClickMovement>();
        _health = GetComponent<HealthController>();
        _cc = GetComponent<CrowdControlController>();
        _abilityLock = GetComponent<AbilityLockController>();
        _facing = GetComponent<PlayerMouseFacing>();
    }

    public void CastOrApproach(Vector3 point, float range, Action cast)
    {
        StartApproach(null, point, range, cast);
    }

    public void CastOrApproach(Targetable target, float range, Action cast)
    {
        if (target == null || target.IsDead || !target.isActiveAndEnabled) return;
        StartApproach(target, target.GetClosestPoint(transform.position), range, cast);
    }

    private void StartApproach(Targetable target, Vector3 point, float range, Action cast)
    {
        CancelPendingApproach();
        if (cast == null || range <= 0f) return;
        _target = target;
        _isTargetApproach = target != null;
        _point = point;
        _range = range;
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= range * range)
        {
            CancelPendingApproach();
            cast();
            return;
        }
        _cast = cast;
        _clickMovement?.StopMovement();
        _selector?.ClearTargetSelection();
    }

    private void Update()
    {
        if (_cast == null) return;
        if (_cc == null) _cc = GetComponent<CrowdControlController>();
        if (_abilityLock == null) _abilityLock = GetComponent<AbilityLockController>();
        if ((_health != null && _health.IsDead) || OboroCombatUtility.IsMatchEnded ||
            (_cc != null && _cc.IsMovementBlocked) ||
            (_abilityLock != null && _abilityLock.IsLocked) ||
            (_input != null && _input.SPressedThisFrame))
        {
            CancelPendingApproach();
            return;
        }
        // A held target-selection click must not cancel the approach every frame.
        // Only a new ground movement order replaces the pending skill.
        if (_input != null && _input.RightClickPressedThisFrame &&
            (_selector == null || !_selector.IsPointingAtTargetable()))
        {
            CancelPendingApproach();
            return;
        }
        if (_isTargetApproach)
        {
            if (_target == null || _target.IsDead || !_target.isActiveAndEnabled)
            {
                CancelPendingApproach();
                return;
            }
            _point = _target.GetClosestPoint(transform.position);
        }
        Vector3 delta = _point - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= Mathf.Max(0f, _range - 0.02f))
        {
            Action cast = _cast;
            CancelPendingApproach();
            cast();
            return;
        }
        if (_controller == null || !_controller.enabled || _stats == null || distance < 0.001f) return;
        float radius = _controller.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        Vector3 direction = ObstacleAvoidance.SteerDirection(transform.position,
            delta / distance, radius, distance, null);
        _facing?.SetMovementLookDirection(direction);
        _controller.Move(direction * Mathf.Min(_stats.CurrentMoveSpeed * Time.deltaTime,
            distance - Mathf.Max(0f, _range - 0.02f)));
    }

    public void CancelPendingApproach()
    {
        _cast = null;
        _target = null;
        _isTargetApproach = false;
    }

    private void OnDisable() => CancelPendingApproach();
}
