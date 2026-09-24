using NUnit.Framework;

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
    }
}
