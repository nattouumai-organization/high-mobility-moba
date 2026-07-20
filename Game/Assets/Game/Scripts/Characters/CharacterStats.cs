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
    // Current Attack Speedの下限。攻撃速度が0以下になることと、攻撃間隔の0除算を防ぐ。
    private const float MinAttackSpeed = 0.01f;

    // 基礎移動速度(毎秒Unity units)。試作マップ用の値であり、
    // GAME_DESIGN.mdの最終MS数値(360など)はまだ使用しない。
    // 将来的にCharacterDataのmsBaseへ置き換える想定。
    [SerializeField] private float _baseMoveSpeed = 6f;

    // 移動速度の一時的な増減分。将来のバフ・デバフ用の土台で、今回は初期値0のまま使用する。
    [SerializeField] private float _bonusMoveSpeed = 0f;

    // 基礎攻撃速度(毎秒の攻撃回数)。試作用の値であり、
    // GAME_DESIGN.mdの最終ASBase数値はまだ使用しない。
    // 将来的にCharacterDataのasBaseへ置き換える想定。
    [SerializeField] private float _baseAttackSpeed = 1f;

    // 攻撃速度の増加率(%)。将来のASUPやバフ用の土台で、今回は初期値0のまま使用する。
    [SerializeField] private float _bonusAttackSpeedPercent = 0f;

    // 基礎攻撃射程(Unity units)。試作マップ用の値であり、
    // GAME_DESIGN.mdの最終AARange数値(200など)はまだ使用しない。
    // 将来的にCharacterDataのaaRangeBaseへ置き換える想定。
    [SerializeField] private float _baseAttackRange = 2f;

    // 基礎最大HP。試作用の値であり、GAME_DESIGN.mdの最終HPBase数値はまだ使用しない。
    // 将来的にCharacterDataのhpBaseとHPUPによるレベル成長へ置き換える想定。
    [SerializeField] private float _baseMaxHealth = 100f;

    // 最大HPの一時的な増減分。将来のルーン・バフ用の土台で、今回は初期値0のまま使用する。
    [SerializeField] private float _bonusMaxHealth = 0f;

    // 基礎攻撃力。試作用の値であり、GAME_DESIGN.mdの最終ADBase数値はまだ使用しない。
    // 将来的にCharacterDataのadBaseとADUPによるレベル成長へ置き換える想定。
    [SerializeField] private float _baseAttackDamage = 20f;

    // 攻撃力の一時的な増減分。将来のルーン・スキル強化用の土台で、今回は初期値0のまま使用する。
    [SerializeField] private float _bonusAttackDamage = 0f;

    /// <summary>
    /// 現在の移動速度。Base Move SpeedとBonus Move Speedの合計値で、0未満にはならない。
    /// 取得のたびに計算するため、Inspector値の変更が即座に反映される。
    /// </summary>
    public float CurrentMoveSpeed => Mathf.Max(0f, _baseMoveSpeed + _bonusMoveSpeed);

    /// <summary>
    /// 現在の攻撃速度(毎秒の攻撃回数)。Base Attack SpeedにBonus Attack Speed Percentを
    /// 掛け率(1 + パーセント ÷ 100)として反映した値で、0以下にはならない。
    /// GAME_DESIGN.mdのAS成長式と同じ掛け算の構造にしてある。
    /// </summary>
    public float CurrentAttackSpeed =>
        Mathf.Max(MinAttackSpeed, _baseAttackSpeed * (1f + _bonusAttackSpeedPercent / 100f));

    /// <summary>攻撃間隔(秒)。1をCurrent Attack Speedで割った値。</summary>
    public float AttackInterval => 1f / CurrentAttackSpeed;

    /// <summary>現在の攻撃射程(Unity units)。0未満にはならない。</summary>
    public float CurrentAttackRange => Mathf.Max(0f, _baseAttackRange);

    /// <summary>現在の最大HP。Base Max HealthとBonus Max Healthの合計値で、1未満にはならない。</summary>
    public float CurrentMaxHealth => Mathf.Max(1f, _baseMaxHealth + _bonusMaxHealth);

    /// <summary>現在の攻撃力。Base Attack DamageとBonus Attack Damageの合計値で、0未満にはならない。</summary>
    public float CurrentAttackDamage => Mathf.Max(0f, _baseAttackDamage + _bonusAttackDamage);
}
