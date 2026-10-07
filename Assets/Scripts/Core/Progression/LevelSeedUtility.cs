public static class LevelSeedUtility
{
    public static int Calculate(int baseSeed, int levelSeedStep, int levelNumber)
    {
        int safeStep = levelSeedStep < 1 ? 1 : levelSeedStep;
        int safeLevel = levelNumber < 1 ? 1 : levelNumber;
        return baseSeed + (safeLevel - 1) * safeStep;
    }
}
