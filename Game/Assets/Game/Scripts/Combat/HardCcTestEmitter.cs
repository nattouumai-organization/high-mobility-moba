using UnityEngine;

/// <summary>
/// テスト用: 一定間隔で対象へCC(スタン・スネア・スロウ)を発射するコンポーネント。
/// TrainingDummyなどに手動でアタッチして使う。発射前に予告ログを出すので、それに合わせてDを押すと
/// 共通Dの0.20秒無効化のタイミングを確認できる(スロウはハードCCではないため共通Dでは防げない)。
/// CCの種類はInspectorのCc Typeで切り替える。
/// 製品コードからは参照しない(テスト専用)。
/// </summary>
public class HardCcTestEmitter : MonoBehaviour
{
    public enum TestCcType
    {
        Stun,
        Snare,
        Slow,
    }

    // 発射対象。未設定の場合はPlayer(PlayerInputHubを持つオブジェクト)を自動検索する。
    [SerializeField] private CrowdControlController _target;
    // 発射するCCの種類。
    [SerializeField] private TestCcType _ccType = TestCcType.Stun;
    [SerializeField, Min(0.5f)] private float _interval = 3f;
    // 発射の何秒前に予告ログを出すか。
    [SerializeField, Min(0f)] private float _warningLead = 1f;
    [SerializeField, Min(0.05f)] private float _ccDuration = 1f;
    // スロウ発射時の減速率(%)。
    [SerializeField, Range(1f, 99f)] private float _slowPercent = 40f;
    [SerializeField] private bool _emitEnabled = true;

    private float _nextFireTime;
    private bool _warned;

    private void Start()
    {
        if (_target == null)
        {
            PlayerInputHub hub = FindFirstObjectByType<PlayerInputHub>();
            if (hub != null)
            {
                _target = hub.GetComponent<CrowdControlController>();
                if (_target == null) _target = hub.gameObject.AddComponent<CrowdControlController>();
            }
        }

        if (_target == null)
        {
            Debug.LogWarning("HardCcTestEmitter: 発射対象が見つかりません。InspectorでTargetを設定してください。", this);
        }

        _nextFireTime = Time.time + _interval;
    }

    private void Update()
    {
        if (!_emitEnabled || _target == null) return;

        if (!_warned && Time.time >= _nextFireTime - _warningLead)
        {
            _warned = true;
            Debug.Log($"HardCcTestEmitter: {_warningLead:F1}秒後に{_ccType}を発射します。", this);
        }

        if (Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + _interval;
            _warned = false;
            Fire();
        }
    }

    [ContextMenu("今すぐCCを発射する")]
    private void Fire()
    {
        if (_target == null) return;

        switch (_ccType)
        {
            case TestCcType.Stun:
            {
                bool blocked = _target.ApplyStun(_ccDuration, transform);
                Debug.Log(blocked
                    ? "HardCcTestEmitter: スタンは共通Dに無効化されました。"
                    : "HardCcTestEmitter: スタンが命中しました。", this);
                break;
            }
            case TestCcType.Snare:
            {
                bool blocked = _target.ApplySnare(_ccDuration, transform);
                Debug.Log(blocked
                    ? "HardCcTestEmitter: スネアは共通Dに無効化されました。"
                    : "HardCcTestEmitter: スネアが命中しました。", this);
                break;
            }
            case TestCcType.Slow:
            {
                _target.ApplySlow(_slowPercent, _ccDuration);
                Debug.Log("HardCcTestEmitter: スロウを適用しました(スロウは共通Dでは防げません)。", this);
                break;
            }
        }
    }
}
