using System;
using UnityEngine;

/// <summary>
/// マッチ進行を管理する。ミニオンウェーブ、ヒーローのチーム設定、正式な勝敗状態を担当する。
/// 正式な勝利条件は第2タワーの破壊だけで、本拠地は勝敗条件に使用しない。
/// </summary>
[DefaultExecutionOrder(-250)]
public class GameManager : MonoBehaviour
{
    private const float HeroCheckInterval = 5f;

    public static GameManager Instance { get; private set; }

    [SerializeField] private float _firstWaveDelay = 15f;
    [SerializeField] private float _waveInterval = 20f;
    [SerializeField] private int _meleePerWave = 3;
    [SerializeField] private int _rangedPerWave = 2;

    private float _nextWaveTime;
    private int _waveNumber;
    private float _heroCheckTimer;
    private bool _matchEnded;

    /// <summary>試合終了済みならtrue。</summary>
    public bool IsMatchEnded => _matchEnded;

    /// <summary>試合終了時の勝利チーム。</summary>
    public Team WinningTeam { get; private set; } = Team.Blue;

    /// <summary>試合終了時の敗北チーム。</summary>
    public Team LosingTeam { get; private set; } = Team.Red;

    /// <summary>第2タワー破壊で1試合につき1回だけ発火する。引数は勝利チーム。</summary>
    public event Action<Team> MatchEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameManager: 既に別のGameManagerが存在するため、このコンポーネントは破棄します。", this);
            Destroy(this);
            return;
        }

        Instance = this;

        AddIfMissing<PointsHud>();
        AddIfMissing<HeroKillRewards>();
        AddIfMissing<HeroLevelGrowth>();
        AddIfMissing<SkillUpgradeHud>();
        AddIfMissing<RuneApplier>();
        AddIfMissing<MatchResultController>();
        AddIfMissing<PrototypeMatchDebugController>();
    }

    private void AddIfMissing<T>() where T : Component
    {
        if (GetComponent<T>() == null)
        {
            gameObject.AddComponent<T>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        _nextWaveTime = Time.time + Mathf.Max(0f, _firstWaveDelay);
        Debug.Log($"GameManager: ウェーブ管理を開始しました(初回{_firstWaveDelay}秒後・以後{_waveInterval}秒間隔)。", this);
    }

    private void Update()
    {
        if (_matchEnded)
        {
            return;
        }

        _heroCheckTimer -= Time.deltaTime;
        if (_heroCheckTimer <= 0f)
        {
            _heroCheckTimer = HeroCheckInterval;
            EnsureHeroTeamMembers();
        }

        if (Time.time < _nextWaveTime)
        {
            return;
        }

        if (MapBuilder.Instance == null)
        {
            _nextWaveTime = Time.time + 1f;
            return;
        }

        _waveNumber++;
        SpawnWave(_waveNumber);
        _nextWaveTime += _waveInterval;
    }

    /// <summary>タワー破壊通知。第1タワーでは終了せず、第2タワーだけで終了する。</summary>
    public void NotifyTowerDestroyed(Team destroyedTowerTeam, int tier = 1)
    {
        if (_matchEnded)
        {
            return;
        }

        if (tier < 2)
        {
            Debug.Log($"GameManager: {destroyedTowerTeam}チームの第1タワーが破壊されました。第2タワーが攻撃可能になります。", this);
            return;
        }

        EndMatch(destroyedTowerTeam.Opponent(), destroyedTowerTeam);
    }

    private void EndMatch(Team winningTeam, Team losingTeam)
    {
        if (_matchEnded)
        {
            return;
        }

        _matchEnded = true;
        WinningTeam = winningTeam;
        LosingTeam = losingTeam;

        Debug.Log($"GameManager: {losingTeam}チームの第2タワーが破壊されました。{winningTeam}チームの勝利です。", this);
        MatchEnded?.Invoke(winningTeam);
    }

    /// <summary>
    /// 旧NexusControllerとの互換入口。本拠地は現在の勝敗条件ではないため、試合終了処理は行わない。
    /// </summary>
    [Obsolete("本拠地は廃止されています。勝敗はNotifyTowerDestroyedの第2タワー破壊だけで確定します。")]
    public void NotifyNexusDestroyed(Team destroyedNexusTeam)
    {
        Debug.LogWarning(
            $"GameManager: 旧本拠地({destroyedNexusTeam})の破壊通知を無視しました。正式な勝利条件は第2タワーの破壊です。",
            this);
    }

    private void EnsureHeroTeamMembers()
    {
        foreach (PlayerClickMovement hero in FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None))
        {
            if (hero.GetComponent<TeamMember>() != null)
            {
                continue;
            }

            TeamMember member = hero.gameObject.AddComponent<TeamMember>();
            member.SetTeam(ResolveHeroTeam(hero.transform.position));
            Debug.Log($"GameManager: {hero.name}を{member.Team}チームに設定しました。", hero);
        }
    }

    private static Team ResolveHeroTeam(Vector3 worldPosition)
    {
        MapBuilder map = MapBuilder.Instance;
        if (map == null)
        {
            return Team.Blue;
        }

        Vector3 local = Quaternion.Inverse(map.LaneRotation) * worldPosition;
        return local.x <= 0f ? Team.Blue : Team.Red;
    }

    private void SpawnWave(int waveNumber)
    {
        if (_matchEnded)
        {
            return;
        }

        int waveLevel = Mathf.Max(0, (waveNumber - 1) / 2);
        SpawnTeamWave(Team.Blue, waveLevel);
        SpawnTeamWave(Team.Red, waveLevel);
        Debug.Log($"GameManager: ウェーブ{waveNumber}を出撃させました(ウェーブレベル{waveLevel}・各チーム 近接{_meleePerWave}体+遠隔{_rangedPerWave}体)。", this);
    }

    private void SpawnTeamWave(Team team, int waveLevel)
    {
        if (_matchEnded)
        {
            return;
        }

        MapBuilder map = MapBuilder.Instance;
        if (map == null)
        {
            return;
        }

        Vector3 basePosition = map.GetMinionSpawnPosition(team);
        Vector3 forward = map.GetLaneForward(team);
        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;

        for (int i = 0; i < _meleePerWave; i++)
        {
            float offset = (i - (_meleePerWave - 1) * 0.5f) * 1.4f;
            Vector3 position = basePosition + forward * 1.2f + lateral * offset;
            MinionController.Spawn(team, MinionController.MinionType.Melee, position, waveLevel, map.TargetableLayer);
        }

        for (int i = 0; i < _rangedPerWave; i++)
        {
            float offset = (i - (_rangedPerWave - 1) * 0.5f) * 1.6f;
            Vector3 position = basePosition - forward * 1.2f + lateral * offset;
            MinionController.Spawn(team, MinionController.MinionType.Ranged, position, waveLevel, map.TargetableLayer);
        }
    }
}
