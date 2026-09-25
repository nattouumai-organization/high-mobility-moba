using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CrowdControlHealthBarTests
{
    private readonly List<GameObject> _created = new List<GameObject>();
    private TeamMember _localTeam;
    private GameObject _enemy;
    private HealthController _health;
    private CrowdControlController _cc;
    private WorldHealthBar _bar;

    [SetUp]
    public void SetUp()
    {
        GameObject local = NewObject("Local player", Team.Blue, TargetClassification.Character);
        _localTeam = local.GetComponent<TeamMember>();
        _enemy = NewObject("Enemy player", Team.Red, TargetClassification.Character);
        _health = _enemy.GetComponent<HealthController>();

        GameObject barObject = new GameObject("Health bar", typeof(RectTransform), typeof(Canvas));
        _created.Add(barObject);
        barObject.transform.SetParent(_enemy.transform, false);
        Canvas canvas = barObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _bar = barObject.AddComponent<WorldHealthBar>();
        SetField(_bar, "_healthController", _health);
        SetField(_bar, "_canvas", canvas);
        Invoke(_bar, "OnEnable");
        Invoke(_bar, "BindCrowdControlLabels", _localTeam);
        _cc = _enemy.AddComponent<CrowdControlController>();
        if (GetField(_cc, "_health") == null) Invoke(_cc, "Awake");
        Invoke(_cc, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        if (_bar != null) Invoke(_bar, "OnDisable");
        foreach (GameObject item in _created)
            if (item != null) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void FilledHealthBar_UsesOpaqueSpriteAcrossTheEntireExecuteScale()
    {
        GameObject barObject = new GameObject("Sprite test bar", typeof(RectTransform), typeof(Canvas));
        _created.Add(barObject);
        WorldHealthBar spriteBar = barObject.AddComponent<WorldHealthBar>();
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
        _created.Add(fillObject);
        fillObject.transform.SetParent(barObject.transform, false);
        Image fill = fillObject.AddComponent<Image>();
        fill.type = Image.Type.Filled;
        Texture2D paddedTexture = new Texture2D(4, 1);
        Sprite paddedSprite = Sprite.Create(paddedTexture, new Rect(0, 0, 4, 1), Vector2.one * 0.5f);
        try
        {
            fill.sprite = paddedSprite;
            spriteBar.InitializeRuntime(_health, fill);
            Assert.That(fill.sprite.texture, Is.EqualTo(Texture2D.whiteTexture));
            Assert.That(fill.sprite.rect.width, Is.EqualTo(fill.sprite.texture.width));
            Invoke(spriteBar, "CreateExecuteMarker", 0.1f);
            Image marker = (Image)GetField(spriteBar, "_oboroExecuteMarker");
            Assert.That(marker.rectTransform.parent, Is.EqualTo(fill.rectTransform));
            Assert.That(marker.rectTransform.anchorMin.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(marker.rectTransform.sizeDelta.x, Is.EqualTo(2f));
        }
        finally
        {
            Object.DestroyImmediate(paddedSprite);
            Object.DestroyImmediate(paddedTexture);
        }
    }

    [Test]
    public void Stun_StartsEndsAndWorksForNonRinesCaster()
    {
        GameObject otherCaster = new GameObject("Other character");
        _created.Add(otherCaster);
        _cc.ApplyStun(0.3f, otherCaster.transform);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible, Is.True);
        Assert.That(_bar.IsSnareLabelVisible, Is.False);

        SetField(_cc, "_stunEndTime", Time.time - 1f);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible, Is.False);
    }

    [Test]
    public void Snare_ReapplicationExtendsDisplayUntilActualEnd()
    {
        _cc.ApplySnare(0.1f, null);
        float firstEnd = (float)GetField(_cc, "_snareEndTime");
        _cc.ApplySnare(0.6f, null);
        float extendedEnd = (float)GetField(_cc, "_snareEndTime");
        Assert.That(extendedEnd, Is.GreaterThan(firstEnd));
        Refresh();
        Assert.That(_bar.IsSnareLabelVisible, Is.True);

        SetField(_cc, "_snareEndTime", Time.time - 1f);
        Refresh();
        Assert.That(_bar.IsSnareLabelVisible, Is.False);
    }

    [Test]
    public void StunAndSnare_AppearInSeparateRows()
    {
        _cc.ApplyStun(0.5f, null);
        _cc.ApplySnare(0.5f, null);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible && _bar.IsSnareLabelVisible, Is.True);
        TextPositionIsAboveSnare();
    }

    [Test]
    public void OnlyEnemyPlayerQualifiesForCrowdControlLabels()
    {
        Assert.That(WorldHealthBar.IsEnemyPlayer(_localTeam, _health), Is.True);
        GameObject ally = NewObject("Ally", Team.Blue, TargetClassification.Character);
        GameObject minion = NewObject("Minion", Team.Red, TargetClassification.Minion);
        minion.tag = "Untagged";
        Assert.That(WorldHealthBar.IsEnemyPlayer(_localTeam, ally.GetComponent<HealthController>()), Is.False);
        Assert.That(WorldHealthBar.IsEnemyPlayer(_localTeam, minion.GetComponent<HealthController>()), Is.False);
    }

    [Test]
    public void CharacterClassifiedTrainingDummy_ShowsStunAndSnareAboveItsHealthBar()
    {
        GameObject dummy = NewObject("TrainingDummy", Team.Red, TargetClassification.Character);
        Object.DestroyImmediate(dummy.GetComponent<TeamMember>());
        dummy.tag = "Untagged";
        HealthController health = dummy.GetComponent<HealthController>();
        Assert.That(WorldHealthBar.IsEnemyPlayer(null, health), Is.True);

        GameObject barObject = new GameObject("Dummy health bar", typeof(RectTransform), typeof(Canvas));
        _created.Add(barObject);
        barObject.transform.SetParent(dummy.transform, false);
        Canvas canvas = barObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        WorldHealthBar bar = barObject.AddComponent<WorldHealthBar>();
        SetField(bar, "_healthController", health);
        SetField(bar, "_canvas", canvas);
        Invoke(bar, "OnEnable");
        Invoke(bar, "BindCrowdControlLabels", (object)null);
        CrowdControlController cc = dummy.AddComponent<CrowdControlController>();
        if (GetField(cc, "_health") == null) Invoke(cc, "Awake");
        cc.ApplyStun(0.5f, null);
        cc.ApplySnare(0.5f, null);
        Invoke(bar, "UpdateCrowdControlLabels");
        Assert.That(bar.IsStunLabelVisible && bar.IsSnareLabelVisible, Is.True);
        UnityEngine.UI.Text stun = bar.transform.Find("Stun Status").GetComponent<UnityEngine.UI.Text>();
        UnityEngine.UI.Text snare = bar.transform.Find("Snare Status").GetComponent<UnityEngine.UI.Text>();
        foreach (char character in "スタン")
            Assert.That(stun.font.HasCharacter(character), Is.True, $"Missing stun glyph: {character}");
        foreach (char character in "スネア")
            Assert.That(snare.font.HasCharacter(character), Is.True, $"Missing snare glyph: {character}");

        SetField(cc, "_stunEndTime", Time.time - 1f);
        SetField(cc, "_snareEndTime", Time.time - 1f);
        Invoke(bar, "UpdateCrowdControlLabels");
        Assert.That(bar.IsStunLabelVisible || bar.IsSnareLabelVisible, Is.False);
        Invoke(bar, "OnDisable");
    }

    [Test]
    public void TrainingDummyRequiresCharacterClassificationForProxyDisplay()
    {
        GameObject dummy = NewObject("TrainingDummy", Team.Red, TargetClassification.TrainingDummy);
        Object.DestroyImmediate(dummy.GetComponent<TeamMember>());
        dummy.tag = "Untagged";
        Assert.That(WorldHealthBar.IsEnemyPlayer(null, dummy.GetComponent<HealthController>()), Is.False);
    }

    [Test]
    public void StatusAppliedAfterCrowdControlCreation_RecoversDummyHealthBarBinding()
    {
        GameObject dummy = NewObject("TrainingDummy", Team.Red, TargetClassification.Character);
        Object.DestroyImmediate(dummy.GetComponent<TeamMember>());
        dummy.tag = "Untagged";
        CrowdControlController cc = dummy.AddComponent<CrowdControlController>();
        if (GetField(cc, "_health") == null) Invoke(cc, "Awake");

        GameObject barObject = new GameObject("Dummy health bar", typeof(RectTransform), typeof(Canvas));
        _created.Add(barObject);
        barObject.transform.SetParent(dummy.transform, false);
        Canvas canvas = barObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        WorldHealthBar bar = barObject.AddComponent<WorldHealthBar>();
        SetField(bar, "_healthController", dummy.GetComponent<HealthController>());
        SetField(bar, "_canvas", canvas);
        Invoke(bar, "OnEnable");
        try
        {
            Assert.That(bar.transform.Find("Stun Status"), Is.Null);
            cc.ApplyStun(0.5f, null);
            Assert.That(bar.transform.Find("Stun Status"), Is.Not.Null);
            Assert.That(bar.IsStunLabelVisible, Is.True);
        }
        finally
        {
            Invoke(bar, "OnDisable");
        }
    }

    [Test]
    public void PrototypeSceneTrainingDummy_BindsRealHealthBarToCrowdControl()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/SC_Prototype.unity",
            OpenSceneMode.Additive);
        WorldHealthBar dummyBar = null;
        try
        {
            GameObject dummy = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "TrainingDummy") dummy = root;
            Assert.That(dummy, Is.Not.Null);
            dummyBar = dummy.GetComponentInChildren<WorldHealthBar>();
            Assert.That(dummyBar, Is.Not.Null);
            Assert.That(dummy.GetComponent<Targetable>().IsTrainingDummyPlayerProxy, Is.True);
            if (GetField(dummyBar, "_canvas") == null) Invoke(dummyBar, "Awake");
            Invoke(dummyBar, "OnEnable");
            Invoke(dummyBar, "Start");
            Assert.That(dummyBar.transform.Find("Stun Status"), Is.Not.Null);
            Assert.That(dummyBar.transform.Find("Snare Status"), Is.Not.Null);

            CrowdControlController cc = dummy.AddComponent<CrowdControlController>();
            if (GetField(cc, "_health") == null) Invoke(cc, "Awake");
            HardCcTestEmitter emitter = dummy.GetComponent<HardCcTestEmitter>();
            Assert.That(emitter, Is.Not.Null);
            Invoke(emitter, "Start");
            Assert.That(GetField(emitter, "_target"), Is.SameAs(cc));
            cc.ApplyStun(0.5f, null);
            cc.ApplySnare(0.5f, null);
            Invoke(dummyBar, "UpdateCrowdControlLabels");
            Assert.That(dummyBar.IsStunLabelVisible && dummyBar.IsSnareLabelVisible, Is.True);
            UnityEngine.UI.Text stun = dummyBar.transform.Find("Stun Status")
                .GetComponent<UnityEngine.UI.Text>();
            UnityEngine.UI.Image stunBackdrop = dummyBar.transform.Find("Stun Status Backdrop")
                .GetComponent<UnityEngine.UI.Image>();
            Assert.That(stun.fontSize, Is.GreaterThanOrEqualTo(56));
            Assert.That(stun.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
            Assert.That(stunBackdrop.enabled, Is.True);
            TextGenerator generator = new TextGenerator();
            Assert.That(generator.Populate(stun.text,
                stun.GetGenerationSettings(stun.rectTransform.rect.size)), Is.True);
            Assert.That(generator.characterCountVisible, Is.GreaterThan(0));
        }
        finally
        {
            if (dummyBar != null) Invoke(dummyBar, "OnDisable");
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void CrowdControlAddedAfterHealthBarBinding_StillShowsStatus()
    {
        Object.DestroyImmediate(_cc);
        _cc = _enemy.AddComponent<CrowdControlController>();
        if (GetField(_cc, "_health") == null) Invoke(_cc, "Awake");
        _cc.ApplySnare(0.5f, null);
        Refresh();
        Assert.That(_bar.IsSnareLabelVisible, Is.True);
    }

    [Test]
    public void DeathDisableAndRevive_DoNotLeaveStaleLabels()
    {
        _cc.ApplyStun(1f, null);
        _cc.ApplySnare(1f, null);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible && _bar.IsSnareLabelVisible, Is.True);

        _bar.enabled = false;
        Assert.That(_bar.IsStunLabelVisible || _bar.IsSnareLabelVisible, Is.False);
        _bar.enabled = true;
        Refresh();
        Assert.That(_bar.IsStunLabelVisible, Is.True);

        _enemy.SetActive(false);
        Assert.That(_bar.IsStunLabelVisible || _bar.IsSnareLabelVisible, Is.False);
        _enemy.SetActive(true);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible, Is.True);

        _enemy.GetComponent<Targetable>().enabled = false;
        _health.TakeDamage(1000f);
        Refresh();
        Assert.That(_bar.IsStunLabelVisible || _bar.IsSnareLabelVisible, Is.False);
        _health.Revive();
        Refresh();
        Assert.That(_bar.IsStunLabelVisible || _bar.IsSnareLabelVisible, Is.False);
    }

    private void TextPositionIsAboveSnare()
    {
        RectTransform stun = _bar.transform.Find("Stun Status") as RectTransform;
        RectTransform snare = _bar.transform.Find("Snare Status") as RectTransform;
        Assert.That(stun, Is.Not.Null);
        Assert.That(snare, Is.Not.Null);
        Assert.That(stun.anchoredPosition.y, Is.GreaterThan(snare.anchoredPosition.y));
        Assert.That(snare.anchoredPosition.y, Is.GreaterThan(16f));
    }

    private GameObject NewObject(string name, Team team, TargetClassification classification)
    {
        GameObject result = new GameObject(name);
        _created.Add(result);
        result.tag = "Player";
        result.AddComponent<TeamMember>().SetTeam(team);
        result.AddComponent<CharacterStats>();
        result.AddComponent<HealthController>();
        result.AddComponent<Targetable>().InitializeRuntime(classification, null);
        return result;
    }

    private void Refresh() => Invoke(_bar, "UpdateCrowdControlLabels");

    private static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }

    private static object GetField(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
