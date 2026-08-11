using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10)]
public sealed class SkillCooldownHud : MonoBehaviour
{
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
    [SerializeField] private string _portraitLabel = "ze";

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
    [SerializeField] private Color _levelBadgeTextColor = Color.white;

    [Header("Flash")]
    [SerializeField] private float _readyFlashDuration = 0.3f;

    private sealed class Slot
    {
        public MonoBehaviour Controller;
        public FieldInfo CdEndField;
        public FieldInfo CdField;
        public FieldInfo ActiveField;
        public FieldInfo ChargesField;
        public FieldInfo MaxChargesField;
        public Image Border;
        public Image CooldownOverlay;
        public Text CooldownText;
        public Image ReadyFlash;
        public bool WasOnCooldown;
        public float FlashEndTime;
    }

    private Font _font;
    private Canvas _canvas;
    private CharacterStats _stats;
    private HealthController _health;
    private TeamMember _teamMember;
    private Text _attackDamageValue;
    private Text _attackSpeedValue;
    private Text _moveSpeedValue;
    private Text _attackRangeValue;
    private Image _healthFill;
    private Text _healthText;
    private Text _levelBadgeText;
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
        if (_levelBadgeText != null && _teamMember != null)
            _levelBadgeText.text = "Lv" + LevelSystem.GetLevelForTeam(_teamMember.Team);
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    private void FindPlayer()
    {
        PlayerClickMovement movement = FindFirstObjectByType<PlayerClickMovement>();
        if (movement == null)
        {
            PlayerInputHub hub = FindFirstObjectByType<PlayerInputHub>();
            if (hub == null) { enabled = false; return; }
            movement = hub.GetComponent<PlayerClickMovement>();
        }
        if (movement == null) { enabled = false; return; }

        GameObject player = movement.gameObject;
        _stats = player.GetComponent<CharacterStats>();
        _health = player.GetComponent<HealthController>();
        _teamMember = player.GetComponent<TeamMember>();
        if (_stats != null && _stats.Data != null)
        {
            _portraitLabel = _stats.Data.CharacterId == "Oboro" ? "ob" :
                _stats.Data.CharacterId == "Volbraak" ? "vo" : "ze";
        }
        CreateCanvas();
        BuildHud(player);
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Player Status HUD Canvas", typeof(RectTransform));
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void BuildHud(GameObject player)
    {
        GameObject panelObject = new GameObject("Status Panel", typeof(RectTransform));
        panelObject.transform.SetParent(_canvas.transform, false);
        RectTransform panel = (RectTransform)panelObject.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, _bottomMargin);
        Image bg = panelObject.AddComponent<Image>();
        bg.color = _panelBg; bg.raycastTarget = false;

        float skillBarY = _panelPadding + _healthBarHeight + 6f;
        float totalHeight = skillBarY + _mainSlotSize + _panelPadding;
        float qwerWidth = _mainSlotSize * 4f + _slotSpacing * 3f;
        float dfWidth = _subSlotSize * 2f + _slotSpacing;
        float skillBarWidth = qwerWidth + _groupGap + dfWidth;
        float portraitWidth = totalHeight;
        float panelWidth = _panelPadding + _statsBlockWidth + _sectionGap + portraitWidth + _sectionGap + skillBarWidth + _panelPadding;
        panel.sizeDelta = new Vector2(panelWidth, totalHeight);

        float x = _panelPadding;
        BuildStatsBlock(panel, x, _statsBlockWidth, totalHeight);
        x += _statsBlockWidth + _sectionGap;
        BuildPortrait(panel, x, portraitWidth, totalHeight);
        x += portraitWidth + _sectionGap;
        BuildHealthBar(panel, x, skillBarWidth);
        BuildSkillBar(panel, x, skillBarWidth, player);
    }

