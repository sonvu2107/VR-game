using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>Va chạm chân vật trang trí; dành lối đi trước khi dựng hình và bake NavMesh.</summary>
public sealed class DecorationCollision : MonoBehaviour
{
    private static readonly ConditionalWeakTable<DungeonLayout, HashSet<Vector2Int>> blockedByLayout = new();
    private static readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public static HashSet<Vector2Int> BlockedCells(DungeonLayout layout) =>
        new(blockedByLayout.GetOrCreateValue(layout));

    public static bool TryPlace(DungeonLayout layout, Transform parent, string label, Rect feet,
        HashSet<Vector2Int> reserved)
    {
        if (Application.isPlaying && LanLaunch.Mode == LanMode.Client) return false;
        var blocked = blockedByLayout.GetOrCreateValue(layout);
        var cells = new HashSet<Vector2Int>();
        // Chừa khoảng cho bán kính nhân vật; chỉ tính các ô thực sự nằm trên sàn.
        var clearance = new Rect(feet.xMin - .25f, feet.yMin - .25f, feet.width + .5f, feet.height + .5f);
        for (int x = Mathf.FloorToInt(clearance.xMin); x <= Mathf.FloorToInt(clearance.xMax - .001f); x++)
        for (int y = Mathf.FloorToInt(clearance.yMin); y <= Mathf.FloorToInt(clearance.yMax - .001f); y++)
        {
            var cell = new Vector2Int(x, y);
            if (!layout.FloorTiles.Contains(cell)) continue;
            if (blocked.Contains(cell) || (reserved != null && reserved.Contains(cell))) return false;
            cells.Add(cell);
        }
        if (cells.Count == 0) return false;
        // SpawnPoint có thể được dùng cho spawn/cửa: giữ nguyên vùng tiếp cận.
        foreach (var room in layout.Rooms)
            if (cells.Any(c => (c - room.SpawnPoint).sqrMagnitude <= 4)) return false;
        var candidate = new HashSet<Vector2Int>(blocked);
        candidate.UnionWith(cells);
        if (!IsConnected(layout.FloorTiles, candidate)) return false;
        blocked.UnionWith(cells);
        reserved?.UnionWith(cells);
        var obj = new GameObject(label + " Footprint");
        obj.transform.SetParent(parent, false);
        obj.transform.position = feet.center;
        obj.AddComponent<DecorationCollision>();
        var collider = obj.AddComponent<BoxCollider2D>();
        collider.size = feet.size;
        collider.isTrigger = false;
        var volume = obj.AddComponent<NavMeshPlus.Components.NavMeshModifierVolume>();
        volume.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        volume.size = new Vector3(feet.width + .6f, feet.height + .6f, 4f);
        return true;
    }

    public static bool TrySprite(DungeonLayout layout, SpriteRenderer sprite, HashSet<Vector2Int> reserved)
    {
        Bounds b = sprite.bounds;
        float width = Mathf.Min(b.size.x * .8f, 1.1f);
        float height = Mathf.Clamp(b.size.y * .25f, .22f, .5f);
        var feet = new Rect(b.center.x - width / 2f, b.min.y + b.size.y * .08f, width, height);
        bool placed = TryPlace(layout, sprite.transform, sprite.gameObject.name, feet, reserved);
        if (placed) WorldDepthSort.Attach(sprite.gameObject, feet.center.y);
        return placed;
    }

    public static bool IsConnected(HashSet<Vector2Int> floor, HashSet<Vector2Int> blocked)
    {
        var remaining = new HashSet<Vector2Int>(floor);
        remaining.ExceptWith(blocked);
        if (remaining.Count == 0) return false;
        var pending = new Queue<Vector2Int>();
        var first = remaining.First();
        remaining.Remove(first); pending.Enqueue(first);
        while (pending.Count > 0)
        {
            var at = pending.Dequeue();
            foreach (var direction in directions)
                if (remaining.Remove(at + direction)) pending.Enqueue(at + direction);
        }
        return remaining.Count == 0;
    }
}
