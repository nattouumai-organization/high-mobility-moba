using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ルーン選択画面UI(フェーズ7ルーン)。
/// 左=プレイヤーキャラ名（青）／右=敵キャラ名（赤）／中央下=ルーンアイコン。
/// アイコンホバーで詳細ツールチップ表示。
/// CharacterSelectionUIから遷移し、确定後にSC_Prototypeを読み追む。
/// </summary>
public class RuneSelectionUI : MonoBehaviour
{
    [SerializeField] private string _prototypeSceneName = "SC_Prototype";
    [SerializeField] private string _enemyName = "Training Dummy";

    private Font _font;
    private RuneType _selected = RuneType.Relentless;

    private sealed class IconView
    {
        public RuneType Rune;
        public Image Frame;
    }

    private readonly IconView[] _icons = new IconView[4];
    private GameObject _tooltip;
    private Text _ttTitle;
    private Text _ttBody;
    private Text _confirmLbl;

    private static readonly RuneType[] Order =
        { RuneType.Relentless, RuneType.Indomitable, RuneType.Pursuit, RuneType.Siege };

    private static string Disp(RuneType r) => r switch
    {
        RuneType.Relentless  => "連撃",
        RuneType.Indomitable => "不屈",
        RuneType.Pursuit     => "追撃",
        RuneType.Siege       => "攻城",
        _ => "?"
    };

    private static string Desc(RuneType r) => r switch
    {
        RuneType.Relentless =>
            "3秒以内に敵ヒーローへ3回命中\n\n効果: 45 + AD x 35%のダメージ\nMS +12% (1.5秒)\n\nCD: 8秒",
        RuneType.Indomitable =>
            "2秒以内に最大HPの15%以上のダメージを受ける\n\n効果: 最大HPの8%のシールド (2.5秒)\n\nCD: 40秒",
        RuneType.Pursuit =>
            "EまたはFの後1.25秒以内に敵ヒーローへ命中\n\n効果: 40 + AD x 30%のダメージ\n15%スロウ (0.5秒)\n\nCD: 12秒",
        RuneType.Siege =>
            "味方ミニオンが敵タワー射程内にいる\n\n効果: タワーへのダメージ +12%\n\nCD: なし (パッシブ)",
        _ => ""
    };

    private static Color RuneCol(RuneType r) => r switch
    {
        RuneType.Relentless  => new Color(1f, 0.55f, 0.15f, 1f),
        RuneType.Indomitable => new Color(0.3f, 0.75f, 1f, 1f),
        RuneType.Pursuit     => new Color(0.5f, 1f, 0.45f, 1f),
        RuneType.Siege       => new Color(1f, 0.85f, 0.25f, 1f),
        _ => Color.gray
    };

