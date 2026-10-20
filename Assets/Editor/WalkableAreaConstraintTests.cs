using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Editor assembly tests can reference the runtime-generated map classes.
public sealed class WalkableAreaConstraintTests
{
    private static readonly Vector2 PlayerExtents = new(0.2f, 0.25f);

    [Test]
    public void GeneratorSpawnAnchor_AllowsNormalKeyboardSteps()
    {
        HashSet<Vector2Int> floor = Rectangle(-2, -2, 5, 5);
        Vector2 spawn = new(0f, 1f); // PrefabPlacer.GetWorldPosition(Vector2Int.zero)
        Vector2 lookupOffset = new(.5f, -.5f);

        foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
        {
            Vector2 movement = WalkableAreaConstraint.ConstrainDelta(
                floor, spawn, direction * .1f, Vector2.zero, Vector2.zero, lookupOffset);
            Assert.That(movement, Is.EqualTo(direction * .1f), "direction=" + direction);
        }
    }

    [Test]
    public void LongDash_CannotTunnelAcrossOneTileBorder()
    {
        HashSet<Vector2Int> floor = Rectangle(0, 0, 8, 8);

        Vector2 movement = WalkableAreaConstraint.ConstrainDelta(
            floor, new Vector2(4.5f, 4.5f), new Vector2(12f, 0f), Vector2.zero, PlayerExtents);
        Vector2 result = new Vector2(4.5f, 4.5f) + movement;

        Assert.That(result.x, Is.LessThan(8f));
        Assert.That(WalkableAreaConstraint.ContainsFootprint(
            floor, result, Vector2.zero, PlayerExtents), Is.True);
    }

    [Test]
    public void DiagonalDash_CannotCutAcrossConcaveCorner()
    {
        HashSet<Vector2Int> floor = Rectangle(0, 0, 3, 3);
        floor.UnionWith(Rectangle(3, 2, 3, 1));

        Vector2 start = new(2.5f, 1.5f);
        Vector2 movement = WalkableAreaConstraint.ConstrainDelta(
            floor, start, new Vector2(2f, 2f), Vector2.zero, PlayerExtents);
        Vector2 result = start + movement;

        Assert.That(WalkableAreaConstraint.ContainsFootprint(
            floor, result, Vector2.zero, PlayerExtents), Is.True);
    }

    [Test]
    public void EveryCampaignLayout_KeepsSweptMovementOnItsFloor()
    {
        List<RoomType> rooms = new()
        {
            RoomType.Start, RoomType.Combat, RoomType.Combat,
            RoomType.Treasure, RoomType.Boss, RoomType.Exit
        };

        for (int style = 0; style < 8; style++)
        for (int seed = 0; seed < 24; seed++)
        {
            DungeonLayout layout = CampaignDungeonLayout.Generate(
                rooms, new SeededRandom(seed), (CampaignLayoutStyle)style);
            foreach (DungeonRoom room in layout.Rooms)
            {
                Vector2 start = (Vector2)room.SpawnPoint + Vector2.one * 0.5f;
                foreach (Vector2 direction in new[]
                {
                    Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                    new Vector2(1f, 1f).normalized,
                    new Vector2(-1f, 1f).normalized,
                    new Vector2(1f, -1f).normalized,
                    new Vector2(-1f, -1f).normalized
                })
                {
                    Vector2 movement = WalkableAreaConstraint.ConstrainDelta(
                        layout.FloorTiles, start, direction * 100f, Vector2.zero, PlayerExtents);
                    Assert.That(WalkableAreaConstraint.ContainsFootprint(
                        layout.FloorTiles, start + movement, Vector2.zero, PlayerExtents), Is.True,
                        $"style={style}, seed={seed}, room={room.Type}, direction={direction}");
                }
            }
        }
    }

    private static HashSet<Vector2Int> Rectangle(int x, int y, int width, int height)
    {
        HashSet<Vector2Int> result = new();
        for (int iy = y; iy < y + height; iy++)
        for (int ix = x; ix < x + width; ix++)
            result.Add(new Vector2Int(ix, iy));
        return result;
    }
}
