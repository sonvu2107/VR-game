using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Procedurally decorates underground dungeon walls with warm wall torches and rare banners,
/// plus wooden storage crates in room corners and room centerpieces.
/// Leaves room floors and corridors 100% clean and unobstructed for an authentic basement setting.
/// </summary>
public static class DungeonDecorator
{
    private static bool assetsLoaded;
    private static Sprite[] torchFrames;
    private static Sprite[] flagFrames;
    private static Sprite[] boxSprites;
    private static Sprite chestSprite;

    private static readonly Color TorchWarmColor = new(1f, 0.68f, 0.28f, 1f);
    private static readonly Color DarknessTorchColor = new(1f, 0.60f, 0.20f, 1f);

    // Shared animated torch for the new map; no old crates/banners are spawned.
    public static GameObject CreateWallTorch(Vector3 position, Transform parent, Material material = null, Color? lightColor = null)
    {
        EnsureAssets();
        if (torchFrames == null || torchFrames.Length == 0) return null;
        var torch = new GameObject("Wall Torch");
        torch.transform.SetParent(parent, false);
        torch.transform.position = position;
        var renderer = torch.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 3;
        if (material != null) renderer.sharedMaterial = material;
        var animation = torch.AddComponent<AnimatedDecor>();
        animation.ConfigureFrames(torchFrames, 5.5f);
        animation.ConfigureTorchLight(lightColor ?? TorchWarmColor, 2.2f, 0.2f, 0.9f, lightColor.HasValue);
        animation.SetWallMountedFlame();
        torch.transform.localScale = Vector3.one * 0.55f;
        return torch;
    }

    public static void DecorateDungeon(
        DungeonLayout layout,
        SeededRandom random,
        HashSet<Vector2Int> occupiedCells,
        ICollection<GameObject> spawnedObjects,
        int currentLevel,
        LevelType levelType, bool formalLayout = false)
    {
        if (layout == null || layout.FloorTiles == null || layout.FloorTiles.Count == 0)
            return;

        EnsureAssets();

        GameObject root = new("Dungeon Decorations");
        spawnedObjects?.Add(root);

        HashSet<Vector2Int> wallDecorCells = new();

        // 1. Decorate Walls (Warm torches ~40%, rare banners, ambient 2D lighting)
        DecorateWalls(layout, random, spawnedObjects, root.transform, currentLevel, levelType, wallDecorCells, formalLayout);

        // 2. Decorate Main Rooms (Storage crates in corners and Treasure Chest)
        if (layout.Rooms != null)
        {
            foreach (DungeonRoom room in layout.Rooms)
            {
                DecorateRoom(room, layout.FloorTiles, random, occupiedCells, spawnedObjects, root.transform, currentLevel, levelType);
            }
        }
    }