    private void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureES();
        BuildUi();
        Pick(_selected);
    }

    private void EnsureES()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUi()
    {
        RectTransform cv = MakeCanvas();

        // 背景。
        FillImg("Bg", cv, new Color(0.06f, 0.07f, 0.10f, 1f));

        // タイトル。
        Text title = Txt("Title", cv, "RUNE SELECT", 52, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnch(title.rectTransform, 0f, 0.88f, 1f, 1f, 0f, 0f, 0f, 0f);

        // 左：自分キャラ名。
        string playerName = "Hero";
        if (CharacterSelectionManager.Instance?.SelectedCharacter != null)
            playerName = CharacterSelectionManager.Instance.SelectedCharacter.DisplayName;

        Text leftName = Txt("LeftName", cv, playerName, 68, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.4f, 0.7f, 1f, 1f));
        SetAnch(leftName.rectTransform, 0f, 0.45f, 0.45f, 0.88f, 20f, 0f, -20f, 0f);

        // スラッシュ区切り線。
        Image slash = Img("Slash", cv, new Color(0.7f, 0.7f, 0.7f, 1f));
        slash.rectTransform.anchorMin = new Vector2(0.5f, 0.48f);
        slash.rectTransform.anchorMax = new Vector2(0.5f, 0.95f);
        slash.rectTransform.sizeDelta = new Vector2(5f, 0f);
        slash.rectTransform.anchoredPosition = Vector2.zero;
        slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -15f);

        // 右：敵キャラ名。
        Text rightName = Txt("RightName", cv, _enemyName, 68, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(1f, 0.4f, 0.4f, 1f));
        SetAnch(rightName.rectTransform, 0.55f, 0.45f, 1f, 0.88f, 20f, 0f, -20f, 0f);

        // ルーンアイコンバー。
        BuildRuneBar(cv);

        // 確定ボタン。
        BuildConfirm(cv);

        // ツールチップ（最前面）。
        BuildTooltip(cv);
    }

    private void BuildRuneBar(RectTransform cv)
    {
        RectTransform bar = MakeRect("RuneBar", cv);
        SetAnch(bar, 0.1f, 0.05f, 0.9f, 0.42f, 0f, 0f, 0f, 0f);

        float size = 110f, gap = 32f;
        float total = Order.Length * size + (Order.Length - 1) * gap;
        float startX = -total * 0.5f;

        for (int i = 0; i < Order.Length; i++)
        {
            RuneType rune = Order[i];
            float cx = startX + i * (size + gap) + size * 0.5f;
            Color col = RuneCol(rune);

            // 外楸（選择中を示す）。
            Image frame = Img("Frame_" + rune, bar, new Color(0.25f, 0.28f, 0.35f, 1f));
            frame.raycastTarget = true;
            RectTransform fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.pivot = new Vector2(0.5f, 0.5f);
            fr.anchoredPosition = new Vector2(cx, 10f);
            fr.sizeDelta = new Vector2(size + 8f, size + 8f);

            // 背景円。
            Image bgc = Img("Bg_" + rune, fr, new Color(0.12f, 0.14f, 0.18f, 1f));
            Center(bgc.rectTransform, size, size);

            // カラーサークル。
            Image circle = Img("Circle_" + rune, fr, col * 0.55f);
            Center(circle.rectTransform, size - 18f, size - 18f);

            // ルーン名ラベル。
            Text lbl = Txt("Lbl_" + rune, fr, Disp(rune), 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            lbl.raycastTarget = false;
            Center(lbl.rectTransform, size, size);

            Button btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.transition = Selectable.Transition.None;
            RuneType cap = rune;
            btn.onClick.AddListener(() => Pick(cap));

            RuneHoverHandler hov = frame.gameObject.AddComponent<RuneHoverHandler>();
            hov.Init(rune, ShowTip, HideTip);

            _icons[i] = new IconView { Rune = rune, Frame = frame };
        }
    }

    private void BuildConfirm(RectTransform cv)
    {
        Image bi = Img("ConfirmBtn", cv, new Color(0.2f, 0.45f, 1f, 1f));
        bi.raycastTarget = true;
        bi.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        bi.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        bi.rectTransform.pivot = new Vector2(0.5f, 0f);
        bi.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        bi.rectTransform.sizeDelta = new Vector2(380f, 66f);
        Button btn = bi.gameObject.AddComponent<Button>();
        btn.targetGraphic = bi;
        btn.onClick.AddListener(() => SceneManager.LoadScene(_prototypeSceneName));
        _confirmLbl = Txt("ConfLbl", bi.rectTransform, "", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        FillRect(_confirmLbl.rectTransform);
    }

    private void BuildTooltip(RectTransform cv)
    {
        _tooltip = new GameObject("Tooltip");
        _tooltip.layer = LayerMask.NameToLayer("UI");
        RectTransform tr = _tooltip.AddComponent<RectTransform>();
        tr.SetParent(cv, false);
        Image tBg = _tooltip.AddComponent<Image>();
        tBg.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
        tBg.raycastTarget = false;
        tr.anchorMin = new Vector2(0.5f, 0.40f);
        tr.anchorMax = new Vector2(0.5f, 0.40f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = new Vector2(400f, 220f);

        _ttTitle = Txt("TipTitle", tr, "", 22, FontStyle.Bold, TextAnchor.UpperCenter,
            new Color(1f, 0.88f, 0.35f, 1f));
        _ttTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        _ttTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        _ttTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        _ttTitle.rectTransform.anchoredPosition = new Vector2(0f, -12f);
        _ttTitle.rectTransform.sizeDelta = new Vector2(-24f, 34f);

        _ttBody = Txt("TipBody", tr, "", 18, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.88f, 0.90f, 0.94f, 1f));
        _ttBody.rectTransform.anchorMin = new Vector2(0f, 0f);
        _ttBody.rectTransform.anchorMax = new Vector2(1f, 1f);
        _ttBody.rectTransform.offsetMin = new Vector2(14f, 10f);
        _ttBody.rectTransform.offsetMax = new Vector2(-14f, -52f);
        _ttBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        _ttBody.verticalOverflow = VerticalWrapMode.Overflow;

        _tooltip.SetActive(false);
    }

    private void ShowTip(RuneType r, Vector2 _)
    {
        if (_tooltip == null) return;
        _ttTitle.text = Disp(r);
        _ttBody.text  = Desc(r);
        _tooltip.SetActive(true);
    }

    private void HideTip() => _tooltip?.SetActive(false);

    private void Pick(RuneType r)
    {
        _selected = r;
        RuneSelectionManager.GetOrCreateInstance().SelectRune(r);
        for (int i = 0; i < _icons.Length; i++)
        {
            if (_icons[i] == null) continue;
            bool sel = _icons[i].Rune == r;
            _icons[i].Frame.color = sel
                ? RuneCol(_icons[i].Rune)
                : new Color(0.25f, 0.28f, 0.35f, 1f);
        }
        if (_confirmLbl != null) _confirmLbl.text = Disp(r) + " で開始";
    }

    // ---------- Helpers ----------

    private RectTransform MakeCanvas()
    {
        GameObject go = new GameObject("RuneSelectCanvas");
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(transform, false);
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.GetComponent<RectTransform>();
    }

    private RectTransform MakeRect(string n, Transform p)
    {
        GameObject go = new GameObject(n);
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform r = go.AddComponent<RectTransform>();
        r.SetParent(p, false);
        return r;
    }

    private Image Img(string n, Transform p, Color c)
    {
        Image i = MakeRect(n, p).gameObject.AddComponent<Image>();
        i.color = c; i.raycastTarget = false; return i;
    }

    private Image FillImg(string n, Transform p, Color c)
    {
        Image i = Img(n, p, c);
        FillRect(i.rectTransform); return i;
    }

    private Text Txt(string n, Transform p, string v, int fs, FontStyle st, TextAnchor a, Color c)
    {
        Text t = MakeRect(n, p).gameObject.AddComponent<Text>();
        t.font = _font; t.text = v; t.fontSize = fs; t.fontStyle = st;
        t.alignment = a; t.color = c;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void FillRect(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private static void SetAnch(RectTransform r, float xMin, float yMin, float xMax, float yMax,
        float l, float b, float rig, float t)
    {
        r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(rig, t);
    }

    private static void Center(RectTransform r, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(w, h);
    }
}
