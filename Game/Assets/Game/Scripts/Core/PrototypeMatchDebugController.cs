using UnityEngine;

/// <summary>
/// SC_Prototypeでチーム別の勝敗確認を行うための一時デバッグ設定。
/// 旧シーンに残っている未初期化TowerControllerを無効化し、Playerチームとミニオン与ダメージをInspectorから変更できるようにする。
/// </summary>
[DefaultExecutionOrder(-230)]
public sealed class PrototypeMatchDebugController : MonoBehaviour
{
    [Header("Player Team Debug")]
    [Tooltip("有効時、PlayerInputHubを持つPlayerのTeamをDebug Player Teamへ固定します。")]
    [SerializeField] private bool _overridePlayerTeam;
    [SerializeField] private Team _debugPlayerTeam = Team.Blue;

    [Header("Minion Attack Debug")]
    [Tooltip("有効時、ミニオンによる有効な1ヒットのダメージを指定値へ置き換えます。無敵・味方攻撃による0ダメージは維持します。")]
    [SerializeField] private bool _overrideMinionAttackDamage;
    [SerializeField, Min(0f)] private float _minionFinalDamagePerHit = 100f;

    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

    private float _nextRefreshTime;

    public bool OverrideMinionAttackDamage => _overrideMinionAttackDamage;
    public float MinionFinalDamagePerHit => Mathf.Max(0f, _minionFinalDamagePerHit);

    private void Awake()
    {
        DisableLegacyMapTowerControllers();
        RefreshDebugTargets();
    }

    private void Start()
    {
        RefreshDebugTargets();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshInterval);
        RefreshDebugTargets();
    }

    /// <summary>
    /// 旧SC_PrototypeではMap本体に未初期化のTowerControllerが残っている。
    /// これがBlue第1タワーとして静的リストへ登録され、第2タワーを永久に無敵扱いするため無効化する。
    /// MapBuilderが生成した正規タワーは別GameObjectなので対象外。
    /// </summary>
    private static void DisableLegacyMapTowerControllers()
    {
        foreach (MapBuilder map in FindObjectsByType<MapBuilder>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map == null)
            {
                continue;
            }

            foreach (TowerController tower in map.GetComponents<TowerController>())
            {
                if (tower == null || !tower.enabled)
                {
                    continue;
                }

                tower.enabled = false;
                Debug.LogWarning(
                    "PrototypeMatchDebugController: Map本体に残っていた旧TowerControllerを無効化しました。生成された第1・第2タワーには影響しません。",
                    map);
            }
        }
    }

    private void RefreshDebugTargets()
    {
        ApplyPlayerTeamOverride();
        EnsureMinionDamageModifiers();
    }

    private void ApplyPlayerTeamOverride()
    {
        if (!_overridePlayerTeam)
        {
            return;
        }

        foreach (PlayerInputHub player in FindObjectsByType<PlayerInputHub>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player == null)
            {
                continue;
            }

            TeamMember member = player.GetComponent<TeamMember>();
            if (member == null)
            {
                member = player.gameObject.AddComponent<TeamMember>();
            }

            if (member.Team != _debugPlayerTeam)
            {
                member.SetTeam(_debugPlayerTeam);
            }
        }
    }

    private void EnsureMinionDamageModifiers()
    {
        foreach (HealthController health in FindObjectsByType<HealthController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health == null)
            {
                continue;
            }

            MinionAttackDebugDamageModifier modifier =
                health.GetComponent<MinionAttackDebugDamageModifier>();
            bool wasAdded = false;

            if (modifier == null)
            {
                modifier = health.gameObject.AddComponent<MinionAttackDebugDamageModifier>();
                wasAdded = true;
            }

            modifier.Initialize(this);
            if (wasAdded)
            {
                health.RefreshDamageModifiers();
            }
        }
    }
}
