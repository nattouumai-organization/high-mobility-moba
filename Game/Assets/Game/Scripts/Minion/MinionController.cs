using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// レーンを進軍するミニオン(GAME_DESIGN.md 3章)。GameManagerがウェーブごとにSpawnで生成する。
/// - 近接: HP420/AD18/AS0.85/射程1.75、遠隔: HP290/AD22/AS0.70/射程5。移動速度3.3。
/// - ウェーブレベルで強化: 近接 HP+20/AD+1.5/AR+1、遠隔 HP+14/AD+1.5/AR+0.5。
/// - 索敵範囲7以内の最も近い敵(TeamMemberを持つ対象)を狙う。無敵状態の本拠地は狙わない。
/// - 敵がいない間はレーン進行方向へ進軍する(レーン中心線への引き寄せ付き)。
/// - ミニオン同士が重ならないよう、毎フレームの最後に分離処理(近すぎるミニオンを押し離す)を行う。
/// - 進路上の他ミニオンは接線方向の横移動で回り込む。それでも動けない状態が続く場合は一定時間の横移動で
///   スタックを解消し、「前進する」か「標的を攻撃する」のどちらかを常に行う(何もしない時間を作らない)。
/// - 経路上の障害物(タワー・本拠地)はObstacleAvoidanceで最短側へ迂回し、ぶつからずに移動する(攻撃対象の構造物は除く)。
/// - 攻撃は通常攻撃扱い(isBasicAttack: true)。タワー・本拠地は通常攻撃のみダメージを受けるため、
///   ミニオンの攻撃は構造物にも有効。
///   頭上にワールド空間のHPバーを表示する(フェーズ6)。
///   死亡時に近くの敵ヒーローへ2pt、ラストヒットしたヒーローへ追加3ptを付与する(フェーズ6)。
/// </summary>
public class MinionController : MonoBehaviour, IIncomingDamageModifier
{
    public enum MinionType
    {
        Melee,
        Ranged,
    }

    private const float MoveSpeed = 3.3f;
    private const float AggroRange = 7f;
    private const float RetargetInterval = 0.25f;
    private const float CenterPullStrength = 0.15f;

    // 障害物回避: 進行方向この距離以内の障害物(タワー・本拠地)を迂回する。
    private const float AvoidanceLookAhead = 3f;

    // ミニオン回避: 進行方向この距離以内の他ミニオンを接線方向へ避けて回り込む。
    private const float MinionAvoidLookAhead = 1.6f;

    // 接線方向へ曲げる際に追加する角度(度)。ちょうど接線だと縁を擦るため少し外側へ向ける。
    private const float MinionAvoidExtraTurnDegrees = 6f;

    // スタック判定: 期待移動量に対する実移動量がこの割合未満のフレームを「動けていない」とみなす。
    private const float StuckMovementRatio = 0.35f;

    // 動けない状態がこの時間(秒)続いたら、横移動によるスタック解消を開始する。
    private const float StuckTimeThreshold = 0.4f;

    // スタック解消の横移動を続ける時間(秒)。
    private const float SidestepDuration = 0.5f;

    // 分離処理: 半径の合計+余白より近づいたミニオン同士を、最大SeparationSpeed(m/秒)で押し離す。
    private const float SeparationPadding = 0.1f;
    private const float SeparationSpeed = 2f;
    private const float HealthBarWorldScale = 0.01f;
    private const float HealthBarClearance = 0.4f;
    private const float ProximityPointRange = 12f;
    private const int ProximityPoints = 2;
    private const int LastHitBonusPoints = 3;

    private static readonly List<MinionController> Active = new List<MinionController>();

    // スポーン順の連番。分離処理で完全に重なった場合の決定的な方向決めに使う。
    // (Object.GetInstanceIDはUnity 6で廃止(CS0619)のため使用しない)
    private static int _spawnCounter;

    /// <summary>生存中のミニオン一覧。タワーのミニオン同伴判定などが参照する。</summary>
    public static IReadOnlyList<MinionController> ActiveMinions => Active;

    private Team _team = Team.Blue;
    private MinionType _type = MinionType.Melee;
    private HealthController _health;
    private float _attackDamage;
    private float _attackInterval;
    private float _attackRange;
    private float _armor;
    private float _radius;
    private int _spawnIndex;
    private float _attackCooldown;
    private float _retargetTimer;
    private HealthController _currentTarget;
    private Targetable _currentTargetable;

