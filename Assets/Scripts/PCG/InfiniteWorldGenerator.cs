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
    [Tooltip("Optional: use a distinct map only for level 3 without resetting the session.")]
    [SerializeField] private CourtyardTheme campaignThirdMap;
    [SerializeField] private CampaignMapSet campaignMaps;
    // Không thay serialized theme gốc khi đổi level, tránh ghi nhầm Arcane lên map đầu.
    public CourtyardTheme ActiveTheme
    {
        get
        {
            if (currentLevel == 3 && campaignThirdMap != null) return campaignThirdMap;
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
    public IReadOnlyCollection<Vector2Int> GeneratedFloorTiles => dungeonLayout?.FloorTiles;

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
        if (ActiveTheme != null && ActiveTheme.UsesCustomGeometry)
        {
            // Static props and non-walkable volumes must exist BEFORE the bake.
            // Their footprint cells also keep player/items/enemies from spawning inside them.
            tilemapVisualizer.SetMapArtVisible(false);
            spawnedObjects.Add(ActiveTheme.UsesArrowCryptArt
                ? ArrowCryptGeometry.Build(dungeonLayout, ActiveTheme, occupiedCells)
                : ArcaneCitadelGeometry.Build(dungeonLayout, ActiveTheme, occupiedCells));
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

        if (ActiveTheme == null || !ActiveTheme.UsesCustomGeometry)
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
            spawnedObjects.Add(ArcaneCitadelGeometry.BuildAmbienceOnly(dungeonLayout, ActiveTheme, occupiedCells));
        }

        // Đồ trang trí sinh sau item/bẫy vẫn phải được tính vào đường đi của quái.
        Physics2D.SyncTransforms();
        if (ActiveTheme == null || !ActiveTheme.UsesArcaneArt) surface.BuildNavMesh();
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
                    // Mỗi tầng có một phòng yên tĩnh trước nhịp khám phá.
                    // Suất lính canh được chuyển sang phòng chiến đấu đầu tiên.
                    break;
                case RoomType.Combat:
                    spawnedEnemyCount += SpawnRoomMobs(room, combatRoomIndex, room == widestCombatRoom,
                        GetRoomEnemyQuota(room.Type, currentLevelDefinition.Type) +
                        (combatRoomIndex == 0 ? GetRoomEnemyQuota(RoomType.Start, currentLevelDefinition.Type) : 0));
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

        Vector3 spawnPosition = PrefabPlacer.GetWorldPosition(spawnPoint);
        playerTransform.position = spawnPosition;
        PlayerMovement movement = playerTransform.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ConfigureWalkableArea(dungeonLayout.FloorTiles, spawnPosition);
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
            {
                // Giữ bộ ba vai trò và nhận thêm lính được chuyển khỏi phòng đầu.
                while (squad.Count < numberOfMobs) squad.Add(enemyInfo.Mob[0].sprite);
                return PrefabPlacer.PlaceMobs(squad, room, random, occupiedCells, spawnClearance, spawnedObjects);
            }
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
        // Cốt truyện cố định: Vua Xương giữ đường trở về, không chọn quái thường làm boss.
        return bossPrefab != null && bossPrefab.GetComponent<SkeletonKingController>() != null
            ? new List<GameObject> { bossPrefab } : new List<GameObject>();
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
        bool arrowCrypt = ActiveTheme != null && ActiveTheme.UsesArrowCryptArt;
        bool themedLayout = arcane || arrowCrypt;
        Physics2D.SyncTransforms();
        LastTrapPlan = new TacticalTrapPlanner(dungeonLayout, spawnedObjects, playerTransform, themedLayout);
        // Decor được sinh sau bước này phải nhường cả lối đi và tầm nhìn của bẫy.
        occupiedCells.UnionWith(LastTrapPlan.Reserved);
        if (!LastTrapPlan.RoutesVerified)
        {
            Debug.LogWarning("TACTICAL_TRAPS_SKIPPED: " + LastTrapPlan.FailureReason + "; no hazards added.");
            return;
        }
        int themedBudget = arcane ? ActiveTheme.arcaneTrapsPerCombatRoom :
            arrowCrypt ? ActiveTheme.arrowCryptTrapsPerCombatRoom : 0;
        int budget = Mathf.Max(currentLevelDefinition.TrapsPerCombatRoom,
            themedLayout ? themedBudget : currentLevel >= 3 ? 2 : 0);
        budget = Mathf.Min(budget, currentLevel == 1 ? 1 : currentLevel < 4 ? 2 : currentLevel < 7 ? 3 : 4);
        foreach (DungeonRoom room in dungeonLayout.Rooms)
        {
            if (room.Type == RoomType.Start || room.Type == RoomType.Exit || budget == 0) continue;
            var pattern = room.Type == RoomType.Boss ? TacticalTrapPattern.BossFlanks :
                room.Type == RoomType.Treasure ? TacticalTrapPattern.TreasureGuard :
                TacticalTrapPattern.DoorwayChoke;
            Vector2Int focus = room.SpawnPoint;
            Vector2Int? rangedFocus = null;
            foreach (var obj in spawnedObjects)
            {
                if (obj == null) continue;
                var position = obj.transform.position;
                // Cả map gốc và atlas dùng cùng phép đo vị trí của planner, tránh lệch một ô.
                var cell = LastTrapPlan.Cell(position);
                if (!room.FloorTiles.Contains(cell)) continue;
                var config = obj.GetComponent<EnemyHealth>()?.Config;
                if (config != null && (config.archetype == EnemyArchetype.Ranged || config.archetype == EnemyArchetype.Summoner))
                    rangedFocus ??= cell;
                if (pattern == TacticalTrapPattern.TreasureGuard && obj.GetComponent<DungeonKey>() != null) focus = cell;
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
                focus, 0, occupiedCells);
            // Chỉ dùng lá chắn quái đánh xa khi cửa không đủ chỗ cho một cụm an toàn.
            if (cells.Count == 0 && pattern == TacticalTrapPattern.DoorwayChoke && rangedFocus.HasValue)
            {
                pattern = TacticalTrapPattern.RangedScreen;
                cells = LastTrapPlan.Plan(room, pattern, budget, rangedFocus.Value, 0, occupiedCells);
            }
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
                else if (arrowCrypt) trap.ConfigureArrowCryptVisual();
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
        int formationCount = currentLevelDefinition.IsFinalBossLevel ? SpawnThroneFormation(room) : 0;
        if (!TryClaimRoomSpawn(room, "Boss", out Vector2Int spawnPoint))
            return formationCount;

        GameObject selectedPrefab = currentLevelDefinition.IsFinalBossLevel ? bossPrefab : miniBossPrefab;
        if (currentLevelDefinition.IsFinalBossLevel)
        {
            var candidates = GetFinalBossCandidates();
            if (candidates.Count == 0)
            {
                Debug.LogError("Thiếu prefab Vua Xương có SkeletonKingController.");
                return 0;
            }
            selectedPrefab = candidates[0];
            // Boss ở gần giữa chính điện, không chọn sát ngai hoặc chồng vào đội hình hộ vệ.
            var bossTiles = new HashSet<Vector2Int>(room.FloorTiles.Where(cell => !occupiedCells.Contains(cell)));
            bossTiles.Add(spawnPoint);
            var arena = new DungeonRoom(room.Type, bossTiles, spawnPoint);
            Vector3 preferred = ActiveTheme != null && ActiveTheme.usesCampaignAtlas && ActiveTheme.layoutStyle == CampaignLayoutStyle.CrimsonThrone
                ? PrefabPlacer.GetWorldPosition(new Vector2Int(0, room.FloorTiles.Max(p => p.y) - 9))
                : PrefabPlacer.GetWorldPosition(spawnPoint);
            var finalBoss = CampaignFinalBoss.Create(selectedPrefab, arena, preferred);
            BossWaveAttackController.Attach(finalBoss.gameObject);
            spawnedObjects.Add(finalBoss.gameObject);
            return 1 + formationCount;
        }
        if (selectedPrefab == null)
        {
            Debug.LogError($"Level {currentLevel} boss prefab is missing.");
            return 0;
        }

        GameObject boss = Instantiate(selectedPrefab, PrefabPlacer.GetWorldPosition(spawnPoint), Quaternion.identity);
        BossWaveAttackController.Attach(boss);
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

        // Tune a per-run mini-boss config so levels 5 and 9 do not mutate shared assets.
        if (health.Config != null && boss.GetComponent<MiniBossController>() != null)
        {
            EnemyConfigSO variant = Instantiate(health.Config);
            if (currentLevel == 9)
            {
                variant.maxHealth = 260;
                variant.attackCooldown = 1.75f;
                boss.GetComponent<MiniBossController>()?.SetVariantColor(new Color(0.9f, 0.72f, 1f));
            }
            variant.attackCooldown = Mathf.Clamp(variant.attackCooldown * 0.72f, 1.15f, 2.4f);
            variant.attackWindup = Mathf.Clamp(variant.attackWindup * 0.75f, 0.35f, 0.58f);
            boss.GetComponent<MiniBossController>()?.ConfigureRuntimeVariant(variant);
        }

        return 1;
    }

    private int SpawnThroneFormation(DungeonRoom room)
    {
        if (ActiveTheme == null || !ActiveTheme.usesCampaignAtlas || ActiveTheme.layoutStyle != CampaignLayoutStyle.CrimsonThrone)
            return 0;
        var squad = GetCombatSquad();
        if (squad.Count != 3) return 0;
        int cx = (room.FloorTiles.Min(p => p.x) + room.FloorTiles.Max(p => p.x)) / 2;
        int top = room.FloorTiles.Max(p => p.y);
        int count = 0;
        // Hai tướng đứng dưới hai tượng hộ vệ, bốn lính canh dọc lối lên ngai.
        for (int rank = 0; rank < 3; rank++)
        foreach (int side in new[] { -1, 1 })
        {
            var cell = new Vector2Int(cx + side * (rank == 0 ? 6 : 5), top - 6 - rank * 4);
            if (!room.FloorTiles.Contains(cell) || occupiedCells.Contains(cell)) continue;
            var guard = Instantiate(squad[0], PrefabPlacer.GetWorldPosition(cell), Quaternion.identity);
            guard.name = rank == 0 ? "Throne General" : "Throne Guard";
            if (rank == 0) guard.transform.localScale *= 1.25f;
            guard.GetComponent<EnemyMovement>()?.ConfigureBossArena(room.FloorTiles, Vector2.one);
            spawnedObjects.Add(guard);
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) occupiedCells.Add(cell + new Vector2Int(x,y));
            count++;
        }
        return count;
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
