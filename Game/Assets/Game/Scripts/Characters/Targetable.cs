using System.Collections;
using UnityEngine;

/// <summary>
/// ターゲット選択される側(TrainingDummyなど)の見た目を管理する。
/// 選択リングの表示・非表示、選択中の本体色の変更、
/// 攻撃射程内外に応じた選択リングの色の切替、
/// 通常攻撃から呼び出す被弾時の短時間フラッシュを持つ。
/// HealthControllerの死亡イベントを受け取り、死亡時は選択不可にして短時間後に非表示化する。
/// </summary>
public class Targetable : MonoBehaviour
{
    // 選択中だけ表示する足元の選択リング。
    [SerializeField] private GameObject _selectionRing;

    // 射程内外に応じて色を変更する選択リングのRenderer。
    [SerializeField] private Renderer _selectionRingRenderer;

    // 選択状態に応じて色を変更する本体のRenderer。
    [SerializeField] private Renderer _bodyRenderer;

    // 選択中の本体色(非選択時より明るい赤)。射程内外では変更しない。
    [SerializeField] private Color _selectedColor = new Color(1f, 0.45f, 0.35f, 1f);

    // 選択中かつ攻撃射程内のときの選択リングの色(明るい緑)。
    [SerializeField] private Color _inRangeRingColor = new Color(0.35f, 1f, 0.35f, 1f);

    // 選択中だが攻撃射程外のときの選択リングの色(オレンジ)。
    [SerializeField] private Color _outOfRangeRingColor = new Color(1f, 0.5f, 0.1f, 1f);

    // 被弾フラッシュの色。
    [SerializeField] private Color _hitFlashColor = Color.white;

    // 被弾フラッシュの表示時間(秒)。
    [SerializeField] private float _hitFlashDuration = 0.15f;

    // 死亡後、死亡状態を確認できる時間(秒)。経過後にGameObjectを非表示にする。
    [SerializeField] private float _deathHideDelay = 0.6f;

    private Collider _collider;
    private Color _defaultColor;
    private bool _isSelected;
    private bool _isInAttackRange;
    private Coroutine _hitFlashCoroutine;
    private HealthController _healthController;
    private bool _isDead;

    public bool IsSelected => _isSelected;

    /// <summary>攻撃射程内として表示中かどうか。PlayerBasicAttackControllerが毎フレーム更新する。</summary>
    public bool IsInAttackRange => _isInAttackRange;

    /// <summary>死亡済みかどうか。死亡後はターゲットとして選択・攻撃できない。</summary>
    public bool IsDead => _isDead;

    /// <summary>自身のHealthController。持たない場合はnull。通常攻撃のダメージ処理から使用する。</summary>
    public HealthController Health => _healthController;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _healthController = GetComponent<HealthController>();

        if (_bodyRenderer != null)
        {
            // 実行時はマテリアルのインスタンスへ色を設定するため、元のマテリアルアセットは変化しない。
            _defaultColor = _bodyRenderer.material.color;
        }

        if (_selectionRing != null)
        {
            _selectionRing.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_healthController != null)
        {
            _healthController.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (_healthController != null)
        {
            _healthController.Died -= HandleDied;
        }
    }

    /// <summary>
    /// 指定位置に最も近い自身のCollider上の点を返す。攻撃射程の判定に使用する。
    /// Colliderが無い場合は自身の位置を返す。
    /// </summary>
    public Vector3 GetClosestPoint(Vector3 position)
    {
        return _collider != null ? _collider.ClosestPoint(position) : transform.position;
    }

    /// <summary>選択状態を設定し、選択リングの表示と本体色を切り替える。</summary>
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;

        if (!isSelected)
        {
            // 未選択時は選択リングが非表示になるため、射程内表示もリセットする。
            _isInAttackRange = false;
        }

        if (_selectionRing != null)
        {
            _selectionRing.SetActive(isSelected);
        }

        ApplyCurrentColor();
        ApplyRingColor();
    }

    /// <summary>
    /// 攻撃射程内かどうかの表示状態を設定し、選択リングの色を切り替える。
    /// 射程内は明るい緑、射程外はオレンジで表示する。本体色は変更しない。
    /// </summary>
    public void SetInAttackRange(bool isInAttackRange)
    {
        if (_isInAttackRange == isInAttackRange)
        {
            return;
        }

        _isInAttackRange = isInAttackRange;
        ApplyRingColor();
    }

    /// <summary>
    /// 被弾時の視覚フィードバック。短時間だけフラッシュ色で点灯し、選択状態に応じた通常の色へ戻る。
    /// 通常攻撃(PlayerBasicAttackController)から呼び出す。
    /// </summary>
    public void PlayHitFlash()
    {
        if (_bodyRenderer == null || !isActiveAndEnabled)
        {
            return;
        }

        if (_hitFlashCoroutine != null)
        {
            StopCoroutine(_hitFlashCoroutine);
        }

        _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _bodyRenderer.material.color = _hitFlashColor;
        yield return new WaitForSeconds(_hitFlashDuration);

        ApplyCurrentColor();
        _hitFlashCoroutine = null;
    }

    /// <summary>
    /// 死亡時の処理。選択不可にし、短時間死亡状態を表示した後にGameObjectを非表示にする。
    /// Destroyは使わず非表示化するため、他コンポーネントからのMissing Referenceは発生しない。
    /// </summary>
    private void HandleDied()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        // 以後はクリックで選択できないよう、Colliderを無効化する。
        if (_collider != null)
        {
            _collider.enabled = false;
        }

        // 選択リングを非表示にする(選択自体の解除はPlayerTargetSelectorが行う)。
        if (_selectionRing != null)
        {
            _selectionRing.SetActive(false);
        }

        // 被弾フラッシュの後、短時間だけ死亡状態を確認できるようにしてから非表示にする。
        StartCoroutine(DeathHideRoutine());
    }

    private IEnumerator DeathHideRoutine()
    {
        yield return new WaitForSeconds(_deathHideDelay);
        gameObject.SetActive(false);
    }

    private void ApplyCurrentColor()
    {
        if (_bodyRenderer == null)
        {
            return;
        }

        _bodyRenderer.material.color = _isSelected ? _selectedColor : _defaultColor;
    }

    private void ApplyRingColor()
    {
        if (_selectionRingRenderer == null)
        {
            return;
        }

        // 実行時はマテリアルのインスタンスへ色を設定するため、元のマテリアルアセットは変化しない。
        _selectionRingRenderer.material.color = _isInAttackRange ? _inRangeRingColor : _outOfRangeRingColor;
    }
}
