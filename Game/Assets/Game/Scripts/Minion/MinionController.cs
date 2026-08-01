using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レーンを進軍するミニオン(GAME_DESIGN.md 3章)。GameManagerがウェーブごとにSpawnで生成する。
/// - 近接: HP420/AD18/AS0.85/射程1.75、遠隔: HP290/AD22/AS0.70/射程5。移動速度3.3。
/// - ウェーブレベルで強化: 近接 HP+20/AD+1.5/AR+1、遠隔 HP+14/AD+1.5/AR+0.5。
/// - 索敵範囲7以内の最も近い敵(TeamMemberを持つ対象)を狙う。無敵状態の本拠地は狙わない。
/// - 敵がいない間はレーン進行方向へ進軍する(レーン中心線への引き寄せ付き)。
/// - 攻撃は通常攻撃扱い(isBasicAttack: true)。タワー・本拠地は通常攻撃のみダメージを受けるため、
///   ミニオンの攻撃は構造物にも有効。
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

    private static readonly List<MinionController> Active = new List<MinionController>();

    /// <summary>生存中のミニオン一覧。タワーのミニオン同伴判定などが参照する。</summary>
    public static IReadOnlyList<MinionController> ActiveMinions => Active;

    private Team _team = Team.Blue;
    private MinionType _type = MinionType.Melee;
    private HealthController _health;
    private float _attackDamage;
    private float _attackInterval;
    private float _attackRange;
    private float _armor;
    private float _attackCooldown;
    private float _retargetTimer;
    private HealthController _currentTarget;
    private Targetable _currentTargetable;

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

        float maxHealth;
        if (type == MinionType.Melee)
        {
            maxHealth = 420f + 20f * waveLevel;
            _attackDamage = 18f + 1.5f * waveLevel;
            _attackInterval = 1f / 0.85f;
            _attackRange = 1.75f;
            _armor = 1f * waveLevel;
        }
        else
        {
            maxHealth = 290f + 14f * waveLevel;
            _attackDamage = 22f + 1.5f * waveLevel;
            _attackInterval = 1f / 0.70f;
            _attackRange = 5f;
            _armor = 0.5f * waveLevel;
        }

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
        if (_health == null || _health.IsDead)
        {
            return;
        }

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

            MoveInDirection(toTarget.normalized);
            return;
        }

        MoveForward();
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

    // 索敵範囲内の最も近い敵を狙う。無敵状態の本拠地は狙わない。
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

        MoveInDirection(direction);
    }

    private void MoveInDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        transform.position += direction * (MoveSpeed * Time.deltaTime);
        FaceDirection(direction);
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

    private void HandleDied()
    {
        // 死体がターゲット選択・索敵に引っかからないようにコライダーを無効化し、少し遅らせて破棄する。
        Collider bodyCollider = GetComponent<Collider>();
        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        Destroy(gameObject, 2f);
    }
}
