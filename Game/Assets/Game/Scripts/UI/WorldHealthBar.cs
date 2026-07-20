using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 対象の頭上に表示するワールド空間のHPバー。
/// TASKS.md「HPバーを実装する」用の試作スクリプト。
/// HealthControllerのHP変化を購読してUI ImageのFill Amountを更新し、
/// 毎フレームMain Cameraと同じ向きに揃えることで、常にカメラ方向を向き裏返らない。
/// 対象が死亡した場合はHPバーを非表示にし、復活した場合は再表示する。HP数値のテキスト表示は今回実装しない。
/// World Space Canvasの子(Background / Fill)と合わせて使用する。
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    // HPの取得元。未設定の場合はAwakeで親オブジェクトから取得する。
    [SerializeField] private HealthController _healthController;

    // 残りHPを表すFilledタイプのUI Image。HP割合に応じてFill Amountを更新する。
    [SerializeField] private Image _fillImage;

    private Camera _mainCamera;
    private Canvas _canvas;

    private void Awake()
    {
        if (_healthController == null)
        {
            _healthController = GetComponentInParent<HealthController>();
        }

        _mainCamera = Camera.main;
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        if (_healthController == null)
        {
            return;
        }

        _healthController.HealthChanged += HandleHealthChanged;
        _healthController.Died += HandleDied;
        _healthController.Revived += HandleRevived;
    }

    private void OnDisable()
    {
        if (_healthController == null)
        {
            return;
        }

        _healthController.HealthChanged -= HandleHealthChanged;
        _healthController.Died -= HandleDied;
        _healthController.Revived -= HandleRevived;
    }

    private void Start()
    {
        // HealthControllerの初期通知と実行順が前後しても表示が揃うよう、開始時に最新値を反映する。
        if (_healthController != null)
        {
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
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

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (_fillImage == null)
        {
            return;
        }

        // HP 100%でバー全体、50%で半分、0で空になる。
        _fillImage.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    }

    private void HandleDied()
    {
        // 死亡時はHPバーを非表示にする。GameObjectは無効化せずCanvasのみ無効化することで、
        // 復活イベントを受け取ってHPバーを再表示できる。
        if (_canvas != null)
        {
            _canvas.enabled = false;
        }
    }

    private void HandleRevived()
    {
        // 復活時はHPバーを再表示し、全快した現在HPを反映する。
        if (_canvas != null)
        {
            _canvas.enabled = true;
        }

        HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
    }
}
