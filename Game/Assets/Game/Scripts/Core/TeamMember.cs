using UnityEngine;

namespace Core
{
    /// <summary>
    /// Lightweight component that marks a GameObject as belonging to a team.
    /// Attach to players (added by PlayerSpawner) and minions.
    /// TowerController / NexusController use this to distinguish friend from foe.
    /// </summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] private Team _team;

        public Team Team
        {
            get => _team;
            set => _team = value;
        }
    }
}
