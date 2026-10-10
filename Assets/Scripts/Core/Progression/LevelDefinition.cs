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

    [Header("Level Mechanics")]
    [SerializeField, Min(0)] private int requiredKeys;
    [SerializeField, Min(0)] private int trapsPerCombatRoom;
    [SerializeField, Min(0f)] private float darknessRadius;
    [SerializeField, Min(0f)] private float timeLimitSeconds;
    [SerializeField, HideInInspector] private int mechanicsSchemaVersion;

    public int LevelNumber => Mathf.Max(1, levelNumber);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Level {LevelNumber}" : displayName;
    public int RoomCount => Mathf.Max(5, roomCount);
    public int EnemyCountBonus => Mathf.Max(0, enemyCountBonus);
    public LevelType Type => levelType;
    public bool IsFinalBossLevel => levelType == LevelType.FinalBoss;
    public int RequiredKeys => Mathf.Max(0, requiredKeys);
    public int TrapsPerCombatRoom => Mathf.Max(0, trapsPerCombatRoom);
    public float DarknessRadius => Mathf.Max(0f, darknessRadius);
    public float TimeLimitSeconds => Mathf.Max(0f, timeLimitSeconds);
    public bool RequiresKeys => RequiredKeys > 0;
    public bool HasTraps => TrapsPerCombatRoom > 0;
    public bool IsDark => DarknessRadius > 0f;
    public bool HasTimeLimit => TimeLimitSeconds > 0f;

    public LevelDefinition()
    {
    }

    public LevelDefinition(int number, string name, int rooms, int enemyBonus, LevelType type,
        int keys = 0, int traps = 0, float darkRadius = 0f, float timeLimit = 0f)
    {
        levelNumber = Mathf.Max(1, number);
        displayName = name;
        roomCount = Mathf.Max(5, rooms);
        enemyCountBonus = Mathf.Max(0, enemyBonus);
        levelType = type;
        requiredKeys = Mathf.Max(0, keys);
        trapsPerCombatRoom = Mathf.Max(0, traps);
        darknessRadius = Mathf.Max(0f, darkRadius);
        timeLimitSeconds = Mathf.Max(0f, timeLimit);
        mechanicsSchemaVersion = 1;
    }

    /// <summary>Applies the new mechanics to level data serialized before these fields existed.</summary>
    public void EnsureMechanicsDefaults()
    {
        if (mechanicsSchemaVersion >= 1)
            return;

        switch (LevelNumber)
        {
            case 4:
                requiredKeys = 1;
                trapsPerCombatRoom = 3;
                break;
            case 6:
                darknessRadius = 7f;
                break;
            case 8:
                requiredKeys = 2;
                break;
            case 9:
                timeLimitSeconds = 240f;
                break;
        }

        mechanicsSchemaVersion = 1;
    }

    public static List<LevelDefinition> CreateDefaultCampaign()
    {
        return new List<LevelDefinition>
        {
            new(1, "Khởi đầu", 6, 0, LevelType.Standard),
            new(2, "Mở rộng", 7, 0, LevelType.Standard),
            new(3, "Tấn công từ xa", 7, 1, LevelType.Standard),
            new(4, "Cạm bẫy", 7, 1, LevelType.Standard, keys: 1, traps: 3),
            new(5, "Mini-boss I", 7, 1, LevelType.MiniBoss),
            new(6, "Bóng tối", 7, 1, LevelType.Standard, darkRadius: 7f),
            new(7, "Elite", 7, 2, LevelType.Standard),
            new(8, "Mê cung khóa", 7, 2, LevelType.Standard, keys: 2),
            new(9, "Thử thách cuối", 7, 2, LevelType.MiniBoss, timeLimit: 240f),
            new(10, "Boss cuối", 7, 3, LevelType.FinalBoss)
        };
    }
}
