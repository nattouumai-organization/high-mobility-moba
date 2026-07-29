using UnityEngine;
using Core;

namespace Map
{
    /// <summary>
    /// ゲームマップを実行時に構築するコンポーネント。
    ///
    /// ■ 地面レイヤーの自動設定
    ///   生成する地面 Plane のレイヤーを「GroundLayer」へ自動設定する。
    ///   Unity プロジェクトに「GroundLayer」というレイヤーが存在しない場合は
    ///   フォールバック番号(既定値 6)を使用する。
    ///   PlayerClickMovement / ZelfQController などの _groundLayer は
    ///   PlayerLayerMaskFallback が自動補正するため、地面の layer さえ合えば動作する。
    ///
    /// ■ タワー / ネクサスの自動生成
    ///   Inspector で TowerController / NexusController を割り当てていない場合、
    ///   シリンダー(タワー)・キューブ(ネクサス)のプリミティブを自動生成して
    ///   必要なコンポーネントを追加する。
    ///   既に割り当てられている場合は何もしない(Prefab 運用と共存できる)。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class MapBuilder : MonoBehaviour
    {
        // ---- マップサイズ ----
        [Header("Map Size")]
        [SerializeField] private float _halfLength = 20f;
        [SerializeField] private float _halfWidth  = 10f;
        [SerializeField] private float _laneWidth  = 3f;

        // ---- スポーン地点 ----
        [Header("Spawn Points (auto-created if null)")]
        [SerializeField] private Transform _blueSpawn;
        [SerializeField] private Transform _redSpawn;

        // ---- 構造物 (null = 自動生成) ----
        [Header("Towers (auto-created if null)")]
        [SerializeField] private Structures.TowerController _blueTower;
        [SerializeField] private Structures.TowerController _redTower;

        [Header("Nexus (auto-created if null)")]
        [SerializeField] private Structures.NexusController _blueNexus;
        [SerializeField] private Structures.NexusController _redNexus;

        // ---- マテリアル ----
        [Header("Materials (optional)")]
        [SerializeField] private Material _laneMaterial;
        [SerializeField] private Material _terrainMaterial;

        // ---- Ground レイヤー設定 ----
        [Header("Ground Layer")]
        [Tooltip("Unityプロジェクト内の地面レイヤー名。見つからない場合は FallbackIndex を使用する。")]
        [SerializeField] private string _groundLayerName = "GroundLayer";
        [Tooltip("GroundLayer という名前のレイヤーが存在しない場合のフォールバックレイヤー番号。")]
        [SerializeField] private int _groundLayerFallbackIndex = 6;

        // ---- 公開プロパティ ----
        public Vector2 BoundsMin => new Vector2(-_halfLength, -_halfWidth);
        public Vector2 BoundsMax => new Vector2( _halfLength,  _halfWidth);

        public Transform                  GetSpawnPoint(Team team) => team == Team.Blue ? _blueSpawn  : _redSpawn;
        public Structures.TowerController GetTower     (Team team) => team == Team.Blue ? _blueTower  : _redTower;
        public Structures.NexusController GetNexus     (Team team) => team == Team.Blue ? _blueNexus  : _redNexus;

        // ---- 初期化 ----
        private void Awake()
        {
            int groundLayer = ResolveGroundLayer();
            BuildGround(groundLayer);
            BuildLane();
            EnsureSpawnPoints();
            EnsureTower(ref _blueTower, Team.Blue,  new Vector3(-_halfLength + 5f, 0f, 0f));
            EnsureTower(ref _redTower,  Team.Red,   new Vector3( _halfLength - 5f, 0f, 0f));
            EnsureNexus(ref _blueNexus, Team.Blue,  new Vector3(-_halfLength + 2f, 0f, 0f));
            EnsureNexus(ref _redNexus,  Team.Red,   new Vector3( _halfLength - 2f, 0f, 0f));
        }

        // ---- Ground レイヤー解決 ----
        private int ResolveGroundLayer()
        {
            int idx = LayerMask.NameToLayer(_groundLayerName);
            if (idx >= 0)
            {
                Debug.Log(string.Format("[MapBuilder] Ground layer '{0}' = {1}", _groundLayerName, idx));
                return idx;
            }
            Debug.LogWarning(string.Format(
                "[MapBuilder] レイヤー '{0}' が見つかりません。フォールバック番号 {1} を使用します。\n"
                + "Unity Editor の Project Settings > Tags and Layers で '{0}' レイヤーを作成してください。",
                _groundLayerName, _groundLayerFallbackIndex));
            return _groundLayerFallbackIndex;
        }

        // ---- 地面生成 ----
        private void BuildGround(int groundLayer)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(_halfLength * 0.2f, 1f, _halfWidth * 0.2f);

            // ★ 地面のレイヤーを GroundLayer に設定 (PlayerClickMovement の raycast がヒットするために必要)
            ground.layer = groundLayer;

            if (_terrainMaterial != null)
                ground.GetComponent<Renderer>().material = _terrainMaterial;

            Debug.Log(string.Format("[MapBuilder] Ground plane created on layer {0}.", groundLayer));
        }

