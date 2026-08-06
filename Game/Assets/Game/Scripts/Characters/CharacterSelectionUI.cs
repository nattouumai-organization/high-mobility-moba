using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TASKS.md「キャラクター選択画面を実装する」用のUI制御スクリプト。
/// Inspectorで設定したキャラクター一覧から、実行時にキャラクターカード・詳細パネル・
/// 開始ボタンをUnity UI Canvas上へ構築する(外部アセット・外部フォントは使用しない)。
/// Availableのキャラクターだけを選択可能にし、Coming Soonのカードは半透明の選択不可として表示する。
/// 選択中のCharacterDataはCharacterSelectionManagerへ渡し、開始ボタンでSC_Prototypeを読み込む。
/// スキルの詳細説明はCharacterData側が保持しており、将来ツールチップや別パネルとして表示できる
/// (この画面では短い一覧のみ表示する)。
/// </summary>
public class CharacterSelectionUI : MonoBehaviour
{
    /// <summary>
    /// カード1枚分のキャラクター情報。CharacterDataが設定されていればその値を優先し、
    /// CharacterData未作成のキャラクター(Coming Soon)はフォールバック値で表示する。
    /// </summary>
    [System.Serializable]
    public class CharacterEntry
    {
        [SerializeField] private CharacterData _characterData;
        [SerializeField] private string _fallbackDisplayName = "";
        [SerializeField] private string _fallbackRoleName = "";
        [SerializeField] private Color _fallbackThemeColor = Color.gray;
        [SerializeField] private CharacterStatus _fallbackStatus = CharacterStatus.ComingSoon;
        [Tooltip("詳細パネルに表示する短いスキル一覧(1行1スキル)")]
        [SerializeField] private string[] _skillSummaryLines = new string[0];

        public CharacterData Data => _characterData;
        public string DisplayName => _characterData != null ? _characterData.DisplayName : _fallbackDisplayName;
        public string RoleName => _characterData != null ? _characterData.RoleName : _fallbackRoleName;
        public Color ThemeColor => _characterData != null ? _characterData.ThemeColor : _fallbackThemeColor;
        public CharacterStatus Status => _characterData != null ? _characterData.CharacterStatus : _fallbackStatus;
        public bool IsSelectable => _characterData != null && _characterData.IsAvailable;
        public string[] SkillSummaryLines => _skillSummaryLines;
    }

    private class CardView
    {
        public Image FrameImage;
        public Image BackgroundImage;
    }

    [Header("キャラクター一覧(上のカードから順に表示)")]
    [SerializeField] private List<CharacterEntry> _characters = new List<CharacterEntry>();

    [Header("シーン遷移")]
    [SerializeField] private string _runeSelectSceneName = "SC_RuneSelect";

    [Header("画面テキスト")]
    [SerializeField] private string _titleText = "HIGH MOBILITY MOBA";
    [SerializeField] private string _subtitleText = "キャラクターを選択";
    [SerializeField] private string _startButtonLabel = "ルーンを選択";
    [SerializeField] private string _availableLabel = "選択可能";
    [SerializeField] private string _comingSoonLabel = "Coming Soon";
    [SerializeField] private string _selectedLabelPrefix = "選択中：";

