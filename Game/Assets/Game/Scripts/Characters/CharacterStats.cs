using UnityEngine;

/// <summary>
/// キャラクターの基本ステータスを管理する土台となるコンポーネント。
/// 移動速度に加えて、通常攻撃の攻撃速度(毎秒の攻撃回数)と攻撃射程(Unity units)を管理する。
/// 将来的にHP、AD、AR、レベル成長などを追加し、
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
}
