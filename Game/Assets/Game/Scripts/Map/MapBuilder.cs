using UnityEngine;

/// <summary>
/// フェーズ5: SC_Prototypeの1レーン対称マップ(GAME_DESIGN 3章)を実行時にプリミティブから生成する。
/// 100ゲーム単位=1.0 Unity単位・中央原点で、地面・中央線・横道の壁(開口部付き)・外周の境界壁・
/// 両陣営の開始地点(中央向きの床マーカー付き)・両陣営のタワー(TowerController)を組み立てる。
/// - GetSpawnPoint(Team): 陣営の開始地点Transform(PlayerSpawnerが使用)。
/// - GetTower(Team): 陣営のTowerController。
/// - BoundsMin / BoundsMax: マップ範囲(TopDownCameraControllerのスクロールクランプが使用)。
/// DefaultExecutionOrder(-300)により、PlayerSpawner(-200)やPlayerを自動検出する他コンポーネントより先に生成する。
/// 旧Ground(手動配置のPlane)がシーンへ残っている場合は警告を出す(重なるため手動で削除する)。
/// タワーはCapsuleCollider→TowerController→HealthController→Targetableの順で構成する
/// (HealthControllerはAwakeでIIncomingDamageModifierをキャッシュするため、TowerControllerを先に追加する)。
/// </summary>
[DefaultExecutionOrder(-300)]
public sealed class MapBuilder : MonoBehaviour
{
    [Header("マップ寸法 (100ゲーム単位 = 1.0 Unity単位)")]
    [Tooltip("地面のX方向の半分の長さ(GAME_DESIGN: 全長7000 = 70.0)")]
    [SerializeField, Min(1f)] private float _halfLength = 35f;

    [Tooltip("地面のZ方向の半分の幅(GAME_DESIGN: 全幅2000 = 20.0)")]
    [SerializeField, Min(1f)] private float _halfWidth = 10f;

    [Header("横道の壁")]
    [Tooltip("横道の壁のZ位置(±)")]
    [SerializeField] private float _laneWallZ = 8.5f;

    [Tooltip("横道の壁のX範囲(±)")]
    [SerializeField] private float _laneWallHalfLength = 28f;

    [Tooltip("開口部の中心X位置")]
    [SerializeField] private float[] _openingCenters = { -18f, 0f, 18f };

    [Tooltip("開口部の幅")]
    [SerializeField, Min(0.5f)] private float _openingWidth = 4f;

    [Tooltip("壁の高さ")]
    [SerializeField, Min(0.5f)] private float _wallHeight = 2f;

    [Tooltip("壁の厚さ")]
    [SerializeField, Min(0.1f)] private float _wallThickness = 0.5f;

    [Header("開始地点とタワー")]
    [Tooltip("開始地点のX位置(±。青が-X、赤が+X)")]
    [SerializeField] private float _spawnPointX = 31f;

    [Tooltip("タワーのX位置(±。青が-X、赤が+X)")]
    [SerializeField] private float _towerX = 16f;

    [Header("陣営カラー")]
    [SerializeField] private Color _blueTeamColor = new Color(0.25f, 0.5f, 1f, 1f);
    [SerializeField] private Color _redTeamColor = new Color(1f, 0.35f, 0.3f, 1f);

    private Transform _blueSpawnPoint;
    private Transform _redSpawnPoint;
    private TowerController _blueTower;
    private TowerController _redTower;

    private int _groundLayer;
    private int _wallLayer;
    private int _targetableLayer;

    /// <summary>マップ範囲の最小コーナー(地面の高さ0)。カメラのスクロールクランプが使用する。</summary>
    public Vector3 BoundsMin => new Vector3(-_halfLength, 0f, -_halfWidth);

    /// <summary>マップ範囲の最大コーナー(地面の高さ0)。カメラのスクロールクランプが使用する。</summary>
    public Vector3 BoundsMax => new Vector3(_halfLength, 0f, _halfWidth);

    /// <summary>陣営の開始地点。PlayerSpawnerが生成位置・向きに使用する。生成前はnull。</summary>
    public Transform GetSpawnPoint(Team team)
    {
        return team == Team.Blue ? _blueSpawnPoint : _redSpawnPoint;
    }

    /// <summary>陣営のタワー。生成前はnull。</summary>
    public TowerController GetTower(Team team)
    {
        return team == Team.Blue ? _blueTower : _redTower;
    }

