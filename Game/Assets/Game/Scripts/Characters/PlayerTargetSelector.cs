using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 右クリックによる攻撃対象(Targetable)の選択・切替・解除を管理する。
/// TASKS.md「通常攻撃のターゲット選択を実装する」用の試作スクリプト。
/// 入力優先順位は「TargetableLayerの対象選択 > GroundLayerへの移動」とし、
/// PlayerClickMovementはIsPointingAtTargetable()に問い合わせて移動可否を判断する。
/// ターゲットを選択した際は、PlayerClickMovement.StopMovement()を呼びPlayerをその場で停止させる。
/// ターゲットが死亡・破棄・無効化された場合は、選択を安全に解除する。
/// </summary>
public class PlayerTargetSelector : MonoBehaviour
{
    // ターゲット選択の判定に使用するLayerMask。InspectorでTargetableLayerを設定する。
    [SerializeField] private LayerMask _targetableLayer;

    // ターゲット解除の判定に使用するLayerMask。InspectorでGroundLayerを設定する。
    [SerializeField] private LayerMask _groundLayer;

    private Camera _mainCamera;
    private Targetable _currentTarget;

    // 同じPlayer上の移動制御(任意)。ターゲット選択時に移動を停止するために使用する。
    private PlayerClickMovement _clickMovement;

    // 現在選択中のターゲット。未選択時はnull。
    public Targetable CurrentTarget => _currentTarget;

    public bool HasTarget => _currentTarget != null;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _clickMovement = GetComponent<PlayerClickMovement>();
    }

    private void Update()
    {
        ClearTargetIfInvalid();
        HandleRightClick();
    }

    /// <summary>
    /// 現在のマウス位置がTargetableLayerの対象を指しているかを返す。
    /// PlayerClickMovementがGround移動を開始するかどうかの優先順位判定に使用する。
    /// </summary>
    public bool IsPointingAtTargetable()
    {
        return TryGetTargetableUnderMouse(out _);
    }

    private void HandleRightClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
        {
            return;
        }

        // 優先1: Targetableの対象を右クリックした場合は選択(選択中なら新しい対象へ切替)する。
        if (TryGetTargetableUnderMouse(out Targetable targetable))
        {
            SelectTarget(targetable);

            // ターゲット選択時は、進行中の移動を中断してその場で停止する。
            if (_clickMovement != null)
            {
                _clickMovement.StopMovement();
            }
            return;
        }

        // 優先2: Groundを右クリックした場合はターゲットを解除する(移動処理はPlayerClickMovementが行う)。
        if (IsPointingAtGround())
        {
            ClearTarget();
        }
    }

    private bool TryGetTargetableUnderMouse(out Targetable targetable)
    {
        targetable = null;

        if (!TryGetMouseRay(out Ray ray))
        {
            return false;
        }

        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _targetableLayer))
        {
            return false;
        }

        targetable = hitInfo.collider.GetComponentInParent<Targetable>();
        return targetable != null;
    }

    private bool IsPointingAtGround()
    {
        return TryGetMouseRay(out Ray ray)
            && Physics.Raycast(ray, out _, Mathf.Infinity, _groundLayer);
    }

    private bool TryGetMouseRay(out Ray ray)
    {
        ray = default;

        Mouse mouse = Mouse.current;
        if (mouse == null || _mainCamera == null)
        {
            return false;
        }

        ray = _mainCamera.ScreenPointToRay(mouse.position.ReadValue());
        return true;
    }

    private void SelectTarget(Targetable newTarget)
    {
        if (_currentTarget == newTarget)
        {
            return;
        }

        if (_currentTarget != null)
        {
            _currentTarget.SetSelected(false);
        }

        _currentTarget = newTarget;
        _currentTarget.SetSelected(true);
    }

    private void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget.SetSelected(false);
        }

        _currentTarget = null;
    }

    private void ClearTargetIfInvalid()
    {
        // Destroy済みの対象(Unity上のnull)は参照を破棄し、死亡・無効化された対象は選択を安全に解除する。
        if (_currentTarget == null)
        {
            _currentTarget = null;
            return;
        }

        if (_currentTarget.IsDead || !_currentTarget.isActiveAndEnabled)
        {
            ClearTarget();
        }
    }
}
