using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ground上を右クリックした地点へ、CharacterController.Moveで滑らかに移動させる。
/// TASKS.md「右クリック移動を実装する」用の試作スクリプト。
/// 移動速度はCharacterStatsのCurrent Move Speedから取得する。
/// TargetableLayerの対象を右クリックした場合は、ターゲット選択を優先しGround移動を開始しない。
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

    private Camera _mainCamera;
    private Vector3 _destination;
    private bool _hasDestination;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _characterStats = GetComponent<CharacterStats>();
        _targetSelector = GetComponent<PlayerTargetSelector>();
        _mainCamera = Camera.main;

        if (_characterStats == null)
        {
            // RequireComponentにより通常は発生しないが、万一の場合は分かりやすく知らせて安全に停止する。
            Debug.LogError("PlayerClickMovement: CharacterStatsが見つからないため、移動を無効化します。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        UpdateDestinationFromRightClick();
        MoveTowardsDestination();
    }

    private void UpdateDestinationFromRightClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
        {
            return;
        }

        if (_mainCamera == null)
        {
            return;
        }

        // 入力優先順位: ターゲット選択 > Ground移動。
        // TargetableLayerの対象を右クリックした場合は、Groundへの移動を開始しない。
        if (_targetSelector != null && _targetSelector.IsPointingAtTargetable())
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mouse.position.ReadValue());
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
}
