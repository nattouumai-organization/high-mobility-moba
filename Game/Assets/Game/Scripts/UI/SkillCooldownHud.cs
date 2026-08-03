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
    [SerializeField] private Color _levelBadgeTextColor = new Color(1f, 1f, 1f, 1f);

    [Header("Flash")]
    [SerializeField] private float _readyFlashDuration = 0.3f;

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
        public Text CooldownText;
        public Image ReadyFlash;
        public bool WasOnCooldown;
        public float FlashEndTime;
    }

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

        // Level Badge: TeamMember.Team -> LevelSystem(phase7)
        if (_levelBadgeText != null && _teamMember != null)
        {
            int lv = LevelSystem.GetLevelForTeam(_teamMember.Team);
            _levelBadgeText.text = "Lv" + lv;
        }
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    private void FindPlayer()
    {
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
        _teamMember = player.GetComponent<TeamMember>();
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

        float skillBarY = _panelPadding + _healthBarHeight + 6f;
        float totalH = skillBarY + _mainSlotSize + _panelPadding;
        float qwerW = _mainSlotSize * 4f + _slotSpacing * 3f;
        float dfW = _subSlotSize * 2f + _slotSpacing;
        float skillBarW = qwerW + _groupGap + dfW;
        float portraitW = totalH;
        float panelW = _panelPadding + _statsBlockWidth + _sectionGap + portraitW + _sectionGap + skillBarW + _panelPadding;
        panelRect.sizeDelta = new Vector2(panelW, totalH);

        float x = _panelPadding;
        BuildStatsBlock(panelRect, x, _statsBlockWidth, totalH);
        x += _statsBlockWidth + _sectionGap;
        BuildPortrait(panelRect, x, portraitW, totalH);
        x += portraitW + _sectionGap;
        BuildHealthBar(panelRect, x, skillBarW);
        BuildSkillBar(panelRect, x, skillBarW, player);
    }

    private void BuildStatsBlock(RectTransform parent, float x, float w, float h)
    {
        string[] labels = { "AD", "AS", "MS", "AR" };
        Text[] values = new Text[labels.Length];
        float rowH = h / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            float rowY = h - rowH * (i + 1);
            MakeText(labels[i] + "_lbl", parent, labels[i], 11, _statLabelColor,
                x, rowY + rowH * 0.55f, w * 0.38f, rowH * 0.42f);
            values[i] = MakeText(labels[i] + "_val", parent, "-", 13, _statValueColor,
                x + w * 0.38f, rowY + rowH * 0.52f, w * 0.62f, rowH * 0.46f);
        }
        _attackDamageValue = values[0];
        _attackSpeedValue  = values[1];
        _moveSpeedValue    = values[2];
        _attackRangeValue  = values[3];
    }

    private void BuildPortrait(RectTransform parent, float x, float w, float h)
    {
        MakeImage("Portrait BG", parent, _portraitBg, 0f, x, 0f, w, h);
        MakeText("Portrait Label", parent, _portraitLabel, 28, Color.white, x, 0f, w, h);
        float badgeH = h * 0.22f;
        MakeImage("Level Badge BG", parent, _levelBadgeBg, 0f, x, 0f, w, badgeH);
        _levelBadgeText = MakeText("Level Badge Text", parent, "Lv1", 11, _levelBadgeTextColor, x, 0f, w, badgeH);
    }

    private void BuildHealthBar(RectTransform parent, float x, float w)
    {
        MakeImage("HP Bar BG", parent, _hpBgColor, 0f, x, _panelPadding, w, _healthBarHeight);
        Image fill = MakeImage("HP Bar Fill", parent, _hpFillColor, 0f, x, _panelPadding, w, _healthBarHeight);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        _healthFill = fill;
        _healthText = MakeText("HP Text", parent, "- / -", 10, Color.white, x, _panelPadding, w, _healthBarHeight);
    }

    private void BuildSkillBar(RectTransform parent, float x, float w, GameObject player)
    {
        float barY = _panelPadding + _healthBarHeight + 6f;
        GameObject go = new GameObject("Skill Bar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform ct = (RectTransform)go.transform;
        ct.anchorMin = Vector2.zero;
        ct.anchorMax = Vector2.zero;
        ct.pivot = Vector2.zero;
        ct.anchoredPosition = new Vector2(x, barY);
        ct.sizeDelta = new Vector2(w, _mainSlotSize);

        // phase7-fix1: Zelf or Volbraak controllers
        bool isVolbraak = player.GetComponent<VolbraakQController>() != null;
        MonoBehaviour qCtrl = player.GetComponent<ZelfQController>() as MonoBehaviour ?? player.GetComponent<VolbraakQController>();
        MonoBehaviour wCtrl = player.GetComponent<ZelfWController>() as MonoBehaviour ?? player.GetComponent<VolbraakWController>();
        MonoBehaviour eCtrl = player.GetComponent<ZelfEController>() as MonoBehaviour ?? player.GetComponent<VolbraakEController>();
        MonoBehaviour rCtrl = player.GetComponent<ZelfRController>() as MonoBehaviour ?? player.GetComponent<VolbraakRController>();
        string rActive = isVolbraak ? "_isTetherActive" : "_isRActive";

        System.Collections.Generic.List<Slot> slots = new System.Collections.Generic.List<Slot>();
        float sx = 0f;
        AddSlotTo(slots, ct, sx, "Q", qCtrl, null,    _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, ct, sx, "W", wCtrl, "_isWActive", _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, ct, sx, "E", eCtrl, "_isDashing", _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _slotSpacing;
        AddSlotTo(slots, ct, sx, "R", rCtrl, rActive,  _mainAccentColor, _mainSlotSize); sx += _mainSlotSize + _groupGap;
        AddSlotTo(slots, ct, sx, "D", player.GetComponent<CommonDController>(), "_isWindowActive", _dAccentColor, _subSlotSize); sx += _subSlotSize + _slotSpacing;
        AddSlotTo(slots, ct, sx, "F", player.GetComponent<FlashController>(), null, _fAccentColor, _subSlotSize);
        _slots = slots.ToArray();
    }

    private void AddSlotTo(System.Collections.Generic.List<Slot> list, RectTransform container,
        float slotX, string key, MonoBehaviour controller, string activeFieldName, Color accentColor, float slotSize)
    {
        if (controller == null)
        {
            Debug.LogWarning("SkillCooldownHud: " + key + " controller not found, slot skipped.");
            return;
        }
        System.Type t = controller.GetType();
        FieldInfo cdEndField = t.GetField("_cooldownEndTime", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo cdField    = t.GetField("_remainingCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeField = activeFieldName != null
            ? t.GetField(activeFieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            : null;
        if (cdEndField == null && cdField == null)
            Debug.LogWarning("SkillCooldownHud: " + key + " missing cooldown fields.");

        GameObject slotGo = new GameObject("Skill Slot " + key, typeof(RectTransform));
        slotGo.transform.SetParent(container, false);
        RectTransform sr = (RectTransform)slotGo.transform;
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.zero; sr.pivot = Vector2.zero;
        sr.anchoredPosition = new Vector2(slotX, 0f);
        sr.sizeDelta = new Vector2(slotSize, slotSize);

        Image border = slotGo.AddComponent<Image>();
        border.color = _slotBorderColor;
        border.raycastTarget = false;

        MakeImage("Background", sr, _slotBg, 2f, 0f, 0f, slotSize, slotSize);
        float accentH = Mathf.Max(3f, slotSize * 0.05f);
        MakeImage("Accent", sr, accentColor, 2f, 0f, slotSize - accentH - 2f, slotSize, accentH);
        MakeText("Key Label", sr, key, Mathf.RoundToInt(slotSize * 0.22f), _keyLabelColor,
            0f, slotSize * 0.55f, slotSize, slotSize * 0.3f);

        Image cdOverlay = MakeImage("Cooldown Overlay", sr, _cooldownOverlayColor, 2f, 0f, 0f, slotSize, slotSize);
        cdOverlay.type = Image.Type.Filled;
        cdOverlay.fillMethod = Image.FillMethod.Radial360;
        cdOverlay.fillOrigin = (int)Image.Origin360.Top;
        cdOverlay.fillClockwise = false;
        cdOverlay.fillAmount = 0f;

        Text cdText = MakeText("Cooldown Text", sr, "", Mathf.RoundToInt(slotSize * 0.28f), _cdTextColor,
            0f, 0f, slotSize, slotSize * 0.5f);
        Image readyFlash = MakeImage("Ready Flash", sr, Color.white, 2f, 0f, 0f, slotSize, slotSize);
        readyFlash.color = new Color(1f, 1f, 1f, 0f);

        list.Add(new Slot
        {
            Key = key, Controller = controller,
            CdEndField = cdEndField, CdField = cdField, ActiveField = activeField,
            Border = border, CooldownOverlay = cdOverlay,
            CooldownText = cdText, ReadyFlash = readyFlash,
        });
    }

    private void UpdateSlot(Slot slot)
    {
        if (slot == null || slot.Controller == null) return;

        float remaining = 0f;
        float total = 1f;
        if (slot.CdEndField != null)
        {
            double endTime = (double)slot.CdEndField.GetValue(slot.Controller);
            remaining = (float)System.Math.Max(0.0, endTime - Time.timeAsDouble);
        }
        else if (slot.CdField != null)
        {
            remaining = (float)slot.CdField.GetValue(slot.Controller);
        }
        if (slot.CdField != null)
        {
            float r = (float)slot.CdField.GetValue(slot.Controller);
            if (r > 0f) total = r;
        }

        bool onCooldown = remaining > 0.05f;
        if (slot.CooldownOverlay != null)
            slot.CooldownOverlay.fillAmount = onCooldown ? Mathf.Clamp01(remaining / total) : 0f;
        if (slot.CooldownText != null)
            slot.CooldownText.text = onCooldown ? remaining.ToString("F1") : "";

        if (slot.WasOnCooldown && !onCooldown)
            slot.FlashEndTime = Time.time + _readyFlashDuration;
        slot.WasOnCooldown = onCooldown;

        if (slot.Border != null)
        {
            bool isActive = slot.ActiveField != null &&
                            slot.ActiveField.GetValue(slot.Controller) is bool b && b;
            slot.Border.color = isActive ? _activeBorderColor : _slotBorderColor;
        }
        if (slot.ReadyFlash != null)
        {
            float a = 0f;
            if (Time.time < slot.FlashEndTime)
                a = Mathf.Clamp01(1f - (Time.time - (slot.FlashEndTime - _readyFlashDuration)) / _readyFlashDuration);
            slot.ReadyFlash.color = new Color(1f, 1f, 1f, a);
        }
    }

    private void UpdateStatusPanel()
    {
        if (_stats != null)
        {
            if (_attackDamageValue != null) _attackDamageValue.text = _stats.CurrentAttackDamage.ToString("F1");
            if (_attackSpeedValue != null)  _attackSpeedValue.text  = _stats.CurrentAttackSpeed.ToString("F2");
            if (_moveSpeedValue != null)    _moveSpeedValue.text    = (_stats.CurrentMoveSpeed * CharacterStats.MoveSpeedStatPerUnityUnit).ToString("F0");
            if (_attackRangeValue != null)  _attackRangeValue.text  = (_stats.CurrentAttackRange * CharacterStats.RangeStatPerUnityUnit).ToString("F0");
        }
        if (_health != null)
        {
            if (_healthFill != null && _health.MaxHealth > 0f)
                _healthFill.fillAmount = Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth);
            if (_healthText != null)
                _healthText.text = Mathf.CeilToInt(_health.CurrentHealth) + "/" + Mathf.CeilToInt(_health.MaxHealth);
        }
    }

    private Image MakeImage(string n, RectTransform parent, Color c, float inset, float ax, float ay, float aw, float ah)
    {
        GameObject o = new GameObject(n, typeof(RectTransform));
        o.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)o.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(ax + inset, ay + inset);
        r.sizeDelta = new Vector2(aw - inset * 2f, ah - inset * 2f);
        Image img = o.AddComponent<Image>();
        img.color = c; img.raycastTarget = false;
        return img;
    }

    private Text MakeText(string n, RectTransform parent, string content, int fs, Color c,
        float ax, float ay, float aw, float ah)
    {
        GameObject o = new GameObject(n, typeof(RectTransform));
        o.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)o.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(ax, ay);
        r.sizeDelta = new Vector2(aw, ah);
        Text t = o.AddComponent<Text>();
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.font = _font; t.fontSize = fs; t.color = c; t.text = content;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
