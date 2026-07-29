using System.Collections.Generic;
using UnityEngine;

/// <summary>ハードCCの種類。スロウはハードCCではないため含めない。</summary>
public enum HardCcType
{
    Stun,
    Snare,
}

/// <summary>
/// 全キャラクター・ダミー共通の「CC(行動妨害)を受け取る入口」と行動制限の実装。
/// - スタン(ハードCC): 移動・通常攻撃・全スキルを禁止する(AbilityLockControllerへロックを追加)。
/// - スネア(ハードCC): 移動と移動スキル(ゼルフQ/Eなど)を禁止する。通常攻撃とその他のスキルは使用できる。
///   フラッシュ(F)はLoL準拠でスネア中も使用できる(スタン中は行動ロックにより使用不可)。
/// - スロウ(ソフトCC): 移動速度を割合で減少させる。複数のスロウは最も強い1つだけが適用される(LoL方式)。
/// ハードCC(スタン・スネア)は必ずApplyStun/ApplySnare(またはApplyHardCC)を経由させることで、
/// 共通Dの無効化判定を一箇所に集約する。スロウは共通Dで防げないため、ApplySlowは無効化判定を行わない。
/// スロウは発生源を問わず全てApplySlowを経由させる(ZelfRのエリア内・退出スロウも含む)。
/// これにより異なる発生源のスロウが加算されることはなく、常に最も強い1つだけが適用される。
/// 同種のハードCCを重ねて受けた場合、持続時間は加算せず「残り時間が長い方」を採用する。
/// 移動の禁止はPlayerClickMovementがIsMovementBlockedを参照して行い、
/// Q/Rの射程外自動接近(スキル側の直接移動)は本クラスが毎フレーム中止する。
/// 死亡時はすべてのCCを解除する(死亡中の行動禁止は死亡ロックが担当する)。
/// </summary>
public class CrowdControlController : MonoBehaviour
{
    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isStunned;
    [SerializeField] private bool _isSnared;
    [SerializeField] private float _currentSlowPercent;

    private CommonDController _commonD;
    private AbilityLockController _abilityLock;
    private CharacterStats _stats;
    private HealthController _health;
    private PlayerClickMovement _clickMovement;
    private ZelfQController _qController;
    private ZelfRController _rController;

    private float _stunEndTime;
    private float _snareEndTime;
    private bool _stunLockAdded;

    // スロウ効果(減速率%と終了時刻)。最も強い1つだけを移動速度へ適用する。
    private struct SlowEffect
    {
        public float Percent;
        public float EndTime;
    }

    private readonly List<SlowEffect> _slows = new List<SlowEffect>();

    // CharacterStatsへ適用中の移動速度ボーナス(スロウなので0または負の値)。
    private float _appliedSlowBonus;

    /// <summary>スタン中か。移動・通常攻撃・全スキルが禁止される。</summary>
    public bool IsStunned => Time.time < _stunEndTime;

    /// <summary>スネア中か。移動と移動スキルが禁止される。</summary>
    public bool IsSnared => Time.time < _snareEndTime;

    /// <summary>移動が禁止されているか(スタンまたはスネア中)。移動スキルの発動可否判定にも使用する。</summary>
    public bool IsMovementBlocked => IsStunned || IsSnared;

    /// <summary>現在適用中のスロウ率(%)。スロウを受けていない場合は0。</summary>
    public float CurrentSlowPercent => _currentSlowPercent;

    private void Awake()
    {
        _commonD = GetComponent<CommonDController>();
        _abilityLock = GetComponent<AbilityLockController>();
        _stats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        _clickMovement = GetComponent<PlayerClickMovement>();
        _qController = GetComponent<ZelfQController>();
        _rController = GetComponent<ZelfRController>();
    }

    private void OnEnable()
    {
        if (_health != null) _health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (_health != null) _health.Died -= HandleDied;
    }

    private void Update()
    {
        // スタンの期限切れ: 行動ロックを解除する。
        if (_stunLockAdded && !IsStunned)
        {
            _stunLockAdded = false;
            if (_abilityLock != null) _abilityLock.RemoveLock(AbilityLockController.ReasonStun);
            Debug.Log("CrowdControl: スタンが終了しました。", this);
        }

        // スネアの期限切れ(前フレームのデバッグ値との比較でログを1回だけ出す)。
        if (_isSnared && !IsSnared)
        {
            Debug.Log("CrowdControl: スネアが終了しました。", this);
        }

        // スタン・スネア中はQ/Rの射程外自動接近(スキル側の直接移動)も毎フレーム中止する。
        if (IsMovementBlocked) CancelSkillApproaches();

        // 期限切れのスロウを取り除き、最も強いスロウを適用し直す。
        bool removedAny = false;
        for (int i = _slows.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _slows[i].EndTime)
            {
                _slows.RemoveAt(i);
                removedAny = true;
            }
        }
        if (removedAny)
        {
            RefreshSlow();
            if (_slows.Count == 0) Debug.Log("CrowdControl: スロウが終了しました。", this);
        }

