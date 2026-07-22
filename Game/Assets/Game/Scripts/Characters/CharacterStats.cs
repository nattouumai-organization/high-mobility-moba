using UnityEngine;

/// <summary>
/// キャラクターの基本ステータスを管理する土台となるコンポーネント。
/// 移動速度、通常攻撃の攻撃速度(毎秒の攻撃回数)・攻撃射程(Unity units)に加えて、
/// 最大HPと攻撃力(AD)の基礎値を管理する。現在HPはHealthControllerが管理する。
/// Character Data(ScriptableObject)を設定すると、Awakeで基礎値をCharacterDataから読み込む。
/// CharacterDataの数値はステータス単位(MS360・射程200など)であり、
/// Unity単位への換算は本クラスの定数で一元管理する。
/// 未設定の場合は従来どおりInspectorの基礎値を使用する(後方互換)。
/// 将来的にAR、レベル成長などを追加する想定。
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

    // 基礎値の供給元(任意)。設定するとAwakeで下の基礎値を上書きする。
    // PlayerにはZelfData.assetを設定する。未設定の場合はInspectorの基礎値を使用する。
    [SerializeField] private CharacterData _characterData;

    [SerializeField] private float _baseMoveSpeed = 6f;
    // 移動速度の一時的な増減分。ZelfRControllerのスロウ/MS上昇はAddMoveSpeedBonus/RemoveMoveSpeedBonusで操作する。
    [SerializeField] private float _bonusMoveSpeed = 0f;
    [SerializeField] private float _baseAttackSpeed = 1f;
    [SerializeField] private float _bonusAttackSpeedPercent = 0f;
    [SerializeField] private float _baseAttackRange = 2f;
    [SerializeField] private float _baseMaxHealth = 100f;
    [SerializeField] private float _bonusMaxHealth = 0f;
    [SerializeField] private float _baseAttackDamage = 20f;
    [SerializeField] private float _bonusAttackDamage = 0f;

    /// <summary>
    /// 基礎移動速度(毎秒Unity units)。
    /// ZelfRControllerのスロウ・MS上昇計算の基準値として参照する。
    /// </summary>
    public float BaseMoveSpeed => _baseMoveSpeed;

    public float CurrentMoveSpeed => Mathf.Max(0f, _baseMoveSpeed + _bonusMoveSpeed);
    public float CurrentAttackSpeed =>
        Mathf.Max(MinAttackSpeed, _baseAttackSpeed * (1f + _bonusAttackSpeedPercent / 100f));
    public float AttackInterval => 1f / CurrentAttackSpeed;
    public float CurrentAttackRange => Mathf.Max(0f, _baseAttackRange);
    public float CurrentMaxHealth => Mathf.Max(1f, _baseMaxHealth + _bonusMaxHealth);
    public float CurrentAttackDamage => Mathf.Max(0f, _baseAttackDamage + _bonusAttackDamage);

    private void Awake()
    {
        ApplyCharacterData();
    }

    // CharacterData(SO)から基礎値を読み込む。数値の一元管理先はZelfData.assetになる。
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

        Debug.Log(
            $"CharacterStats: CharacterData '{_characterData.DisplayName}' を適用しました。" +
            $"HP={_baseMaxHealth}, AD={_baseAttackDamage}, AS={_baseAttackSpeed}/s, " +
            $"MS={_baseMoveSpeed}units/s, 射程={_baseAttackRange}units",
            this);
    }

    /// <summary>
    /// 移動速度ボーナスを加算する。
    /// 正の値でMS上昇バフ、負の値でスロウとして使用する。
    /// ZelfRControllerのエリア内スロウ・エリア外スロウ・MS上昇バフから呼び出す。
    /// </summary>
    public void AddMoveSpeedBonus(float amount)
    {
        _bonusMoveSpeed += amount;
    }

    /// <summary>
    /// AddMoveSpeedBonusで加算した移動速度ボーナスを解除する。
    /// ZelfRControllerのエリア終了・スロウ期限切れから呼び出す。
    /// </summary>
    public void RemoveMoveSpeedBonus(float amount)
    {
        _bonusMoveSpeed -= amount;
    }

    /// <summary>
    /// 最大HPボーナスを加算する。将来のアイテム・バフ・レベル成長から使用する。
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
}
