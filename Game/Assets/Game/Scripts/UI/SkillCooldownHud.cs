using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーHUD(スキルクールダウン + ステータスパネル)。Eternal Return / LoL風に画面下中央へ
/// HUDパネル(枠組み)を表示する。構成は左から: ステータス一覧 / ポートレート(レベルバッジ付き) /
/// スキルスロット(Q/W/E/R + 共通D/F)とその下のHPバー。
/// - スキルスロット: クールダウン中は暗転 + 時計回りのラジアルワイプ + 残り秒数(10秒未満は小数点1桁)を表示し、
///   完了時に白いフラッシュで通知する。発動中(W持続中・Eダッシュ中・R決闘エリア中・共通Dウィンドウ中)は
///   スロット枠をゴールドで強調する。
/// - ステータス: 攻撃力・攻撃速度・移動速度・攻撃射程をリアルタイム表示する。
///   移動速度・攻撃射程はGAME_DESIGN.mdと同じステータス単位(MS360・射程200など)へ換算して表示する。
/// - HPバー: 現在HP / 最大HPを緑のバーと数値で表示する(Eternal Returnの下部HPバー風)。
/// - ポートレート: アイコン未実装のためキャラクター名の頭文字を表示する。レベルバッジは
///   レベルシステム実装までのプレースホルダー(常に1)。
/// 見た目はCGアニメ調に合わせた濃紺ベース + 青アクセント。色・サイズはInspectorで調整できる。
///
/// UIはすべてコード生成(Screen Space Overlay)で、シーンやプレハブの追加は不要。
/// 空のGameObject(またはPlayer)にアタッチするだけで動作し、プレイヤーと各コンポーネントを自動検出する。
///
/// 既存のスキルコントローラーは変更せず、クールダウン値(_cooldownEndTime / _cooldown)と
/// 発動中フラグをリフレクションで参照する。対象フィールドが改名された場合は警告ログを出し、
/// そのスロットは常に使用可能表示になる(ゲーム進行には影響しない)。
/// </summary>
public sealed class SkillCooldownHud : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField, Min(16f)] private float _mainSlotSize = 64f;
    [SerializeField, Min(16f)] private float _subSlotSize = 52f;
    [SerializeField, Min(0f)] private float _slotSpacing = 8f;
    [SerializeField, Min(0f)] private float _groupGap = 20f;
    [SerializeField, Min(0f)] private float _bottomMargin = 24f;
    [SerializeField, Min(0f)] private float _panelPadding = 12f;
    [SerializeField, Min(0f)] private float _sectionGap = 12f;
    [SerializeField, Min(60f)] private float _statsBlockWidth = 210f;
    [SerializeField, Min(4f)] private float _healthBarHeight = 16f;

    [Header("Portrait")]
    // ポートレートアイコン未実装のため、キャラクター名の頭文字などを表示する。
    [SerializeField] private string _portraitLabel = "ゼ";
    // レベルシステム実装までのプレースホルダー表示。
    [SerializeField] private string _levelLabel = "1";

    [Header("Colors")]
    [SerializeField] private Color _panelBackgroundColor = new Color(0.03f, 0.05f, 0.10f, 0.86f);
    [SerializeField] private Color _panelBorderColor = new Color(0.25f, 0.40f, 0.65f, 0.9f);
    [SerializeField] private Color _slotBackgroundColor = new Color(0.05f, 0.08f, 0.14f, 0.92f);
    [SerializeField] private Color _slotBorderColor = new Color(0.35f, 0.55f, 0.85f, 0.9f);
    [SerializeField] private Color _activeBorderColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _keyLabelColor = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField] private Color _cooldownOverlayColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color _cooldownTextColor = new Color(1f, 0.96f, 0.85f, 1f);
    [SerializeField] private Color _mainAccentColor = new Color(0.24f, 0.61f, 1f, 1f);
    [SerializeField] private Color _dAccentColor = new Color(1f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color _fAccentColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _statLabelColor = new Color(0.55f, 0.66f, 0.82f, 1f);
    [SerializeField] private Color _statValueColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color _healthBarColor = new Color(0.25f, 0.80f, 0.35f, 1f);
    [SerializeField] private Color _healthBarBackgroundColor = new Color(0.08f, 0.12f, 0.16f, 0.95f);
    [SerializeField] private Color _healthTextColor = Color.white;
    [SerializeField] private Color _levelBadgeColor = new Color(0.92f, 0.76f, 0.28f, 1f);
    [SerializeField] private Color _levelTextColor = new Color(0.10f, 0.08f, 0.02f, 1f);

    [Header("Ready Flash")]
    [SerializeField, Min(0f)] private float _readyFlashDuration = 0.3f;

    private sealed class Slot
    {
        public string Key;
        public MonoBehaviour Controller;
        public FieldInfo CooldownEndTimeField;
        public FieldInfo CooldownField;
        public FieldInfo ActiveField;
        public Image Border;
        public Text KeyLabel;
        public Image CooldownOverlay;
        public Text CooldownText;
        public Image ReadyFlash;
        public bool WasOnCooldown;
        public float FlashEndTime;
        public bool WarnedMissingField;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private Font _font;
    private Canvas _canvas;

    // ステータス表示の参照元。プレイヤーから自動取得する。
    private CharacterStats _stats;
    private HealthController _health;

    // ステータスパネルのUI参照。
    private Text _attackDamageValue;
    private Text _attackSpeedValue;
    private Text _moveSpeedValue;
    private Text _attackRangeValue;
    private Image _healthFill;
    private Text _healthText;

    private void Start()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("ステータスHUD: プレイヤーが見つからないため、HUDを生成しません。", this);
            enabled = false;
            return;
        }

        _stats = player.GetComponent<CharacterStats>();
        _health = player.GetComponent<HealthController>();
        if (_stats == null)
        {
            Debug.LogWarning("ステータスHUD: CharacterStatsが見つからないため、ステータス値を表示できません。", this);
        }
        if (_health == null)
        {
            Debug.LogWarning("ステータスHUD: HealthControllerが見つからないため、HPバーを更新できません。", this);
        }

        CreateCanvas();
        BuildHud(player);
        Debug.Log($"ステータスHUD: 初期化しました(スロット数{_slots.Count})。", this);
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    private void Update()
    {
        foreach (Slot slot in _slots)
        {
            UpdateSlot(slot);
        }

        UpdateStatusPanel();
    }

    // プレイヤー本体を自動検出する(右クリック移動を持つオブジェクト = 操作キャラクター)。
    private GameObject FindPlayer()
    {
        PlayerClickMovement clickMovement = FindFirstObjectByType<PlayerClickMovement>();
        if (clickMovement != null) return clickMovement.gameObject;
        PlayerInputHub inputHub = FindFirstObjectByType<PlayerInputHub>();
        return inputHub != null ? inputHub.gameObject : null;
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Player Status HUD Canvas");
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    // HUD全体(枠組みパネルと各セクション)を生成する。
    private void BuildHud(GameObject player)
    {
        float skillBarWidth = _mainSlotSize * 4f + _slotSpacing * 4f + _groupGap + _subSlotSize * 2f;
        float innerHeight = _mainSlotSize + 6f + _healthBarHeight;
        float portraitSize = innerHeight;
        float panelWidth = _panelPadding * 2f + _statsBlockWidth + _sectionGap + portraitSize + _sectionGap + skillBarWidth;
        float panelHeight = innerHeight + _panelPadding * 2f;

        // パネルの枠組み(Eternal Return風の下部HUDフレーム)。
        GameObject panelObject = new GameObject("Status Panel", typeof(RectTransform));
        panelObject.transform.SetParent(_canvas.transform, false);
        RectTransform panelRect = (RectTransform)panelObject.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, _bottomMargin);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        Image panelBorder = panelObject.AddComponent<Image>();
        panelBorder.color = _panelBorderColor;
        panelBorder.raycastTarget = false;
        CreateInsetImage("Panel Background", panelObject.transform, _panelBackgroundColor, 1f);

        float x = _panelPadding;
        BuildStatsBlock(panelObject.transform, x, innerHeight);
        x += _statsBlockWidth + _sectionGap;
        BuildPortrait(panelObject.transform, x, portraitSize);
        x += portraitSize + _sectionGap;
        BuildHealthBar(panelObject.transform, x, skillBarWidth);
        BuildSkillBar(panelObject.transform, x, skillBarWidth, player);
    }

    // パネル内へ左下基準のセクション(RectTransformのみ)を生成する。
    private GameObject CreateSection(Transform parent, string name, float x, float width, float height)
    {
        GameObject sectionObject = new GameObject(name, typeof(RectTransform));
        sectionObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)sectionObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, _panelPadding);
        rect.sizeDelta = new Vector2(width, height);
        return sectionObject;
    }

    // 攻撃力・攻撃速度・移動速度・攻撃射程を縦に並べて表示する(Eternal Returnの左側ステータス欄風)。
    private void BuildStatsBlock(Transform parent, float x, float height)
    {
        GameObject blockObject = CreateSection(parent, "Stats Block", x, _statsBlockWidth, height);
        float rowHeight = height / 4f;
        _attackDamageValue = CreateStatRow(blockObject.transform, 0, rowHeight, "攻撃力");
        _attackSpeedValue = CreateStatRow(blockObject.transform, 1, rowHeight, "攻撃速度");
        _moveSpeedValue = CreateStatRow(blockObject.transform, 2, rowHeight, "移動速度");
        _attackRangeValue = CreateStatRow(blockObject.transform, 3, rowHeight, "攻撃射程");
    }

    // ステータス1行(左: 項目名 / 右: 値)を生成し、値のTextを返す。
    private Text CreateStatRow(Transform parent, int rowIndex, float rowHeight, string label)
    {
        GameObject rowObject = new GameObject($"Stat Row {label}", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)rowObject.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -rowIndex * rowHeight);
        rect.sizeDelta = new Vector2(0f, rowHeight);

        Text labelText = CreateText("Label", rowObject.transform, label, 14, _statLabelColor);
        labelText.alignment = TextAnchor.MiddleLeft;
        Text valueText = CreateText("Value", rowObject.transform, "-", 15, _statValueColor);
        valueText.alignment = TextAnchor.MiddleRight;
        return valueText;
    }

    // ポートレート枠。アイコン未実装のため頭文字を表示し、左下へレベルバッジを重ねる。
    private void BuildPortrait(Transform parent, float x, float size)
    {
        GameObject portraitObject = CreateSection(parent, "Portrait", x, size, size);
        Image border = portraitObject.AddComponent<Image>();
        border.color = _slotBorderColor;
        border.raycastTarget = false;
        Image background = CreateInsetImage("Background", portraitObject.transform, _slotBackgroundColor, 2f);
        CreateText("Portrait Label", background.transform, _portraitLabel, Mathf.RoundToInt(size * 0.42f), _keyLabelColor);

        // レベルバッジ(レベルシステム実装までのプレースホルダー)。
        GameObject badgeObject = new GameObject("Level Badge", typeof(RectTransform));
        badgeObject.transform.SetParent(portraitObject.transform, false);
        RectTransform badgeRect = (RectTransform)badgeObject.transform;
        badgeRect.anchorMin = Vector2.zero;
        badgeRect.anchorMax = Vector2.zero;
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(2f, 2f);
        badgeRect.sizeDelta = new Vector2(24f, 24f);
        Image badge = badgeObject.AddComponent<Image>();
        badge.color = _levelBadgeColor;
        badge.raycastTarget = false;
        Text levelText = CreateText("Level", badgeObject.transform, _levelLabel, 14, _levelTextColor);
        levelText.fontStyle = FontStyle.Bold;
    }

    // HPバー(緑)。現在HP / 最大HPを数値でも表示する。
    private void BuildHealthBar(Transform parent, float x, float width)
    {
        GameObject barObject = CreateSection(parent, "Health Bar", x, width, _healthBarHeight);
        Image background = barObject.AddComponent<Image>();
        background.color = _healthBarBackgroundColor;
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        _healthFill = fillObject.AddComponent<Image>();
        _healthFill.color = _healthBarColor;
        _healthFill.raycastTarget = false;
        _healthFill.type = Image.Type.Filled;
        _healthFill.fillMethod = Image.FillMethod.Horizontal;
        _healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _healthFill.fillAmount = 1f;

        _healthText = CreateText("Health Text", barObject.transform, "", 13, _healthTextColor);
    }

    // スキルスロット列(Q/W/E/R + 共通D/F)を生成する。
    private void BuildSkillBar(Transform parent, float x, float width, GameObject player)
    {
        GameObject container = new GameObject("Skill Bar", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        RectTransform containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.zero;
        containerRect.pivot = Vector2.zero;
        containerRect.anchoredPosition = new Vector2(x, _panelPadding + _healthBarHeight + 6f);
        containerRect.sizeDelta = new Vector2(width, _mainSlotSize);

        float slotX = 0f;
        slotX = AddSlot(container.transform, slotX, "Q", player.GetComponent<ZelfQController>(), null, _mainAccentColor, _mainSlotSize) + _slotSpacing;
        slotX = AddSlot(container.transform, slotX, "W", player.GetComponent<ZelfWController>(), "_isWActive", _mainAccentColor, _mainSlotSize) + _slotSpacing;
        slotX = AddSlot(container.transform, slotX, "E", player.GetComponent<ZelfEController>(), "_isDashing", _mainAccentColor, _mainSlotSize) + _slotSpacing;
        slotX = AddSlot(container.transform, slotX, "R", player.GetComponent<ZelfRController>(), "_isRActive", _mainAccentColor, _mainSlotSize) + _groupGap;
        slotX = AddSlot(container.transform, slotX, "D", player.GetComponent<CommonDController>(), "_isWindowActive", _dAccentColor, _subSlotSize) + _slotSpacing;
        AddSlot(container.transform, slotX, "F", player.GetComponent<FlashController>(), null, _fAccentColor, _subSlotSize);
    }

    // スロット1つを生成して登録する。戻り値はスロット右端のX座標。
    private float AddSlot(Transform parent, float x, string key, MonoBehaviour controller, string activeFieldName, Color accentColor, float size)
    {
        if (controller == null)
        {
            Debug.LogWarning($"ステータスHUD: {key}のコントローラーが見つからないため、スロットを表示しません。", this);
            return x + size;
        }

        // 枠(ボーダー)。背景より一回り大きいImageを枠として使う。
        GameObject slotObject = new GameObject($"Skill Slot {key}", typeof(RectTransform));
        slotObject.transform.SetParent(parent, false);
        RectTransform slotRect = (RectTransform)slotObject.transform;
        slotRect.anchorMin = Vector2.zero;
        slotRect.anchorMax = Vector2.zero;
        slotRect.pivot = Vector2.zero;
        slotRect.anchoredPosition = new Vector2(x, 0f);
        slotRect.sizeDelta = new Vector2(size, size);
        Image border = slotObject.AddComponent<Image>();
        border.color = _slotBorderColor;
        border.raycastTarget = false;

        Image background = CreateInsetImage("Background", slotObject.transform, _slotBackgroundColor, 2f);

        // スキルアクセント(スロット上端のライン)。Eternal Return風の差し色。
        GameObject accentObject = new GameObject("Accent", typeof(RectTransform));
        accentObject.transform.SetParent(slotObject.transform, false);
        RectTransform accentRect = (RectTransform)accentObject.transform;
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(2f, -5f);
        accentRect.offsetMax = new Vector2(-2f, -2f);
        Image accent = accentObject.AddComponent<Image>();
        accent.color = accentColor;
        accent.raycastTarget = false;

        // キー文字(アイコン未実装のためキー名をアイコン代わりに中央表示)。
        Text keyLabel = CreateText("Key Label", background.transform, key, Mathf.RoundToInt(size * 0.46f), _keyLabelColor);

        // クールダウンのラジアルワイプ(時計回りに残量を表示するLoL風の暗転オーバーレイ)。
        Image cooldownOverlay = CreateInsetImage("Cooldown Overlay", slotObject.transform, _cooldownOverlayColor, 2f);
        cooldownOverlay.type = Image.Type.Filled;
        cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        cooldownOverlay.fillClockwise = true;
        cooldownOverlay.fillAmount = 0f;
        cooldownOverlay.enabled = false;

        // 残り秒数テキスト。
        Text cooldownText = CreateText("Cooldown Text", slotObject.transform, "", Mathf.RoundToInt(size * 0.40f), _cooldownTextColor);
        cooldownText.enabled = false;

        // クールダウン完了フラッシュ。
        Image readyFlash = CreateInsetImage("Ready Flash", slotObject.transform, new Color(1f, 1f, 1f, 0f), 2f);

        Type controllerType = controller.GetType();
        Slot slot = new Slot
        {
            Key = key,
            Controller = controller,
            CooldownEndTimeField = controllerType.GetField("_cooldownEndTime", BindingFlags.Instance | BindingFlags.NonPublic),
            CooldownField = controllerType.GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic),
            ActiveField = string.IsNullOrEmpty(activeFieldName) ? null : controllerType.GetField(activeFieldName, BindingFlags.Instance | BindingFlags.NonPublic),
            Border = border,
            KeyLabel = keyLabel,
            CooldownOverlay = cooldownOverlay,
            CooldownText = cooldownText,
            ReadyFlash = readyFlash,
        };
        _slots.Add(slot);
        return x + size;
    }

    private void UpdateSlot(Slot slot)
    {
        if (slot.Controller == null) return;

        float remaining = 0f;
        float maxCooldown = 0f;
        if (slot.CooldownEndTimeField != null && slot.CooldownField != null)
        {
            float endTime = (float)slot.CooldownEndTimeField.GetValue(slot.Controller);
            maxCooldown = (float)slot.CooldownField.GetValue(slot.Controller);
            remaining = Mathf.Max(0f, endTime - Time.time);
        }
        else if (!slot.WarnedMissingField)
        {
            slot.WarnedMissingField = true;
            Debug.LogWarning($"ステータスHUD: {slot.Key}のクールダウンフィールド(_cooldownEndTime/_cooldown)が見つかりません。フィールド名の変更に合わせてSkillCooldownHudも更新してください。", this);
        }

        bool onCooldown = remaining > 0.05f;

        // ラジアルワイプ: 残り割合に応じて時計回りに減っていく。
        slot.CooldownOverlay.enabled = onCooldown;
        if (onCooldown && maxCooldown > 0f)
        {
            slot.CooldownOverlay.fillAmount = Mathf.Clamp01(remaining / maxCooldown);
        }

        // 残り秒数: 10秒以上は整数(切り上げ)、10秒未満は小数点1桁。
        slot.CooldownText.enabled = onCooldown;
        if (onCooldown)
        {
            slot.CooldownText.text = remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("F1");
        }

        // キー文字はクールダウン中は薄くする。
        Color keyColor = _keyLabelColor;
        keyColor.a = onCooldown ? 0.28f : 1f;
        slot.KeyLabel.color = keyColor;

        // クールダウン完了の瞬間に白フラッシュを開始する。
        if (slot.WasOnCooldown && !onCooldown)
        {
            slot.FlashEndTime = Time.time + _readyFlashDuration;
        }
        slot.WasOnCooldown = onCooldown;

        float flashRemaining = slot.FlashEndTime - Time.time;
        Color flashColor = Color.white;
        flashColor.a = _readyFlashDuration > 0f && flashRemaining > 0f
            ? Mathf.Clamp01(flashRemaining / _readyFlashDuration) * 0.85f
            : 0f;
        slot.ReadyFlash.color = flashColor;

        // 発動中は枠を強調する。
        bool isActive = slot.ActiveField != null && (bool)slot.ActiveField.GetValue(slot.Controller);
        slot.Border.color = isActive ? _activeBorderColor : _slotBorderColor;
    }

    // ステータスパネル(HPバー・各ステータス値)を毎フレーム最新値へ更新する。
    private void UpdateStatusPanel()
    {
        if (_health != null && _healthFill != null)
        {
            float max = Mathf.Max(1f, _health.MaxHealth);
            _healthFill.fillAmount = Mathf.Clamp01(_health.CurrentHealth / max);
            _healthText.text = $"{Mathf.CeilToInt(_health.CurrentHealth)} / {Mathf.CeilToInt(max)}";
        }

        if (_stats != null)
        {
            // 移動速度・攻撃射程はGAME_DESIGN.mdと同じステータス単位(MS360・射程200など)で表示する。
            if (_attackDamageValue != null) _attackDamageValue.text = _stats.CurrentAttackDamage.ToString("F0");
            if (_attackSpeedValue != null) _attackSpeedValue.text = _stats.CurrentAttackSpeed.ToString("F2");
            if (_moveSpeedValue != null) _moveSpeedValue.text = (_stats.CurrentMoveSpeed * CharacterStats.MoveSpeedStatPerUnityUnit).ToString("F0");
            if (_attackRangeValue != null) _attackRangeValue.text = (_stats.CurrentAttackRange * CharacterStats.RangeStatPerUnityUnit).ToString("F0");
        }
    }

    // 親の内側にinsetピクセル分小さく張ったImageを生成する。
    private static Image CreateInsetImage(string name, Transform parent, Color color, float inset)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, Transform parent, string text, int fontSize, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text label = textObject.AddComponent<Text>();
        label.font = GetFont();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private Font GetFont()
    {
        if (_font == null)
        {
            // CombatTextManagerと同じく、外部フォントは追加せずUnity組み込みのLegacyRuntimeフォントを使用する。
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return _font;
    }
}