        // ---- レーン生成 ----
        private void BuildLane()
        {
            var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = "Lane";
            lane.transform.SetParent(transform);
            lane.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            lane.transform.localScale = new Vector3(_halfLength * 2f, 0.02f, _laneWidth);
            Destroy(lane.GetComponent<BoxCollider>());
            if (_laneMaterial != null)
                lane.GetComponent<Renderer>().material = _laneMaterial;
        }

        // ---- スポーン地点 ----
        private void EnsureSpawnPoints()
        {
            if (_blueSpawn == null)
            {
                var go = new GameObject("BlueSpawn");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(-_halfLength + 2f, 0f, 0f);
                _blueSpawn = go.transform;
            }
            if (_redSpawn == null)
            {
                var go = new GameObject("RedSpawn");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(_halfLength - 2f, 0f, 0f);
                _redSpawn = go.transform;
            }
        }

        // ---- タワー自動生成 ----
        private void EnsureTower(
            ref Structures.TowerController field, Team team, Vector3 localPos)
        {
            if (field != null) return;  // Inspector 設定済みなら何もしない

            // シリンダーでタワー外観を作成
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = team + "_Tower";
            go.transform.SetParent(transform);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(1.5f, 2.5f, 1.5f);

            // チームカラー
            go.GetComponent<Renderer>().material.color =
                team == Team.Blue ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);

            // TeamMember
            var tm = go.AddComponent<TeamMember>();
            tm.Team = team;

            // HealthController + Targetable は RequireComponent で TowerController が自動追加する
            var tower = go.AddComponent<Structures.TowerController>();
            tower.Initialize(team);

            field = tower;
            Debug.Log(string.Format("[MapBuilder] {0} Tower auto-created at {1}.", team, localPos));
        }

        // ---- ネクサス自動生成 ----
        private void EnsureNexus(
            ref Structures.NexusController field, Team team, Vector3 localPos)
        {
            if (field != null) return;  // Inspector 設定済みなら何もしない

            // キューブでネクサス外観を作成
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = team + "_Nexus";
            go.transform.SetParent(transform);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(2f, 2f, 2f);

            // チームカラー(タワーより暗め)
            go.GetComponent<Renderer>().material.color =
                team == Team.Blue ? new Color(0.1f, 0.2f, 0.8f) : new Color(0.8f, 0.1f, 0.1f);

            // TeamMember
            var tm = go.AddComponent<TeamMember>();
            tm.Team = team;

            // HealthController + Targetable は RequireComponent で NexusController が自動追加する
            var nexus = go.AddComponent<Structures.NexusController>();

            field = nexus;
            Debug.Log(string.Format("[MapBuilder] {0} Nexus auto-created at {1}.", team, localPos));
        }
    }
}
