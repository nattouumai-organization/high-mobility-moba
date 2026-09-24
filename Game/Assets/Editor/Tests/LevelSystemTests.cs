using NUnit.Framework;

public class LevelSystemTests
{
    [TestCase(-1, 1)]
    [TestCase(0, 1)]
    [TestCase(39, 1)]
    [TestCase(40, 2)]
    [TestCase(89, 2)]
    [TestCase(90, 3)]
    [TestCase(149, 3)]
    [TestCase(150, 4)]
    [TestCase(224, 4)]
    [TestCase(225, 5)]
    [TestCase(309, 5)]
    [TestCase(310, 6)]
    [TestCase(10000, 6)]
    public void GetLevel_ReturnsExpectedLevelAtThresholds(int points, int expectedLevel)
    {
        Assert.That(LevelSystem.GetLevel(points), Is.EqualTo(expectedLevel));
    }

    [TestCase(-1, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 40)]
    [TestCase(6, 310)]
    [TestCase(7, 310)]
    public void GetPointsRequiredForLevel_ClampsOutsideSupportedRange(int level, int expectedPoints)
    {
        Assert.That(LevelSystem.GetPointsRequiredForLevel(level), Is.EqualTo(expectedPoints));
    }
}
