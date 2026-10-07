public static class LevelCompletionRules
{
    public static bool AreRequirementsMet(int enemyCount, int keysCollected, int keysRequired)
    {
        return enemyCount <= 0 && keysCollected >= keysRequired;
    }
}