    [Header("見た目設定")]
    [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Color _screenBackgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
    [SerializeField] private Color _cardBackgroundColor = new Color(0.13f, 0.15f, 0.2f, 1f);
    [SerializeField] private Color _cardNormalFrameColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color _selectedFrameColor = new Color(1f, 0.95f, 0.55f, 1f);
    [Tooltip("選択中カードを明るくする度合い(0〜1)")]
    [SerializeField] [Range(0f, 1f)] private float _selectedBrightness = 0.2f;
    [Tooltip("Coming Soonカードの透明度")]
    [SerializeField] [Range(0f, 1f)] private float _comingSoonAlpha = 0.45f;
    [SerializeField] private Color _detailPanelColor = new Color(0.11f, 0.13f, 0.17f, 1f);
    [SerializeField] private Color _startButtonColor = new Color(0.2f, 0.45f, 1f, 1f);

    // 画面レイアウト(基準解像度でのピクセル値)。仮UIのため定数で持つ。
    private const float CardAreaLeft = 48f;
    private const float CardAreaWidth = 500f;
    private const float CardHeight = 128f;
    private const float CardSpacing = 14f;

    private readonly List<CardView> _cardViews = new List<CardView>();
    private Font _font;
    private int _selectedIndex = -1;

    private Image _detailPlaceholderImage;
    private Text _detailNameText;
    private Text _detailRoleText;
    private Text _detailDescriptionText;
    private Text _detailStatsText;
    private Text _detailSkillsText;
    private Text _selectedCharacterText;
    private Button _startButton;

    private void Start()
    {
        // 外部フォントを追加しないため、Unity組み込みのLegacyRuntimeフォントを使用する。
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureEventSystem();
        BuildUi();
        SelectFirstAvailableCharacter();
    }

    /// <summary>New Input System対応のEventSystemをシーンに用意する。</summary>
    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUi()
    {
        RectTransform canvasRect = CreateCanvas();

        Image background = CreateImage("Background", canvasRect, _screenBackgroundColor);
        SetRect(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 画面上部: タイトルとサブタイトル
        Text title = CreateText("Title", canvasRect, _titleText, 64, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -100f), new Vector2(-40f, -24f));

        Text subtitle = CreateText("Subtitle", canvasRect, _subtitleText, 30, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.95f, 1f));
        SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -150f), new Vector2(-40f, -104f));

        // 画面左側: キャラクターカード一覧(縦並び)
        RectTransform cardArea = CreateUiObject("CardArea", canvasRect);
        SetRect(cardArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(CardAreaLeft, 150f), new Vector2(CardAreaLeft + CardAreaWidth, -170f));

        for (int i = 0; i < _characters.Count; i++)
        {
            CreateCard(cardArea, i);
        }

        // 画面右側: 詳細パネル
        BuildDetailPanel(canvasRect);

        // 画面下部: 選択中キャラクター名と開始ボタン
        BuildBottomBar(canvasRect);
    }

    private RectTransform CreateCanvas()
    {
        GameObject canvasObject = new GameObject("CharacterSelectCanvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = _referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        return canvasObject.GetComponent<RectTransform>();
    }

    private void CreateCard(RectTransform cardArea, int index)
    {
        CharacterEntry entry = _characters[index];
        bool isSelectable = entry.IsSelectable;

        // カードの外枠(選択中は明るい枠線になる)
        Image frame = CreateImage("CharacterCard_" + entry.DisplayName, cardArea, _cardNormalFrameColor);
        frame.raycastTarget = true;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0f, 1f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        float top = -index * (CardHeight + CardSpacing);
        frameRect.offsetMin = new Vector2(0f, top - CardHeight);
        frameRect.offsetMax = new Vector2(0f, top);

        // カード背景
        Image cardBackground = CreateImage("Background", frameRect, _cardBackgroundColor);
        SetRect(cardBackground.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

        // イメージカラー(左側の色付きパネル)
        Image colorSwatch = CreateImage("ThemeColor", cardBackground.rectTransform, entry.ThemeColor);
        SetRect(colorSwatch.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(96f, -10f));

        // キャラクター名
        Text nameText = CreateText("NameText", cardBackground.rectTransform, entry.DisplayName, 30, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        SetRect(nameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(112f, 62f), new Vector2(-12f, -10f));

        // 役割
        Text roleText = CreateText("RoleText", cardBackground.rectTransform, entry.RoleName, 20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.75f, 0.8f, 0.9f, 1f));
        SetRect(roleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(112f, 34f), new Vector2(-12f, -58f));

        // 利用可能状態
        string statusLabel = entry.Status == CharacterStatus.Available ? _availableLabel : _comingSoonLabel;
        Color statusColor = isSelectable ? new Color(0.55f, 1f, 0.6f, 1f) : new Color(1f, 0.75f, 0.4f, 1f);
        Text statusText = CreateText("StatusText", cardBackground.rectTransform, statusLabel, 20, FontStyle.Bold, TextAnchor.LowerLeft, statusColor);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(112f, 8f), new Vector2(-12f, 34f));

        // Coming Soonカードは半透明表示にする
        CanvasGroup canvasGroup = frame.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = isSelectable ? 1f : _comingSoonAlpha;

        // クリック処理(選択不可のカードはクリックしても選択状態を変更しない)
        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.None;
        button.interactable = isSelectable;
        int capturedIndex = index;
        button.onClick.AddListener(() => OnCardClicked(capturedIndex));

        _cardViews.Add(new CardView { FrameImage = frame, BackgroundImage = cardBackground });
    }

    private void BuildDetailPanel(RectTransform canvasRect)
    {
        Image panel = CreateImage("DetailPanel", canvasRect, _detailPanelColor);
        SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(CardAreaLeft + CardAreaWidth + 32f, 150f), new Vector2(-48f, -170f));
        RectTransform panelRect = panel.rectTransform;

        // イメージカラーを使った大きな仮プレースホルダー
        _detailPlaceholderImage = CreateImage("CharacterPlaceholder", panelRect, Color.gray);
        SetRect(_detailPlaceholderImage.rectTransform, new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(24f, 24f), new Vector2(-12f, -24f));

        _detailNameText = CreateText("NameText", panelRect, "", 44, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        SetRect(_detailNameText.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(12f, -84f), new Vector2(-24f, -24f));

        _detailRoleText = CreateText("RoleText", panelRect, "", 26, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.75f, 0.8f, 0.9f, 1f));
        SetRect(_detailRoleText.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(12f, -124f), new Vector2(-24f, -88f));

        _detailDescriptionText = CreateText("DescriptionText", panelRect, "", 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.9f, 0.92f, 0.96f, 1f));
        SetRect(_detailDescriptionText.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(12f, -224f), new Vector2(-24f, -132f));

        _detailStatsText = CreateText("StatsText", panelRect, "", 24, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetRect(_detailStatsText.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(12f, -464f), new Vector2(-24f, -236f));

        _detailSkillsText = CreateText("SkillsText", panelRect, "", 24, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        SetRect(_detailSkillsText.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 1f), new Vector2(12f, 24f), new Vector2(-24f, -476f));
    }

    private void BuildBottomBar(RectTransform canvasRect)
    {
        _selectedCharacterText = CreateText("SelectedCharacterText", canvasRect, "", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(_selectedCharacterText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 98f), new Vector2(-40f, 142f));

        Image buttonImage = CreateImage("StartButton", canvasRect, _startButtonColor);
        buttonImage.raycastTarget = true;
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 20f);
        buttonRect.sizeDelta = new Vector2(420f, 70f);

        _startButton = buttonImage.gameObject.AddComponent<Button>();
        _startButton.targetGraphic = buttonImage;
        _startButton.onClick.AddListener(OnStartButtonClicked);

        Text buttonText = CreateText("ButtonText", buttonRect, _startButtonLabel, 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void SelectFirstAvailableCharacter()
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            if (_characters[i].IsSelectable)
            {
                ApplySelection(i);
                return;
            }
        }

        ApplySelection(-1);
    }

    private void OnCardClicked(int index)
    {
        if (index < 0 || index >= _characters.Count)
        {
            return;
        }

        // Availableのキャラクターだけを選択可能にする。
        if (!_characters[index].IsSelectable)
        {
            return;
        }

        ApplySelection(index);
    }

    private void ApplySelection(int index)
    {
        _selectedIndex = index;
        CharacterEntry selected = index >= 0 && index < _characters.Count ? _characters[index] : null;

        // 選択中のCharacterDataをマネージャーへ渡す(シーン遷移後も参照できる)。
        CharacterSelectionManager.GetOrCreateInstance().SelectCharacter(selected != null ? selected.Data : null);

        RefreshCardVisuals();
        RefreshDetailPanel(selected);

        _selectedCharacterText.text = selected != null ? _selectedLabelPrefix + selected.DisplayName : "";
        _startButton.interactable = selected != null && selected.Data != null;
    }

    private void RefreshCardVisuals()
    {
        for (int i = 0; i < _cardViews.Count; i++)
        {
            bool isSelected = i == _selectedIndex;
            CardView view = _cardViews[i];

            // 選択中カードは明るい枠線を表示し、カードを少し明るくする。
            view.FrameImage.color = isSelected ? _selectedFrameColor : _cardNormalFrameColor;
            view.BackgroundImage.color = isSelected
                ? Color.Lerp(_cardBackgroundColor, Color.white, _selectedBrightness)
                : _cardBackgroundColor;
        }
    }

    private void RefreshDetailPanel(CharacterEntry entry)
    {
        if (entry == null || entry.Data == null)
        {
            _detailPlaceholderImage.color = Color.gray;
            _detailNameText.text = "";
            _detailRoleText.text = "";
            _detailDescriptionText.text = "";
            _detailStatsText.text = "";
            _detailSkillsText.text = "";
            return;
        }

        CharacterData data = entry.Data;
        _detailPlaceholderImage.color = data.ThemeColor;
        _detailNameText.text = data.DisplayName;
        _detailRoleText.text = data.RoleName;
        _detailDescriptionText.text = data.ShortDescription;
        _detailStatsText.text =
            "基礎HP：" + data.BaseHp + "\n" +
            "基礎AD：" + data.BaseAttackDamage + "\n" +
            "基礎AS：" + data.BaseAttackSpeed + "\n" +
            "基礎AR：" + data.BaseArmor + "\n" +
            "基礎MS：" + data.BaseMoveSpeed + "\n" +
            "AA Range：" + data.BaseAttackRange;
        _detailSkillsText.text = string.Join("\n", entry.SkillSummaryLines);
    }

    private void OnStartButtonClicked()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _characters.Count)
        {
            return;
        }

        CharacterEntry selected = _characters[_selectedIndex];
        if (selected.Data == null)
        {
            return;
        }

        // 選択結果を保持したままSC_Prototypeを読み込む(Playerへの適用は今回行わない)。
        CharacterSelectionManager.GetOrCreateInstance().SelectCharacter(selected.Data);
        SceneManager.LoadScene(_runeSelectSceneName);
    }

    private RectTransform CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name);
        uiObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = uiObject.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateUiObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        RectTransform rect = CreateUiObject(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
