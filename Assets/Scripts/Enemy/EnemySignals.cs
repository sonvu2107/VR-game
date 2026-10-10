// Các sự kiện chết của enemy/mini-boss/boss để UI và tiến trình level lắng nghe.
using System;

public static class EnemySignals
{
    public static event Action<EnemyHealth> EnemyDied;
    public static event Action<EnemyHealth> MiniBossDefeated;
    public static event Action<EnemyHealth> BossDefeated;
    public static event Action<EnemyHealth, int> BossPhaseChanged;

    internal static void RaiseEnemyDied(EnemyHealth enemy)
    {
        EnemyDied?.Invoke(enemy);
    }

    internal static void RaiseBossDefeated(EnemyHealth boss)
    {
        BossDefeated?.Invoke(boss);
    }

    internal static void RaiseMiniBossDefeated(EnemyHealth miniBoss)
    {
        MiniBossDefeated?.Invoke(miniBoss);
    }

    internal static void RaiseBossPhaseChanged(EnemyHealth boss, int phase)
    {
        BossPhaseChanged?.Invoke(boss, phase);
    }
}
