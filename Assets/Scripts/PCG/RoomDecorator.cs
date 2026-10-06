using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automatically places decorations (torches, pots, chests, etc.) inside
/// dungeon rooms. Call DecorateRooms() after floor generation.
/// Attach to the same object as InfiniteWorldGenerator.
/// </summary>
public class RoomDecorator : MonoBehaviour
{
    [System.Serializable]
    public class DecorationEntry
    {
        public GameObject prefab;
        [Tooltip("Relative spawn weight. Higher = more common.")]
        [Min(1)] public int weight = 1;
        [Tooltip("Sorting order for the sprite.")]
        public int sortingOrder = 3;
        [Tooltip("Scale multiplier.")]
        public float scale = 1f;
    }

    [Header("Decoration Prefabs")]
    [Tooltip("List of complex decorations (chests, animated torches) that can spawn in rooms.")]
    public DecorationEntry[] decorations;

    [Header("Simple Sprite Decorations")]
    [Tooltip("Drag and drop sprites here (like grass, rocks) to auto-generate decorations without making prefabs.")]
    public Sprite[] simpleSprites;
    [Tooltip("Scale multiplier for simple sprites.")]
    public float simpleSpriteScale = 1.5f;

    [Header("Spawn Settings")]
    [Tooltip("Average number of decorations per room.")]
    [Range(0, 50)] public int decosPerRoom = 15;
    [Tooltip("Random variance on deco count.")]
    [Range(0, 20)] public int decoVariance = 5;
    [Tooltip("Minimum distance from other objects.")]
    [Min(0)] public int spawnClearance = 1;

    [Header("Wall Decorations")]
    [Tooltip("Prefab placed along walls (e.g., torches). Can be null.")]
    public GameObject wallDecoPrefab;
    [Tooltip("Chance to place a wall deco on each wall-adjacent tile.")]
    [Range(0f, 1f)] public float wallDecoChance = 0.08f;
    public int wallDecoSortingOrder = 5;

    private readonly List<GameObject> spawnedDecos = new();

    /// <summary>
    /// Decorate all rooms with random props. Call after dungeon generation.
    /// </summary>
    public void DecorateRooms(
        IReadOnlyList<DungeonRoom> rooms,
        HashSet<Vector2Int> allFloorTiles,
        HashSet<Vector2Int> occupiedCells,
        SeededRandom random,
        ICollection<GameObject> spawnedObjects)
    {
        bool hasPrefabs = decorations != null && decorations.Length > 0;
        bool hasSprites = simpleSprites != null && simpleSprites.Length > 0;

        if (!hasPrefabs && !hasSprites)
            return;

        // Calculate total weight for prefabs
        int totalWeight = 0;
        if (hasPrefabs)
        {
            foreach (var d in decorations)
                totalWeight += d.weight;
        }

        foreach (DungeonRoom room in rooms)
        {
            // Skip start/exit rooms
            if (room.Type == RoomType.Start || room.Type == RoomType.Exit)
                continue;

            int count = decosPerRoom + random.Range(-decoVariance, decoVariance + 1);
            count = Mathf.Max(0, count);

            for (int i = 0; i < count; i++)
            {
                if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells,
                        spawnClearance, out Vector2Int spawnPoint))
                    break;

                Vector3 worldPos = PrefabPlacer.GetWorldPosition(spawnPoint);
                GameObject deco = null;

                // Randomly choose between a prefab and a simple sprite
                bool pickSprite = hasSprites && (!hasPrefabs || random.Range(0, 2) == 0);

                if (pickSprite)
                {
                    // Create from simple sprite
                    Sprite chosenSprite = simpleSprites[random.Range(0, simpleSprites.Length)];
                    if (chosenSprite != null)
                    {
                        deco = new GameObject("Deco_Sprite");
                        deco.transform.position = worldPos;
                        deco.transform.localScale = Vector3.one * simpleSpriteScale;
                        SpriteRenderer sr = deco.AddComponent<SpriteRenderer>();
                        sr.sprite = chosenSprite;
                        sr.sortingOrder = 3; // Ensure it renders above player/floor if it's an obstacle

                        // Add collider if user wants them to be obstacles
                        BoxCollider2D col = deco.AddComponent<BoxCollider2D>();
                        // Bóp nhỏ khung va chạm và dời nó xuống gốc cây
                        // Để người chơi có thể đi lẩn ra sau tán lá (ảo giác 3D)
                        col.size = new Vector2(0.4f, 0.4f); 
                        col.offset = new Vector2(0f, -0.3f);
                    }
                }
                else
                {
                    // Create from prefab
                    DecorationEntry entry = PickRandom(random, totalWeight);
                    if (entry?.prefab != null)
                    {
                        deco = Instantiate(entry.prefab, worldPos, Quaternion.identity);

                        if (Mathf.Abs(entry.scale - 1f) > 0.01f)
                            deco.transform.localScale *= entry.scale;

                        SpriteRenderer sr = deco.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null)
                            sr.sortingOrder = entry.sortingOrder;
                    }
                }

                if (deco != null)
                {
                    spawnedDecos.Add(deco);
                    spawnedObjects?.Add(deco);
                }
            }

            // Wall decorations (torches along walls)
            if (wallDecoPrefab != null)
                PlaceWallDecos(room, allFloorTiles, occupiedCells, random, spawnedObjects);
        }
    }

    private void PlaceWallDecos(
        DungeonRoom room,
        HashSet<Vector2Int> allFloorTiles,
        HashSet<Vector2Int> occupiedCells,
        SeededRandom random,
        ICollection<GameObject> spawnedObjects)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int tile in room.FloorTiles)
        {
            if (occupiedCells.Contains(tile)) continue;

            // Check if this tile is adjacent to a wall (non-floor tile)
            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = tile + dir;
                if (!allFloorTiles.Contains(neighbor))
                {
                    // This tile borders a wall
                    if (random.Range(0, 100) < (int)(wallDecoChance * 100))
                    {
                        Vector3 worldPos = PrefabPlacer.GetWorldPosition(tile);
                        GameObject deco = Instantiate(wallDecoPrefab, worldPos, Quaternion.identity);

                        SpriteRenderer sr = deco.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null)
                            sr.sortingOrder = wallDecoSortingOrder;

                        occupiedCells.Add(tile);
                        spawnedDecos.Add(deco);
                        spawnedObjects?.Add(deco);
                    }
                    break; // Only place one deco per tile
                }
            }
        }
    }

    private DecorationEntry PickRandom(SeededRandom random, int totalWeight)
    {
        int roll = random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var d in decorations)
        {
            cumulative += d.weight;
            if (roll < cumulative)
                return d;
        }
        return decorations[^1];
    }

    public void ClearDecorations()
    {
        foreach (var deco in spawnedDecos)
        {
            if (deco != null)
                Destroy(deco);
        }
        spawnedDecos.Clear();
    }
}
