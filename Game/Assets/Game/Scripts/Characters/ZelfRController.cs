using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゼルフR(決闘エリア)を管理する。
/// Rキー長押しで射程円を表示し、キーを離すとマウス下のCharacter敵中心に決闘エリアを展開する。
/// 射程外の対象には射程内まで自動接近してから発動する(右クリックで接近中止)。
///
/// タスク1: 決闘エリアを実装する
///   発動時、エリア中心(対象位置)に固定された円形エリアをLineRendererで可視化する。
///   持続時間はInspector設定。クールダウンはInspector設定。
///
/// タスク2: ミニオン押し出しを実装する
///   発動時にエリア内のミニオン等をエリア外縁の少し外へ瞬時に押し出す。
///   Character・TrainingDummy・Tower分類は押し出さない(エリア内に留まる)。
///
/// タスク3: エリア内外スロウを実装する
///   エリア内のCharacter/TrainingDummy分類の敵: Inner Slow Percent分のスロウを継続付与。
///   エリア外へ退出した同分類の敵: Outer Slow Percent分のスロウをOuter Slow Duration秒間付与。
///   ゼルフ自身: Self MS Boost Percent分のMS上昇をエリア持続中付与する。
///
/// 共通Dで完全不発(共通D未実装のため今回はD判定なし。将来追加予定)。
/// ZelfEのダッシュ中・ZelfW発動中・死亡中はAbilityLockControllerのロックにより入力を受け付けない。
/// コンポーネント自体は無効化されないため、発動済みの決闘エリアはW/E中も正しく進行・終了する。
/// ゼルフ自身が死亡した場合は決闘エリアを即時終了する。
/// </summary>
public sealed class ZelfRController : MonoBehaviour
{
    // Cast Rangeの既定値。旧バージョンのシーンで0のまま保存されていてもこの値へ自動補正する。
    private const float DefaultCastRange = 7f;

    [Header("References")]
    [SerializeField] private CharacterStats _selfStats;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Arena")]
    [SerializeField, Min(0f)] private float _arenaRadius = 5f;
    [SerializeField, Min(0f)] private float _duration = 5f;
    [SerializeField, Min(0f)] private float _cooldown = 120f;
    // R射程(Unity units)。Inspectorで未設定(0以下)の場合はDefaultCastRangeへ自動補正される。
    [SerializeField, Min(0f)] private float _castRange = DefaultCastRange;

    [Header("Slow & Boost")]
    // エリア内スロウ: BaseMoveSpeedの何%を減速するか
    [SerializeField, Range(0f, 1f)] private float _innerSlowPercent = 0.30f;
    // エリア外退出後スロウ: BaseMoveSpeedの何%を減速するか
    [SerializeField, Range(0f, 1f)] private float _outerSlowPercent = 0.60f;
    // エリア外スロウ持続時間(秒)
    [SerializeField, Min(0f)] private float _outerSlowDuration = 2.5f;
    // ゼルフ自身のMS上昇率(%)
    [SerializeField, Range(0f, 100f)] private float _selfMSBoostPercent = 20f;

    [Header("Visual")]
    [SerializeField] private Color _arenaColor = new Color(0.75f, 0.25f, 1f, 0.9f);
    [SerializeField, Min(4)] private int _arenaSegments = 64;
    [SerializeField, Min(0.005f)] private float _arenaLineWidth = 0.08f;
    [SerializeField] private Color _rangeCircleColor = new Color(0.75f, 0.25f, 1f, 0.6f);
    [SerializeField, Min(0.005f)] private float _rangeCircleWidth = 0.035f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isRActive;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private float _remainingDuration;
    [SerializeField] private bool _isApproachingRTarget;

    private float _cooldownEndTime;
    private float _activeEndTime;
    private Vector3 _arenaCenter;
    private LineRenderer _arenaCircle;
    private Material _arenaMaterial;
    private Camera _mainCamera;
    private HealthController _selfHealth;
    private AbilityLockController _abilityLock;
    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;

