using System.Collections.Generic;
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
    private CrowdControlController _crowdControl;
    private Image _stunBackdrop;
    private Image _snareBackdrop;
    private Text _stunLabel;
    private Text _snareLabel;
    private Image _stunIcon;
    private Image _snareIcon;
    private readonly HashSet<TowerController> _targetingTowers = new HashSet<TowerController>();
    private GameObject _towerWarning;
    private bool _started;
    private static Sprite _sharedFillSprite;

    public bool IsStunLabelVisible => isActiveAndEnabled && _stunLabel != null && _stunLabel.enabled &&
        _canvas != null && _canvas.enabled;
    public bool IsSnareLabelVisible => isActiveAndEnabled && _snareLabel != null && _snareLabel.enabled &&
        _canvas != null && _canvas.enabled;
    public bool IsTowerWarningVisible => _towerWarning != null && _towerWarning.activeInHierarchy &&
        _canvas != null && _canvas.enabled;

    private void Awake()
    {
        if (_healthController == null) _healthController = GetComponentInParent<HealthController>();
        _mainCamera = Camera.main;
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        CrowdControlController.Created -= HandleCrowdControlEvent;
        CrowdControlController.Created += HandleCrowdControlEvent;
        CrowdControlController.StatusApplied -= HandleCrowdControlEvent;
        CrowdControlController.StatusApplied += HandleCrowdControlEvent;
        Subscribe();
        if (_started) InitializeCrowdControlLabels();
        UpdateCrowdControlLabels();
        UpdateTowerWarning();
    }

    private void OnDisable()
    {
        Unsubscribe();
        CrowdControlController.Created -= HandleCrowdControlEvent;
        CrowdControlController.StatusApplied -= HandleCrowdControlEvent;
        HideCrowdControlLabels();
        if (_towerWarning != null) _towerWarning.SetActive(false);
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
        _crowdControl = null;
        if (_stunBackdrop != null) Destroy(_stunBackdrop.gameObject);
        if (_snareBackdrop != null) Destroy(_snareBackdrop.gameObject);
        if (_stunLabel != null) Destroy(_stunLabel.gameObject);
        if (_snareLabel != null) Destroy(_snareLabel.gameObject);
        if (_stunIcon != null) Destroy(_stunIcon.gameObject);
        if (_snareIcon != null) Destroy(_snareIcon.gameObject);
        _stunBackdrop = null;
        _snareBackdrop = null;
        _stunLabel = null;
        _snareLabel = null;
        _stunIcon = null;
        _snareIcon = null;
        _targetingTowers.Clear();
        if (_towerWarning != null) Destroy(_towerWarning);
        _towerWarning = null;

        if (_healthController != null && isActiveAndEnabled)
        {
            Subscribe();
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
        }
        if (_started) InitializeCrowdControlLabels();
    }

    private void Start()
    {
        _started = true;
        EnsureFillSprite();
        if (_healthController != null)
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
        InitializeCrowdControlLabels();
    }

    private void LateUpdate()
    {
        FaceMainCamera();
        UpdateShieldGauge();
        UpdateOboroExecuteMarker();
        UpdateCrowdControlLabels();
    }

    public static bool IsEnemyPlayer(TeamMember localTeam, HealthController targetHealth)
    {
        if (targetHealth == null) return false;
        Targetable target = targetHealth.GetComponent<Targetable>();
        if (target == null || target.Classification != TargetClassification.Character) return false;
        if (target.IsTrainingDummyPlayerProxy) return true;
        if (localTeam == null || !targetHealth.CompareTag("Player")) return false;
        TeamMember targetTeam = targetHealth.GetComponent<TeamMember>();
        return targetTeam != null && localTeam.Team != targetTeam.Team;
    }

    private void InitializeCrowdControlLabels()
    {
        if (_healthController == null || _canvas == null || _stunLabel != null) return;
        PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
        GameObject localPlayer = spawner != null ? spawner.SpawnedPlayer : null;
        if (localPlayer == null)
        {
            PlayerClickMovement movement = FindFirstObjectByType<PlayerClickMovement>();
            if (movement != null) localPlayer = movement.gameObject;
        }
        TeamMember localTeam = localPlayer != null ? localPlayer.GetComponent<TeamMember>() : null;
        BindCrowdControlLabels(localTeam);
    }

    private void BindCrowdControlLabels(TeamMember localTeam)
    {
        if (!IsEnemyPlayer(localTeam, _healthController)) return;
        _crowdControl = _healthController.GetComponent<CrowdControlController>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _stunLabel = CreateStatusLabel("Stun Status", "スタン", new Color(1f, 0.65f, 0.2f),
            font, Resources.Load<Sprite>("UI/StatusIcons/Stun"), out _stunBackdrop, out _stunIcon);
        _snareLabel = CreateStatusLabel("Snare Status", "スネア", new Color(0.45f, 0.9f, 1f),
            font, Resources.Load<Sprite>("UI/StatusIcons/Snare"), out _snareBackdrop, out _snareIcon);
        UpdateCrowdControlLabels();
    }

    private void HandleCrowdControlEvent(CrowdControlController controller)
    {
        if (_healthController == null || controller.gameObject != _healthController.gameObject) return;
        if (_stunLabel == null) InitializeCrowdControlLabels();
        if (_stunLabel == null) return;
        _crowdControl = controller;
        UpdateCrowdControlLabels();
    }

    private Text CreateStatusLabel(string objectName, string text, Color color, Font font,
        Sprite iconSprite, out Image backdrop, out Image icon)
    {
        GameObject backdropObject = new GameObject(objectName + " Backdrop", typeof(RectTransform));
        backdropObject.transform.SetParent(transform, false);
        RectTransform backdropRect = (RectTransform)backdropObject.transform;
        backdropRect.anchorMin = backdropRect.anchorMax = new Vector2(0.5f, 1f);
        backdropRect.pivot = new Vector2(0.5f, 0f);
        backdropRect.sizeDelta = new Vector2(290f, 68f);
        backdrop = backdropObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);
        backdrop.raycastTarget = false;
        backdrop.enabled = false;

        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)labelObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(200f, 68f);
        Text label = labelObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 56;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = color;
        label.text = text;
        label.raycastTarget = false;
        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        label.enabled = false;

        GameObject iconObject = new GameObject(objectName + " Icon", typeof(RectTransform));
        iconObject.transform.SetParent(transform, false);
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0f);
        iconRect.sizeDelta = new Vector2(56f, 56f);
        icon = iconObject.AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.raycastTarget = false;
        icon.enabled = false;
        return label;
    }

    private void UpdateCrowdControlLabels()
    {
        if (_stunLabel == null || _snareLabel == null) return;
        bool visible = isActiveAndEnabled && _canvas != null && _canvas.enabled &&
                       _healthController != null && !_healthController.IsDead &&
                       _crowdControl != null && _crowdControl.isActiveAndEnabled;
        bool stunned = visible && _crowdControl.IsStunned;
        bool snared = visible && _crowdControl.IsSnared;
        _stunLabel.enabled = stunned;
        _snareLabel.enabled = snared;
        _stunIcon.enabled = stunned;
        _snareIcon.enabled = snared;
        _stunBackdrop.enabled = stunned;
        _snareBackdrop.enabled = snared;
        if (stunned)
        {
            Vector2 position = new Vector2(0f, snared ? 104f : 34f);
            _stunLabel.rectTransform.anchoredPosition = position + new Vector2(28f, 0f);
            _stunBackdrop.rectTransform.anchoredPosition = position;
            _stunIcon.rectTransform.anchoredPosition = position + new Vector2(-105f, 6f);
        }
        if (snared)
        {
            Vector2 position = new Vector2(0f, 34f);
            _snareLabel.rectTransform.anchoredPosition = position + new Vector2(28f, 0f);
            _snareBackdrop.rectTransform.anchoredPosition = position;
            _snareIcon.rectTransform.anchoredPosition = position + new Vector2(-105f, 6f);
        }
    }

    private void HideCrowdControlLabels()
    {
        if (_stunLabel != null) _stunLabel.enabled = false;
        if (_snareLabel != null) _snareLabel.enabled = false;
        if (_stunIcon != null) _stunIcon.enabled = false;
        if (_snareIcon != null) _snareIcon.enabled = false;
        if (_stunBackdrop != null) _stunBackdrop.enabled = false;
        if (_snareBackdrop != null) _snareBackdrop.enabled = false;
    }

    /// <summary>タワーの現在の標的だけに警告を表示する。複数タワーから同時に狙われても維持する。</summary>
    public void SetTowerTargeted(TowerController tower, bool targeted)
    {
        if (tower == null) return;
        if (targeted) _targetingTowers.Add(tower);
        else _targetingTowers.Remove(tower);
        UpdateTowerWarning();
    }

    private void UpdateTowerWarning()
    {
        bool visible = _targetingTowers.Count > 0 && isActiveAndEnabled &&
            _canvas != null && _canvas.enabled && _healthController != null &&
            !_healthController.IsDead;
        if (visible)
        {
            Targetable target = _healthController.GetComponent<Targetable>();
            visible = target != null && target.Classification == TargetClassification.Character;
        }
        if (visible && _towerWarning == null) CreateTowerWarning();
        if (_towerWarning != null && _towerWarning.activeSelf != visible)
            _towerWarning.SetActive(visible);
    }

    private void CreateTowerWarning()
    {
        _towerWarning = new GameObject("Tower Target Warning", typeof(RectTransform));
        _towerWarning.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)_towerWarning.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, 210f);
        root.sizeDelta = new Vector2(68f, 68f);

        Image icon = _towerWarning.AddComponent<Image>();
        icon.sprite = Resources.Load<Sprite>("UI/StatusIcons/TowerTarget");
        icon.raycastTarget = false;
    }

    private void EnsureFillSprite()
    {
        // The built-in rounded UI sprite has transparent horizontal padding. A filled
        // image clips that padding too, making a 10% execute marker look smaller.
        if (_fillImage != null && _fillImage.type == Image.Type.Filled)
            _fillImage.sprite = GetSharedFillSprite();
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
        markerObject.transform.SetParent(_fillImage.rectTransform, false);
        _oboroExecuteMarker = markerObject.AddComponent<Image>();
        _oboroExecuteMarker.sprite = GetSharedFillSprite();
        _oboroExecuteMarker.raycastTarget = false;
        PositionExecuteMarker(ratio);
    }

    private void PositionExecuteMarker(float ratio)
    {
        if (_oboroExecuteMarker == null || _fillImage == null) return;
        _oboroMarkerRatio = ratio;
        RectTransform marker = _oboroExecuteMarker.rectTransform;
        marker.anchorMin = new Vector2(ratio, 0f);
        marker.anchorMax = new Vector2(ratio, 1f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(2f, 0f);
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
        HideCrowdControlLabels();
        _targetingTowers.Clear();
        UpdateTowerWarning();
    }

    private void HandleRevived()
    {
        if (_canvas != null) _canvas.enabled = true;
        if (_healthController != null)
            HandleHealthChanged(_healthController.CurrentHealth, _healthController.MaxHealth);
        UpdateCrowdControlLabels();
        UpdateTowerWarning();
    }
}
