using UnityEngine;

/// <summary>
/// 試合マップを実行時に生成するビルダー(SC_Prototypeの空オブジェクトへアタッチする)。
/// GAME_DESIGN.md 3章のマップ仕様(横8,400 x 縦2,400)を1:100スケール(84 x 24ユニット)で再現する。
/// レーンは原点中心の斜め配置(既定-45度 = 左下(青本拠地)から右上(赤本拠地))。
/// - レーン座標(X=レーン方向/Z=幅方向)をLaneRotationでワールド座標へ変換し、
///   地面・本拠地(レーン座標X=±33)・1本目のタワー(レーン座標X=±16)・スポーン位置を配置する。
/// - 地面Plane(GroundLayer)を生成し、右クリック移動・スキルの地面Raycastが機能するようにする。
/// - Inspectorへ既存のオブジェクトを割り当てた場合、該当の自動生成はスキップする。
/// - カメラのスクロール範囲(回転後のマップ境界から算出)と、PlayerSpawner / GameManagerが使う
///   スポーン位置・レーン方向を提供する。
/// DefaultExecutionOrder(-300)により、GameManager(-250)・PlayerSpawner(-200)より先に生成する。
/// </summary>
[DefaultExecutionOrder(-300)]
public class MapBuilder : MonoBehaviour
{
    /// <summary>シーン上のMapBuilder。カメラ・スポナーが参照する。無い場合はnull。</summary>
    public static MapBuilder Instance { get; private set; }

    [Header("マップサイズ(1:100スケール)")]
    [SerializeField, Min(10f)] private float _mapLength = 84f;
    [SerializeField, Min(4f)] private float _mapWidth = 24f;

    [Header("レーンの向き(Y軸回転、度)。0=X軸方向、-45=左下(青)から右上(赤)への斜め")]
    [SerializeField, Range(-90f, 90f)] private float _laneYawDegrees = -45f;

    [Header("構造物の位置(中央からのレーン座標X距離)")]
    [SerializeField, Min(1f)] private float _nexusOffsetX = 33f;
    [SerializeField, Min(1f)] private float _towerOffsetX = 16f;

    [Header("レイヤー")]
    [SerializeField] private string _groundLayerName = "GroundLayer";
    [SerializeField] private string _targetableLayerName = "TargetableLayer";

    [Header("既存オブジェクト(未設定なら自動生成)")]
    [SerializeField] private GameObject _existingGround;
    [SerializeField] private GameObject _blueTower;
    [SerializeField] private GameObject _redTower;
    [SerializeField] private GameObject _blueNexus;
    [SerializeField] private GameObject _redNexus;

    private int _groundLayer = 6;
    private int _targetableLayer = 7;
    private Quaternion _laneRotation = Quaternion.identity;
    private Vector3 _cameraBoundsMin;
    private Vector3 _cameraBoundsMax;

    /// <summary>地面のレイヤー番号。</summary>
    public int GroundLayer => _groundLayer;

    /// <summary>タワー・ミニオンなど攻撃対象のレイヤー番号。</summary>
    public int TargetableLayer => _targetableLayer;

    /// <summary>レーン座標→ワールド座標の回転。</summary>
    public Quaternion LaneRotation => _laneRotation;

