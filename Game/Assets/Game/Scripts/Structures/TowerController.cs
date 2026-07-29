using UnityEngine;

/// <summary>
/// フェーズ5: タワー本体(GAME_DESIGN 4章)。MapBuilderが実行時に組み立ててInitialize()で初期化する。
/// - ステータス: HP5000 / AR60 / AD130 / AS0.80 / 射程800(=8.0)。HPregなし・復活なし。
/// - CharacterStatsを持たないため、AR(防御力)による通常ダメージ軽減はIIncomingDamageModifierとして自前で適用する
///   (確定ダメージは軽減しない。HealthControllerの最大HPはSetMaxHealth()で設定する)。
/// - 射程内の敵ヒーロー(PlayerSpawnerのTeamで敵味方判定。スポナーが無いシーンはBlue扱い)を
///   攻撃間隔(1/AS)ごとに即時攻撃する(攻撃者として自身のTransformを渡す通常ダメージ。
///   ヴォルブラークPなどの攻撃者分類判定は自身のTargetable(Tower)でそのまま機能する)。
/// - 連続攻撃ダメージ増加: 同じ対象への連続攻撃で2発目から基礎ADの25%ずつ増加。増加分の上限は基礎の200%
///   (最終ダメージは基礎の3倍)。2秒間ヒーローを攻撃しない・対象変更でリセットする。
/// - HPは1000刻みの5段階で管理し、段階を割るたびにログへ出す(段階報酬・破壊報酬のポイントはフェーズ6)。
/// - 破壊時は攻撃を停止し、クリスタル消灯・ビーム/HPバー非表示(本体の非表示はTargetableの死亡処理)。
/// - 攻撃ビームと頭上のHPバーはLineRendererの実行時生成。
/// - ミニオン不在時の90%軽減+確定ダメージ無効はミニオン実装と同時の後続タスクで実装する。
/// </summary>
public sealed class TowerController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("ステータス (GAME_DESIGN 4章)")]
    [SerializeField, Min(1f)] private float _maxHealth = 5000f;
    [SerializeField, Min(0f)] private float _armor = 60f;
    [SerializeField, Min(0f)] private float _attackDamage = 130f;
    [Tooltip("毎秒の攻撃回数。攻撃間隔 = 1 / 攻撃速度")]
    [SerializeField, Min(0.01f)] private float _attackSpeed = 0.8f;
    [Tooltip("攻撃射程(GAME_DESIGN: 800 = 8.0 Unity単位)。対象Colliderの最も近い点との水平距離で判定する")]
    [SerializeField, Min(0.5f)] private float _attackRange = 8f;

    [Header("連続攻撃ダメージ増加")]
    [Tooltip("連続攻撃の1発ごとの増加量(基礎ADに対する%)。2発目から適用する")]
    [SerializeField, Min(0f)] private float _bonusPerHitPercent = 25f;
    [Tooltip("増加分の上限(基礎ADに対する%)。200なら最終ダメージは基礎の3倍")]
    [SerializeField, Min(0f)] private float _maxBonusPercent = 200f;
    [Tooltip("この秒数ヒーローを攻撃しないと連続攻撃がリセットされる")]
    [SerializeField, Min(0.1f)] private float _resetSeconds = 2f;

    [Header("HP段階")]
    [Tooltip("HP段階の刻み幅(1000ならHP5000は5段階)。段階報酬のポイント付与はフェーズ6")]
    [SerializeField, Min(1f)] private float _healthStageSize = 1000f;

    [Header("見た目")]
    [SerializeField, Min(0.02f)] private float _beamDuration = 0.15f;
    [SerializeField, Min(1f)] private float _healthBarHeight = 5.4f;
    [SerializeField, Min(0.5f)] private float _healthBarWidth = 2.4f;

    // ターゲット検索の間隔(秒)。毎フレームのFindFirstObjectByTypeを避ける。
    private const float TargetSearchInterval = 0.5f;

    private Team _team;
    private Color _teamColor;
    private Renderer _crystalRenderer;
    private HealthController _health;

    private Targetable _targetTargetable;
    private HealthController _targetHealth;
    private Transform _targetTransform;
    private float _nextSearchTime;
    private float _nextAttackTime;

    private int _consecutiveHits;
    private float _lastHeroAttackTime;
    private float _lastLoggedBonusPercent;

    private int _lastHealthStage;
    private float _healthFraction = 1f;

    private LineRenderer _beam;
    private float _beamHideTime;
    private LineRenderer _healthBarBackground;
    private LineRenderer _healthBarFill;

    /// <summary>このタワーの陣営。敵味方判定に使用する。</summary>
    public Team Team => _team;

    /// <summary>破壊済みかどうか。破壊されたタワーは復活しない。</summary>
    public bool IsDestroyed { get; private set; }

    /// <summary>
    /// MapBuilderのタワー組み立てから呼び出す初期化。陣営・陣営色・クリスタルRendererを設定し、
    /// HealthControllerの最大HP設定・イベント購読・ビーム/HPバーの生成を行う。
    /// </summary>
    public void Initialize(Team team, Color teamColor, Renderer crystalRenderer)
    {
        _team = team;
        _teamColor = teamColor;
        _crystalRenderer = crystalRenderer;

        _health = GetComponent<HealthController>();
        if (_health == null)
        {
            Debug.LogError("TowerController: HealthControllerが見つからないためタワーを初期化できません。", this);
            return;
        }

        _health.SetMaxHealth(_maxHealth);
        _health.HealthChanged += HandleHealthChanged;
        _health.Died += HandleDied;

        _lastHealthStage = Mathf.CeilToInt(_maxHealth / _healthStageSize);

        CreateBeam();
        CreateHealthBar();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.HealthChanged -= HandleHealthChanged;
            _health.Died -= HandleDied;
        }
    }

    private void Update()
    {
        // ビームは短時間だけ表示する。
        if (_beam != null && _beam.enabled && Time.time >= _beamHideTime)
        {
            _beam.enabled = false;
        }

        if (IsDestroyed)
        {
            return;
        }

        // 一定時間ヒーローを攻撃しないと連続攻撃がリセットされる。
        if (_consecutiveHits > 0 && Time.time - _lastHeroAttackTime > _resetSeconds)
        {
            ResetConsecutiveHits("一定時間攻撃しなかった");
        }

        if (Time.time >= _nextSearchTime)
        {
            _nextSearchTime = Time.time + TargetSearchInterval;
            TryAcquireEnemyHero();
        }

        if (_targetHealth == null || _targetTargetable == null || _targetTargetable.IsDead)
        {
            return;
        }

        if (!IsInRange() || Time.time < _nextAttackTime)
        {
            return;
        }

        Attack();
        _nextAttackTime = Time.time + 1f / _attackSpeed;
    }

    private void LateUpdate()
    {
        AlignHealthBar();
    }

    /// <summary>
    /// 被ダメージの直前に呼ばれる(HealthController)。CharacterStatsを持たないため、
    /// 通常ダメージ(Normal)へのAR軽減式 FinalDamage = RawDamage x 100 / (100 + AR) を自前で適用する。
    /// 確定ダメージ(True)は軽減しない。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float amount)
    {
        if (context.Type != DamageType.Normal || _armor <= 0f)
        {
            return amount;
        }

        return amount * 100f / (100f + _armor);
    }

    // 敵ヒーローを探す。試作は1v1のためPlayer(PlayerClickMovement)を1体だけ検出し、
    // PlayerSpawnerのTeamで敵味方を判定する(スポナーが無いシーンはBlue扱い)。
    private void TryAcquireEnemyHero()
    {
        PlayerClickMovement player = FindFirstObjectByType<PlayerClickMovement>();
        if (player == null)
        {
            ClearTarget();
            return;
        }

        PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
        Team playerTeam = spawner != null ? spawner.Team : Team.Blue;
        if (playerTeam == _team)
        {
            ClearTarget();
            return;
        }

        if (_targetTransform == player.transform)
        {
            return;
        }

        // 対象変更で連続攻撃はリセットする。
        if (_targetTransform != null)
        {
            ResetConsecutiveHits("対象を変更した");
        }

        _targetTransform = player.transform;
        _targetTargetable = player.GetComponent<Targetable>();
        _targetHealth = player.GetComponent<HealthController>();
    }

    private void ClearTarget()
    {
        _targetTransform = null;
        _targetTargetable = null;
        _targetHealth = null;
    }

    // 射程判定は通常攻撃と同じく、対象Colliderの最も近い点との水平距離(XZ平面)で行う。
    private bool IsInRange()
    {
        Vector3 closest = _targetTargetable.GetClosestPoint(transform.position);
        Vector3 delta = closest - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= _attackRange * _attackRange;
    }

    private void Attack()
    {
        _consecutiveHits++;
        _lastHeroAttackTime = Time.time;

        // 2発目から基礎ADの25%ずつ増加。増加分の上限は基礎の200%(最終ダメージは基礎の3倍)。
        float bonusPercent = Mathf.Min(_bonusPerHitPercent * (_consecutiveHits - 1), _maxBonusPercent);
        float damage = _attackDamage * (1f + bonusPercent / 100f);

        _targetHealth.TakeDamage(damage, transform, DamageType.Normal);
        _targetTargetable.PlayHitFlash();
        ShowBeam();

        // ログは倍率が変わった攻撃だけ出す(毎撃のログで埋もれないようにする)。
        if (!Mathf.Approximately(bonusPercent, _lastLoggedBonusPercent))
        {
            _lastLoggedBonusPercent = bonusPercent;
            Debug.Log($"タワー({_team}): 連続攻撃{_consecutiveHits}発目。ダメージ倍率 {1f + bonusPercent / 100f:F2}倍({damage:F0}ダメージ)。", this);
        }
    }

    private void ResetConsecutiveHits(string reason)
    {
        if (_consecutiveHits > 1)
        {
            Debug.Log($"タワー({_team}): {reason}ため連続攻撃をリセットしました。", this);
        }

        _consecutiveHits = 0;
        _lastLoggedBonusPercent = 0f;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        _healthFraction = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        // HP1000刻みの段階を割ったらログへ出す(段階報酬のポイント付与はフェーズ6で実装する)。
        int stage = Mathf.Max(0, Mathf.CeilToInt(currentHealth / _healthStageSize));
        if (stage < _lastHealthStage && currentHealth > 0f)
        {
            Debug.Log($"タワー({_team}): HP段階が {_lastHealthStage} → {stage} へ下がりました(残HP {currentHealth:F0})。", this);
            _lastHealthStage = stage;
        }
    }

    private void HandleDied()
    {
        IsDestroyed = true;
        ClearTarget();

        // クリスタルを消灯(暗色化)。本体の非表示・選択不可化はTargetableの死亡処理が行う。
        if (_crystalRenderer != null)
        {
            _crystalRenderer.material.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        if (_beam != null)
        {
            _beam.enabled = false;
        }

        SetHealthBarVisible(false);

        Debug.Log($"タワー({_team}): 破壊されました(破壊報酬のポイント付与はフェーズ6)。", this);
    }

    // 攻撃ビーム: クリスタル付近から対象へのLineRendererを短時間表示する。
    private void CreateBeam()
    {
        GameObject beamObject = new GameObject("AttackBeam");
        beamObject.transform.SetParent(transform, false);
        _beam = beamObject.AddComponent<LineRenderer>();
        _beam.positionCount = 2;
        _beam.startWidth = 0.15f;
        _beam.endWidth = 0.05f;
        _beam.material = new Material(Shader.Find("Sprites/Default"));
        _beam.startColor = _teamColor;
        _beam.endColor = _teamColor;
        _beam.enabled = false;
    }

    private void ShowBeam()
    {
        if (_beam == null || _targetTransform == null)
        {
            return;
        }

        _beam.SetPosition(0, transform.position + Vector3.up * 4.3f);
        _beam.SetPosition(1, _targetTransform.position + Vector3.up * 0.5f);
        _beam.enabled = true;
        _beamHideTime = Time.time + _beamDuration;
    }

    // 頭上のHPバー: 背景と残HPの2本のLineRenderer。LateUpdateでカメラの横方向へ揃える。
    private void CreateHealthBar()
    {
        _healthBarBackground = CreateHealthBarLine("HealthBarBackground", new Color(0.1f, 0.1f, 0.1f, 1f), 0.3f);
        _healthBarFill = CreateHealthBarLine("HealthBarFill", _teamColor, 0.22f);
    }

    private LineRenderer CreateHealthBarLine(string lineName, Color color, float width)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void AlignHealthBar()
    {
        if (_healthBarBackground == null || _healthBarFill == null || IsDestroyed)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        // カメラの横方向へ平行なバーとして描画する(ビルボード相当)。
        Vector3 center = transform.position + Vector3.up * _healthBarHeight;
        Vector3 right = mainCamera.transform.right;
        Vector3 left = center - right * (_healthBarWidth * 0.5f);

        _healthBarBackground.SetPosition(0, left);
        _healthBarBackground.SetPosition(1, left + right * _healthBarWidth);
        _healthBarFill.SetPosition(0, left + Vector3.up * 0.001f);
        _healthBarFill.SetPosition(1, left + right * (_healthBarWidth * _healthFraction) + Vector3.up * 0.001f);
    }

    private void SetHealthBarVisible(bool visible)
    {
        if (_healthBarBackground != null)
        {
            _healthBarBackground.enabled = visible;
        }

        if (_healthBarFill != null)
        {
            _healthBarFill.enabled = visible;
        }
    }
}
