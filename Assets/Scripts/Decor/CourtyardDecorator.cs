using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Curated clusters from the original atlas; solid props have ground collision; no rewards.</summary>
public static class CourtyardDecorator
{
    public static void Decorate(DungeonLayout layout, CourtyardTheme theme, SeededRandom random,
        HashSet<Vector2Int> occupied, ICollection<GameObject> spawned)
    {
        if (theme == null) return;
        var root = new GameObject("Courtyard Atlas Details");
        spawned.Add(root);
        foreach (DungeonRoom room in layout.Rooms)
        {
            PlaceInlay(root.transform, room, theme);
            var edge = room.FloorTiles.OrderBy(cell => cell.y).ThenBy(cell => cell.x)
                .Where(cell => !layout.FloorTiles.Contains(cell + Vector2Int.up) &&
                    layout.FloorTiles.Contains(cell + Vector2Int.down) && !occupied.Contains(cell)).ToList();
            int placements = 0;
            foreach (Vector2Int cell in edge)
            {
                if (placements >= 6 || random.Range(0, 100) > 65 || occupied.Contains(cell)) continue;
                if ((cell - room.SpawnPoint).sqrMagnitude < 25) continue;
                Sprite[] pool = room.Type == RoomType.Boss ? theme.remainsProps :
                    room.Type == RoomType.Treasure ? theme.storageProps :
                    placements % 2 == 0 ? theme.potteryProps : theme.storageProps;
                if (pool == null || pool.Length == 0) continue;
                if (!SpawnSolid(layout, root.transform, random.Choose(pool), cell, "Atlas Border Prop", occupied)) continue;
                // A second piece makes a composed supply cluster, not scattered noise.
                Vector2Int side = cell + Vector2Int.right;
                if (room.FloorTiles.Contains(side) && !occupied.Contains(side) &&
                    !layout.FloorTiles.Contains(side + Vector2Int.up))
                {
                    SpawnSolid(layout, root.transform, random.Choose(pool), side, "Atlas Supply Cluster", occupied);
                }
                placements++;
            }
            if (room.Type == RoomType.Start && theme.entranceSteps != null)
            {
                Vector2Int stepCell = Center(room) + Vector2Int.left * 3;
                if (room.FloorTiles.Contains(stepCell))
                    Spawn(root.transform, theme.entranceSteps, stepCell, "Entrance Stair Marker", 1);
            }
            if (theme.wallDetails != null && theme.wallDetails.Length > 0 && edge.Count > 4)
                foreach (Vector2Int cell in new[] { edge[edge.Count / 4], edge[edge.Count * 3 / 4] })
                    Spawn(root.transform, random.Choose(theme.wallDetails), cell + Vector2Int.up, "Wall Atlas Plaque", 2);
            if (theme.wallBanner != null && edge.Count > 5)
                Spawn(root.transform, theme.wallBanner, edge[edge.Count / 2] + Vector2Int.up, "Heraldic Wall Banner", 3);
        }
    }

    private static void PlaceInlay(Transform parent, DungeonRoom room, CourtyardTheme theme)
    {
        if (theme.pavingSprites == null || theme.pavingSprites.Length == 0) return;
        // Find a stable geometric center: runtime SpawnPoint moves as objects
        // claim cells, so it is not suitable as the decorative room center.
        Vector2Int center = Center(room);
        for (int x = -4; x <= 4; x++)
        for (int y = -3; y <= 3; y++)
        {
            int distance = Mathf.Abs(x) + Mathf.Abs(y);
            if (room.Type == RoomType.Combat && distance != 4) continue;
            if (room.Type == RoomType.Treasure && distance > 5) continue;
            if (room.Type == RoomType.Start && Mathf.Abs(x) > 2) continue;
            if (room.Type == RoomType.Exit && (Mathf.Abs(x) > 1 || y % 2 != 0)) continue;
            Vector2Int cell = center + new Vector2Int(x, y);
            if (!room.FloorTiles.Contains(cell)) continue;
            int pattern = Mathf.Abs(x * 7 + y * 11) % theme.pavingSprites.Length;
            SpriteRenderer renderer = Spawn(parent, theme.pavingSprites[pattern], cell, "Courtyard Stone Inlay", 0);
            if (renderer == null) continue;
            bool border = Mathf.Abs(x) == 4 || Mathf.Abs(y) == 3 || room.Type == RoomType.Combat;
            renderer.color = room.Type == RoomType.Boss ? new Color(1f, 0.65f, 0.7f, 0.65f) :
                border ? new Color(1f, 0.82f, 0.63f, 0.65f) : new Color(0.88f, 0.88f, 1f, 0.45f);
        }
    }

    private static Vector2Int Center(DungeonRoom room)
    {
        return new Vector2Int((room.FloorTiles.Min(cell => cell.x) + room.FloorTiles.Max(cell => cell.x)) / 2,
            (room.FloorTiles.Min(cell => cell.y) + room.FloorTiles.Max(cell => cell.y)) / 2);
    }

    private static bool SpawnSolid(DungeonLayout layout, Transform parent, Sprite sprite, Vector2Int cell,
        string label, HashSet<Vector2Int> reserved)
    {
        var renderer = Spawn(parent, sprite, cell, label, 2);
        if (renderer == null) return false;
        if (DecorationCollision.TrySprite(layout, renderer, reserved)) return true;
        renderer.gameObject.SetActive(false);
        if (Application.isPlaying) Object.Destroy(renderer.gameObject); else Object.DestroyImmediate(renderer.gameObject);
        return false;
    }

    private static SpriteRenderer Spawn(Transform parent, Sprite sprite, Vector2Int cell, string name, int order)
    {
        if (sprite == null) return null;
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }
}
