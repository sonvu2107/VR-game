using System.Collections.Generic;
using NUnit.Framework;

public sealed class CoreProgressionTests
{
    [Test]
    public void DefaultCampaign_HasTenOrderedLevels()
    {
        List<LevelDefinition> levels = LevelDefinition.CreateDefaultCampaign();

        Assert.That(levels, Has.Count.EqualTo(10));
        for (int index = 0; index < levels.Count; index++)
            Assert.That(levels[index].LevelNumber, Is.EqualTo(index + 1));
    }

    [Test]
    public void DefaultCampaign_EndsWithFinalBossLevel()
    {
        List<LevelDefinition> levels = LevelDefinition.CreateDefaultCampaign();

        Assert.That(levels[^1].Type, Is.EqualTo(LevelType.FinalBoss));
        Assert.That(levels[^1].IsFinalBossLevel, Is.True);
    }

    [Test]
    public void DefaultCampaign_EnablesOnlyTheNewMechanicsAssignedToEachLevel()
    {
        List<LevelDefinition> levels = LevelDefinition.CreateDefaultCampaign();

        Assert.That(levels[3].RequiredKeys, Is.EqualTo(1));
        Assert.That(levels[3].TrapsPerCombatRoom, Is.EqualTo(3));
        Assert.That(levels[5].DarknessRadius, Is.EqualTo(7f));
        Assert.That(levels[7].RequiredKeys, Is.EqualTo(2));
        Assert.That(levels[8].TimeLimitSeconds, Is.EqualTo(240f));

        Assert.That(levels[0].RequiresKeys, Is.False);
        Assert.That(levels[0].HasTraps, Is.False);
        Assert.That(levels[0].IsDark, Is.False);
        Assert.That(levels[0].HasTimeLimit, Is.False);
    }

    [Test]
    public void LevelSeeds_AreStableAndDifferentPerLevel()
    {
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 1), Is.EqualTo(12345));
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 2), Is.EqualTo(13354));
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 10), Is.EqualTo(21426));
    }

    [TestCase(1, 2, 2, false)]
    [TestCase(0, 1, 2, false)]
    [TestCase(0, 2, 2, true)]
    [TestCase(0, 0, 0, true)]
    public void CompletionRules_RequireAllEnemiesAndKeys(
        int enemies, int collectedKeys, int requiredKeys, bool expected)
    {
        Assert.That(LevelCompletionRules.AreRequirementsMet(enemies, collectedKeys, requiredKeys),
            Is.EqualTo(expected));
    }
}
