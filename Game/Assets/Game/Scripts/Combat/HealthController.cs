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
/// 同じGameObject上のIIncomingDamageModifier(ゼルフWの前方ダメージ軽減・ヴォルブラークPの初撃無効化など)がDamageContextを使ってダメージ量を変更できる。
/// 通常ダメージ(Normal)はAR(防御力)で軽減される: FinalDamage = RawDamage × 100 / (100 + AR)。
/// 確定ダメージ(True)はARでは軽減されない分類。IIncomingDamageModifierによる変更はダメージ種別を問わず適用され、
/// 軽減・無効化するかどうかは各コンポーネントが種別を見て判断する(ゼルフWはNormalのみ軽減、ヴォルブラークPは両方を無効化)。
/// 実ダメージ(実際に減ったHP)が発生したときはDamageTakenイベントで(ダメージ情報, 実ダメージ量)を通知する(ヴォルブラークRの反射などが購読)。
/// TakeDamageは反射ダメージかどうか(isReflected)も受け取れ、DamageContext.IsReflectedとして軽減判定(IIncomingDamageModifier)と
/// 被ダメージ通知(DamageTaken)へ引き継ぐ(ヴォルブラークRの反射は反射フラグ付きのダメージを再反射しない)。
/// TakeDamageはダメージ発生源ID(sourceId、既定null)も受け取れ、DamageContext.SourceIdとして軽減判定と被ダメージ通知の両方へ引き継ぐ
/// (1回のスキル発動で発生した多段ヒット・複数対象ダメージは同じIDを共有する。連撃ルーンの1スキル1カウント判定などに使用。phase7-runes-fix4)。
/// HPreg(毎秒自動回復)はCharacterStats.CurrentHealthRegenを毎フレーム参照して回復する(死亡中は回復しない)。
/// シールドは今回実装しない。
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

    /// <summary>
    /// ダメージを実際に受けた(実ダメージ > 0)ときに(ダメージ情報, 実ダメージ量)を通知する。ヴォルブラークRの反射などが購読する。
    /// 死亡処理(Died)より前に通知するため、死亡の瞬間の致死ダメージも通知対象になる。
    /// </summary>
    public event Action<DamageContext, float> DamageTaken;

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

        // HPreg: 毎秒のHP自動回復(GAME_DESIGN.mdゼルフ: 3.5/秒)。死亡中は回復しない。
        // CharacterStatsを持たない対象(TrainingDummyなど)はHPregなし。
        if (!_isDead && _characterStats != null)
        {
            float regen = _characterStats.CurrentHealthRegen;
            if (regen > 0f && _currentHealth < MaxHealth)
            {
                Heal(regen * Time.deltaTime);
            }
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
    /// 攻撃者情報が取得できないダメージとして扱うため、ゼルフWなどの前方判定による軽減は行われない(ARによる軽減は行われる)。
    /// </summary>
    /// <returns>実際に減少したHP量(実ダメージ)。死亡済み・無効な値の場合は0を返す。</returns>
    public float TakeDamage(float damage)
    {
        return TakeDamage(damage, null, DamageType.Normal);
    }

    /// <summary>
    /// ダメージを受けて現在HPを減らす。HPは0未満にならず、0になったら死亡処理を開始する。
    /// HPへ適用する直前に、同じGameObject上のIIncomingDamageModifier(ゼルフWの前方ダメージ軽減など)が
    /// 攻撃者・ダメージ種別を使ってダメージ量を変更できる。
    /// その後、通常ダメージ(Normal)にはAR(防御力)による軽減式 FinalDamage = RawDamage × 100 / (100 + AR) を適用する。
    /// </summary>
    /// <param name="damage">元ダメージ量。</param>
    /// <param name="attacker">攻撃者のTransform。取得できない場合はnull(前方判定による軽減は行われない)。</param>
    /// <param name="damageType">ダメージ種別。既定は通常ダメージ(Normal)。確定ダメージ(True)はARでは軽減されない。</param>
    /// <param name="isReflected">反射によるダメージかどうか。DamageContext.IsReflectedへ引き継がれ、反射ダメージの再反射防止(ヴォルブラークR)に使用する。既定はfalse。</param>
    /// <param name="isBasicAttack">通常攻撃によるダメージかどうか。DamageContext.IsBasicAttackへ引き継がれ、タワー・本拠地の「通常攻撃のみ被弾」判定に使用する。既定はfalse。</param>
    /// <param name="sourceId">ダメージ発生源ID(例: "ZelfW#3")。DamageContext.SourceIdとして軽減判定(IIncomingDamageModifier)と被ダメージ通知(DamageTaken)の両方へ引き継がれる。1回のスキル発動で発生した多段ヒット・複数対象ダメージは同じIDを共有する(連撃ルーンの1スキル1カウント判定などに使用)。既定はnull。</param>
    /// <returns>
    /// 実際に減少したHP量(実ダメージ)。軽減後のダメージが基準で、残りHPを超えた過剰ダメージ分は含まない。
    /// 死亡済み・無効な値の場合は0を返す。
    /// </returns>
    public float TakeDamage(float damage, Transform attacker, DamageType damageType = DamageType.Normal, bool isReflected = false, bool isBasicAttack = false, string sourceId = null)
    {
        if (_isDead || damage <= 0f)
        {
            return 0f;
        }

        // 受けたダメージごとに、HPへ適用する直前の軽減判定(ゼルフWなど)を行う。
        float modifiedDamage = ApplyIncomingDamageModifiers(new DamageContext(attacker, damageType, damage, isReflected, isBasicAttack, sourceId), damage);
        if (modifiedDamage <= 0f)
        {
            return 0f;
        }

        // AR(防御力)による通常ダメージの軽減: FinalDamage = RawDamage × 100 / (100 + AR)。
        // 確定ダメージ(True)は軽減しない。CharacterStatsを持たない対象はAR 0として扱う(軽減なし)。
        if (damageType == DamageType.Normal && _characterStats != null)
        {
            float armor = _characterStats.CurrentArmor;
            if (armor > 0f)
            {
                modifiedDamage = modifiedDamage * 100f / (100f + armor);
            }
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - modifiedDamage);
        float actualDamage = previousHealth - _currentHealth;
        NotifyHealthChanged();

        // 実際にHPが減った場合のみ、被ダメージを通知する(ヴォルブラークRの反射などが購読)。
        // 死亡処理より前に通知するため、死亡の瞬間の致死ダメージも通知対象になる。
        if (actualDamage > 0f)
        {
            DamageTaken?.Invoke(new DamageContext(attacker, damageType, damage, isReflected, isBasicAttack, sourceId), actualDamage);
        }

        if (_currentHealth <= 0f)
        {
            Die();
        }

        return actualDamage;
    }

    /// <summary>
    /// 現在HPを回復する。最大HPは超えない。ゼルフPの与ダメージ回復やHPregなどから呼び出す。
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

    /// <summary>
    /// CharacterStatsを持たないオブジェクト(タワー・本拠地・ミニオンなど)の最大HPを実行時に設定する。
    /// CharacterStatsがある場合はそちらを優先するため何もしない。
    /// </summary>
    /// <param name="maxHealth">新しい最大HP(1未満は1に切り上げ)。</param>
    /// <param name="refillToFull">trueの場合は現在HPを最大HPまで回復させる。falseの場合は現在HPを新しい最大HP以下に切り詰める。</param>
    public void SetMaxHealth(float maxHealth, bool refillToFull = true)
    {
        if (_characterStats != null)
        {
            return;
        }

        _maxHealth = Mathf.Max(1f, maxHealth);
        if (refillToFull)
        {
            _currentHealth = MaxHealth;
        }
        else
        {
            _currentHealth = Mathf.Min(_currentHealth, MaxHealth);
        }

        _lastKnownMaxHealth = MaxHealth;
        NotifyHealthChanged();
    }

    /// <summary>
    /// IIncomingDamageModifierのキャッシュを再取得する。
    /// Awake後にAddComponentでモディファイア(TowerControllerなど)を付与した場合に呼ぶこと。
    /// </summary>
    public void RefreshDamageModifiers()
    {
        _damageModifiers = GetComponents<IIncomingDamageModifier>();
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
