using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TacticalTrapValidation
{
    public static void ValidateScene(string path, int seedOffset)
    {
        int groups = 0, verified = 0, skipped = 0;
        var patterns = new HashSet<TacticalTrapPattern>();
        for (int level = 1; level <= 10; level++)
        {
            EditorSceneManager.OpenScene(path);
            var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
            if (!generator.GenerateLevel(LevelDefinition.CreateDefaultCampaign()[level - 1], 10, 12345 + level * 1009 + seedOffset))
                throw new InvalidOperationException("Generation failed: " + path + " level " + level);
            ValidateCurrent(generator);
            var found = UnityEngine.Object.FindObjectsOfType<TacticalTrapGroup>();
            groups += found.Length;
            foreach (var item in found) patterns.Add(item.Pattern);
            if (generator.LastTrapPlan.RoutesVerified) verified++; else skipped++;
        }
        if (groups == 0) throw new InvalidOperationException("No tactical layouts were exercised: " + path);
        Debug.Log($"TACTICAL_TRAPS_SCENE_PASS scene={path} seedOffset={seedOffset} levels=10 verified={verified} failClosed={skipped} groups={groups} patterns={string.Join(",", patterns)}");
    }

    public static void ValidateCurrent(InfiniteWorldGenerator generator)
    {
        var plan = generator.LastTrapPlan;
        var traps = UnityEngine.Object.FindObjectsOfType<SpikeTrap>();
        if (plan == null) throw new InvalidOperationException("No trap plan.");
        if (!plan.RoutesVerified)
        {
            if (traps.Length != 0) throw new InvalidOperationException("Hazards exist without verified routes.");
            return;
        }
        foreach (var path in plan.SafeRoutes)
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 from = path[i - 1], to = path[i];
            if (!plan.ClearBodySegment(from, to)) throw new InvalidOperationException("Safe route intersects static collision.");
            foreach (var trap in traps)
            {
                // Quét thể tích thân người qua AABB lớn nhất của bẫy đang bật, không dựa vào màu/state hiện thời.
                var collider = trap.GetComponent<BoxCollider2D>();
                Vector3 size = new Vector3(collider.size.x * 1.15f + plan.RouteBodySize.x,
                    collider.size.y * 1.15f + plan.RouteBodySize.y, 2f);
                var danger = new Bounds(trap.transform.position, size);
                Vector3 start = from + plan.RouteBodyOffset;
                Vector3 delta = to - from;
                if (danger.Contains(start) || danger.IntersectRay(new Ray(start, delta.normalized), out float hit) && hit <= delta.magnitude)
                    throw new InvalidOperationException("Safe route intersects fully active trap: " + trap.transform.position);
            }
        }
        foreach (var group in UnityEngine.Object.FindObjectsOfType<TacticalTrapGroup>())
        {
            if (group.Pattern != TacticalTrapPattern.BossFlanks && group.Cells.Length > 1)
            {
                var remaining = new HashSet<Vector2Int>(group.Cells);
                var pending = new Queue<Vector2Int>(); pending.Enqueue(remaining.First()); remaining.Remove(pending.Peek());
                while (pending.Count > 0)
                {
                    var at = pending.Dequeue();
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        if (remaining.Remove(at + direction)) pending.Enqueue(at + direction);
                }
                if (remaining.Count > 0) throw new InvalidOperationException("Trap cluster is scattered: " + group.Pattern);
            }
            if (group.MinimumClearance < TacticalTrapPlanner.SafeClearance)
                throw new InvalidOperationException("Trap route clearance too small.");
            if (group.GetComponentsInChildren<SpikeTrap>().Length != group.Cells.Length)
                throw new InvalidOperationException("Trap group wiring mismatch.");
            foreach (var trap in group.GetComponentsInChildren<SpikeTrap>())
            {
                if (trap.WarningSeconds < 0.8f) throw new InvalidOperationException("Warning too short.");
                foreach (var hit in Physics2D.OverlapCircleAll(trap.transform.position, 0.7f, EnemyCombat.ResolveWallLayers()))
                    if (!hit.isTrigger) throw new InvalidOperationException("Trap hidden by obstacle: " + hit.name);
            }
        }
    }
}