    /// <summary>カメラのスクロール範囲(TopDownCameraControllerが参照)。回転後のマップ境界から算出する。</summary>
    public float CameraMinX => _cameraBoundsMin.x;
    public float CameraMaxX => _cameraBoundsMax.x;
    public float CameraMinZ => _cameraBoundsMin.z;
    public float CameraMaxZ => _cameraBoundsMax.z;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MapBuilder: 複数のMapBuilderが存在するため、後から起動したものは無効化します。", this);
            enabled = false;
            return;
        }

        Instance = this;

        _groundLayer = ResolveLayer(_groundLayerName, 6);
        _targetableLayer = ResolveLayer(_targetableLayerName, 7);
        _laneRotation = Quaternion.Euler(0f, _laneYawDegrees, 0f);
        RecalculateCameraBounds();

        BuildGround();
        BuildStructures();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>ヒーローのスポーン位置(本拠地の少し前・レーンやや下)。高さ(Y)は呼び出し側が維持する。</summary>
    public Vector3 GetHeroSpawnPosition(Team team)
    {
        float sign = team == Team.Blue ? -1f : 1f;
        return _laneRotation * new Vector3(sign * (_nexusOffsetX - 3f), 0f, -2.5f);
    }

    /// <summary>ミニオンウェーブのスポーン位置(本拠地の少し前)。</summary>
    public Vector3 GetMinionSpawnPosition(Team team)
    {
        float sign = team == Team.Blue ? -1f : 1f;
        return _laneRotation * new Vector3(sign * (_nexusOffsetX - 3f), 0f, 0f);
    }

    /// <summary>指定チームの進軍方向(敵本拠地へ向かうワールド方向)。</summary>
    public Vector3 GetLaneForward(Team team)
    {
        return _laneRotation * (team == Team.Blue ? Vector3.right : Vector3.left);
    }

    /// <summary>レーン中心線へ戻る方向ベクトル(ミニオンの進軍補正用。中心線上ならゼロ)。</summary>
    public Vector3 GetLaneCenterPull(Vector3 worldPosition)
    {
        Vector3 laneDirection = _laneRotation * Vector3.right;
        Vector3 flat = new Vector3(worldPosition.x, 0f, worldPosition.z);
        Vector3 lateral = flat - laneDirection * Vector3.Dot(flat, laneDirection);
        return -lateral;
    }

    private static int ResolveLayer(string layerName, int fallbackLayerNumber)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : fallbackLayerNumber;
    }

    // 回転後のマップ4隅からカメラのスクロール範囲を求める(俯瞰カメラの引き分としてZに余裕を持たせる)。
    private void RecalculateCameraBounds()
    {
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            float signX = (i & 1) == 0 ? -1f : 1f;
            float signZ = (i & 2) == 0 ? -1f : 1f;
            Vector3 corner = _laneRotation * new Vector3(signX * _mapLength * 0.5f, 0f, signZ * _mapWidth * 0.5f);
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        _cameraBoundsMin = new Vector3(min.x - 3f, 0f, min.z - 15f);
        _cameraBoundsMax = new Vector3(max.x + 3f, 0f, max.z + 5f);
    }

    private void BuildGround()
    {
        if (_existingGround != null)
        {
            _existingGround.layer = _groundLayer;
            Debug.Log("MapBuilder: 既存の地面のレイヤーを設定しました。", this);
            return;
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground (MapBuilder)";
        ground.transform.position = Vector3.zero;
        ground.transform.rotation = _laneRotation;
        ground.transform.localScale = new Vector3(_mapLength / 10f, 1f, _mapWidth / 10f);
        ground.layer = _groundLayer;
        SetColor(ground, new Color(0.33f, 0.42f, 0.33f, 1f));
        Debug.Log($"MapBuilder: 地面({_mapLength} x {_mapWidth}、レーン角度{_laneYawDegrees}度)をレイヤー{_groundLayer}({LayerMask.LayerToName(_groundLayer)})で生成しました。", this);
    }

    private void BuildStructures()
    {
        _blueTower = EnsureTower(_blueTower, Team.Blue);
        _redTower = EnsureTower(_redTower, Team.Red);
        _blueNexus = EnsureNexus(_blueNexus, Team.Blue);
        _redNexus = EnsureNexus(_redNexus, Team.Red);
    }

    private GameObject EnsureTower(GameObject existing, Team team)
    {
        if (existing != null)
        {
            return existing;
        }

        float sign = team == Team.Blue ? -1f : 1f;
        Vector3 position = _laneRotation * new Vector3(sign * _towerOffsetX, 2f, 0f);

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = $"{team} Tower (1st)";
        tower.transform.position = position;
        tower.transform.rotation = _laneRotation;
        tower.transform.localScale = new Vector3(2.4f, 2f, 2.4f);
        tower.layer = _targetableLayer;
        SetColor(tower, team.GetTeamColor());

        HealthController health = tower.AddComponent<HealthController>();
        health.SetMaxHealth(5000f);

        TeamMember member = tower.AddComponent<TeamMember>();
        member.SetTeam(team);

        Targetable targetable = tower.AddComponent<Targetable>();
        targetable.InitializeRuntime(TargetClassification.Tower, tower.GetComponent<Renderer>());

        TowerController controller = tower.AddComponent<TowerController>();
        controller.Initialize(team);

        Debug.Log($"MapBuilder: {team}チームの1本目のタワーを({position.x:F1}, {position.z:F1})へ生成しました。", this);
        return tower;
    }

    private GameObject EnsureNexus(GameObject existing, Team team)
    {
        if (existing != null)
        {
            return existing;
        }

        float sign = team == Team.Blue ? -1f : 1f;
        Vector3 position = _laneRotation * new Vector3(sign * _nexusOffsetX, 2f, 0f);

        GameObject nexus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nexus.name = $"{team} Nexus";
        nexus.transform.position = position;
        nexus.transform.rotation = _laneRotation;
        nexus.transform.localScale = new Vector3(4f, 4f, 4f);
        nexus.layer = _targetableLayer;
        SetColor(nexus, Color.Lerp(team.GetTeamColor(), Color.black, 0.35f));

        HealthController health = nexus.AddComponent<HealthController>();
        health.SetMaxHealth(6000f);

        TeamMember member = nexus.AddComponent<TeamMember>();
        member.SetTeam(team);

        Targetable targetable = nexus.AddComponent<Targetable>();
        targetable.InitializeRuntime(TargetClassification.Tower, nexus.GetComponent<Renderer>());

        NexusController controller = nexus.AddComponent<NexusController>();
        controller.Initialize(team);

        Debug.Log($"MapBuilder: {team}チームの本拠地を({position.x:F1}, {position.z:F1})へ生成しました。", this);
        return nexus;
    }

    private static void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
}
