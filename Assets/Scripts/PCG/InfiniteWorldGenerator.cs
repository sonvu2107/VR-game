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
    [Tooltip("Gold coin prefab with GoldPickup script. Spawns in all levels.")]
    public GameObject goldPrefab;

    [Header("Decorations")]
    [Tooltip("Optional room decorator for auto-placing props.")]
    public RoomDecorator roomDecorator;

    [Header("Floor Configs (10 Levels)")]
    [Tooltip("Per-floor configuration. Index 0 = Floor 1, etc. Falls back to defaults if empty.")]
    public FloorConfigSO[] floorConfigs;

    [Header("Special Systems")]
    public DarknessController darknessController;

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
    public FloorConfigSO activeFloorConfig;

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

        // Auto-find components on same GameObject if not assigned
        if (roomDecorator == null)
            roomDecorator = GetComponent<RoomDecorator>();
        if (darknessController == null)
            darknessController = GetComponent<DarknessController>();
    }

    public bool GenerateFloor(int floorNumber, int runFloorCount, int floorSeed)
    {
        if (!HasRequiredReferences())
            return false;

        CleanupCurrentFloor();

        currentFloor = floorNumber;
        totalFloors = Mathf.Max(1, runFloorCount);
        random = new SeededRandom(floorSeed);

        // Load floor-specific config
        activeFloorConfig = null;
        if (floorConfigs != null && floorNumber - 1 < floorConfigs.Length)
            activeFloorConfig = floorConfigs[floorNumber - 1];

        // Apply floor config overrides
        if (activeFloorConfig != null)
        {
            numberOfRooms = activeFloorConfig.numberOfRooms;
            corridorLength = activeFloorConfig.corridorLength;

            string floorLabel = activeFloorConfig.floorName;
            Debug.Log($"Generating floor {currentFloor}/{totalFloors} [{floorLabel}] with seed {floorSeed}.");
        }
        else
        {
            Debug.Log($"Generating floor {currentFloor}/{totalFloors} with seed {floorSeed}.");
        }

        // Darkness system
        if (darknessController != null)
        {
            if (activeFloorConfig != null && activeFloorConfig.darkFloor)
                darknessController.EnableDarkness(activeFloorConfig.lightRadius);
            else
                darknessController.DisableDarkness();
        }

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

        // Auto-decorate rooms
        if (roomDecorator != null && dungeonLayout != null)
        {
            roomDecorator.DecorateRooms(
                dungeonLayout.Rooms,
                dungeonLayout.FloorTiles,
                occupiedCells,
                random,
                spawnedObjects);
        }

        // Spawn keys AFTER decorations so they can be hidden near deco objects
        SpawnKeysNearDecorations();

        // Spawn gold coins in all levels
        SpawnGoldCoins();

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
                    spawnedEnemyCount += SpawnCombatMobs(room); // Bổ sung quái bảo vệ rương
                    break;
                case RoomType.Boss:
                    bool shouldSpawnBoss = (activeFloorConfig != null && activeFloorConfig.hasBoss)
                                           || currentFloor == totalFloors;
                    if (shouldSpawnBoss)
                        spawnedEnemyCount += PlaceFinalBoss(room);
                    else
                        spawnedEnemyCount += SpawnCombatMobs(room); // Fallback to normal mobs if no boss
                    break;
                case RoomType.Exit:
                    floorExit = PlaceExit(room);
                    spawnedEnemyCount += SpawnCombatMobs(room); // Bổ sung quái chặn cửa ra
                    break;
            }
        }

    }

    /// <summary>
    /// Spawns keys hidden near existing decoration objects (trees, pots, etc.)
    /// so the player has to explore and search near props to find them.
    /// </summary>
    private void SpawnKeysNearDecorations()
    {
        if (activeFloorConfig == null || !activeFloorConfig.requireKeys || activeFloorConfig.keyPrefab == null)
            return;

        int keysToSpawn = activeFloorConfig.keysRequired;
        if (keysToSpawn <= 0) return;

        // Collect all decoration GameObjects from the spawned list
        List<GameObject> decoObjects = new();
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj == null) continue;
            // Decorations are named "Deco_Sprite" or contain a SpriteRenderer but no Enemy/FloorExit
            if (obj.name.StartsWith("Deco") || 
                (obj.GetComponent<SpriteRenderer>() != null 
                 && obj.GetComponent<Enemy>() == null 
                 && obj.GetComponent<FloorExit>() == null
                 && obj.GetComponent<DungeonKey>() == null))
            {
                decoObjects.Add(obj);
            }
        }

        // Shuffle deco list so keys are placed near random decorations
        for (int i = decoObjects.Count - 1; i > 0; i--)
        {
            int j = random.Range(0, i + 1);
            (decoObjects[i], decoObjects[j]) = (decoObjects[j], decoObjects[i]);
        }

        int placed = 0;
        foreach (GameObject deco in decoObjects)
        {
            if (placed >= keysToSpawn) break;

            // Place key with a small offset from the deco so it peeks out slightly
            Vector3 decoPos = deco.transform.position;
            float offsetX = random.Range(-1, 2) * 0.3f; // small random offset
            float offsetY = random.Range(-1, 2) * 0.3f;
            Vector3 keyPos = decoPos + new Vector3(offsetX, offsetY, 0f);

            GameObject key = Instantiate(activeFloorConfig.keyPrefab, keyPos, Quaternion.identity);
            spawnedObjects.Add(key);
            placed++;
        }

        // Fallback: if not enough decos, place remaining keys randomly in rooms
        if (placed < keysToSpawn)
        {
            List<DungeonRoom> validRooms = dungeonLayout.Rooms
                .Where(r => r.Type == RoomType.Combat || r.Type == RoomType.Treasure).ToList();

            for (int i = placed; i < keysToSpawn; i++)
            {
                if (validRooms.Count == 0) break;
                DungeonRoom room = validRooms[random.Range(0, validRooms.Count)];
                if (PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, 0, out Vector2Int keyPoint))
                {
                    GameObject key = Instantiate(activeFloorConfig.keyPrefab,
                        PrefabPlacer.GetWorldPosition(keyPoint), Quaternion.identity);
                    spawnedObjects.Add(key);
                }
            }
        }

        Debug.Log($"Floor {currentFloor}: Spawned {Mathf.Min(placed, keysToSpawn)} keys hidden near decorations.");
    }

    /// <summary>
    /// Spawns gold coins in combat and treasure rooms on every floor.
    /// </summary>
    private void SpawnGoldCoins()
    {
        if (goldPrefab == null) return;

        List<DungeonRoom> validRooms = dungeonLayout.Rooms
            .Where(r => r.Type == RoomType.Combat || r.Type == RoomType.Treasure)
            .ToList();

        foreach (DungeonRoom room in validRooms)
        {
            int coinCount = random.Range(2, 5); // 2-4 coins per room

            for (int i = 0; i < coinCount; i++)
            {
                if (PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, 0, out Vector2Int coinPoint))
                {
                    GameObject coin = Instantiate(goldPrefab,
                        PrefabPlacer.GetWorldPosition(coinPoint), Quaternion.identity);
                    spawnedObjects.Add(coin);
                }
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
        // Use floor-specific enemy info if available
        EnemyInfoSO activeEnemyInfo = (activeFloorConfig != null && activeFloorConfig.enemyInfo != null)
            ? activeFloorConfig.enemyInfo
            : enemyInfo;

        if (activeEnemyInfo == null || activeEnemyInfo.Mob.Count == 0)
        {
            Debug.LogError("Enemy spawn configuration is missing.");
            return 0;
        }

        int minMobs, maxMobs;
        if (activeFloorConfig != null)
        {
            minMobs = activeFloorConfig.minEnemiesPerRoom;
            maxMobs = activeFloorConfig.maxEnemiesPerRoom;
        }
        else
        {
            minMobs = activeEnemyInfo.Mob[0].min;
            maxMobs = activeEnemyInfo.Mob[0].max;
        }

        // Determine base number of mobs based on floor config
        int numberOfMobs = random.Range(minMobs, maxMobs + 1);
        
        // Add a slight scaling difficulty (+1 extra mob every 2 floors) to keep things exciting but manageable
        int difficultyBonus = currentFloor / 2;
        numberOfMobs += difficultyBonus;

        int placed = PrefabPlacer.PlaceMobs(activeEnemyInfo, room, numberOfMobs, random, occupiedCells,
            spawnClearance, spawnedObjects);

        // Spawn traps if floor config has them
        if (activeFloorConfig != null && activeFloorConfig.hasTraps)
        {
            int trapCount = activeFloorConfig.trapsPerRoom;
            for (int i = 0; i < trapCount; i++)
            {
                if (PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells,
                        0, out Vector2Int trapPoint))
                {
                    GameObject trap;
                    if (activeFloorConfig.trapPrefab != null)
                    {
                        trap = Instantiate(activeFloorConfig.trapPrefab,
                            PrefabPlacer.GetWorldPosition(trapPoint), Quaternion.identity);
                    }
                    else
                    {
                        // Auto-create trap if prefab is missing
                        trap = new GameObject("AutoSpikeTrap");
                        trap.transform.position = PrefabPlacer.GetWorldPosition(trapPoint);
                        
                        BoxCollider2D col = trap.AddComponent<BoxCollider2D>();
                        col.size = new Vector2(0.8f, 0.8f);
                        col.isTrigger = true;
                        
                        trap.AddComponent<SpikeTrap>(); // This will auto-create visuals
                    }
                    spawnedObjects.Add(trap);
                }
            }
        }

        return placed;
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

        // Use floor config boss prefab, fall back to default
        GameObject activeBossPrefab = (activeFloorConfig != null && activeFloorConfig.bossPrefab != null)
            ? activeFloorConfig.bossPrefab
            : bossPrefab;
        if (activeBossPrefab == null)
        {
            Debug.LogWarning("Boss prefab is not assigned for this floor.");
            return 0;
        }

        GameObject boss = Instantiate(activeBossPrefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
        
        // Tự động "hóa phép" biến quái thường thành Boss xịn nếu đây là phòng Boss
        Enemy enemyScript = boss.GetComponentInChildren<Enemy>();
        if (enemyScript != null)
        {
            // Tăng sức mạnh đột biến
            enemyScript.maxHealth = 150; // Máu trâu (chém mỏi tay)
            enemyScript.attackDamage += 2; // Đánh đau hơn
            enemyScript.showHealthBar = true; // Hiện thanh máu to trên đầu
            
            // Thay đổi ngoại hình để khác biệt hoàn toàn với lính thường
            boss.transform.localScale = new Vector3(2.5f, 2.5f, 1f); // Khổng lồ (gấp 2.5 lần)
            
            SpriteRenderer sr = boss.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(1f, 0.4f, 0.4f); // Nhuộm màu Đỏ Máu
            }
        }

        spawnedObjects.Add(boss);
        return enemyScript != null ? 1 : 0;
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
