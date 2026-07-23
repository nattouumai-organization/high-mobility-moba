using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゼルフWを管理する。
/// Wキーで発動。持続中は前方からの通常ダメージを軽減し、
/// 周囲W Damage Radius以内の敵に母ティックAD×1.5分のダメージを与える。
/// Character/TrainingDummyに命中した場合はQのCDを即時リセットしロックも解除する。
/// W発動中はAbilityLockControllerへロックを追加し、通常攻撃・Q・E・Rの入力を一括で禁止する(W終了時に解除)。
/// </summary>
[RequireComponent(typeof(HealthController))]
public sealed class ZelfWController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Settings")]
    [SerializeField, Min(0f)] private float _duration = 0.75f;
    [SerializeField, Min(0f)] private float _cooldown = 10f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;
    [SerializeField, Range(0f, 360f)] private float _frontAngle = 120f;
    [SerializeField, Range(0f, 1f)] private float _damageReduction = 0.55f;

    [Header("Area Damage")]
    // Wダメージを与える半径。Shield Radiusより少し大きく設定する。
    [SerializeField, Min(0f)] private float _wDamageRadius = 2.0f;

    // W第1ティックのダメージを計算する際のスナップショットADレート
    // (Duration / TickIntervalティックの合計がAD × TotalADRatioになる)。
    [SerializeField, Range(0f, 5f)] private float _wTotalADRatio = 1.5f;

    // ダメージティック間隔(秒)。
    [SerializeField, Min(0.02f)] private float _wTickInterval = 0.1f;

    [Header("Visual")]
    [SerializeField] private Color _shieldColor = new Color(0.25f, 0.6f, 1f, 0.9f);
    [SerializeField, Min(0.1f)] private float _shieldRadius = 1.5f;
    [SerializeField, Min(0.005f)] private float _shieldWidth = 0.06f;
    [SerializeField, Min(4)] private int _shieldSegments = 24;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWActive;
    [SerializeField] private float _remainingCooldown;

    private HealthController _health;
    private CharacterStats _characterStats;
    private ZelfQController _qController;
    private ZelfPassiveHeal _passiveHeal;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;
    private SkillRangeIndicator _rangeIndicator;
    private float _indicatorYOffset;
    private LayerMask _targetableLayer;

    private float _activeEndTime;
    private float _cooldownEndTime;
    private LineRenderer _shieldArc;
    private Material _shieldMaterial;
    private Coroutine _damageCoroutine;

    // W発動中にCharacterに初めて命中したか（Qリセットは1回のみ）。
    private bool _wQResetTriggered;

    // WがAbilityLockControllerへロックを追加済みか(二重解除・未解除の防止)。
    private bool _lockAdded;

    public bool IsWActive => _isWActive;

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        _characterStats = GetComponent<CharacterStats>();
        _qController = GetComponent<ZelfQController>();
        _passiveHeal = GetComponent<ZelfPassiveHeal>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();

        _rangeIndicator = SkillRangeIndicator.Create(transform, "W Range Indicator");
        CharacterController cc = GetComponent<CharacterController>();
        _indicatorYOffset = cc != null ? cc.center.y - cc.height * 0.5f + 0.05f : 0.05f;

        // TargetableLayerMaskはZelfQControllerと共有する。
        if (_qController != null)
        {
            _targetableLayer = _qController.TargetableLayerMask;
        }

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

    private void OnHealthRevived()
    {
        // 復活時もWのクールダウンは維持する。
        // GAME_DESIGN.mdのデス時CD短縮はR・共通D・Fの60%短縮のみで、Wは対象外(短縮は後続タスクで実装)。
        Deactivate();
    }

    private void Update()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        if (_isWActive && Time.time >= _activeEndTime)
        {
            Deactivate();
        }

        // PlayerDeathHandlerが復活時に従属コンポーネントを一括再有効化しても、次フレームに扇形が残らないよう毎フレーム強制同期。
        if (_shieldArc != null)
        {
            _shieldArc.enabled = _isWActive;
        }

        // NormalCast: 押している間は効果範囲円を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
        UpdateRangeIndicator();

        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.WPressedThisFrame, _inputHub.WReleasedThisFrame))
        {
            HandleWPressed();
        }
    }

    // Wキーを押している間、前方軽減の向き(本体の正面方向)を方向線で表示する(方向指定スキルの可視化)。
    // 発動時の前方判定は本体の正面(transform.forward)を使うため、正面方向をそのまま表示すれば
    // 「発動した場合の向き」と常に一致する(発動中・ロック中・死亡中は表示しない)。
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.WPressed && !_isWActive
            && (_abilityLock == null || !_abilityLock.IsLocked)
            && (_health == null || !_health.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            _rangeIndicator.HideAll();
            return;
        }

        Vector3 origin = transform.position + new Vector3(0f, _indicatorYOffset, 0f);
        _rangeIndicator.ShowDirectionLine(origin, forward.normalized, _wDamageRadius, new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, 0.9f));
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

        // 他の行動ロック中(Eダッシュ中など)は発動できない。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("Zelf W: 他の行動中のため発動できません。", this);
            return;
        }

        _isWActive = true;
        _activeEndTime = Time.time + _duration;
        _cooldownEndTime = Time.time + _cooldown;
        _wQResetTriggered = false;

        // W発動中は通常攻撃・Q・E・Rの入力をロックする
        // (各コントローラーがIsLockedを確認する。コンポーネント自体は無効化しない)。
        if (_abilityLock != null && !_lockAdded)
        {
            _abilityLock.AddLock(AbilityLockController.ReasonZelfW);
            _lockAdded = true;
        }

        RebuildShieldArcPositions();
        _shieldArc.enabled = true;

        if (_damageCoroutine != null) StopCoroutine(_damageCoroutine);
        _damageCoroutine = StartCoroutine(WDamageLoop());

        Debug.Log("Zelf W: 前方ダメージ軽減を発動しました。", this);
    }

    private void Deactivate()
    {
        _isWActive = false;
        if (_shieldArc != null) _shieldArc.enabled = false;

        // コルーチン停止。
        if (_damageCoroutine != null)
        {
            StopCoroutine(_damageCoroutine);
            _damageCoroutine = null;
        }

        // Wが追加したロックを解除する。ロック方式のため、死亡・復活・他スキルと
        // 無効化理由が重なっても復元漏れ・二重復元は起きない。
        if (_abilityLock != null && _lockAdded)
        {
            _abilityLock.RemoveLock(AbilityLockController.ReasonZelfW);
            _lockAdded = false;
        }
    }

    // W持続中、毎_wTickInterval秒周囲の敵にダメージを与えるコルーチン。
    private IEnumerator WDamageLoop()
    {
        // ダメージ/ティック = AD × TotalADRatio / (ティック回数)。
        // ティック回数 = duration / tickInterval。
        float ticksInDuration = _duration / Mathf.Max(0.02f, _wTickInterval);

        while (_isWActive)
        {
            // 毎ティックごとにADを再取得する(W中にADが変わった場合の対応)。
            float ad = _characterStats != null ? _characterStats.CurrentAttackDamage : 0f;
            float damagePerTick = ad * _wTotalADRatio / Mathf.Max(1f, ticksInDuration);

            if (damagePerTick > 0f)
            {
                DealWAreaDamage(damagePerTick);
            }

            yield return new WaitForSeconds(_wTickInterval);
        }

        _damageCoroutine = null;
    }

    // Wダメージを_wDamageRadius以内の全Targetableに与える。
    private void DealWAreaDamage(float damage)
    {
        if (_targetableLayer.value == 0) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, _wDamageRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            Targetable target = col.GetComponentInParent<Targetable>();
            if (target == null || !target.isActiveAndEnabled || target.IsDead) continue;

            HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
            if (health == null || health.IsDead) continue;

            float actual = health.TakeDamage(damage, transform);
            if (actual <= 0f) continue;

            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actual);
            if (_passiveHeal != null)
            {
                _passiveHeal.NotifyDamageDealt(actual, target.Classification);
            }

            // Character/TrainingDummyに初めて命中した時だけQリセット。
            if (!_wQResetTriggered &&
                (target.Classification == TargetClassification.Character ||
                 target.Classification == TargetClassification.TrainingDummy) &&
                _qController != null)
            {
                _wQResetTriggered = true;
                _qController.ResetCooldown();
                _qController.ClearLockout(target);
                Debug.Log("Zelf W: Character分類へ命中！QCDリセットと同一対象ロックを解除しました。", this);
            }
        }
    }

    // IIncomingDamageModifier: 前方から受ける通常ダメージを軽減する。
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
