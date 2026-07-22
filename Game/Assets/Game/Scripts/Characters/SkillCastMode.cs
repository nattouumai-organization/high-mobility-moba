/// <summary>
/// スキルの発動方式。各スキルコントローラーのInspectorで個別に切り替えられる。
/// 将来的には設定画面から一括で切り替えられるようにする想定。
/// </summary>
public enum SkillCastMode
{
    /// <summary>ノーマルキャスト: キーを押している間は範囲を表示し、キーを離した瞬間に発動する(既定)。</summary>
    NormalCast = 0,

    /// <summary>クイックキャスト: キーを押した瞬間に発動する。</summary>
    QuickCast = 1,
}

public static class SkillCastModeExtensions
{
    /// <summary>
    /// 現在のフレームで発動判定を行うべきかを返す。
    /// NormalCastはキーを離した瞬間、QuickCastは押した瞬間に発動する。
    /// </summary>
    public static bool IsCastTriggered(this SkillCastMode mode, bool pressedThisFrame, bool releasedThisFrame)
    {
        return mode == SkillCastMode.QuickCast ? pressedThisFrame : releasedThisFrame;
    }
}
