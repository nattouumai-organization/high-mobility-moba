using System.Reflection;
using UnityEngine;

/// <summary>
/// スキル射程・範囲のプレビューを追加表示するコンポーネント(フェーズ3)。
/// 各スキルコントローラーの既存プレビュー(Qの射程円・W/Eの方向線・Rの射程円)に加えて、キー長押し中に以下を表示する。
/// - Q: マウス下の有効な対象に発動対象マーカー(射程内: 白 / 射程外: オレンジ=自動接近)。
/// - R: マウス下の有効な対象に、発動した場合の決闘エリア円(射程内: 紫 / 射程外: オレンジ=自動接近)。
/// Fフラッシュは押した瞬間に発動する仕様のためプレビューは表示しない。
///
/// W/Eのプレビューは各コントローラーの既存表示(方向線)のままとし、このコンポーネントでは扱わない。
/// 既存のスキルコントローラーは変更せず、設定値(_targetRange / _castRange / _arenaRadius /
/// _isRActive)をリフレクションで参照する。フィールドが見つからない場合は
/// 警告ログを出し、そのスキルのプレビューだけ無効になる。
/// 空のGameObject(またはPlayer)にアタッチするだけで動作し、プレイヤーを自動検出する。
/// </summary>
public sealed class SkillRangePreview : MonoBehaviour
{
    [Header("Q Target Marker")]
    [SerializeField, Min(0.05f)] private float _qMarkerRadius = 0.6f;
    [SerializeField] private Color _qInRangeColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color _qOutOfRangeColor = new Color(1f, 0.6f, 0.15f, 0.95f);

    [Header("R Arena Preview")]
    [SerializeField] private Color _rInRangeColor = new Color(0.75f, 0.25f, 1f, 0.8f);
    [SerializeField] private Color _rOutOfRangeColor = new Color(1f, 0.6f, 0.15f, 0.8f);

    private GameObject _player;
    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private HealthController _health;
    private Camera _mainCamera;
    private LayerMask _targetableLayer;
    private float _circleYOffset;

    private ZelfQController _qController;
    private ZelfRController _rController;

    private FieldInfo _qTargetRangeField;
    private FieldInfo _rCastRangeField;
    private FieldInfo _rArenaRadiusField;
    private FieldInfo _rIsActiveField;

    private SkillRangeIndicator _qIndicator;
    private SkillRangeIndicator _rIndicator;

    private void Start()
    {
        _player = FindPlayer();
        if (_player == null)
        {
            Debug.LogWarning("スキルプレビュー: プレイヤーが見つからないため、プレビューを生成しません。", this);
            enabled = false;
            return;
        }

        _inputHub = _player.GetComponent<PlayerInputHub>();
        _abilityLock = _player.GetComponent<AbilityLockController>();
        _health = _player.GetComponent<HealthController>();
        _qController = _player.GetComponent<ZelfQController>();
        _rController = _player.GetComponent<ZelfRController>();
        _mainCamera = Camera.main;

        if (_qController != null) _targetableLayer = _qController.TargetableLayerMask;

        CharacterController characterController = _player.GetComponent<CharacterController>();
        _circleYOffset = characterController != null
            ? characterController.center.y - characterController.height * 0.5f + 0.05f
            : 0.05f;

        _qTargetRangeField = FindField(_qController, "_targetRange");
        _rCastRangeField = FindField(_rController, "_castRange");
        _rArenaRadiusField = FindField(_rController, "_arenaRadius");
        _rIsActiveField = FindField(_rController, "_isRActive");

        if (_qController != null) _qIndicator = SkillRangeIndicator.Create(_player.transform, "Q Cast Preview");
        if (_rController != null) _rIndicator = SkillRangeIndicator.Create(_player.transform, "R Arena Preview");

        Debug.Log("スキルプレビュー: 初期化しました。", this);
    }

    private void Update()
    {
        bool blocked = _inputHub == null
            || (_health != null && _health.IsDead)
            || (_abilityLock != null && _abilityLock.IsLocked);
        UpdateQPreview(blocked);
        UpdateRPreview(blocked);
    }

    // プレイヤー本体を自動検出する(右クリック移動を持つオブジェクト = 操作キャラクター)。
    private GameObject FindPlayer()
    {
        PlayerClickMovement clickMovement = FindFirstObjectByType<PlayerClickMovement>();
        if (clickMovement != null) return clickMovement.gameObject;
        PlayerInputHub inputHub = FindFirstObjectByType<PlayerInputHub>();
        return inputHub != null ? inputHub.gameObject : null;
    }

