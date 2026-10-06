using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class DamageContextTests
{
    [Test]
    public void Constructor_PreservesDamageMetadata()
    {
        DamageContext context = new DamageContext(
            null,
            DamageType.True,
            125f,
            isReflected: true,
            isBasicAttack: true,
            sourceId: "ZelfW#3");

        Assert.That(context.Attacker, Is.Null);
        Assert.That(context.Type, Is.EqualTo(DamageType.True));
        Assert.That(context.BaseAmount, Is.EqualTo(125f));
        Assert.That(context.IsReflected, Is.True);
        Assert.That(context.IsBasicAttack, Is.True);
        Assert.That(context.SourceId, Is.EqualTo("ZelfW#3"));
        Assert.That(context.AttackAttributes, Is.EqualTo(SkillAttribute.BasicAttack));
    }

    [TestCase("Zelf")]
    [TestCase("Volbraak")]
    [TestCase("Oboro")]
    [TestCase("Rines")]
    [TestCase("Lieselotte")]
    public void SkillCatalog_ContainsBasicAttackAndAllFiveAbilities(string characterId)
    {
        foreach (CharacterAbilitySlot slot in System.Enum.GetValues(typeof(CharacterAbilitySlot)))
        {
            Assert.That(SkillAttributeCatalog.TryGet(characterId, slot, out _), Is.True,
                $"{characterId}/{slot}");
        }
        Assert.That(SkillAttributeCatalog.Get(characterId, CharacterAbilitySlot.BasicAttack)
            .Has(SkillAttribute.BasicAttack), Is.True);
        Assert.That(SkillAttributeCatalog.TryGet("Unknown", CharacterAbilitySlot.Q, out _), Is.False);
    }

    [Test]
    public void SkillCatalog_SeparatesConditionalCcAndTargetingFromDamageClassification()
    {
        SkillAttributeDefinition rinesW = SkillAttributeCatalog.Get("Rines", CharacterAbilitySlot.W);
        Assert.That(rinesW.Has(SkillAttribute.Slow | SkillAttribute.Snare), Is.True);
        Assert.That(SkillAttributeCatalog.DamageOf("Rines", HeroSkillSlot.W),
            Is.EqualTo(SkillAttribute.SkillAttack));
        Assert.That(SkillAttributeCatalog.Get("Lieselotte", CharacterAbilitySlot.Q).Targeting,
            Is.EqualTo(SkillTargetingType.UnitTarget));
        Assert.That(SkillAttributeCatalog.DamageOf("Lieselotte", HeroSkillSlot.Q),
            Is.EqualTo(SkillAttribute.BasicAttack));
        Assert.That(SkillAttributeCatalog.Get("Oboro", CharacterAbilitySlot.E)
            .Has(SkillAttribute.Blink | SkillAttribute.SkillsDisabled), Is.True);
        Assert.That(SkillAttributeCatalog.DamageOf("Oboro", HeroSkillSlot.R),
            Is.EqualTo(SkillAttribute.TrueDamage));
    }

    [Test]
    public void HealthDamageEvent_CarriesAttributesWithoutChangingArmorOrBasicAttackRules()
    {
        GameObject target = new GameObject("Attribute test target");
        try
        {
            CharacterStats stats = target.AddComponent<CharacterStats>();
            stats.AddArmorBonus(100f);
            HealthController health = target.AddComponent<HealthController>();
            if (typeof(HealthController).GetField("_characterStats",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(health) == null)
                typeof(HealthController).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
            DamageContext received = default;
            health.DamageTaken += (context, _) => received = context;

            float dealt = health.TakeDamage(20f, null, DamageType.Normal,
                attackAttributes: SkillAttribute.BasicAttack);

            Assert.That(dealt, Is.EqualTo(20f * 100f / (100f + stats.CurrentArmor)).Within(0.001f));
            Assert.That(received.AttackAttributes, Is.EqualTo(SkillAttribute.BasicAttack));
            Assert.That(received.IsBasicAttack, Is.False);
            Assert.That(received.Type, Is.EqualTo(DamageType.Normal));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }
}