    // スタック検出用: 前フレームの位置・動けていない累計時間・横移動の残り時間と左右。
    private Vector3 _lastPosition;
    private float _stuckTime;
    private float _sidestepTimer;
    private float _sidestepSide = 1f;

    // このフレームで移動を試みたか(攻撃中はスタック判定の対象外にする)。
    private bool _triedToMoveThisFrame;
    private Transform _lastAttacker;
    private GameObject _healthBarRoot;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>死亡済みかどうか。</summary>
    public bool IsDead => _health == null || _health.IsDead;

    /// <summary>
    /// ミニオンを生成する。カプセルのプリミティブに必要なコンポーネントを付与して初期化する。
    /// AddComponentの順序はHealthController→TeamMember→Targetable→MinionController(タワーと同じ方式)。
    /// </summary>
    public static MinionController Spawn(Team team, MinionType type, Vector3 position, int waveLevel, int targetableLayer)
    {
        GameObject minionObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        minionObject.name = $"{team} {type} Minion";

        float scale = type == MinionType.Melee ? 0.65f : 0.5f;
        minionObject.transform.localScale = new Vector3(scale, scale, scale);
        minionObject.transform.position = new Vector3(position.x, scale, position.z);
        if (targetableLayer >= 0 && targetableLayer <= 31)
        {
            minionObject.layer = targetableLayer;
        }

        Renderer minionRenderer = minionObject.GetComponent<Renderer>();
        if (minionRenderer != null)
        {
            // チームカラーを少し白へ寄せて、タワー・本拠地と見分けやすくする。
            minionRenderer.material.color = Color.Lerp(team.GetTeamColor(), Color.white, 0.35f);
        }

        HealthController health = minionObject.AddComponent<HealthController>();
        TeamMember member = minionObject.AddComponent<TeamMember>();
        member.SetTeam(team);
        Targetable targetable = minionObject.AddComponent<Targetable>();
        targetable.InitializeRuntime(TargetClassification.Minion, minionRenderer);
        MinionController minion = minionObject.AddComponent<MinionController>();
        minion.Initialize(team, type, waveLevel, health);
        return minion;
    }

