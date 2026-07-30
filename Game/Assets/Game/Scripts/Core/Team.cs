using UnityEngine;

/// <summary>
/// チーム識別子。青(Blue)は左側(X負)、赤(Red)は右側(X正)の陣地を持つ。
/// プレイヤー・ミニオン・タワー・本拠地の敵味方判定に使用する。
/// </summary>
public enum Team
{
    Blue = 0,
    Red = 1,
}

/// <summary>Team用の共通ヘルパー。</summary>
public static class TeamExtensions
{
    /// <summary>相手チームを返す。</summary>
    public static Team Opponent(this Team team)
    {
        return team == Team.Blue ? Team.Red : Team.Blue;
    }

    /// <summary>チームカラー(構造物・ミニオンの見た目用)を返す。</summary>
    public static Color GetTeamColor(this Team team)
    {
        return team == Team.Blue
            ? new Color(0.25f, 0.45f, 1f, 1f)
            : new Color(1f, 0.3f, 0.25f, 1f);
    }
}
