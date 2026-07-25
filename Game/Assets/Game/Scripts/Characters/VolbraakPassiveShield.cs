using UnityEngine;

/// <summary>
/// ヴォルブラークP(初撃無効化)を管理するコンポーネント。SC_PrototypeのPlayerへアタッチして使用する。
/// 一定時間(Recharge Duration)被弾しないとシールドが展開され、次に受ける攻撃1回を完全に無効化する(ダメージ0)。
/// - シールドは消費されるまで永続する(時間切れでは消えない)。
/// - ミニオン(TargetClassification.Minion)の攻撃はシールドを剥がさない(無効化もされず、通常どおりダメージを受ける)。
///   ミニオン以外(敵ヒーロー・タワー・練習用ダミー・攻撃者不明のダメージ)は全て無効化の対象で、シールドを消費する。
/// - タワー(TargetClassification.Tower)の攻撃も1回無効化するが、Pは消費される(GAME_DESIGN §12)。
///   タワー本体はフェーズ5で実装予定だが、攻撃者のTargetable分類で判定するため実装後そのまま機能する。
/// - 被弾(実際にHPが減るダメージ)があるたびに無被弾タイマーはリセットされる(ミニオンからの被弾も含む)。
/// HealthControllerのIIncomingDamageModifierとして、HPへ適用する直前にダメージ量を0へ変更する方式
/// (ゼルフWの前方ダメージ軽減と同じ経路)。ダメージ種別(Normal / True)を問わず無効化する。
/// シールド展開中はPlayerの周囲へLineRendererのリングを表示する(Inspectorで無効化可能)。
/// 死亡中は再展開せず、復活時はシールド展開済みで復活する。
/// </summary>
[DisallowMultipleComponent]
public sealed class VolbraakPassiveShield : MonoBehaviour, IIncomingDamageModifier
{
    [Header("シールド")]
    [Tooltip("最後の被弾(またはシールド消費)からシールドが再展開されるまでの時間(秒)")]
    [SerializeField] private float _rechargeDuration = 10f;

    [Header("見た目")]
    [Tooltip("シールド展開中にPlayerの周囲へリングを表示する")]
    [SerializeField] private bool _showShieldRing = true;

    [Tooltip("シールドリングの色")]
    [SerializeField] private Color _ringColor = new Color(0.95f, 0.9f, 0.55f, 0.9f);

    [Tooltip("シールドリングの半径(Unity units)")]
    [SerializeField] private float _ringRadius = 0.9f;

    [Tooltip("シールドリングのPlayer中心からの高さ(ローカル座標)")]
    [SerializeField] private float _ringLocalHeight = 0.2f;

    [Tooltip("シールドリングの線の太さ")]
    [SerializeField] private float _ringWidth = 0.06f;

    // リング円周の分割数。多いほど滑らかになる。
    private const int RingSegmentCount = 48;

    private HealthController _healthController;
    private bool _isShieldReady;

    // 最後に被弾した(またはシールドを消費した)時刻。Time.timeAsDouble基準(長時間起動時のfloat精度劣化対策)。
    private double _lastDamagedTime;

    private LineRenderer _ringRenderer;

    /// <summary>シールドが展開中(次の攻撃を無効化できる状態)かどうか。</summary>
    public bool IsShieldReady => _isShieldReady;

    private void Awake()
    {
        _healthController = GetComponent<HealthController>();
        CreateRingRenderer();
    }

    private void OnEnable()
    {
        if (_healthController != null)
        {
            _healthController.Died += HandleDied;
            _healthController.Revived += HandleRevived;
        }
    }

    private void OnDisable()
    {
        if (_healthController != null)
        {
            _healthController.Died -= HandleDied;
            _healthController.Revived -= HandleRevived;
        }
    }

    private void Start()
    {
        // 試合開始時はシールド展開済みの状態から始める。
        SetShieldReady(true);
    }

    private void Update()
    {
        // 展開済みなら消費まで永続する。死亡中は再展開しない(復活時に展開済みとなる)。
        if (_isShieldReady || (_healthController != null && _healthController.IsDead))
        {
            return;
        }

        if (Time.timeAsDouble - _lastDamagedTime >= _rechargeDuration)
        {
            SetShieldReady(true);
        }
    }

    /// <summary>
    /// HealthControllerがHPへ適用する直前に呼び出すダメージ変更処理。
    /// シールド展開中にミニオン以外からダメージを受けた場合、ダメージを0にしてシールドを消費する。
    /// それ以外(ミニオンの攻撃・シールド未展開)は変更せず、被弾として無被弾タイマーをリセットする。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (currentAmount <= 0f)
        {
            return currentAmount;
        }

        if (_isShieldReady && !IsMinionAttack(context.Attacker))
        {
            // 完全無効化: ダメージ種別(Normal / True)を問わず0にする。タワーの攻撃もここで1回無効化して消費される。
            SetShieldReady(false);
            _lastDamagedTime = Time.timeAsDouble;
            return 0f;
        }

        // 実際にダメージを受けるため、無被弾タイマーをリセットする(ミニオンからの被弾も含む)。
        _lastDamagedTime = Time.timeAsDouble;
        return currentAmount;
    }

    // 攻撃者がミニオン(TargetClassification.Minion)かどうか。
    // 攻撃者不明(null)やTargetableを持たない攻撃者はミニオン扱いしない(=無効化の対象)。
    private static bool IsMinionAttack(Transform attacker)
    {
        if (attacker == null)
        {
            return false;
        }

        Targetable targetable = attacker.GetComponentInParent<Targetable>();
        return targetable != null && targetable.Classification == TargetClassification.Minion;
    }

    private void HandleDied()
    {
        // 死亡中はシールドを解除・非表示にする(復活時に展開済みへ戻す)。
        SetShieldReady(false);
    }

    private void HandleRevived()
    {
        // 復活時はシールド展開済みで復活する。
        SetShieldReady(true);
    }

    private void SetShieldReady(bool isReady)
    {
        _isShieldReady = isReady;

        if (_ringRenderer != null)
        {
            _ringRenderer.enabled = _showShieldRing && isReady;
        }
    }

    // シールド展開中の表示用リングを実行時生成する(子オブジェクトのローカル座標描画のため、Playerの移動へ自動追従する)。
    private void CreateRingRenderer()
    {
        if (!_showShieldRing)
        {
            return;
        }

        GameObject ringObject = new GameObject("VolbraakPassiveShieldRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, _ringLocalHeight, 0f);

        _ringRenderer = ringObject.AddComponent<LineRenderer>();
        _ringRenderer.useWorldSpace = false;
        _ringRenderer.loop = true;
        _ringRenderer.positionCount = RingSegmentCount;
        _ringRenderer.startWidth = _ringWidth;
        _ringRenderer.endWidth = _ringWidth;
        _ringRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _ringRenderer.startColor = _ringColor;
        _ringRenderer.endColor = _ringColor;

        for (int i = 0; i < RingSegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegmentCount;
            _ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * _ringRadius, 0f, Mathf.Sin(angle) * _ringRadius));
        }

        _ringRenderer.enabled = false;
    }
}
