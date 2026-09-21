using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collects the generated rooms and all walkable floor tiles for a dungeon run.
/// </summary>
public sealed class DungeonLayout
{
    private readonly List<DungeonRoom> rooms = new();

    public IReadOnlyList<DungeonRoom> Rooms => rooms;
    public HashSet<Vector2Int> FloorTiles { get; } = new();

    public void AddRoom(DungeonRoom room)
    {
        rooms.Add(room);
        FloorTiles.UnionWith(room.FloorTiles);
    }

    public void AddCorridor(IEnumerable<Vector2Int> corridorTiles)
    {
        FloorTiles.UnionWith(corridorTiles);
    }
}
