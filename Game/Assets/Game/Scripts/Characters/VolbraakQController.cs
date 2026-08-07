using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ヴォルブラークQ(地面叩きと亀裂)を管理する。
/// Qキーでマウスカーソル方向へ地面を叩き、前方の帯状範囲(亀裂の範囲)へ範囲ダメージを与える。
/// 叩いた場所には亀裂が残り、持続時間の間、亀裂の上にいる敵へスロウを継続付与する。
/// - 同時に複数の亀裂は存在しない(GAME_DESIGN 12章)。亀裂が残っている間に再発動した場合、古い亀裂は即時消滅する。
/// - スロウはCrowdControlController.ApplySlow経由で適用する(複数スロウは最も強い1つだけが有効になるLoL方式)。
///   スロウはソフトCCのため共通Dでは防げない。Tower分類は移動しないためスロウは掛けない(ダメージは与える)。
/// - Qは移動を伴わないスキルのため、スネア中も使用できる(スタン中・死亡中などは行動ロックにより使用不可)。
/// - ヴォルブラーク自身が死亡した場合、展開中の亀裂は即時消滅する(行動ロック中も展開済みの亀裂は進行・終了する)。
/// - 発動時の範囲ダメージには発動ごとのSourceId("VolbraakQ#n")を付与する(全対象で共通。連撃ルーンの1スキル1カウント判定に使用。phase7-runes-fix4)。
/// NormalCast: Qキーを押している間は方向線を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class VolbraakQController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterStats _characterStats;

    [Header("Layers")]
    // ZelfQControllerと同じレイヤーを設定する(Ground: マウス地点・地面高さ判定用 / Targetable: 命中判定用)。
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Fissure")]
    // 亀裂(範囲攻撃)の長さ・幅(Unity units)。自身の足元からマウス方向へ伸びる帯状の範囲。
    [SerializeField, Min(0.5f)] private float _fissureLength = 4f;
    [SerializeField, Min(0.1f)] private float _fissureWidth = 1.6f;
    // 亀裂の持続時間(秒)。
    [SerializeField, Min(0f)] private float _fissureDuration = 4f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _baseDamage = 25f;
    [SerializeField, Range(0f, 2f)] private float _adRatio = 0.8f;

    [Header("Slow")]
    // 亀裂上の敵へ掛けるスロウ率(0.35 = 基礎移動速度の35%減)。
    [SerializeField, Range(0f, 1f)] private float _slowPercent = 0.35f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 8f;

    [Header("Cast")]
    [SerializeField] private SkillCastMode _castMode = SkillCastMode.NormalCast;

    [Header("Visual")]
    [SerializeField] private Color _fissureColor = new Color(0.95f, 0.5f, 0.15f, 0.9f);
    [SerializeField, Min(0.005f)] private float _fissureLineWidth = 0.06f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isFissureActive;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private float _remainingFissureDuration;

    // 亀裂上スロウの掛け直し間隔(秒)と1回分の持続時間(秒)。
    // 短い持続のスロウを一定間隔で掛け直して「亀裂の上にいる間だけスロウ」を維持する(ZelfRのエリア内スロウと同じ方式)。
    private const float SlowRefreshInterval = 0.25f;
    private const float SlowDuration = 0.4f;

    private CharacterController _characterController;
    private PlayerMouseFacing _mouseFacing;
    private HealthController _selfHealth;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;
    private Camera _mainCamera;
    private SkillRangeIndicator _rangeIndicator;

    // クールダウン終了時刻。長時間起動でもfloat精度が落ちないよう、Time.timeAsDouble基準のdoubleで管理する。
    private double _cooldownEndTime;

    // Qの発動回数。発動ごとのSourceId("VolbraakQ#n")の発行に使用する
    // (連撃ルーンの1スキル1カウント判定用。phase7-runes-fix4)。
    private int _qCastCount;

    // 展開中の亀裂。_fissureRootがnullのとき亀裂の見た目は存在しない。
    private GameObject _fissureRoot;
    private Material _fissureMaterial;
    private Vector3 _fissureCenter;
    private Quaternion _fissureRotation;
    private float _fissureEndTime;
    private float _nextSlowRefreshTime;
    // 現在「亀裂の上」としてスロウを掛けている対象。
    private readonly HashSet<CrowdControlController> _targetsOnFissure = new HashSet<CrowdControlController>();

    public bool IsFissureActive => _isFissureActive;

    /// <summary>FlashControllerなどがInspector未設定時に流用するGroundのLayerMask。</summary>
    public LayerMask GroundLayerMask => _groundLayer;

    /// <summary>FlashControllerなどがInspector未設定時に流用するTargetableのLayerMask。</summary>
    public LayerMask TargetableLayerMask => _targetableLayer;

    private void Awake()
    {
        _characterStats = _characterStats != null ? _characterStats : GetComponent<CharacterStats>();
        _characterController = GetComponent<CharacterController>();
        _mouseFacing = GetComponent<PlayerMouseFacing>();
        _selfHealth = GetComponent<HealthController>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _mainCamera = Camera.main;
        _rangeIndicator = SkillRangeIndicator.Create(transform, "Volbraak Q Range Indicator");

        if (_selfHealth != null) _selfHealth.Died += HandleSelfDied;

        if (_groundLayer.value == 0 || _targetableLayer.value == 0)
        {
            Debug.LogWarning("ヴォルブラーク Q: Ground Layer / Targetable LayerをInspectorで設定してください(ZelfQControllerと同じ設定)。", this);
        }
    }

    private void OnDestroy()
    {
        if (_selfHealth != null) _selfHealth.Died -= HandleSelfDied;
        DestroyFissureVisual();
        if (_fissureMaterial != null) Destroy(_fissureMaterial);
    }

    // 自身の死亡時: 展開中の亀裂を即時終了する。
    private void HandleSelfDied()
    {
        if (!_isFissureActive) return;
        EndFissure();
        Debug.Log("ヴォルブラーク Q: 死亡により亀裂を終了しました。", this);
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);

        // 展開済みの亀裂の進行(持続時間・スロウ更新)は行動ロック中も継続する。
        if (_isFissureActive)
        {
            _remainingFissureDuration = Mathf.Max(0f, _fissureEndTime - Time.time);
            if (Time.time >= _fissureEndTime)
            {
                EndFissure();
                Debug.Log("ヴォルブラーク Q: 亀裂が消滅しました。", this);
            }
            else
            {
                UpdateFissureSlow();
            }
        }

        // 行動ロック中(スタン中・死亡中など)は入力を受け付けない。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            if (_rangeIndicator != null) _rangeIndicator.HideAll();
            // 診断用: ロック中のQ押下は理由をログに出す。
            if (_inputHub != null && _inputHub.QPressedThisFrame)
            {
                Debug.Log("ヴォルブラーク Q: 他の行動中のため発動できません。", this);
            }
            return;
        }

        // NormalCast: 押している間は方向線を表示し、離した瞬間に発動 / QuickCast: 押した瞬間に発動。
        UpdateRangeIndicator();

        if (_inputHub != null && _castMode.IsCastTriggered(_inputHub.QPressedThisFrame, _inputHub.QReleasedThisFrame))
        {
            HandleQPressed();
        }
    }

    // Qキーを押している間、本体→カーソル方向の直線(長さ=亀裂の長さ)のみを表示する(方向指定スキルの可視化)。
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null) return;
        bool visible = _inputHub != null && _inputHub.QPressed
            && (_selfHealth == null || !_selfHealth.IsDead);
        if (!visible)
        {
            _rangeIndicator.HideAll();
            return;
        }

        Vector3 direction = GetCastDirection();
        if (direction.sqrMagnitude < 0.0001f)
        {
            _rangeIndicator.HideAll();
            return;
        }

        float yOffset = _characterController != null
            ? _characterController.center.y - _characterController.height * 0.5f + 0.05f
            : 0.05f;
        Vector3 origin = transform.position + new Vector3(0f, yOffset, 0f);
        _rangeIndicator.ShowDirectionLine(origin, direction, _fissureLength, new Color(_fissureColor.r, _fissureColor.g, _fissureColor.b, 0.9f));
    }

    private void HandleQPressed()
    {
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("ヴォルブラーク Q: クールダウン中です。", this);
            return;
        }
        if (_selfHealth != null && _selfHealth.IsDead)
        {
            Debug.Log("ヴォルブラーク Q: 死亡中のため発動できません。", this);
            return;
        }

        Vector3 direction = GetCastDirection();
        if (direction.sqrMagnitude < 0.0001f) return;

        _cooldownEndTime = Time.timeAsDouble + _cooldown;

        // 発動方向を向く(移動は伴わない)。
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        if (_mouseFacing != null) _mouseFacing.SetLookDirection(direction);

        // 同時に複数の亀裂は存在しない: 既存の亀裂は即時消滅させてから新しい亀裂を作る。
        if (_isFissureActive)
        {
            EndFissure();
            Debug.Log("ヴォルブラーク Q: 古い亀裂を消滅させました。", this);
        }

        CreateFissure(direction);
        DealSlamDamage();
        Debug.Log("ヴォルブラーク Q: 地面を叩いて亀裂を作りました。", this);
    }

    // マウスカーソルの地面位置から本体→カーソルのXZ方向を求める。取得できない場合は本体の正面方向を使う。
    private Vector3 GetCastDirection()
    {
        if (TryGetMouseGroundPoint(out Vector3 groundPoint))
        {
            Vector3 direction = groundPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) return direction.normalized;
        }
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (_inputHub == null || _groundLayer.value == 0) return false;
        // Camera.mainは毎フレーム呼ぶと検索コストがかかるため、Awakeでキャッシュし、破棄時のみ再取得する。
        if (_mainCamera == null) { _mainCamera = Camera.main; if (_mainCamera == null) return false; }
        Ray ray = _mainCamera.ScreenPointToRay(_inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _groundLayer, QueryTriggerInteraction.Ignore)) return false;
        point = hit.point;
        return true;
    }

    private void CreateFissure(Vector3 direction)
    {
        _fissureRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 origin = transform.position;
        _fissureCenter = origin + direction * (_fissureLength * 0.5f);
        _fissureCenter.y = GetGroundY(_fissureCenter);

        _isFissureActive = true;
        _fissureEndTime = Time.time + _fissureDuration;
        _remainingFissureDuration = _fissureDuration;
        _nextSlowRefreshTime = 0f;
        _targetsOnFissure.Clear();

        CreateFissureVisual();
    }

    // 発動時の範囲攻撃: 亀裂の帯状範囲にいる全Targetable(自身を除く)へダメージを与える。
    private void DealSlamDamage()
    {
        float damage = _baseDamage + (_characterStats != null ? _characterStats.CurrentAttackDamage : 0f) * _adRatio;
        if (damage <= 0f) return;

        // 今回のQ発動を識別するSourceIdを発行する(全対象で共通。連撃ルーンの1スキル1カウント判定に使用)。
        _qCastCount++;
        string sourceId = $"VolbraakQ#{_qCastCount}";

        foreach (Targetable target in FindTargetablesOnFissure())
        {
            HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
            if (health == null || health.IsDead) continue;

            float actualDamage = health.TakeDamage(damage, transform, DamageType.Normal, sourceId: sourceId);
            if (actualDamage <= 0f) continue;
            target.PlayHitFlash();
            CombatTextManager.ShowDamageDealt(target.transform.position, actualDamage);
        }
    }

    // 亀裂上スロウの付与と掛け直し。亀裂から離れた対象は管理から外す(残っているスロウは持続後に自然に切れる)。
    private void UpdateFissureSlow()
    {
        bool refreshNow = Time.time >= _nextSlowRefreshTime;
        if (refreshNow) _nextSlowRefreshTime = Time.time + SlowRefreshInterval;

        HashSet<CrowdControlController> detected = new HashSet<CrowdControlController>();
        foreach (Targetable target in FindTargetablesOnFissure())
        {
            // Tower分類は移動しないためスロウは掛けない。
            if (target.Classification == TargetClassification.Tower) continue;

            // CCを受け取る入口を取得(未追加でも動くようにget-or-add)。
            CrowdControlController cc = target.GetComponentInParent<CrowdControlController>();
            if (cc == null) cc = target.gameObject.AddComponent<CrowdControlController>();
            detected.Add(cc);

            if (!_targetsOnFissure.Contains(cc))
            {
                _targetsOnFissure.Add(cc);
                cc.ApplySlow(_slowPercent * 100f, SlowDuration);
                Debug.Log($"ヴォルブラーク Q: {target.name} に亀裂スロウを付与しました。", this);
            }
            else if (refreshNow)
            {
                // 掛け直し(リフレッシュ)はログなしで行い、ログの連打を防ぐ。
                cc.ApplySlow(_slowPercent * 100f, SlowDuration, withLog: false);
            }
        }

        _targetsOnFissure.RemoveWhere(cc => cc == null || !detected.Contains(cc));
    }

    // 亀裂の帯状範囲にいるTargetable(自身を除く・重複なし)を列挙する。
    private List<Targetable> FindTargetablesOnFissure()
    {
        List<Targetable> results = new List<Targetable>();
        if (_targetableLayer.value == 0) return results;

        // 高さ方向は地面の上下をゆるくカバーする(中心=地面+1m、半径1.5m → 地面-0.5m〜+2.5m)。
        Vector3 halfExtents = new Vector3(_fissureWidth * 0.5f, 1.5f, _fissureLength * 0.5f);
        Collider[] hits = Physics.OverlapBox(_fissureCenter + Vector3.up * 1f, halfExtents, _fissureRotation, _targetableLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            Targetable target = hit.GetComponentInParent<Targetable>();
            if (target == null || results.Contains(target)) continue;
            if (target.transform == transform || target.transform.IsChildOf(transform)) continue;
            if (!target.isActiveAndEnabled || target.IsDead) continue;
            results.Add(target);
        }
        return results;
    }

    private void EndFissure()
    {
        _isFissureActive = false;
        _remainingFissureDuration = 0f;
        _targetsOnFissure.Clear();
        DestroyFissureVisual();
    }

    // 亀裂の見た目(帯状範囲の枠+中央のジグザグ線)をシーン直下へ実行時生成する。
    // Playerの子にはしない(亀裂は地面に固定され、ヴォルブラークが移動しても付いてこない)。
    private void CreateFissureVisual()
    {
        DestroyFissureVisual();
        _fissureRoot = new GameObject("Volbraak Q Fissure");
        _fissureRoot.transform.SetPositionAndRotation(_fissureCenter + Vector3.up * 0.03f, _fissureRotation);

        if (_fissureMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _fissureMaterial = new Material(shader);
            _fissureMaterial.color = _fissureColor;
        }

        float halfWidth = _fissureWidth * 0.5f;
        float halfLength = _fissureLength * 0.5f;

        // 帯状範囲の枠。
        LineRenderer outline = CreateFissureLine("Outline");
        outline.loop = true;
        outline.positionCount = 4;
        outline.SetPosition(0, new Vector3(-halfWidth, 0f, -halfLength));
        outline.SetPosition(1, new Vector3(halfWidth, 0f, -halfLength));
        outline.SetPosition(2, new Vector3(halfWidth, 0f, halfLength));
        outline.SetPosition(3, new Vector3(-halfWidth, 0f, halfLength));

        // 中央のジグザグ線(亀裂の見た目)。
        LineRenderer crack = CreateFissureLine("Crack");
        crack.loop = false;
        const int crackPoints = 9;
        crack.positionCount = crackPoints;
        for (int i = 0; i < crackPoints; i++)
        {
            float t = (float)i / (crackPoints - 1);
            float x = (i == 0 || i == crackPoints - 1) ? 0f : (i % 2 == 0 ? -1f : 1f) * halfWidth * 0.35f;
            crack.SetPosition(i, new Vector3(x, 0f, Mathf.Lerp(-halfLength, halfLength, t)));
        }
    }

    private LineRenderer CreateFissureLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(_fissureRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = _fissureMaterial;
        line.startColor = _fissureColor;
        line.endColor = _fissureColor;
        line.startWidth = _fissureLineWidth;
        line.endWidth = _fissureLineWidth;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void DestroyFissureVisual()
    {
        if (_fissureRoot == null) return;
        Destroy(_fissureRoot);
        _fissureRoot = null;
    }

    private float GetGroundY(Vector3 position)
    {
        if (_groundLayer.value != 0 &&
            Physics.Raycast(new Vector3(position.x, transform.position.y + 20f, position.z), Vector3.down,
                out RaycastHit hit, 50f, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        // Groundレイヤー未設定などで地面が取れない場合は、自身の足元の高さで代用する。
        return _characterController != null
            ? transform.position.y + _characterController.center.y - _characterController.height * 0.5f
            : transform.position.y;
    }
}
