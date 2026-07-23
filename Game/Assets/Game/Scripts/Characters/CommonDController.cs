using System;
using UnityEngine;

/// <summary>
/// 共通D(全キャラクター共通のカウンタースキル)の第1段階。
/// Dキー押下で0.20秒の無効化ウィンドウを開始し、ウィンドウ中に受けた「最初のハードCC」を1回だけ無効化する。
/// クールダウンは34秒。
/// スキル指定方式はSkillTargetingType.NoTarget(無指定)。反応スキルのためSkillCastModeの対象外とし、押した瞬間に即発動する。
/// 成功時: 攻撃者だ45 + ADの30%の通常ダメージのカウンターを与え、短時間MSが10%上がる。追加スタン・スネアは与えない。
/// 失敗時(無効化できずにウィンドウが終了)は何も起きない(仕様変更 2026-07-23: 硬直などのペナルティなし。クールダウンのみ消費)。
/// スタン中・W発動中・Eダッシュ中・死亡中などの行動ロック中(AbilityLockController.IsLocked)は発動できない。
/// 共通DはCCを受ける前に予測して押す技のため、スタンを受けてからの後出しは不可(スネアはDの発動を妨げない)。
/// ゼルフ専用ではなく、全キャラクターのPlayerオブジェクトにアタッチして使う。
/// </summary>
public class CommonDController : MonoBehaviour
{
    [Header("Common D")]
    // 無効化ウィンドウの長さ(秒)。GAME_DESIGN.md: 0.20秒。将来はScriptableObject化する想定。
    [SerializeField, Min(0f)] private float _invulnerabilityDuration = 0.2f;
    // クールダウン(秒)。GAME_DESIGN.md: 34秒。
    [SerializeField, Min(0f)] private float _cooldown = 34f;

    [Header("Counter Attack (成功時)")]
    // カウンターの固定ダメージ。GAME_DESIGN.md: 45。
    [SerializeField, Min(0f)] private float _counterBaseDamage = 45f;
    // カウンターのAD倍率。GAME_DESIGN.md: ADの30%。
    [SerializeField, Min(0f)] private float _counterAdRatio = 0.3f;
    // 成功時のMS上昇率(%)。GAME_DESIGN.md: 10%。
    [SerializeField, Min(0f)] private float _msBoostPercent = 10f;
    // MS上昇の持続時間(秒)。仕様は「短時間」のためInspectorで調整する。
    [SerializeField, Min(0f)] private float _msBoostDuration = 1.5f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWindowActive;
    [SerializeField] private float _remainingCooldown;

    private PlayerInputHub _inputHub;
    private HealthController _health;
    private AbilityLockController _abilityLock;
    private float _windowEndTime;
    private float _cooldownEndTime;
    private bool _hasBlockedThisWindow;
    private CharacterStats _characterStats;
    // 適用中のMS上昇量(未適用は0)とその終了時刻。
    private float _activeMsBonus;
    private float _msBoostEndTime;

    /// <summary>
    /// 無効化に成功した瞬間に発火する(引数は攻撃者。nullの場合あり)。
    /// カウンター攻撃とMS上昇の適用後に発火する(外部システムの拡張用)。
    /// </summary>
    public event Action<Transform> CounterSucceeded;

    /// <summary>
    /// 無効化に成功しないままウィンドウが終了した瞬間に発火する。
    /// 仕様変更により失敗時のペナルティはないため、現在はUIなどの拡張用に残している。
    /// </summary>
    public event Action WindowExpired;

    /// <summary>無効化ウィンドウが有効か。</summary>
    public bool IsWindowActive => _isWindowActive;

    /// <summary>残りクールダウン(秒)。クールダウンUIタスクで使用する想定。</summary>
    public float RemainingCooldown => Mathf.Max(0f, _cooldownEndTime - Time.time);

    private void Awake()
    {
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _health = GetComponent<HealthController>();
        _characterStats = GetComponent<CharacterStats>();

        // スタン・W発動中・Eダッシュ中などの行動ロックを確認するため参照する(未追加でも動くようにget-or-add)。
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();

        // CCを受け取る入口をこのキャラクターに用意する(未追加でも動くようにget-or-add)。
        if (GetComponent<CrowdControlController>() == null)
        {
            gameObject.AddComponent<CrowdControlController>();
        }
    }

    private void Update()
    {
        _remainingCooldown = RemainingCooldown;

        // MS上昇の時間切れ処理(死亡時は即解除)。
        if (_activeMsBonus > 0f && (Time.time >= _msBoostEndTime || (_health != null && _health.IsDead)))
        {
            RemoveMsBoost();
        }

        // 死亡中はウィンドウを即終了する(失敗扱いのイベントは発火しない)。
        if (_isWindowActive && _health != null && _health.IsDead)
        {
            _isWindowActive = false;
        }

        // ウィンドウの時間切れ(=無効化に成功しないまま終了)。
        if (_isWindowActive && Time.time >= _windowEndTime)
        {
            _isWindowActive = false;
            Debug.Log("共通D: 無効化ウィンドウが終了しました(無効化なし)。", this);
            // 仕様変更(2026-07-23): 失敗しても何も起きない(硬直なし)。イベントはUIなどの拡張用に残す。
            WindowExpired?.Invoke();
        }

        if (_inputHub != null && _inputHub.DPressedThisFrame)
        {
            HandleDPressed();
        }
    }

