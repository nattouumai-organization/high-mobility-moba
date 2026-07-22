using UnityEngine;

/// <summary>
/// 右クリックによる攻撃対象(Targetable)の選択・切替・解除を管理する。
/// TASKS.md「通常攻撃のターゲット選択を実装する」用の試作スクリプト。
/// 入力はPlayerInputHub(InputAction)経由で取得する。
/// 入力優先順位は「TargetableLayerの対象選択 > GroundLayerへの移動」とし、
/// PlayerClickMovementはIsPointingAtTargetable()に問い合わせて移動可否を判断する。
/// ターゲットを選択した際は、PlayerClickMovement.StopMovement()を呼びPlayerをその場で停止させる。
/// 右クリック長押し中にTargetableを指した場合はターゲット選択を優先し、その対象を選択・切替する。
/// 右クリック長押し中にGroundを指している間はターゲットを解除し続け、長押し移動へ切り替える。
/// ターゲットが死亡・破棄・無効化された場合は、選択を安全に解除する。
/// 自分(Player)が死亡している間は、選択中のターゲットを解除し、新しいターゲット選択も受け付けない。
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

    // 自分(Player)のHP。死亡中はターゲット選択を停止するために参照する。
    private HealthController _selfHealth;

    private PlayerInputHub _inputHub;

    // 現在選択中のターゲット。未選択時はnull。
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
        // 死亡中は選択中のターゲットを解除し、新しいターゲット選択も受け付けない。
        // (復活後は自動的に選択可能に戻る)
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            ClearTarget();
            return;
        }

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
        if (_inputHub == null || !_inputHub.RightClickPressed)
        {
            return;
        }

        // 優先1: Targetableの対象を右クリック(長押し中に指している場合を含む)した場合は、
        // ターゲット選択を優先し、選択(選択中なら新しい対象へ切替)する。
        if (TryGetTargetableUnderMouse(out Targetable targetable))
        {
            bool isNewSelection = _currentTarget != targetable;
            SelectTarget(targetable);

            // 新しくターゲットを選択・切替した時のみ、進行中の移動を中断してその場で停止する。
            // 長押しで同じターゲットを指し続けている間は、射程外からの自動接近を妨げない。
            if (isNewSelection && _clickMovement != null)
            {
                _clickMovement.StopMovement();
            }
            return;
        }

        // 優先2: Groundを右クリックしている場合はターゲットを解除する(移動処理はPlayerClickMovementが行う)。
        // 長押し中も解除し続けることで、長押しによるGround移動とターゲットへの自動接近が競合しないようにする。
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

        if (_inputHub == null)
        {
            return false;
        }

        // Camera.mainは毎フレーム呼ぶと検索コストがかかるため、Awakeでキャッシュし、破棄時のみ再取得する。
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return false;
            }
        }

        ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
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
