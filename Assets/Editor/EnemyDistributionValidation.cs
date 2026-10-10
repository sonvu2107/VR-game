// Kiểm tra bằng map sinh thật trong Editor: đếm quái ở từng phòng thay vì chỉ
// kiểm tra quota tĩnh. Chạy qua Tools/Enemy Boss/Validate Room Distribution.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyDistributionValidation
{
    [MenuItem("Tools/Enemy Boss/Validate Room Distribution")]
    public static void Validate()
    {
        List<LevelDefinition> campaign = LevelDefinition.CreateDefaultCampaign();
        for (int levelNumber = 1; levelNumber <= campaign.Count; levelNumber++)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
            InfiniteWorldGenerator generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
            GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (generator == null || manager == null)
                throw new InvalidOperationException("Dungeon scene is missing generator or manager.");

            LevelDefinition definition = campaign[levelNumber - 1];
            if (!generator.GenerateLevel(definition, campaign.Count, 12345 + levelNumber * 1009))
                throw new InvalidOperationException($"Level {levelNumber} could not be generated.");

            FieldInfo spawnedField = typeof(InfiniteWorldGenerator).GetField("spawnedObjects",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var spawned = spawnedField?.GetValue(generator) as List<GameObject>;
            if (spawned == null) throw new InvalidOperationException("Cannot inspect spawned objects.");

            int total = 0;
            for (int roomIndex = 0; roomIndex < generator.GeneratedRooms.Count; roomIndex++)
            {
                DungeonRoom room = generator.GeneratedRooms[roomIndex];
                int count = 0;
                foreach (GameObject obj in spawned)
                {
                    if (obj == null || (obj.GetComponent<EnemyHealth>() == null && obj.GetComponent<Enemy>() == null))
                        continue;
                    Vector2Int cell = new(Mathf.RoundToInt(obj.transform.position.x),
                        Mathf.RoundToInt(obj.transform.position.y - 1f));
                    if (room.FloorTiles.Contains(cell)) count++;
                }
                int expected = room.Type == RoomType.Boss && definition.Type != LevelType.Standard
                    ? 1 : InfiniteWorldGenerator.GetRoomEnemyQuota(room.Type, definition.Type);
                if (count != expected)
                    throw new InvalidOperationException($"Level {levelNumber} room {roomIndex + 1} ({room.Type}): expected {expected}, got {count}.");
                if (room.Type == RoomType.Boss && definition.Type != LevelType.Standard)
                    CheckBossArtworkInsideRoom(spawned, room, levelNumber);
                total += count;
                Debug.Log($"ROOM_DISTRIBUTION level={levelNumber} room={roomIndex + 1}/{generator.GeneratedRooms.Count} type={room.Type} enemies={count}");
            }
            Debug.Log($"LEVEL_DISTRIBUTION_OK level={levelNumber} totalEnemies={total}");
        }
        Debug.Log("ENEMY_DISTRIBUTION_VALIDATION_OK");
    }

    private static void CheckBossArtworkInsideRoom(List<GameObject> spawned, DungeonRoom room, int level)
    {
        foreach (GameObject obj in spawned)
        {
            if (obj == null || (obj.GetComponent<MiniBossController>() == null &&
                                obj.GetComponent<SkeletonKingController>() == null)) continue;
            SpriteRenderer art = obj.GetComponentInChildren<SpriteRenderer>();
            if (art == null) throw new InvalidOperationException($"Level {level} boss has no art.");
            Bounds bounds = art.bounds;
            foreach (int xSign in new[] { -1, 1 })
            foreach (int ySign in new[] { -1, 1 })
            {
                Vector2Int corner = new Vector2Int(
                    Mathf.RoundToInt(bounds.center.x + xSign * bounds.extents.x),
                    Mathf.RoundToInt(bounds.center.y + ySign * bounds.extents.y - 1f));
                if (!room.FloorTiles.Contains(corner))
                    throw new InvalidOperationException($"Level {level} boss artwork reaches outside its arena at {corner}.");
            }
        }
    }
}
