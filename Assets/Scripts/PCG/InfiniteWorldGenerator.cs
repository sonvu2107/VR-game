// Sinh 6-7 phòng cho mỗi level. Quái được rải theo từng phòng sau khi giữ chỗ
// cho người chơi, vật phẩm, boss và cổng; số đếm chỉ gồm quái thật đã tạo.
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

    [Header("Level Enemy Prefabs")]
    [SerializeField] private GameObject skeletonArcherPrefab;
    [SerializeField] private GameObject poisonSlimePrefab;
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private GameObject darkMagePrefab;
    [SerializeField] private GameObject eliteEnemyPrefab;
    [SerializeField] private GameObject miniBossPrefab;
    [SerializeField] private GameObject skullPrefab;
    [SerializeField] private GameObject vampirePrefab;
    [SerializeField] private EnemyRosterSO additionalEnemyRoster;

    [Header("Optional Level Mechanic Prefabs")]
    [Tooltip("Uses a generated key visual when unassigned.")]
    [SerializeField] private GameObject keyPrefab;
    [Tooltip("Uses a generated spike visual when unassigned.")]
    [SerializeField] private GameObject spikeTrapPrefab;

    [Header("Generation")]
    public int corridorLength = 20;
    public int numberOfRooms = 10;
    [Min(1)] public int maxGenerationAttempts = 20;
    [Min(0)] public int spawnClearance = 1;

    [Header("Optional Courtyard Prototype")]
    [Tooltip("Opt-in only. Existing scenes keep the Random Walk layout.")]
    public CourtyardTheme courtyardTheme;
    [Header("Campaign Map Progression")]
    [Tooltip("Optional: switch to this map from level 2 without resetting the session.")]
    [SerializeField] private CourtyardTheme campaignSecondMap;
    [SerializeField] private CampaignMapSet campaignMaps;
    // Không thay serialized theme gốc khi đổi level, tránh ghi nhầm Arcane lên map đầu.
    public CourtyardTheme ActiveTheme
    {
        get
        {
            if (campaignMaps != null && campaignMaps.TryGetTheme(currentLevel, out var theme)) return theme;
            return campaignSecondMap != null && currentLevel >= 2 ? campaignSecondMap : courtyardTheme;
        }
    }

    private RoomGenerator roomGenerator;
    private CorridorGenerator corridorGenerator;
    private SeededRandom random;
    private DungeonLayout dungeonLayout;
    private readonly HashSet<Vector2Int> occupiedCells = new();
    private readonly List<GameObject> spawnedObjects = new();
    private DarknessController darknessController;
    public TacticalTrapPlanner LastTrapPlan { get; private set; }

    private int currentLevel;
    private int totalLevels;
    private LevelDefinition currentLevelDefinition;

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

        darknessController = GetComponent<DarknessController>();
    }

    public bool GenerateFloor(int floorNumber, int runFloorCount, int floorSeed)
    {
        LevelDefinition legacyDefinition = new(
            floorNumber,
            $"Floor {floorNumber}",
            numberOfRooms,
            Mathf.Max(0, floorNumber - 1),
            floorNumber >= runFloorCount ? LevelType.FinalBoss : LevelType.Standard);
        return GenerateLevel(legacyDefinition, runFloorCount, floorSeed);
    }

    public bool GenerateLevel(LevelDefinition levelDefinition, int runLevelCount, int levelSeed)
    {
        if (!HasRequiredReferences() || levelDefinition == null)
            return false;

        CleanupCurrentFloor();


        currentLevelDefinition = levelDefinition;
        currentLevel = levelDefinition.LevelNumber;
        totalLevels = Mathf.Max(1, runLevelCount);
        random = new SeededRandom(levelSeed);
        Debug.Log($"Generating level {currentLevel}/{totalLevels} ({levelDefinition.DisplayName}) with seed {levelSeed}.");

        if (!TryGenerateValidDungeon())
        {
            Debug.LogError($"Unable to generate level {currentLevel} after {maxGenerationAttempts} attempts.");
            return false;
        }

        tilemapVisualizer.PaintFloorTiles(dungeonLayout.FloorTiles);
        WallGenerator.CreateWalls(dungeonLayout.FloorTiles, tilemapVisualizer);
        if (ActiveTheme != null && ActiveTheme.UsesArcaneArt)
        {
            // Static props and non-walkable volumes must exist BEFORE the bake.
            // Their footprint cells also keep player/items/enemies from spawning inside them.
            tilemapVisualizer.SetMapArtVisible(false);
            spawnedObjects.Add(ArcaneCitadelGeometry.Build(dungeonLayout, ActiveTheme, occupiedCells));
            Physics2D.SyncTransforms();
        }
        tilemapVisualizer.SynchronizeColliders();
        surface.BuildNavMesh();

        SpawnRoomObjects(out FloorExit floorExit, out int spawnedEnemyCount);
        if (floorExit == null)
        {
            Debug.LogError($"Level {currentLevel} has no usable exit.");
            return false;
        }

        if (!PlaceRequiredKeys())
        {
            Debug.LogError($"Level {currentLevel} could not place every required key.");
            return false;
        }

        PlaceTacticalTraps();
        ConfigureLevelLighting();

        if (ActiveTheme == null || !ActiveTheme.UsesArcaneArt)
        {
            tilemapVisualizer.SetMapArtVisible(true);
            DungeonDecorator.DecorateDungeon(
                dungeonLayout,
                random,
                occupiedCells,
                spawnedObjects,
                currentLevel,
                currentLevelDefinition.Type, ActiveTheme != null);

            if (ActiveTheme != null)
                CourtyardDecorator.Decorate(dungeonLayout, ActiveTheme, random, occupiedCells, spawnedObjects);
            spawnedObjects.Add(ArcaneCitadelGeometry.BuildAmbienceOnly(dungeonLayout, ActiveTheme));
        }

        manager.CompleteLevelGeneration(floorExit, spawnedEnemyCount);
        return true;
    }

    private bool HasRequiredReferences()
    {
        if (surface != null && roomParameters != null && tilemapVisualizer != null && manager != null &&
            enemyInfo != null && enemyInfo.Mob.Count > 0 && enemyInfo.Mob[0].sprite != null &&
            skeletonArcherPrefab != null && poisonSlimePrefab != null && batPrefab != null &&
            darkMagePrefab != null && eliteEnemyPrefab != null && miniBossPrefab != null && bossPrefab != null)
            return true;

        Debug.LogError("Dungeon generator is missing a scene or level enemy prefab reference.");
        return false;
    }

    private void CleanupCurrentFloor()
    {
        darknessController?.DisableDarkness();

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
                Debug.Log($"Level {currentLevel} validation passed on attempt {attempt}.");
                return true;
            }

            Debug.LogWarning($"Level {currentLevel}, generation attempt {attempt} failed: {failureReason}");
        }

        return false;
    }

    private void GenerateDungeonLayout()
    {
        List<RoomType> roomSequence = BuildRoomSequence();
        if (ActiveTheme != null)
        {
            dungeonLayout = ActiveTheme.usesCampaignAtlas
                ? CampaignDungeonLayout.Generate(roomSequence, random, ActiveTheme.layoutStyle)
                : CourtyardLayout.Generate(roomSequence, random);
            return;
        }
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
        floorExit = null;
        spawnedEnemyCount = 0;

        DungeonRoom widestCombatRoom = dungeonLayout.Rooms
            .Where(candidate => candidate.Type == RoomType.Combat)
            .OrderByDescending(candidate => candidate.FloorTiles.Count)
            .FirstOrDefault();
        int combatRoomIndex = 0;
        for (int roomIndex = 0; roomIndex < dungeonLayout.Rooms.Count; roomIndex++)
        {
            DungeonRoom room = dungeonLayout.Rooms[roomIndex];
            // Place one potion in generated room 3 and one in generated room 5.
            if (roomIndex == 2 || roomIndex == 4)
                PlaceHealingPickup(room);

            switch (room.Type)
            {
                case RoomType.Start:
                    PlacePlayer(room);
                    spawnedEnemyCount += SpawnRoomMobs(room, -1, false,
                        GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type));
                    break;
                case RoomType.Combat:
                    spawnedEnemyCount += SpawnRoomMobs(room, combatRoomIndex, room == widestCombatRoom,
                        GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type));
                    combatRoomIndex++;
                    break;
                case RoomType.Treasure:
                    PlaceOptionalRoomPrefab(room, treasurePrefab, "Treasure");
                    spawnedEnemyCount += SpawnRoomMobs(room, combatRoomIndex, false,
                        GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type));
                    break;
                case RoomType.Boss:
                    if (currentLevelDefinition.Type != LevelType.Standard)
                        spawnedEnemyCount += PlaceLevelBoss(room);
                    else
                        spawnedEnemyCount += SpawnRoomMobs(room, combatRoomIndex, false,
                            GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type));
                    break;
                case RoomType.Exit:
                    floorExit = PlaceExit(room);
                    spawnedEnemyCount += SpawnRoomMobs(room, combatRoomIndex, false,
                        GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type));
                    break;
            }
            Debug.Log($"Level {currentLevel} room {roomIndex + 1}/{dungeonLayout.Rooms.Count} ({room.Type}): {spawnedEnemyCount} enemies placed so far.");
        }
    }

    // Phòng đầu/cuối nhẹ hơn; phòng boss thật không có add để người chơi đọc đòn boss.
    public static int GetRoomEnemyQuota(RoomType roomType, LevelType levelType)
    {
        return roomType switch
        {
            RoomType.Start or RoomType.Exit => 1,
            RoomType.Combat => 3,
            RoomType.Treasure => 2,
            RoomType.Boss when levelType == LevelType.Standard => 3,
            _ => 0
        };
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

    // Phòng chiến đấu dùng bộ ba vai trò; phòng boss thật dành riêng cho boss.
    private int SpawnRoomMobs(DungeonRoom room, int combatRoomIndex, bool isWideRoom, int numberOfMobs)
    {
        if (enemyInfo == null || enemyInfo.Mob.Count == 0)
        {
            Debug.LogError("Enemy spawn configuration is missing.");
            return 0;
        }

        List<GameObject> prefabs = new(numberOfMobs);
        if (room.Type == RoomType.Combat || room.Type == RoomType.Boss)
        {
            var squad = GetCombatSquad();
            if (squad.Count == 3)
                return PrefabPlacer.PlaceMobs(squad, room, random, occupiedCells, spawnClearance, spawnedObjects);
        }
        for (int mobIndex = 0; mobIndex < numberOfMobs; mobIndex++)
            prefabs.Add(combatRoomIndex < 0 ? enemyInfo.Mob[0].sprite :
                ChooseEnemyPrefab(combatRoomIndex, mobIndex, isWideRoom));

        return PrefabPlacer.PlaceMobs(prefabs, room, random, occupiedCells,
            spawnClearance, spawnedObjects);
    }

    public List<GameObject> GetCombatSquad()
    {
        var squad = new List<GameObject>();
        foreach (string name in new[] { "GoblinTorch", "GoblinTNT", "GoblinBarrel" })
        {
            var entry = additionalEnemyRoster?.entries?.FirstOrDefault(item => item?.prefab != null && item.prefab.name == name);
            if (entry == null) return new List<GameObject>();
            squad.Add(entry.prefab);
        }
        return squad;
    }

    public List<GameObject> GetFinalBossCandidates()
    {
        var pool = new List<GameObject>();
        if (enemyInfo != null) foreach (var mob in enemyInfo.Mob)
            if (mob.sprite != null && mob.sprite.name != "SkeletonScout" && mob.sprite.name != "SkeletonGuard" &&
                !pool.Contains(mob.sprite)) pool.Add(mob.sprite);
        foreach (var prefab in new[] { skeletonArcherPrefab, poisonSlimePrefab, batPrefab, darkMagePrefab,
            eliteEnemyPrefab, skullPrefab, vampirePrefab, miniBossPrefab, bossPrefab })
            if (prefab != null && !pool.Contains(prefab)) pool.Add(prefab);
        foreach (var prefab in GetCombatSquad()) if (!pool.Contains(prefab)) pool.Add(prefab);
        return pool;
    }

    private GameObject ChooseEnemyPrefab(int combatRoomIndex, int mobIndex, bool isWideRoom)
    {
        GameObject original = ChooseOriginalEnemyPrefab(combatRoomIndex, mobIndex, isWideRoom);
        bool protectedSlot = mobIndex == 0 && (combatRoomIndex == 0 && currentLevel is 2 or 3 or 4 or 6 or 7 ||
            isWideRoom && currentLevel is 8 or 9 || currentLevel == 7 && combatRoomIndex == 1);
        return protectedSlot || additionalEnemyRoster == null ? original :
            additionalEnemyRoster.Choose(original, currentLevel, combatRoomIndex, mobIndex, isWideRoom, random);
    }

    private GameObject ChooseOriginalEnemyPrefab(int combatRoomIndex, int mobIndex, bool isWideRoom)
    {
        GameObject warrior = enemyInfo.Mob[0].sprite;
        bool firstInFirstCombatRoom = combatRoomIndex == 0 && mobIndex == 0;
        // Optional references preserve older scenes until their content is upgraded.
        GameObject skull = skullPrefab != null ? skullPrefab : warrior;
        GameObject vampire = vampirePrefab != null ? vampirePrefab : eliteEnemyPrefab;
        if (currentLevel <= 1) return warrior;
        if (currentLevel == 2)
            return firstInFirstCombatRoom ? skull : ChooseWeighted(warrior, skull);
        if (currentLevel == 3)
            return firstInFirstCombatRoom ? skeletonArcherPrefab :
                ChooseWeighted(warrior, skeletonArcherPrefab, skull);
        if (currentLevel == 4)
        {
            if (firstInFirstCombatRoom) return poisonSlimePrefab;
            return ChooseWeighted(warrior, skeletonArcherPrefab, poisonSlimePrefab, skull);
        }
        if (currentLevel == 5)
            return ChooseWeighted(warrior, skeletonArcherPrefab, poisonSlimePrefab, skull);
        if (currentLevel == 6)
        {
            if (firstInFirstCombatRoom) return batPrefab;
            return ChooseWeighted(warrior, skeletonArcherPrefab, batPrefab, skull);
        }
        if (currentLevel == 7)
        {
            if (firstInFirstCombatRoom) return eliteEnemyPrefab;
            if (combatRoomIndex == 1 && mobIndex == 0) return vampire;
            return ChooseWeighted(warrior, skeletonArcherPrefab, poisonSlimePrefab,
                batPrefab, eliteEnemyPrefab, skull, vampire);
        }
        if (currentLevel == 8 && isWideRoom && mobIndex == 0)
            return darkMagePrefab;
        if (currentLevel == 8)
            return ChooseWeighted(warrior, skeletonArcherPrefab, poisonSlimePrefab,
                eliteEnemyPrefab, skull, vampire);
        if (currentLevel == 9)
        {
            if (isWideRoom && mobIndex == 0) return darkMagePrefab;
            return ChooseWeighted(warrior, skeletonArcherPrefab, poisonSlimePrefab,
                batPrefab, eliteEnemyPrefab, skull, vampire);
        }
        // Level 10 reserves its separate Boss room for Skeleton King.
        return ChooseWeighted(warrior, warrior, eliteEnemyPrefab, vampire);
    }

    private GameObject ChooseWeighted(params GameObject[] prefabs)
    {
        return prefabs[random.Range(0, prefabs.Length)];
    }

    private void PlaceHealingPickup(DungeonRoom room)
    {
        if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells,
                spawnClearance, out Vector2Int spawnPoint))
        {
            Debug.LogWarning($"Level {currentLevel}: no safe tile for a potion in room {room.Type}.");
            return;
        }

        HealingPickup pickup = HealingPickup.Create(PrefabPlacer.GetWorldPosition(spawnPoint));
        spawnedObjects.Add(pickup.gameObject);
    }

    private void PlaceTacticalTraps()
    {
        bool arcane = ActiveTheme != null && ActiveTheme.UsesArcaneArt;
        Physics2D.SyncTransforms();
        LastTrapPlan = new TacticalTrapPlanner(dungeonLayout, spawnedObjects, playerTransform, arcane);
        // Decor được sinh sau bước này phải nhường cả lối đi và tầm nhìn của bẫy.
        occupiedCells.UnionWith(LastTrapPlan.Reserved);
        if (!LastTrapPlan.RoutesVerified)
        {
            Debug.LogWarning("TACTICAL_TRAPS_SKIPPED: " + LastTrapPlan.FailureReason + "; no hazards added.");
            return;
        }
        int budget = Mathf.Max(currentLevelDefinition.TrapsPerCombatRoom,
            arcane ? ActiveTheme.arcaneTrapsPerCombatRoom : currentLevel >= 3 ? 2 : 0);
        budget = Mathf.Min(budget, currentLevel == 1 ? 1 : currentLevel < 4 ? 2 : currentLevel < 7 ? 3 : 4);
        foreach (DungeonRoom room in dungeonLayout.Rooms)
        {
            if (room.Type == RoomType.Start || room.Type == RoomType.Exit || budget == 0) continue;
            var pattern = room.Type == RoomType.Boss ? TacticalTrapPattern.BossFlanks :
                room.Type == RoomType.Treasure ? TacticalTrapPattern.TreasureGuard :
                random.Range(0, 2) == 0 ? TacticalTrapPattern.Chicane : TacticalTrapPattern.RangedScreen;
            Vector2Int focus = room.SpawnPoint;
            foreach (var obj in spawnedObjects)
            {
                if (obj == null) continue;
                var position = obj.transform.position;
                var cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y - 1f));
                if (!room.FloorTiles.Contains(cell)) continue;
                if (pattern == TacticalTrapPattern.RangedScreen && obj.GetComponent<EnemyHealth>()?.Config?.displayName == "GoblinTNT" ||
                    pattern == TacticalTrapPattern.TreasureGuard && obj.GetComponent<DungeonKey>() != null) focus = cell;
                if (room.Type == RoomType.Boss && (obj.GetComponent<CampaignFinalBoss>() != null || obj.GetComponent<MiniBossController>() != null))
                {
                    var visual = obj.GetComponent<EnemySheetAnimation>()?.Renderer ?? obj.GetComponent<EnemySpriteAnimation>()?.Renderer ?? obj.GetComponentInChildren<SpriteRenderer>();
                    if (visual == null) continue;
                    Bounds safe = visual.bounds; safe.Expand(3f);
                    foreach (var tile in room.FloorTiles)
                    {
                        Vector3 p = LastTrapPlan.Position(tile); p.z = safe.center.z;
                        if (safe.Contains(p)) occupiedCells.Add(tile);
                    }
                }
            }
            var cells = LastTrapPlan.Plan(room, pattern, room.Type == RoomType.Treasure ? Mathf.Max(1, budget / 2) : budget,
                focus, random.Range(0, 4), occupiedCells);
            if (cells.Count == 0) continue;
            var root = new GameObject("Tactical Traps - " + pattern);
            var group = root.AddComponent<TacticalTrapGroup>();
            group.Initialize(pattern, cells.ToArray(), cells.Min(cell => LastTrapPlan.DistanceToSafeRoute(LastTrapPlan.Position(cell))));
            spawnedObjects.Add(root);
            float rest = currentLevel < 4 ? 3.2f : 2.7f;
            foreach (var cell in cells)
            {
                Vector3 position = LastTrapPlan.Position(cell);
                var obj = spikeTrapPrefab != null ? Instantiate(spikeTrapPrefab, position, Quaternion.identity) : CreateRuntimeSpikeTrap(position);
                obj.transform.SetParent(root.transform, true);
                var trap = obj.GetComponent<SpikeTrap>() ?? obj.AddComponent<SpikeTrap>();
                trap.Initialize(currentLevel);
                trap.ConfigureTacticalCycle(rest, 0.85f, 0.9f);
                if (arcane) trap.ConfigureAtlasVisual(ActiveTheme.arcaneSpike, ActiveTheme.usesCampaignAtlas
                    ? Enumerable.Range(12, 4).Select(ActiveTheme.AtlasUv).ToArray() : null);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) occupiedCells.Add(cell + new Vector2Int(x, y));
            }
            Debug.Log($"TACTICAL_TRAP_GROUP level={currentLevel} pattern={pattern} count={cells.Count} clearance={group.MinimumClearance:F2}");
        }
    }

    private bool PlaceRequiredKeys()
    {
        int keyCount = currentLevelDefinition?.RequiredKeys ?? 0;
        if (keyCount <= 0)
            return true;

        List<DungeonRoom> candidateRooms = dungeonLayout.Rooms
            .Where(room => room.Type is RoomType.Combat or RoomType.Treasure or RoomType.Boss)
            .ToList();
        if (candidateRooms.Count == 0)
            return false;

        for (int index = 0; index < keyCount; index++)
        {
            bool placed = false;
            for (int roomOffset = 0; roomOffset < candidateRooms.Count; roomOffset++)
            {
                DungeonRoom room = candidateRooms[(index + roomOffset) % candidateRooms.Count];
                if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells,
                        spawnClearance, out Vector2Int spawnPoint))
                    continue;

                GameObject keyObject = keyPrefab != null
                    ? Instantiate(keyPrefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity)
                    : CreateRuntimeKey(PrefabPlacer.GetWorldPosition(spawnPoint));

                DungeonKey key = keyObject.GetComponent<DungeonKey>();
                if (key == null)
                    key = keyObject.AddComponent<DungeonKey>();
                key.Initialize(manager);
                spawnedObjects.Add(keyObject);
                placed = true;
                break;
            }

            if (!placed)
                return false;
        }

        return true;
    }

    private void ConfigureLevelLighting()
    {
        float radius = currentLevelDefinition?.DarknessRadius ?? 0f;
        if (radius <= 0f)
        {
            darknessController?.DisableDarkness();
            return;
        }

        if (darknessController == null)
            darknessController = gameObject.AddComponent<DarknessController>();
        darknessController.EnableDarkness(playerTransform, radius);
    }

    private static GameObject CreateRuntimeKey(Vector3 worldPosition)
    {
        GameObject keyObject = new("Dungeon Key");
        keyObject.transform.position = worldPosition;
        keyObject.AddComponent<CircleCollider2D>().isTrigger = true;
        keyObject.AddComponent<DungeonKey>();
        return keyObject;
    }

    private static GameObject CreateRuntimeSpikeTrap(Vector3 worldPosition)
    {
        GameObject trapObject = new("Spike Trap");
        trapObject.transform.position = worldPosition;
        trapObject.AddComponent<BoxCollider2D>().isTrigger = true;
        trapObject.AddComponent<SpikeTrap>();
        return trapObject;
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

    private int PlaceLevelBoss(DungeonRoom room)
    {
        if (!TryClaimRoomSpawn(room, "Boss", out Vector2Int spawnPoint))
            return 0;

        GameObject selectedPrefab = currentLevelDefinition.IsFinalBossLevel ? bossPrefab : miniBossPrefab;
        if (currentLevelDefinition.IsFinalBossLevel)
        {
            var candidates = GetFinalBossCandidates();
            selectedPrefab = candidates[random.Range(0, candidates.Count)];
            var finalBoss = CampaignFinalBoss.Create(selectedPrefab, room, PrefabPlacer.GetWorldPosition(spawnPoint));
            spawnedObjects.Add(finalBoss.gameObject);
            return 1;
        }
        if (selectedPrefab == null)
        {
            Debug.LogError($"Level {currentLevel} boss prefab is missing.");
            return 0;
        }

        GameObject boss = Instantiate(selectedPrefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
        spawnedObjects.Add(boss);
        // Bosses remain inside their own room instead of chasing through a
        // corridor or walking their large sprite beyond the room walls.
        EnemyMovement bossMovement = boss.GetComponent<EnemyMovement>();
        SpriteRenderer bossVisual = boss.GetComponentInChildren<SpriteRenderer>();
        if (bossMovement != null)
            bossMovement.ConfigureBossArena(room.FloorTiles,
                bossVisual != null ? (Vector2)bossVisual.bounds.extents : Vector2.one);
        EnemyHealth health = boss.GetComponent<EnemyHealth>();
        if (health == null)
        {
            Debug.LogError($"Level {currentLevel} boss prefab has no EnemyHealth component.");
            return 0;
        }

        // Level 9 uses a tougher Bone Sentinel without modifying the shared asset from level 5.
        if (currentLevel == 9 && health.Config != null)
        {
            EnemyConfigSO variant = Instantiate(health.Config);
            variant.maxHealth = 260;
            variant.attackCooldown = 1.75f;
            health.Configure(variant);
            boss.GetComponent<EnemyCombat>()?.Configure(variant);
            boss.GetComponent<EnemySummoner>()?.Configure(variant);
            boss.GetComponent<MiniBossController>()?.SetVariantColor(new Color(0.9f, 0.72f, 1f));
        }

        return 1;
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
        exitObject.AddComponent<PortalVisual>();

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
        if (roomType == RoomType.Boss && currentLevelDefinition.Type != LevelType.Standard)
        {
            // A predictable clear arena gives the large mini/final boss room to
            // move without its artwork clipping the procedural outer walls.
            for (int x = -8; x <= 8; x++)
            for (int y = -8; y <= 8; y++)
                roomFloor.Add(startPosition + new Vector2Int(x, y));
        }
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
        int totalRoomCount = currentLevelDefinition?.RoomCount ?? Mathf.Max(numberOfRooms, 5);
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