    private void Initialize(Team team, MinionType type, int waveLevel, HealthController health)
    {
        _team = team;
        _type = type;
        _health = health;
        _spawnIndex = _spawnCounter++;
        _lastPosition = transform.position;

        float maxHealth;
        if (type == MinionType.Melee)
        {
            maxHealth = 420f + 20f * waveLevel;
            _attackDamage = 18f + 1.5f * waveLevel;
            _attackInterval = 1f / 0.85f;
            _attackRange = 1.75f;
            _armor = 1f * waveLevel;
            _radius = 0.65f * 0.5f;
        }
        else
        {
            maxHealth = 290f + 14f * waveLevel;
            _attackDamage = 22f + 1.5f * waveLevel;
            _attackInterval = 1f / 0.70f;
            _attackRange = 5f;
            _armor = 0.5f * waveLevel;
            _radius = 0.5f * 0.5f;
        }

        if (_health != null)
        {
            _health.SetMaxHealth(maxHealth);
            // HealthController.AwakeのキャッシュはこのAddComponentより先に実行済みのため再取得させる。
            _health.RefreshDamageModifiers();
            _health.Died += HandleDied;
            _health.DamageTaken += HandleDamageTaken;
            CreateHealthBar();
        }
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
            _health.DamageTaken -= HandleDamageTaken;
        }
    }

    private void Update()
    {
        if (_health == null || _health.IsDead)
        {
            return;
        }

        _triedToMoveThisFrame = false;
        UpdateCombatAndMovement();
        ApplySeparation();
        UpdateStuckState();
    }

    private void UpdateCombatAndMovement()
    {
        _attackCooldown -= Time.deltaTime;

        // 一定間隔で索敵する(毎フレームの全走査を避ける)。
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = RetargetInterval;
            AcquireTarget();
        }

        if (_currentTarget != null && !_currentTarget.IsDead)
        {
            Vector3 closest = GetTargetClosestPoint();
            Vector3 toTarget = closest - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= _attackRange)
            {
                FaceDirection(toTarget);
                if (_attackCooldown <= 0f)
                {
                    AttackCurrentTarget();
                    _attackCooldown = _attackInterval;
                }
                return;
            }

            // 攻撃対象そのものは障害物として扱わない(構造物が対象の場合に接近できなくなるため)。
            // 他ミニオンの回り込みは停止予定地点(攻撃射程に入る位置)までを対象にする。
            MoveInDirection(toTarget.normalized, _currentTarget.transform, toTarget.magnitude - _attackRange);
            return;
        }

        MoveForward();
    }

    // ミニオン同士が重ならないようにする分離処理。
    // 半径の合計+余白より近いミニオンから離れる方向へ、最大SeparationSpeed(m/秒)で押し出される。
    // 相互に押し合うため数フレームで自然に間隔が確保される。攻撃中・停止中も適用する。
    private void ApplySeparation()
    {
        Vector3 push = Vector3.zero;
        foreach (MinionController other in Active)
        {
            if (other == this || other == null || other.IsDead)
            {
                continue;
            }

            Vector3 delta = transform.position - other.transform.position;
            delta.y = 0f;
            float minDistance = _radius + other._radius + SeparationPadding;
            float distance = delta.magnitude;
            if (distance >= minDistance)
            {
                continue;
            }

            Vector3 direction;
            if (distance > 0.001f)
            {
                direction = delta / distance;
            }
            else
            {
                // 完全に重なった場合はスポーン順の連番から決定的な方向を選ぶ(毎フレーム同じ方向へ離れられる)。
                // 47は360と互いに素のため、連番が違えば方向もばらける。
                float angle = (_spawnIndex * 47) % 360;
                direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }

            push += direction * (minDistance - distance);
        }

        if (push == Vector3.zero)
        {
            return;
        }

        Vector3 step = Vector3.ClampMagnitude(push, SeparationSpeed * Time.deltaTime);
        step.y = 0f;
        transform.position += step;
    }

    /// <summary>受けるダメージの変更(IIncomingDamageModifier)。ARで通常ダメージを軽減する。</summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (context.Type == DamageType.Normal && _armor > 0f)
        {
            currentAmount = currentAmount * 100f / (100f + _armor);
        }

        return currentAmount;
    }

    // 索敵範囲内の最も近い敵を狙う。無敵状態の本拠地・タワー(2本目)は狙わない。
    private void AcquireTarget()
    {
        _currentTarget = null;
        _currentTargetable = null;
        float bestDistance = float.MaxValue;

        foreach (TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (member.Team == _team)
            {
                continue;
            }

            NexusController nexus = member.GetComponent<NexusController>();
            if (nexus != null && nexus.IsInvulnerable)
            {
                continue;
            }

            // 無敵状態の2本目のタワーも狙わない(1本目の破壊まで)。
            TowerController invulnerableTower = member.GetComponent<TowerController>();
            if (invulnerableTower != null && invulnerableTower.IsInvulnerable)
            {
                continue;
            }

            HealthController health = member.GetComponent<HealthController>();
            if (health == null || health.IsDead)
            {
                continue;
            }

            Collider memberCollider = member.GetComponent<Collider>();
            Vector3 closest = memberCollider != null && memberCollider.enabled
                ? memberCollider.ClosestPoint(transform.position)
                : member.transform.position;
            Vector3 delta = closest - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > AggroRange || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            _currentTarget = health;
            _currentTargetable = member.GetComponent<Targetable>();
        }
    }

    private Vector3 GetTargetClosestPoint()
    {
        if (_currentTarget == null)
        {
            return transform.position;
        }

        Collider targetCollider = _currentTarget.GetComponent<Collider>();
        return targetCollider != null && targetCollider.enabled
            ? targetCollider.ClosestPoint(transform.position)
            : _currentTarget.transform.position;
    }

    // 通常攻撃(即時・弾丸なし)。isBasicAttack: trueを渡すことでタワー・本拠地にもダメージが通る。
    private void AttackCurrentTarget()
    {
        if (_currentTarget == null)
        {
            return;
        }

        float dealt = _currentTarget.TakeDamage(_attackDamage, transform, DamageType.Normal, isBasicAttack: true);
        if (dealt > 0f && _currentTargetable != null)
        {
            _currentTargetable.PlayHitFlash();
        }
    }

    // 敵がいない間はレーン進行方向へ進軍する(レーン中心線への引き寄せ付き)。
    private void MoveForward()
    {
        Vector3 direction;
        MapBuilder map = MapBuilder.Instance;
        if (map != null)
        {
            direction = map.GetLaneForward(_team) + map.GetLaneCenterPull(transform.position) * CenterPullStrength;
            direction.y = 0f;
            direction.Normalize();
        }
        else
        {
            direction = _team == Team.Blue ? Vector3.right : Vector3.left;
        }

        MoveInDirection(direction, null, MinionAvoidLookAhead);
    }

    // ignoreObstacleには攻撃対象など障害物扱いしないTransformを渡す(不要ならnull)。
    // minionAvoidDistanceは他ミニオンの回り込みを行う先読み距離(停止予定地点より先のミニオンは避けない)。
    private void MoveInDirection(Vector3 direction, Transform ignoreObstacle, float minionAvoidDistance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        _triedToMoveThisFrame = true;

        if (_sidestepTimer > 0f)
        {
            // スタック解消中: 進行方向に対して真横へ移動し、詰まりから抜け出す。
            _sidestepTimer -= Time.deltaTime;
            direction = (Quaternion.Euler(0f, _sidestepSide * 90f, 0f) * direction).normalized;
        }
        else
        {
            // 進路上の他ミニオンは接線方向へ避けて回り込む。
            direction = AvoidOtherMinions(direction, ignoreObstacle, minionAvoidDistance);
        }

        // 経路上に障害物(タワー・本拠地)がある場合は、最短側の接線方向へ迂回する。
        direction = ObstacleAvoidance.SteerDirection(
            transform.position, direction, _radius, AvoidanceLookAhead, ignoreObstacle);

        transform.position += direction * (MoveSpeed * Time.deltaTime);
        FaceDirection(direction);
    }

    // 進路上(minionAvoidDistance以内)を塞ぐ最も近い他ミニオンを円として扱い、その接線方向へ進行方向を曲げる。
    // ignoreTransformには攻撃対象を渡す(標的自身は避けずに接近する)。接触済みの相手は分離処理が押し離すため対象外。
    private Vector3 AvoidOtherMinions(Vector3 direction, Transform ignoreTransform, float minionAvoidDistance)
    {
        float lookAhead = Mathf.Min(MinionAvoidLookAhead, minionAvoidDistance);
        if (lookAhead <= 0f)
        {
            return direction;
        }

        bool found = false;
        Vector3 bestTo = Vector3.zero;
        float bestBlockRadius = 0f;
        float bestProj = float.MaxValue;

        foreach (MinionController other in Active)
        {
            if (other == this || other == null || other.IsDead || other.transform == ignoreTransform)
            {
                continue;
            }

            Vector3 to = other.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            float blockRadius = _radius + other._radius + SeparationPadding;
            if (distance <= blockRadius)
            {
                continue;
            }

            float proj = Vector3.Dot(to, direction);
            if (proj <= 0f || proj - blockRadius > lookAhead)
            {
                continue;
            }

            float perpSq = distance * distance - proj * proj;
            if (perpSq >= blockRadius * blockRadius)
            {
                continue;
            }

            if (proj < bestProj)
            {
                bestProj = proj;
                bestTo = to;
                bestBlockRadius = blockRadius;
                found = true;
            }
        }

        if (!found)
        {
            return direction;
        }

        float dist = bestTo.magnitude;
        Vector3 toDir = bestTo / dist;

        // 迂回する側: 基本は外れる角度が小さい側(最短側)。正面ちょうどの場合はスポーン順で左右をばらけさせる。
        float crossY = Vector3.Cross(bestTo, direction).y;
        float side;
        if (Mathf.Abs(crossY) < 0.01f)
        {
            side = _spawnIndex % 2 == 0 ? 1f : -1f;
        }
        else
        {
            side = crossY >= 0f ? 1f : -1f;
        }

        float tangentAngle = Mathf.Asin(Mathf.Clamp01(bestBlockRadius / dist)) * Mathf.Rad2Deg + MinionAvoidExtraTurnDegrees;
        return (Quaternion.Euler(0f, side * tangentAngle, 0f) * toDir).normalized;
    }

    // 実際の移動量を監視し、移動を試みているのに動けない状態が続いたら横移動でスタックを解消する。
    // 攻撃中(射程内で足を止めている間)はスタック判定の対象外。
    private void UpdateStuckState()
    {
        Vector3 moved = transform.position - _lastPosition;
        moved.y = 0f;
        _lastPosition = transform.position;

        if (!_triedToMoveThisFrame)
        {
            _stuckTime = 0f;
            return;
        }

        if (moved.magnitude >= MoveSpeed * Time.deltaTime * StuckMovementRatio)
        {
            _stuckTime = 0f;
            return;
        }

        _stuckTime += Time.deltaTime;
        if (_stuckTime >= StuckTimeThreshold && _sidestepTimer <= 0f)
        {
            _stuckTime = 0f;
            _sidestepTimer = SidestepDuration;
            // 横移動の左右はスポーン順の偶奇でばらけさせる(全員が同じ側へ動いて詰まり直すのを防ぐ)。
            _sidestepSide = _spawnIndex % 2 == 0 ? 1f : -1f;
        }
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void HandleDamageTaken(DamageContext context, float damage)
    {
        if (context.Attacker != null)
        {
            _lastAttacker = context.Attacker;
        }
    }

    /// <summary>
    /// 死亡時のポイント付与。範囲内(ProximityPointRange)の敵ヒーローに2pt、
    /// ラストヒットしたヒーローに追加3ptを与える。範囲12fは仕様未定義のための仮値。
    /// </summary>
    private void AwardDeathPoints()
    {
        var heroes = FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None);
        foreach (var hero in heroes)
        {
            if (hero == null)
            {
                continue;
            }

            var member = hero.GetComponent<TeamMember>();
            if (member == null || member.Team == _team)
            {
                continue;
            }

            var heroHealth = hero.GetComponent<HealthController>();
            if (heroHealth != null && heroHealth.IsDead)
            {
                continue;
            }

            int points = 0;
            string reason = null;
            if (Vector3.Distance(hero.transform.position, transform.position) <= ProximityPointRange)
            {
                points += ProximityPoints;
                reason = "minion death nearby";
            }

            if (_lastAttacker != null && (_lastAttacker == hero.transform || _lastAttacker.IsChildOf(hero.transform)))
            {
                points += LastHitBonusPoints;
                reason = reason == null ? "minion last hit" : reason + " + last hit";
            }

            if (points > 0)
            {
                PointsManager.AddPoints(member.Team, points, reason);
            }
        }
    }

    /// <summary>
    /// 頭上のHPバーを実行時生成する。ミニオンは一様スケールなので子オブジェクトでも歪まない。
    /// </summary>
    private void CreateHealthBar()
    {
        if (_health == null || _healthBarRoot != null)
        {
            return;
        }

        float scale = Mathf.Max(transform.localScale.y, 0.0001f);

        _healthBarRoot = new GameObject("MinionHealthBar");
        _healthBarRoot.transform.SetParent(transform, false);
        _healthBarRoot.transform.localPosition = Vector3.up * ((scale + HealthBarClearance) / scale);
        _healthBarRoot.transform.localScale = Vector3.one * (HealthBarWorldScale / scale);

        var canvas = _healthBarRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRect = _healthBarRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(90f, 12f);

        CreateBarImage("Background", _healthBarRoot.transform, new Color(0.08f, 0.08f, 0.08f, 0.85f));

        var fill = CreateBarImage("Fill", _healthBarRoot.transform, Color.Lerp(_team.GetTeamColor(), Color.white, 0.2f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        var healthBar = _healthBarRoot.AddComponent<WorldHealthBar>();
        healthBar.InitializeRuntime(_health, fill);
    }

    private static Image CreateBarImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private void HandleDied()
    {
        AwardDeathPoints();

        // 死体がターゲット選択・索敵に引っかからないようにコライダーを無効化し、少し遅らせて破棄する。
        Collider bodyCollider = GetComponent<Collider>();
        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        Destroy(gameObject, 2f);
    }
}