    private void Awake()
    {
        _groundLayer = ResolveLayer("GroundLayer", 6);
        _targetableLayer = ResolveLayer("TargetableLayer", 7);

        // 壁は将来のF(壁越え禁止)判定用にWallLayerを優先し、無ければGroundLayerを使う。
        int wallLayer = LayerMask.NameToLayer("WallLayer");
        _wallLayer = wallLayer >= 0 ? wallLayer : _groundLayer;

        WarnIfLegacyGroundExists();

        BuildGround();
        BuildCenterLine();
        BuildLaneWalls();
        BuildBoundaryWalls();

        _blueSpawnPoint = BuildSpawnPoint(Team.Blue, new Vector3(-_spawnPointX, 0f, 0f), _blueTeamColor);
        _redSpawnPoint = BuildSpawnPoint(Team.Red, new Vector3(_spawnPointX, 0f, 0f), _redTeamColor);

        _blueTower = BuildTower(Team.Blue, new Vector3(-_towerX, 0f, 0f), _blueTeamColor);
        _redTower = BuildTower(Team.Red, new Vector3(_towerX, 0f, 0f), _redTeamColor);

        Debug.Log("MapBuilder: 1レーン対称マップを生成しました(開始地点±" + _spawnPointX + " / タワー±" + _towerX + ")。", this);
    }

