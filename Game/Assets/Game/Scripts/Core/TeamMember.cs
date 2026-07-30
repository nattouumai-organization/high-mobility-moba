using UnityEngine;

/// <summary>
/// 所属チームを保持するコンポーネント。プレイヤー・ミニオン・タワー・本拠地へ付与し、
/// タワーの索敵やミニオンの敵味方判定が参照する。
/// シーン配置オブジェクトはInspectorで、実行時生成オブジェクトはSetTeamで設定する。
/// </summary>
public class TeamMember : MonoBehaviour
{
    [SerializeField] private Team _team = Team.Blue;

    /// <summary>所属チーム。</summary>
    public Team Team => _team;

    /// <summary>実行時生成オブジェクト用のチーム設定。</summary>
    public void SetTeam(Team team)
    {
        _team = team;
    }
}
