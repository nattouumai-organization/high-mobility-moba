using UnityEngine;

/// <summary>
/// 近づいたPlayerへ自動的に攻撃を行う攻撃ダミー用のコンポーネント。
/// Playerの被ダメージ表示(黄)とHP減少をテストするために使用する。
/// 攻撃対象(PlayerのHealthController)が攻撃射程内にいる場合のみ、攻撃間隔ごとに即時ダメージを与え、
/// 実際に与えたダメージ量を受けた側(Player)の頭上に黄色で表示する。
/// 攻撃力・攻撃速度・攻撃射程はInspectorで設定し、C#コードへ直接書かない。
/// 自身が死亡している間(復活待ち)、または対象が死亡している間は攻撃しない。
/// 移動・回転・追跡・弾丸・投射物・攻撃アニメーション・敵AIは今回実装しない。
/// </summary>
public class DummyAutoAttack : MonoBehaviour
{
    // 攻撃速度の下限。攻撃間隔の0除算を防ぐ。
    private const float MinAttackSpeed = 0.01f;

    // 攻撃対象のHealthController。SC_PrototypeではPlayerのHealthControllerをInspectorで設定する。
    [SerializeField] private HealthController _targetHealth;

    // 1回の攻撃で与えるダメージ。
    [SerializeField] private float _attackDamage = 10f;

    // 攻撃速度(毎秒の攻撃回数)。攻撃間隔は 1 / Attack Speed。
    [SerializeField] private float _attackSpeed = 1f;

    // 攻撃射程(Unity units)。Playerの通常攻撃と同じく、対象のColliderの最も近い点との水平距離で判定する。
    [SerializeField] private float _attackRange = 2f;

    private HealthController _selfHealth;
    private Collider _targetCollider;
    private float _nextAttackTime;

    private void Awake()
    {
        _selfHealth = GetComponent<HealthController>();

        if (_targetHealth != null)
        {
            // PlayerのCharacterControllerはCollider派生のため、そのまま射程判定に使用できる。
            _targetCollider = _targetHealth.GetComponent<Collider>();
        }
    }

    private void Update()
    {
        if (_targetHealth == null)
        {
            return;
        }

        // 自身が死亡中(復活待ち)、または対象が死亡中は攻撃しない。
        if ((_selfHealth != null && _selfHealth.IsDead) || _targetHealth.IsDead)
        {
            return;
        }

        if (!IsTargetInAttackRange() || Time.time < _nextAttackTime)
        {
            return;
        }

        Attack();
    }

    /// <summary>
    /// 対象が攻撃射程内かどうかを判定する。Playerの通常攻撃(PlayerBasicAttackController)と同じく、
    /// 対象のColliderの自身に最も近い点とのY軸を除いた水平距離(XZ平面)で判定する。
    /// </summary>
    private bool IsTargetInAttackRange()
    {
        Vector3 targetPoint = _targetCollider != null
            ? _targetCollider.ClosestPoint(transform.position)
            : _targetHealth.transform.position;

        Vector3 toTarget = targetPoint - transform.position;
        toTarget.y = 0f;

        return toTarget.magnitude <= _attackRange;
    }

    private void Attack()
    {
        // 即時ダメージを与え、実際に減少させたHP量(実ダメージ)を受け取る(弾丸・投射物は使わない)。
        float actualDamage = _targetHealth.TakeDamage(_attackDamage);

        if (actualDamage > 0f)
        {
            // プレイヤー視点の被ダメージ表示: 受けた側(Player)の頭上に黄色で表示する。
            CombatTextManager.ShowDamageTaken(_targetHealth.transform.position, actualDamage);
        }

        _nextAttackTime = Time.time + 1f / Mathf.Max(MinAttackSpeed, _attackSpeed);
    }
}
