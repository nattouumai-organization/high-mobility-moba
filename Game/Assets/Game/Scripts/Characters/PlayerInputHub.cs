using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player入力の一元管理(InputAction)。Q/W/E/R・停止コマンド(S)・共通D・F(フラッシュ)・
/// カメラ操作(Space: プレイヤー中心 / Y: カメラモード切替)・右クリック・マウス座標を公開する。
/// 各コントローラーのAwakeからget-or-addで自動追加されるため、Inspector設定は不要。
/// 将来のキーコンフィグ・ゲームパッド対応は、このクラスのバインディング変更のみで行う。
/// フェーズ7: スキル強化用の修飾キー(左右Ctrl)を追加。Ctrl押下中はQ/W/E/Rの通常スキル入力を
/// 抑制し(Pressed/PressedThisFrameがfalseになる)、Ctrl+スキルキーはUpgrade*PressedThisFrameとして
/// HeroSkillUpgradesが強化操作に使用する(ReleasedThisFrameは抑制しないため、押下済みの
/// ホールド系スキルは通常どおり解放できる)。
/// </summary>
public sealed class PlayerInputHub : MonoBehaviour
{
    private InputAction _qAction;
    private InputAction _wAction;
    private InputAction _eAction;
    private InputAction _rAction;
    private InputAction _sAction;
    private InputAction _dAction;
    private InputAction _fAction;
    private InputAction _cameraCenterAction;
    private InputAction _cameraLockToggleAction;
    private InputAction _rightClickAction;
    private InputAction _mousePositionAction;
    private InputAction _upgradeModifierAction;

    private bool _initialized;
    private bool _qPressedWithModifier;
    private bool _wPressedWithModifier;
    private bool _ePressedWithModifier;
    private bool _rPressedWithModifier;

    // --- スキル強化修飾キー(Ctrl)とCtrl+スキルキー(フェーズ7) ---
    public bool UpgradeModifierPressed => _upgradeModifierAction != null && _upgradeModifierAction.IsPressed();
    public bool UpgradeQPressedThisFrame => UpgradeModifierPressed && _qAction != null && _qAction.WasPressedThisFrame();
    public bool UpgradeWPressedThisFrame => UpgradeModifierPressed && _wAction != null && _wAction.WasPressedThisFrame();
    public bool UpgradeEPressedThisFrame => UpgradeModifierPressed && _eAction != null && _eAction.WasPressedThisFrame();
    public bool UpgradeRPressedThisFrame => UpgradeModifierPressed && _rAction != null && _rAction.WasPressedThisFrame();

    // --- Q ---
    public bool QPressedThisFrame => !UpgradeModifierPressed && _qAction != null && _qAction.WasPressedThisFrame();
    public bool QPressed => !UpgradeModifierPressed && _qAction != null && _qAction.IsPressed();
    public bool QReleasedThisFrame => !_qPressedWithModifier && _qAction != null && _qAction.WasReleasedThisFrame();

    // --- W ---
    public bool WPressedThisFrame => !UpgradeModifierPressed && _wAction != null && _wAction.WasPressedThisFrame();
    public bool WPressed => !UpgradeModifierPressed && _wAction != null && _wAction.IsPressed();
    public bool WReleasedThisFrame => !_wPressedWithModifier && _wAction != null && _wAction.WasReleasedThisFrame();

    // --- E ---
    public bool EPressedThisFrame => !UpgradeModifierPressed && _eAction != null && _eAction.WasPressedThisFrame();
    public bool EPressed => !UpgradeModifierPressed && _eAction != null && _eAction.IsPressed();
    public bool EReleasedThisFrame => !_ePressedWithModifier && _eAction != null && _eAction.WasReleasedThisFrame();

    // --- R ---
    public bool RPressedThisFrame => !UpgradeModifierPressed && _rAction != null && _rAction.WasPressedThisFrame();
    public bool RPressed => !UpgradeModifierPressed && _rAction != null && _rAction.IsPressed();
    public bool RReleasedThisFrame => !_rPressedWithModifier && _rAction != null && _rAction.WasReleasedThisFrame();

