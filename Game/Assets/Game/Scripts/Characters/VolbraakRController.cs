using UnityEngine;

/// <summary>
/// ヴォルブラークR(鎖)を管理する。
/// Rキーでマウスカーソル方向へ鎖を飛ばし、最初に当たった敵ヒーロー(Character/TrainingDummy分類)を鎖で繋ぐ(GAME_DESIGN 12章)。
/// - 鎖で繋がれた敵は、持続時間の間ヴォルブラークから一定距離以上離れられない
///   (境界を越えた分だけ毎フレーム内側へ引き戻される。ヴォルブラークが移動すると敵も引きずられる)。
/// - 鎖はミニオン・タワーには当たらず、すり抜けて敵ヒーローだけを判定する。
/// - 対象が共通Dの無効化ウィンドウ中の場合、鎖(拘束)は不発になる(クールダウンは消費)。
///   GAME_DESIGN 12章「Dで鎖を弾かれても反射は付与」のため、R反射(後続タスクで実装)は
///   共通Dに弾かれた場合にも付与する。本タスクでは拘束(鎖)のみを実装する。
/// - 移動を伴わないためスネア中も使用できる(スタン中・E突進中・死亡中などは行動ロックにより使用不可)。
/// - 自身が死亡した場合は鎖・拘束を即時終了する。デス時は残りクールダウンを60%短縮する(GAME_DESIGN 7章)。
/// - R反射タスク向けのpublic API: IsTetherActive / TetherTarget / TetherRemainingDuration。
/// NormalCast: Rキーを押している間は方向線(長さ=鎖の射程)のみを表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
/// </summary>
[DisallowMultipleComponent]
public sealed class VolbraakRController : MonoBehaviour
{
    [Header("Chain")]
    // 鎖の最大射程(Unity units)と先端が伸びる速度(units/sec)。
    [SerializeField, Min(0.1f)] private float _chainRange = 6f;
    [SerializeField, Min(0.1f)] private float _chainSpeed = 18f;
    // 鎖の先端の命中判定半径。
    [SerializeField, Min(0f)] private float _hitRadius = 0.6f;
    [SerializeField, Min(0f)] private float _minCastDistance = 0.1f;
    // 鎖の表示・命中判定の高さ(対象の足元からのオフセット)。
    [SerializeField, Min(0f)] private float _chainHeightOffset = 0.9f;

    [Header("Tether")]
    // 拘束中、敵がヴォルブラークから離れられる最大距離(Unity units)。
    [SerializeField, Min(0.1f)] private float _tetherMaxDistance = 4f;
    // 拘束の持続時間(秒)。R反射(後続タスク)も同じ時間持続する予定。
    [SerializeField, Min(0f)] private float _tetherDuration = 3f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 90f;
    // デス時に残りクールダウンを短縮する割合(0.6 = 60%短縮)。GAME_DESIGN.md 7章準拠。
    [SerializeField, Range(0f, 1f)] private float _deathCooldownReduction = 0.6f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;

    [Header("Layers")]
    // ZelfQControllerと同じレイヤーを設定する(Ground: マウス地点判定用 / Targetable: 命中判定用)。
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Visual")]
    [SerializeField] private Color _chainColor = new Color(0.95f, 0.3f, 0.2f, 0.95f);
    [SerializeField, Min(0.005f)] private float _chainWidth = 0.08f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isChainFlying;
    [SerializeField] private bool _isTetherActive;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private float _remainingTetherDuration;

    private CharacterController _characterController;
    private PlayerMouseFacing _mouseFacing;
    private HealthController _selfHealth;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;
    private LineRenderer _chainLine;
    private Material _chainMaterial;
    private Vector3 _chainDirection;
    private Vector3 _chainTipPosition;
    private float _chainTraveledDistance;
    private Targetable _tetherTarget;
    private float _tetherEndTime;
    // クールダウン終了時刻。長時間起動でもfloat精度が落ちないよう、Time.timeAsDouble基準のdoubleで管理する。
    private double _cooldownEndTime;

