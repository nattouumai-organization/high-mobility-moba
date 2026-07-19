using UnityEngine;

/// <summary>
/// キャラクターの基本ステータスを管理する土台となるコンポーネント。
/// TASKS.md「キャラクターの移動速度ステータスを実装する」用の試作実装。
/// 今回は移動速度のみを管理する。将来的にHP、AD、AS、AR、レベル成長などを追加し、
/// 基礎値はCharacterData(ScriptableObject)から読み込む想定。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    // 基礎移動速度(毎秒Unity units)。試作マップ用の値であり、
    // GAME_DESIGN.mdの最終MS数値(360など)はまだ使用しない。
    // 将来的にCharacterDataのmsBaseへ置き換える想定。
    [SerializeField] private float _baseMoveSpeed = 6f;

    // 移動速度の一時的な増減分。将来のバフ・デバフ用の土台で、今回は初期値0のまま使用する。
    [SerializeField] private float _bonusMoveSpeed = 0f;

    /// <summary>
    /// 現在の移動速度。Base Move SpeedとBonus Move Speedの合計値で、0未満にはならない。
    /// 取得のたびに計算するため、Inspector値の変更が即座に反映される。
    /// </summary>
    public float CurrentMoveSpeed => Mathf.Max(0f, _baseMoveSpeed + _bonusMoveSpeed);
}
