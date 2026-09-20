using System.Collections.Generic;
using System.Linq;
using NavMeshPlus.Components;
using UnityEngine;

public class InfiniteWorldGenerator : MonoBehaviour
{
    public NavMeshSurface surface;
    public RandomWalkSO roomParameters;
    public TilemapVisualizer tilemapVisualizer;
    public EnemyInfoSO enemyInfo;
    public GameManager manager;
    [Header("Room Spawn References")]
    public Transform playerTransform;
    public GameObject treasurePrefab;
    public GameObject bossPrefab;
    public GameObject exitPrefab;

    public int corridorLength = 20;
    public int numberOfRooms = 10;
    [Min(1)] public int maxGenerationAttempts = 20;
    [Min(0)] public int spawnClearance = 1;
    [Tooltip("The same seed always produces the same dungeon layout and mob spawn positions.")]
    public int seed = 12345;

    private RoomGenerator roomGenerator;
    private CorridorGenerator corridorGenerator;
    private SeededRandom random;

    private DungeonLayout dungeonLayout;
    private readonly HashSet<Vector2Int> occupiedCells = new();

    public IReadOnlyList<DungeonRoom> GeneratedRooms => dungeonLayout?.Rooms;

    private void Start()
    {
        random = new SeededRandom(seed);
        Debug.Log($"Generating dungeon with seed: {seed}");

        if (!TryGenerateValidDungeon())
        {
            Debug.LogError($"Unable to generate a valid dungeon after {maxGenerationAttempts} attempts.");
            return;
        }

        SpawnRoomObjects();
        tilemapVisualizer.PaintFloorTiles(dungeonLayout.FloorTiles);
        WallGenerator.CreateWalls(dungeonLayout.FloorTiles, tilemapVisualizer);
        surface.BuildNavMesh();
    }

    private bool TryGenerateValidDungeon()
    {
        for (int attempt = 1; attempt <= maxGenerationAttempts; attempt++)
        {
            roomGenerator = new RoomGenerator(roomParameters, tilemapVisualizer, random);
            corridorGenerator = new CorridorGenerator();
            dungeonLayout = new DungeonLayout();

            GenerateDungeonLayout();
            if (DungeonValidator.IsValid(dungeonLayout, out string failureReason))
            {
                Debug.Log($"Dungeon validation passed on attempt {attempt}.");
                return true;
            }

            Debug.LogWarning($"Dungeon generation attempt {attempt} failed: {failureReason}");
        }

        return false;
    }

    private void GenerateDungeonLayout()
    {
        List<RoomType> roomSequence = BuildRoomSequence();
        Vector2Int nextRoomStart = Vector2Int.zero;

        for (int i = 0; i < roomSequence.Count; i++)
        {
            RoomType roomType = roomSequence[i];
            bool createCorridor = i < roomSequence.Count - 1;
            GenerateRoomCorridorPair(nextRoomStart, roomType, createCorridor);
            nextRoomStart = corridorGenerator.corridorEnd;
        }
    }

    private void SpawnRoomObjects()
    {
        occupiedCells.Clear();
        manager.enemyCount = 0;

        foreach (DungeonRoom room in dungeonLayout.Rooms)
        {
            switch (room.Type)
            {
                case RoomType.Start:
                    PlacePlayer(room);
                    break;
                case RoomType.Combat:
                    SpawnCombatMobs(room);
                    break;
                case RoomType.Treasure:
                    PlaceRoomPrefab(room, treasurePrefab, "Treasure");
                    break;
                case RoomType.Boss:
                    PlaceRoomPrefab(room, bossPrefab, "Boss");
                    break;
                case RoomType.Exit:
                    PlaceRoomPrefab(room, exitPrefab, "Exit");
                    break;
            }
        }

        manager.UpdateCounter();
    }

    private void PlacePlayer(DungeonRoom room)
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTransform = player.transform;
        }

        if (playerTransform == null)
        {
            Debug.LogError("Player Transform is not assigned and no GameObject named 'Player' was found.");
            return;
        }

        if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, spawnClearance,
                out Vector2Int spawnPoint))
        {
            Debug.LogError("No safe spawn point was found for Player.");
            return;
        }

        room.SetSpawnPoint(spawnPoint);
        playerTransform.position = PrefabPlacer.GetWorldPosition(spawnPoint);
    }

    private void SpawnCombatMobs(DungeonRoom room)
    {
        int numberOfMobs = random.Range(enemyInfo.Mob[0].min, enemyInfo.Mob[0].max);
        manager.enemyCount += PrefabPlacer.PlaceMobs(enemyInfo, room, numberOfMobs, random,
            occupiedCells, spawnClearance);
    }

    private void PlaceRoomPrefab(DungeonRoom room, GameObject prefab, string objectName)
    {
        if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, spawnClearance,
                out Vector2Int spawnPoint))
        {
            Debug.LogError($"No safe spawn point was found for {objectName}.");
            return;
        }

        room.SetSpawnPoint(spawnPoint);
        if (prefab == null)
        {
            Debug.LogWarning($"{objectName} prefab is not assigned. A safe spawn point was reserved at {spawnPoint}.");
            return;
        }

        Instantiate(prefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
    }

    private DungeonRoom GenerateRoomCorridorPair(Vector2Int startPosition, RoomType roomType, bool createCorridor)
    {
        HashSet<Vector2Int> roomFloor = roomGenerator.GenerateRoom(startPosition);
        DungeonRoom room = new DungeonRoom(roomType, roomFloor, GetRoomSpawnPoint(roomFloor));
        dungeonLayout.AddRoom(room);

        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();
        if(createCorridor)
            corridor = corridorGenerator.GenerateCorridor(random.Choose(GetOrderedPositions(roomFloor)), corridorLength);

        dungeonLayout.AddCorridor(corridor);
        Debug.Log($"Generated {roomType} room at {room.SpawnPoint}");
        return room;
    }

    private List<RoomType> BuildRoomSequence()
    {
        int totalRoomCount = Mathf.Max(numberOfRooms, 5);
        int combatRoomCount = totalRoomCount - 4;
        List<RoomType> roomSequence = new() { RoomType.Start };

        for (int i = 0; i < combatRoomCount; i++)
            roomSequence.Add(RoomType.Combat);

        roomSequence.Add(RoomType.Treasure);
        roomSequence.Add(RoomType.Boss);
        roomSequence.Add(RoomType.Exit);

        return roomSequence;
    }

    private static Vector2Int GetRoomSpawnPoint(IEnumerable<Vector2Int> roomTiles)
    {
        List<Vector2Int> orderedPositions = GetOrderedPositions(roomTiles);
        return orderedPositions[orderedPositions.Count / 2];
    }

    private static List<Vector2Int> GetOrderedPositions(IEnumerable<Vector2Int> positions)
    {
        return positions
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToList();
    }
}
