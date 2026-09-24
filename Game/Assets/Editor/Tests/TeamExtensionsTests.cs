using NUnit.Framework;
using UnityEngine;

public class TeamExtensionsTests
{
    [Test]
    public void Opponent_ChangesBlueToRedAndRedToBlue()
    {
        Assert.That(Team.Blue.Opponent(), Is.EqualTo(Team.Red));
        Assert.That(Team.Red.Opponent(), Is.EqualTo(Team.Blue));
    }

    [Test]
    public void GetTeamColor_ReturnsConfiguredColorForEachTeam()
    {
        Assert.That(Team.Blue.GetTeamColor(), Is.EqualTo(new Color(0.25f, 0.45f, 1f, 1f)));
        Assert.That(Team.Red.GetTeamColor(), Is.EqualTo(new Color(1f, 0.3f, 0.25f, 1f)));
    }
}
