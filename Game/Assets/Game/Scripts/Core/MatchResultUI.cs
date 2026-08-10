using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 外部アセットを使わず、試合終了時の全画面リザルトUIを実行時生成する。
/// TextMeshProへの依存を増やさないためUnity標準Textを使用する。
/// </summary>
public sealed class MatchResultUI : MonoBehaviour
{
    private Canvas _canvas;
    private Image _panel;
    private Image _accentBar;
    private Text _resultTitle;
    private Text _resultJapanese;
    private Text _winnerLabel;
    private Text _descriptionLabel;
    private Text _pointsLabel;
    private bool _shown;

    private void Awake()
    {
        CreateUi();
        SetVisible(false);
    }

    /// <summary>結果UIを1回だけ表示する。</summary>
    public void ShowResult(Team winningTeam, Team losingTeam)
    {
        if (_shown)
        {
            return;
        }

        _shown = true;
        TeamMember localMember = FindLocalPlayerTeamMember();
        bool hasLocalTeam = localMember != null;
        bool localWon = hasLocalTeam && localMember.Team == winningTeam;

        if (_resultTitle != null)
        {
            _resultTitle.text = hasLocalTeam ? (localWon ? "VICTORY" : "DEFEAT") : "MATCH FINISHED";
        }

        if (_resultJapanese != null)
        {
            _resultJapanese.text = hasLocalTeam ? (localWon ? "勝利" : "敗北") : "試合終了";
        }

        Color accent = !hasLocalTeam || localWon
            ? winningTeam.GetTeamColor()
            : new Color(0.48f, 0.12f, 0.12f, 1f);

        if (_resultTitle != null) _resultTitle.color = accent;
        if (_resultJapanese != null) _resultJapanese.color = Color.Lerp(accent, Color.white, 0.35f);
        if (_accentBar != null) _accentBar.color = accent;

        if (_winnerLabel != null)
        {
            _winnerLabel.text = $"勝利チーム: {GetTeamDisplayName(winningTeam)}";
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.text =
                $"{GetTeamDisplayName(losingTeam)}の第2タワーが破壊されました\n" +
                $"破壊された第2タワー: {GetTeamDisplayName(losingTeam)}";
        }

        int winnerPoints = PointsManager.GetPoints(winningTeam);
        int loserPoints = PointsManager.GetPoints(losingTeam);
        string localPoints = hasLocalTeam
            ? $"現在のチームポイント: {PointsManager.GetPoints(localMember.Team)} pt"
            : "現在のチームポイント: Player未生成またはTeam未設定";

        if (_pointsLabel != null)
        {
            _pointsLabel.text =
                $"{localPoints}\n" +
                $"勝利チーム: {winnerPoints} pt\n" +
                $"敗北チーム: {loserPoints} pt";
        }

        SetVisible(true);
    }

    private TeamMember FindLocalPlayerTeamMember()
    {
        PlayerInputHub[] players = FindObjectsByType<PlayerInputHub>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerInputHub player in players)
        {
            if (player == null)
            {
                continue;
            }

            TeamMember member = player.GetComponent<TeamMember>();
            if (member != null)
            {
                return member;
            }
        }

        return null;
    }

    private static string GetTeamDisplayName(Team team)
    {
        return team == Team.Blue ? "BLUE / ブルーチーム" : "RED / レッドチーム";
    }

    private void CreateUi()
    {
        if (_canvas != null)
        {
            return;
        }

        Font font = LoadUiFont();

        GameObject canvasObject = new GameObject("Match Result Canvas");
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        _panel = CreateImage("Overlay", canvasObject.transform, new Color(0.015f, 0.02f, 0.035f, 0.88f));
        StretchToParent(_panel.rectTransform);

        GameObject card = new GameObject("Result Card");
        card.transform.SetParent(_panel.transform, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(980f, 650f);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.055f, 0.065f, 0.09f, 0.97f);

        _accentBar = CreateImage("Accent", card.transform, Color.white);
        RectTransform accentRect = _accentBar.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(0f, -12f);
        accentRect.offsetMax = Vector2.zero;

        _resultTitle = CreateText("Result", card.transform, font, 96, FontStyle.Bold);
        SetRect(_resultTitle.rectTransform, new Vector2(50f, -40f), new Vector2(-50f, -155f));

        _resultJapanese = CreateText("Result Japanese", card.transform, font, 44, FontStyle.Bold);
        SetRect(_resultJapanese.rectTransform, new Vector2(50f, -155f), new Vector2(-50f, -220f));

        _winnerLabel = CreateText("Winner", card.transform, font, 34, FontStyle.Bold);
        SetRect(_winnerLabel.rectTransform, new Vector2(70f, -250f), new Vector2(-70f, -305f));

        _descriptionLabel = CreateText("Description", card.transform, font, 28, FontStyle.Normal);
        SetRect(_descriptionLabel.rectTransform, new Vector2(70f, -320f), new Vector2(-70f, -410f));

        _pointsLabel = CreateText("Points", card.transform, font, 28, FontStyle.Normal);
        SetRect(_pointsLabel.rectTransform, new Vector2(70f, -435f), new Vector2(-70f, -585f));
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(visible);
        }
    }

    private static Font LoadUiFont()
    {
        // 外部フォントアセットは使わず、Windows標準の日本語フォントを優先する。
        // 見つからない環境ではUnity組み込みフォントへ安全にフォールバックする。
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP" },
            32);
        return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, Font font, int size, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 offsetMinFromTopLeft, Vector2 offsetMaxFromTopRight)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(offsetMinFromTopLeft.x, offsetMaxFromTopRight.y);
        rect.offsetMax = new Vector2(offsetMaxFromTopRight.x, offsetMinFromTopLeft.y);
    }
}
