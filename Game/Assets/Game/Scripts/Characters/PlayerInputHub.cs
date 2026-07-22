using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Playerの入力をInputActionで一元管理するハブ。
/// 従来のKeyboard.current / Mouse.currentの直接ポーリングを置き換え、
/// 将来のキーコンフィグ・ゲームパッド対応はこのクラスのバインディング変更だけで行えるようにする。
/// 各コントローラーのAwakeでGetComponent→なければAddComponentされるため、
/// Inspectorでの手動アタッチは不要(手動でアタッチしてもよい)。
/// </summary>
public sealed class PlayerInputHub : MonoBehaviour
{
    private InputAction _qAction;
    private InputAction _wAction;
    private InputAction _eAction;
    private InputAction _rAction;
    private InputAction _rightClickAction;
    private InputAction _mousePositionAction;
    private bool _initialized;

    /// <summary>Qキーがこのフレームに押されたか。</summary>
    public bool QPressedThisFrame => _qAction.WasPressedThisFrame();

    /// <summary>Qキーが押されている間true(長押しで射程円表示に使用)。</summary>
    public bool QPressed => _qAction.IsPressed();

    /// <summary>Wキーがこのフレームに押されたか。</summary>
    public bool WPressedThisFrame => _wAction.WasPressedThisFrame();

    /// <summary>Eキーがこのフレームに押されたか。</summary>
    public bool EPressedThisFrame => _eAction.WasPressedThisFrame();

    /// <summary>Rキーが押されている間true(長押しで射程円表示に使用)。</summary>
    public bool RPressed => _rAction.IsPressed();

    /// <summary>Rキーがこのフレームに離されたか(離して発動に使用)。</summary>
    public bool RReleasedThisFrame => _rAction.WasReleasedThisFrame();

    /// <summary>右クリックが押されている間true。</summary>
    public bool RightClickPressed => _rightClickAction.IsPressed();

    /// <summary>右クリックがこのフレームに押されたか。</summary>
    public bool RightClickPressedThisFrame => _rightClickAction.WasPressedThisFrame();

    /// <summary>マウスカーソルのスクリーン座標。</summary>
    public Vector2 MousePosition => _mousePositionAction.ReadValue<Vector2>();

    private void Awake()
    {
        InitializeActions();
    }

    private void OnEnable()
    {
        InitializeActions();
        EnableAll();
    }

    private void OnDisable()
    {
        DisableAll();
    }

    private void OnDestroy()
    {
        _qAction?.Dispose();
        _wAction?.Dispose();
        _eAction?.Dispose();
        _rAction?.Dispose();
        _rightClickAction?.Dispose();
        _mousePositionAction?.Dispose();
    }

    // AddComponent直後にプロパティが参照されても安全なよう、初期化は冪等にする。
    private void InitializeActions()
    {
        if (_initialized) return;
        _initialized = true;

        // キー割り当てを変える場合はここのバインディングを変更する。
        _qAction = new InputAction("SkillQ", InputActionType.Button, "<Keyboard>/q");
        _wAction = new InputAction("SkillW", InputActionType.Button, "<Keyboard>/w");
        _eAction = new InputAction("SkillE", InputActionType.Button, "<Keyboard>/e");
        _rAction = new InputAction("SkillR", InputActionType.Button, "<Keyboard>/r");
        _rightClickAction = new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position");
        EnableAll();
    }

    private void EnableAll()
    {
        _qAction?.Enable();
        _wAction?.Enable();
        _eAction?.Enable();
        _rAction?.Enable();
        _rightClickAction?.Enable();
        _mousePositionAction?.Enable();
    }

    private void DisableAll()
    {
        _qAction?.Disable();
        _wAction?.Disable();
        _eAction?.Disable();
        _rAction?.Disable();
        _rightClickAction?.Disable();
        _mousePositionAction?.Disable();
    }
}
