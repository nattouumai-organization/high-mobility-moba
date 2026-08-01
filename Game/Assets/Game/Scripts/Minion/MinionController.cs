using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レーンミニオン(GAME_DESIGN.md 5章)。GameManagerがウェーブごとにSpawnで生成する。
/// - 近接: HP420 / AD18 / AS0.85 / AR0 / 射程1.75(設計175)。成長: HP+20 / AD+1.5 / AR+1。
/// - 遠距離: HP290 / AD22 / AS0.70 / AR0 / 射程5(設計500)。成長: HP+14 / AD+1.5 / AR+0.5。
/// - MS3.3(設計330)。HPregなし。AS・MS・射程は成長しない。
/// - 敵本拠地方向へ進軍し(進軍方向はMapBuilderのレーン方向に従う。斜め配置対応)、
///   索敵範囲内の敵(ミニオン・ヒーロー・タワー・本拠地)を攻撃する。
///   タワー破壊前の本拠地(無敵)は狙わない。
/// - ARはCharacterStatsを持たないためIIncomingDamageModifierとして自前で適用する。
/// </summary>
public class MinionController : MonoBehaviour, IIncomingDamageModifier
{
    /// <summary>ミニオンの種類。</summary>
    public enum MinionType
    {
        Melee,
        Ranged,
    }

    private static readonly List<MinionController> Active = new List<MinionController>();

    /// <summary>生存中の全ミニオン。タワーのミニオン保護判定などが参照する。</summary>
    public static IReadOnlyList<MinionController> ActiveMinions => Active;

    // GAME_DESIGN.md 5章の基礎値(距離は1:100スケール)。
    private const float MeleeBaseHealth = 420f;
    private const float MeleeBaseDamage = 18f;
    private const float MeleeAttacksPerSecond = 0.85f;
    private const float MeleeBaseArmor = 0f;
    private const float MeleeAttackRange = 1.75f;
    private const float MeleeHealthUp = 20f;
    private const float MeleeDamageUp = 1.5f;
    private const float MeleeArmorUp = 1f;

    private const float RangedBaseHealth = 290f;
    private const float RangedBaseDamage = 22f;
    private const float RangedAttacksPerSecond = 0.70f;
    private const float RangedBaseArmor = 0f;
    private const float RangedAttackRange = 5f;
    private const float RangedHealthUp = 14f;
    private const float RangedDamageUp = 1.5f;
    private const float RangedArmorUp = 0.5f;

    private const float MoveSpeed = 3.3f;
    private const float AggroRange = 7f;
    private const float RetargetInterval = 0.25f;

    private Team _team = Team.Blue;
    private MinionType _type = MinionType.Melee;
    private float _attackDamage;
    private float _attacksPerSecond = 1f;
    private float _armor;
    private float _attackRange = 1.75f;

    private HealthController _health;
    private HealthController _currentTarget;
    private float _attackCooldown;
    private float _retargetTimer;
    private bool _isDead;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>ミニオンの種類。</summary>
    public MinionType Type => _type;

    /// <summary>死亡済みかどうか。</summary>
    public bool IsDead => _isDead;

    /// <summary>ミニオンを生成する(GameManagerのウェーブ出撃から呼び出す)。</summary>
    public static MinionController Spawn(Team team, MinionType type, Vector3 position, int waveLevel, int targetableLayer)
    {
        GameObject minion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        minion.name = $"{team} {type} Minion";

        float scale = type == MinionType.Melee ? 0.65f : 0.5f;
        minion.transform.localScale = new Vector3(scale, scale, scale);
        minion.transform.position = new Vector3(position.x, scale, position.z);
        minion.layer = targetableLayer;

        Renderer renderer = minion.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color color = team.GetTeamColor();
            if (type == MinionType.Ranged)
            {
                color = Color.Lerp(color, Color.white, 0.35f);
            }

            renderer.material.color = color;
        }

        HealthController health = minion.AddComponent<HealthController>();
        TeamMember member = minion.AddComponent<TeamMember>();
        member.SetTeam(team);

        Targetable targetable = minion.AddComponent<Targetable>();
        targetable.InitializeRuntime(TargetClassification.Minion, renderer);

