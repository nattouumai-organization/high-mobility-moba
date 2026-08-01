using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移動経路上の静的障害物(タワー・本拠地)を回避するためのステアリング計算(共有ユーティリティ)。
/// - 障害物をXZ平面上の円(コライダー形状から半径を算出+移動体半径+余白)として扱い、
///   直進経路が円と交差する場合は、目的地への向きから外れる角度が小さい側(=最短側)の接線方向へ進行方向を曲げる。
/// - 障害物一覧はTowerController/NexusControllerから一定間隔で自動収集する(タワー破壊などのシーン変化に追従)。
/// - PlayerClickMovement(ヒーロー)とMinionController(ミニオン)が使用する。
///   ダッシュ系スキル(ゼルフEなど)は高機動アクションの仕様として直進のまま(回避を適用しない)。
/// </summary>
public static class ObstacleAvoidance
{
    private struct Obstacle
    {
        public Transform Transform;
        public float Radius;
    }

    // 障害物一覧の再収集間隔(秒)。
    private const float RefreshInterval = 1f;

    // 障害物との間に確保する余白(m)。
    private const float Margin = 0.15f;

    // 接線方向へ曲げる際に追加する角度(度)。ちょうど接線だと縁を擦るため少し外側へ向ける。
    private const float ExtraTurnDegrees = 4f;

    private static readonly List<Obstacle> Obstacles = new List<Obstacle>();
    private static float _nextRefreshTime;

    /// <summary>
    /// 希望する進行方向を、障害物を避ける方向へ補正して返す(正規化済み)。経路上に障害物が無い場合はそのまま返す。
    /// </summary>
    /// <param name="position">現在位置。</param>
    /// <param name="desiredDirection">希望する進行方向(水平)。</param>
    /// <param name="agentRadius">移動体の半径。</param>
    /// <param name="maxDistance">この距離より先の障害物は無視する(残り移動距離や先読み距離)。</param>
    /// <param name="ignore">障害物として扱わないTransform(攻撃対象の構造物など)。不要ならnull。</param>
    public static Vector3 SteerDirection(Vector3 position, Vector3 desiredDirection, float agentRadius, float maxDistance, Transform ignore)
    {
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        desiredDirection.Normalize();
        RefreshIfNeeded();

        // 経路を塞ぐ最も近い障害物を探す。
        bool found = false;
        Vector3 bestCenter = Vector3.zero;
        float bestBlockRadius = 0f;
        float bestProj = float.MaxValue;

        foreach (Obstacle obstacle in Obstacles)
        {
            if (obstacle.Transform == null || obstacle.Transform == ignore || !obstacle.Transform.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 center = obstacle.Transform.position;
            Vector3 toCenter = center - position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;
            float blockRadius = obstacle.Radius + agentRadius + Margin;

            float proj;
            if (distance <= blockRadius)
            {
                // 既に障害物に接触・食い込んでいる場合は最優先で回避する。
                proj = 0f;
            }
            else
            {
                proj = Vector3.Dot(toCenter, desiredDirection);
                if (proj <= 0f || proj - blockRadius > maxDistance)
                {
                    // 後方にある、または移動距離より遠い。
                    continue;
                }

                float perpSq = distance * distance - proj * proj;
                if (perpSq >= blockRadius * blockRadius)
                {
                    // 直進しても円と交差しない。
                    continue;
                }
            }

            if (proj < bestProj)
            {
                bestProj = proj;
                bestCenter = center;
                bestBlockRadius = blockRadius;
                found = true;
            }
        }

        if (!found)
        {
            return desiredDirection;
        }

        Vector3 to = bestCenter - position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.001f)
        {
            // 中心と完全一致(通常は発生しない)。
            return desiredDirection;
        }

        Vector3 toDir = to / dist;

        // 迂回する側: 希望方向が中心線のどちら側を向いているか(外れる角度が小さい側=最短側)。
        float side = Vector3.Cross(to, desiredDirection).y >= 0f ? 1f : -1f;

        if (dist <= bestBlockRadius)
        {
            // 接触中: 接線方向+外向き成分で滑らかに縁を回り込む。
            Vector3 tangent = Vector3.Cross(Vector3.up, toDir) * side;
            return (tangent - toDir * 0.5f).normalized;
        }

        // 外側から接近中: 中心への向きを接線角+余裕角だけ最短側へ回転した方向へ進む。
        float tangentAngle = Mathf.Asin(Mathf.Clamp01(bestBlockRadius / dist)) * Mathf.Rad2Deg + ExtraTurnDegrees;
        return (Quaternion.Euler(0f, side * tangentAngle, 0f) * toDir).normalized;
    }

