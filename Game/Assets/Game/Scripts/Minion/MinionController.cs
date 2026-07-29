using UnityEngine;
using Core;

namespace Minion
{
    /// <summary>
    /// Phase 5 stub. Exposes Team via TeamMember and its own property.
    /// Full movement / attack / wave spawning to be added in Phase 6.
    /// </summary>
    public class MinionController : MonoBehaviour
    {
        // TeamMember component is the authoritative source of team.
        // We keep a serialized field here as a convenience for scene setup,
        // synced to TeamMember in Awake.
        [SerializeField] private Team _team;

        public Team Team => _team;

        private void Awake()
        {
            // Ensure TeamMember is in sync so TowerController can find it.
            var tm = GetComponent<TeamMember>() ?? gameObject.AddComponent<TeamMember>();
            tm.Team = _team;
        }
    }
}
