using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MinionTowerBalanceTests
{
    [TestCase(MinionController.MinionType.Melee, 0, 275f, 20f, 2)]
    [TestCase(MinionController.MinionType.Melee, 1, 293f, 22f, 2)]
    [TestCase(MinionController.MinionType.Melee, 7, 401f, 34f, 2)]
    [TestCase(MinionController.MinionType.Ranged, 0, 145f, 24f, 1)]
    [TestCase(MinionController.MinionType.Ranged, 1, 157f, 26f, 1)]
    [TestCase(MinionController.MinionType.Ranged, 7, 229f, 38f, 1)]
    public void TowerShots_LeaveFifteenHealthAtEveryWaveLevel(
        MinionController.MinionType type, int waveLevel, float expectedHealth, float expectedAttack, int shots)
    {
        GameObject minionObject = new GameObject("Test minion");
        GameObject towerObject = new GameObject("Test tower");
        try
        {
            HealthController health = minionObject.AddComponent<HealthController>();
            MinionController minion = minionObject.AddComponent<MinionController>();
            MethodInfo initialize = typeof(MinionController).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(initialize, Is.Not.Null);
            initialize.Invoke(minion, new object[] { Team.Red, type, waveLevel, health });

            Assert.That(health.MaxHealth, Is.EqualTo(expectedHealth));
            FieldInfo attackDamage = typeof(MinionController).GetField("_attackDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(attackDamage, Is.Not.Null);
            Assert.That((float)attackDamage.GetValue(minion), Is.EqualTo(expectedAttack));

            TowerController tower = towerObject.AddComponent<TowerController>();
            MethodInfo attack = typeof(TowerController).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(attack, Is.Not.Null);
            for (int shot = 0; shot < shots; shot++)
            {
                attack.Invoke(tower, new object[] { health, false });
            }

            Assert.That(health.IsDead, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(15f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(minionObject);
            Object.DestroyImmediate(towerObject);
        }
    }

    [Test]
    public void TowerHeroDamage_RemainsAtBaseAndConsecutiveValues()
    {
        GameObject heroObject = new GameObject("Test hero");
        GameObject towerObject = new GameObject("Test tower");
        try
        {
            HealthController health = heroObject.AddComponent<HealthController>();
            health.SetMaxHealth(1000f);
            TowerController tower = towerObject.AddComponent<TowerController>();
            MethodInfo attack = typeof(TowerController).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(attack, Is.Not.Null);

            attack.Invoke(tower, new object[] { health, true });
            Assert.That(health.CurrentHealth, Is.EqualTo(870f));
            attack.Invoke(tower, new object[] { health, true });
            Assert.That(health.CurrentHealth, Is.EqualTo(707.5f));
        }
        finally
        {
            Object.DestroyImmediate(heroObject);
            Object.DestroyImmediate(towerObject);
        }
    }
}
