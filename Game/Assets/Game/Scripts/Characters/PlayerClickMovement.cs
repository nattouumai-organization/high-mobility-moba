using UnityEngine;

/// <summary>
/// Ground上を右クリックした地点へ、CharacterController.Moveで滑らかに移動させる。
/// 右クリックを長押ししている間は、カーソル下のGround地点へ移動先を毎フレーム更新し続ける。
/// TASKS.md「右クリック移動を実装する」用の試作スクリプト。
/// 移動速度はCharacterStatsのCurrent Move Speedから取得する。
/// 入力はPlayerInputHub(InputAction)経由で取得する。
/// TargetableLayerの対象を右クリックした場合は、ターゲット選択を優先しGround移動を開始しない。
/// ターゲット選択時はPlayerTargetSelectorがStopMovement()を呼び、Playerはその場で停止する。
/// 射程外のターゲットを選択した場合は、PlayerBasicAttackControllerがMoveToPosition()で射程内まで自動接近させる。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
public class PlayerClickMovement : MonoBehaviour
{
    // Ground判定に使用するLayerMask。InspectorでGroundLayerを設定する。
    [SerializeField] private LayerMask _groundLayer;

    // 移動先にこの距離まで近づいたら停止する。
    [SerializeField] private float _stoppingDistance = 0.1f;

    private CharacterController _characterController;
    private CharacterStats _characterStats;

    // 同じPlayer上のターゲット選択(任意)。存在する場合、Targetable対象の右クリック時は移動しない。
    private PlayerTargetSelector _targetSelector;

    private ZelfQController _qController;
    private ZelfRController _rController;
    private PlayerInputHub _inputHub;

    private Camera _mainCamera;
    private Vector3 _destination;
    private bool _hasDestination;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _characterStats = GetComponent<CharacterStats>();
        _targetSelector = GetComponent<PlayerTargetSelector>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mainCamera = Camera.main;
        _qController = GetComponent<ZelfQController>();
        _rController = GetComponent<ZelfRController>();

        if (_characterStats == null)
        {
            // RequireComponentにより通常は発生しないが、万一の場合は分かりやすく知らせて安全に停止する。
            Debug.LogError("PlayerClickMovement: CharacterStatsが見つからないため、移動を無効化します。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        HandleStopCommand();

        UpdateDestinationFromRightClick();
        MoveTowardsDestination();
    }

    /// <summary>
    /// 現在の移動を中断し、その場に停止する。
    /// ターゲット選択時(将来的には通常攻撃時)にPlayerTargetSelectorから呼び出される。
    /// </summary>
    public void StopMovement()
    {
        _hasDestination = false;
    }

    /// <summary>
    /// 指定したワールド座標へ向けた移動を開始する。
    /// 射程外のターゲットへの自動接近時にPlayerBasicAttackControllerから呼び出される。
    /// 重力・段差処理は今回実装しないため、高さは現在のY座標を維持する。
    /// </summary>
    public void MoveToPosition(Vector3 worldPosition)
    {
        _destination = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
        _hasDestination = true;
    }

    private void UpdateDestinationFromRightClick()
    {
        // 長押し中は常にカーソル下の地点へ移動する仕様のため、
        // 押した瞬間だけでなく、右ボタンが押されている間は毎フレーム移動先を更新する。
        if (_inputHub == null || !_inputHub.RightClickPressed)
        {
            return;
        }

        if (_mainCamera == null)
        {
            return;
        }

        // 入力優先順位: ターゲット選択 > Ground移動。
        // TargetableLayerの対象を右クリック(長押し中に指している場合を含む)した場合は、移動先を更新しない。
        if (_targetSelector != null && _targetSelector.IsPointingAtTargetable())
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _groundLayer))
        {
            // Ground以外を右クリックした場合は移動先を変更しない。
            return;
        }

        // 重力・段差処理は今回実装しないため、高さは現在のY座標を維持する。
        _destination = new Vector3(hitInfo.point.x, transform.position.y, hitInfo.point.z);
        _hasDestination = true;
    }

    private void MoveTowardsDestination()
    {
        if (!_hasDestination)
        {
            return;
        }

        Vector3 toDestination = _destination - transform.position;
        toDestination.y = 0f;

        float remainingDistance = toDestination.magnitude;
        if (remainingDistance <= _stoppingDistance)
        {
            _hasDestination = false;
            return;
        }

        // 移動速度はCharacterStatsから毎フレーム取得するため、Inspector値の変更が即座に反映される。
        // 移動先を通り過ぎないよう、残り距離でクランプする。
        float moveDistance = Mathf.Min(_characterStats.CurrentMoveSpeed * Time.deltaTime, remainingDistance);
        Vector3 motion = toDestination.normalized * moveDistance;
        _characterController.Move(motion);
    }

    /// <summary>
    /// 停止コマンド(Sキー): 進行中の移動を中断し、ターゲット選択とQ/Rの自動接近も解除して、その場で停止する。
    /// (ターゲット解除により通常攻撃の自動接近・継続攻撃も停止する)
    /// </summary>
    private void HandleStopCommand()
    {
        if (_inputHub == null || !_inputHub.SPressedThisFrame) return;

        StopMovement();
        if (_targetSelector != null) _targetSelector.ClearTargetSelection();
        if (_qController != null) _qController.CancelPendingApproach();
        if (_rController != null) _rController.CancelPendingApproach();
    }
}