    /// <summary>
    /// 目的地が障害物の内側にある場合、移動体が到達できる障害物の縁の位置へずらして返す。
    /// </summary>
    public static Vector3 ClampDestination(Vector3 position, Vector3 destination, float agentRadius, Transform ignore)
    {
        RefreshIfNeeded();

        foreach (Obstacle obstacle in Obstacles)
        {
            if (obstacle.Transform == null || obstacle.Transform == ignore || !obstacle.Transform.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 center = obstacle.Transform.position;
            Vector3 fromCenter = destination - center;
            fromCenter.y = 0f;
            float blockRadius = obstacle.Radius + agentRadius + Margin;
            if (fromCenter.magnitude >= blockRadius)
            {
                continue;
            }

            // 目的地を円の外周へ押し出す。中心ちょうどを指した場合は現在位置側の縁にする。
            Vector3 outward;
            if (fromCenter.sqrMagnitude > 0.0001f)
            {
                outward = fromCenter.normalized;
            }
            else
            {
                Vector3 toPosition = position - center;
                toPosition.y = 0f;
                outward = toPosition.sqrMagnitude > 0.0001f ? toPosition.normalized : Vector3.forward;
            }

            Vector3 edge = center + outward * blockRadius;
            destination = new Vector3(edge.x, destination.y, edge.z);
        }

        return destination;
    }

    // 障害物一覧を一定間隔で再収集する(タワー破壊・生成などのシーン変化に追従)。
    private static void RefreshIfNeeded()
    {
        if (Time.time < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.time + RefreshInterval;
        Obstacles.Clear();

        foreach (TowerController tower in Object.FindObjectsByType<TowerController>(FindObjectsSortMode.None))
        {
            AddObstacle(tower.transform);
        }

        foreach (NexusController nexus in Object.FindObjectsByType<NexusController>(FindObjectsSortMode.None))
        {
            AddObstacle(nexus.transform);
        }
    }

    private static void AddObstacle(Transform obstacleTransform)
    {
        if (obstacleTransform == null)
        {
            return;
        }

        float radius = CalculateRadius(obstacleTransform);
        if (radius <= 0f)
        {
            return;
        }

        Obstacles.Add(new Obstacle { Transform = obstacleTransform, Radius = radius });
    }

    // コライダー形状からXZ平面上の外接円の半径を求める。
    private static float CalculateRadius(Transform obstacleTransform)
    {
        Vector3 scale = obstacleTransform.lossyScale;
        Collider obstacleCollider = obstacleTransform.GetComponent<Collider>();

        if (obstacleCollider is CapsuleCollider capsule)
        {
            // 円柱プリミティブ(タワー)はCapsuleCollider。水平半径そのまま。
            return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        if (obstacleCollider is BoxCollider box)
        {
            // 箱(本拠地)は水平断面の対角半径(回転しても安全な外接円)。
            float halfX = box.size.x * 0.5f * Mathf.Abs(scale.x);
            float halfZ = box.size.z * 0.5f * Mathf.Abs(scale.z);
            return Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
        }

        if (obstacleCollider is SphereCollider sphere)
        {
            return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        if (obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.5f;
    }
}