    // --- 停止コマンド(S) ---
    public bool SPressedThisFrame => _sAction != null && _sAction.WasPressedThisFrame();

    // --- 共通D(カウンター) ---
    public bool DPressedThisFrame => _dAction != null && _dAction.WasPressedThisFrame();

    // --- F(フラッシュ) ---
    public bool FPressedThisFrame => _fAction != null && _fAction.WasPressedThisFrame();
    public bool FPressed => _fAction != null && _fAction.IsPressed();
    public bool FReleasedThisFrame => _fAction != null && _fAction.WasReleasedThisFrame();

    // --- カメラ(Space: 押している間プレイヤー中心 / Y: カメラモード切替) ---
    public bool CameraCenterPressed => _cameraCenterAction != null && _cameraCenterAction.IsPressed();
    public bool CameraCenterPressedThisFrame => _cameraCenterAction != null && _cameraCenterAction.WasPressedThisFrame();
    public bool CameraLockTogglePressedThisFrame => _cameraLockToggleAction != null && _cameraLockToggleAction.WasPressedThisFrame();

    // --- マウス ---
    public bool RightClickPressed => _rightClickAction != null && _rightClickAction.IsPressed();
    public bool RightClickPressedThisFrame => _rightClickAction != null && _rightClickAction.WasPressedThisFrame();
    public Vector2 MousePosition => _mousePositionAction != null ? _mousePositionAction.ReadValue<Vector2>() : Vector2.zero;

    private void Update()
    {
        if (_qAction != null && _qAction.WasPressedThisFrame()) _qPressedWithModifier = UpgradeModifierPressed;
        if (_wAction != null && _wAction.WasPressedThisFrame()) _wPressedWithModifier = UpgradeModifierPressed;
        if (_eAction != null && _eAction.WasPressedThisFrame()) _ePressedWithModifier = UpgradeModifierPressed;
        if (_rAction != null && _rAction.WasPressedThisFrame()) _rPressedWithModifier = UpgradeModifierPressed;
    }

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
        _fAction.Enable();
        _cameraCenterAction.Enable();
        _cameraLockToggleAction.Enable();
        _rightClickAction.Enable();
        _mousePositionAction.Enable();
        _upgradeModifierAction.Enable();
    }

    private void OnDisable()
    {
        _qAction?.Disable();
        _wAction?.Disable();
        _eAction?.Disable();
        _rAction?.Disable();
        _sAction?.Disable();
        _dAction?.Disable();
        _fAction?.Disable();
        _cameraCenterAction?.Disable();
        _cameraLockToggleAction?.Disable();
        _rightClickAction?.Disable();
        _mousePositionAction?.Disable();
        _upgradeModifierAction?.Disable();
    }

    private void OnDestroy()
    {
        _qAction?.Dispose();
        _wAction?.Dispose();
        _eAction?.Dispose();
        _rAction?.Dispose();
        _sAction?.Dispose();
        _dAction?.Dispose();
        _fAction?.Dispose();
        _cameraCenterAction?.Dispose();
        _cameraLockToggleAction?.Dispose();
        _rightClickAction?.Dispose();
        _mousePositionAction?.Dispose();
        _upgradeModifierAction?.Dispose();
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
        _fAction = new InputAction("Flash", InputActionType.Button, "<Keyboard>/f");
        _cameraCenterAction = new InputAction("CameraCenter", InputActionType.Button, "<Keyboard>/space");
        _cameraLockToggleAction = new InputAction("CameraLockToggle", InputActionType.Button, "<Keyboard>/y");
        _rightClickAction = new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position");
        _upgradeModifierAction = new InputAction("UpgradeModifier", InputActionType.Button, "<Keyboard>/leftCtrl");
        _upgradeModifierAction.AddBinding("<Keyboard>/rightCtrl");
    }
}
