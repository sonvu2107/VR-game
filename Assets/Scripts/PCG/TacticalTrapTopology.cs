using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Mẫu bẫy bám cửa thật của layout, không suy đoán cửa từ tâm/bounding box phòng.</summary>
public static class TacticalTrapTopology
{
    public readonly struct Portal
    {
        public readonly Vector2Int Cell, Inward;
        public readonly int Width;
        public Portal(Vector2Int cell, Vector2Int inward, int width)
        { Cell = cell; Inward = inward; Width = width; }
    }

    public static List<Portal> FindPortals(DungeonLayout layout, DungeonRoom room)
    {
        var portals = new List<Portal>();
        foreach (var outward in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
        {
            var side = new Vector2Int(-outward.y, outward.x);
            var mouths = new HashSet<Vector2Int>(room.FloorTiles.Where(p =>
                layout.FloorTiles.Contains(p + outward) && !room.FloorTiles.Contains(p + outward)));
            foreach (var first in mouths.OrderBy(p => p.x).ThenBy(p => p.y).ToArray())
            {
                if (!mouths.Remove(first)) continue;
                var strip = new List<Vector2Int> { first };
                foreach (int sign in new[] { -1, 1 })
                {
                    var next = first + side * sign;
                    while (mouths.Remove(next)) { strip.Add(next); next += side * sign; }
                }
                strip = strip.OrderBy(p => p.x * side.x + p.y * side.y).ToList();
                portals.Add(new Portal(strip[strip.Count / 2], -outward, strip.Count));
            }
        }
        return portals;
    }

    public static IEnumerable<List<Vector2Int>> DoorwayBands(Portal portal)
    {
        var side = new Vector2Int(-portal.Inward.y, portal.Inward.x);
        int span = Mathf.Clamp(portal.Width + 2, 3, 6);
        // Thử phần hành lang trước, sau đó sân đệm phía trong phòng.
        // Không đặt ngay dưới chân lúc người chơi vừa bước qua cửa.
        foreach (int depth in new[] { -3, -4, 3, 4, 5, 6 })
        {
            var row = new List<Vector2Int>();
            for (int i = -span; i <= span; i++) row.Add(portal.Cell + portal.Inward * depth + side * i);
            yield return row;
        }
        // Lối hẹp không đủ hàng ngang: tạo dải dọc ở mép lối vào, không bịt làn chính.
        foreach (int flank in new[] { -2, 2, -3, 3 })
        {
            var strip = new List<Vector2Int>();
            for (int depth = 3; depth <= 6; depth++) strip.Add(portal.Cell + portal.Inward * depth + side * flank);
            yield return strip;
        }
    }

    public static List<Vector2Int> BestRun(IEnumerable<Vector2Int> band, int budget, Func<Vector2Int, bool> allowed)
    {
        var best = new List<Vector2Int>();
        var run = new List<Vector2Int>();
        Vector2Int previous = default;
        bool hasPrevious = false;
        foreach (var cell in band)
        {
            if (!allowed(cell)) { run.Clear(); hasPrevious = false; continue; }
            if (hasPrevious && Mathf.Abs(cell.x - previous.x) + Mathf.Abs(cell.y - previous.y) != 1) run.Clear();
            run.Add(cell); previous = cell; hasPrevious = true;
            if (run.Count > best.Count) best = run.Take(budget).ToList();
            if (best.Count == budget) break;
        }
        // Không nhặt ô lẻ từ một hàng bẫy bị cắt vụn.
        return best.Count >= Mathf.Min(2, budget) ? best : new List<Vector2Int>();
    }
}
