/// <summary>
/// フェーズ5: 青/赤の陣営。設計時のTeamType予定名をTeamとして実装した。
/// マップの開始地点のほか、後続のタワー・本拠地・勝敗判定でも共通使用する。
/// </summary>
public enum Team
{
    /// <summary>青陣営(マップ左側。本拠地x=-33)。</summary>
    Blue,

    /// <summary>赤陣営(マップ右側。本拠地x=+33)。</summary>
    Red,
}
