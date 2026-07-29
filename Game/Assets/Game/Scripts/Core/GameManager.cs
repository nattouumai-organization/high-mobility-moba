using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public enum MatchState { Waiting, CharacterSelect, Playing, Finished }

        [SerializeField] private MatchState _state = MatchState.Waiting;
        public MatchState State => _state;

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void StartMatch()
        {
            if (_state == MatchState.Playing) return;
            _state = MatchState.Playing;
            Debug.Log("[GameManager] Match started.");
        }

        /// <summary>Called by NexusController when a nexus is destroyed.</summary>
        /// <param name="winner">The team that destroyed the enemy nexus (winning team).</param>
        public void OnNexusDestroyed(Team winner)
        {
            if (_state == MatchState.Finished) return;
            _state = MatchState.Finished;
            Team loser = winner == Team.Blue ? Team.Red : Team.Blue;
            Debug.Log(string.Format(
                "[GameManager] Match over!  Winner: {0}   Loser: {1}", winner, loser));
            // TODO Phase 5 Task 7: show result UI / return to lobby.
        }
    }
}
