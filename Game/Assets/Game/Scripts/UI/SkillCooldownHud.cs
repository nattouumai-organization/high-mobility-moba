using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーステータスパネル(画面下内側中央)とスキルクールダウン表示を管理するコンポーネント。
/// - スキルスロット(Q/W/E/R大 + D/F小)を画面下内側に表示する。
/// - 各スロットはリフレクションでコントローラーの_cooldownEndTime/_cooldownを参照する。
/// - SlotはControllerがnullの場合は作成されない。
/// - フェーズ7: ゲームデザイン6章のレベルバッジ(「Level Badge」)をLevelSystem連動で更新する。
/// - フェーズ7-fix1: ゲームデザイン11章のヴォルブラーク(VolbraakXController)にも対応。
/// </summary>
[DefaultExecutionOrder(10)]
public sealed class SkillCooldownHud : MonoBehaviour
{
    // 画面下内側の補正(px)。
    [Header("Layout")]
    [SerializeField] private float _bottomMargin = 24f;
    [SerializeField] private float _panelPadding = 12f;
    [SerializeField] private float _sectionGap = 12f;
    [SerializeField] private float _statsBlockWidth = 210f;
    [SerializeField] private float _healthBarHeight = 16f;

    [Header("Slot")]
    [SerializeField] private float _mainSlotSize = 64f;
    [SerializeField] private float _subSlotSize = 52f;
    [SerializeField] private float _slotSpacing = 8f;
    [SerializeField] private float _groupGap = 20f;

    [Header("Portrait")]
    [SerializeField] private string _portraitLabel = "ゼ";
    [SerializeField] private string _levelLabel = "1";