    private void HandleDPressed()
    {
        if (_health != null && _health.IsDead)
        {
            Debug.Log("共通D: 死亡中のため発動できません。", this);
            return;
        }
        // スタン中・W発動中・Eダッシュ中などの行動ロック中は発動できない。
        // 共通DはCCを受ける前に予測して押す技のため、スタンを受けてからの後出しは不可。
        // スネアはロックを追加しないため、仕様どおりスネア中はDを使用できる。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("共通D: 行動ロック中(スタン・他スキル発動中など)のため発動できません。", this);
            return;
        }
        if (_isWindowActive)
        {
            // 発動中の再入力は無視する。
            return;
        }
        if (Time.time < _cooldownEndTime)
        {
            Debug.Log($"共通D: クールダウン中です(残り{_cooldownEndTime - Time.time:F1}秒)。", this);
            return;
        }

        _isWindowActive = true;
        _hasBlockedThisWindow = false;
        _windowEndTime = Time.time + _invulnerabilityDuration;
        _cooldownEndTime = Time.time + _cooldown;
        Debug.Log($"共通D: 発動しました({_invulnerabilityDuration:F2}秒)。", this);
    }

    /// <summary>
    /// CrowdControlControllerから呼ばれる。ウィンドウ中の最初のハードCCであれば無効化してtrueを返す。
    /// 無効化は1回のウィンドウにつき1回のみで、成功したらウィンドウを終了する。
    /// </summary>
    public bool TryBlockHardCC(Transform attacker)
    {
        if (!_isWindowActive || _hasBlockedThisWindow) return false;
        if (Time.time >= _windowEndTime)
        {
            _isWindowActive = false;
            return false;
        }

        _hasBlockedThisWindow = true;
        _isWindowActive = false;
        Debug.Log("共通D: ハードCCを無効化しました。", this);
        PerformCounterAttack(attacker);
        ApplyMsBoost();
        CounterSucceeded?.Invoke(attacker);
        return true;
    }

    // 成功時のカウンター攻撃: 攻撃者だ45 + ADの30%の通常ダメージを与える。
    // 仕様どおり、成功しても追加のスタンやスネアは与えない(ダメージのみ)。
    private void PerformCounterAttack(Transform attacker)
    {
        if (attacker == null)
        {
            Debug.Log("共通D: 攻撃者が不明のため、カウンター攻撃は発生しません。", this);
            return;
        }

        HealthController attackerHealth = attacker.GetComponentInParent<HealthController>();
        if (attackerHealth == null)
        {
            Debug.Log("共通D: 攻撃者にHPがないため、カウンター攻撃は発生しません。", this);
            return;
        }

        float attackDamage = _characterStats != null ? _characterStats.CurrentAttackDamage : 0f;
        float damage = _counterBaseDamage + attackDamage * _counterAdRatio;
        float actualDamage = attackerHealth.TakeDamage(damage, transform);
        Debug.Log($"共通D: カウンター攻撃({damage:F1}ダメージ)を与えました。", this);

        if (actualDamage > 0f)
        {
            Targetable targetable = attacker.GetComponentInParent<Targetable>();
            if (targetable != null) targetable.PlayHitFlash();
        }
    }

    // 成功時のMS上昇: 発動時点のMSの10%をフラット量へ換算して加算する。
    // 効果中に再成功した場合は掛け直し(重複加算しない)。
    private void ApplyMsBoost()
    {
        if (_characterStats == null)
        {
            Debug.Log("共通D: CharacterStatsがないため、MS上昇は発生しません。", this);
            return;
        }

        // 既に適用中なら一度解除してから掛け直す。
        RemoveMsBoost();

        _activeMsBonus = _characterStats.CurrentMoveSpeed * (_msBoostPercent / 100f);
        if (_activeMsBonus <= 0f)
        {
            _activeMsBonus = 0f;
            return;
        }

        _characterStats.AddMoveSpeedBonus(_activeMsBonus);
        _msBoostEndTime = Time.time + _msBoostDuration;
        Debug.Log($"共通D: 移動速度が{_msBoostPercent:F0}%上昇しました({_msBoostDuration:F1}秒)。", this);
    }

    private void RemoveMsBoost()
    {
        if (_activeMsBonus <= 0f) return;
        if (_characterStats != null) _characterStats.RemoveMoveSpeedBonus(_activeMsBonus);
        _activeMsBonus = 0f;
    }

    private void OnDisable()
    {
        RemoveMsBoost();
    }
}
