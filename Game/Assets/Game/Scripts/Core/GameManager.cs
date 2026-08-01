using UnityEngine;

/// <summary>
/// マッチ進行の管理(フェーズ5)。ミニオンウェーブの出撃とヒーローのチーム設定を担当する。
/// シーンに無い場合はMapBuilderが自動生成するため、手動でアタッチしなくても動作する。
/// - ウェーブ: 開始15秒後に初回、以陀20秒間隔で両チームに近接3体+遠隔2体を出撃させる。
///   ウェーブレベル = floor((ウェーブ番号-1) / 2) でミニオンが徐々に強化される(GAME_DESIGN.md 3章)。
/// - ヒーロー(PlayerClickMovementを持つオブジェクト)へは開始直後にTeamMember(ブルー)を付与する。
///   タワーはTeamMemberを持つ敵しか索敵しないため、この付与が無いとタワーがヒーローを攻撃しない。
/// - タワー・本拠地の破壊通知を受け取る(勝敗UI・リスタートはフェーズ5タスク7で実装予定)。
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

    /// <summary>本拠地破壊によりマッチが終了したかどうか。</summary>
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

    /// <summary>タワー破壊時にTowerControllerから呼ばれる。</summary>
    public void NotifyTowerDestroyed(Team team)
    {
        Debug.Log($"GameManager: {team}チームのタワーが破壊されました。{team}チームの本拠地が攻撃可能になります。", this);
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

    // PlayerClickMovementを持つヒーローへTeamMember(ブルー)を付与する。
    // タワーの索敵とタワー側の同一チーム判定はTeamMemberを前提にするため、この付与が必須。
    private void EnsureHeroTeamMembers()
    {
        foreach (PlayerClickMovement hero in FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None))
        {
            if (hero.GetComponent<TeamMember>() == null)
            {
                TeamMember member = hero.gameObject.AddComponent<TeamMember>();
                member.SetTeam(Team.Blue);
                Debug.Log($"GameManager: {hero.name}をブルーチームに設定しました。", hero);
            }
        }
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
