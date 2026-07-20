using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 最後に右クリックしたGround上の地点の方向を、PlayerがY軸のみ回転して向くようにする。
/// TASKS.md「マウス方向へキャラクターが向く処理を実装する」用の試作スクリプト。
/// 移動はPlayerClickMovementの責務であり、本スクリプトは回転のみを担う。
/// </summary>
public class PlayerMouseFacing : MonoBehaviour
{
    // 試作用の回転速度(毎秒度数)。Inspectorから変更できる。
    [SerializeField] private float _rotationSpeed = 1440f;

    // Ground判定に使用するLayerMask。InspectorでGroundLayerを設定する。
    [SerializeField] private LayerMask _groundLayer;

    // クリック地点がPlayerとほぼ同じ位置の場合は回転しないためのしきい値。
    [SerializeField] private float _minLookDistance = 0.1f;

    private Camera _mainCamera;
    private Quaternion _targetRotation;
    private bool _hasTargetRotation;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _targetRotation = transform.rotation;
    }

    private void Update()
    {
        UpdateTargetRotationFromRightClick();
        RotateTowardsTarget();
    }

    private void UpdateTargetRotationFromRightClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || _mainCamera == null)
        {
            return;
        }

        // 最後に右クリックした方向を向く仕様のため、右クリックした瞬間だけ目標回転を更新する。
        if (!mouse.rightButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _groundLayer))
        {
            // Ground外を右クリックした場合は、最後に向いていた方向を維持する。
            return;
        }

        // Y成分を0にして、水平面上の方向だけを使う。
        Vector3 toMousePoint = hitInfo.point - transform.position;
        toMousePoint.y = 0f;

        // クリック地点がPlayerとほぼ同じ位置の場合は、不要な回転を行わない。
        if (toMousePoint.sqrMagnitude < _minLookDistance * _minLookDistance)
        {
            return;
        }

        _targetRotation = Quaternion.LookRotation(toMousePoint.normalized, Vector3.up);
        _hasTargetRotation = true;
    }

    private void RotateTowardsTarget()
    {
        if (!_hasTargetRotation)
        {
            return;
        }

        // 目標回転はY軸回転のみなので、Playerが地面へ傾くことはない。
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            _targetRotation,
            _rotationSpeed * Time.deltaTime);
    }
}
