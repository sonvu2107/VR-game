using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verifies the structural rules required for a generated dungeon.
/// </summary>
public static class DungeonValidator
{
    private static readonly RoomType[] RequiredRoomTypes =
    {
        RoomType.Start,
        RoomType.Treasure,
        RoomType.Boss,
        RoomType.Exit
    };

    public static bool IsValid(DungeonLayout layout, out string failureReason)
    {
        if (layout == null || layout.Rooms.Count == 0)
        {
            failureReason = "The dungeon has no rooms.";
            return false;
        }

        if (!HasRequiredRooms(layout, out failureReason) ||
            !RoomsDoNotOverlap(layout, out failureReason))
            return false;

        DungeonRoom startRoom = GetSingleRoom(layout, RoomType.Start);
        HashSet<Vector2Int> reachableTiles = GetReachableTiles(layout.FloorTiles, startRoom.SpawnPoint);

        foreach (DungeonRoom room in layout.Rooms)
        {
            if (!reachableTiles.Contains(room.SpawnPoint))
            {
                failureReason = $"The {room.Type} room at {room.SpawnPoint} cannot be reached from Start.";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool HasRequiredRooms(DungeonLayout layout, out string failureReason)
    {
        foreach (RoomType roomType in RequiredRoomTypes)
        {
            if (CountRooms(layout, roomType) != 1)
            {
                failureReason = $"Expected exactly one {roomType} room.";
                return false;
            }
        }

        if (CountRooms(layout, RoomType.Combat) == 0)
        {
            failureReason = "Expected at least one Combat room.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool RoomsDoNotOverlap(DungeonLayout layout, out string failureReason)
    {
        HashSet<Vector2Int> occupiedRoomTiles = new();

        foreach (DungeonRoom room in layout.Rooms)
        {
            foreach (Vector2Int tile in room.FloorTiles)
            {
                if (!occupiedRoomTiles.Add(tile))
                {
                    failureReason = $"The {room.Type} room overlaps another room at {tile}.";
                    return false;
                }
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private static HashSet<Vector2Int> GetReachableTiles(HashSet<Vector2Int> floorTiles, Vector2Int startPosition)
    {
        HashSet<Vector2Int> reachableTiles = new();
        Queue<Vector2Int> positionsToVisit = new();

        if (!floorTiles.Contains(startPosition))
            return reachableTiles;

        reachableTiles.Add(startPosition);
        positionsToVisit.Enqueue(startPosition);

        while (positionsToVisit.Count > 0)
        {
            Vector2Int currentPosition = positionsToVisit.Dequeue();

            foreach (Vector2Int direction in Direction2D.cardinalDirectionsList)
            {
                Vector2Int neighbour = currentPosition + direction;
                if (floorTiles.Contains(neighbour) && reachableTiles.Add(neighbour))
                    positionsToVisit.Enqueue(neighbour);
            }
        }

        return reachableTiles;
    }

    private static int CountRooms(DungeonLayout layout, RoomType roomType)
    {
        int count = 0;
        foreach (DungeonRoom room in layout.Rooms)
        {
            if (room.Type == roomType)
                count++;
        }

        return count;
    }

    private static DungeonRoom GetSingleRoom(DungeonLayout layout, RoomType roomType)
    {
        foreach (DungeonRoom room in layout.Rooms)
        {
            if (room.Type == roomType)
                return room;
        }

        return null;
    }
}
