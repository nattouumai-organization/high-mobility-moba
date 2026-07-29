using UnityEngine;

namespace Core
{
    /// <summary>
    /// どのチームに属するかを保持するコンポーネント。
    /// Tower / Nexus / Player / Minion に Add してチーム判定に使用する。
    /// </summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] private Team _team;
        public Team Team { get => _team; set => _team = value; }
    }
}