    private PlayerInputHub _inputHub;
    private ZelfQController _qController;
    private CharacterController _characterController;
    private PlayerClickMovement _clickMovement;
    private PlayerMouseFacing _mouseFacing;
    private LineRenderer _rangeCircle;
    private Material _rangeMaterial;
    // 射程外発動の自動接近対象。nullのときは接近中ではない。
    private Targetable _pendingTarget;

    // エリア内スロウ適用中の対象と適用量
    private readonly Dictionary<CharacterStats, float> _innerSlowAmounts = new Dictionary<CharacterStats, float>();
    // エリア外スロウが既に適用中かどうか(重複防止)
    private readonly HashSet<CharacterStats> _outerSlowActive = new HashSet<CharacterStats>();
    // 自身のMSブースト適用済みか
    private bool _selfBoostApplied;
    private float _selfBoostAmount;

    /// <summary>R射程外の自動接近中かどうか。通常攻撃の自動接近との競合防止に使用する。</summary>
    public bool IsApproachingRTarget => _pendingTarget != null;

    // エディタでシーン読み込み・値変更時に呼ばれる。
    // 旧バージョンのスクリプトでアタッチ済みのコンポーネントはCast Range=0が
    // シーンに保存されているため、手動設定不要で既定値へ自動補正する。
    private void OnValidate()
    {
        if (_castRange <= 0f) _castRange = DefaultCastRange;
    }

    private void Awake()
    {
        // 実行時も同様に自動補正する(ビルドではOnValidateが呼ばれないため)。
        if (_castRange <= 0f) _castRange = DefaultCastRange;

        _selfStats = _selfStats != null ? _selfStats : GetComponent<CharacterStats>();
        _selfHealth = GetComponent<HealthController>();
        _characterController = GetComponent<CharacterController>();
        _clickMovement = GetComponent<PlayerClickMovement>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _mainCamera = Camera.main;
        _qController = GetComponent<ZelfQController>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();

        // ゼルフ自身の死亡時に決闘エリアを即時終了する。
        if (_selfHealth != null) _selfHealth.Died += OnSelfDied;

        // LayerMaskをZelfQControllerと共有する
        if (_targetableLayer.value == 0 && _qController != null)
        {
            _targetableLayer = _qController.TargetableLayerMask;
        }

        CreateArenaCircle();
        CreateRangeCircle();

        // 診断用: このログがPlay開始時に出ない場合、コンポーネントがPlayerに付いていない。
        Debug.Log($"Zelf R: 初期化しました。CastRange={_castRange}, TargetableLayer={_targetableLayer.value}, SelfStats={(_selfStats != null ? "OK" : "未設定")}", this);
    }

    // W/Eはロック方式(AbilityLockController)に変更されたため、
    // 通常プレイでこのコンポーネントが無効化されることはない。
    private void OnDisable()
    {
        CancelPendingApproach();
        if (_rangeCircle != null) _rangeCircle.enabled = false;
    }

    private void OnDestroy()
    {
        if (_selfHealth != null) _selfHealth.Died -= OnSelfDied;
        CleanUpAllEffects();
        if (_arenaCircle != null) Destroy(_arenaCircle.gameObject);
        if (_arenaMaterial != null) Destroy(_arenaMaterial);
        if (_rangeCircle != null) Destroy(_rangeCircle.gameObject);
        if (_rangeMaterial != null) Destroy(_rangeMaterial);
    }

    // ゼルフ自身の死亡時: 自動接近を中止し、展開中の決闘エリアを即時終了する。
    private void OnSelfDied()
    {
        CancelPendingApproach();
        if (_isRActive)
        {
            EndArena();
            Debug.Log("Zelf R: ゼルフの死亡により決闘エリアを終了しました。", this);
        }
    }

