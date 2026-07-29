using UnityEngine;

namespace Core
{
    /// <summary>
    /// Marks a GameObject as belonging to a team.
    /// Attached to players (by PlayerSpawner) and minions (by MinionController).
    /// TowerController / NexusController use GetComponent<TeamMember>() for friend/foe detection.
    /// </summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] private Team _team;
        public Team Team { get => _team; set => _team = value; }
    }
}
