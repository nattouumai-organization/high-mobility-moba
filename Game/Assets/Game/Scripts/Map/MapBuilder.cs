using UnityEngine;
using Core;

namespace Map
{
    [DefaultExecutionOrder(-300)]
    public class MapBuilder : MonoBehaviour
    {
        [Header("Map Size")]
        [SerializeField] private float _halfLength = 20f;
        [SerializeField] private float _halfWidth  = 10f;
        [SerializeField] private float _laneWidth  = 3f;

        [Header("Spawn Points")]
        [SerializeField] private Transform _blueSpawn;
        [SerializeField] private Transform _redSpawn;

        [Header("Towers")]
        [SerializeField] private Structures.TowerController _blueTower;
        [SerializeField] private Structures.TowerController _redTower;

        [Header("Nexus")]
        [SerializeField] private Structures.NexusController _blueNexus;
        [SerializeField] private Structures.NexusController _redNexus;

        [Header("Materials")]
        [SerializeField] private Material _laneMaterial;
        [SerializeField] private Material _terrainMaterial;

        public Vector2 BoundsMin => new Vector2(-_halfLength, -_halfWidth);
        public Vector2 BoundsMax => new Vector2( _halfLength,  _halfWidth);

        public Transform               GetSpawnPoint(Team team) => team == Team.Blue ? _blueSpawn : _redSpawn;
        public Structures.TowerController GetTower(Team team)  => team == Team.Blue ? _blueTower : _redTower;
        public Structures.NexusController GetNexus(Team team)  => team == Team.Blue ? _blueNexus : _redNexus;

        private void Awake() { BuildMap(); }

        private void BuildMap()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.localScale = new Vector3(_halfLength * 0.2f, 1f, _halfWidth * 0.2f);
            if (_terrainMaterial != null)
                ground.GetComponent<Renderer>().material = _terrainMaterial;

            var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = "Lane";
            lane.transform.SetParent(transform);
            lane.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            lane.transform.localScale = new Vector3(_halfLength * 2f, 0.02f, _laneWidth);
            Destroy(lane.GetComponent<BoxCollider>());
            if (_laneMaterial != null)
                lane.GetComponent<Renderer>().material = _laneMaterial;

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
    }
}
