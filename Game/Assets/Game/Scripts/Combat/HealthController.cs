using System;
using UnityEngine;

/// <summary>
/// 現在HPを管理し、被ダメージ・回復・死亡処理の起点となるコンポーネント。
/// CharacterStatsを持つ対象(Player)はCurrent Max Healthを最大HPとして使用し、
/// CharacterStatsを持たない対象(TrainingDummy)はInspectorのMax Healthを使用する。
/// TakeDamage / Healは、実際に適用したダメージ量・回復量(残りHP・最大HPを超えない値)を返し、
/// ダメージを与えた側がゼルフPの与ダメージ回復やダメージ表示に実ダメージ量を使用できるようにする。
/// HPreg・シールド・ARによるダメージ軽減・確定ダメージは今回実装しない。
/// 将来的にTECHNICAL_DESIGN.mdのHealthComponent / DamageSystemへ発展させる想定。
/// </summary>
public class HealthController : MonoBehaviour
{
    // CharacterStatsを持たない対象(TrainingDummyなど)用の最大HP。
    // CharacterStatsを持つ場合は、この値ではなくCurrent Max Healthを使用する。
    [SerializeField] private float _maxHealth = 100f;

    // 現在HP。実行時にAwakeで最大HPへ初期化する。Inspectorでの確認用に表示する。
    [SerializeField] private float _currentHealth = 100f;

    private CharacterStats _characterStats;
    private bool _isDead;

    /// <summary>現在HP。0未満にはならない。</summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>
    /// 最大HP。CharacterStatsがあればCurrent Max Healthを、なければInspectorのMax Healthを返す。
    /// 1未満にはならない。
    /// </summary>
    public float MaxHealth =>
        _characterStats != null ? _characterStats.CurrentMaxHealth : Mathf.Max(1f, _maxHealth);

    /// <summary>死亡済みかどうか。死亡処理は1回しか実行されない。</summary>
    public bool IsDead => _isDead;

    /// <summary>現在HPが変化したときに(現在HP, 最大HP)を通知する。HPバーなどが購読する。</summary>
    public event Action<float, float> HealthChanged;

    /// <summary>現在HPが0になり死亡したときに1回だけ通知する。</summary>
    public event Action Died;

    private void Awake()
    {
        _characterStats = GetComponent<CharacterStats>();
        _currentHealth = MaxHealth;
    }

    private void Start()
    {
        // 購読側(HPバーなど)の初期表示のため、開始時に現在HPを通知する。
        NotifyHealthChanged();
    }

    /// <summary>
    /// ダメージを受けて現在HPを減らす。HPは0未満にならず、0になったら死亡処理を開始する。
    /// ARによる軽減・確定ダメージの区別は今回実装しない。
    /// </summary>
    /// <returns>
    /// 実際に減少したHP量(実ダメージ)。残りHPを超えた過剰ダメージ分は含まない。
    /// 死亡済み・無効な値の場合は0を返す。
    /// </returns>
    public float TakeDamage(float damage)
    {
        if (_isDead || damage <= 0f)
        {
            return 0f;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        float actualDamage = previousHealth - _currentHealth;
        NotifyHealthChanged();

        if (_currentHealth <= 0f)
        {
            Die();
        }

        return actualDamage;
    }

    /// <summary>
    /// 現在HPを回復する。最大HPは超えない。ゼルフPの与ダメージ回復などから呼び出す。
    /// </summary>
    /// <returns>
    /// 実際に増加したHP量(実回復量)。最大HPを超えた過剰回復分は含まない。
    /// 死亡済み・無効な値・最大HPで回復量0の場合は0を返す。
    /// </returns>
    public float Heal(float amount)
    {
        if (_isDead || amount <= 0f)
        {
            return 0f;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        float actualHeal = _currentHealth - previousHealth;

        // 最大HPで実際にはHPが増えなかった場合は、通知もしない。
        if (actualHeal > 0f)
        {
            NotifyHealthChanged();
        }

        return actualHeal;
    }

    private void Die()
    {
        // 同じオブジェクトが死亡処理を複数回実行しないようにする。
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        Died?.Invoke();
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
    }
}