    /// <summary>拘束(鎖)が有効か。R反射タスクが反射ウィンドウの判定に使用する。</summary>
    public bool IsTetherActive => _isTetherActive;

    /// <summary>拘束中の対象。拘束していない場合はnull。</summary>
    public Targetable TetherTarget => _tetherTarget;

    /// <summary>拘束の残り時間(秒)。拘束していない場合は0。</summary>
    public float TetherRemainingDuration => _isTetherActive ? Mathf.Max(0f, _tetherEndTime - Time.time) : 0f;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _selfHealth = GetComponent<HealthController>();
        _mainCamera = Camera.main;
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Volbraak R Range Indicator");

        // 自身の死亡時に鎖を即時終了し、残りクールダウンを短縮する。
        if (_selfHealth != null) _selfHealth.Died += OnSelfDied;

        if (_groundLayer.value == 0 || _targetableLayer.value == 0)
        {
            Debug.LogWarning("ヴォルブラーク R: Ground Layer / Targetable LayerをInspectorで設定してください(ZelfQControllerと同じ設定)。", this);
        }

        CreateChainLine();
    }

    private void OnDestroy()
    {
        if (_selfHealth != null) _selfHealth.Died -= OnSelfDied;
        if (_chainLine != null) Destroy(_chainLine.gameObject);
        if (_chainMaterial != null) Destroy(_chainMaterial);
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
        _remainingTetherDuration = TetherRemainingDuration;

        if (_isChainFlying)
        {
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            UpdateChainFlight();
            return;
        }

        if (_isTetherActive)
        {
            UpdateTether();
        }

        // NormalCast: 押している間は鎖の射程と方向を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
        UpdateRangeIndicator();

        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.RPressedThisFrame, _inputHub.RReleasedThisFrame))
        {
            HandleRCast();
        }
    }

    // Rキーを押している間、本体→カーソル方向の直線(長さ=鎖の射程)のみを表示する(方向指定スキルの可視化)。
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.RPressed && !_isChainFlying
            && (_abilityLock == null || !_abilityLock.IsLocked)
            && (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }

        float yOffset = _characterController != null
            ? _characterController.center.y - _characterController.height * 0.5f + 0.05f
            : 0.05f;

        // カーソルの地面位置から本体→カーソルのXZ方向を求め、鎖の射程ぶんの方向線のみを表示する。
        if (TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Vector3 direction = groundPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Vector3 origin = transform.position + new Vector3(0f, yOffset, 0f);
                _rangeIndicator.ShowDirectionLine(origin, direction.normalized, _chainRange, new Color(_chainColor.r, _chainColor.g, _chainColor.b, 0.9f));
                return;
            }
        }
        _rangeIndicator.HideAll();
    }

    private void HandleRCast()
    {
        // 他の行動ロック中(スタン中・E突進中・死亡中など)は発動できない。
        // クールダウン判定より先に確認し、ロックが原因のときは必ずこのログを出す。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("ヴォルブラーク R: 他の行動中のため発動できません。", this);
            return;
        }
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("ヴォルブラーク R: クールダウン中です。", this);
            return;
        }
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            Debug.Log("ヴォルブラーク R: 死亡中のため発動できません。", this);
            return;
        }

        if (!TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Debug.Log("ヴォルブラーク R: マウスカーソルがGroundを指していないため発動しません。", this);
            return;
        }

        Vector3 direction = groundPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < _minCastDistance * _minCastDistance)
        {
            Debug.Log("ヴォルブラーク R: マウス地点が近すぎるため発動しません。", this);
            return;
        }

        StartChain(direction.normalized);
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_inputHub == null || _groundLayer.value == 0) return false;
        // Camera.mainは毎フレーム呼ぶと検索コストがかかるため、Awakeでキャッシュし、破棄時のみ再取得する。
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return false;
        }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer, QueryTriggerInteraction.Ignore)) return false;
        point = hit.point;
        return true;
    }

    private void StartChain(Vector3 direction)
    {
        // 既存の拘束が残っている場合は終了してから新しい鎖を飛ばす(通常はクールダウンにより発生しない)。
        if (_isTetherActive) EndTether("再発動のため");

        _chainDirection = direction;
        _chainTraveledDistance = 0f;
        _chainTipPosition = GetChainOrigin();
        _isChainFlying = true;
        _cooldownEndTime = Time.timeAsDouble + _cooldown;

        if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);
        if (_chainLine != null)
        {
            UpdateChainLine(_chainTipPosition);
            _chainLine.enabled = true;
        }
        Debug.Log("ヴォルブラーク R: 鎖を発射しました。", this);
    }

    private void UpdateChainFlight()
    {
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            CancelChainFlight("死亡により鎖を中断しました");
            return;
        }

        float step = Mathf.Min(_chainSpeed * Time.deltaTime, _chainRange - _chainTraveledDistance);
        Vector3 previousTip = _chainTipPosition;
        _chainTipPosition = previousTip + _chainDirection * step;
        _chainTraveledDistance += step;
        UpdateChainLine(_chainTipPosition);

        Targetable hitTarget = FindHeroTargetAlongSegment(previousTip, _chainTipPosition);
        if (hitTarget != null)
        {
            _isChainFlying = false;
            HandleChainHit(hitTarget);
            return;
        }

        if (_chainTraveledDistance >= _chainRange - 0.0001f)
        {
            _isChainFlying = false;
            if (_chainLine != null) _chainLine.enabled = false;
            Debug.Log("ヴォルブラーク R: 鎖が敵ヒーローに当たりませんでした。", this);
        }
    }

    // 鎖の先端が通過した区間で最初の敵ヒーロー(Character/TrainingDummy分類)を探す。
    // ミニオン・タワーには当たらず、すり抜ける(Rは敵ヒーローを繋ぐスキル)。
    private Targetable FindHeroTargetAlongSegment(Vector3 from, Vector3 to)
    {
        if (_targetableLayer.value == 0) return null;
        Targetable closest = null;
        float closestSqr = float.MaxValue;
        Collider[] overlaps = Physics.OverlapCapsule(from, to, _hitRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            Targetable target = overlap.GetComponentInParent<Targetable>();
            if (target == null) continue;
            if (target.transform == transform || target.transform.IsChildOf(transform)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;
            if (target.Classification != TargetClassification.Character &&
                target.Classification != TargetClassification.TrainingDummy) continue;
            float sqr = (target.transform.position - from).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = target;
            }
        }
        return closest;
    }

    private void HandleChainHit(Targetable target)
    {
        // 共通Dによる無効化: 鎖(拘束)は不発になる(クールダウンは消費)。
        // GAME_DESIGN 12章「Dで鎖を弾かれても反射は付与」のため、R反射(後続タスク)は
        // このブロックでも反射バフを付与する予定。本タスクでは拘束のみのため、ここで終了する。
        CommonDController targetCommonD = target.GetComponentInParent<CommonDController>();
        if (targetCommonD != null && targetCommonD.TryBlockHardCC(transform))
        {
            if (_chainLine != null) _chainLine.enabled = false;
            Debug.Log($"ヴォルブラーク R: {target.name} の共通Dに弾かれたため、拘束は不発になりました(反射は後続タスクで付与予定)。", this);
            return;
        }

        _tetherTarget = target;
        _tetherEndTime = Time.time + _tetherDuration;
        _isTetherActive = true;
        UpdateChainLine(GetTargetChainPoint(target));
        Debug.Log($"ヴォルブラーク R: {target.name} を鎖で繋ぎました({_tetherDuration:F1}秒)。", this);
    }

    private void UpdateTether()
    {
        if (_tetherTarget == null || !_tetherTarget.isActiveAndEnabled || _tetherTarget.IsDead)
        {
            EndTether("対象が無効になったため");
            return;
        }
        if (Time.time >= _tetherEndTime)
        {
            EndTether("持続時間が終了したため");
            return;
        }

        // 一定距離以上離れられない: 境界を越えた分だけ対象をヴォルブラーク方向へ引き戻す。
        // 相手のCharacterControllerが有効な場合はMove(壁との衝突を考慮)、それ以外はTransformを直接動かす。
        Vector3 diff = _tetherTarget.transform.position - transform.position;
        diff.y = 0f;
        float distance = diff.magnitude;
        if (distance > _tetherMaxDistance && distance > 0.0001f)
        {
            Vector3 pull = -diff / distance * (distance - _tetherMaxDistance);
            CharacterController targetController = _tetherTarget.GetComponentInParent<CharacterController>();
            if (targetController != null && targetController.enabled)
            {
                targetController.Move(pull);
            }
            else
            {
                _tetherTarget.transform.position += pull;
            }
        }

        UpdateChainLine(GetTargetChainPoint(_tetherTarget));
    }

    private void EndTether(string reason)
    {
        _isTetherActive = false;
        _tetherTarget = null;
        if (_chainLine != null) _chainLine.enabled = false;
        Debug.Log($"ヴォルブラーク R: {reason}、鎖を解除しました。", this);
    }

    private void CancelChainFlight(string reason)
    {
        _isChainFlying = false;
        if (_chainLine != null) _chainLine.enabled = false;
        Debug.Log($"ヴォルブラーク R: {reason}。", this);
    }

    // 自身の死亡時: 鎖・拘束を即時終了し、残りクールダウンを60%短縮する(GAME_DESIGN.md 7章)。
    private void OnSelfDied()
    {
        if (_isChainFlying) CancelChainFlight("死亡により鎖を中断しました");
        if (_isTetherActive) EndTether("ヴォルブラークの死亡により");

        double remaining = _cooldownEndTime - Time.timeAsDouble;
        if (remaining > 0.0)
        {
            _cooldownEndTime = Time.timeAsDouble + remaining * (1f - _deathCooldownReduction);
            Debug.Log($"ヴォルブラーク R: デスにより残りクールダウンを{_deathCooldownReduction * 100f:F0}%短縮しました(残り{System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble):F1}秒)。", this);
        }
    }

    // 鎖の始点(自身の胸元)。CharacterControllerがあれば中心高さ、なければ足元+オフセット。
    private Vector3 GetChainOrigin()
    {
        if (_characterController != null) return transform.position + _characterController.center;
        return transform.position + Vector3.up * _chainHeightOffset;
    }

    // 拘束中の鎖の終点(対象の胸元)。
    private Vector3 GetTargetChainPoint(Targetable target)
    {
        return target.transform.position + Vector3.up * _chainHeightOffset;
    }

    private void UpdateChainLine(Vector3 endPoint)
    {
        if (_chainLine == null) return;
        _chainLine.SetPosition(0, GetChainOrigin());
        _chainLine.SetPosition(1, endPoint);
    }

    private void CreateChainLine()
    {
        GameObject chainObject = new GameObject("Volbraak R Chain");
        chainObject.transform.SetParent(transform, false);
        _chainLine = chainObject.AddComponent<LineRenderer>();
        _chainLine.useWorldSpace = true;
        _chainLine.positionCount = 2;
        _chainLine.startWidth = _chainWidth;
        _chainLine.endWidth = _chainWidth;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _chainMaterial = new Material(shader);
        _chainMaterial.color = _chainColor;
        _chainLine.material = _chainMaterial;
        _chainLine.startColor = _chainColor;
        _chainLine.endColor = _chainColor;
        _chainLine.numCapVertices = 4;
        _chainLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _chainLine.receiveShadows = false;
        _chainLine.enabled = false;
    }
}
