using UnityEngine;

/// <summary>
/// テスト用: 一定間隔で対象へハードCCを発射し、共通Dの0.20秒無効化のタイミングを確認するためのコンポーネント。
/// TrainingDummyなどに手動でアタッチして使う。発射前に予告ログを出すので、それに合わせてDを押すと成功を確認できる。
/// 製品コードからは参照しない(テスト専用)。
/// </summary>
public class HardCcTestEmitter : MonoBehaviour
{
    // 発射対象。未設定の場合はPlayer(PlayerInputHubを持つオブジェクト)を自動検索する。
    [SerializeField] private CrowdControlController _target;
    [SerializeField, Min(0.5f)] private float _interval = 3f;
    // 発射の何秒前に予告ログを出すか。
    [SerializeField, Min(0f)] private float _warningLead = 1f;
    [SerializeField, Min(0.05f)] private float _ccDuration = 1f;
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
            Debug.Log($"HardCcTestEmitter: {_warningLead:F1}秒後にハードCCを発射します。", this);
        }

        if (Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + _interval;
            _warned = false;
            Fire();
        }
    }

    [ContextMenu("今すぐハードCCを発射する")]
    private void Fire()
    {
        if (_target == null) return;
        bool blocked = _target.ApplyHardCC(_ccDuration, transform);
        Debug.Log(blocked
            ? "HardCcTestEmitter: ハードCCは共通Dに無効化されました。"
            : "HardCcTestEmitter: ハードCCが命中しました。", this);
    }
}
