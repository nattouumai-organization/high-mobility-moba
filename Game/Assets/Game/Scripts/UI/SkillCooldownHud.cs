using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// クールダウンUI(スキルHUD)。LoL / Eternal Return風に画面下中央へスキルスロットを並べる。
/// 構成: Q / W / E / R(大スロット) + 共通D / F(小スロット。LoLのサモナースペル風)。
/// - クールダウン中: スロットを暗くし、時計回りのラジアルワイプと残り秒数(10秒未満は小数点1桁)を表示する。
/// - クールダウン完了時: 白いフラッシュで完了を通知する。
/// - スキル発動中(W持続中・Eダッシュ中・R決闘エリア中・共通Dウィンドウ中): スロット枠を強調する。
/// 見た目はCGアニメ調に合わせた濃紺ベース+青アクセント。色・サイズはInspectorで調整できる。
///
/// UIはすべてコード生成(Screen Space Overlay)で、シーンやプレハブの追加は不要。
/// 空のGameObject(またはPlayer)にアタッチするだけで動作し、プレイヤーと各スキルコントローラーを自動検出する。
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

    [Header("Colors")]
    [SerializeField] private Color _slotBackgroundColor = new Color(0.05f, 0.08f, 0.14f, 0.92f);
    [SerializeField] private Color _slotBorderColor = new Color(0.35f, 0.55f, 0.85f, 0.9f);
    [SerializeField] private Color _activeBorderColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _keyLabelColor = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField] private Color _cooldownOverlayColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color _cooldownTextColor = new Color(1f, 0.96f, 0.85f, 1f);
    [SerializeField] private Color _mainAccentColor = new Color(0.24f, 0.61f, 1f, 1f);
    [SerializeField] private Color _dAccentColor = new Color(1f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color _fAccentColor = new Color(1f, 0.85f, 0.3f, 1f);

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

    private void Start()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("クールダウンUI: プレイヤーが見つからないため、HUDを生成しません。", this);
            enabled = false;
            return;
        }

        CreateCanvas();
        BuildSlots(player);
        Debug.Log($"クールダウンUI: 初期化しました(スロット数{_slots.Count})。", this);
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
        GameObject canvasObject = new GameObject("Skill Cooldown HUD Canvas");
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildSlots(GameObject player)
    {
        float totalWidth = _mainSlotSize * 4f + _slotSpacing * 4f + _groupGap + _subSlotSize * 2f;

        GameObject container = new GameObject("Skill Bar", typeof(RectTransform));
        container.transform.SetParent(_canvas.transform, false);
        RectTransform containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, _bottomMargin);
        containerRect.sizeDelta = new Vector2(totalWidth, _mainSlotSize);

        float x = 0f;
        x = AddSlot(container.transform, x, "Q", player.GetComponent<ZelfQController>(), null, _mainAccentColor, _mainSlotSize) + _slotSpacing;
        x = AddSlot(container.transform, x, "W", player.GetComponent<ZelfWController>(), "_isWActive", _mainAccentColor, _mainSlotSize) + _slotSpacing;
        x = AddSlot(container.transform, x, "E", player.GetComponent<ZelfEController>(), "_isDashing", _mainAccentColor, _mainSlotSize) + _slotSpacing;
        x = AddSlot(container.transform, x, "R", player.GetComponent<ZelfRController>(), "_isRActive", _mainAccentColor, _mainSlotSize) + _groupGap;
        x = AddSlot(container.transform, x, "D", player.GetComponent<CommonDController>(), "_isWindowActive", _dAccentColor, _subSlotSize) + _slotSpacing;
        AddSlot(container.transform, x, "F", player.GetComponent<FlashController>(), null, _fAccentColor, _subSlotSize);
    }

    // スロット1つを生成して登録する。戻り値はスロット右端のX座標。
    private float AddSlot(Transform parent, float x, string key, MonoBehaviour controller, string activeFieldName, Color accentColor, float size)
    {
        if (controller == null)
        {
            Debug.LogWarning($"クールダウンUI: {key}のコントローラーが見つからないため、スロットを表示しません。", this);
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
            Debug.LogWarning($"クールダウンUI: {slot.Key}のクールダウンフィールド(_cooldownEndTime/_cooldown)が見つかりません。フィールド名の変更に合わせてSkillCooldownHudも更新してください。", this);
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
