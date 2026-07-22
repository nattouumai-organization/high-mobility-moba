using System;
using UnityEngine;

/// <summary>
/// 共通D(全キャラクター共通のカウンタースキル)の第1段階。
/// Dキー押下で0.20秒の無効化ウィンドウを開始し、ウィンドウ中に受けた「最初のハードCC」を1回だけ無効化する。
/// クールダウンは34秒。
/// スキル指定方式はSkillTargetingType.NoTarget(無指定)。反応スキルのためSkillCastModeの対象外とし、押した瞬間に即発動する。
/// 【後続タスク】成功時カウンター(45+AD30%の通常ダメージ・MS10%上昇)はCounterSucceededイベントに接続して実装する。
/// 【後続タスク】失敗時(無効化できずにウィンドウが終了)の0.30秒硬直はWindowExpiredイベントに接続して実装する。
/// ゼルフ専用ではなく、全キャラクターのPlayerオブジェクトにアタッチして使う。
/// </summary>
public class CommonDController : MonoBehaviour
{
    [Header("Common D")]
    // 無効化ウィンドウの長さ(秒)。GAME_DESIGN.md: 0.20秒。将来はScriptableObject化する想定。
    [SerializeField, Min(0f)] private float _invulnerabilityDuration = 0.2f;
    // クールダウン(秒)。GAME_DESIGN.md: 34秒。
    [SerializeField, Min(0f)] private float _cooldown = 34f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWindowActive;
    [SerializeField] private float _remainingCooldown;

    private PlayerInputHub _inputHub;
    private HealthController _health;
    private float _windowEndTime;
    private float _cooldownEndTime;
    private bool _hasBlockedThisWindow;

    /// <summary>
    /// 無効化に成功した瞬間に発火する(引数は攻撃者。nullの場合あり)。
    /// 後続タスクのカウンター攻撃(45+AD30%)とMS10%上昇はこのイベントに接続する。
    /// </summary>
    public event Action<Transform> CounterSucceeded;

    /// <summary>
    /// 無効化に成功しないままウィンドウが終了した瞬間に発火する。
    /// 後続タスクの失敗時硬直(0.30秒)はこのイベントに接続する。
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

        // CCを受け取る入口をこのキャラクターに用意する(未追加でも動くようにget-or-add)。
        if (GetComponent<CrowdControlController>() == null)
        {
            gameObject.AddComponent<CrowdControlController>();
        }
    }

    private void Update()
    {
        _remainingCooldown = RemainingCooldown;

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
            // 失敗時の0.30秒硬直は後続タスクでこのイベントに接続して実装する。
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
        CounterSucceeded?.Invoke(attacker);
        return true;
    }
}