    private void BuildStatsBlock(RectTransform parent, float x, float width, float height)
    {
        string[] labels = { "AD", "AS", "MS", "AR" };
        Text[] values = new Text[labels.Length];
        float rowHeight = height / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            float y = height - rowHeight * (i + 1);
            MakeText(labels[i] + "_lbl", parent, labels[i], 11, _statLabelColor,
                x, y + rowHeight * 0.55f, width * 0.38f, rowHeight * 0.42f);
            values[i] = MakeText(labels[i] + "_val", parent, "-", 13, _statValueColor,
                x + width * 0.38f, y + rowHeight * 0.52f, width * 0.62f, rowHeight * 0.46f);
        }
        _attackDamageValue = values[0]; _attackSpeedValue = values[1];
        _moveSpeedValue = values[2]; _attackRangeValue = values[3];
    }

    private void BuildPortrait(RectTransform parent, float x, float width, float height)
    {
        MakeImage("Portrait BG", parent, _portraitBg, 0f, x, 0f, width, height);
        MakeText("Portrait Label", parent, _portraitLabel, 28, Color.white, x, 0f, width, height);
        float badgeHeight = height * 0.22f;
        MakeImage("Level Badge BG", parent, _levelBadgeBg, 0f, x, 0f, width, badgeHeight);
        _levelBadgeText = MakeText("Level Badge Text", parent, "Lv1", 11, _levelBadgeTextColor,
            x, 0f, width, badgeHeight);
    }

    private void BuildHealthBar(RectTransform parent, float x, float width)
    {
        MakeImage("HP Bar BG", parent, _hpBgColor, 0f, x, _panelPadding, width, _healthBarHeight);
        _healthFill = MakeImage("HP Bar Fill", parent, _hpFillColor, 0f, x, _panelPadding, width, _healthBarHeight);
        _healthFill.type = Image.Type.Filled;
        _healthFill.fillMethod = Image.FillMethod.Horizontal;
        _healthFill.fillAmount = 1f;
        _healthText = MakeText("HP Text", parent, "- / -", 10, Color.white,
            x, _panelPadding, width, _healthBarHeight);
    }

    private void BuildSkillBar(RectTransform parent, float x, float width, GameObject player)
    {
        float barY = _panelPadding + _healthBarHeight + 6f;
        GameObject barObject = new GameObject("Skill Bar", typeof(RectTransform));
        barObject.transform.SetParent(parent, false);
        RectTransform container = (RectTransform)barObject.transform;
        container.anchorMin = container.anchorMax = container.pivot = Vector2.zero;
        container.anchoredPosition = new Vector2(x, barY);
        container.sizeDelta = new Vector2(width, _mainSlotSize);

        bool isVolbraak = player.GetComponent<VolbraakQController>() != null;
        bool isOboro = player.GetComponent<OboroQController>() != null;
        MonoBehaviour q = player.GetComponent<ZelfQController>() as MonoBehaviour ??
                          player.GetComponent<VolbraakQController>() as MonoBehaviour ?? player.GetComponent<OboroQController>();
        MonoBehaviour w = player.GetComponent<ZelfWController>() as MonoBehaviour ??
                          player.GetComponent<VolbraakWController>() as MonoBehaviour ?? player.GetComponent<OboroWController>();
        MonoBehaviour e = player.GetComponent<ZelfEController>() as MonoBehaviour ??
                          player.GetComponent<VolbraakEController>() as MonoBehaviour ?? player.GetComponent<OboroEController>();
        MonoBehaviour r = player.GetComponent<ZelfRController>() as MonoBehaviour ??
                          player.GetComponent<VolbraakRController>() as MonoBehaviour ?? player.GetComponent<OboroRController>();
        string rActive = isVolbraak ? "_isTetherActive" : isOboro ? null : "_isRActive";
        string eActive = isOboro ? "_isExecuting" : "_isDashing";

        var slots = new System.Collections.Generic.List<Slot>();
        float sx = 0f;
        AddSlotTo(slots, container, sx, "Q", q, null, _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, container, sx, "W", w, "_isWActive", _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, container, sx, "E", e, eActive, _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, container, sx, "R", r, rActive, _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _groupGap;
        AddSlotTo(slots, container, sx, "D", player.GetComponent<CommonDController>(), "_isWindowActive", _dAccentColor, _subSlotSize); sx += _subSlotSize + _slotSpacing;
        AddSlotTo(slots, container, sx, "F", player.GetComponent<FlashController>(), null, _fAccentColor, _subSlotSize);
        _slots = slots.ToArray();
    }

    private void AddSlotTo(System.Collections.Generic.List<Slot> list, RectTransform container,
        float x, string key, MonoBehaviour controller, string activeFieldName, Color accent, float size)
    {
        if (controller == null)
        {
            Debug.LogWarning("SkillCooldownHud: " + key + " controller not found, slot skipped.");
            return;
        }
        System.Type type = controller.GetType();
        FieldInfo endField = type.GetField("_cooldownEndTime", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo cdField = type.GetField("_remainingCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeField = activeFieldName != null
            ? type.GetField(activeFieldName, BindingFlags.NonPublic | BindingFlags.Instance) : null;
        FieldInfo chargesField = type.GetField("_currentCharges", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo maxChargesField = type.GetField("_maxCharges", BindingFlags.NonPublic | BindingFlags.Instance);

        GameObject slotObject = new GameObject("Skill Slot " + key, typeof(RectTransform));
        slotObject.transform.SetParent(container, false);
        RectTransform rect = (RectTransform)slotObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, 0f); rect.sizeDelta = new Vector2(size, size);
        Image border = slotObject.AddComponent<Image>(); border.color = _slotBorderColor; border.raycastTarget = false;
        MakeImage("Background", rect, _slotBg, 2f, 0f, 0f, size, size);
        float accentHeight = Mathf.Max(3f, size * 0.05f);
        MakeImage("Accent", rect, accent, 2f, 0f, size - accentHeight - 2f, size, accentHeight);
        MakeText("Key Label", rect, key, Mathf.RoundToInt(size * 0.22f), _keyLabelColor,
            0f, size * 0.55f, size, size * 0.3f);
        Image overlay = MakeImage("Cooldown Overlay", rect, _cooldownOverlayColor, 2f, 0f, 0f, size, size);
        overlay.type = Image.Type.Filled; overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top; overlay.fillClockwise = false; overlay.fillAmount = 0f;
        Text cdText = MakeText("Cooldown Text", rect, "", Mathf.RoundToInt(size * 0.25f), _cdTextColor,
            0f, 0f, size, size * 0.52f);
        Image flash = MakeImage("Ready Flash", rect, Color.white, 2f, 0f, 0f, size, size);
        flash.color = new Color(1f, 1f, 1f, 0f);

        list.Add(new Slot { Controller = controller, CdEndField = endField, CdField = cdField,
            ActiveField = activeField, ChargesField = chargesField, MaxChargesField = maxChargesField,
            Border = border, CooldownOverlay = overlay, CooldownText = cdText, ReadyFlash = flash });
    }

    private void UpdateSlot(Slot slot)
    {
        if (slot == null || slot.Controller == null) return;
        float remaining = 0f;
        if (slot.CdEndField != null)
        {
            double end = (double)slot.CdEndField.GetValue(slot.Controller);
            remaining = (float)System.Math.Max(0.0, end - Time.timeAsDouble);
        }
        else if (slot.CdField != null) remaining = (float)slot.CdField.GetValue(slot.Controller);

        float total = remaining > 0f ? remaining : 1f;
        bool onCooldown = remaining > 0.05f;
        if (slot.CooldownOverlay != null) slot.CooldownOverlay.fillAmount = onCooldown ? Mathf.Clamp01(remaining / total) : 0f;

        string text = onCooldown ? remaining.ToString("F1") : "";
        if (slot.ChargesField != null && slot.MaxChargesField != null)
        {
            int charges = (int)slot.ChargesField.GetValue(slot.Controller);
            int maxCharges = (int)slot.MaxChargesField.GetValue(slot.Controller);
            text = onCooldown ? $"{charges}/{maxCharges}\n{remaining:F1}" : $"{charges}/{maxCharges}";
        }
        if (slot.CooldownText != null) slot.CooldownText.text = text;

        if (slot.WasOnCooldown && !onCooldown) slot.FlashEndTime = Time.time + _readyFlashDuration;
        slot.WasOnCooldown = onCooldown;
        if (slot.Border != null)
        {
            bool active = slot.ActiveField != null && slot.ActiveField.GetValue(slot.Controller) is bool value && value;
            slot.Border.color = active ? _activeBorderColor : _slotBorderColor;
        }
        if (slot.ReadyFlash != null)
        {
            float alpha = 0f;
            if (Time.time < slot.FlashEndTime)
                alpha = Mathf.Clamp01(1f - (Time.time - (slot.FlashEndTime - _readyFlashDuration)) / _readyFlashDuration);
            slot.ReadyFlash.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void UpdateStatusPanel()
    {
        if (_stats != null)
        {
            if (_attackDamageValue != null) _attackDamageValue.text = _stats.CurrentAttackDamage.ToString("F1");
            if (_attackSpeedValue != null) _attackSpeedValue.text = _stats.CurrentAttackSpeed.ToString("F2");
            if (_moveSpeedValue != null) _moveSpeedValue.text = (_stats.CurrentMoveSpeed * CharacterStats.MoveSpeedStatPerUnityUnit).ToString("F0");
            if (_attackRangeValue != null) _attackRangeValue.text = (_stats.CurrentAttackRange * CharacterStats.RangeStatPerUnityUnit).ToString("F0");
        }
        if (_health != null)
        {
            if (_healthFill != null && _health.MaxHealth > 0f) _healthFill.fillAmount = Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth);
            if (_healthText != null) _healthText.text = Mathf.CeilToInt(_health.CurrentHealth) + "/" + Mathf.CeilToInt(_health.MaxHealth);
        }
    }

    private Image MakeImage(string name, RectTransform parent, Color color, float inset, float x, float y, float width, float height)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x + inset, y + inset);
        rect.sizeDelta = new Vector2(width - inset * 2f, height - inset * 2f);
        Image image = obj.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    private Text MakeText(string name, RectTransform parent, string content, int fontSize, Color color,
        float x, float y, float width, float height)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        Text text = obj.AddComponent<Text>();
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.font = _font; text.fontSize = fontSize; text.color = color; text.text = content;
        text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        return text;
    }
}
