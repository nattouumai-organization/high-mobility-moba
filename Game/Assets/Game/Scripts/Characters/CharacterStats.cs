using UnityEngine;

/// <summary>
/// キャラクターの基本ステータスを管理する土台となるコンポーネント。
/// 移動速度、通常攻撃の攻撃速度(毎秒の攻撃回数)・攻撃射程(Unity units)に加えて、
/// 最大HPと攻撃力(AD)の基礎値を管理する。現在HPはHealthControllerが管理する。
/// 将来的にAR、レベル成長などを追加し、
/// 基礎値はCharacterData(ScriptableObject)から読み込む想定。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    private const float MinAttackSpeed = 0.01f;

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
}
