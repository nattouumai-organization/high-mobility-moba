using UnityEngine;

/// <summary>
/// マッチ進行の管理(フェーズ5)。ミニオンウェーブの出撃とヒーローのチーム設定を担当する。
/// シーンに無い場合はMapBuilderが自動生成するため、手動でアタッチしなくても動作する。
/// - ウェーブ: 開始15秒後に初回、以陀20秒間隔で両チームに近接3体+遠隔2体を出撃させる。
///   ウェーブレベル = floor((ウェーブ番号-1) / 2) でミニオンが徐々に強化される(GAME_DESIGN.md 3章)。
/// - ヒーロー(PlayerClickMovementを持つオブジェクト)へは開始直後にTeamMemberを付与する。
///   チームはレーンのどちら側にいるかで決める(ブルー陣側がブルー・レッド陣側がレッド)。
///   タワーはTeamMemberを持つ敵しか索敵しないため、この付与が無いとタワーがヒーローを攻撃しない。
/// - タワーの破壊通知を受け取り、2本目のタワーが破壊されたチームの負けとしてマッチを終了する
///   (勝敗UI・リスタートはフェーズ5タスク7で実装予定)。
/// - ポイントHUD(PointsHud)を実行時に生成する(フェーズ6)。
/// - キル・シャットダウン報酬を扱うHeroKillRewardsを実行時に生成する(フェーズ6)。
/// - レベル成長を扱うHeroLevelGrowthとスキル強化HUDのSkillUpgradeHudを実行時に生成する(フェーズ7)。
/// </summary>
[DefaultExecutionOrder(-250)]
public class GameManager : MonoBehaviour
{
    private const float HeroCheckInterval = 5f;

    /// <summary>シーン上のGameManager。タワー・本拠地の破壊通知などが参照する。</summary>
    public static GameManager Instance { get; private set; }

    // 初回ウェーブまでの秒数とウェーブ間隔(GAME_DESIGN.md: 初回15秒・以陀20秒)。
    [SerializeField] private float _firstWaveDelay = 15f;
    [SerializeField] private float _waveInterval = 20f;

    // 1ウェーブあたりのミニオン数(近接3体+遠隔2体)。
    [SerializeField] private int _meleePerWave = 3;
    [SerializeField] private int _rangedPerWave = 2;

    private float _nextWaveTime;
    private int _waveNumber;
    private float _heroCheckTimer; // 0のため開始直後の最初のUpdateで実行される。
    private bool _matchEnded;

    /// <summary>2本目のタワー(または本拠地)の破壊によりマッチが終了したかどうか。</summary>
    public bool MatchEnded => _matchEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 重複した場合はこのコンポーネントだけを破棄する(GameObjectは他のコンポーネントがいる可能性があるため残す)。
            Debug.LogWarning("GameManager: 既に別のGameManagerが存在するため、このコンポーネントは破棄します。", this);
            Destroy(this);
            return;
        }

        Instance = this;

        if (GetComponent<PointsHud>() == null)
        {
            gameObject.AddComponent<PointsHud>();
        }

        if (GetComponent<HeroKillRewards>() == null)
        {
            gameObject.AddComponent<HeroKillRewards>();
        }

        if (GetComponent<HeroLevelGrowth>() == null)
        {
            gameObject.AddComponent<HeroLevelGrowth>();
        }

        if (GetComponent<SkillUpgradeHud>() == null)
        {
            gameObject.AddComponent<SkillUpgradeHud>();
        }

        if (GetComponent<RuneApplier>() == null)
        {
            gameObject.AddComponent<RuneApplier>();
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
        // ヒーローへのTeamMember付与(開始直後に1回、以际5秒間隔で再確認)。
        // リスポーンや途中生成されたヒーローにも付与するため定期的に確認する。
        _heroCheckTimer -= Time.deltaTime;
        if (_heroCheckTimer <= 0f)
        {
            _heroCheckTimer = HeroCheckInterval;
            EnsureHeroTeamMembers();
        }

        if (_matchEnded)
        {
            return;
        }

        if (Time.time >= _nextWaveTime)
        {
            if (MapBuilder.Instance == null)
            {
                // マップが無いシーン(キャラクター選択など)では出撃させず、1秒後に再確認する。
                _nextWaveTime = Time.time + 1f;
                return;
            }

            _waveNumber++;
            SpawnWave(_waveNumber);
            _nextWaveTime += _waveInterval;
        }
    }

    /// <summary>タワー破壊時にTowerControllerから呼ばれる。2本目(tier=2)の破壊でそのチームの負けとなる。</summary>
    public void NotifyTowerDestroyed(Team team, int tier = 1)
    {
        if (tier >= 2)
        {
            if (_matchEnded)
            {
                return;
            }

            _matchEnded = true;
            Debug.Log($"GameManager: {team}チームの2本目のタワーが破壊されました。{team.Opponent()}チームの勝利です(勝敗UIはフェーズ5タスク7で実装予定)。", this);
            return;
        }

        Debug.Log($"GameManager: {team}チームの1本目のタワーが破壊されました。{team}チームの2本目のタワーが攻撃可能になります。", this);
    }

    /// <summary>本拠地破壊時にNexusControllerから呼ばれる。</summary>
    public void NotifyNexusDestroyed(Team team)
    {
        if (_matchEnded)
        {
            return;
        }

        _matchEnded = true;
        Debug.Log($"GameManager: {team}チームの本拠地が破壊されました。{team.Opponent()}チームの勝利です(勝敗UIはフェーズ5タスク7で実装予定)。", this);
    }

    // PlayerClickMovementを持つヒーローへTeamMemberを付与する。
    // タワーの索敵とタワー側の同一チーム判定はTeamMemberを前提にするため、この付与が必須。
    // チームはレーンのローカルX座標で決める(ブルー陣側=負がブルー、正がレッド)。
    // 従来の全員ブルー割当だと敵ヒーローが同一チーム扱いになり、キルポイントが発生しない。
    private void EnsureHeroTeamMembers()
    {
        foreach (PlayerClickMovement hero in FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None))
        {
            if (hero.GetComponent<TeamMember>() == null)
            {
                TeamMember member = hero.gameObject.AddComponent<TeamMember>();
                member.SetTeam(ResolveHeroTeam(hero.transform.position));
                Debug.Log($"GameManager: {hero.name}を{member.Team}チームに設定しました。", hero);
            }
        }
    }

    // ヒーローの所属チームを位置から決める。MapBuilderが無いシーン(キャラクター選択など)ではブルー扱い。
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
        int waveLevel = Mathf.Max(0, (waveNumber - 1) / 2);
        SpawnTeamWave(Team.Blue, waveLevel);
        SpawnTeamWave(Team.Red, waveLevel);
        Debug.Log($"GameManager: ウェーブ{waveNumber}を出撃させました(ウェーブレベル{waveLevel}・各チーム 近接{_meleePerWave}体+遠隔{_rangedPerWave}体)。", this);
    }

    // 1チーム分のウェーブを出撃させる。近接は前方に横並び、遠隔は後方に横並びで配置する。
    // 横並びの基準(lateral)はレーン進行方向と直交する水平ベクトル。
    private void SpawnTeamWave(Team team, int waveLevel)
    {
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
