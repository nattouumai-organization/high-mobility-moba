using UnityEngine;

/// <summary>
/// 1レーンマップを実行時に生成する(GAME_DESIGN.md 3章)。シーンに置いた空のGameObjectにアタッチするだけでよい。
/// - レイアウト(ローカルX軸基準): 地面84x24、1本目のタワーX=±16、本拠地X=±33、ヒーロー初期位置(±30, -2.5)、
///   ミニオン出撃位置X=±30。_laneYawDegrees(既定-45度)で全体を回転し、ブルー左下→レッド右上の斜めレーンにする。
/// - 生成物はInspectorのスロットがnullの場合のみCreatePrimitiveで自動生成する(差し替え可能)。
/// - 生成した構造物にはHealthController/TeamMember/Targetable/各Controllerを付与する。
/// - Targetableレイヤーはレイヤー名から実行時に解決する(PlayerTargetSelectorのLayerMaskと合わせる)。
/// - StartでGameManagerの存在を確認し、無ければ自動生成する(ミニオン不出撃の自己修復)。
/// - カメラ用にマップ外周の移動限界(CameraMinXなど)を公開する。
/// </summary>
[DefaultExecutionOrder(-300)]
public class MapBuilder : MonoBehaviour
{
    private const float GroundLength = 84f;
    private const float GroundWidth = 24f;
    private const float TowerLocalX = 16f;
    private const float NexusLocalX = 33f;
    private const float HeroSpawnLocalX = 30f;
    private const float HeroSpawnLocalZ = -2.5f;
    private const float MinionSpawnLocalX = 30f;
    private const float TowerMaxHealth = 5000f;
    private const float NexusMaxHealth = 6000f;
    private const float CameraMarginX = 3f;
    private const float CameraMarginZMin = 15f;
    private const float CameraMarginZMax = 5f;

    /// <summary>シーン上のMapBuilder。カメラ・ミニオン・PlayerSpawnerが参照する。</summary>
    public static MapBuilder Instance { get; private set; }

    // レーン全体の回転角(Y軸)。-45度でブルー左下→レッド右上の斜め配置になる。
    [SerializeField] [Range(-90f, 90f)] private float _laneYawDegrees = -45f;

    // Targetable/Groundレイヤーの名前。PlayerTargetSelectorのLayerMask(Inspector設定)と一致させること。
    [SerializeField] private string _targetableLayerName = "Targetable";
    [SerializeField] private string _groundLayerName = "Ground";

    // nullの場合はCreatePrimitiveで自動生成する。モデル差し替え用のスロット。
    [SerializeField] private GameObject _ground;
    [SerializeField] private GameObject _blueTower;
    [SerializeField] private GameObject _redTower;
    [SerializeField] private GameObject _blueNexus;
    [SerializeField] private GameObject _redNexus;

    private int _targetableLayer = -1;
    private int _groundLayer = -1;

    /// <summary>レーン全体の回転。ローカル座標→ワールド座標の変換に使う。</summary>
    public Quaternion LaneRotation => Quaternion.Euler(0f, _laneYawDegrees, 0f);

    /// <summary>Targetableレイヤー番号。未定義の場合は-1。</summary>
    public int TargetableLayer => _targetableLayer;

    /// <summary>カメラの移動限界(ワールド座標)。</summary>
    public float CameraMinX { get; private set; }
    public float CameraMaxX { get; private set; }
    public float CameraMinZ { get; private set; }
    public float CameraMaxZ { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MapBuilder: 既に別のMapBuilderが存在するため、このコンポーネントは無効化します。", this);
            enabled = false;
            return;
        }

