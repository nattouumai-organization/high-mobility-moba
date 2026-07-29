using UnityEngine;
using Core;

namespace Minion
{
    public class MinionController : MonoBehaviour
    {
        [SerializeField] private Team _team;
        public Team Team => _team;

        private void Awake()
        {
            var tm = GetComponent<TeamMember>() ?? gameObject.AddComponent<TeamMember>();
            tm.Team = _team;
        }
    }
}
