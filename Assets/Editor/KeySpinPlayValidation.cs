using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Observe real keys in Play Mode without moving the player or collecting items.</summary>
[InitializeOnLoad]
public static class KeySpinPlayValidation
{
    private static double started;
    private static bool running;
    private static readonly Dictionary<int, HashSet<string>> frames = new();
    private static readonly List<string> issues = new();
    static KeySpinPlayValidation() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        const string request = "Temp/KeySpin.playcheck";
        if (!running && File.Exists(request) && Application.isPlaying)
        {
            File.Delete(request);
            frames.Clear(); issues.Clear();
            started = EditorApplication.timeSinceStartup;
            running = true;
            for (int i = 0; i < 16; i++)
            {
                var sprite = Resources.Load<Sprite>($"Pickups/KeySpin/KeySpin_{i:00}");
                if (sprite == null || sprite.texture.filterMode != FilterMode.Point ||
                    sprite.texture.mipmapCount != 1 || !sprite.texture.isReadable ||
                    sprite.rect.size != new Vector2(32, 48) || sprite.pixelsPerUnit != 32)
                    issues.Add("Invalid imported frame " + i);
            }
        }
        if (!running) return;
        foreach (var key in UnityEngine.Object.FindObjectsOfType<DungeonKey>())
        {
            var renderer = key.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) continue;
            int id = key.GetInstanceID();
            if (!frames.TryGetValue(id, out var seen)) frames[id] = seen = new HashSet<string>();
            seen.Add(renderer.sprite.name);
            if (renderer.color != Color.white && !issues.Contains("Unexpected tint"))
                issues.Add("Unexpected tint");
            var trigger = key.GetComponent<CircleCollider2D>();
            if ((trigger == null || !trigger.isTrigger || !Mathf.Approximately(trigger.radius, .6f)) &&
                !issues.Contains("Invalid pickup trigger")) issues.Add("Invalid pickup trigger");
        }
        if (EditorApplication.timeSinceStartup - started < 6 && Application.isPlaying) return;
        running = false;
        if (frames.Count == 0) issues.Add("No live keys observed");
        if (!frames.Values.Any(s => s.Count == 16)) issues.Add("Did not observe all 16 frames on a live key");
        Directory.CreateDirectory("Captures/KeySpin");
        var result = new List<string> { issues.Count == 0 ? "PASS" : "INCOMPLETE" };
        result.Add("Observed actual DungeonKey sprites in Play Mode for 6 seconds.");
        result.AddRange(frames.Select(p => "Key " + p.Key + ": " + p.Value.Count + " distinct frames: " +
            string.Join(", ", p.Value.OrderBy(s => s))));
        result.AddRange(issues);
        result.Add("Pickup trigger checked; did not collect a key or exercise two-machine LAN.");
        File.WriteAllLines("Captures/KeySpin/play-validation.txt", result);
        if (Application.isPlaying) ScreenCapture.CaptureScreenshot("Captures/KeySpin/in-game.png");
        Debug.Log("KEY_SPIN_PLAY_CHECK " + result[0]);
    }
}