        Instance = this;
        transform.rotation = LaneRotation;
        _targetableLayer = ResolveLayer(_targetableLayerName);
        _groundLayer = ResolveLayer(_groundLayerName);
        BuildMap();
        RecalculateCameraBounds();
    }

    private void Start()
    {
        EnsureGameManager();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // GameManagerがシーンに無い/無効の場合に自動生成・有効化する。
    // ミニオンウェーブとヒーローのチーム設定はGameManagerが担当するため、
    // この自己修復が無いと「ミニオンが出撃しない」「タワーがヒーローを攻撃しない」不具合になる。
    private void EnsureGameManager()
    {
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.isActiveAndEnabled)
            {
                GameManager.Instance.gameObject.SetActive(true);
                GameManager.Instance.enabled = true;
                Debug.LogWarning("MapBuilder: 無効化されていたGameManagerを有効化しました。", GameManager.Instance);
            }

            return;
        }

        GameManager existing = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            Debug.LogWarning("MapBuilder: 非アクティブなGameManagerを有効化しました。", existing);
            return;
        }

        GameObject managerObject = new GameObject("GameManager (Auto)");
        managerObject.AddComponent<GameManager>();
        Debug.LogWarning("MapBuilder: シーンにGameManagerが無かったため自動生成しました。ミニオンウェーブとヒーローのチーム設定が有効になります。", managerObject);
    }

    private int ResolveLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName))
        {
            return -1;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"MapBuilder: レイヤー'{layerName}'がプロジェクトに定義されていません。レイヤー設定をスキップします(Project Settings > Tags and Layersで追加できます)。", this);
            return -1;
        }

        return layer;
    }

    /// <summary>ヒーローの初期位置(ワールド座標)。PlayerSpawnerが参照する。</summary>
    public Vector3 GetHeroSpawnPosition(Team team)
    {
        float sign = team == Team.Blue ? -1f : 1f;
        return LocalToWorld(new Vector3(sign * HeroSpawnLocalX, 0f, HeroSpawnLocalZ));
    }

    /// <summary>ミニオンの出撃位置(ワールド座標)。</summary>
    public Vector3 GetMinionSpawnPosition(Team team)
    {
        float sign = team == Team.Blue ? -1f : 1f;
        return LocalToWorld(new Vector3(sign * MinionSpawnLocalX, 0f, 0f));
    }

    /// <summary>レーンの進行方向(ワールド座標・水平)。ブルーは敵陣地へ向かう+X方向。</summary>
    public Vector3 GetLaneForward(Team team)
    {
        Vector3 forward = LaneRotation * (team == Team.Blue ? Vector3.right : Vector3.left);
        forward.y = 0f;
        return forward.normalized;
    }

    /// <summary>レーン中心線への引き寄せベクトル(ワールド座標)。ミニオンの進軍が使う。</summary>
    public Vector3 GetLaneCenterPull(Vector3 worldPosition)
    {
        Vector3 local = Quaternion.Inverse(LaneRotation) * worldPosition;
        float pull = Mathf.Clamp(-local.z, -1f, 1f);
        Vector3 world = LaneRotation * new Vector3(0f, 0f, pull);
        world.y = 0f;
        return world;
    }

    private Vector3 LocalToWorld(Vector3 local)
    {
        return LaneRotation * local;
    }

    private void BuildMap()
    {
        EnsureGround();
        _blueTower = EnsureTower(_blueTower, Team.Blue, new Vector3(-TowerLocalX, 0f, 0f));
        _redTower = EnsureTower(_redTower, Team.Red, new Vector3(TowerLocalX, 0f, 0f));
        _blueNexus = EnsureNexus(_blueNexus, Team.Blue, new Vector3(-NexusLocalX, 0f, 0f));
        _redNexus = EnsureNexus(_redNexus, Team.Red, new Vector3(NexusLocalX, 0f, 0f));
    }

    private void EnsureGround()
    {
        if (_ground == null)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "Lane Ground";
            // Planeは10x10のためスケールで 84x24 に合わせる。
            _ground.transform.localScale = new Vector3(GroundLength / 10f, 1f, GroundWidth / 10f);
            Renderer groundRenderer = _ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                groundRenderer.material.color = new Color(0.32f, 0.55f, 0.32f);
            }
        }

        _ground.transform.position = Vector3.zero;
        _ground.transform.rotation = LaneRotation;
        if (_groundLayer >= 0 && _groundLayer <= 31)
        {
            _ground.layer = _groundLayer;
        }
    }

    private GameObject EnsureTower(GameObject tower, Team team, Vector3 localPosition)
    {
        if (tower == null)
        {
            tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.name = $"{team} Tower";
            tower.transform.localScale = new Vector3(2.4f, 2f, 2.4f);
        }

        Vector3 world = LocalToWorld(localPosition);
        tower.transform.position = new Vector3(world.x, 2f, world.z);
        ApplyTeamVisual(tower, team);
        SetupStructure(tower, team, TowerMaxHealth, TargetClassification.Tower);

        TowerController controller = tower.GetComponent<TowerController>();
        if (controller == null)
        {
            controller = tower.AddComponent<TowerController>();
        }

        controller.Initialize(team);
        return tower;
    }

    private GameObject EnsureNexus(GameObject nexus, Team team, Vector3 localPosition)
    {
        if (nexus == null)
        {
            nexus = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nexus.name = $"{team} Nexus";
            nexus.transform.localScale = new Vector3(4f, 4f, 4f);
        }

        Vector3 world = LocalToWorld(localPosition);
        nexus.transform.position = new Vector3(world.x, 2f, world.z);
        nexus.transform.rotation = LaneRotation;
        ApplyTeamVisual(nexus, team);
        SetupStructure(nexus, team, NexusMaxHealth, TargetClassification.Tower);

        NexusController controller = nexus.GetComponent<NexusController>();
        if (controller == null)
        {
            controller = nexus.AddComponent<NexusController>();
        }

        controller.Initialize(team);
        return nexus;
    }

    private void ApplyTeamVisual(GameObject structure, Team team)
    {
        Renderer structureRenderer = structure.GetComponent<Renderer>();
        if (structureRenderer != null)
        {
            structureRenderer.material.color = team.GetTeamColor();
        }
    }

    // 構造物へHealthController/TeamMember/Targetableを付与する(既に付いていれば再利用)。
    private void SetupStructure(GameObject structure, Team team, float maxHealth, TargetClassification classification)
    {
        if (_targetableLayer >= 0 && _targetableLayer <= 31)
        {
            structure.layer = _targetableLayer;
        }

        HealthController health = structure.GetComponent<HealthController>();
        if (health == null)
        {
            health = structure.AddComponent<HealthController>();
        }

        health.SetMaxHealth(maxHealth);

        TeamMember member = structure.GetComponent<TeamMember>();
        if (member == null)
        {
            member = structure.AddComponent<TeamMember>();
        }

        member.SetTeam(team);

        Targetable targetable = structure.GetComponent<Targetable>();
        if (targetable == null)
        {
            targetable = structure.AddComponent<Targetable>();
            targetable.InitializeRuntime(classification, structure.GetComponent<Renderer>());
        }
    }

    private void RecalculateCameraBounds()
    {
        // 回転後の地面の4隅をワールド座標へ変換し、外接矩形にマージンを加えてカメラ限界とする。
        Vector3[] corners =
        {
            new Vector3(-GroundLength / 2f, 0f, -GroundWidth / 2f),
            new Vector3(-GroundLength / 2f, 0f, GroundWidth / 2f),
            new Vector3(GroundLength / 2f, 0f, -GroundWidth / 2f),
            new Vector3(GroundLength / 2f, 0f, GroundWidth / 2f),
        };

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        foreach (Vector3 corner in corners)
        {
            Vector3 world = LocalToWorld(corner);
            minX = Mathf.Min(minX, world.x);
            maxX = Mathf.Max(maxX, world.x);
            minZ = Mathf.Min(minZ, world.z);
            maxZ = Mathf.Max(maxZ, world.z);
        }

        CameraMinX = minX - CameraMarginX;
        CameraMaxX = maxX + CameraMarginX;
        CameraMinZ = minZ - CameraMarginZMin;
        CameraMaxZ = maxZ + CameraMarginZMax;
    }
}
