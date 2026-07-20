using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// フローティング戦闘テキスト(FloatingCombatText)の生成と管理を行うマネージャー。
/// プレイヤー視点の与ダメージ(赤・例: 60)、被ダメージ(黄・例: -10)、回復(緑・例: +3)の表示要求をstatic関数で受け取り、
/// 対象の頭上のワールド空間にWorld Space Canvasの整数テキストを生成する。
/// 与ダメージは攻撃対象の頭上、被ダメージは受けた側の頭上に表示し、1回のダメージで表示は1つだけとする(二重表示しない)。
/// 表示色・高さ・移動速度・表示時間などはInspectorで設定し、C#コードへ直接書かない。
/// 現在はPlayerとTrainingDummyの通常攻撃・ゼルフP回復で使用するが、
/// 将来のキャラクター・ミニオン・タワーからも同じ関数で共通利用できる。
/// プール処理は今回実装しない。生成をCreateTextへ集約してあるため、将来プールへ置き換えやすい。
/// TextMeshPro Essentialsが未導入のため、Unity標準のTextとLegacyRuntimeフォントを使用する(外部フォントは追加しない)。
/// </summary>
public class CombatTextManager : MonoBehaviour
{
    private static CombatTextManager _instance;

    // 与ダメージ表示の色(攻撃対象の頭上に表示する赤)。
    [SerializeField] private Color _damageDealtColor = new Color(1f, 0.3f, 0.25f, 1f);

    // 被ダメージ表示の色(ダメージを受けた側の頭上に表示する黄)。
    [SerializeField] private Color _damageTakenColor = new Color(1f, 0.85f, 0.25f, 1f);

    // 回復表示の色(回復した側の頭上に表示する緑)。
    [SerializeField] private Color _healColor = new Color(0.35f, 1f, 0.4f, 1f);

    // 対象の位置から頭上までの高さ(Unity units)。HPバーより少し上に表示する。
    [SerializeField] private float _headHeightOffset = 2f;

    // 同時に複数表示された場合に重なりすぎないようにする、横方向のランダムオフセットの最大値(Unity units)。
    [SerializeField] private float _randomHorizontalOffset = 0.4f;

    // テキストが上方向へ移動する速度(毎秒Unity units)。
    [SerializeField] private float _moveSpeed = 1.2f;

    // テキストの表示時間(秒)。この時間をかけてフェードアウトする。
    [SerializeField] private float _lifetime = 0.8f;

    // テキストのフォントサイズ。Canvasのスケールと組み合わせてワールド上の大きさが決まる。
    [SerializeField] private int _fontSize = 60;

    // World Space Canvasのスケール。WorldHealthBarと同じく小さな値にする。
    [SerializeField] private float _canvasScale = 0.01f;

    // World Space Canvasのサイズ(ピクセル)。
    [SerializeField] private Vector2 _canvasSize = new Vector2(300f, 100f);

    private Font _font;

    private void Awake()
    {
        // シーン上に複数存在しても最初の1つだけを使用する。
        if (_instance == null)
        {
            _instance = this;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>攻撃対象の頭上に、与えたダメージ量を赤色で表示する(例: 60)。</summary>
    public static void ShowDamageDealt(Vector3 targetPosition, float amount)
    {
        CombatTextManager instance = GetOrCreateInstance();
        instance.Spawn(targetPosition, ToDisplayAmount(amount).ToString(), instance._damageDealtColor);
    }

    /// <summary>ダメージを受けた側の頭上に、受けたダメージ量を黄色で表示する(例: -10)。</summary>
    public static void ShowDamageTaken(Vector3 ownerPosition, float amount)
    {
        CombatTextManager instance = GetOrCreateInstance();
        instance.Spawn(ownerPosition, "-" + ToDisplayAmount(amount), instance._damageTakenColor);
    }

    /// <summary>回復した側の頭上に、回復量を緑色で表示する(例: +3)。</summary>
    public static void ShowHeal(Vector3 ownerPosition, float amount)
    {
        CombatTextManager instance = GetOrCreateInstance();
        instance.Spawn(ownerPosition, "+" + ToDisplayAmount(amount), instance._healColor);
    }

    /// <summary>
    /// 表示用の整数値へ変換する。1未満の値が0と表示されないよう、0より大きい値は最低1として表示する。
    /// </summary>
    private static int ToDisplayAmount(float amount)
    {
        if (amount <= 0f)
        {
            return 0;
        }

        return Mathf.Max(1, Mathf.RoundToInt(amount));
    }

    private static CombatTextManager GetOrCreateInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        // シーン上に配置済みのマネージャー(Inspector設定値を持つ)を優先して使用する。
        _instance = FindFirstObjectByType<CombatTextManager>();
        if (_instance != null)
        {
            return _instance;
        }

        // 見つからない場合は既定値で自動生成する。将来の別シーンでもそのまま利用できる。
        GameObject managerObject = new GameObject("CombatTextManager");
        _instance = managerObject.AddComponent<CombatTextManager>();
        return _instance;
    }

    private void Spawn(Vector3 ownerPosition, string text, Color color)
    {
        Vector3 position = ownerPosition
            + Vector3.up * _headHeightOffset
            + GetRandomHorizontalOffset();

        // 将来プール化する場合は、このCreateText呼び出しをプールからの取得へ置き換える。
        FloatingCombatText combatText = CreateText();
        combatText.Show(position, text, color, _moveSpeed, _lifetime);
    }

    /// <summary>
    /// カメラから見た左右方向に、少しランダムなオフセットを返す。
    /// 同時に複数のダメージが発生しても表示が重なりすぎないようにする。
    /// </summary>
    private Vector3 GetRandomHorizontalOffset()
    {
        Camera mainCamera = Camera.main;
        Vector3 right = mainCamera != null ? mainCamera.transform.right : Vector3.right;
        right.y = 0f;

        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        return right.normalized * Random.Range(-_randomHorizontalOffset, _randomHorizontalOffset);
    }

    /// <summary>
    /// フローティングテキスト1つ分(World Space Canvas + Text + FloatingCombatText)を生成する。
    /// 対象には親子付けせず独立したオブジェクトとして生成するため、
    /// 対象が死亡・非表示化されてもMissing Referenceは発生しない。
    /// </summary>
    private FloatingCombatText CreateText()
    {
        GameObject root = new GameObject("FloatingCombatText", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = _canvasSize;
        rootRect.localScale = Vector3.one * _canvasScale;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.AddComponent<Text>();
        label.font = GetFont();
        label.fontSize = _fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        FloatingCombatText combatText = root.AddComponent<FloatingCombatText>();
        combatText.SetLabel(label);
        return combatText;
    }

    private Font GetFont()
    {
        if (_font == null)
        {
            // 外部フォントは追加せず、Unity組み込みのLegacyRuntimeフォントを使用する。
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return _font;
    }
}
