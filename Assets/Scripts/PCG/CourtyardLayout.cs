using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in fortress layout. Legacy Random Walk remains unchanged.</summary>
public static class CourtyardLayout
{
    private static readonly Vector2Int[] Centers =
    {
        new(0, 0), new(26, 0), new(26, 24), new(0, 24),
        new(-26, 24), new(-26, 0), new(-26, -26)
    };

    public static DungeonLayout Generate(IReadOnlyList<RoomType> sequence, SeededRandom random)
    {
        if (sequence == null || sequence.Count < 5 || sequence.Count > Centers.Length)
            throw new ArgumentException("Courtyard prototype supports five to seven rooms.");
        var layout = new DungeonLayout();
        int turns = random.Range(0, 4);
        bool mirror = random.Range(0, 2) == 1;
        var centers = new List<Vector2Int>();
        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2Int center = Transform(Centers[i], turns, mirror);
            centers.Add(center);
            int halfX = sequence[i] == RoomType.Boss ? 9 : random.Range(6, 9);
            int halfY = sequence[i] == RoomType.Boss ? 9 : random.Range(6, 8);
            var tiles = new HashSet<Vector2Int>();
            for (int x = -halfX; x <= halfX; x++)
            for (int y = -halfY; y <= halfY; y++)
            {
                if (Mathf.Abs(x) > halfX - 2 && Mathf.Abs(y) > halfY - 2) continue;
                if (sequence[i] == RoomType.Combat && Mathf.Abs(x) == 4 && Mathf.Abs(y) == 3) continue;
                tiles.Add(center + Transform(new Vector2Int(x, y), turns, mirror));
            }
            layout.AddRoom(new DungeonRoom(sequence[i], tiles, center));
        }
        for (int i = 1; i < centers.Count; i++) Connect(layout, centers[i - 1], centers[i]);
        Connect(layout, centers[0], centers[3]);
        return layout;
    }

    private static void Connect(DungeonLayout layout, Vector2Int from, Vector2Int to)
    {
        var cells = new HashSet<Vector2Int>();
        Vector2Int point = from;
        while (true)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++) cells.Add(point + new Vector2Int(x, y));
            if (point == to) break;
            if (point.x != to.x) point.x += Math.Sign(to.x - point.x);
            else point.y += Math.Sign(to.y - point.y);
        }
        layout.AddCorridor(cells);
    }

    private static Vector2Int Transform(Vector2Int point, int turns, bool mirror)
    {
        if (mirror) point.x = -point.x;
        for (int i = 0; i < turns; i++) point = new Vector2Int(-point.y, point.x);
        return point;
    }
}