    private static void DecorateWalls(
        DungeonLayout layout,
        SeededRandom random,
        ICollection<GameObject> spawnedObjects,
        Transform parent,
        int currentLevel,
        LevelType levelType,
        HashSet<Vector2Int> wallDecorCells, bool formalLayout)
    {
        HashSet<Vector2Int> floorTiles = layout.FloorTiles;
        List<Vector2Int> candidateWalls = new();

        foreach (Vector2Int floorPos in floorTiles)
        {
            Vector2Int wallPos = floorPos + Vector2Int.up;
            if (!floorTiles.Contains(wallPos))
            {
                candidateWalls.Add(wallPos);
            }
        }

        List<Vector2Int> sortedWalls = candidateWalls
            .Distinct()
            .OrderBy(w => w.y)
            .ThenBy(w => w.x)
            .ToList();

        if (sortedWalls.Count == 0)
            return;

        int step = 5;
        int wallIndex = random.Range(0, step);
        int lastFlagWallIndex = -999;
        int torchChance = currentLevel == 6 ? 70 : (levelType == LevelType.FinalBoss ? 70 : 65);
        List<Vector2Int> placedTorchCells = new();
        const float minTorchSpacing = 4.5f;

        while (wallIndex < sortedWalls.Count)
        {
            Vector2Int wallCell = sortedWalls[wallIndex];

            if (formalLayout && (floorTiles.Contains(wallCell + Vector2Int.left) ||
                floorTiles.Contains(wallCell + Vector2Int.right)))
            {
                wallIndex += 1;
                continue;
            }

            if (floorTiles.Contains(wallCell + Vector2Int.down))
            {
                bool isTooCloseToOtherTorch = placedTorchCells.Any(p => Vector2Int.Distance(p, wallCell) < minTorchSpacing);
                int roll = random.Range(0, 100);
                bool placeTorch = !isTooCloseToOtherTorch && (roll < torchChance);
                bool placeFlag = !placeTorch && (roll >= 90) && (wallIndex - lastFlagWallIndex >= 14) && currentLevel != 6;

                Vector3 worldPos = new(wallCell.x + 0.5f, wallCell.y + 0.38f, 0f);

                if (placeTorch && torchFrames != null && torchFrames.Length > 0)
                {
                    GameObject torchObj = new("Wall Torch");
                    torchObj.transform.position = worldPos;
                    torchObj.transform.localScale = new Vector3(0.58f, 0.58f, 1f);
                    torchObj.transform.SetParent(parent, true);

                    SpriteRenderer sr = torchObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 3; // Wall surface layer (behind trees on floor)

                    AnimatedDecor decor = torchObj.AddComponent<AnimatedDecor>();
                    decor.ConfigureFrames(torchFrames, 5.5f);

                    float lightRadius = currentLevel == 6 ? 2.3f : 1.9f;
                    float lightIntensity = currentLevel == 6 ? 1.4f : 1.2f;
                    Color lightCol = currentLevel == 6 ? DarknessTorchColor : TorchWarmColor;
                    decor.ConfigureTorchLight(lightCol, lightRadius, 0.25f, lightIntensity);

                    spawnedObjects?.Add(torchObj);
                    wallDecorCells.Add(wallCell);
                    placedTorchCells.Add(wallCell);
                }
                else if (placeFlag && flagFrames != null && flagFrames.Length > 0)
                {
                    GameObject flagObj = new("Wall Banner");
                    flagObj.transform.position = new Vector3(wallCell.x + 0.5f, wallCell.y + 0.25f, 0f);
                    flagObj.transform.SetParent(parent, true);

                    SpriteRenderer sr = flagObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 3; // Wall surface layer (behind trees on floor)

                    AnimatedDecor decor = flagObj.AddComponent<AnimatedDecor>();
                    decor.ConfigureFrames(flagFrames, 4.5f);

                    spawnedObjects?.Add(flagObj);
                    wallDecorCells.Add(wallCell);
                    lastFlagWallIndex = wallIndex;
                }
            }

            wallIndex += random.Range(step, step + 3);
        }
    }

