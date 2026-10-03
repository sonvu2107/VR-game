using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevelType
{
    Standard,
    MiniBoss,
    FinalBoss
}

[Serializable]
public sealed class LevelDefinition
{
    [SerializeField, Min(1)] private int levelNumber = 1;
    [SerializeField] private string displayName = "Dungeon";
    [SerializeField, Min(5)] private int roomCount = 6;
    [SerializeField, Min(0)] private int enemyCountBonus;
    [SerializeField] private LevelType levelType = LevelType.Standard;

    public int LevelNumber => Mathf.Max(1, levelNumber);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Level {LevelNumber}" : displayName;
    public int RoomCount => Mathf.Max(5, roomCount);
    public int EnemyCountBonus => Mathf.Max(0, enemyCountBonus);
    public LevelType Type => levelType;
    public bool IsFinalBossLevel => levelType == LevelType.FinalBoss;

    public LevelDefinition()
    {
    }

    public LevelDefinition(int number, string name, int rooms, int enemyBonus, LevelType type)
    {
        levelNumber = Mathf.Max(1, number);
        displayName = name;
        roomCount = Mathf.Max(5, rooms);
        enemyCountBonus = Mathf.Max(0, enemyBonus);
        levelType = type;
    }

    public static List<LevelDefinition> CreateDefaultCampaign()
    {
        return new List<LevelDefinition>
        {
            new(1, "Khởi đầu", 6, 0, LevelType.Standard),
            new(2, "Mở rộng", 7, 0, LevelType.Standard),
            new(3, "Tấn công từ xa", 7, 1, LevelType.Standard),
            new(4, "Cạm bẫy", 8, 1, LevelType.Standard),
            new(5, "Mini-boss I", 8, 1, LevelType.MiniBoss),
            new(6, "Bóng tối", 9, 1, LevelType.Standard),
            new(7, "Elite", 9, 2, LevelType.Standard),
            new(8, "Mê cung khóa", 10, 2, LevelType.Standard),
            new(9, "Thử thách cuối", 10, 2, LevelType.MiniBoss),
            new(10, "Boss cuối", 11, 3, LevelType.FinalBoss)
        };
    }
}
