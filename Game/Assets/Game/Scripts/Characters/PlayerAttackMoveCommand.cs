using UnityEngine;

/// <summary>Aを押している間だけ通常攻撃射程を表示し、左クリック地点に最も近い敵を攻撃対象にする。</summary>
public sealed class PlayerAttackMoveCommand : MonoBehaviour
{
    private PlayerInputHub _input;
    private PlayerTargetSelector _selector;
    private CharacterStats _stats;
    private HealthController _health;
    private SkillRangeIndicator _indicator;
    private Camera _camera;
    private TeamMember _team;

    private void Awake()
    {
        _input = GetComponent<PlayerInputHub>();
        if (_input == null) _input = gameObject.AddComponent<PlayerInputHub>();
        _selector = GetComponent<PlayerTargetSelector>();
        _stats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        _team = GetComponent<TeamMember>();
        _indicator = SkillRangeIndicator.Create(transform, "Basic Attack Range");
    }

    private void Update()
    {
        bool visible = _input != null && _input.APressed && (_health == null || !_health.IsDead);
        if (!visible)
        {
            _indicator.HideAll();
            return;
        }
        if (_stats != null) _indicator.ShowCircle(_stats.CurrentAttackRange,
            new Color(1f, 1f, 1f, 0.8f), 0.05f);
        if (!_input.LeftClickPressedThisFrame) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        Ray ray = _camera.ScreenPointToRay(_input.MousePosition);
        Vector3 clickPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity,
            OboroCombatUtility.ResolveGroundLayer(0), QueryTriggerInteraction.Ignore))
            clickPoint = hit.point;
        else if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float distance)) return;
        else clickPoint = ray.GetPoint(distance);

        Targetable nearest = FindNearestEnemy(clickPoint);
        if (nearest != null) _selector?.SelectAttackTarget(nearest);
    }

    private Targetable FindNearestEnemy(Vector3 clickPoint)
    {
        Targetable nearest = null;
        float best = float.PositiveInfinity;
        foreach (Targetable target in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
        {
            if (target == null || !target.isActiveAndEnabled || target.IsDead ||
                target.Classification == TargetClassification.Tower ||
                !OboroCombatUtility.IsEnemy(transform, target) ||
                !OboroWController.CanBeTargetSelected(target, transform)) continue;
            TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
            if (_team != null && targetTeam != null && targetTeam.Team == _team.Team) continue;
            Vector3 delta = target.GetClosestPoint(clickPoint) - clickPoint;
            delta.y = 0f;
            float distance = delta.sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            nearest = target;
        }
        return nearest;
    }

    private void OnDisable()
    {
        if (_indicator != null) _indicator.HideAll();
    }
}
