using UnityEngine;

/// <summary>
/// トップダウンカメラのモード管理(Main Cameraへ追加する)。
/// - ロックモード(既定): プレイヤーを中心にカメラが追従する。
/// - フリーモード: プレイヤーに追従せず、マウスカーソルを画面端(上下左右)へ持っていくと
///   その方向へゆっくりスクロールする(スクロール速度・画面端の判定幅はInspector設定)。
///   Spaceを押している間は即座にプレイヤー中心になり追従し、離すとその場でフリーモードへ戻る。
/// - Yでロック/フリーを切り替える(フリー→ロック切替時は即座にプレイヤー中心へ戻る)。
/// 追従対象は未設定なら自動検出する(PlayerClickMovement / PlayerInputHubを持つオブジェクト)。
/// カメラの俯瞰角度・高さは、対象取得時のカメラ位置と対象の相対オフセットとして維持する(シーン設定のまま)。
/// スクロール方向はカメラのY軸回転に合わせたXZ平面上の右・前方向を使用する。
/// フェーズ5: MapBuilderがあるシーンでは、対象取得時にシーンカメラの高さ・俯瞰角度から追従オフセットを計算し
/// (開始地点が原点から離れていてもプレイヤーを画面中央に映す)、フリーモードのスクロールを
/// マップ範囲+余白(Bounds Margin)の内側へ注視点基準でクランプする。
/// 入力はPlayerInputHub(CameraCenterPressed / CameraLockTogglePressedThisFrame / MousePosition)を使用する。
/// </summary>
public class TopDownCameraController : MonoBehaviour
{
    /// <summary>カメラモード。LockedFollow=プレイヤー中心に追従(既定) / FreeScroll=画面端スクロール。</summary>
    public enum CameraMode
    {
        LockedFollow,
        FreeScroll,
    }

    [Header("Target")]
    // 追従対象。未設定の場合はプレイヤー(PlayerClickMovement / PlayerInputHub)を自動検出する。
    [SerializeField] private Transform _target;

    [Header("Scroll Settings")]
    // フリーモードの画面端スクロール速度(ワールド単位/秒)。Inspectorで変更できる。
    [SerializeField, Min(0.1f)] private float _edgeScrollSpeed = 8f;
    // 画面端と判定するスクリーン端からのピクセル幅。
    [SerializeField, Min(1f)] private float _edgeThicknessPixels = 12f;

    [Header("Map Bounds (フェーズ5)")]
    // フリーモードのスクロールで、注視点がマップ端から外側へ出られる余白。
    [SerializeField, Min(0f)] private float _boundsMargin = 2f;

    [Header("Debug (Runtime)")]
    [SerializeField] private CameraMode _mode = CameraMode.LockedFollow;

    private PlayerInputHub _inputHub;
    private Vector3 _offset;
    private bool _hasOffset;
    // 画面端スクロールに使うXZ平面上の方向(カメラのY軸回転基準)。
    private Vector3 _scrollRight = Vector3.right;
    private Vector3 _scrollForward = Vector3.forward;
    // マップ範囲のクランプに使用する(無いシーンではクランプしない)。
    private MapBuilder _mapBuilder;

    /// <summary>現在のカメラモード。</summary>
    public CameraMode Mode => _mode;

    private void Start()
    {
        _mapBuilder = FindFirstObjectByType<MapBuilder>();
        TryAcquireTarget();
        CacheScrollAxes();
    }

    private void LateUpdate()
    {
        // プレイヤーが後から生成される場合に備えて、取得できるまで再検出する。
        if ((_target == null || _inputHub == null) && !TryAcquireTarget())
        {
            return;
        }

        HandleModeToggle();

        // ロックモード、またはSpaceを押している間は、即座にプレイヤー中心へ移動して追従する。
        bool followNow = _mode == CameraMode.LockedFollow || _inputHub.CameraCenterPressed;
        if (followNow)
        {
            transform.position = _target.position + _offset;
            return;
        }

        HandleEdgeScroll();
    }

    // Yでロック/フリーモードを切り替える。フリー→ロックの切替時は同じフレームで即座にプレイヤー中心へ戻る。
    private void HandleModeToggle()
    {
        if (!_inputHub.CameraLockTogglePressedThisFrame)
        {
            return;
        }

        _mode = _mode == CameraMode.LockedFollow ? CameraMode.FreeScroll : CameraMode.LockedFollow;
        string modeName = _mode == CameraMode.LockedFollow ? "ロック(プレイヤー追従)" : "フリー(画面端スクロール)";
        Debug.Log($"カメラ: モードを{modeName}へ切り替えました。", this);
    }

