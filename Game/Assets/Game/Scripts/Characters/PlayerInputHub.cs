using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player入力の一元管理(InputAction)。Q/W/E/R・停止コマンド(S)・右クリック・マウス座標を公開する。
/// 各コントローラーのAwakeからget-or-addで自動追加されるため、Inspector設定は不要。
/// 将来のキーコンフィグ・ゲームパッド対応は、このクラスのバインディング変更のみで行う。
/// </summary>
public sealed class PlayerInputHub : MonoBehaviour
{
    private InputAction _qAction;
    private InputAction _wAction;
    private InputAction _eAction;
    private InputAction _rAction;
    private InputAction _sAction;
    private InputAction _dAction;
    private InputAction _rightClickAction;
    private InputAction _mousePositionAction;

    private bool _initialized;

    // --- Q ---
    public bool QPressedThisFrame => _qAction != null && _qAction.WasPressedThisFrame();
    public bool QPressed => _qAction != null && _qAction.IsPressed();
    public bool QReleasedThisFrame => _qAction != null && _qAction.WasReleasedThisFrame();

    // --- W ---
    public bool WPressedThisFrame => _wAction != null && _wAction.WasPressedThisFrame();
    public bool WPressed => _wAction != null && _wAction.IsPressed();
    public bool WReleasedThisFrame => _wAction != null && _wAction.WasReleasedThisFrame();

    // --- E ---
    public bool EPressedThisFrame => _eAction != null && _eAction.WasPressedThisFrame();
    public bool EPressed => _eAction != null && _eAction.IsPressed();
    public bool EReleasedThisFrame => _eAction != null && _eAction.WasReleasedThisFrame();

    // --- R ---
    public bool RPressedThisFrame => _rAction != null && _rAction.WasPressedThisFrame();
    public bool RPressed => _rAction != null && _rAction.IsPressed();
    public bool RReleasedThisFrame => _rAction != null && _rAction.WasReleasedThisFrame();

    // --- 停止コマンド(S) ---
    public bool SPressedThisFrame => _sAction != null && _sAction.WasPressedThisFrame();

    // --- 共通D(カウンター) ---
    public bool DPressedThisFrame => _dAction != null && _dAction.WasPressedThisFrame();

    // --- マウス ---
    public bool RightClickPressed => _rightClickAction != null && _rightClickAction.IsPressed();
    public bool RightClickPressedThisFrame => _rightClickAction != null && _rightClickAction.WasPressedThisFrame();
    public Vector2 MousePosition => _mousePositionAction != null ? _mousePositionAction.ReadValue<Vector2>() : Vector2.zero;

    private void Awake()
    {
        InitializeActions();
    }

    private void OnEnable()
    {
        InitializeActions();
        _qAction.Enable();
        _wAction.Enable();
        _eAction.Enable();
        _rAction.Enable();
        _sAction.Enable();
        _dAction.Enable();
        _rightClickAction.Enable();
        _mousePositionAction.Enable();
    }

    private void OnDisable()
    {
        _qAction?.Disable();
        _wAction?.Disable();
        _eAction?.Disable();
        _rAction?.Disable();
        _sAction?.Disable();
        _dAction?.Disable();
        _rightClickAction?.Disable();
        _mousePositionAction?.Disable();
    }

    private void OnDestroy()
    {
        _qAction?.Dispose();
        _wAction?.Dispose();
        _eAction?.Dispose();
        _rAction?.Dispose();
        _sAction?.Dispose();
        _dAction?.Dispose();
        _rightClickAction?.Dispose();
        _mousePositionAction?.Dispose();
    }

    // アクション生成は冪等。どのコンポーネントから先に呼ばれても安全。
    private void InitializeActions()
    {
        if (_initialized) return;
        _initialized = true;

        _qAction = new InputAction("SkillQ", InputActionType.Button, "<Keyboard>/q");
        _wAction = new InputAction("SkillW", InputActionType.Button, "<Keyboard>/w");
        _eAction = new InputAction("SkillE", InputActionType.Button, "<Keyboard>/e");
        _rAction = new InputAction("SkillR", InputActionType.Button, "<Keyboard>/r");
        _sAction = new InputAction("StopCommand", InputActionType.Button, "<Keyboard>/s");
        _dAction = new InputAction("CommonD", InputActionType.Button, "<Keyboard>/d");
        _rightClickAction = new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position");
    }
}
