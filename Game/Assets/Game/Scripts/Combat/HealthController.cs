using System;
using UnityEngine;

/// <summary>
/// 現在HPを管理し、被ダメージ・回復・死亡処理の起点となるコンポーネント。
/// CharacterStatsを持つ対象(Player)はCurrent Max Healthを最大HPとして使用し、
/// CharacterStatsを持たない対象(TrainingDummy)はInspectorのMax Healthを使用する。
/// 最大HPの動的変化(バフ・レベル成長・Inspector変更)を毎フレーム検知し、
/// 増加分は現在HPへ加算(LoL方式)、減少時は現在HPを新しい最大HPへクランプする。
/// TakeDamage / Healは、実際に適用したダメージ量・回復量(残りHP・最大HPを超えない値)を返し、
/// ダメージを与えた側がゼルフPの与ダメージ回復やダメージ表示に実ダメージ量を使用できるようにする。
/// Reviveで死亡状態から現在HPを全快して復活でき、復活イベントで見た目・操作の復元を各コンポーネントへ通知する。
/// TakeDamageは攻撃者Transformとダメージ種別(DamageType.Normal / True)を受け取れ、HPへ適用する直前に
/// 同じGameObject上のIIncomingDamageModifier(ゼルフWの前方ダメージ軽減など)がDamageContextを使ってダメージ量を変更できる。
/// 通常ダメージ(Normal)だけが軽減の対象で、確定ダメージ(True)は軽減不可の分類のみ用意し今回は使用しない。
/// HPreg・シールド・ARによるダメージ軽減は今回実装しない。
/// 将来的にTECHNICAL_DESIGN.mdのHealthComponent / DamageSystemへ発展させる想定。
/// </summary>
public class HealthController : MonoBehaviour
{
    // CharacterStatsを持たない対象(TrainingDummyなど)用の最大HP。
    // CharacterStatsを持つ場合は、この値ではなくCurrent Max Healthを使用する。
    [SerializeField] private float _maxHealth = 100f;

    // 現在HP。実行時にStartで最大HPへ初期化する。Inspectorでの確認用に表示する。
    [SerializeField] private float _currentHealth = 100f;

    private CharacterStats _characterStats;
    private bool _isDead;

    // 最大HPの動的変化を検知するための前回値。
    private float _lastKnownMaxHealth;

    // 同じGameObject上の被ダメージ変更コンポーネント(ゼルフWなど)。Awakeで1回だけ取得する。
    private IIncomingDamageModifier[] _damageModifiers;

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

    /// <summary>復活して現在HPが全快したときに通知する。死亡時に無効化した見た目・操作の復元に使用する。</summary>
    public event Action Revived;

    private void Awake()
    {
        _characterStats = GetComponent<CharacterStats>();
        _damageModifiers = GetComponents<IIncomingDamageModifier>();
    }

    private void Start()
    {
        // CharacterStatsのAwake(CharacterData適用)が先に終わっている保証がないため、
        // 現在HPの初期化はStartで行う(Startは全コンポーネントのAwake後に呼ばれる)。
        _currentHealth = MaxHealth;
        _lastKnownMaxHealth = MaxHealth;

        // 購読側(HPバーなど)の初期表示のため、開始時に現在HPを通知する。
        NotifyHealthChanged();
    }

    private void Update()
    {
        // 最大HPの動的変化(バフ・レベル成長・Inspector変更)を検知して現在HPへ反映する。
        float max = MaxHealth;
        if (!Mathf.Approximately(max, _lastKnownMaxHealth))
        {
            HandleMaxHealthChanged(_lastKnownMaxHealth, max);
            _lastKnownMaxHealth = max;
        }
    }

    // 最大HP変化時: 増加分は現在HPへ加算(LoL方式)、減少時は現在HPを新しい最大HPへクランプする。
    // 死亡中は現在HP(0)を変えず、HPバーの最大値表示だけを更新する。
    private void HandleMaxHealthChanged(float previousMax, float newMax)
    {
        if (!_isDead)
        {
            if (newMax > previousMax)
            {
                _currentHealth += newMax - previousMax;
            }
            else
            {
                _currentHealth = Mathf.Min(_currentHealth, newMax);
            }
        }

        NotifyHealthChanged();
    }

    /// <summary>
    /// ダメージを受けて現在HPを減らす(攻撃者情報なし)。
    /// 攻撃者情報が取得できないダメージとして扱うため、ゼルフWなどの前方判定による軽減は行われない。
    /// </summary>
    /// <returns>実際に減少したHP量(実ダメージ)。死亡済み・無効な値の場合は0を返す。</returns>
    public float TakeDamage(float damage)
    {
        return TakeDamage(damage, null, DamageType.Normal);
    }

    /// <summary>
    /// ダメージを受けて現在HPを減らす。HPは0未満にならず、0になったら死亡処理を開始する。
    /// HPへ適用する直前に、同じGameObject上のIIncomingDamageModifier(ゼルフWの前方ダメージ軽減など)が
    /// 攻撃者・ダメージ種別を使ってダメージ量を変更できる。ARによる軽減は今回実装しない。
    /// </summary>
    /// <param name="damage">元ダメージ量。</param>
    /// <param name="attacker">攻撃者のTransform。取得できない場合はnull(前方判定による軽減は行われない)。</param>
    /// <param name="damageType">ダメージ種別。既定は通常ダメージ(Normal)。確定ダメージ(True)は軽減されない。</param>
    /// <returns>
    /// 実際に減少したHP量(実ダメージ)。軽減後のダメージが基準で、残りHPを超えた過剰ダメージ分は含まない。
    /// 死亡済み・無効な値の場合は0を返す。
    /// </returns>
    public float TakeDamage(float damage, Transform attacker, DamageType damageType = DamageType.Normal)
    {
        if (_isDead || damage <= 0f)
        {
            return 0f;
        }

        // 受けたダメージごとに、HPへ適用する直前の軽減判定(ゼルフWなど)を行う。
        float modifiedDamage = ApplyIncomingDamageModifiers(new DamageContext(attacker, damageType, damage), damage);
        if (modifiedDamage <= 0f)
        {
            return 0f;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - modifiedDamage);
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

    /// <summary>
    /// 死亡状態から復活し、現在HPを最大HPまで全快する。RespawnControllerから呼び出す。
    /// HP変化を通知した後、復活イベントを発行し、死亡時に無効化した見た目・操作を各コンポーネントが復元する。
    /// 死亡していない場合は何もしない。
    /// </summary>
    public void Revive()
    {
        if (!_isDead)
        {
            return;
        }

        _isDead = false;
        _currentHealth = MaxHealth;
        NotifyHealthChanged();
        Revived?.Invoke();
    }

    // HPへ適用する直前のダメージ変更(ゼルフWの前方ダメージ軽減など)を順番に適用する。負の値にはならない。
    private float ApplyIncomingDamageModifiers(DamageContext context, float amount)
    {
        if (_damageModifiers == null)
        {
            return amount;
        }

        foreach (IIncomingDamageModifier modifier in _damageModifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            amount = Mathf.Max(0f, modifier.ModifyIncomingDamage(context, amount));
        }

        return amount;
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