        _isStunned = IsStunned;
        _isSnared = IsSnared;
    }

    /// <summary>スタンを適用する。共通Dに無効化された場合はtrueを返す(呼び元はダメージも適用しないこと)。</summary>
    public bool ApplyStun(float duration, Transform attacker)
    {
        return ApplyHardCC(HardCcType.Stun, duration, attacker);
    }

    /// <summary>スネアを適用する。共通Dに無効化された場合はtrueを返す(呼び元はダメージも適用しないこと)。</summary>
    public bool ApplySnare(float duration, Transform attacker)
    {
        return ApplyHardCC(HardCcType.Snare, duration, attacker);
    }

    /// <summary>
    /// ハードCCを適用する(後方互換API)。種類未指定の場合はスタンとして扱う。
    /// </summary>
    public bool ApplyHardCC(float duration, Transform attacker)
    {
        return ApplyHardCC(HardCcType.Stun, duration, attacker);
    }

    /// <summary>
    /// ハードCCを適用する。
    /// 戻り値がtrueの場合は共通Dに無効化された。CC付きスキルの呼び元は、
    /// trueのときそのスキルのダメージも適用しないこと(ダメージとCCの両方を無効化する仕様)。
    /// </summary>
    /// <param name="type">ハードCCの種類(スタン・スネア)。</param>
    /// <param name="duration">CCの持続時間(秒)。</param>
    /// <param name="attacker">CCを発生させた攻撃者。共通D成功時のカウンター対象になる。null可。</param>
    /// <returns>共通Dに無効化された場合はtrue。</returns>
    public bool ApplyHardCC(HardCcType type, float duration, Transform attacker)
    {
        // CommonDControllerが後から追加された場合に備えて再取得する。
        if (_commonD == null) _commonD = GetComponent<CommonDController>();

        if (_commonD != null && _commonD.TryBlockHardCC(attacker))
        {
            return true;
        }

        // 死亡中はCCを受けない(共通Dによる無効化ではないためfalseを返す)。
        if (_health != null && _health.IsDead) return false;

        if (type == HardCcType.Stun) BeginStun(duration);
        else BeginSnare(duration);
        return false;
    }

    /// <summary>
    /// スロウを適用する。スロウはハードCCではないため共通Dでは防げない(無効化判定なし)。
    /// 複数のスロウを同時に受けた場合、最も強い1つだけが移動速度へ適用される(LoL方式)。
    /// ZelfRのエリア内スロウのように短い持続を繰り返し掛け直す場合は、withLogをfalseにしてログの連打を防ぐ。
    /// </summary>
    /// <param name="slowPercent">減速率(%)。40なら基礎移動速度の40%減。</param>
    /// <param name="duration">持続時間(秒)。</param>
    /// <param name="withLog">trueの場合は適用ログを出す。掛け直し(リフレッシュ)用途ではfalseを指定する。</param>
    public void ApplySlow(float slowPercent, float duration, bool withLog = true)
    {
        if (_health != null && _health.IsDead) return;
        if (slowPercent <= 0f || duration <= 0f) return;
        if (_stats == null)
        {
            Debug.LogWarning("CrowdControl: CharacterStatsが見つからないため、スロウを適用できません。", this);
            return;
        }

        _slows.Add(new SlowEffect { Percent = Mathf.Clamp(slowPercent, 0f, 99f), EndTime = Time.time + duration });
        RefreshSlow();
        if (withLog) Debug.Log($"CrowdControl: スロウを受けました({slowPercent:F0}% / {duration:F2}秒)。", this);
    }

    private void BeginStun(float duration)
    {
        _stunEndTime = Mathf.Max(_stunEndTime, Time.time + duration);
        if (!_stunLockAdded)
        {
            if (_abilityLock == null) _abilityLock = GetComponent<AbilityLockController>();
            if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
            _abilityLock.AddLock(AbilityLockController.ReasonStun);
            _stunLockAdded = true;
        }
        InterruptMovement();
        Debug.Log($"CrowdControl: スタンを受けました({duration:F2}秒)。移動・通常攻撃・スキルが禁止されます。", this);
    }

    private void BeginSnare(float duration)
    {
        _snareEndTime = Mathf.Max(_snareEndTime, Time.time + duration);
        InterruptMovement();
        Debug.Log($"CrowdControl: スネアを受けました({duration:F2}秒)。移動と移動スキルが禁止されます(通常攻撃と他のスキルは使用可能)。", this);
    }

    // ハードCC発生時: 進行中の移動命令とQ/Rの射程外自動接近を中止する。
    private void InterruptMovement()
    {
        if (_clickMovement == null) _clickMovement = GetComponent<PlayerClickMovement>();
        if (_clickMovement != null) _clickMovement.StopMovement();
        CancelSkillApproaches();
    }

    private void CancelSkillApproaches()
    {
        if (_qController != null) _qController.CancelPendingApproach();
        if (_rController != null) _rController.CancelPendingApproach();
    }

    // 最も強いスロウをCharacterStatsの移動速度ボーナス(負の値)として適用し直す。
    private void RefreshSlow()
    {
        float strongestPercent = 0f;
        foreach (SlowEffect slow in _slows)
        {
            if (slow.Percent > strongestPercent) strongestPercent = slow.Percent;
        }

        if (_stats != null)
        {
            float desiredBonus = -_stats.BaseMoveSpeed * strongestPercent / 100f;
            if (!Mathf.Approximately(desiredBonus, _appliedSlowBonus))
            {
                // 以前の適用分を解除してから、新しい減速量を適用する(0の解除は実質何もしない)。
                _stats.RemoveMoveSpeedBonus(_appliedSlowBonus);
                _appliedSlowBonus = desiredBonus;
                _stats.AddMoveSpeedBonus(_appliedSlowBonus);
            }
        }
        _currentSlowPercent = strongestPercent;
    }

    // 死亡時: すべてのCCを解除する(死亡中の行動禁止は死亡ロックが担当するため、CCのロックは残さない)。
    private void HandleDied()
    {
        _stunEndTime = 0f;
        _snareEndTime = 0f;
        if (_stunLockAdded)
        {
            _stunLockAdded = false;
            if (_abilityLock != null) _abilityLock.RemoveLock(AbilityLockController.ReasonStun);
        }
        _slows.Clear();
        RefreshSlow();
        _isStunned = false;
        _isSnared = false;
    }
}
