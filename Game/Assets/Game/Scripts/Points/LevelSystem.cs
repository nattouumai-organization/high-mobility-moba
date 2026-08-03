using UnityEngine;

/// <summary>
/// チームポイントからヒーローレベル(Lv1〜Lv6)を算出する静的クラス(フェーズ7)。
/// 閾値はGAME_DESIGN.mdの定義に従う: Lv1:0 / Lv2:40 / Lv3:90 / Lv4:150 / Lv5:225 / Lv6:310。
/// レベルアップに必要なポイントは徐々に増える(+40/+50/+60/+75/+85)。
/// 状態は持たず、常にPointsManagerの現在ポイントから算出する
/// (リセット処理が不要で、RuntimeInitializeOnLoadMethodの初期化順序にも依存しない)。
/// </summary>
public static class LevelSystem
{
    public const int MinLevel = 1;
    public const int MaxLevel = 6;

    // Thresholds[i] = Lv(i+1)に必要な合計ポイント。
    private static readonly int[] Thresholds = { 0, 40, 90, 150, 225, 310 };

    /// <summary>合計ポイントからレベル(1〜6)を返す。</summary>
    public static int GetLevel(int points)
    {
        int level = MinLevel;
        for (int i = 1; i < Thresholds.Length; i++)
        {
            if (points >= Thresholds[i])
            {
                level = i + 1;
            }
        }

        return level;
    }

    /// <summary>チームの現在ポイントからレベルを返す。</summary>
    public static int GetLevelForTeam(Team team)
    {
        return GetLevel(PointsManager.GetPoints(team));
    }

    /// <summary>指定レベルに必要な合計ポイントを返す(範囲外はクランプ)。</summary>
    public static int GetPointsRequiredForLevel(int level)
    {
        int index = Mathf.Clamp(level, MinLevel, MaxLevel) - 1;
        return Thresholds[index];
    }
}
