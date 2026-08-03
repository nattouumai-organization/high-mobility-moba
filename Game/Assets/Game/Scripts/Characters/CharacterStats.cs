using UnityEngine;

/// <summary>
/// キャラクターの基本ステータスを管理する土台となるコンポーネント。
/// 移動速度、通常攻撃の攻撃速度(毎秒の攻撃回数)・攻撃射程(Unity units)に加えて、
/// 最大HP・攻撃力(AD)・防御力(AR)・HPreg(毎秒自動回復)の基礎値を管理する。現在HPはHealthControllerが管理する。
/// Character Data(ScriptableObject)を設定すると、Awakeで基礎値をCharacterDataから読み込む。
/// CharacterDataの数値はステータス単位(MS360・射程200など)であり、
/// Unity単位への換算は本クラスの定数で一元管理する(AR・HPregはステータス値をそのまま使用する)。
/// 未設定の場合は従来どおりInspectorの基礎値を使用する(後方互換)。
/// レベル成長はフェーズ7で実装: HeroLevelGrowthがCharacterDataの成長値をボーナスAPI経由で加算する。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    private const float MinAttackSpeed = 0.01f;

    /// <summary>
    /// ステータス上の移動速度をUnity units/秒へ換算する除数。
    /// MS 360 = 6 units/秒(現在のゲームプレイ感を基準に確定)。
    /// </summary>
    public const float MoveSpeedStatPerUnityUnit = 60f;

    /// <summary>
    /// ステータス上の射程をUnity unitsへ換算する除数。
    /// 射程200 = 2 units(現在のゲームプレイ感を基準に確定)。
    /// </summary>
    public const float RangeStatPerUnityUnit = 100f;

    /// <summary>
    /// スロウ適用後の移動速度の下限(ステータス値)。LoL準拠で、どれだけスロウを重ねても
    /// 現在MSはこの値(MS110 = 約1.83 units/秒)未満にならない(フェーズ1〜3見直し)。
    /// 基礎MSがこの値より低い対象(移動しない練習用ダミーなど)は、基礎MSがそのまま下限になる。
    /// </summary>
    public const float MinMoveSpeedStat = 110f;

    // 基礎値の供給元(任意)。設定するとAwakeで下の基礎値を上書きする。
    // PlayerにはCharacterDataアセット(ZelfData/VolbraakData)を設定できる。未設定の場合はInspectorの基礎値を使用する。
    // SC_Prototype開始時は、キャラクター選択結果に応じてPlayerCharacterApplierがSetCharacterDataで上書きする(フェーズ4前準備)。
    [SerializeField] private CharacterData _characterData;

    [SerializeField] private float _baseMoveSpeed = 6f;
    // 移動速度の一時的な増減分。ZelfRControllerのMS上昇やCrowdControlControllerのスロウはAddMoveSpeedBonus/RemoveMoveSpeedBonusで操作する。
    [SerializeField] private float _bonusMoveSpeed = 0f;
    [SerializeField] private float _baseAttackSpeed = 1f;
    [SerializeField] private float _bonusAttackSpeedPercent = 0f;
    [SerializeField] private float _baseAttackRange = 2f;
    [SerializeField] private float _baseMaxHealth = 100f;
    [SerializeField] private float _bonusMaxHealth = 0f;
    [SerializeField] private float _baseAttackDamage = 20f;
    [SerializeField] private float _bonusAttackDamage = 0f;
    // 防御力(AR)。通常ダメージの軽減式 FinalDamage = RawDamage × 100 / (100 + AR) に使用する(HealthControllerが参照する)。
    [SerializeField] private float _baseArmor = 0f;
    [SerializeField] private float _bonusArmor = 0f;
    // HPreg(毎秒のHP自動回復量)。HealthControllerが毎フレーム参照して回復する。
    [SerializeField] private float _baseHealthRegen = 0f;
    [SerializeField] private float _bonusHealthRegen = 0f;

    /// <summary>
    /// 基礎移動速度(毎秒Unity units)。
    /// CrowdControlControllerのスロウ・ZelfRControllerのMS上昇計算の基準値として参照する。
    /// </summary>
    public float BaseMoveSpeed => _baseMoveSpeed;

    /// <summary>
    /// 現在適用されているCharacterData(未設定の場合はnull)。
    /// レベル成長(HeroLevelGrowth)が成長値の参照に使用する(フェーズ7)。
    /// </summary>
    public CharacterData Data => _characterData;

    /// <summary>
    /// 現在の移動速度(毎秒Unity units)。スロウを受けても下限(MinMoveSpeedStat)未満にはならない。
    /// </summary>
    public float CurrentMoveSpeed
    {
        get
        {
            float minMoveSpeed = Mathf.Min(Mathf.Max(0f, _baseMoveSpeed), MinMoveSpeedStat / MoveSpeedStatPerUnityUnit);
            return Mathf.Max(minMoveSpeed, _baseMoveSpeed + _bonusMoveSpeed);
        }
    }
    public float CurrentAttackSpeed =>
        Mathf.Max(MinAttackSpeed, _baseAttackSpeed * (1f + _bonusAttackSpeedPercent / 100f));
    public float AttackInterval => 1f / CurrentAttackSpeed;
    public float CurrentAttackRange => Mathf.Max(0f, _baseAttackRange);
    public float CurrentMaxHealth => Mathf.Max(1f, _baseMaxHealth + _bonusMaxHealth);
    public float CurrentAttackDamage => Mathf.Max(0f, _baseAttackDamage + _bonusAttackDamage);

    /// <summary>現在の防御力(AR)。0未満にはならない。HealthControllerの通常ダメージ軽減で使用する。</summary>
    public float CurrentArmor => Mathf.Max(0f, _baseArmor + _bonusArmor);

    /// <summary>現在のHPreg(毎秒のHP自動回復量)。0未満にはならない。</summary>
    public float CurrentHealthRegen => Mathf.Max(0f, _baseHealthRegen + _bonusHealthRegen);

    private void Awake()
    {
        ApplyCharacterData();
    }

    /// <summary>
    /// CharacterDataを差し替えて基礎値を適用し直す(キャラクター選択結果の反映用。フェーズ4前準備)。
    /// SC_Prototype開始時にPlayerCharacterApplierがAwake(DefaultExecutionOrder(-100))から呼び出す。
    /// </summary>
    public void SetCharacterData(CharacterData characterData)
    {
        _characterData = characterData;
        ApplyCharacterData();
    }

    // CharacterData(SO)から基礎値を読み込む。数値の一元管理先はCharacterDataアセット(ZelfData/VolbraakData)になる。
    private void ApplyCharacterData()
    {
        if (_characterData == null)
        {
            // 従来の動作(Inspector基礎値)のまま。TrainingDummyなどSO不要な対象もここを通る。
            return;
        }

        _baseMaxHealth = _characterData.BaseHp;
        _baseAttackDamage = _characterData.BaseAttackDamage;
        _baseAttackSpeed = _characterData.BaseAttackSpeed;
        _baseMoveSpeed = _characterData.BaseMoveSpeed / MoveSpeedStatPerUnityUnit;
        _baseAttackRange = _characterData.BaseAttackRange / RangeStatPerUnityUnit;
        // AR・HPregはステータス値をそのまま使用する(ゼルフ: AR28・HPreg3.5)。
        _baseArmor = _characterData.BaseArmor;
        _baseHealthRegen = _characterData.BaseHpRegeneration;

        Debug.Log(
            $"CharacterStats: CharacterData '{_characterData.DisplayName}' を適用しました。" +
            $"HP={_baseMaxHealth}, AD={_baseAttackDamage}, AS={_baseAttackSpeed}/s, " +
            $"MS={_baseMoveSpeed}units/s, 射程={_baseAttackRange}units, AR={_baseArmor}, HPreg={_baseHealthRegen}/s",
            this);
    }

    /// <summary>
    /// 移動速度ボーナスを加算する。
    /// 正の値でMS上昇バフ、負の値でスロウとして使用する。
    /// スロウはCrowdControlControllerのApplySlow経由で適用され、MS上昇バフは各スキルが直接呼び出す。
    /// %指定のMSバフは、基礎MS(BaseMoveSpeed)基準でフラット量へ換算してから渡す(ゼルフR・共通Dで統一。フェーズ1〜3見直し)。
    /// </summary>
    public void AddMoveSpeedBonus(float amount)
    {
        _bonusMoveSpeed += amount;
    }

    /// <summary>
    /// AddMoveSpeedBonusで加算した移動速度ボーナスを解除する。
    /// </summary>
    public void RemoveMoveSpeedBonus(float amount)
    {
        _bonusMoveSpeed -= amount;
    }

    /// <summary>
    /// 最大HPボーナスを加算する。レベル成長(HeroLevelGrowth)・将来のアイテム・バフから使用する。
    /// 変化はHealthControllerが検知し、増加分は現在HPへも加算される。
    /// </summary>
    public void AddMaxHealthBonus(float amount)
    {
        _bonusMaxHealth += amount;
    }

    /// <summary>AddMaxHealthBonusで加算した最大HPボーナスを解除する。</summary>
    public void RemoveMaxHealthBonus(float amount)
    {
        _bonusMaxHealth -= amount;
    }

    /// <summary>防御力(AR)ボーナスを加算する。レベル成長(HeroLevelGrowth)・将来のバフから使用する。</summary>
    public void AddArmorBonus(float amount)
    {
        _bonusArmor += amount;
    }

    /// <summary>AddArmorBonusで加算した防御力ボーナスを解除する。</summary>
    public void RemoveArmorBonus(float amount)
    {
        _bonusArmor -= amount;
    }

    /// <summary>攻撃力(AD)ボーナスを加算する。レベル成長(HeroLevelGrowth)・将来のバフから使用する(フェーズ7)。</summary>
    public void AddAttackDamageBonus(float amount)
    {
        _bonusAttackDamage += amount;
    }

    /// <summary>AddAttackDamageBonusで加算した攻撃力ボーナスを解除する。</summary>
    public void RemoveAttackDamageBonus(float amount)
    {
        _bonusAttackDamage -= amount;
    }

    /// <summary>
    /// 攻撃速度ボーナス(%)を加算する。CurrentAttackSpeedは基礎値×(1+合計%/100)で計算される。
    /// レベル成長(HeroLevelGrowth)・将来のバフから使用する(フェーズ7)。
    /// </summary>
    public void AddAttackSpeedPercentBonus(float percent)
    {
        _bonusAttackSpeedPercent += percent;
    }

    /// <summary>AddAttackSpeedPercentBonusで加算した攻撃速度ボーナスを解除する。</summary>
    public void RemoveAttackSpeedPercentBonus(float percent)
    {
        _bonusAttackSpeedPercent -= percent;
    }

    /// <summary>HPreg(毎秒回復)ボーナスを加算する。レベル成長(HeroLevelGrowth)・将来のバフから使用する(フェーズ7)。</summary>
    public void AddHealthRegenBonus(float amount)
    {
        _bonusHealthRegen += amount;
    }

    /// <summary>AddHealthRegenBonusで加算したHPregボーナスを解除する。</summary>
    public void RemoveHealthRegenBonus(float amount)
    {
        _bonusHealthRegen -= amount;
    }
}
