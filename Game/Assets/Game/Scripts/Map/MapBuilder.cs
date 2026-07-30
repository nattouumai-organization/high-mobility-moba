using UnityEngine;

/// <summary>
/// 試合マップを実行時に生成するビルダー(SC_Prototypeの空オブジェクトへアタッチする)。
/// GAME_DESIGN.md 3章のマップ仕様(横8,400 x 縦2,400)を1:100スケール(84 x 24ユニット)で再現する。
/// - 地面Plane(GroundLayer)を生成し、右クリック移動・スキルの地面Raycastが機能するようにする。
/// - 各チームの本拠地(中央からX±33 = 設計のX900/7,500)と、レーン中間の1本目のタワー
///   (中央からX±16 = 設計のX2,600/5,800)を設計どおりの位置へ自動生成する。
///   (以前の修正でタワーが本拠地のすぐ隣へ生成され、1本目のタワーが消えていた問題を修正)
/// - Inspectorへ既存のオブジェクトを割り当てた場合、該当の自動生成はスキップする。
/// - カメラのスクロール範囲(TopDownCameraControllerのクランプ)と、
///   PlayerSpawner / GameManagerが使うスポーン位置を提供する。
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

    [Header("構造物の位置(中央からのX距離)")]
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

    /// <summary>地面のレイヤー番号。</summary>
    public int GroundLayer => _groundLayer;

    /// <summary>タワー・ミニオンなど攻撃対象のレイヤー番号。</summary>
    public int TargetableLayer => _targetableLayer;

    /// <summary>カメラのスクロール範囲(TopDownCameraControllerが参照)。</summary>
    public float CameraMinX => -_mapLength * 0.5f;
    public float CameraMaxX => _mapLength * 0.5f;
    public float CameraMinZ => -_mapWidth * 0.5f - 15f;
    public float CameraMaxZ => _mapWidth * 0.5f + 5f;

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
        return new Vector3(sign * (_nexusOffsetX - 3f), 0f, -2.5f);
    }

    /// <summary>ミニオンウェーブのスポーン位置(本拠地の少し前)。</summary>
    public Vector3 GetMinionSpawnPosition(Team team)
    {
        float sign = team == Team.Blue ? -1f : 1f;
        return new Vector3(sign * (_nexusOffsetX - 3f), 0f, 0f);
    }

    private static int ResolveLayer(string layerName, int fallbackLayerNumber)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : fallbackLayerNumber;
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
        ground.transform.localScale = new Vector3(_mapLength / 10f, 1f, _mapWidth / 10f);
        ground.layer = _groundLayer;
        SetColor(ground, new Color(0.33f, 0.42f, 0.33f, 1f));
        Debug.Log($"MapBuilder: 地面({_mapLength} x {_mapWidth})をレイヤー{_groundLayer}({LayerMask.LayerToName(_groundLayer)})で生成しました。", this);
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
        Vector3 position = new Vector3(sign * _towerOffsetX, 2f, 0f);

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = $"{team} Tower (1st)";
        tower.transform.position = position;
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

        Debug.Log($"MapBuilder: {team}チームの1本目のタワーをX={position.x}へ生成しました。", this);
        return tower;
    }

    private GameObject EnsureNexus(GameObject existing, Team team)
    {
        if (existing != null)
        {
            return existing;
        }

        float sign = team == Team.Blue ? -1f : 1f;
        Vector3 position = new Vector3(sign * _nexusOffsetX, 2f, 0f);

        GameObject nexus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nexus.name = $"{team} Nexus";
        nexus.transform.position = position;
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

        Debug.Log($"MapBuilder: {team}チームの本拠地をX={position.x}へ生成しました。", this);
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
