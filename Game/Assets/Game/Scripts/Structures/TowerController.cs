using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1本目のタワー(GAME_DESIGN.md 3章)。MapBuilderが実行時に生成し、Initializeで所属チームを設定する。
/// - 射程8(設計800)内の敵(TeamMemberを持つ対象)を自動攻撃する。敵ミニオンを優先し、
///   ミニオンがいない場合のみ敵ヒーローを狙う。構造物(タワー・本拠地)は狙わない。
/// - 同一ヒーローへの連続攻撃で威力+25%/発(最大+200%)。攻撃が2秒間途切れるとリセット。
/// - 受けるダメージ(IIncomingDamageModifier):
///   1. 同一チームからのダメージは0(味方のタワーは殴れない)。
///   2. 通常攻撃(DamageContext.IsBasicAttack)以外のダメージは0(スキルでは攻撃できない)。
///   3. 攻撃者の周囲8以内に攻撃側チームのミニオンがいない場合、確定ダメージは無効・通常ダメージは90%軽減。
///   4. 最後にAR60で通常ダメージを軽減(CharacterStatsを持たないため自前で適用)。
/// - 頭上にワールド空間のHPバーを実行時生成する(WorldHealthBarを再利用)。
/// - 破壊されるとGameManagerへ通知し、自チームの本拠地が攻撃可能になる。
/// </summary>
public class TowerController : MonoBehaviour, IIncomingDamageModifier
{
    private const float AttackRange = 8f;
    private const float AttackDamage = 130f;
    private const float AttacksPerSecond = 0.8f;
    private const float Armor = 60f;
    private const float ConsecutiveBonusPerHit = 0.25f;
    private const float ConsecutiveBonusMax = 2f;
    private const float ConsecutiveResetSeconds = 2f;
    private const float MinionEscortRange = 8f;
    private const float NoMinionDamageMultiplier = 0.1f;
    private const float RetargetCooldown = 0.25f;

    // HPバー: タワー中心(y=2)からの高さオフセットとワールドスケール(1px=0.01m、240x28px=2.4x0.28m)。
    private const float HealthBarHeightOffset = 3.2f;
    private const float HealthBarWorldScale = 0.01f;

    private static readonly List<TowerController> Towers = new List<TowerController>();

    private Team _team = Team.Blue;
    private HealthController _health;
    private GameObject _healthBarRoot;
    private float _attackCooldown;
    private Transform _lastHeroTarget;
    private int _consecutiveHits;
    private float _consecutiveResetTimer;
    private bool _isDestroyed;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>破壊済みかどうか。</summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>指定チームの1本目のタワーが破壊済みかどうか。本拠地の無敵判定が参照する。</summary>
    public static bool IsTowerDestroyed(Team team)
    {
        foreach (TowerController tower in Towers)
        {
            if (tower != null && tower._team == team)
            {
                return tower._isDestroyed || (tower._health != null && tower._health.IsDead);
            }
        }

        // タワーが存在しない場合は破壊済み扱い(本拠地を攻撃可能にする)。
        return true;
    }

    /// <summary>生成直後の初期化(MapBuilderから呼び出す)。</summary>
    public void Initialize(Team team)
    {
        _team = team;
        _health = GetComponent<HealthController>();
        if (_health != null)
        {
            // HealthController.AwakeのキャッシュはこのAddComponentより先に実行済みのため再取得させる。
            _health.RefreshDamageModifiers();
            _health.Died += HandleDied;
            CreateHealthBar();
        }
    }

    private void OnEnable()
    {
        Towers.Add(this);
    }

    private void OnDisable()
    {
        Towers.Remove(this);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
        }

