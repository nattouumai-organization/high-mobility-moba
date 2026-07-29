using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フェーズ5: 1レーン対称マップと各陣営の開始地点を実行時に生成する。
/// SC_Prototypeの空オブジェクト"Map"へアタッチする(位置は原点推奨。回転は想定しない)。
/// - GAME_DESIGN 3章の座標を100ゲーム単位=1 Unity単位へ換算し、マップ中央(X=4,200 / Y=1,200)を
///   このオブジェクトの位置へ置く(unityX=(X-4200)/100、unityZ=(Y-1200)/100)。
/// - 生成物: 地面84×24(GroundLayer・名前MapGround)、外周の壁、レーン(幅16)上下の横道の壁
///   (開口部x=-18/0/+18・幅4で区切る。左右対称・壁はレーンの外側に置く)、
///   開始地点SpawnPoint_Blue(x=-31)/SpawnPoint_Red(x=+31)(互いの敵陣方向を向く・チーム色マーカー付き)。
/// - レイヤーは名前で解決する。GroundLayerが無ければ従来の6番へフォールバックして警告。
///   壁はWallLayerが定義されていればそのレイヤー、無ければDefaultで生成する
///   (壁のコライダーはCharacterControllerの移動を物理的に遮る。FlashControllerのWall Layerへ
///   設定すればFの壁越え禁止にも使える)。
/// - タワー(x=±16)・本拠地(x=±33)は後続タスクでこのマップ上へ実装する。
/// DefaultExecutionOrder(-300)によりPlayerSpawner(-200)や他コンポーネントより先に生成する。
/// </summary>
[DefaultExecutionOrder(-300)]
public sealed class MapBuilder : MonoBehaviour
{
    [Header("マップ寸法(Unity単位。100ゲーム単位=1)")]
    // X方向の全幅(GAME_DESIGN: 8400)。
    [SerializeField, Min(1f)] private float _mapWidth = 84f;
    // Z方向の全奥行(GAME_DESIGN: 2400)。
    [SerializeField, Min(1f)] private float _mapDepth = 24f;
    // レーン幅(GAME_DESIGN: 1600)。横道の壁はこの外側に置くためレーン幅は削らない。
    [SerializeField, Min(1f)] private float _laneWidth = 16f;

    [Header("壁")]
    [SerializeField, Min(0.5f)] private float _wallHeight = 3f;
    [SerializeField, Min(0.1f)] private float _wallThickness = 1f;

    [Header("横道(レーン上下)の壁")]
    // 横道の壁のX方向の範囲(±)。外周壁との間が左右の通路になる。
    [SerializeField, Min(0f)] private float _sidePathWallExtent = 28f;
    // 開口部(レーン↔横道の出入口)の中心X座標。左右対称にすること。
    [SerializeField] private float[] _sidePathGapCenters = { -18f, 0f, 18f };
    // 開口部の幅。
    [SerializeField, Min(0f)] private float _sidePathGapWidth = 4f;

    [Header("開始地点")]
    // 青本拠地のX座標(GAME_DESIGN: 900)。
    [SerializeField] private float _blueBaseX = -33f;
    // 赤本拠地のX座標(GAME_DESIGN: 7500)。
    [SerializeField] private float _redBaseX = 33f;
    // 本拠地位置から敵陣方向へどれだけ離して開始地点を置くか。
    [SerializeField, Min(0f)] private float _spawnOffsetFromBase = 2f;

    [Header("色")]
    [SerializeField] private Color _groundColor = new Color(0.22f, 0.26f, 0.22f);
    [SerializeField] private Color _wallColor = new Color(0.35f, 0.35f, 0.38f);
    [SerializeField] private Color _blueColor = new Color(0.25f, 0.5f, 1f);
    [SerializeField] private Color _redColor = new Color(1f, 0.35f, 0.3f);

    // 既存プロジェクトのGroundLayer番号(TagManagerで定義済み)。
    private const int FallbackGroundLayer = 6;
    private const float GroundThickness = 0.2f;
    private const float MinSegmentLength = 0.01f;

    private Transform _blueSpawnPoint;
    private Transform _redSpawnPoint;
    private bool _built;

    /// <summary>マップ範囲(XZ)の最小値。TopDownCameraControllerのスクロールクランプなどが使用する。</summary>
    public Vector2 BoundsMin => new Vector2(transform.position.x - _mapWidth * 0.5f, transform.position.z - _mapDepth * 0.5f);

    /// <summary>マップ範囲(XZ)の最大値。</summary>
    public Vector2 BoundsMax => new Vector2(transform.position.x + _mapWidth * 0.5f, transform.position.z + _mapDepth * 0.5f);

    private void Awake()
    {
        Build();
    }

    /// <summary>
    /// 指定陣営の開始地点を返す(位置・向きを持つTransform)。未生成なら先にマップを生成する。
    /// </summary>
    public Transform GetSpawnPoint(Team team)
    {
        Build();
        return team == Team.Blue ? _blueSpawnPoint : _redSpawnPoint;
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        WarnIfLegacyGroundExists();

        int groundLayer = ResolveLayer("GroundLayer", FallbackGroundLayer, true);
        int wallLayer = ResolveLayer("WallLayer", 0, false);

        CreateGround(groundLayer);
        CreateOuterWalls(wallLayer);
        CreateSidePathWalls(wallLayer);
        _blueSpawnPoint = CreateSpawnPoint(Team.Blue);
        _redSpawnPoint = CreateSpawnPoint(Team.Red);

        Debug.Log($"MapBuilder: 1レーン対称マップ({_mapWidth}×{_mapDepth})と各陣営の開始地点を生成しました。", this);
    }

