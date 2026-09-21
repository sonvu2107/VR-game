// Generates a room using the Random Walk algorithm

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomGenerator
{
    private RandomWalkSO randomWalkParameters;
    private readonly SeededRandom random;

    public RoomGenerator(RandomWalkSO randomWalkParameters, TilemapVisualizer tilemapVisualizer, SeededRandom random)
    {
        this.randomWalkParameters = randomWalkParameters;
        this.random = random;
    }

    // This can be used to generate a single room, not a series of rooms connected by corridors like CorridorFirstGenerator
    // This function is overriden again in CorridorFirstGenerator, run that to get a group of rooms
    //
    // Returns the positions of the room created
    public HashSet<Vector2Int> GenerateRoom(Vector2Int startPosition)
    {
        // Hashset of all 2D coordinates that are marked as floor
        HashSet<Vector2Int> floorPositions = RunRandomWalk(randomWalkParameters, startPosition);
        
        return floorPositions;
    }
    
    protected HashSet<Vector2Int> RunRandomWalk(RandomWalkSO parameters, Vector2Int position)
    {
        var currentPosition = position;
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
        
        for (int i = 0; i < parameters.iterations; i++)
        {
            var path = RandomWalk.Walk(currentPosition, parameters.walkLength, random);
            floorPositions.UnionWith(path);
            if (parameters.startRandomlyEachIteration)
                currentPosition = random.Choose(GetOrderedPositions(floorPositions));
        }

        return floorPositions;
    }

    private static List<Vector2Int> GetOrderedPositions(IEnumerable<Vector2Int> positions)
    {
        return positions
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToList();
    }
}