        // HPバーは親子付けしていないため、タワー破棄時に明示的に破棄する。
        if (_healthBarRoot != null)
        {
            Destroy(_healthBarRoot);
        }
    }

    // タワー頭上にワールド空間のHPバーを実行時生成する。
    // タワー本体は非一様スケール(2.4, 2, 2.4)のため、子にするとカメラ向け回転時に歪む。
    // タワーは移動しないので、親子付けせずワールド位置だけ合わせる。
    private void CreateHealthBar()
    {
        if (_healthBarRoot != null)
        {
            return;
        }

        _healthBarRoot = new GameObject($"{_team} Tower Health Bar");
        _healthBarRoot.transform.position = transform.position + Vector3.up * HealthBarHeightOffset;
        _healthBarRoot.transform.localScale = Vector3.one * HealthBarWorldScale;

        Canvas canvas = _healthBarRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rootRect = _healthBarRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(240f, 28f);

        Image background = CreateBarImage("Background", rootRect, Vector2.zero, Vector2.zero);
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        Image fill = CreateBarImage("Fill", rootRect, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.color = Color.Lerp(_team.GetTeamColor(), Color.white, 0.2f);

        // WorldHealthBarはCanvas追加後にAddComponentする(AwakeでCanvasをキャッシュするため)。
        WorldHealthBar healthBar = _healthBarRoot.AddComponent<WorldHealthBar>();
        healthBar.InitializeRuntime(_health, fill);
    }

    private static Image CreateBarImage(string imageName, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject imageObject = new GameObject(imageName);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return imageObject.AddComponent<Image>();
    }

    private void Update()
    {
        if (_isDestroyed || _health == null || _health.IsDead)
        {
            return;
        }

        // 連続攻撃ボーナスは攻撃が2秒間途切れるとリセットする。
        if (_consecutiveResetTimer > 0f)
        {
            _consecutiveResetTimer -= Time.deltaTime;
            if (_consecutiveResetTimer <= 0f)
            {
                _consecutiveHits = 0;
                _lastHeroTarget = null;
            }
        }

        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f)
        {
            return;
        }

        HealthController target = AcquireTarget(out bool isHero);
        if (target == null)
        {
            // ターゲットがいない間は短い間隔で再索敵する。
            _attackCooldown = RetargetCooldown;
            return;
        }

        Attack(target, isHero);
        _attackCooldown = 1f / AttacksPerSecond;
    }

    // 射程内の敵を探す。敵ミニオンを優先し、いなければ敵ヒーロー。構造物は狙わない。
    private HealthController AcquireTarget(out bool isHero)
    {
        HealthController bestMinion = null;
        float bestMinionDistance = float.MaxValue;
        HealthController bestHero = null;
        float bestHeroDistance = float.MaxValue;

        foreach (TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (member.Team == _team)
            {
                continue;
            }

            // 構造物(タワー・本拠地)は狙わない。
            if (member.GetComponent<TowerController>() != null || member.GetComponent<NexusController>() != null)
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
            if (distance > AttackRange)
            {
                continue;
            }

            bool isMinion = member.GetComponent<MinionController>() != null;
            if (isMinion)
            {
                if (distance < bestMinionDistance)
                {
                    bestMinionDistance = distance;
                    bestMinion = health;
                }
            }
            else if (distance < bestHeroDistance)
            {
                bestHeroDistance = distance;
                bestHero = health;
            }
        }

        if (bestMinion != null)
        {
            isHero = false;
            return bestMinion;
        }

        isHero = bestHero != null;
        return bestHero;
    }

    private void Attack(HealthController target, bool isHero)
    {
        float damage = AttackDamage;

        if (isHero)
        {
            // 同一ヒーローへの連続攻撃で威力+25%/発(最大+200%)。
            if (_lastHeroTarget == target.transform)
            {
                _consecutiveHits++;
            }
            else
            {
                _lastHeroTarget = target.transform;
                _consecutiveHits = 0;
            }

            damage *= 1f + Mathf.Min(ConsecutiveBonusMax, _consecutiveHits * ConsecutiveBonusPerHit);
            _consecutiveResetTimer = ConsecutiveResetSeconds;
        }
        else
        {
            _lastHeroTarget = null;
            _consecutiveHits = 0;
        }

        // タワーの攻撃は通常攻撃扱い(将来ミニオン以外へのルール拡張に備えてフラグを明示)。
        float dealt = target.TakeDamage(damage, transform, DamageType.Normal, isBasicAttack: true);
        if (dealt > 0f)
        {
            Targetable targetable = target.GetComponent<Targetable>();
            if (targetable != null)
            {
                targetable.PlayHitFlash();
            }
        }
    }

    /// <summary>受けるダメージの変更(IIncomingDamageModifier)。クラスコメントのルールを適用する。</summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        // 同一チームからのダメージは受けない(味方のタワーは殴れない)。
        if (context.Attacker != null)
        {
            TeamMember attackerTeam = context.Attacker.GetComponent<TeamMember>();
            if (attackerTeam != null && attackerTeam.Team == _team)
            {
                return 0f;
            }
        }

        // タワーは通常攻撃でのみダメージを受ける(ゼルフW/Eなどのスキル・反射は無効)。
        if (!context.IsBasicAttack)
        {
            return 0f;
        }

        // 攻撃者の周囲に攻撃側チームのミニオンがいない場合: 確定ダメージ無効・通常ダメージ90%軽減。
        if (!HasEscortMinions(context.Attacker))
        {
            if (context.Type == DamageType.True)
            {
                return 0f;
            }

            currentAmount *= NoMinionDamageMultiplier;
        }

        // ARによる通常ダメージの軽減(CharacterStatsを持たないため自前で適用)。
        if (context.Type == DamageType.Normal)
        {
            currentAmount = currentAmount * 100f / (100f + Armor);
        }

        return currentAmount;
    }

    // 攻撃者の周囲MinionEscortRange以内に、攻撃側チームの生存ミニオンがいるかどうか。
    private static bool HasEscortMinions(Transform attacker)
    {
        if (attacker == null)
        {
            return false;
        }

        TeamMember attackerTeam = attacker.GetComponent<TeamMember>();
        if (attackerTeam == null)
        {
            return false;
        }

        foreach (MinionController minion in MinionController.ActiveMinions)
        {
            if (minion == null || minion.IsDead || minion.Team != attackerTeam.Team)
            {
                continue;
            }

            Vector3 delta = minion.transform.position - attacker.position;
            delta.y = 0f;
            if (delta.magnitude <= MinionEscortRange)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleDied()
    {
        if (_isDestroyed)
        {
            return;
        }

        _isDestroyed = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyTowerDestroyed(_team);
        }

        // 少し遅らせてオブジェクトを破棄する。
        Destroy(gameObject, 2f);
    }
}
