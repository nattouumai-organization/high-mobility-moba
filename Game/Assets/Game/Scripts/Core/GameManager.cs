using UnityEngine;

/// <summary>
/// 試合進行の管理(SC_Prototypeの空オブジェクトへアタッチする)。
/// - ミニオンウェーブの出撃(GAME_DESIGN.md 5章: 近接3体 + 遠距離2体、初回15秒後、間隔20秒)。
///   出撃方向・横並びはMapBuilderのレーン方向(斜め配置対応)に従う。
/// - ウェーブ成長(WaveLv = floor((WaveNumber - 1) / 2))をMinionControllerへ引き渡す。
/// - ヒーローへTeamMemberを自動付与し、タワー・ミニオンの索敵対象にする。
/// - タワー破壊・本拠地破壊の通知を受け取り、勝敗を判定する(勝敗UIはフェーズ5タスク7で実装予定)。
/// DefaultExecutionOrder(-250)により、MapBuilder(-300)の後・通常コンポーネントより先に初期化する。
/// </summary>
[DefaultExecutionOrder(-250)]
public class GameManager : MonoBehaviour
{
    /// <summary>シーン上のGameManager。無い場合はnull。</summary>
    public static GameManager Instance { get; private set; }

    [Header("ミニオンウェーブ(GAME_DESIGN.md 5章)")]
    [SerializeField, Min(0f)] private float _firstWaveDelay = 15f;
    [SerializeField, Min(1f)] private float _waveInterval = 20f;
    [SerializeField, Min(0)] private int _meleePerWave = 3;
    [SerializeField, Min(0)] private int _rangedPerWave = 2;

    private float _nextWaveTime;
    private int _waveNumber;
    private float _heroCheckTimer = 1f;
    private bool _matchEnded;

    /// <summary>勝敗が決まったかどうか。</summary>
    public bool MatchEnded => _matchEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameManager: 複数のGameManagerが存在するため、後から起動したものは破棄します。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        _nextWaveTime = Time.time + _firstWaveDelay;
    }

    private void Update()
    {
        // ヒーローのTeamMember付与を定期確認(スポーン方式に依らず敵味方判定を成立させる)。
        _heroCheckTimer -= Time.deltaTime;
        if (_heroCheckTimer <= 0f)
        {
            _heroCheckTimer = 5f;
            EnsureHeroTeamMembers();
        }

        if (_matchEnded)
        {
            return;
        }

        if (Time.time >= _nextWaveTime)
        {
            _waveNumber++;
            SpawnWave(_waveNumber);
            _nextWaveTime += _waveInterval;
        }
    }

    // ヒーロー(PlayerClickMovementを持つオブジェクト)へTeamMemberを自動付与する。1v1プロトタイプではBlue所属。
    private void EnsureHeroTeamMembers()
    {
        foreach (PlayerClickMovement hero in FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None))
        {
            if (hero.GetComponent<TeamMember>() == null)
            {
                TeamMember member = hero.gameObject.AddComponent<TeamMember>();
                member.SetTeam(Team.Blue);
            }
        }
    }

    private void SpawnWave(int waveNumber)
    {
        MapBuilder map = MapBuilder.Instance;
        if (map == null)
        {
            Debug.LogWarning("GameManager: MapBuilderが無いためミニオンを生成できません。シーンへMapBuilderを追加してください。", this);
            return;
        }

        // 成長式: WaveLv = floor((WaveNumber - 1) / 2)
        int waveLevel = (waveNumber - 1) / 2;
        SpawnTeamWave(Team.Blue, map, waveLevel);
        SpawnTeamWave(Team.Red, map, waveLevel);
        Debug.Log($"GameManager: ウェーブ{waveNumber}(成長Lv{waveLevel})を出撃させました。", this);
    }

    private void SpawnTeamWave(Team team, MapBuilder map, int waveLevel)
    {
        Vector3 basePosition = map.GetMinionSpawnPosition(team);
        Vector3 forward = map.GetLaneForward(team);
        // レーンに対して横方向(斜め配置でも横並びが崩れないようにレーン基準で算出)。
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

    /// <summary>タワー破壊の通知(TowerControllerから)。</summary>
    public void NotifyTowerDestroyed(Team team)
    {
        Debug.Log($"GameManager: {team}チームの1本目のタワーが破壊されました。{team}チームの本拠地が攻撃可能になります。", this);
    }

    /// <summary>本拠地破壊の通知(NexusControllerから)。破壊された側の相手チームが勝利する。</summary>
    public void NotifyNexusDestroyed(Team team)
    {
        if (_matchEnded)
        {
            return;
        }

        _matchEnded = true;
        Team winner = team.Opponent();
        Debug.Log($"GameManager: {team}チームの本拠地が破壊されました。{winner}チームの勝利! (勝敗UIはフェーズ5タスク7で実装予定)", this);
    }
}
