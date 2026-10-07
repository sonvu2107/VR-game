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
    public void LevelSeeds_AreStableAndDifferentPerLevel()
    {
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 1), Is.EqualTo(12345));
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 2), Is.EqualTo(13354));
        Assert.That(LevelSeedUtility.Calculate(12345, 1009, 10), Is.EqualTo(21426));
    }
}
