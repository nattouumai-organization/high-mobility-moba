using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthControllerのHPをワールド空間のFilled Imageへ反映する。
/// 不屈ルーンのシールド表示に加え、朧Rが存在する場合は敵ヒーローのHPバーへ処刑閾値を表示する。
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    private const float ShieldScanInterval = 0.5f;

    [SerializeField] private HealthController _healthController;
    [SerializeField] private Image _fillImage;

    private Camera _mainCamera;
    private Canvas _canvas;
    private IndomitableRune _shieldSource;
    private float _shieldScanTimer;
    private Image _shieldFillImage;
    private Image _oboroExecuteMarker;
    private float _oboroMarkerRatio = -1f;
    private static Sprite _sharedFillSprite;

    private void Awake()
    {
        if (_healthController == null) _healthController = GetComponentInParent<HealthController>();
        _mainCamera = Camera.main;
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_healthController == null) return;
        _healthController.HealthChanged -= HandleHealthChanged;
        _healthController.Died -= HandleDied;
        _healthController.Revived -= HandleRevived;
        _healthController.HealthChanged += HandleHealthChanged;
        _healthController.Died += HandleDied;
        _healthController.Revived += HandleRevived;
    }

    private void Unsubscribe()
    {
        if (_healthController == null) return;
        _healthController.HealthChanged -= HandleHealthChanged;
        _healthController.Died -= HandleDied;
        _healthController.Revived -= HandleRevived;
    }

    public void InitializeRuntime(HealthController healthController, Image fillImage)
    {
        if (_healthController != null && isActiveAndEnabled) Unsubscribe();
        _healthController = healthController;
        _fillImage = fillImage;
        EnsureFillSprite();

        if (_shieldFillImage != null) Destroy(_shieldFillImage.gameObject);
        if (_oboroExecuteMarker != null) Destroy(_oboroExecuteMarker.gameObject);
        _shieldFillImage = null;
        _oboroExecuteMarker = null;
        _shieldSource = null;
        _shieldScanTimer = 0f;
        _oboroMarkerRatio = -1f;

        if (_healthController != null && isActiveAndEnabled)
        {
            Subscribe();
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
        }
    }

    private void Start()
    {
        EnsureFillSprite();
        if (_healthController != null)
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
    }

    private void LateUpdate()
    {
        FaceMainCamera();
        UpdateShieldGauge();
        UpdateOboroExecuteMarker();
    }

    private void EnsureFillSprite()
    {
        if (_fillImage != null && _fillImage.sprite == null) _fillImage.sprite = GetSharedFillSprite();
    }

    private static Sprite GetSharedFillSprite()
    {
        if (_sharedFillSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            _sharedFillSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        return _sharedFillSprite;
    }

    private void FaceMainCamera()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }
        transform.rotation = _mainCamera.transform.rotation;
    }

    private void UpdateShieldGauge()
    {
        if (_healthController == null || _fillImage == null) return;
        if (_shieldSource == null)
        {
            _shieldScanTimer -= Time.deltaTime;
            if (_shieldScanTimer > 0f) return;
            _shieldScanTimer = ShieldScanInterval;
            _shieldSource = _healthController.GetComponent<IndomitableRune>();
            if (_shieldSource == null) return;
        }

        float shield = _shieldSource.ShieldAmount;
        bool visible = shield > 0f && !_healthController.IsDead;
        if (!visible)
        {
            if (_shieldFillImage != null) _shieldFillImage.enabled = false;
            return;
        }

        if (_shieldFillImage == null) CreateShieldFillImage();
        if (_shieldFillImage == null) return;
        _shieldFillImage.enabled = true;
        float maxHealth = _healthController.MaxHealth;
        _shieldFillImage.fillAmount = maxHealth > 0f
            ? Mathf.Clamp01((_healthController.CurrentHealth + shield) / maxHealth) : 0f;
    }

    private void CreateShieldFillImage()
    {
        RectTransform fillRect = _fillImage.rectTransform;
        GameObject shieldObject = new GameObject("Shield Fill", typeof(RectTransform));
        RectTransform shieldRect = shieldObject.GetComponent<RectTransform>();
        shieldRect.SetParent(fillRect.parent, false);
        CopyRect(fillRect, shieldRect);
        shieldRect.SetSiblingIndex(fillRect.GetSiblingIndex());

        _shieldFillImage = shieldObject.AddComponent<Image>();
        _shieldFillImage.sprite = GetSharedFillSprite();
        _shieldFillImage.type = Image.Type.Filled;
        if (_fillImage.type == Image.Type.Filled)
        {
            _shieldFillImage.fillMethod = _fillImage.fillMethod;
            _shieldFillImage.fillOrigin = _fillImage.fillOrigin;
        }
        else
        {
            _shieldFillImage.fillMethod = Image.FillMethod.Horizontal;
            _shieldFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        _shieldFillImage.color = Color.white;
        _shieldFillImage.raycastTarget = false;
    }

    private void UpdateOboroExecuteMarker()
    {
        if (_healthController == null || _fillImage == null) return;
        bool shouldShow = OboroRController.TryGetExecuteThreshold(_healthController,
            out float thresholdRatio, out bool isInExecuteRange);
        if (!shouldShow)
        {
            if (_oboroExecuteMarker != null) _oboroExecuteMarker.enabled = false;
            return;
        }

        thresholdRatio = Mathf.Clamp01(thresholdRatio);
        if (_oboroExecuteMarker == null)
        {
            CreateExecuteMarker(thresholdRatio);
        }
        else if (!Mathf.Approximately(_oboroMarkerRatio, thresholdRatio))
        {
            PositionExecuteMarker(thresholdRatio);
        }
        if (_oboroExecuteMarker == null) return;

        _oboroExecuteMarker.enabled = !_healthController.IsDead;
        _oboroExecuteMarker.color = isInExecuteRange
            ? new Color(1f, 0.08f, 0.08f, 1f)
            : new Color(1f, 0.75f, 0.2f, 0.95f);
        _oboroExecuteMarker.rectTransform.SetAsLastSibling();
    }

    private void CreateExecuteMarker(float ratio)
    {
        GameObject markerObject = new GameObject("Oboro R Execute Threshold", typeof(RectTransform));
        markerObject.transform.SetParent(_fillImage.rectTransform.parent, false);
        _oboroExecuteMarker = markerObject.AddComponent<Image>();
        _oboroExecuteMarker.sprite = GetSharedFillSprite();
        _oboroExecuteMarker.raycastTarget = false;
        PositionExecuteMarker(ratio);
    }

    private void PositionExecuteMarker(float ratio)
    {
        if (_oboroExecuteMarker == null || _fillImage == null) return;
        _oboroMarkerRatio = ratio;
        RectTransform fill = _fillImage.rectTransform;
        RectTransform marker = _oboroExecuteMarker.rectTransform;

        if (Mathf.Abs(fill.anchorMax.x - fill.anchorMin.x) > 0.0001f)
        {
            float anchorX = Mathf.Lerp(fill.anchorMin.x, fill.anchorMax.x, ratio);
            marker.anchorMin = new Vector2(anchorX, fill.anchorMin.y);
            marker.anchorMax = new Vector2(anchorX, fill.anchorMax.y);
            marker.pivot = new Vector2(0.5f, fill.pivot.y);
            marker.anchoredPosition = Vector2.zero;
            marker.offsetMin = new Vector2(-2f, fill.offsetMin.y);
            marker.offsetMax = new Vector2(2f, fill.offsetMax.y);
        }
        else
        {
            marker.anchorMin = marker.anchorMax = fill.anchorMin;
            marker.pivot = new Vector2(0.5f, fill.pivot.y);
            float left = fill.anchoredPosition.x - fill.sizeDelta.x * fill.pivot.x;
            marker.anchoredPosition = new Vector2(left + fill.sizeDelta.x * ratio, fill.anchoredPosition.y);
            marker.sizeDelta = new Vector2(4f, Mathf.Max(1f, fill.sizeDelta.y));
        }
        marker.SetAsLastSibling();
    }

    private static void CopyRect(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.localScale = source.localScale;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    }

    private void HandleDied()
    {
        if (_canvas != null) _canvas.enabled = false;
    }

    private void HandleRevived()
    {
        if (_canvas != null) _canvas.enabled = true;
        if (_healthController != null)
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
    }
}
