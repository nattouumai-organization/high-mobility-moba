using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(HealthController))]
public sealed class ZelfWController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Settings")]
    [SerializeField, Min(0f)] private float _duration = 0.75f;
    [SerializeField, Min(0f)] private float _cooldown = 10f;
    [SerializeField, Range(0f, 360f)] private float _frontAngle = 120f;
    [SerializeField, Range(0f, 1f)] private float _damageReduction = 0.55f;

    [Header("Visual")]
    [SerializeField] private Color _shieldColor = new Color(0.25f, 0.6f, 1f, 0.9f);
    [SerializeField, Min(0.1f)] private float _shieldRadius = 1.1f;
    [SerializeField, Min(0.005f)] private float _shieldWidth = 0.06f;
    [SerializeField, Min(4)] private int _shieldSegments = 24;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWActive;
    [SerializeField] private float _remainingCooldown;

    private HealthController _health;
    private float _activeEndTime;
    private float _cooldownEndTime;
    private LineRenderer _shieldArc;
    private Material _shieldMaterial;

    public bool IsWActive => _isWActive;

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        // Died: 死亡時即座にエフェクトを消す(Update順序に非依存)。
        // Revived: PlayerDeathHandlerが従属コンポーネントを一括再有効化する前に呼ばれる場合でも
        // 復活後に扇形が居残るバグを防ぐため、こちらでもDeactivate()を実行する。
        _health.Died += OnHealthDied;
        _health.Revived += OnHealthRevived;
        CreateShieldArc();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= OnHealthDied;
            _health.Revived -= OnHealthRevived;
        }
        if (_shieldArc != null) Destroy(_shieldArc.gameObject);
        if (_shieldMaterial != null) Destroy(_shieldMaterial);
    }

    private void OnHealthDied()
    {
        if (_isWActive)
        {
            Deactivate();
            Debug.Log("Zelf W: 死亡により前方ダメージ軽減を終了しました。", this);
        }
    }

    // 復活時にWの状態を強制リセットする。
    // PlayerDeathHandlerがRevivedイベント後に従属コンポーネントを再有効化する場合でも、
    // このハンドラの後にUpdate()で毎フレーム同期するため扇形が居残ることはない。
    private void OnHealthRevived()
    {
        Deactivate();
        _cooldownEndTime = 0f; // 復活時にWCDもリセット（必要に応じて削除可）
    }

    private void Update()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        // 持続時間終了でDeactivate。
        if (_isWActive && Time.time >= _activeEndTime)
        {
            Deactivate();
        }

        // PlayerDeathHandlerが従属コンポーネントを一括再有効化する場合に備え、毎フレーム強制同期する。
        // これによりどのタイミングで扇形が居残しても次フレームには消える。
        if (_shieldArc != null)
        {
            _shieldArc.enabled = _isWActive;
        }

        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
        {
            HandleWPressed();
        }
    }

    private void HandleWPressed()
    {
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
        if (_health.IsDead) return;

        _isWActive = true;
        _activeEndTime = Time.time + _duration;
        _cooldownEndTime = Time.time + _cooldown;
        RebuildShieldArcPositions();
        _shieldArc.enabled = true;
        Debug.Log("Zelf W: 前方ダメージ軽減を発動しました。", this);
    }

    private void Deactivate()
    {
        _isWActive = false;
        if (_shieldArc != null) _shieldArc.enabled = false;
    }

    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (!_isWActive || currentAmount <= 0f) return currentAmount;
        if (context.Type != DamageType.Normal) return currentAmount;
        if (context.Attacker == null) return currentAmount;

        Vector3 toAttacker = context.Attacker.position - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, toAttacker) > _frontAngle * 0.5f)
        {
            return currentAmount;
        }

        float reducedAmount = currentAmount * (1f - _damageReduction);
        Debug.Log($"Zelf W: 前方からの通常ダメージを軽減しました({currentAmount:F1} -> {reducedAmount:F1})。", this);
        return reducedAmount;
    }

    private void CreateShieldArc()
    {
        GameObject arcObject = new GameObject("Zelf W Shield Arc");
        arcObject.transform.SetParent(transform, false);
        _shieldArc = arcObject.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

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

    private void RebuildShieldArcPositions()
    {
        int segments = Mathf.Max(4, _shieldSegments);
        _shieldArc.positionCount = segments + 1;
        float halfAngle = _frontAngle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
            Vector3 localPos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _shieldRadius;
            _shieldArc.SetPosition(i, localPos);
        }
    }
}
