using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゼルフW(前方ダメージ軽減)を管理する。
/// TASKS.md「ゼルフWの前方ダメージ軽減を実装する」用のスクリプト。
/// Wキー(Input System)で発動し、Duration秒間だけ、前方Front Angle度から受ける通常ダメージをDamage Reduction割合で軽減する。
/// Wは攻撃技ではなくダメージ・ノックバック・スロウ・スタン・スネアを与えず、CC無効化・無敵・対象指定不可・シールドも持たない。
/// 軽減判定は、HealthControllerがHPへ適用する直前に呼ぶIIncomingDamageModifierとして、W持続中に受けたダメージごとに行う。
/// 前方判定はダメージを受けた瞬間のPlayer.transform.forwardと攻撃者への水平方向(Y軸高さは含めない)で行い、
/// 背後・側面からのダメージ、攻撃者情報が取得できないダメージ、確定ダメージ(将来用)は軽減しない。
/// W中も右クリック移動・マウス方向への回転・通常攻撃・Q・Eは制限しない。
/// 持続中はPlayer前方に青い扇形のLineRenderer防御エフェクトを表示し、Playerの回転に追従して終了時に非表示になる
/// (外部アセット・VFX Graph不使用。URP設定は変更しない)。
/// </summary>
[RequireComponent(typeof(HealthController))]
public sealed class ZelfWController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Settings")]
    // Wの持続時間(秒)。
    [SerializeField, Min(0f)] private float _duration = 0.75f;

    // Wのクールダウン(秒)。発動した瞬間から計測する。
    [SerializeField, Min(0f)] private float _cooldown = 10f;

    // 軽減対象となる前方の角度(度)。transform.forwardを中心に左右へ半分ずつ広がる。
    [SerializeField, Range(0f, 360f)] private float _frontAngle = 120f;

    // 前方から受ける通常ダメージの軽減割合(0.55 = 55%軽減)。
    [SerializeField, Range(0f, 1f)] private float _damageReduction = 0.55f;

    [Header("Visual")]
    // 防御エフェクト(前方の青い扇形)の色。
    [SerializeField] private Color _shieldColor = new Color(0.25f, 0.6f, 1f, 0.9f);

    // 防御エフェクトの半径(Unity units)。
    [SerializeField, Min(0.1f)] private float _shieldRadius = 1.1f;

    // 防御エフェクトの線の太さ。
    [SerializeField, Min(0.005f)] private float _shieldWidth = 0.06f;

    // 防御エフェクトの扇形の分割数。
    [SerializeField, Min(4)] private int _shieldSegments = 24;

    [Header("Debug (Runtime)")]
    // W持続中かどうか(Inspector確認用)。
    [SerializeField] private bool _isWActive;

    // Wの残りクールダウン秒数(Inspector確認用)。
    [SerializeField] private float _remainingCooldown;

    private HealthController _health;
    private float _activeEndTime;
    private float _cooldownEndTime;
    private LineRenderer _shieldArc;
    private Material _shieldMaterial;

    /// <summary>W持続中かどうか。</summary>
    public bool IsWActive => _isWActive;

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        CreateShieldArc();
    }

    private void OnDestroy()
    {
        if (_shieldArc != null)
        {
            Destroy(_shieldArc.gameObject);
        }

        if (_shieldMaterial != null)
        {
            Destroy(_shieldMaterial);
        }
    }

    private void Update()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        // 持続時間の終了、または死亡でWを終了する。
        if (_isWActive && (Time.time >= _activeEndTime || _health.IsDead))
        {
            Deactivate();
        }

        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
        {
            HandleWPressed();
        }
    }

    private void HandleWPressed()
    {
        // 発動中の再入力では、持続時間の延長も再発動もしない。
        if (_isWActive)
        {
            Debug.Log("Zelf W: 発動中のため、持続時間の延長・再発動はしません。", this);
            return;
        }

        if (Time.time < _cooldownEndTime)
        {
            Debug.Log("Zelf W: クールダウン中です。", this);
            return;
        }

        if (_health.IsDead)
        {
            return;
        }

        _isWActive = true;
        _activeEndTime = Time.time + _duration;
        _cooldownEndTime = Time.time + _cooldown;

        // Inspectorで前方角度・半径を変更した場合に備え、発動のたびに扇形を再計算する。
        RebuildShieldArcPositions();
        _shieldArc.enabled = true;
        Debug.Log("Zelf W: 前方ダメージ軽減を発動しました。", this);
    }

    private void Deactivate()
    {
        _isWActive = false;

        if (_shieldArc != null)
        {
            _shieldArc.enabled = false;
        }
    }

    /// <summary>
    /// HealthControllerがHPへ適用する直前に呼び出す軽減判定。W持続中に受けたダメージごとに判定する。
    /// 前方Front Angle度から受けた通常ダメージだけをDamage Reduction割合で軽減し、それ以外はそのまま返す。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (!_isWActive || currentAmount <= 0f)
        {
            return currentAmount;
        }

        // 通常ダメージだけを軽減する。将来の確定ダメージ(DamageType.True)は軽減しない。
        if (context.Type != DamageType.Normal)
        {
            return currentAmount;
        }

        // 攻撃者情報が取得できないダメージは軽減しない。
        if (context.Attacker == null)
        {
            return currentAmount;
        }

        // 前方判定: ダメージを受けた瞬間のPlayerの向き(transform.forward)と、
        // 攻撃者への方向の角度で判定する。ダメージのY軸高さは前方判定に含めない。
        Vector3 toAttacker = context.Attacker.position - transform.position;
        toAttacker.y = 0f;

        // 攻撃者がほぼ同じ位置にいる場合は前方として扱う。
        if (toAttacker.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, toAttacker) > _frontAngle * 0.5f)
        {
            // 攻撃者が背後または側面にいる場合、ダメージ軽減しない。
            return currentAmount;
        }

        float reducedAmount = currentAmount * (1f - _damageReduction);
        Debug.Log($"Zelf W: 前方からの通常ダメージを軽減しました({currentAmount:F1} → {reducedAmount:F1})。", this);
        return reducedAmount;
    }

    // Player前方の青い扇形の防御エフェクトを生成する(外部アセット・VFX Graph不使用)。
    // Playerの子オブジェクトにしてローカル座標で描画するため、Playerの回転へ自動的に追従する。
    private void CreateShieldArc()
    {
        GameObject arcObject = new GameObject("Zelf W Shield Arc");
        arcObject.transform.SetParent(transform, false);
        _shieldArc = arcObject.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        _shieldMaterial = new Material(shader);
        _shieldMaterial.color = _shieldColor;

        _shieldArc.useWorldSpace = false;
        _shieldArc.material = _shieldMaterial;
        _shieldArc.startColor = _shieldColor;
        _shieldArc.endColor = _shieldColor;
        _shieldArc.startWidth = _shieldWidth;
        _shieldArc.endWidth = _shieldWidth;
        _shieldArc.numCornerVertices = 4;
        _shieldArc.numCapVertices = 4;
        _shieldArc.alignment = LineAlignment.View;
        _shieldArc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _shieldArc.receiveShadows = false;
        _shieldArc.loop = false;
        RebuildShieldArcPositions();
        _shieldArc.enabled = false;
    }

    // Inspectorの前方角度・半径・分割数の現在値で、前方扇形の頂点を再計算する。
    private void RebuildShieldArcPositions()
    {
        int segments = Mathf.Max(4, _shieldSegments);
        _shieldArc.positionCount = segments + 1;

        float halfAngle = _frontAngle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
            Vector3 localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _shieldRadius;
            _shieldArc.SetPosition(i, localPosition);
        }
    }
}
