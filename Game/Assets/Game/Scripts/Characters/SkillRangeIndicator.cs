using UnityEngine;

/// <summary>
/// スキルの射程・効果範囲を表示する汎用インジケーター(円+方向線)。
/// 各スキルコントローラーがCreate()で専用インスタンスを生成して使う(スキル間で共有しない)。
/// フェーズ3以降のスキル(地点指定・方向指定など)でも再利用する。
/// </summary>
public sealed class SkillRangeIndicator : MonoBehaviour
{
    private const int CircleSegments = 64;
    private const float LineWidth = 0.05f;

    private LineRenderer _circle;
    private LineRenderer _directionLine;
    private Material _material;

    /// <summary>parentの子として専用のインジケーターを生成する。</summary>
    public static SkillRangeIndicator Create(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<SkillRangeIndicator>();
    }

    /// <summary>親(Player)を中心とした半径radiusの円を表示する。yOffsetは親基準の接地高さ。</summary>
    public void ShowCircle(float radius, Color color, float yOffset)
    {
        EnsureCircle();
        _circle.enabled = true;
        _circle.startColor = color;
        _circle.endColor = color;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (float)i / CircleSegments * Mathf.PI * 2f;
            _circle.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius));
        }
    }

    /// <summary>origin(ワールド座標)からdirection方向へlengthの直線を表示する(方向指定スキル用)。directionは正規化済みであること。</summary>
    public void ShowDirectionLine(Vector3 origin, Vector3 direction, float length, Color color)
    {
        EnsureDirectionLine();
        _directionLine.enabled = true;
        _directionLine.startColor = color;
        _directionLine.endColor = color;
        _directionLine.SetPosition(0, origin);
        _directionLine.SetPosition(1, origin + direction * length);
    }

    public void HideCircle()
    {
        if (_circle != null) _circle.enabled = false;
    }

    public void HideDirectionLine()
    {
        if (_directionLine != null) _directionLine.enabled = false;
    }

    public void HideAll()
    {
        HideCircle();
        HideDirectionLine();
    }

    private void EnsureCircle()
    {
        if (_circle != null) return;
        _circle = gameObject.AddComponent<LineRenderer>();
        _circle.useWorldSpace = false;
        _circle.loop = true;
        _circle.positionCount = CircleSegments;
        _circle.startWidth = LineWidth;
        _circle.endWidth = LineWidth;
        _circle.material = GetMaterial();
        _circle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _circle.receiveShadows = false;
    }

    private void EnsureDirectionLine()
    {
        if (_directionLine != null) return;
        GameObject child = new GameObject("Direction Line");
        child.transform.SetParent(transform, false);
        _directionLine = child.AddComponent<LineRenderer>();
        _directionLine.useWorldSpace = true;
        _directionLine.loop = false;
        _directionLine.positionCount = 2;
        _directionLine.startWidth = LineWidth;
        _directionLine.endWidth = LineWidth;
        _directionLine.material = GetMaterial();
        _directionLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _directionLine.receiveShadows = false;
    }

    private Material GetMaterial()
    {
        if (_material == null)
        {
            _material = new Material(Shader.Find("Sprites/Default"));
        }
        return _material;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
