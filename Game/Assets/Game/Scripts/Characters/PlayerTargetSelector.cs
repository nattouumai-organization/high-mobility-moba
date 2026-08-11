using UnityEngine;

/// <summary>
/// 右クリックによるTargetableの選択・切替・解除を管理する。
/// 対象選択の優先順位はTargetableLayer > GroundLayer。
/// 朧W中の対象は選択不可とし、敵タワー射程内へ入った場合だけ通常どおり選択可能に戻す。
/// </summary>
public class PlayerTargetSelector : MonoBehaviour
{
    [SerializeField] private LayerMask _targetableLayer;
    [SerializeField] private LayerMask _groundLayer;

    private Camera _mainCamera;
    private Targetable _currentTarget;
    private PlayerClickMovement _clickMovement;
    private HealthController _selfHealth;
    private PlayerInputHub _inputHub;
    private TeamMember _teamMember;

    public Targetable CurrentTarget => _currentTarget;
    public bool HasTarget => _currentTarget != null;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _clickMovement = GetComponent<PlayerClickMovement>();
        _selfHealth = GetComponent<HealthController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
    }

    private void Update()
    {
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            ClearTarget();
            return;
        }
        ClearTargetIfInvalid();
        HandleRightClick();
    }

    public bool IsPointingAtTargetable()
    {
        return TryGetTargetableUnderMouse(out _);
    }

    private void HandleRightClick()
    {
        if (_inputHub == null || !_inputHub.RightClickPressed) return;

        if (TryGetTargetableUnderMouse(out Targetable targetable))
        {
            bool isNewSelection = _currentTarget != targetable;
            SelectTarget(targetable);
            if (isNewSelection && _clickMovement != null) _clickMovement.StopMovement();
            return;
        }

        if (IsPointingAtGround()) ClearTarget();
    }

    private bool TryGetTargetableUnderMouse(out Targetable targetable)
    {
        targetable = null;
        if (!TryGetMouseRay(out Ray ray)) return false;
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _targetableLayer)) return false;

        targetable = hitInfo.collider.GetComponentInParent<Targetable>();
        if (targetable == null || targetable.IsDead || !targetable.isActiveAndEnabled ||
            IsSameTeam(targetable) || !OboroWController.CanBeTargetSelected(targetable, transform))
        {
            targetable = null;
            return false;
        }
        return true;
    }

    private bool IsSameTeam(Targetable targetable)
    {
        if (_teamMember == null)
        {
            _teamMember = GetComponent<TeamMember>();
            if (_teamMember == null) return false;
        }
        TeamMember targetTeam = targetable.GetComponentInParent<TeamMember>();
        return targetTeam != null && targetTeam.Team == _teamMember.Team;
    }

    private bool IsPointingAtGround()
    {
        return TryGetMouseRay(out Ray ray) && Physics.Raycast(ray, out _, Mathf.Infinity, _groundLayer);
    }

    private bool TryGetMouseRay(out Ray ray)
    {
        ray = default;
        if (_inputHub == null) return false;
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return false;
        }
        ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        return true;
    }

    private void SelectTarget(Targetable newTarget)
    {
        if (_currentTarget == newTarget) return;
        if (_currentTarget != null) _currentTarget.SetSelected(false);
        _currentTarget = newTarget;
        _currentTarget.SetSelected(true);
    }

    public void ClearTargetSelection()
    {
        ClearTarget();
    }

    private void ClearTarget()
    {
        if (_currentTarget != null) _currentTarget.SetSelected(false);
        _currentTarget = null;
    }

    private void ClearTargetIfInvalid()
    {
        if (_currentTarget == null)
        {
            _currentTarget = null;
            return;
        }

        if (_currentTarget.IsDead || !_currentTarget.isActiveAndEnabled ||
            !OboroWController.CanBeTargetSelected(_currentTarget, transform))
        {
            ClearTarget();
        }
    }
}