    // マウスカーソルが画面端(上下左右)にあるとき、その方向へ水平にスクロールする。
    private void HandleEdgeScroll()
    {
        Vector2 mousePosition = _inputHub.MousePosition;

        // ウィンドウ外の座標は画面内へクランプして扱う(ウィンドウモードでカーソルが外へ出た場合)。
        float x = Mathf.Clamp(mousePosition.x, 0f, Screen.width);
        float y = Mathf.Clamp(mousePosition.y, 0f, Screen.height);

        Vector3 direction = Vector3.zero;
        if (x <= _edgeThicknessPixels) direction -= _scrollRight;
        if (x >= Screen.width - _edgeThicknessPixels) direction += _scrollRight;
        if (y <= _edgeThicknessPixels) direction -= _scrollForward;
        if (y >= Screen.height - _edgeThicknessPixels) direction += _scrollForward;

        if (direction == Vector3.zero)
        {
            return;
        }

        // 斜め(画面の角)の場合も移動速度が一定になるよう正規化する。
        Vector3 nextPosition = transform.position + direction.normalized * (_edgeScrollSpeed * Time.deltaTime);
        transform.position = ClampToMapBounds(nextPosition);
    }

    // 追従対象と入力ハブを取得する。取得できた時点のカメラ位置から相対オフセットを記録する。
    private bool TryAcquireTarget()
    {
        if (_target == null)
        {
            // プレイヤー本体を自動検出する(右クリック移動を持つオブジェクト = 操作キャラクター)。
            PlayerClickMovement clickMovement = FindFirstObjectByType<PlayerClickMovement>();
            if (clickMovement != null)
            {
                _target = clickMovement.transform;
            }
            else
            {
                PlayerInputHub inputHub = FindFirstObjectByType<PlayerInputHub>();
                if (inputHub != null) _target = inputHub.transform;
            }
        }

        if (_target == null)
        {
            return false;
        }

        if (_inputHub == null)
        {
            _inputHub = _target.GetComponent<PlayerInputHub>();
            if (_inputHub == null) _inputHub = _target.gameObject.AddComponent<PlayerInputHub>();
        }

        // 俯瞰角度・高さを維持するため、最初に対象を取得した時点で追従オフセットを決める。
        // マップがあるシーンでは、開始地点が原点から離れていてもプレイヤーを画面中央に映せるよう、
        // シーンカメラの高さ・俯瞰角度からオフセットを計算する(カメラの初期位置には依存しない)。
        if (!_hasOffset)
        {
            _offset = _mapBuilder != null ? ComputeOffsetFromSceneCamera() : transform.position - _target.position;
            _hasOffset = true;
        }

        return true;
    }

    // シーンカメラの高さ・俯瞰角度を維持したまま、プレイヤーを画面中央に映す相対オフセットを求める。
    // カメラの向き(transform.forward)に沿って、対象の高さまでの距離だけ後ろへ下がった位置を使う。
    private Vector3 ComputeOffsetFromSceneCamera()
    {
        Vector3 forward = transform.forward;
        float height = transform.position.y - _target.position.y;

        // ほぼ水平(または上向き)のカメラでは距離を計算できないため、現在の相対位置を維持する。
        if (forward.y >= -0.01f || height <= 0f)
        {
            return transform.position - _target.position;
        }

        float distance = height / -forward.y;
        return -forward * distance;
    }

    // フリーモードのスクロール位置を、注視点(カメラが映す地面上の点)がマップ範囲+余白の内側に収まるようクランプする。
    private Vector3 ClampToMapBounds(Vector3 cameraPosition)
    {
        if (_mapBuilder == null)
        {
            return cameraPosition;
        }

        // カメラ位置→注視点のオフセットは一定のため、注視点基準でクランプしてからカメラ位置へ戻す。
        Vector3 lookOffset = ComputeLookOffset(cameraPosition);
        Vector3 lookPoint = cameraPosition + lookOffset;

        Vector3 min = _mapBuilder.BoundsMin;
        Vector3 max = _mapBuilder.BoundsMax;
        lookPoint.x = Mathf.Clamp(lookPoint.x, min.x - _boundsMargin, max.x + _boundsMargin);
        lookPoint.z = Mathf.Clamp(lookPoint.z, min.z - _boundsMargin, max.z + _boundsMargin);

        return lookPoint - lookOffset;
    }

    // カメラ位置から、カメラの向きで地面(高さ0)と交わる注視点までのオフセットを求める。
    private Vector3 ComputeLookOffset(Vector3 cameraPosition)
    {
        Vector3 forward = transform.forward;
        if (forward.y >= -0.01f)
        {
            // 水平カメラでは注視点を計算できないため、真下を注視点として扱う。
            return new Vector3(0f, -cameraPosition.y, 0f);
        }

        float distance = cameraPosition.y / -forward.y;
        return forward * distance;
    }

    // 画面端スクロールに使う方向を、カメラのY軸回転に合わせたXZ平面上の右・前方向として求める。
    private void CacheScrollAxes()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        _scrollRight = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        // 真下を向いたカメラではforwardのXZ成分がほぼ0になるため、その場合はupのXZ成分を使う。
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.up;
            forward.y = 0f;
        }
        _scrollForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
}
