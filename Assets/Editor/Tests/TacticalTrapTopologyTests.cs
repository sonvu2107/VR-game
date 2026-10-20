using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class TacticalTrapTopologyTests
{
    [Test]
    public void Portal_UsesActualOffCenterDoorNotRoomCenter()
    {
        var roomTiles = new List<Vector2Int>();
        for (int x = 0; x < 20; x++) for (int y = 0; y < 20; y++) roomTiles.Add(new Vector2Int(x, y));
        var room = new DungeonRoom(RoomType.Combat, roomTiles, new Vector2Int(10, 10));
        var layout = new DungeonLayout(); layout.AddRoom(room);
        var corridor = new List<Vector2Int>();
        for (int x = -8; x < 0; x++) for (int y = 3; y <= 5; y++) corridor.Add(new Vector2Int(x, y));
        layout.AddCorridor(corridor);
        var portals = TacticalTrapTopology.FindPortals(layout, room);
        Assert.That(portals.Count, Is.EqualTo(1));
        Assert.That(portals[0].Cell, Is.EqualTo(new Vector2Int(0, 4)));
        Assert.That(portals[0].Inward, Is.EqualTo(Vector2Int.right));
        Assert.That(portals[0].Width, Is.EqualTo(3));
    }

    [Test]
    public void ReservedLane_SplitsBandButNeverCreatesScatteredCluster()
    {
        var band = Enumerable.Range(-4, 9).Select(x => new Vector2Int(x, 0));
        var chosen = TacticalTrapTopology.BestRun(band, 4, cell => Mathf.Abs(cell.x) >= 2);
        Assert.That(chosen.Count, Is.EqualTo(3));
        Assert.That(chosen.All(p => p.x <= -2), Is.True);
        for (int i = 1; i < chosen.Count; i++) Assert.That(chosen[i].x - chosen[i - 1].x, Is.EqualTo(1));
    }

    [Test]
    public void IsolatedSafeCells_AreSkippedInsteadOfScattered()
    {
        var band = Enumerable.Range(0, 9).Select(x => new Vector2Int(x, 0));
        Assert.That(TacticalTrapTopology.BestRun(band, 4, p => p.x % 2 == 0), Is.Empty);
    }

    [Test]
    public void MissingCellInBand_CannotBridgeTwoSeparateClusters()
    {
        var band = new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) };
        Assert.That(TacticalTrapTopology.BestRun(band, 2, _ => true), Is.Empty);
    }

    [Test]
    public void DoorwayBand_KeepsProtectedCenterLaneAndRemainsNearEntrance()
    {
        var portal = new TacticalTrapTopology.Portal(new Vector2Int(0, 4), Vector2Int.right, 3);
        var band = TacticalTrapTopology.DoorwayBands(portal).First(row => row.All(p => p.x == 3));
        var chosen = TacticalTrapTopology.BestRun(band, 3, p => Mathf.Abs(p.y - 4) >= 2);
        Assert.That(chosen.Count, Is.EqualTo(3));
        Assert.That(chosen.All(p => p.x == 3 && Mathf.Abs(p.y - 4) >= 2), Is.True);
    }
}