    // Q: マウス下の有効な対象に発動対象マーカーを表示する(同一対象ロック中は表示しない)。
    private void UpdateQPreview(bool blocked)
    {
        if (_qIndicator == null) return;
        bool visible = !blocked && _qController != null && _inputHub.QPressed && !_qController.IsCurrentTargetLocked;
        if (visible && TryGetTargetUnderMouse(out Targetable target) && IsValidQTarget(target))
        {
            float range = ReadFloat(_qController, _qTargetRangeField, 4.5f);
            Color color = IsWithinRange(target, range) ? _qInRangeColor : _qOutOfRangeColor;
            _qIndicator.ShowPointMarker(GetGroundPosition(target), _qMarkerRadius, color);
            return;
        }
        _qIndicator.HideAll();
    }

    // R: マウス下の有効な対象に、発動した場合の決闘エリア円をプレビュー表示する。
    private void UpdateRPreview(bool blocked)
    {
        if (_rIndicator == null) return;
        bool isRActive = ReadBool(_rController, _rIsActiveField, false);
        bool visible = !blocked && _rController != null && _inputHub.RPressed && !isRActive;
        if (visible && TryGetTargetUnderMouse(out Targetable target) && IsValidRTarget(target))
        {
            float castRange = ReadFloat(_rController, _rCastRangeField, 7f);
            float arenaRadius = ReadFloat(_rController, _rArenaRadiusField, 5f);
            Color color = IsWithinRange(target, castRange) ? _rInRangeColor : _rOutOfRangeColor;
            _rIndicator.ShowPointMarker(GetGroundPosition(target), arenaRadius, color);
            return;
        }
        _rIndicator.HideAll();
    }

    // Q対象: Tower分類以外の生存Targetable(ZelfQControllerと同じ判定)。
    private static bool IsValidQTarget(Targetable target)
    {
        return IsAlive(target) && target.Classification != TargetClassification.Tower;
    }

    // R対象: Character/TrainingDummy分類の生存Targetable(ZelfRControllerと同じ判定)。
    private static bool IsValidRTarget(Targetable target)
    {
        return IsAlive(target) &&
            (target.Classification == TargetClassification.Character ||
             target.Classification == TargetClassification.TrainingDummy);
    }

    private static bool IsAlive(Targetable target)
    {
        if (target == null || !target.isActiveAndEnabled || target.IsDead) return false;
        HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
        return health != null && !health.IsDead;
    }

    private bool IsWithinRange(Targetable target, float range)
    {
        Vector3 difference = target.GetClosestPoint(_player.transform.position) - _player.transform.position;
        difference.y = 0f;
        return difference.sqrMagnitude <= range * range;
    }

    // マーカーの接地位置: 対象コライダーの下端(なければ対象位置)を使う。
    private static Vector3 GetGroundPosition(Targetable target)
    {
        Vector3 position = target.transform.position;
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) position.y = collider.bounds.min.y;
        return position;
    }

    private bool TryGetTargetUnderMouse(out Targetable target)
    {
        target = null;
        if (_inputHub == null || _targetableLayer.value == 0) return false;
        if (_mainCamera == null) { _mainCamera = Camera.main; if (_mainCamera == null) return false; }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _targetableLayer, QueryTriggerInteraction.Ignore)) return false;
        target = hit.collider.GetComponentInParent<Targetable>();
        return target != null;
    }

    private FieldInfo FindField(MonoBehaviour controller, string fieldName)
    {
        if (controller == null) return null;
        FieldInfo field = controller.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Debug.LogWarning($"スキルプレビュー: {controller.GetType().Name}のフィールド({fieldName})が見つかりません。フィールド名の変更に合わせてSkillRangePreviewも更新してください。", this);
        }
        return field;
    }

    private static float ReadFloat(MonoBehaviour controller, FieldInfo field, float fallback)
    {
        return controller != null && field != null ? (float)field.GetValue(controller) : fallback;
    }

    private static bool ReadBool(MonoBehaviour controller, FieldInfo field, bool fallback)
    {
        return controller != null && field != null ? (bool)field.GetValue(controller) : fallback;
    }
}
