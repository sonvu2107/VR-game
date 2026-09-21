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

    [Header("Generation")]
    public int corridorLength = 20;
    public int numberOfRooms = 10;
    [Min(1)] public int maxGenerationAttempts = 20;
    [Min(0)] public int spawnClearance = 1;

    private RoomGenerator roomGenerator;
    private CorridorGenerator corridorGenerator;
    private SeededRandom random;
    private DungeonLayout dungeonLayout;
    private readonly HashSet<Vector2Int> occupiedCells = new();
    private readonly List<GameObject> spawnedObjects = new();

    private int currentFloor;
    private int totalFloors;

    public IReadOnlyList<DungeonRoom> GeneratedRooms => dungeonLayout?.Rooms;

    private void Awake()
    {
        if (manager == null)
            manager = FindObjectOfType<GameManager>();

        if (playerTransform == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    public bool GenerateFloor(int floorNumber, int runFloorCount, int floorSeed)
    {
        if (!HasRequiredReferences())
            return false;

        CleanupCurrentFloor();

        currentFloor = floorNumber;
        totalFloors = Mathf.Max(1, runFloorCount);
        random = new SeededRandom(floorSeed);
        Debug.Log($"Generating floor {currentFloor}/{totalFloors} with seed {floorSeed}.");

        if (!TryGenerateValidDungeon())
        {
            Debug.LogError($"Unable to generate floor {currentFloor} after {maxGenerationAttempts} attempts.");
            return false;
        }

        tilemapVisualizer.PaintFloorTiles(dungeonLayout.FloorTiles);
        WallGenerator.CreateWalls(dungeonLayout.FloorTiles, tilemapVisualizer);
        surface.BuildNavMesh();

        SpawnRoomObjects(out FloorExit floorExit, out int spawnedEnemyCount);
        if (floorExit == null)
        {
            Debug.LogError($"Floor {currentFloor} has no usable exit.");
            return false;
        }

        manager.CompleteGeneration(floorExit, spawnedEnemyCount);
        return true;
    }

    private bool HasRequiredReferences()
    {
        if (surface != null && roomParameters != null && tilemapVisualizer != null && manager != null)
            return true;

        Debug.LogError("Dungeon generator is missing a required scene reference.");
        return false;
    }

    private void CleanupCurrentFloor()
    {
        foreach (GameObject spawnedObject in spawnedObjects)
        {
            if (spawnedObject == null)
                continue;

            spawnedObject.SetActive(false);
            Destroy(spawnedObject);
        }

        spawnedObjects.Clear();
        occupiedCells.Clear();
        dungeonLayout = null;

        if (surface != null)
            surface.RemoveData();

        tilemapVisualizer?.Clear();
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
                Debug.Log($"Floor {currentFloor} validation passed on attempt {attempt}.");
                return true;
            }

            Debug.LogWarning($"Floor {currentFloor}, generation attempt {attempt} failed: {failureReason}");
        }

        return false;
    }

    private void GenerateDungeonLayout()
    {
        List<RoomType> roomSequence = BuildRoomSequence();
        Vector2Int nextRoomStart = Vector2Int.zero;

        for (int i = 0; i < roomSequence.Count; i++)
        {
            bool createCorridor = i < roomSequence.Count - 1;
            GenerateRoomCorridorPair(nextRoomStart, roomSequence[i], createCorridor);
            nextRoomStart = corridorGenerator.corridorEnd;
        }
    }

    private void SpawnRoomObjects(out FloorExit floorExit, out int spawnedEnemyCount)
    {
        occupiedCells.Clear();
        floorExit = null;
        spawnedEnemyCount = 0;

        foreach (DungeonRoom room in dungeonLayout.Rooms)
        {
            switch (room.Type)
            {
                case RoomType.Start:
                    PlacePlayer(room);
                    break;
                case RoomType.Combat:
                    spawnedEnemyCount += SpawnCombatMobs(room);
                    break;
                case RoomType.Treasure:
                    PlaceOptionalRoomPrefab(room, treasurePrefab, "Treasure");
                    break;
                case RoomType.Boss:
                    if (currentFloor == totalFloors)
                        spawnedEnemyCount += PlaceFinalBoss(room);
                    break;
                case RoomType.Exit:
                    floorExit = PlaceExit(room);
                    break;
            }
        }
    }

    private void PlacePlayer(DungeonRoom room)
    {
        if (playerTransform == null)
        {
            Debug.LogError("Player Transform is not assigned and no GameObject named 'Player' was found.");
            return;
        }

        if (!TryClaimRoomSpawn(room, "Player", out Vector2Int spawnPoint))
            return;

        playerTransform.position = PrefabPlacer.GetWorldPosition(spawnPoint);
    }

    private int SpawnCombatMobs(DungeonRoom room)
    {
        if (enemyInfo == null || enemyInfo.Mob.Count == 0)
        {
            Debug.LogError("Enemy spawn configuration is missing.");
            return 0;
        }

        int difficultyBonus = Mathf.Max(0, currentFloor - 1);
        int numberOfMobs = random.Range(enemyInfo.Mob[0].min, enemyInfo.Mob[0].max) + difficultyBonus;
        return PrefabPlacer.PlaceMobs(enemyInfo, room, numberOfMobs, random, occupiedCells,
            spawnClearance, spawnedObjects);
    }

    private void PlaceOptionalRoomPrefab(DungeonRoom room, GameObject prefab, string objectName)
    {
        if (!TryClaimRoomSpawn(room, objectName, out Vector2Int spawnPoint))
            return;

        if (prefab == null)
        {
            Debug.LogWarning($"{objectName} prefab is not assigned. Spawn point {spawnPoint} was reserved.");
            return;
        }

        GameObject instance = Instantiate(prefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
        spawnedObjects.Add(instance);
    }

    private int PlaceFinalBoss(DungeonRoom room)
    {
        if (!TryClaimRoomSpawn(room, "Boss", out Vector2Int spawnPoint))
            return 0;

        if (bossPrefab == null)
        {
            Debug.LogWarning("Boss prefab is not assigned. The final floor will use its combat rooms.");
            return 0;
        }

        GameObject boss = Instantiate(bossPrefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
        spawnedObjects.Add(boss);
        return boss.GetComponentInChildren<Enemy>() != null ? 1 : 0;
    }

    private FloorExit PlaceExit(DungeonRoom room)
    {
        if (!TryClaimRoomSpawn(room, "Exit", out Vector2Int spawnPoint))
            return null;

        Vector3 worldPosition = PrefabPlacer.GetWorldPosition(spawnPoint);
        GameObject exitObject = exitPrefab != null
            ? Instantiate(exitPrefab, worldPosition, Quaternion.identity)
            : CreateRuntimeExit(worldPosition);

        spawnedObjects.Add(exitObject);

        FloorExit floorExit = exitObject.GetComponent<FloorExit>();
        if (floorExit == null)
            floorExit = exitObject.AddComponent<FloorExit>();

        floorExit.Initialize(manager);
        return floorExit;
    }

    private static GameObject CreateRuntimeExit(Vector3 worldPosition)
    {
        GameObject exitObject = new("Floor Exit");
        exitObject.transform.position = worldPosition;
        exitObject.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

        SpriteRenderer renderer = exitObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f), 1f);
        renderer.sortingOrder = 2;

        CircleCollider2D exitCollider = exitObject.AddComponent<CircleCollider2D>();
        exitCollider.radius = 0.5f;
        exitCollider.isTrigger = true;
        exitObject.AddComponent<FloorExit>();
        return exitObject;
    }

    private bool TryClaimRoomSpawn(DungeonRoom room, string objectName, out Vector2Int spawnPoint)
    {
        if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, spawnClearance,
                out spawnPoint))
        {
            Debug.LogError($"No safe spawn point was found for {objectName}.");
            return false;
        }

        room.SetSpawnPoint(spawnPoint);
        return true;
    }

    private DungeonRoom GenerateRoomCorridorPair(Vector2Int startPosition, RoomType roomType, bool createCorridor)
    {
        HashSet<Vector2Int> roomFloor = roomGenerator.GenerateRoom(startPosition);
        DungeonRoom room = new(roomType, roomFloor, GetRoomSpawnPoint(roomFloor));
        dungeonLayout.AddRoom(room);

        HashSet<Vector2Int> corridor = new();
        if (createCorridor)
            corridor = corridorGenerator.GenerateCorridor(random.Choose(GetOrderedPositions(roomFloor)), corridorLength);

        dungeonLayout.AddCorridor(corridor);
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
