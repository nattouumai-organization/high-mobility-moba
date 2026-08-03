using System;
using UnityEngine;

/// <summary>
/// チーム毎の獲得ポイントを管理する静的クラス(フェーズ6)。
/// - 近くでミニオンが死亡: 2ポイント
/// - ラストヒット: 追加3ポイント
/// 通常キル・タワー関連・シャットダウンのポイントは今後のタスクで追加する。
/// </summary>
public static class PointsManager
{
    private static int _bluePoints;
    private static int _redPoints;

    /// <summary>ポイント変化時に (チーム, 新しい合計値) で通知する。</summary>
    public static event Action<Team, int> PointsChanged;

    // ドメインリロード無効設定でも再生開始毎に状態をリセットする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _bluePoints = 0;
        _redPoints = 0;
        PointsChanged = null;
    }

    public static int GetPoints(Team team)
    {
        return team == Team.Blue ? _bluePoints : _redPoints;
    }

    public static void AddPoints(Team team, int amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        if (team == Team.Blue)
        {
            _bluePoints += amount;
        }
        else
        {
            _redPoints += amount;
        }

        int total = GetPoints(team);
        Debug.Log($"[PointsManager] {team} +{amount}pt ({reason}) total={total}");
        PointsChanged?.Invoke(team, total);
    }
}
