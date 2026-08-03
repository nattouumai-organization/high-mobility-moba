using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 対象の頭上に表示するワールド空間のHPバー。
/// TASKS.md「HPバーを実装する」用の試作スクリプト。
/// HealthControllerのHP変化を購読してUI ImageのFill Amountを更新し、
/// 毎フレームMain Cameraと同じ向きに揃えることで、常にカメラ方向を向き裏返らない。
/// 対象が死亡した場合はHPバーを非表示にし、復活した場合は再表示する。HP数値のテキスト表示は今回実装しない。
/// World Space Canvasの子(Background / Fill)と合わせて使用する。
/// 実行時生成の場合はInitializeRuntimeでHP取得元とFill Imageを設定する(タワーのHPバーなど)。
/// スプライト未設定のUI ImageはFilledタイプが機能せず常に全面描画される(fillAmountが反映されない)ため、
/// Fill Imageにスプライトが無い場合は白スプライトを自動補完する。
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    // HPの取得元。未設定の場合はAwakeで親オブジェクトから取得する。
    [SerializeField] private HealthController _healthController;

    // 残りHPを表すFilledタイプのUI Image。HP割合に応じてFill Amountを更新する。
    [SerializeField] private Image _fillImage;

    private Camera _mainCamera;
    private Canvas _canvas;

    // 実行時生成バー用に補完する白スプライト(全HPバーで共有)。
    private static Sprite _sharedFillSprite;

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

    /// <summary>
    /// 実行時生成用の初期化。HP取得元とFill Imageを設定し、イベント購読と表示を最新化する。
    /// AddComponent直後(Awake/OnEnable実行後)に呼ぶことを想定している。
    /// </summary>
    /// <param name="healthController">HPの取得元。</param>
    /// <param name="fillImage">残りHPを表すFilledタイプのUI Image。</param>
    public void InitializeRuntime(HealthController healthController, Image fillImage)
    {
        // 既に別のHealthControllerを購読済みの場合は付け替える。
        if (_healthController != null && isActiveAndEnabled)
        {
            _healthController.HealthChanged -= HandleHealthChanged;
            _healthController.Died -= HandleDied;
            _healthController.Revived -= HandleRevived;
        }

        _healthController = healthController;
        _fillImage = fillImage;
        EnsureFillSprite();

        if (_healthController != null && isActiveAndEnabled)
        {
            _healthController.HealthChanged += HandleHealthChanged;
            _healthController.Died += HandleDied;
            _healthController.Revived += HandleRevived;
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
        }
    }

    /// <summary>
    /// Fill Imageにスプライトが無い場合は白スプライトを補完する。
    /// スプライト未設定のImageは常に全面描画され、FilledタイプのfillAmountが反映されないため。
    /// </summary>
    private void EnsureFillSprite()
    {
        if (_fillImage == null || _fillImage.sprite != null)
        {
            return;
        }

        if (_sharedFillSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            _sharedFillSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        _fillImage.sprite = _sharedFillSprite;
    }

    private void Start()
    {
        EnsureFillSprite();

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
