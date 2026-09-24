using NUnit.Framework;

public sealed class RinesRulesTests
{
    [TestCase(0, false, 63.8f)]
    [TestCase(1, false, 70.18f)]
    [TestCase(2, true, 91.872f)]
    public void Damage_AppliesRankAndPassiveOnlyOnce(int rank, bool passive, float expected)
    {
        Assert.That(RinesRules.Damage(35f, 0.6f, 48f, rank, passive, 1.2f, 0.1f),
            Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void PrototypeTiming_LeavesQAndRCounterplayWindows()
    {
        var data = UnityEngine.ScriptableObject.CreateInstance<RinesSkillData>();
        try
        {
            Assert.That(data.QDelay, Is.GreaterThan(0f));
            Assert.That(data.QWindow, Is.GreaterThan(data.QDelay));
            Assert.That(data.RWarning, Is.GreaterThan(data.QDelay));
            Assert.That(data.RCenterRadius, Is.LessThan(data.RRadius));
        }
        finally { UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test]
    public void QSecondInput_IsIgnoredBeforeDelayAndAfterWindow()
    {
        Assert.That(RinesRules.CanDetonateQ(0.34, 0.35f, 3f), Is.False);
        Assert.That(RinesRules.CanDetonateQ(0.5, 0.35f, 3f), Is.True);
        Assert.That(RinesRules.CanDetonateQ(3.01, 0.35f, 3f), Is.False);
    }

    [Test]
    public void EHit_AddsTailToContactTime()
    {
        Assert.That(RinesRules.EEndAfterHit(10.9, 0.2f), Is.EqualTo(11.1).Within(0.0001));
    }
}