    [Header("Colors")]
    [SerializeField] private Color _panelBg = new Color(0.08f, 0.08f, 0.10f, 0.92f);
    [SerializeField] private Color _slotBg = new Color(0.15f, 0.15f, 0.18f, 1f);
    [SerializeField] private Color _slotBorderColor = new Color(0.35f, 0.35f, 0.40f, 1f);
    [SerializeField] private Color _activeBorderColor = new Color(1f, 0.78f, 0.1f, 1f);
    [SerializeField] private Color _cooldownOverlayColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color _mainAccentColor = new Color(0.25f, 0.55f, 1f, 1f);
    [SerializeField] private Color _dAccentColor = new Color(0.9f, 0.6f, 0.1f, 1f);
    [SerializeField] private Color _fAccentColor = new Color(0.55f, 0.85f, 0.55f, 1f);
    [SerializeField] private Color _keyLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color _cdTextColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _hpFillColor = new Color(0.2f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color _hpBgColor = new Color(0.1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color _statLabelColor = new Color(0.65f, 0.65f, 0.70f, 1f);
    [SerializeField] private Color _statValueColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color _portraitBg = new Color(0.18f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color _levelBadgeBg = new Color(0.75f, 0.6f, 0.0f, 1f);
    [SerializeField] private Color _levelBadgeTextColor = new Color(1f, 1f, 1f, 1f);

    [Header("Flash")]
    [SerializeField] private float _readyFlashDuration = 0.3f;

    // フェーズ7: フォントキャッシュ・ビルトインで代替。
    private Font _font;

    private sealed class Slot
    {
        public string Key;
        public MonoBehaviour Controller;
        public FieldInfo CdEndField;
        public FieldInfo CdField;
        public FieldInfo ActiveField;
        public Image Border;
        public Image CooldownOverlay;
        public UnityEngine.UI.Text CooldownText;
        public Image ReadyFlash;
        public bool WasOnCooldown;
        public float FlashEndTime;
        public bool WarnedMissingField;
    }

    private Canvas _canvas;
    private CharacterStats _stats;
    private HealthController _health;
    private UnityEngine.UI.Text _attackDamageValue;
    private UnityEngine.UI.Text _attackSpeedValue;
    private UnityEngine.UI.Text _moveSpeedValue;
    private UnityEngine.UI.Text _attackRangeValue;
    private Image _healthFill;
    private UnityEngine.UI.Text _healthText;
    private UnityEngine.UI.Text _levelBadgeText;
    private Slot[] _slots;
    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (_slots == null) return;
        foreach (Slot slot in _slots) UpdateSlot(slot);
        UpdateStatusPanel();

        // Level BadgeをLevelSystem連動で更新する(フェーズ7)。
        if (_levelBadgeText != null && _stats != null)
        {
            int lv = LevelSystem.GetLevelForTeam(_stats.Team);
            _levelBadgeText.text = "Lv" + lv;
        }
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    private void FindPlayer()
    {
        // PlayerClickMovementまたはPlayerInputHubからPlayerを特定する。
        PlayerClickMovement mov = FindFirstObjectByType<PlayerClickMovement>();
        if (mov == null)
        {
            PlayerInputHub hub = FindFirstObjectByType<PlayerInputHub>();
            if (hub == null) { enabled = false; return; }
            mov = hub.GetComponent<PlayerClickMovement>();
        }
        if (mov == null) { enabled = false; return; }
        GameObject player = mov.gameObject;
        _stats = player.GetComponent<CharacterStats>();
        _health = player.GetComponent<HealthController>();
        CreateCanvas();
        BuildHud(player);
    }

    private void CreateCanvas()
    {
        GameObject canvasGo = new GameObject("Player Status HUD Canvas", typeof(RectTransform));
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
    }

    private void BuildHud(GameObject player)
    {
        // Status Panel: 画面下内側中央にアンカー。
        GameObject panelGo = new GameObject("Status Panel", typeof(RectTransform));
        panelGo.transform.SetParent(_canvas.transform, false);
        RectTransform panelRect = (RectTransform)panelGo.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, _bottomMargin);
        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = _panelBg;
        panelBg.raycastTarget = false;

        // 内側の左から: Stats Block / Portrait / HPバー / Skill Bar
        float x = _panelPadding;
        float skillBarY = _panelPadding + _healthBarHeight + 6f;
        float skillBarH = _mainSlotSize;
        float totalH = skillBarY + skillBarH + _panelPadding;

        // スキルバーの幅の和を計算する。
        float qwerW = _mainSlotSize * 4f + _slotSpacing * 3f;
        float dfW = _subSlotSize * 2f + _slotSpacing;
        float skillBarW = qwerW + _groupGap + dfW;

        // ポートレイトの幅。
        float portraitW = totalH;

        // パネルの全幅。
        float panelW = x + _statsBlockWidth + _sectionGap + portraitW + _sectionGap + skillBarW + _panelPadding;
        panelRect.sizeDelta = new Vector2(panelW, totalH);

        BuildStatsBlock(panelRect, x, _statsBlockWidth, totalH);
        x += _statsBlockWidth + _sectionGap;

        BuildPortrait(panelRect, x, portraitW, totalH);
        x += portraitW + _sectionGap;

        BuildHealthBar(panelRect, x, skillBarW);
        BuildSkillBar(panelRect, x, skillBarW, player);
    }

    private void BuildStatsBlock(RectTransform parent, float x, float w, float h)
    {
        // ステータスブロック: AD/AS/MS/ARを小さなラベル+値で列挙。
        string[] labels = { "AD", "AS", "MS", "AR" };
        UnityEngine.UI.Text[] values = new UnityEngine.UI.Text[labels.Length];
        float rowH = h / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            float rowY = h - rowH * (i + 1);
            CreateText(labels[i] + "_lbl", parent, labels[i], 11, _statLabelColor,
                x, rowY + rowH * 0.55f, w * 0.38f, rowH * 0.42f);
            values[i] = CreateText(labels[i] + "_val", parent, "-", 13, _statValueColor,
                x + w * 0.38f, rowY + rowH * 0.52f, w * 0.62f, rowH * 0.46f);
        }
        _attackDamageValue = values[0];
        _attackSpeedValue = values[1];
        _moveSpeedValue = values[2];
        _attackRangeValue = values[3];
    }

    private void BuildPortrait(RectTransform parent, float x, float w, float h)
    {
        // ポートレイト暇可: キャラクターアイコンの代わりに文字で指定。
        CreateInsetImage("Portrait BG", parent, _portraitBg, 0f, x, 0f, w, h);

        // ポートレイトラベル(キャラクター名頭文字)。
        CreateText("Portrait Label", parent, _portraitLabel, 28, Color.white, x, 0f, w, h);

        // Level Badge: 左下に小さなバッジ。
        float badgeH = h * 0.22f;
        float badgeW = w;
        CreateInsetImage("Level Badge BG", parent, _levelBadgeBg, 0f, x, 0f, badgeW, badgeH);
        _levelBadgeText = CreateText("Level Badge Text", parent, "Lv1", 11, _levelBadgeTextColor, x, 0f, badgeW, badgeH);
    }

    private void BuildHealthBar(RectTransform parent, float x, float w)
    {
        float barY = _panelPadding;
        CreateInsetImage("HP Bar BG", parent, _hpBgColor, 0f, x, barY, w, _healthBarHeight);
        Image fill = CreateInsetImage("HP Bar Fill", parent, _hpFillColor, 0f, x, barY, w, _healthBarHeight);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        _healthFill = fill;
        _healthText = CreateText("HP Text", parent, "- / -", 10, Color.white, x, barY, w, _healthBarHeight);
    }

    private void BuildSkillBar(RectTransform parent, float x, float w, GameObject player)
    {
        float barY = _panelPadding + _healthBarHeight + 6f;
        GameObject containerGo = new GameObject("Skill Bar", typeof(RectTransform));
        containerGo.transform.SetParent(parent, false);
        RectTransform container = (RectTransform)containerGo.transform;
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.zero;
        container.pivot = Vector2.zero;
        container.anchoredPosition = new Vector2(x, barY);
        container.sizeDelta = new Vector2(w, _mainSlotSize);

        // フェーズ7-fix1: ゲームデザイン11章 ヴォルブラークのコントローラーにも対応する。
        bool isVolbraak = player.GetComponent<VolbraakQController>() != null;
        MonoBehaviour qCtrl = player.GetComponent<ZelfQController>() as MonoBehaviour
            ?? player.GetComponent<VolbraakQController>();
        MonoBehaviour wCtrl = player.GetComponent<ZelfWController>() as MonoBehaviour
            ?? player.GetComponent<VolbraakWController>();
        MonoBehaviour eCtrl = player.GetComponent<ZelfEController>() as MonoBehaviour
            ?? player.GetComponent<VolbraakEController>();
        MonoBehaviour rCtrl = player.GetComponent<ZelfRController>() as MonoBehaviour
            ?? player.GetComponent<VolbraakRController>();
        string rActiveField = isVolbraak ? "_isTetherActive" : "_isRActive";

        System.Collections.Generic.List<Slot> slots = new System.Collections.Generic.List<Slot>();
        float slotX = 0f;
        Slot qs = AddSlot(container, slotX, "Q", qCtrl, null, _mainAccentColor, _mainSlotSize);
        if (qs != null) slots.Add(qs);
        slotX += _mainSlotSize + _slotSpacing;

        Slot ws = AddSlot(container, slotX, "W", wCtrl, "_isWActive", _mainAccentColor, _mainSlotSize);
        if (ws != null) slots.Add(ws);
        slotX += _mainSlotSize + _slotSpacing;

        Slot es = AddSlot(container, slotX, "E", eCtrl, "_isDashing", _mainAccentColor, _mainSlotSize);
        if (es != null) slots.Add(es);
        slotX += _mainSlotSize + _slotSpacing;

        Slot rs = AddSlot(container, slotX, "R", rCtrl, rActiveField, _mainAccentColor, _mainSlotSize);
        if (rs != null) slots.Add(rs);
        slotX += _mainSlotSize + _groupGap;

        CommonDController dCtrl = player.GetComponent<CommonDController>();
        Slot ds = AddSlot(container, slotX, "D", dCtrl, "_isWindowActive", _dAccentColor, _subSlotSize);
        if (ds != null) slots.Add(ds);
        slotX += _subSlotSize + _slotSpacing;

        FlashController fCtrl = player.GetComponent<FlashController>();
        Slot fs = AddSlot(container, slotX, "F", fCtrl, null, _fAccentColor, _subSlotSize);
        if (fs != null) slots.Add(fs);

        _slots = slots.ToArray();
    }
    private Slot AddSlot(RectTransform container, float slotX, string key, MonoBehaviour controller, string activeFieldName, Color accentColor, float slotSize)
    {
        if (controller == null)
        {
            Debug.LogWarning("SkillCooldownHud: " + key + " controller not found, slot skipped.");
            return null;
        }
        System.Type t = controller.GetType();
        FieldInfo cdEndField = t.GetField("_cooldownEndTime", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo cdField = t.GetField("_remainingCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        if (cdEndField == null && cdField == null)
            Debug.LogWarning("SkillCooldownHud: " + key + " missing _cooldownEndTime and _remainingCooldown fields.");
        FieldInfo activeField = activeFieldName != null
            ? t.GetField(activeFieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            : null;

        GameObject slotGo = new GameObject("Skill Slot " + key, typeof(RectTransform));
        slotGo.transform.SetParent(container, false);
        RectTransform slotRect = (RectTransform)slotGo.transform;
        slotRect.anchorMin = Vector2.zero;
        slotRect.anchorMax = Vector2.zero;
        slotRect.pivot = Vector2.zero;
        slotRect.anchoredPosition = new Vector2(slotX, 0f);
        slotRect.sizeDelta = new Vector2(slotSize, slotSize);

        // Border image.
        Image border = slotGo.AddComponent<Image>();
        border.color = _slotBorderColor;
        border.raycastTarget = false;

        // Background.
        CreateInsetImage("Background", slotRect, _slotBg, 2f, 0f, 0f, slotSize, slotSize);

        // Accent line at top.
        float accentH = Mathf.Max(3f, slotSize * 0.05f);
        CreateInsetImage("Accent", slotRect, accentColor, 2f, 0f, slotSize - accentH - 2f, slotSize, accentH);

        // Key label.
        CreateText("Key Label", slotRect, key, Mathf.RoundToInt(slotSize * 0.22f), _keyLabelColor,
            0f, slotSize * 0.55f, slotSize, slotSize * 0.3f);

        // Cooldown overlay (filled).
        Image cdOverlay = CreateInsetImage("Cooldown Overlay", slotRect, _cooldownOverlayColor, 2f, 0f, 0f, slotSize, slotSize);
        cdOverlay.type = Image.Type.Filled;
        cdOverlay.fillMethod = Image.FillMethod.Radial360;
        cdOverlay.fillOrigin = (int)Image.Origin360.Top;
        cdOverlay.fillClockwise = false;
        cdOverlay.fillAmount = 0f;

        // Cooldown text.
        UnityEngine.UI.Text cdText = CreateText("Cooldown Text", slotRect, "", Mathf.RoundToInt(slotSize * 0.28f), _cdTextColor,
            0f, 0f, slotSize, slotSize * 0.5f);

        // Ready flash image.
        Image readyFlash = CreateInsetImage("Ready Flash", slotRect, Color.white, 2f, 0f, 0f, slotSize, slotSize);
        readyFlash.color = new Color(1f, 1f, 1f, 0f);

        Slot slot = new Slot
        {
            Key = key,
            Controller = controller,
            CdEndField = cdEndField,
            CdField = cdField,
            ActiveField = activeField,
            Border = border,
            CooldownOverlay = cdOverlay,
            CooldownText = cdText,
            ReadyFlash = readyFlash,
        };
        return slot;
    }

    private void UpdateSlot(Slot slot)
    {
        if (slot == null || slot.Controller == null) return;

        float remaining = 0f;
        float total = 0f;
        if (slot.CdEndField != null)
        {
            double endTime = (double)slot.CdEndField.GetValue(slot.Controller);
            remaining = (float)System.Math.Max(0.0, endTime - UnityEngine.Time.timeAsDouble);
        }
        else if (slot.CdField != null)
        {
            remaining = (float)slot.CdField.GetValue(slot.Controller);
        }
        if (slot.CdField != null)
        {
            // _remainingCooldownは残り秒数だが、全体CDは剖定できないのでクールダウン補正値として使用。
            total = (float)slot.CdField.GetValue(slot.Controller);
            if (total > 0f) total = remaining; // fallback
        }

        bool onCooldown = remaining > 0.05f;
        if (slot.CooldownOverlay != null)
        {
            slot.CooldownOverlay.fillAmount = onCooldown && total > 0f ? remaining / total : (onCooldown ? 0.999f : 0f);
        }
        if (slot.CooldownText != null)
        {
            slot.CooldownText.text = onCooldown ? remaining.ToString("F1") : "";
        }

        // クールダウン完了フラッシュ。
        if (slot.WasOnCooldown && !onCooldown)
        {
            slot.FlashEndTime = UnityEngine.Time.time + _readyFlashDuration;
        }
        slot.WasOnCooldown = onCooldown;

        // アクティブ中(W持続・ Eダッシュ中など)は指をゴールドで強調する。
        if (slot.Border != null)
        {
            bool isActive = slot.ActiveField != null && slot.Controller != null &&
                            slot.ActiveField.GetValue(slot.Controller) is bool ab && ab;
            slot.Border.color = isActive ? _activeBorderColor : _slotBorderColor;
        }

        // フラッシュ色の更新。
        if (slot.ReadyFlash != null)
        {
            Color flashColor = new Color(1f, 1f, 1f, 0f);
            if (UnityEngine.Time.time < slot.FlashEndTime)
            {
                float elapsed = UnityEngine.Time.time - (slot.FlashEndTime - _readyFlashDuration);
                flashColor.a = Mathf.Clamp01(1f - elapsed / _readyFlashDuration);
            }
            slot.ReadyFlash.color = flashColor;
        }
    }

    private void UpdateStatusPanel()
    {
        if (_stats != null)
        {
            if (_attackDamageValue != null)
                _attackDamageValue.text = _stats.CurrentAttackDamage.ToString("F1");
            if (_attackSpeedValue != null)
                _attackSpeedValue.text = _stats.CurrentAttackSpeed.ToString("F2");
            if (_moveSpeedValue != null)
                _moveSpeedValue.text = (_stats.CurrentMoveSpeed * CharacterStats.MoveSpeedStatPerUnityUnit).ToString("F0");
            if (_attackRangeValue != null)
                _attackRangeValue.text = (_stats.CurrentAttackRange * CharacterStats.RangeStatPerUnityUnit).ToString("F0");
        }
        if (_health != null)
        {
            if (_healthFill != null && _health.MaxHealth > 0f)
                _healthFill.fillAmount = Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth);
            if (_healthText != null)
                _healthText.text = Mathf.CeilToInt(_health.CurrentHealth) + "/" + Mathf.CeilToInt(_health.MaxHealth);
        }
    }

    // ヘルパー: インセットイメージを作成(絶対座標指定バージョン)。
    private Image CreateInsetImage(string objectName, RectTransform parent, Color color, float inset,
        float ax, float ay, float aw, float ah)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(ax + inset, ay + inset);
        rect.sizeDelta = new Vector2(aw - inset * 2f, ah - inset * 2f);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ヘルパー: テキストを作成(絶対座標指定バージョン)。
    private UnityEngine.UI.Text CreateText(string objectName, RectTransform parent, string content,
        int fontSize, Color color, float ax, float ay, float aw, float ah)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(ax, ay);
        rect.sizeDelta = new Vector2(aw, ah);
        UnityEngine.UI.Text text = go.AddComponent<UnityEngine.UI.Text>();
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.text = content;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
