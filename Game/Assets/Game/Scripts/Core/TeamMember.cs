using UnityEngine;

namespace Core
{
    /// <summary>
    /// どのチームに属するかを保持するコンポーネント。
    /// Tower/Nexus/Player/Minion に Add して TeamMember.Team でチーム判定する。
    /// </summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] private Team _team;
        public Team Team { get => _team; set => _team = value; }
    }
}
