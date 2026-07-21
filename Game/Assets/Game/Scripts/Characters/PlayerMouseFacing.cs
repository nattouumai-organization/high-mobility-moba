using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 最後に右クリックしたGround上の地点の方向を、PlayerがY軸のみ回転して向くようにする。
/// 右クリックを長押ししている間は、カーソル下のGround地点が「最後に右クリックされた場所」として
/// 毎フレーム更新されるため、Playerはカーソル方向を向き続ける。
/// TASKS.md「マウス方向へキャラクターが向く処理を実装する」用の試作スクリプト。
/// 移動はPlayerClickMovementの責務であり、本スクリプトは回転のみを担う。
/// ZelfQControllerなどの外部スクリプトは、publicメソッドのSetLookTarget / SetLookDirectionで
/// 目標回転を安全に更新できる(privateフィールドは本スクリプト内部だけで管理する)。
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

    /// <summary>
    /// 指定したワールド座標の方向をPlayerが向くように、内部の目標回転を更新する。
    /// ZelfQControllerのブリンク後など、外部スクリプトから安全に呼び出すためのpublicメソッド。
    /// Y軸回転のみを使い、指定地点がPlayerとほぼ同じ位置の場合は安全に何もしない。
    /// 実際の回転は毎フレームのRotateTowardsTargetが行うため、InspectorのRotation Speed設定は維持される。
    /// </summary>
    public void SetLookTarget(Vector3 worldPosition)
    {
        SetLookDirection(worldPosition - transform.position);
    }

    /// <summary>
    /// 指定した方向ベクトルをPlayerが向くように、内部の目標回転を更新する。
    /// 方向ベクトルの水平成分(Y成分を除く)のみを使い、ほぼゼロベクトルの場合は安全に何もしない。
    /// </summary>
    public void SetLookDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < _minLookDistance * _minLookDistance)
        {
            return;
        }

        _targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _hasTargetRotation = true;
    }

    private void UpdateTargetRotationFromRightClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || _mainCamera == null)
        {
            return;
        }

        // 最後に右クリックした方向を向く仕様のため、右クリック入力があったフレームだけ目標回転を更新する。
        // 長押し中は毎フレームが右クリック入力として扱われ、カーソル方向を向き続ける。
        if (!mouse.rightButton.isPressed)
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
