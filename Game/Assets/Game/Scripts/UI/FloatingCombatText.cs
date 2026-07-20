using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワールド空間に表示するフローティング戦闘テキスト1つ分の挙動。
/// 与ダメージ(赤)・被ダメージ(青)・回復(緑)の数字を表示し、
/// 短時間上方向へ移動しながらフェードアウトした後、自身を安全に削除する。
/// WorldHealthBarと同じく、常にMain Cameraと同じ向きに揃えることで
/// カメラ方向を向き、カメラに対して裏返らない。
/// 生成と表示要求の受付はCombatTextManagerが行う。
/// TextMeshPro Essentialsが未導入のため、Unity標準のTextを使用する。
/// </summary>
public class FloatingCombatText : MonoBehaviour
{
    private Text _label;
    private Color _baseColor;
    private float _moveSpeed;
    private float _lifetime;
    private float _elapsedTime;
    private bool _isPlaying;
    private Camera _mainCamera;

    /// <summary>表示に使用するTextを設定する。CombatTextManagerが生成直後に呼び出す。</summary>
    public void SetLabel(Text label)
    {
        _label = label;
    }

    /// <summary>
    /// 指定位置からテキスト表示を開始する。移動速度と表示時間はCombatTextManagerのInspector設定値を受け取る。
    /// </summary>
    public void Show(Vector3 worldPosition, string text, Color color, float moveSpeed, float lifetime)
    {
        transform.position = worldPosition;
        _baseColor = color;
        _moveSpeed = moveSpeed;
        _lifetime = Mathf.Max(0.01f, lifetime);
        _elapsedTime = 0f;
        _isPlaying = true;

        if (_label != null)
        {
            _label.text = text;
            _label.color = color;
        }

        _mainCamera = Camera.main;
        FaceMainCamera();
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;

        // 短時間だけ上方向へ移動する。
        transform.position += Vector3.up * (_moveSpeed * Time.deltaTime);

        // 時間経過でフェードアウトする。
        if (_label != null)
        {
            Color color = _baseColor;
            color.a = Mathf.Clamp01(1f - _elapsedTime / _lifetime);
            _label.color = color;
        }

        if (_elapsedTime >= _lifetime)
        {
            _isPlaying = false;

            // 表示終了後は安全に削除する。対象(TrainingDummyなど)には親子付けしていないため、
            // 対象の死亡・非表示化とは独立しており、Missing Referenceは発生しない。
            // 将来プール化する場合は、ここをDestroyからプールへの返却(SetActive(false))へ置き換える。
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        FaceMainCamera();
    }

    private void FaceMainCamera()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                return;
            }
        }

        // カメラと同じ向きに揃えることで、常にカメラ方向を向き、左右が裏返ることもない。
        transform.rotation = _mainCamera.transform.rotation;
    }
}
