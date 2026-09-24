using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RinesWAndVisualTests
{
    private readonly List<Object> _created = new List<Object>();
    private RinesSkillController _rines;
    private GameObject _caster;

    [SetUp]
    public void SetUp()
    {
        _caster = new GameObject("Rines W test caster");
        _caster.SetActive(false);
        _created.Add(_caster);
        _caster.tag = "Player";
        _caster.AddComponent<TeamMember>().SetTeam(Team.Blue);
        _caster.AddComponent<CharacterStats>();
        _caster.AddComponent<HealthController>();
        _rines = _caster.AddComponent<RinesSkillController>();
        RinesSkillData data = ScriptableObject.CreateInstance<RinesSkillData>();
        _created.Add(data);
        SetField(_rines, "_skillData", data);
        _caster.SetActive(true);
        if (GetField(_rines, "_stats") == null) Invoke(_rines, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in _created)
            if (item != null) Object.DestroyImmediate(item);
        _created.Clear();
    }

    [Test]
    public void W_HitsEnemyPlayerInRangeAndShowsImpact()
    {
        Targetable enemy = CreateTarget("Enemy", Team.Red, TargetClassification.Character, 1.5f);
        float before = enemy.Health.CurrentHealth;

        Invoke(_rines, "CastW");

        Assert.That(enemy.Health.CurrentHealth, Is.LessThan(before));
        RinesSkillVisuals visuals = _caster.GetComponent<RinesSkillVisuals>();
        Assert.That(visuals.IsWVisible, Is.True);
        Assert.That(visuals.ActiveHitCount, Is.EqualTo(1));
    }

    [Test]
    public void W_DoesNotHitOutOfRangeAllyMinionOrDeadPlayer()
    {
        Targetable distant = CreateTarget("Distant", Team.Red, TargetClassification.Character, 4f);
        Targetable ally = CreateTarget("Ally", Team.Blue, TargetClassification.Character, 1.5f);
        Targetable minion = CreateTarget("Minion", Team.Red, TargetClassification.Minion, -1.5f);
        minion.gameObject.tag = "Untagged";
        Targetable dead = CreateTarget("Dead", Team.Red, TargetClassification.Character, -1f);
        dead.enabled = false;
        dead.Health.TakeDamage(1000f);
        dead.enabled = true;
        float distantHp = distant.Health.CurrentHealth;
        float allyHp = ally.Health.CurrentHealth;
        float minionHp = minion.Health.CurrentHealth;

        Invoke(_rines, "CastW");

        Assert.That(distant.Health.CurrentHealth, Is.EqualTo(distantHp));
        Assert.That(ally.Health.CurrentHealth, Is.EqualTo(allyHp));
        Assert.That(minion.Health.CurrentHealth, Is.EqualTo(minionHp));
        Assert.That(dead.Health.IsDead, Is.True);
        Assert.That(_caster.GetComponent<RinesSkillVisuals>().ActiveHitCount, Is.Zero);
    }

    [Test]
    public void W_ReappliesSnareForConfiguredDurationThenMovementUnlocks()
    {
        Targetable enemy = CreateTarget("Controlled enemy", Team.Red,
            TargetClassification.Character, 1f);
        CrowdControlController cc = enemy.gameObject.AddComponent<CrowdControlController>();
        cc.ApplyStun(0.1f, _caster.transform);

        Invoke(_rines, "CastW");

        float snareEnd = (float)GetField(cc, "_snareEndTime");
        Assert.That(snareEnd - Time.time, Is.EqualTo(0.5f).Within(0.1f));
        Assert.That(cc.IsSnared, Is.True);
        Assert.That(cc.IsMovementBlocked, Is.True);
        Assert.That(_caster.GetComponent<RinesSkillVisuals>().ActiveHitCount, Is.EqualTo(1));

        SetField(cc, "_stunEndTime", Time.time - 1f);
        SetField(cc, "_snareEndTime", Time.time - 1f);
        Assert.That(cc.IsMovementBlocked, Is.False);
    }

    [Test]
    public void W_CharacterClassifiedTrainingDummyActsAsEnemyPlayerProxy()
    {
        Targetable dummy = CreateTarget("TrainingDummy", Team.Red,
            TargetClassification.Character, 1f);
        Object.DestroyImmediate(dummy.GetComponent<TeamMember>());
        dummy.gameObject.tag = "Untagged";
        CrowdControlController cc = dummy.gameObject.AddComponent<CrowdControlController>();
        cc.ApplyStun(0.5f, _caster.transform);
        float before = dummy.Health.CurrentHealth;

        Invoke(_rines, "CastW");

        Assert.That(dummy.Health.CurrentHealth, Is.LessThan(before));
        Assert.That(cc.IsSnared, Is.True);
        Assert.That(_caster.GetComponent<RinesSkillVisuals>().ActiveHitCount, Is.EqualTo(1));
    }

    [Test]
    public void W_UnclassifiedOrOtherUntaggedObjectIsNotPlayerProxy()
    {
        Targetable dummy = CreateTarget("TrainingDummy", Team.Red,
            TargetClassification.TrainingDummy, 1f);
        Object.DestroyImmediate(dummy.GetComponent<TeamMember>());
        dummy.gameObject.tag = "Untagged";
        Targetable other = CreateTarget("OtherObject", Team.Red,
            TargetClassification.Character, -1f);
        Object.DestroyImmediate(other.GetComponent<TeamMember>());
        other.gameObject.tag = "Untagged";
        float dummyHealth = dummy.Health.CurrentHealth;
        float otherHealth = other.Health.CurrentHealth;

        Invoke(_rines, "CastW");

        Assert.That(dummy.Health.CurrentHealth, Is.EqualTo(dummyHealth));
        Assert.That(other.Health.CurrentHealth, Is.EqualTo(otherHealth));
    }

    [Test]
    public void E_GlowStartsOnceAndClearsOnEndDisableAndDeath()
    {
        RinesSkillVisuals visuals = _caster.GetComponent<RinesSkillVisuals>();
        Invoke(_rines, "CastE");
        Assert.That(_rines.IsEActive, Is.True);
        Assert.That(visuals.IsEVisible, Is.True);
        GameObject glow = _caster.transform.Find("Rines E glow").gameObject;

        visuals.SetEActive(true);
        Assert.That(_caster.transform.Find("Rines E glow").gameObject, Is.SameAs(glow));
        Invoke(_rines, "EndE");
        Assert.That(visuals.IsEVisible, Is.False);

        SetField(_rines, "_eCooldownEndTime", 0d);
        Invoke(_rines, "CastE");
        _rines.enabled = false;
        // EditMode does not dispatch MonoBehaviour lifecycle callbacks on enabled changes.
        Invoke(_rines, "OnDisable");
        Assert.That(visuals.IsEVisible, Is.False);

        _rines.enabled = true;
        SetField(_rines, "_eCooldownEndTime", 0d);
        Invoke(_rines, "CastE");
        _caster.GetComponent<HealthController>().TakeDamage(1000f);
        Assert.That(_rines.IsEActive, Is.False);
        Assert.That(visuals.IsEVisible, Is.False);
    }

    private Targetable CreateTarget(string name, Team team, TargetClassification classification,
        float x)
    {
        GameObject target = new GameObject(name);
        _created.Add(target);
        target.tag = "Player";
        target.layer = 7;
        target.transform.position = new Vector3(x, 0f, 0f);
        target.AddComponent<SphereCollider>().radius = 0.45f;
        target.AddComponent<TeamMember>().SetTeam(team);
        target.AddComponent<CharacterStats>();
        HealthController health = target.AddComponent<HealthController>();
        Targetable targetable = target.AddComponent<Targetable>();
        targetable.InitializeRuntime(classification, null);
        Physics.SyncTransforms();
        return targetable;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static object GetField(object target, string field)
    {
        return target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetField(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
