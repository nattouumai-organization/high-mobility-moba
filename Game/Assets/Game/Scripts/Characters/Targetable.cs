using System.Collections;
using UnityEngine;

/// <summary>
/// ターゲット選択される側(TrainingDummyなど)の見た目を管理する。
/// 選択リングの表示・非表示、選択中の本体色の変更、
/// 将来の通常攻撃から呼び出す被弾時の短時間フラッシュを持つ。
/// HP・ダメージ・死亡処理は今回実装しない。
/// </summary>
public class Targetable : MonoBehaviour
{
    // 選択中だけ表示する足元の選択リング。
    [SerializeField] private GameObject _selectionRing;

    // 選択状態に応じて色を変更する本体のRenderer。
    [SerializeField] private Renderer _bodyRenderer;

    // 選択中の本体色(非選択時より明るい赤)。
    [SerializeField] private Color _selectedColor = new Color(1f, 0.45f, 0.35f, 1f);

    // 被弾フラッシュの色。
    [SerializeField] private Color _hitFlashColor = Color.white;

    // 被弾フラッシュの表示時間(秒)。
    [SerializeField] private float _hitFlashDuration = 0.15f;

    private Color _defaultColor;
    private bool _isSelected;
    private Coroutine _hitFlashCoroutine;

    public bool IsSelected => _isSelected;

    private void Awake()
    {
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

    /// <summary>選択状態を設定し、選択リングの表示と本体色を切り替える。</summary>
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;

        if (_selectionRing != null)
        {
            _selectionRing.SetActive(isSelected);
        }

        ApplyCurrentColor();
    }

    /// <summary>
    /// 被弾時の視覚フィードバック。短時間だけフラッシュ色で点灯し、選択状態に応じた通常の色へ戻る。
    /// 将来の通常攻撃実装から呼び出す想定で、現時点ではどこからも呼び出さない。
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

    private void ApplyCurrentColor()
    {
        if (_bodyRenderer == null)
        {
            return;
        }

        _bodyRenderer.material.color = _isSelected ? _selectedColor : _defaultColor;
    }
}
