using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SharedCombatInputTests
{
    private readonly List<GameObject> _created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject obj in _created)
            if (obj != null) Object.DestroyImmediate(obj);
        _created.Clear();
    }

    [Test]
    public void AllRunesForTesting_EquipsEachRuneOnlyOnce()
    {
        GameObject owner = NewObject("Rune test owner", Vector3.zero, Team.Blue,
            TargetClassification.Character);
        RuneApplier.ApplyTo(owner, RuneType.AllForTesting);
        RuneApplier.ApplyTo(owner, RuneType.AllForTesting);

        Assert.That(owner.GetComponents<RelentlessRune>().Length, Is.EqualTo(1));
        Assert.That(owner.GetComponents<IndomitableRune>().Length, Is.EqualTo(1));
        Assert.That(owner.GetComponents<PursuitRune>().Length, Is.EqualTo(1));
        Assert.That(owner.GetComponents<SiegeRune>().Length, Is.EqualTo(1));
        RuneType[] options = (RuneType[])typeof(RuneSelectionUI)
            .GetField("Order", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Assert.That(options, Does.Contain(RuneType.AllForTesting));
    }

    [Test]
    public void AttackMoveInput_BindsAAndLeftMouse()
    {
        GameObject owner = NewObject("Input owner", Vector3.zero, Team.Blue,
            TargetClassification.Character);
        PlayerInputHub input = owner.AddComponent<PlayerInputHub>();
        typeof(PlayerInputHub).GetMethod("InitializeActions",
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, null);
        InputAction a = (InputAction)typeof(PlayerInputHub)
            .GetField("_aAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
        InputAction click = (InputAction)typeof(PlayerInputHub)
            .GetField("_leftClickAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);

        Assert.That(a.bindings[0].path, Is.EqualTo("<Keyboard>/a"));
        Assert.That(click.bindings[0].path, Is.EqualTo("<Mouse>/leftButton"));
    }

    [Test]
    public void AttackMove_ChoosesNearestEnemyToClickAndExcludesTower()
    {
        GameObject owner = NewObject("Owner", Vector3.zero, Team.Blue, TargetClassification.Character);
        PlayerAttackMoveCommand command = owner.AddComponent<PlayerAttackMoveCommand>();
        GameObject tower = NewObject("Tower", new Vector3(2f, 0f, 0f), Team.Red, TargetClassification.Tower);
        GameObject enemy = NewObject("Enemy", new Vector3(3f, 0f, 0f), Team.Red, TargetClassification.Character);
        NewObject("Ally", new Vector3(2.5f, 0f, 0f), Team.Blue, TargetClassification.Character);

        Targetable selected = (Targetable)typeof(PlayerAttackMoveCommand)
            .GetMethod("FindNearestEnemy", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(command, new object[] { new Vector3(2f, 0f, 0f) });

        Assert.That(selected, Is.EqualTo(enemy.GetComponent<Targetable>()));
        Assert.That(selected, Is.Not.EqualTo(tower.GetComponent<Targetable>()));
    }

    [Test]
    public void CommonD_BlockedHitForcesStunWithoutCounterDamage()
    {
        GameObject defender = NewObject("Defender", Vector3.zero, Team.Blue, TargetClassification.Character);
        GameObject attacker = NewObject("Attacker", Vector3.right, Team.Red, TargetClassification.Character);
        CommonDController d = defender.AddComponent<CommonDController>();
        CrowdControlController attackerCc = attacker.AddComponent<CrowdControlController>();
        InvokeAwakeIfNeeded(d, "_characterStats");
        InvokeAwakeIfNeeded(attackerCc, "_stats");
        float before = attacker.GetComponent<HealthController>().CurrentHealth;
        SetField(d, "_isWindowActive", true);
        SetField(d, "_windowEndTime", Time.time + 1f);

        Assert.That(d.TryBlockHardCC(attacker.transform), Is.True);
        Assert.That(attackerCc.IsStunned, Is.True);
        float stunEnd = (float)typeof(CrowdControlController)
            .GetField("_stunEndTime", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(attackerCc);
        Assert.That(stunEnd - Time.time, Is.EqualTo(0.75f).Within(0.1f));
        Assert.That(attacker.GetComponent<HealthController>().CurrentHealth, Is.EqualTo(before));
        Assert.That(d.TryBlockHardCC(attacker.transform), Is.False);
    }

    [Test]
    public void PursuitSlow_UsesCurrentMoveSpeedAsSnapshot()
    {
        GameObject target = NewObject("Target", Vector3.zero, Team.Red, TargetClassification.Character);
        CharacterStats stats = target.GetComponent<CharacterStats>();
        CrowdControlController cc = target.AddComponent<CrowdControlController>();
        InvokeAwakeIfNeeded(cc, "_stats");
        stats.AddMoveSpeedBonus(1f);
        float before = stats.CurrentMoveSpeed;

        cc.ApplySlowToCurrentSpeed(0.85f, 0.5f);

        Assert.That(stats.CurrentMoveSpeed, Is.EqualTo(before * 0.85f).Within(0.001f));
    }

    [Test]
    public void SkillApproach_CastsAtSavedPointAndCanBeCanceled()
    {
        GameObject owner = NewObject("Caster", Vector3.zero, Team.Blue, TargetClassification.Character);
        owner.AddComponent<CharacterController>();
        SkillApproachController approach = owner.AddComponent<SkillApproachController>();
        InvokeAwakeIfNeeded(approach, "_stats");
        int casts = 0;
        Vector3 point = new Vector3(4f, 0f, 0f);

        approach.CastOrApproach(point, 1f, () => casts++);
        Assert.That(approach.IsApproaching, Is.True);
        Assert.That(casts, Is.Zero);
        owner.transform.position = new Vector3(3.1f, 0f, 0f);
        typeof(SkillApproachController).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(approach, null);
        Assert.That(casts, Is.EqualTo(1));

        approach.CastOrApproach(new Vector3(8f, 0f, 0f), 1f, () => casts++);
        approach.CancelPendingApproach();
        owner.transform.position = new Vector3(7.5f, 0f, 0f);
        typeof(SkillApproachController).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(approach, null);
        Assert.That(casts, Is.EqualTo(1));
    }

    [Test]
    public void Pursuit_MovementSkillHitTriggersOnceIncludingItsOwnEDamage()
    {
        GameObject owner = NewObject("Pursuit owner", Vector3.zero, Team.Blue,
            TargetClassification.Character);
        GameObject hero = NewObject("Pursuit target", Vector3.right, Team.Red,
            TargetClassification.Character);
        InvokeAwakeIfNeeded(hero.GetComponent<Targetable>(), "_healthController");
        PursuitRune rune = owner.AddComponent<PursuitRune>();
        InvokeAwakeIfNeeded(rune, "_stats");
        HealthController health = hero.GetComponent<HealthController>();
        float before = health.CurrentHealth;
        int activations = 0;
        Application.LogCallback countActivation = (message, _, type) =>
        {
            if (type == LogType.Log && message.Contains("[ルーン/追撃] 発動！")) activations++;
        };
        Application.logMessageReceived += countActivation;
        try
        {
            MovementSkillSignal.Report(owner);
            float firstHit = health.TakeDamage(1f, owner.transform, DamageType.Normal,
                sourceId: "ZelfE#1");
            Assert.That(health.CurrentHealth, Is.LessThan(before - firstHit));
            Assert.That(activations, Is.EqualTo(1));

            float afterProc = health.CurrentHealth;
            float secondHit = health.TakeDamage(1f, owner.transform, DamageType.Normal,
                sourceId: "ZelfE#1");
            Assert.That(health.CurrentHealth, Is.EqualTo(afterProc - secondHit).Within(0.001f));
            Assert.That(activations, Is.EqualTo(1));
        }
        finally
        {
            Application.logMessageReceived -= countActivation;
        }
    }

    [Test]
    public void Pursuit_AfterMovementSkillIgnoresMinionAndHitsEnemyHero()
    {
        GameObject owner = NewObject("Rune owner", Vector3.zero, Team.Blue, TargetClassification.Character);
        GameObject minion = NewObject("Minion", Vector3.right, Team.Red, TargetClassification.Minion);
        GameObject hero = NewObject("Hero", Vector3.right * 2f, Team.Red, TargetClassification.Character);
        InvokeAwakeIfNeeded(minion.GetComponent<Targetable>(), "_healthController");
        InvokeAwakeIfNeeded(hero.GetComponent<Targetable>(), "_healthController");
        CrowdControlController heroCc = hero.AddComponent<CrowdControlController>();
        InvokeAwakeIfNeeded(heroCc, "_stats");
        PursuitRune rune = owner.AddComponent<PursuitRune>();
        InvokeAwakeIfNeeded(rune, "_stats");
        float heroBefore = hero.GetComponent<HealthController>().CurrentHealth;
        float minionBefore = minion.GetComponent<HealthController>().CurrentHealth;

        MovementSkillSignal.Report(owner);
        minion.GetComponent<HealthController>().TakeDamage(1f, owner.transform);
        Assert.That(minion.GetComponent<HealthController>().CurrentHealth,
            Is.EqualTo(minionBefore - 1f).Within(0.001f));
        hero.GetComponent<HealthController>().TakeDamage(1f, owner.transform);

        Assert.That(hero.GetComponent<HealthController>().CurrentHealth,
            Is.LessThan(heroBefore - 1f));
        Assert.That(heroCc.CurrentSlowPercent, Is.GreaterThan(0f));
    }

    private GameObject NewObject(string name, Vector3 position, Team team, TargetClassification classification)
    {
        GameObject obj = new GameObject(name);
        _created.Add(obj);
        obj.transform.position = position;
        obj.AddComponent<TeamMember>().SetTeam(team);
        obj.AddComponent<CharacterStats>();
        obj.AddComponent<HealthController>();
        Targetable target = obj.AddComponent<Targetable>();
        SetField(target, "_classification", classification);
        return obj;
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(instance, value);
    }

    private static void InvokeAwakeIfNeeded(object instance, string fieldName)
    {
        System.Type type = instance.GetType();
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        if (type.GetField(fieldName, flags).GetValue(instance) == null)
            type.GetMethod("Awake", flags).Invoke(instance, null);
    }
}