    // 地面: 上面が高さ0になるCube。右クリック移動のレイキャスト対象(GroundLayer)。
    private void BuildGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "MapGround";
        ground.transform.SetParent(transform, false);
        ground.transform.localScale = new Vector3(_halfLength * 2f, 0.2f, _halfWidth * 2f);
        ground.transform.position = new Vector3(0f, -0.1f, 0f);
        ground.layer = _groundLayer;
        SetColor(ground, new Color(0.22f, 0.27f, 0.22f, 1f));
    }

    // 中央線: マップ中央(x=0)の目印。Colliderは持たない。
    private void BuildCenterLine()
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "CenterLine";
        line.transform.SetParent(transform, false);
        line.transform.localScale = new Vector3(0.2f, 0.02f, _halfWidth * 2f);
        line.transform.position = new Vector3(0f, 0.01f, 0f);
        RemoveCollider(line);
        SetColor(line, new Color(0.9f, 0.9f, 0.9f, 1f));
    }

    // 横道の壁: z=±_laneWallZに、開口部(3か所)を空けたセグメントを並べる。
    private void BuildLaneWalls()
    {
        float segmentStart = -_laneWallHalfLength;
        float half = _openingWidth * 0.5f;
        int index = 0;

        foreach (float center in _openingCenters)
        {
            BuildWallSegmentPair(segmentStart, center - half, ref index);
            segmentStart = center + half;
        }
        BuildWallSegmentPair(segmentStart, _laneWallHalfLength, ref index);
    }

    // 同じX範囲の壁セグメントをz=+側と-側の両方へ作る。
    private void BuildWallSegmentPair(float startX, float endX, ref int index)
    {
        if (endX - startX <= 0.01f)
        {
            return;
        }

        foreach (float sign in new[] { 1f, -1f })
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "LaneWall_" + index + (sign > 0f ? "_North" : "_South");
            wall.transform.SetParent(transform, false);
            wall.transform.localScale = new Vector3(endX - startX, _wallHeight, _wallThickness);
            wall.transform.position = new Vector3((startX + endX) * 0.5f, _wallHeight * 0.5f, _laneWallZ * sign);
            wall.layer = _wallLayer;
            SetColor(wall, new Color(0.45f, 0.42f, 0.38f, 1f));
        }

        index++;
    }

    // 外周の境界壁: マップ外へ出られないよう4辺を囲う。
    private void BuildBoundaryWalls()
    {
        float t = _wallThickness;
        BuildBoundaryWall("Boundary_East", new Vector3(_halfLength + t * 0.5f, _wallHeight * 0.5f, 0f), new Vector3(t, _wallHeight, _halfWidth * 2f + t * 2f));
        BuildBoundaryWall("Boundary_West", new Vector3(-_halfLength - t * 0.5f, _wallHeight * 0.5f, 0f), new Vector3(t, _wallHeight, _halfWidth * 2f + t * 2f));
        BuildBoundaryWall("Boundary_North", new Vector3(0f, _wallHeight * 0.5f, _halfWidth + t * 0.5f), new Vector3(_halfLength * 2f, _wallHeight, t));
        BuildBoundaryWall("Boundary_South", new Vector3(0f, _wallHeight * 0.5f, -_halfWidth - t * 0.5f), new Vector3(_halfLength * 2f, _wallHeight, t));
    }

    private void BuildBoundaryWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform, false);
        wall.transform.localScale = scale;
        wall.transform.position = position;
        wall.layer = _wallLayer;
        SetColor(wall, new Color(0.35f, 0.33f, 0.3f, 1f));
    }

    // 開始地点: 中央を向いた空オブジェクト+陣営色の床マーカー(Colliderなし)。
    private Transform BuildSpawnPoint(Team team, Vector3 position, Color teamColor)
    {
        GameObject point = new GameObject("SpawnPoint_" + team);
        point.transform.SetParent(transform, false);
        point.transform.position = position;

        // マップ中央(原点)の方向を向く。PlayerSpawnerが生成時の向きに使用する。
        Vector3 toCenter = -position;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude > 0.0001f)
        {
            point.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Marker";
        marker.transform.SetParent(point.transform, false);
        marker.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
        marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        RemoveCollider(marker);
        SetColor(marker, teamColor);

        return point.transform;
    }

    // タワー: プリミティブ(本体+クリスタル+選択リング)から組み立て、戦闘用コンポーネントを構成する。
    private TowerController BuildTower(Team team, Vector3 position, Color teamColor)
    {
        GameObject tower = new GameObject("Tower_" + team);
        tower.transform.SetParent(transform, false);
        tower.transform.position = position;
        tower.layer = _targetableLayer;

        // 本体(円柱)。子プリミティブのColliderは削除し、クリック判定はルートのCapsuleColliderへ集約する。
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(tower.transform, false);
        body.transform.localScale = new Vector3(1.6f, 1.8f, 1.6f);
        body.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        body.layer = _targetableLayer;
        RemoveCollider(body);
        SetColor(body, new Color(0.5f, 0.5f, 0.55f, 1f));

        // クリスタル(頂部の球)。陣営色にし、破壊時はTowerControllerが暗くする。
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "Crystal";
        crystal.transform.SetParent(tower.transform, false);
        crystal.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        crystal.transform.localPosition = new Vector3(0f, 4.3f, 0f);
        crystal.layer = _targetableLayer;
        RemoveCollider(crystal);
        SetColor(crystal, teamColor);

        // 選択リング(足元)。表示制御と色はTargetableが行う。
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SelectionRing";
        ring.transform.SetParent(tower.transform, false);
        ring.transform.localScale = new Vector3(2.6f, 0.02f, 2.6f);
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        RemoveCollider(ring);

        // クリック選択・攻撃射程判定用のCollider(タワー全体を覆うカプセル)。
        CapsuleCollider towerCollider = tower.AddComponent<CapsuleCollider>();
        towerCollider.center = new Vector3(0f, 2.4f, 0f);
        towerCollider.radius = 0.9f;
        towerCollider.height = 4.8f;

        // HealthControllerはAwakeでIIncomingDamageModifierをキャッシュするため、TowerControllerを先に追加する。
        TowerController controller = tower.AddComponent<TowerController>();
        tower.AddComponent<HealthController>();
        Targetable targetable = tower.AddComponent<Targetable>();

        targetable.InitializeRuntime(
            TargetClassification.Tower,
            ring,
            ring.GetComponent<Renderer>(),
            body.GetComponent<Renderer>());

        controller.Initialize(team, teamColor, crystal.GetComponent<Renderer>());

        return controller;
    }

    // 手動配置の旧Groundが残っているとマップの地面と重なるため、警告を出す(自動削除はしない)。
    private void WarnIfLegacyGroundExists()
    {
        GameObject legacyGround = GameObject.Find("Ground");
        if (legacyGround != null)
        {
            Debug.LogWarning("MapBuilder: シーンに旧Ground '" + legacyGround.name + "' が残っています。マップの地面(MapGround)と重なるため、シーンから削除してください。", legacyGround);
        }
    }

    // レイヤー名からレイヤー番号を取得する。無ければ既定番号(試作の運用: GroundLayer=6 / TargetableLayer=7)を使う。
    private static int ResolveLayer(string layerName, int fallback)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : fallback;
    }

    private static void SetColor(GameObject target, Color color)
    {
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            // 実行時はマテリアルのインスタンスへ色を設定するため、元のマテリアルアセットは変化しない。
            targetRenderer.material.color = color;
        }
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            Destroy(targetCollider);
        }
    }
}