    private void Update()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);

        if (_isRActive)
        {
            _remainingDuration = Mathf.Max(0f, _activeEndTime - Time.time);
            if (Time.time >= _activeEndTime)
            {
                EndArena();
                return;
            }
            UpdateInnerSlowAndOuterTransition();
        }

        // 行動ロック中(W発動中・Eダッシュ中・死亡中など)は入力を受け付けず、自動接近も中止する。
        // 発動済みエリアの進行(持続時間・スロウ更新)は上のブロックでロック中も継続する。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            CancelPendingApproach();
            if (_rangeCircle != null) _rangeCircle.enabled = false;
            // 診断用: ロック中のRリリースは理由をログに出す。
            if (_inputHub != null && _inputHub.RReleasedThisFrame)
            {
                Debug.Log("Zelf R: 他の行動中のため発動できません。", this);
            }
            return;
        }

        // Rキーを押している間は射程円を表示する。
        UpdateRangeCircle();

        // NormalCast: Rキーを離した瞬間に発動 / QuickCast: 押した瞬間に発動。
        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.RPressedThisFrame, _inputHub.RReleasedThisFrame))
        {
            HandleRReleased();
        }

        UpdatePendingApproach();
    }

    private void HandleRReleased()
    {
        CancelPendingApproach();
        if (_selfHealth != null && _selfHealth.IsDead) return;
        if (_isRActive)
        {
            Debug.Log("Zelf R: 発動中です。", this);
            return;
        }
        if (Time.time < _cooldownEndTime)
        {
            Debug.Log("Zelf R: クールダウン中です。", this);
            return;
        }

        if (!TryGetCharacterTargetUnderMouse(out Targetable target))
        {
            Debug.Log("Zelf R: マウスをCharacter分類の有効な敵に合わせてRを離してください。", this);
            return;
        }

        // Qの自動接近と同時進行しないよう中止する(移動の二重制御を防ぐ)。
        if (_qController != null) _qController.CancelPendingApproach();

        // 射程内なら即発動、射程外なら射程内まで自動接近してから発動する。
        if (IsInCastRange(target))
        {
            ActivateArena(target.transform.position);
            return;
        }

        _pendingTarget = target;
        _isApproachingRTarget = true;
        if (_clickMovement != null) _clickMovement.StopMovement();
        Debug.Log("Zelf R: 射程外のため自動接近を開始します。", this);
    }

    /// <summary>R射程外の自動接近中であれば中止する。自動接近中でなければ何もしない。</summary>
    public void CancelPendingApproach()
    {
        _pendingTarget = null;
        _isApproachingRTarget = false;
    }

    private bool IsInCastRange(Targetable target)
    {
        Vector3 diff = target.GetClosestPoint(transform.position) - transform.position;
        diff.y = 0f;
        return diff.sqrMagnitude <= _castRange * _castRange;
    }

    // 射程外発動時の自動接近。ZelfQControllerのUpdatePendingCastと同じ方式。
    // 右クリック・自身死亡・対象消失で中止し、射程内に入ったら発動する。
    private void UpdatePendingApproach()
    {
        if (_pendingTarget == null) return;
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            CancelPendingApproach();
            return;
        }
        if (_inputHub != null && _inputHub.RightClickPressedThisFrame)
        {
            CancelPendingApproach();
            Debug.Log("Zelf R: 右クリック入力により自動接近を中止しました。", this);
            return;
        }
        if (_isRActive || Time.time < _cooldownEndTime)
        {
            CancelPendingApproach();
            return;
        }
        if (!_pendingTarget.isActiveAndEnabled || _pendingTarget.IsDead)
        {
            CancelPendingApproach();
            Debug.Log("Zelf R: 対象が無効になったため自動接近を中止しました。", this);
            return;
        }
        if (!IsInCastRange(_pendingTarget))
        {
            Vector3 direction = _pendingTarget.GetClosestPoint(transform.position) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                if (_characterController != null && _characterController.enabled && _selfStats != null)
                {
                    _characterController.Move(direction.normalized * _selfStats.CurrentMoveSpeed * Time.deltaTime);
                }
                if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);
            }
            return;
        }

        Targetable target = _pendingTarget;
        CancelPendingApproach();
        ActivateArena(target.transform.position);
    }

    private bool TryGetCharacterTargetUnderMouse(out Targetable target)
    {
        target = null;
        if (_inputHub == null || _targetableLayer.value == 0) return false;
        if (_mainCamera == null) { _mainCamera = Camera.main; if (_mainCamera == null) return false; }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _targetableLayer, QueryTriggerInteraction.Ignore)) return false;
        Targetable t = hit.collider.GetComponentInParent<Targetable>();
        if (t == null || t.IsDead || !t.isActiveAndEnabled) return false;
        if (t.Classification != TargetClassification.Character &&
            t.Classification != TargetClassification.TrainingDummy) return false;
        target = t;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // タスク1: 決闘エリア展開
    // ─────────────────────────────────────────────────────────────────────
    private void ActivateArena(Vector3 center)
    {
        _arenaCenter = center;
        _isRActive = true;
        _activeEndTime = Time.time + _duration;
        _cooldownEndTime = Time.time + _cooldown;
        _innerSlowAmounts.Clear();
        _outerSlowActive.Clear();
        _selfBoostApplied = false;
        _selfBoostAmount = 0f;

        DrawArenaCircle(_arenaCenter);
        _arenaCircle.enabled = true;

        // タスク2: 発動時にエリア内の全Targetableを押し出す
        PushOutAllTargetablesInArena();

        // タスク3: ゼルフ自身のMS上昇を適用
        ApplySelfMSBoost();

        Debug.Log("Zelf R: 決闘エリアを展開しました。", this);
    }

    // ─────────────────────────────────────────────────────────────────────
    // タスク2: ミニオン押し出し
    // ─────────────────────────────────────────────────────────────────────
    private void PushOutAllTargetablesInArena()
    {
        if (_targetableLayer.value == 0) return;
        Collider[] cols = Physics.OverlapSphere(_arenaCenter, _arenaRadius,
            _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider col in cols)
        {
            Targetable target = col.GetComponentInParent<Targetable>();
            if (target == null || IsOwnGameObject(target)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;

            // Character・TrainingDummy・Tower分類は押し出さない(エリア内に留まる)。
            if (target.Classification == TargetClassification.Character ||
                target.Classification == TargetClassification.TrainingDummy ||
                target.Classification == TargetClassification.Tower) continue;

            PushTargetOutOfArena(target);
        }
    }

    private bool IsOwnGameObject(Targetable target)
    {
        return target.transform == transform || target.transform.IsChildOf(transform);
    }

    private void PushTargetOutOfArena(Targetable target)
    {
        Vector3 dir = target.transform.position - _arenaCenter;
        dir.y = 0f;
        // エリア中心と完全に重なった場合はゼルフの前方へ押し出す
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir.Normalize();

        // エリア外縁の0.6m外へ押し出す
        Vector3 dest = _arenaCenter + dir * (_arenaRadius + 0.6f);
        dest.y = target.transform.position.y;

        // CharacterControllerがある場合は無効化して位置を直接移動
        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            target.transform.position = dest;
            cc.enabled = was;
        }
        else
        {
            target.transform.position = dest;
        }

        Debug.Log($"Zelf R: {target.name} をエリア外へ押し出しました。", this);
    }

    // ─────────────────────────────────────────────────────────────────────
    // タスク3: エリア内外スロウ
    // ─────────────────────────────────────────────────────────────────────

    // 毎フレーム呼ばれる: エリア内Character敵スロウの付与/解除と退出スロウの適用
    private void UpdateInnerSlowAndOuterTransition()
    {
        if (_targetableLayer.value == 0) return;

        // 検索半径を少し広めにして退出直後の対象も捉える
        float detectRadius = _arenaRadius + 1f;
        Collider[] cols = Physics.OverlapSphere(_arenaCenter, detectRadius,
            _targetableLayer, QueryTriggerInteraction.Ignore);

        HashSet<CharacterStats> detectedInsideSet = new HashSet<CharacterStats>();

        foreach (Collider col in cols)
        {
            Targetable target = col.GetComponentInParent<Targetable>();
            if (target == null || IsOwnGameObject(target)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;
            if (target.Classification != TargetClassification.Character &&
                target.Classification != TargetClassification.TrainingDummy) continue;

            CharacterStats stats = target.GetComponent<CharacterStats>();
            if (stats == null) continue;

            Vector3 horiz = target.transform.position - _arenaCenter;
            horiz.y = 0f;
            bool isInside = horiz.magnitude <= _arenaRadius;

            if (isInside)
            {
                detectedInsideSet.Add(stats);
                if (!_innerSlowAmounts.ContainsKey(stats))
                {
                    // エリア内スロウを新規付与
                    float slowAmt = stats.BaseMoveSpeed * _innerSlowPercent;
                    stats.AddMoveSpeedBonus(-slowAmt);
                    _innerSlowAmounts[stats] = slowAmt;
                    Debug.Log($"Zelf R: {target.name} にエリア内スロウを付与しました。", this);
                }
            }
        }

        // エリア内スロウ中だが検出範囲に入っていない or エリア外にいる対象を処理
        List<CharacterStats> toRemove = null;
        foreach (KeyValuePair<CharacterStats, float> kv in _innerSlowAmounts)
        {
            CharacterStats stats = kv.Key;
            if (!detectedInsideSet.Contains(stats))
            {
                // スロウ解除
                if (stats != null) stats.RemoveMoveSpeedBonus(kv.Value);

                // 退出スロウを付与(対象が生存中のみ)
                Targetable t = stats != null ? stats.GetComponent<Targetable>() : null;
                if (t != null && !t.IsDead && stats != null)
                {
                    StartCoroutine(ApplyOuterSlowCoroutine(stats, t));
                    Debug.Log($"Zelf R: {stats.name} がエリアを退出しました。大きなスロウを付与しました。", this);
                }

                if (toRemove == null) toRemove = new List<CharacterStats>();
                toRemove.Add(stats);
            }
        }
        if (toRemove != null) foreach (CharacterStats s in toRemove) _innerSlowAmounts.Remove(s);

        // 死亡した対象のスロウを解除
        RemoveDeadTargetSlows();
    }

    private IEnumerator ApplyOuterSlowCoroutine(CharacterStats stats, Targetable target)
    {
        if (stats == null) yield break;
        // 既にエリア外スロウが適用中なら重複付与しない
        if (_outerSlowActive.Contains(stats)) yield break;

        _outerSlowActive.Add(stats);
        float slowAmt = stats.BaseMoveSpeed * _outerSlowPercent;
        stats.AddMoveSpeedBonus(-slowAmt);

        float elapsed = 0f;
        while (elapsed < _outerSlowDuration)
        {
            // 対象が死亡・無効化された場合は即座に終了
            if (stats == null || (target != null && target.IsDead)) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (stats != null) stats.RemoveMoveSpeedBonus(slowAmt);
        _outerSlowActive.Remove(stats);
    }

    private void RemoveDeadTargetSlows()
    {
        List<CharacterStats> toRemove = null;
        foreach (KeyValuePair<CharacterStats, float> kv in _innerSlowAmounts)
        {
            bool shouldRemove = kv.Key == null;
            if (!shouldRemove)
            {
                Targetable t = kv.Key.GetComponent<Targetable>();
                shouldRemove = (t != null && t.IsDead);
            }
            if (shouldRemove)
            {
                if (kv.Key != null) kv.Key.RemoveMoveSpeedBonus(kv.Value);
                if (toRemove == null) toRemove = new List<CharacterStats>();
                toRemove.Add(kv.Key);
            }
        }
        if (toRemove != null) foreach (CharacterStats s in toRemove) _innerSlowAmounts.Remove(s);
    }

    private void ApplySelfMSBoost()
    {
        if (_selfStats == null || _selfBoostApplied) return;
        _selfBoostAmount = _selfStats.BaseMoveSpeed * (_selfMSBoostPercent / 100f);
        _selfStats.AddMoveSpeedBonus(_selfBoostAmount);
        _selfBoostApplied = true;
        Debug.Log($"Zelf R: ゼルフのMS上昇を適用しました (+{_selfBoostAmount:F1} units/sec)。", this);
    }

    private void RemoveSelfMSBoost()
    {
        if (_selfStats == null || !_selfBoostApplied) return;
        _selfStats.RemoveMoveSpeedBonus(_selfBoostAmount);
        _selfBoostApplied = false;
        _selfBoostAmount = 0f;
    }

    private void EndArena()
    {
        _isRActive = false;
        _remainingDuration = 0f;
        _arenaCircle.enabled = false;
        CleanUpAllEffects();
        Debug.Log("Zelf R: 決闘エリアが終了しました。", this);
    }

    private void CleanUpAllEffects()
    {
        foreach (KeyValuePair<CharacterStats, float> kv in _innerSlowAmounts)
        {
            if (kv.Key != null) kv.Key.RemoveMoveSpeedBonus(kv.Value);
        }
        _innerSlowAmounts.Clear();
        _outerSlowActive.Clear();
        RemoveSelfMSBoost();
    }

    // ─────────────────────────────────────────────────────────────────────
    // ビジュアル
    // ─────────────────────────────────────────────────────────────────────
    // Rキーを押している間、自分中心にR射程円を表示する(ZelfQの射程円と同方式)。
    private void CreateRangeCircle()
    {
        GameObject obj = new GameObject("Zelf R Range Circle");
        obj.transform.SetParent(transform, false);
        _rangeCircle = obj.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _rangeMaterial = new Material(shader);
        _rangeMaterial.color = _rangeCircleColor;

        _rangeCircle.useWorldSpace = false;
        _rangeCircle.material = _rangeMaterial;
        _rangeCircle.startColor = _rangeCircleColor;
        _rangeCircle.endColor = _rangeCircleColor;
        _rangeCircle.startWidth = _rangeCircleWidth;
        _rangeCircle.endWidth = _rangeCircleWidth;
        _rangeCircle.loop = true;
        _rangeCircle.numCornerVertices = 4;
        _rangeCircle.numCapVertices = 4;
        _rangeCircle.alignment = LineAlignment.View;
        _rangeCircle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _rangeCircle.receiveShadows = false;
        _rangeCircle.enabled = false;
    }

    private void UpdateRangeCircle()
    {
        bool visible = _inputHub != null && _inputHub.RPressed;
        _rangeCircle.enabled = visible;
        if (!visible) return;

        // 足元の高さへ合わせる。
        float footY = _characterController != null
            ? _characterController.center.y - _characterController.height * 0.5f + 0.025f
            : 0.05f;
        _rangeCircle.transform.localPosition = new Vector3(0f, footY, 0f);

        int segments = Mathf.Max(12, _arenaSegments);
        _rangeCircle.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            _rangeCircle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * _castRange, 0f, Mathf.Sin(angle) * _castRange));
        }
    }

    private void CreateArenaCircle()
    {
        GameObject obj = new GameObject("Zelf R Arena Circle");
        obj.transform.SetParent(transform, false);
        _arenaCircle = obj.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _arenaMaterial = new Material(shader);
        _arenaMaterial.color = _arenaColor;

        _arenaCircle.useWorldSpace = true;
        _arenaCircle.material = _arenaMaterial;
        _arenaCircle.startColor = _arenaColor;
        _arenaCircle.endColor = _arenaColor;
        _arenaCircle.startWidth = _arenaLineWidth;
        _arenaCircle.endWidth = _arenaLineWidth;
        _arenaCircle.loop = true;
        _arenaCircle.numCornerVertices = 4;
        _arenaCircle.numCapVertices = 4;
        _arenaCircle.alignment = LineAlignment.View;
        _arenaCircle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _arenaCircle.receiveShadows = false;
        _arenaCircle.enabled = false;
    }

    private void DrawArenaCircle(Vector3 center)
    {
        int segments = Mathf.Max(12, _arenaSegments);
        _arenaCircle.positionCount = segments;
        float y = center.y + 0.05f;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            _arenaCircle.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * _arenaRadius,
                y,
                center.z + Mathf.Sin(angle) * _arenaRadius));
        }
    }
}
