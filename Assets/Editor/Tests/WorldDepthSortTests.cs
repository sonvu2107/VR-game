using NUnit.Framework;
using UnityEngine;

public sealed class WorldDepthSortTests
{
    [TestCase(-120f)]
    [TestCase(0f)]
    [TestCase(136f)]
    public void BehindAndInFront_SortOnOppositeSidesOfProp(float propY)
    {
        Assert.That(WorldDepthSort.OrderAt(propY + .5f), Is.LessThan(WorldDepthSort.OrderAt(propY)));
        Assert.That(WorldDepthSort.OrderAt(propY - .5f), Is.GreaterThan(WorldDepthSort.OrderAt(propY)));
        Assert.That(WorldDepthSort.OrderAt(propY), Is.InRange(100, 29000));
    }

    [Test]
    public void MovingActor_ChangesOrderAndKeepsSlashAboveBody()
    {
        var actor = new GameObject("Depth test actor");
        try
        {
            var body = actor.AddComponent<SpriteRenderer>(); body.sortingOrder = 5;
            var slashObject = new GameObject("Slash"); slashObject.transform.SetParent(actor.transform, false);
            var slash = slashObject.AddComponent<SpriteRenderer>(); slash.sortingOrder = 6;
            var sorter = WorldDepthSort.Attach(actor, 0);
            sorter.RefreshOrder();
            int before = body.sortingOrder;
            actor.transform.position = Vector3.up;
            sorter.RefreshOrder();
            Assert.That(body.sortingOrder, Is.LessThan(before));
            Assert.That(slash.sortingOrder, Is.EqualTo(body.sortingOrder + 1));
            // Không tích lũy offset qua các frame.
            sorter.RefreshOrder();
            Assert.That(slash.sortingOrder, Is.EqualTo(body.sortingOrder + 1));
            slashObject.transform.localPosition = Vector3.up * 3;
            sorter.RefreshOrder();
            Assert.That(body.sortingOrder, Is.EqualTo(WorldDepthSort.OrderAt(1)));
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void TwoActors_CanStandOnOppositeSidesOfSameProp()
    {
        var back = new GameObject("Back player"); var front = new GameObject("Front player");
        try
        {
            var backVisual = back.AddComponent<SpriteRenderer>();
            var frontVisual = front.AddComponent<SpriteRenderer>();
            var backSort = WorldDepthSort.Attach(back, 0);
            var frontSort = WorldDepthSort.Attach(front, 0);
            back.transform.position = new Vector3(0, 10.5f);
            front.transform.position = new Vector3(0, 9.5f);
            backSort.RefreshOrder(); frontSort.RefreshOrder();
            int propOrder = WorldDepthSort.OrderAt(10);
            Assert.That(backVisual.sortingOrder, Is.LessThan(propOrder));
            Assert.That(frontVisual.sortingOrder, Is.GreaterThan(propOrder));
        }
        finally { Object.DestroyImmediate(back); Object.DestroyImmediate(front); }
    }
}
