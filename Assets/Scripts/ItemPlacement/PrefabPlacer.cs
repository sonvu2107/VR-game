using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PrefabPlacer : MonoBehaviour
{
    public static int PlaceMobs(EnemyInfoSO enemyInfo, DungeonRoom room, int numberOfMobs,
        SeededRandom random, HashSet<Vector2Int> occupiedCells, int spawnClearance,
        ICollection<GameObject> spawnedObjects = null)
    {
        if (enemyInfo == null || enemyInfo.Mob.Count == 0)
        {
            Debug.LogError("Enemy spawn configuration is missing.");
            return 0;
        }

        // Filter to mobs that have a valid prefab
        List<EnemySO> validMobs = new();
        foreach (var mob in enemyInfo.Mob)
        {
            if (mob != null && mob.sprite != null)
                validMobs.Add(mob);
        }

        if (validMobs.Count == 0)
        {
            Debug.LogError("No valid mob prefabs found in EnemyInfoSO.");
            return 0;
        }

        int placedMobs = 0;

        for (int i = 0; i < numberOfMobs; i++)
        {
            if (!TryClaimSpawnPoint(room.FloorTiles, random, occupiedCells, spawnClearance, out Vector2Int spawnPoint))
            {
                Debug.LogWarning($"Not enough safe floor tiles to place every mob in the {room.Type} room.");
                break;
            }

            // Randomly pick from all valid mob types
            EnemySO chosenMob = validMobs[random.Range(0, validMobs.Count)];

            GameObject spawnedMob = Instantiate(chosenMob.sprite, GetWorldPosition(spawnPoint),
                Quaternion.identity);
            spawnedObjects?.Add(spawnedMob);
            placedMobs++;
        }

        return placedMobs;
    }

    public static bool TryClaimSpawnPoint(HashSet<Vector2Int> roomTiles, SeededRandom random,
        HashSet<Vector2Int> occupiedCells, int spawnClearance, out Vector2Int spawnPoint)
    {
        List<Vector2Int> candidates = roomTiles
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToList();

        while (candidates.Count > 0)
        {
            int candidateIndex = random.Range(0, candidates.Count);
            Vector2Int candidate = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);

            if (!IsSafeSpawnCell(candidate, roomTiles, occupiedCells, spawnClearance))
                continue;

            ReserveCells(candidate, occupiedCells, spawnClearance);
            spawnPoint = candidate;
            return true;
        }

        spawnPoint = default;
        return false;
    }

    public static Vector3 GetWorldPosition(Vector2Int cell)
    {
        return new Vector3(cell.x, cell.y + 1, 0.1f);
    }

    private static bool IsSafeSpawnCell(Vector2Int cell, HashSet<Vector2Int> roomTiles,
        HashSet<Vector2Int> occupiedCells, int spawnClearance)
    {
        for (int x = -spawnClearance; x <= spawnClearance; x++)
        {
            for (int y = -spawnClearance; y <= spawnClearance; y++)
            {
                Vector2Int checkedCell = cell + new Vector2Int(x, y);
                if (!roomTiles.Contains(checkedCell) || occupiedCells.Contains(checkedCell))
                    return false;
            }
        }

        return true;
    }

    private static void ReserveCells(Vector2Int cell, HashSet<Vector2Int> occupiedCells, int spawnClearance)
    {
        for (int x = -spawnClearance; x <= spawnClearance; x++)
        {
            for (int y = -spawnClearance; y <= spawnClearance; y++)
                occupiedCells.Add(cell + new Vector2Int(x, y));
        }
    }
}
