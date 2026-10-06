using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the configuration for a single dungeon floor.
/// Create one asset per floor: Floor_01, Floor_02, ... Floor_10.
/// </summary>
[CreateAssetMenu(fileName = "Floor_", menuName = "Dungeon/FloorConfig")]
public class FloorConfigSO : ScriptableObject
{
    [Header("Floor Info")]
    public string floorName = "Unnamed Floor";
    [TextArea(2, 3)]
    public string description;

    [Header("Map Generation")]
    [Tooltip("Number of rooms on this floor (including Start/Exit).")]
    [Min(5)] public int numberOfRooms = 10;
    [Tooltip("Length of corridors between rooms.")]
    [Min(5)] public int corridorLength = 40;

    [Header("Enemy Configuration")]
    [Tooltip("Enemy data for this floor. Overrides the default if set.")]
    public EnemyInfoSO enemyInfo;
    [Tooltip("Min number of enemies per combat room.")]
    public int minEnemiesPerRoom = 2;
    [Tooltip("Max number of enemies per combat room.")]
    public int maxEnemiesPerRoom = 5;

    [Header("Boss")]
    [Tooltip("Does this floor have a boss room?")]
    public bool hasBoss = false;
    [Tooltip("Boss prefab override for this floor.")]
    public GameObject bossPrefab;

    [Header("Traps")]
    [Tooltip("Does this floor have traps?")]
    public bool hasTraps = false;
    [Tooltip("Trap prefab to spawn in rooms.")]
    public GameObject trapPrefab;
    [Tooltip("Number of traps per combat room.")]
    [Range(0, 8)] public int trapsPerRoom = 0;

    [Header("Keys & Locks (Level 4, 8)")]
    [Tooltip("Does this floor require keys to open the exit?")]
    public bool requireKeys = false;
    [Tooltip("Number of keys needed to open the exit.")]
    [Min(0)] public int keysRequired = 0;
    [Tooltip("Key pickup prefab.")]
    public GameObject keyPrefab;

    [Header("Lighting (Level 6)")]
    [Tooltip("Limited visibility on this floor?")]
    public bool darkFloor = false;
    [Tooltip("Player's light radius (only used when darkFloor is true).")]
    [Range(1f, 20f)] public float lightRadius = 8f;

    [Header("Wave Combat (Level 7, 9)")]
    [Tooltip("Use wave-based spawning?")]
    public bool waveMode = false;
    [Tooltip("Number of enemy waves.")]
    [Min(1)] public int numberOfWaves = 1;
    [Tooltip("Delay between waves (seconds).")]
    public float waveCooldown = 5f;

    [Header("Time Limit")]
    [Tooltip("Bật giới hạn thời gian cho tầng này?")]
    public bool hasTimeLimit = false;
    [Tooltip("Thời gian tối đa (giây) để hoàn thành tầng. Hết giờ = Game Over.")]
    [Min(10)] public float timeLimitSeconds = 120f;

    [Header("Decorations")]
    [Tooltip("Override decoration settings for this floor.")]
    public int decosPerRoom = 3;
    [Tooltip("Chance to place wall decorations.")]
    [Range(0f, 0.3f)] public float wallDecoChance = 0.08f;
}
