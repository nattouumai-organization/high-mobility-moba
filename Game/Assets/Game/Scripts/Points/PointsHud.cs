using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面左上に両チームの合計ポイントを表示するHUD(フェーズ6)。
/// GameManagerが実行時にAddComponentで生成する。
/// 内蔵フォント(LegacyRuntime.ttf)に日本語グリフが無いため表記は英語。
/// </summary>
public class PointsHud : MonoBehaviour
{
    private Text _label;

    private void OnEnable()
    {
        PointsManager.PointsChanged += HandlePointsChanged;
    }

    private void OnDisable()
    {
        PointsManager.PointsChanged -= HandlePointsChanged;
    }

    private void Start()
    {
        CreateUi();
        Refresh();
    }

    private void HandlePointsChanged(Team team, int total)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_label == null)
        {
            return;
        }

        _label.text = $"Points  Blue: {PointsManager.GetPoints(Team.Blue)}   Red: {PointsManager.GetPoints(Team.Red)}";
    }

    private void CreateUi()
    {
        var canvasObject = new GameObject("PointsHudCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var labelObject = new GameObject("PointsLabel");
        labelObject.transform.SetParent(canvasObject.transform, false);
        var rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(560f, 40f);

        _label = labelObject.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 22;
        _label.fontStyle = FontStyle.Bold;
        _label.color = Color.white;
        _label.alignment = TextAnchor.UpperLeft;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