        MinionController controller = minion.AddComponent<MinionController>();
        controller.Initialize(team, type, waveLevel);
        return controller;
    }

    /// <summary>生成直後の初期化。ウェーブ成長(WaveLv)を適用した戦闘値を設定する。</summary>
    public void Initialize(Team team, MinionType type, int waveLevel)
    {
        _team = team;
        _type = type;

        bool isMelee = type == MinionType.Melee;
        float maxHealth = (isMelee ? MeleeBaseHealth : RangedBaseHealth) + (isMelee ? MeleeHealthUp : RangedHealthUp) * waveLevel;
        _attackDamage = (isMelee ? MeleeBaseDamage : RangedBaseDamage) + (isMelee ? MeleeDamageUp : RangedDamageUp) * waveLevel;
        _armor = (isMelee ? MeleeBaseArmor : RangedBaseArmor) + (isMelee ? MeleeArmorUp : RangedArmorUp) * waveLevel;
        _attacksPerSecond = isMelee ? MeleeAttacksPerSecond : RangedAttacksPerSecond;
        _attackRange = isMelee ? MeleeAttackRange : RangedAttackRange;

        _health = GetComponent<HealthController>();
        if (_health != null)
        {
            _health.SetMaxHealth(maxHealth);
            // HealthController.AwakeのキャッシュはこのAddComponentより先に実行済みのため再取得させる。
            _health.RefreshDamageModifiers();
            _health.Died += HandleDied;
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
        }
    }

    private void Update()
    {
        if (_isDead || _health == null || _health.IsDead)
        {
            return;
        }

        _attackCooldown -= Time.deltaTime;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = RetargetInterval;
            _currentTarget = AcquireTarget();
        }

        if (_currentTarget == null || _currentTarget.IsDead)
        {
            _currentTarget = null;
            MoveForward();
            return;
        }

        Vector3 closest = GetClosestPoint(_currentTarget, transform.position);
        Vector3 toTarget = closest - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > _attackRange)
        {
            Move(toTarget);
            return;
        }

        if (_attackCooldown <= 0f)
        {
            AttackCurrentTarget();
            _attackCooldown = 1f / Mathf.Max(0.05f, _attacksPerSecond);
        }
    }

    // 索敵範囲内で最も近い敵(ミニオン・ヒーロー・タワー・攻撃可能な本拠地)を探す。
    private HealthController AcquireTarget()
    {
        HealthController best = null;
        float bestDistance = float.MaxValue;

        foreach (TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (member.Team == _team)
            {
                continue;
            }

            HealthController health = member.GetComponent<HealthController>();
            if (health == null || health.IsDead)
            {
                continue;
            }

            // タワー破壊前の本拠地(無敵)は狙わない。
            NexusController nexus = member.GetComponent<NexusController>();
            if (nexus != null && nexus.IsInvulnerable)
            {
                continue;
            }

            Vector3 closest = GetClosestPoint(health, transform.position);
            float distance = Vector3.Distance(transform.position, closest);
            if (distance > AggroRange || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = health;
        }

        return best;
    }

    private void AttackCurrentTarget()
    {
        float dealt = _currentTarget.TakeDamage(_attackDamage, transform, DamageType.Normal);
        if (dealt > 0f)
        {
            Targetable targetable = _currentTarget.GetComponent<Targetable>();
            if (targetable != null)
            {
                targetable.PlayHitFlash();
            }
        }
    }

    // 目標がいない間は敵本拠地方向へ進軍する。進軍方向はMapBuilderのレーン方向に従い、
    // レーン中心線へ緩やかに寄せる(斜め配置でもレーン上を進軍する)。
    private void MoveForward()
    {
        Vector3 direction;
        MapBuilder map = MapBuilder.Instance;
        if (map != null)
        {
            direction = map.GetLaneForward(_team) + map.GetLaneCenterPull(transform.position) * 0.15f;
        }
        else
        {
            direction = _team == Team.Blue ? Vector3.right : Vector3.left;
            direction += new Vector3(0f, 0f, -transform.position.z * 0.15f);
        }

        Move(direction);
    }

    private void Move(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.position += direction.normalized * (MoveSpeed * Time.deltaTime);
    }

    /// <summary>受けるダメージの軽減(IIncomingDamageModifier)。ウェーブ成長したARで通常ダメージを軽減する。</summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (context.Type == DamageType.Normal && _armor > 0f)
        {
            currentAmount = currentAmount * 100f / (100f + _armor);
        }

        return currentAmount;
    }

    private static Vector3 GetClosestPoint(Component target, Vector3 from)
    {
        Collider collider = target.GetComponent<Collider>();
        return collider != null && collider.enabled ? collider.ClosestPoint(from) : target.transform.position;
    }

    private void HandleDied()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        // 見た目の非表示はTargetableが行う。少し遅らせてオブジェクトを破棄する。
        Destroy(gameObject, 2f);
    }
}
