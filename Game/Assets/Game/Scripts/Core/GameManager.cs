using UnityEngine;

/// <summary>
/// ゲーム全体の状態(進行中/終了)を管理する。
/// NexusController.HandleNexusDeath から OnNexusDestroyed を呼ぶ。
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum MatchState { Playing, Finished }

    public MatchState State { get; private set; } = MatchState.Playing;

    public void OnNexusDestroyed(Core.Team winner)
    {
        if (State == MatchState.Finished) return;
        State = MatchState.Finished;
        Debug.Log(string.Format("[GameManager] Match finished! Winner = {0}", winner));
    }
}