    private static void DecorateRoom(
        DungeonRoom room,
        HashSet<Vector2Int> allFloorTiles,
        SeededRandom random,
        HashSet<Vector2Int> occupiedCells,
        ICollection<GameObject> spawnedObjects,
        Transform parent,
        int currentLevel,
        LevelType levelType)
    {
        if (room == null || room.FloorTiles == null || room.FloorTiles.Count == 0)
            return;

        List<Vector2Int> cornerCells = new();
        List<Vector2Int> borderCells = new();
        List<Vector2Int> interiorCells = new();

        foreach (Vector2Int cell in room.FloorTiles)
        {
            if (occupiedCells.Contains(cell))
                continue;

            int wallNeighbourCount = 0;
            bool northWall = !allFloorTiles.Contains(cell + Vector2Int.up);
            bool southWall = !allFloorTiles.Contains(cell + Vector2Int.down);
            bool eastWall = !allFloorTiles.Contains(cell + Vector2Int.right);
            bool westWall = !allFloorTiles.Contains(cell + Vector2Int.left);

            if (northWall) wallNeighbourCount++;
            if (southWall) wallNeighbourCount++;
            if (eastWall) wallNeighbourCount++;
            if (westWall) wallNeighbourCount++;

            if ((northWall && westWall) || (northWall && eastWall) ||
                (southWall && westWall) || (southWall && eastWall))
            {
                cornerCells.Add(cell);
            }
            else if (wallNeighbourCount >= 1)
            {
                borderCells.Add(cell);
            }
            else
            {
                interiorCells.Add(cell);
            }
        }

        // A. Corner Props (Wooden storage crates & boxes in 1-2 corners)
        int cornersToDecorate = Mathf.Min(cornerCells.Count, random.Range(1, 3));
        for (int i = 0; i < cornersToDecorate; i++)
        {
            if (cornerCells.Count == 0) break;
            int idx = random.Range(0, cornerCells.Count);
            Vector2Int cornerCell = cornerCells[idx];
            cornerCells.RemoveAt(idx);

            if (occupiedCells.Contains(cornerCell))
                continue;

            Vector3 worldPos = PrefabPlacer.GetWorldPosition(cornerCell) + new Vector3(0.5f, -0.45f, 0f);

            if (boxSprites != null && boxSprites.Length > 0)
            {
                SpawnStaticProp("Crates", boxSprites[random.Range(0, boxSprites.Length)], worldPos, 3, parent, spawnedObjects);
                occupiedCells.Add(cornerCell);
            }
        }

        // B. Special Room Centerpieces (Treasure Chest in Treasure Room)
        if (room.Type == RoomType.Treasure && chestSprite != null)
        {
            Vector2Int centerCell = room.SpawnPoint;
            if (!occupiedCells.Contains(centerCell))
            {
                Vector3 chestPos = PrefabPlacer.GetWorldPosition(centerCell) + new Vector3(0.5f, -0.4f, 0f);
                SpawnStaticProp("Treasure Chest Prop", chestSprite, chestPos, 3, parent, spawnedObjects);
                occupiedCells.Add(centerCell);
            }
        }
    }

    private static GameObject SpawnStaticProp(string name, Sprite sprite, Vector3 position, int sortingOrder, Transform parent, ICollection<GameObject> spawnedObjects)
    {
        if (sprite == null) return null;

        GameObject obj = new(name);
        obj.transform.position = position;
        obj.transform.SetParent(parent, true);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        spawnedObjects?.Add(obj);
        return obj;
    }

    private static void EnsureAssets()
    {
        if (assetsLoaded)
            return;

        assetsLoaded = true;

        // 1. Torches & Flags (16 PPU for 16x16 tiles)
        torchFrames = LoadFrames("Decor/Torches/torch", 4, 16f, new Vector2(0.5f, 0.5f));
        flagFrames = LoadFrames("Decor/Flags/flag", 4, 16f, new Vector2(0.5f, 0.5f));

        // 2. Props & Dungeon Crates (Load all 4 crate styles)
        boxSprites = new[]
        {
            LoadSprite("Decor/Props/box_1", 16f, new Vector2(0.5f, 0.3f)),
            LoadSprite("Decor/Props/box_2", 16f, new Vector2(0.5f, 0.3f)),
            LoadSprite("Decor/Props/box_3", 16f, new Vector2(0.5f, 0.3f)),
            LoadSprite("Decor/Props/box_4", 16f, new Vector2(0.5f, 0.3f))
        }.Where(s => s != null).ToArray();

        chestSprite = LoadSprite("Decor/Props/chest_1", 16f, new Vector2(0.5f, 0.3f));
    }

    private static Sprite LoadSprite(string resourcePath, float pixelsPerUnit, Vector2 pivot)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            pivot,
            pixelsPerUnit,
            0u,
            SpriteMeshType.FullRect);
        sprite.name = System.IO.Path.GetFileNameWithoutExtension(resourcePath);
        return sprite;
    }

    private static Sprite[] LoadFrames(string prefix, int count, float pixelsPerUnit, Vector2 pivot)
    {
        List<Sprite> list = new();
        for (int i = 1; i <= count; i++)
        {
            Sprite s = LoadSprite($"{prefix}_{i}", pixelsPerUnit, pivot);
            if (s != null)
                list.Add(s);
        }
        return list.ToArray();
    }
}
