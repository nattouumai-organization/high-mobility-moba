using UnityEngine;

/// <summary>
/// トップダウンカメラのモード管理(Main Cameraへ追加する)。
/// - ロックモード(既定): プレイヤーを中心にカメラが追従する。
/// - フリーモード: プレイヤーに追従せず、マウスカーソルを画面端(上下左右)へ持っていくと
///   その方向へゆっくりスクロールする(スクロール速度・画面端の判定幅はInspector設定)。
///   Spaceを押している間は即座にプレイヤー中心になり追従し、離すとその場でフリーモードへ戻る。
/// - Yでロック/フリーを切り替える(フリー→ロック切替時は即座にプレイヤー中心へ戻る)。
/// 追従対象は未設定なら自動検出する(PlayerClickMovement / PlayerInputHubを持つオブジェクト)。
/// カメラの俯瞰角度・高さはシーン設定のまま維持する。対象との相対オフセットは、MapBuilderが無いシーンでは
/// 従来どおり対象取得時のカメラ位置との相対位置を使い、MapBuilderがあるシーンでは開始地点がシーンの
/// カメラ位置から離れていてもプレイヤーを中心に映せるよう、シーンカメラの高さと俯瞰角度から計算する。
/// スクロール方向はカメラのY軸回転に合わせたXZ平面上の右・前方向を使用する。
/// フリーモードのスクロールは、シーンにMapBuilderがある場合のみマップ範囲(BoundsMin / BoundsMax)内へ
/// 注視点基準でクランプする(余白Bounds MarginはInspector設定。MapBuilderが無いシーンではクランプしない)。
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
    // マップ端でスクロールを止める際の余白(ワールド単位)。シーンにMapBuilderがある場合のみ使用する。
    [SerializeField, Min(0f)] private float _boundsMargin = 2f;

    [Header("Debug (Runtime)")]
    [SerializeField] private CameraMode _mode = CameraMode.LockedFollow;

    private PlayerInputHub _inputHub;
    private MapBuilder _mapBuilder;
    private Vector3 _offset;
    private bool _hasOffset;
    // 画面端スクロールに使うXZ平面上の方向(カメラのY軸回転基準)。
    private Vector3 _scrollRight = Vector3.right;
    private Vector3 _scrollForward = Vector3.forward;

    /// <summary>現在のカメラモード。</summary>
    public CameraMode Mode => _mode;

    private void Start()
    {
        // マップ生成(MapBuilder)の有無でオフセット計算とスクロールクランプの挙動が変わるため、先に取得する。
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
        transform.position += direction.normalized * (_edgeScrollSpeed * Time.deltaTime);

        ClampToMapBounds();
    }

    // フリースクロールの注視点(カメラ位置-オフセット)をマップ範囲内へクランプする。
    // シーンにMapBuilderが無い場合は何もしない(従来のテストシーンでも動作する)。
    private void ClampToMapBounds()
    {
        if (_mapBuilder == null || !_hasOffset)
        {
            return;
        }

        Vector2 boundsMin = _mapBuilder.BoundsMin;
        Vector2 boundsMax = _mapBuilder.BoundsMax;
        Vector2 boundsCenter = (boundsMin + boundsMax) * 0.5f;
        float halfExtentX = Mathf.Max(0f, (boundsMax.x - boundsMin.x) * 0.5f - _boundsMargin);
        float halfExtentZ = Mathf.Max(0f, (boundsMax.y - boundsMin.y) * 0.5f - _boundsMargin);

        Vector3 focusPoint = transform.position - _offset;
        float clampedX = Mathf.Clamp(focusPoint.x, boundsCenter.x - halfExtentX, boundsCenter.x + halfExtentX);
        float clampedZ = Mathf.Clamp(focusPoint.z, boundsCenter.y - halfExtentZ, boundsCenter.y + halfExtentZ);

        transform.position += new Vector3(clampedX - focusPoint.x, 0f, clampedZ - focusPoint.z);
    }

    // 追従対象と入力ハブを取得する。取得できた時点で相対オフセットを記録する。
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

        // 最初に対象を取得した時点で相対オフセットを決める(俯瞰角度・高さはシーン設定のまま維持する)。
        if (!_hasOffset)
        {
            _offset = CalculateOffset();
            _hasOffset = true;
        }

        return true;
    }

    // 対象との相対オフセットを求める。
    // - MapBuilderが無いシーン: 従来どおり、対象取得時のカメラ位置との相対位置を使う。
    // - MapBuilderがあるシーン: 開始地点がシーンのカメラ位置から離れていてもプレイヤーを中心に映せるよう、
    //   シーンカメラの高さと俯瞰角度(向き)から計算する。
    private Vector3 CalculateOffset()
    {
        if (_mapBuilder == null)
        {
            return transform.position - _target.position;
        }

        Vector3 forward = transform.forward;
        float heightDifference = transform.position.y - _target.position.y;
        if (forward.y >= -0.01f || heightDifference <= 0f)
        {
            // 下を向いていない・対象より低いなど計算できない場合は従来の相対位置へフォールバックする。
            return transform.position - _target.position;
        }

        // 視線が対象の高さへ届くまでの距離を求め、視線の逆方向へ離した位置をオフセットにする。
        float distance = heightDifference / -forward.y;
        return -forward * distance;
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