    // 地面。上面がy=0になるように配置する(既存の移動・スキルのGroundレイキャストと整合)。
    private void CreateGround(int layer)
    {
        CreateBox("MapGround", new Vector3(0f, -GroundThickness * 0.5f, 0f), new Vector3(_mapWidth, GroundThickness, _mapDepth), layer, _groundColor);
    }

    // 外周の壁。北・南(±Z)は角を埋めるため壁卲2枚分だけ幅を広げる。
    private void CreateOuterWalls(int layer)
    {
        float halfWidth = _mapWidth * 0.5f;
        float halfDepth = _mapDepth * 0.5f;
        float y = _wallHeight * 0.5f;

        Vector3 horizontalSize = new Vector3(_mapWidth + _wallThickness * 2f, _wallHeight, _wallThickness);
        CreateBox("Wall_North", new Vector3(0f, y, halfDepth + _wallThickness * 0.5f), horizontalSize, layer, _wallColor);
        CreateBox("Wall_South", new Vector3(0f, y, -(halfDepth + _wallThickness * 0.5f)), horizontalSize, layer, _wallColor);

        Vector3 verticalSize = new Vector3(_wallThickness, _wallHeight, _mapDepth);
        CreateBox("Wall_East", new Vector3(halfWidth + _wallThickness * 0.5f, y, 0f), verticalSize, layer, _wallColor);
        CreateBox("Wall_West", new Vector3(-(halfWidth + _wallThickness * 0.5f), y, 0f), verticalSize, layer, _wallColor);
    }

    // レーン上下の横道を区切る壁。開口部で分割したセグメントを±Zへ対称に生成する。
    private void CreateSidePathWalls(int layer)
    {
        float z = _laneWidth * 0.5f + _wallThickness * 0.5f;
        float y = _wallHeight * 0.5f;
        List<Vector2> segments = CalculateWallSegments();

        for (int i = 0; i < segments.Count; i++)
        {
            float centerX = (segments[i].x + segments[i].y) * 0.5f;
            float length = segments[i].y - segments[i].x;
            Vector3 size = new Vector3(length, _wallHeight, _wallThickness);
            CreateBox($"Wall_SidePathNorth_{i}", new Vector3(centerX, y, z), size, layer, _wallColor);
            CreateBox($"Wall_SidePathSouth_{i}", new Vector3(centerX, y, -z), size, layer, _wallColor);
        }
    }

    // 横道壁のX範囲(±_sidePathWallExtent)から開口部を除いたセグメント一覧を返す(x=始点、y=終点)。
    private List<Vector2> CalculateWallSegments()
    {
        var segments = new List<Vector2>();
        float cursor = -_sidePathWallExtent;

        float[] centers = _sidePathGapCenters != null ? (float[])_sidePathGapCenters.Clone() : new float[0];
        System.Array.Sort(centers);

        foreach (float center in centers)
        {
            float gapStart = center - _sidePathGapWidth * 0.5f;
            float gapEnd = center + _sidePathGapWidth * 0.5f;
            if (gapStart > cursor + MinSegmentLength)
            {
                segments.Add(new Vector2(cursor, gapStart));
            }
            cursor = Mathf.Max(cursor, gapEnd);
        }

        if (_sidePathWallExtent > cursor + MinSegmentLength)
        {
            segments.Add(new Vector2(cursor, _sidePathWallExtent));
        }

        return segments;
    }

    // 開始地点。本拠地の少し前(敵陣方向)に置き、敵陣方向を向く。チーム色の薄いマーカー付き。
    private Transform CreateSpawnPoint(Team team)
    {
        bool isBlue = team == Team.Blue;
        float baseX = isBlue ? _blueBaseX : _redBaseX;
        float x = baseX + (isBlue ? _spawnOffsetFromBase : -_spawnOffsetFromBase);
        Vector3 forward = isBlue ? Vector3.right : Vector3.left;

        var spawnPoint = new GameObject(isBlue ? "SpawnPoint_Blue" : "SpawnPoint_Red");
        spawnPoint.transform.SetParent(transform, false);
        spawnPoint.transform.localPosition = new Vector3(x, 0f, 0f);
        spawnPoint.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Marker";
        // マーカーは見た目だけの目印なのでコライダーは削除する(クリック移動・射程判定を邪魔しない)。
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(spawnPoint.transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        marker.transform.localScale = new Vector3(3f, 0.04f, 3f);
        ApplyColor(marker, isBlue ? _blueColor : _redColor);

        return spawnPoint.transform;
    }

    private GameObject CreateBox(string boxName, Vector3 localPosition, Vector3 localScale, int layer, Color color)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = boxName;
        box.layer = layer;
        box.transform.SetParent(transform, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;
        ApplyColor(box, color);
        return box;
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.material.color = color;
        }
    }

    // レイヤーを名前で解決する。未定義ならフォールバック番号を使う。
    private int ResolveLayer(string layerName, int fallbackLayer, bool warnFallback)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            return layer;
        }

        if (warnFallback)
        {
            Debug.LogWarning($"MapBuilder: レイヤー'{layerName}'が未定義のため{fallbackLayer}番を使用します。Tags & Layersで定義してください。", this);
        }
        else
        {
            Debug.Log($"MapBuilder: レイヤー'{layerName}'が未定義のためDefaultで壁を生成します(FlashControllerのWall LayerでFの壁越えを禁止する場合は定義してください)。", this);
        }

        return fallbackLayer;
    }

    // 旧テスト用のGroundが残っているとマップと重なるため、削除推奨の警告を出す。
    private void WarnIfLegacyGroundExists()
    {
        GameObject legacyGround = GameObject.Find("Ground");
        if (legacyGround != null)
        {
            Debug.LogWarning("MapBuilder: 旧テスト用の'Ground'がシーンに残っています。MapBuilderの地面と重なるため削除を推奨します。", legacyGround);
        }
    }
}
