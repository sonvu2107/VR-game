using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public enum TacticalTrapPattern { Chicane, RangedScreen, TreasureGuard, BossFlanks, DoorwayChoke }

/// <summary>Chỉ chạy lúc sinh map. Bẫy không được chạm mạng đường đi kể cả khi tất cả cùng bật.</summary>
public sealed class TacticalTrapPlanner
{
    public const float SafeClearance = 1.3f;
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    public readonly List<Vector3[]> SafeRoutes = new();
    public readonly HashSet<Vector2Int> Reserved = new();
    public bool RoutesVerified { get; private set; } = true;
    public Vector2 RouteBodySize { get; private set; }
    public Vector3 RouteBodyOffset { get; private set; }
    public string FailureReason { get; private set; }
    private readonly DungeonLayout layout;
    private readonly bool arcane;
    private readonly HashSet<Vector2Int> corridorTiles;
    private readonly Vector3 playerOrigin;

    public TacticalTrapPlanner(DungeonLayout layout, IEnumerable<GameObject> objects, Transform playerTransform, bool arcane)
    {
        this.layout = layout;
        this.arcane = arcane;
        corridorTiles = new HashSet<Vector2Int>(layout.FloorTiles);
        foreach (var room in layout.Rooms) corridorTiles.ExceptWith(room.FloorTiles);
        Vector3 player = playerTransform.position;
        playerOrigin = player;
        var body = playerTransform.GetComponent<Collider2D>();
        Vector3 offset = body != null ? body.bounds.center - player : Vector3.zero;
        RouteBodySize = body != null ? (Vector2)body.bounds.size : Vector2.one * 0.5f;
        // Chừa cả hai hướng quay mặt và một chút dung sai vật lý.
        RouteBodySize += new Vector2(Mathf.Abs(offset.x) * 2f + 0.05f, 0.05f);
        RouteBodyOffset = new Vector3(0f, offset.y, 0f);
        var goals = new List<Vector3>();
        foreach (var room in layout.Rooms)
        {
            goals.Add(Position(room.SpawnPoint));
            foreach (var tile in room.FloorTiles.OrderBy(p => p.x).ThenBy(p => p.y))
                if (Directions.Any(dir => layout.FloorTiles.Contains(tile + dir) && !room.FloorTiles.Contains(tile + dir)))
                {
                    goals.Add(Position(tile));
                    Reserve(tile, 2);
                }
        }
        foreach (var obj in objects)
            if (obj != null && (obj.GetComponent<EnemyHealth>() != null || obj.GetComponent<Enemy>() != null ||
                obj.GetComponent<DungeonKey>() != null || obj.GetComponent<FloorExit>() != null ||
                obj.GetComponent<HealingPickup>() != null)) goals.Add(obj.transform.position);
        var free = new HashSet<Vector2Int>();
        foreach (var tile in layout.FloorTiles)
            if (ClearBodySegment(RoutePosition(tile), RoutePosition(tile))) free.Add(tile);
        var starts = free.OrderBy(tile => Vector2.Distance(RoutePosition(tile), player)).ThenBy(p => p.x).ThenBy(p => p.y);
        Vector2Int start = default; bool hasStart = false;
        foreach (var tile in starts)
            if (Vector2.Distance(RoutePosition(tile), player) <= 1.5f && ClearBodySegment(player, RoutePosition(tile)))
            { start = tile; hasStart = true; break; }
        if (!hasStart) { RoutesVerified = false; FailureReason = "No safe start cell"; return; }
        var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!free.Contains(next) || previous.ContainsKey(next) || !ClearBodySegment(RoutePosition(current), RoutePosition(next))) continue;
                previous.Add(next, current); queue.Enqueue(next);
            }
        }
        foreach (var goal in goals)
        {
            var nearest = previous.Keys.OrderBy(tile => Vector2.Distance(RoutePosition(tile), goal)).ThenBy(p => p.x).ThenBy(p => p.y).First();
            if (Vector2.Distance(RoutePosition(nearest), goal) > 1.5f)
            { RoutesVerified = false; FailureReason = "Cannot approach " + goal; break; }
            var reverse = new List<Vector2Int> { nearest };
            while (nearest != start) { nearest = previous[nearest]; reverse.Add(nearest); }
            reverse.Reverse();
            var corners = new List<Vector3> { player, RoutePosition(start) };
            for (int i = 1; i < reverse.Count - 1; i++)
                if (reverse[i] - reverse[i - 1] != reverse[i + 1] - reverse[i]) corners.Add(RoutePosition(reverse[i]));
            if (reverse.Count > 1) corners.Add(RoutePosition(reverse[reverse.Count - 1]));
            SafeRoutes.Add(corners.ToArray());
        }
        foreach (var tile in layout.FloorTiles)
            if (DistanceToSafeRoute(Position(tile)) < SafeClearance + 0.4f) Reserve(tile, 0);
        // Vùng xuất hiện luôn rộng rãi và không bị kích hoạt bất ngờ.
        foreach (var tile in layout.FloorTiles)
            if (Vector2.Distance(Position(tile), player) < 3f) Reserved.Add(tile);
    }

    private Vector3 RoutePosition(Vector2Int tile) => new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) - RouteBodyOffset;

    public bool ClearBodySegment(Vector3 from, Vector3 to)
    {
        Vector2 delta = to - from;
        int mask = EnemyCombat.ResolveWallLayers();
        if (delta.sqrMagnitude < 0.001f)
        {
            foreach (var collider in Physics2D.OverlapBoxAll(from + RouteBodyOffset, RouteBodySize, 0f, mask))
                if (!collider.isTrigger) return false;
        }
        else foreach (var hit in Physics2D.BoxCastAll(from + RouteBodyOffset, RouteBodySize, 0f,
            delta.normalized, delta.magnitude, mask)) if (hit.collider != null && !hit.collider.isTrigger) return false;
        return true;
    }

    public Vector3 Position(Vector2Int tile) => arcane ? new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) : PrefabPlacer.GetWorldPosition(tile);
    public Vector2Int Cell(Vector3 position)
    {
        Vector3 origin = Position(Vector2Int.zero);
        return new Vector2Int(Mathf.RoundToInt(position.x - origin.x), Mathf.RoundToInt(position.y - origin.y));
    }
    private void Reserve(Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++) for (int y = -radius; y <= radius; y++)
            Reserved.Add(center + new Vector2Int(x, y));
    }
    public float DistanceToSafeRoute(Vector3 point)
    {
        float best = float.PositiveInfinity;
        foreach (var route in SafeRoutes)
        {
            if (route.Length == 1) best = Mathf.Min(best, Vector2.Distance(point, route[0]));
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], b = route[i], p = point, delta = b - a;
                float t = delta.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + delta * t));
            }
        }
        return best;
    }

    public List<Vector2Int> Plan(DungeonRoom room, TacticalTrapPattern pattern, int budget,
        Vector2Int focus, int orientation, HashSet<Vector2Int> occupied)
    {
        var result = new List<Vector2Int>();
        if (!RoutesVerified || budget <= 0 || room.Type == RoomType.Start || room.Type == RoomType.Exit) return result;
        var center = new Vector2Int(Mathf.RoundToInt((float)room.FloorTiles.Average(p => p.x)),
            Mathf.RoundToInt((float)room.FloorTiles.Average(p => p.y)));
        var portals = TacticalTrapTopology.FindPortals(layout, room)
            .OrderBy(p => (Position(p.Cell) - playerOrigin).sqrMagnitude).ToList();
        bool Allowed(Vector2Int cell) => IsSafeCandidate(cell, room, occupied, pattern == TacticalTrapPattern.DoorwayChoke);
        if (pattern == TacticalTrapPattern.DoorwayChoke)
        {
            // Tìm một cụm liền mạch ở cửa gần hướng tiếp cận; không rải bù ở tâm phòng.
            var bestDoorway = new List<Vector2Int>();
            foreach (var portal in portals)
            foreach (var band in TacticalTrapTopology.DoorwayBands(portal))
            {
                var run = TacticalTrapTopology.BestRun(band, budget, Allowed);
                if (run.Count > bestDoorway.Count) bestDoorway = run;
                if (bestDoorway.Count == budget) return bestDoorway;
            }
            return bestDoorway;
        }
        Vector2Int normal = Directions[orientation % 4], side = new Vector2Int(-normal.y, normal.x);
        if (portals.Count > 0)
        {
            normal = portals[0].Inward;
            side = new Vector2Int(-normal.y, normal.x);
        }
        if (pattern == TacticalTrapPattern.RangedScreen || pattern == TacticalTrapPattern.TreasureGuard)
        {
            Vector2Int delta = (portals.Count > 0 ? portals[0].Cell : center) - focus;
            normal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? new Vector2Int(delta.x >= 0 ? 1 : -1, 0) :
                new Vector2Int(0, delta.y >= 0 ? 1 : -1);
            side = new Vector2Int(-normal.y, normal.x);
        }
        // Mỗi ứng viên là một cụm có hình dạng; chọn cụm hợp lệ nhất, không rải bù lung tung.
        var best = new List<Vector2Int>();
        for (int shift = -1; shift <= 2; shift++)
        {
            var cluster = new List<Vector2Int>();
            if (pattern == TacticalTrapPattern.BossFlanks)
            {
                int span = Mathf.Max(3, (room.FloorTiles.Max(p => p.x) - room.FloorTiles.Min(p => p.x)) / 3);
                // Hai cụm đối xứng nguyên vẹn, không lấy vài ô rời ở hai bên boss.
                for (int length = Mathf.Min(3, budget / 2); length >= 1; length--)
                {
                    cluster.Clear();
                    foreach (int sign in new[] { -1, 1 }) for (int step = 0; step < length; step++)
                        cluster.Add(center + Vector2Int.right * (sign * span) + Vector2Int.up * (step + shift));
                    if (cluster.All(Allowed)) { if (cluster.Count > best.Count) best = new List<Vector2Int>(cluster); break; }
                }
            }
            else
            {
                Vector2Int band = pattern == TacticalTrapPattern.Chicane ? center + normal * (2 + shift) : focus + normal * (3 + shift);
                for (int step = -3; step <= 3; step++) cluster.Add(band + side * step);
                var valid = TacticalTrapTopology.BestRun(cluster, budget, Allowed);
                if (valid.Count > best.Count) best = valid;
            }
        }
        return best;
    }

    private bool IsSafeCandidate(Vector2Int tile, DungeonRoom room, HashSet<Vector2Int> occupied, bool allowCorridor = false)
    {
        bool inRoom = room.FloorTiles.Contains(tile);
        if ((!inRoom && (!allowCorridor || !corridorTiles.Contains(tile))) || Reserved.Contains(tile) || occupied.Contains(tile)) return false;
        // Không sát tường, không dưới chân tượng/cột/rương. Kiểm tra thân và cả phần hình mở rộng.
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            if (!(allowCorridor ? layout.FloorTiles : room.FloorTiles).Contains(tile + new Vector2Int(x, y))) return false;
        Vector3 point = Position(tile);
        if (DistanceToSafeRoute(point) < SafeClearance) return false;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(point, out hit, 0.2f, NavMesh.AllAreas)) return false;
        foreach (var collider in Physics2D.OverlapCircleAll(point, 0.7f, EnemyCombat.ResolveWallLayers()))
            if (!collider.isTrigger) return false;
        return true;
    }
}
