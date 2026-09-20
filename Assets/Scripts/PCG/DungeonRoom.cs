using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime data for one generated room. Corridors are intentionally not part of
/// FloorTiles, so later spawn code can place objects inside rooms only.
/// </summary>
public sealed class DungeonRoom
{
    public RoomType Type { get; }
    public HashSet<Vector2Int> FloorTiles { get; }
    public Vector2Int SpawnPoint { get; private set; }

    public DungeonRoom(RoomType type, IEnumerable<Vector2Int> floorTiles, Vector2Int spawnPoint)
    {
        Type = type;
        FloorTiles = new HashSet<Vector2Int>(floorTiles);
        SpawnPoint = spawnPoint;
    }

    public void SetSpawnPoint(Vector2Int spawnPoint)
    {
        SpawnPoint = spawnPoint;
    }
}
