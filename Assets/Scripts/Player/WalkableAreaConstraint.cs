using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps fast player movement inside the generated floor tiles. This is a
/// gameplay safety net in addition to the visual wall colliders: a dash is
/// swept in small steps, so it cannot tunnel through a one-tile map border.
/// </summary>
public static class WalkableAreaConstraint
{
    public const float MaximumSweepStep = 0.15f;

    public static bool ContainsFootprint(
        HashSet<Vector2Int> floorTiles,
        Vector2 bodyPosition,
        Vector2 colliderCenterOffset,
        Vector2 colliderExtents,
        float skin = 0.025f,
        Vector2 cellLookupOffset = default)
    {
        if (floorTiles == null || floorTiles.Count == 0)
            return true;

        Vector2 center = bodyPosition + colliderCenterOffset;
        float extentX = Mathf.Max(0f, colliderExtents.x - skin);
        float extentY = Mathf.Max(0f, colliderExtents.y - skin);

        // The player collider is axis-aligned (rotation is frozen). Sampling
        // its centre, sides and corners prevents clipping a concave tile edge.
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            Vector2 point = center + new Vector2(extentX * x, extentY * y) + cellLookupOffset;
            Vector2Int cell = new(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            if (!floorTiles.Contains(cell))
                return false;
        }

        return true;
    }

    public static Vector2 ConstrainDelta(
        HashSet<Vector2Int> floorTiles,
        Vector2 start,
        Vector2 desiredDelta,
        Vector2 colliderCenterOffset,
        Vector2 colliderExtents,
        Vector2 cellLookupOffset = default)
    {
        if (floorTiles == null || floorTiles.Count == 0 || desiredDelta.sqrMagnitude <= 0f)
            return desiredDelta;

        int stepCount = Mathf.Max(1, Mathf.CeilToInt(desiredDelta.magnitude / MaximumSweepStep));
        Vector2 step = desiredDelta / stepCount;
        Vector2 current = start;

        for (int index = 0; index < stepCount; index++)
        {
            Vector2 fullTarget = current + step;
            if (ContainsFootprint(floorTiles, fullTarget, colliderCenterOffset, colliderExtents,
                cellLookupOffset: cellLookupOffset))
            {
                current = fullTarget;
                continue;
            }

            // Preserve natural wall sliding when only one axis is blocked.
            Vector2 horizontalTarget = current + new Vector2(step.x, 0f);
            if (Mathf.Abs(step.x) > Mathf.Epsilon &&
                ContainsFootprint(floorTiles, horizontalTarget, colliderCenterOffset, colliderExtents,
                    cellLookupOffset: cellLookupOffset))
                current = horizontalTarget;

            Vector2 verticalTarget = current + new Vector2(0f, step.y);
            if (Mathf.Abs(step.y) > Mathf.Epsilon &&
                ContainsFootprint(floorTiles, verticalTarget, colliderCenterOffset, colliderExtents,
                    cellLookupOffset: cellLookupOffset))
                current = verticalTarget;
        }

        return current - start;
    }
}
