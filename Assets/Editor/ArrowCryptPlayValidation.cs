using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ArrowCryptPlayValidation
{
    private const string Active = "ArrowCryptProbe.Active";
    private static double started;
    private static int phase;
    private static GameManager manager;

    static ArrowCryptPlayValidation()
    {
        if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool(Active, true);
        phase = 0;
        started = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            double now = EditorApplication.timeSinceStartup;
            if (started == 0) started = now;
            Require(now - started < 90, "Arrow Crypt play probe timed out.");

            if (phase == 0)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                typeof(GameManager).GetProperty("CurrentLevel").GetSetMethod(true)
                    .Invoke(manager, new object[] { 3 });
                typeof(GameManager).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { GameState.Generating });
                typeof(GameManager).GetMethod("BeginLevelGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
                phase = 1;
                return;
            }

            if (manager.CurrentState != GameState.Playing || manager.CurrentLevel != 3) return;
            InfiniteWorldGenerator generator = manager.dungeonGenerator;
            Require(generator.ActiveTheme != null && generator.ActiveTheme.UsesArrowCryptArt,
                "Level 3 did not select Arrow Crypt theme.");
            Require(UnityEngine.Object.FindObjectsOfType<ArrowCryptGeometry>().Length == 1,
                "Arrow Crypt geometry is missing or duplicated.");
            Require(UnityEngine.Object.FindObjectsOfType<ArcaneCitadelGeometry>().Length == 0,
                "Level 3 kept Arcane Citadel geometry.");
            Require(generator.LastTrapPlan != null && generator.LastTrapPlan.RoutesVerified,
                "Level 3 trap routes are not verified.");

            SpikeTrap[] traps = UnityEngine.Object.FindObjectsOfType<SpikeTrap>();
            Require(traps.Length > 0, "Level 3 spawned no tactical traps.");
            Require(traps.All(trap => trap.GetComponentInChildren<ArrowTrapFrameVisual>() != null),
                "A Level 3 trap is missing separate-frame art.");
            ValidateSprites();
            ValidateLanRoundTrip();
            Debug.Log("ARROW_CRYPT_PLAY_PASS level=3 geometry=true props=true separateTrapFrames=4 routes=true lan=true");
            Finish(null);
        }
        catch (Exception exception)
        {
            Finish(exception);
        }
    }

    private static void ValidateSprites()
    {
        string[] names =
        {
            "floor_stone", "wall_stone", "archery_target", "bow_rack", "weapon_debris", "crypt_gate",
            "arrow_trap_00_idle", "arrow_trap_01_warning", "arrow_trap_02_fire", "arrow_trap_03_reset"
        };
        foreach (string name in names)
        {
            Sprite sprite = Resources.Load<Sprite>("MapThemes/ArrowCrypt/" + name);
            Require(sprite != null, "Missing Arrow Crypt sprite: " + name);
            Require(sprite.texture.width <= 128 && sprite.texture.height <= 128,
                "Arrow Crypt texture is too large: " + name);
            Require(sprite.texture.filterMode == FilterMode.Point, "Non-pixel filtering on " + name);
        }
    }

    private static void ValidateLanRoundTrip()
    {
        LanAssetCatalog catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        catalog.Initialize();
        GameObject root = new("Arrow Crypt LAN Probe");
        root.SetActive(false);
        LanWorldReplication sender = new(catalog, root.transform);
        LanWorldReplication receiver = new(catalog, root.transform);
        try
        {
            LanMessage message = new() { meshes = sender.CaptureMeshes() };
            sender.CaptureVisuals(message);
            Require(message.meshes.Length == 2, "Arrow Crypt should send exactly floor and wall meshes.");
            string wireJson = JsonUtility.ToJson(message);
            Require(System.Text.Encoding.UTF8.GetByteCount(wireJson) < LanTransport.MaxMessageBytes,
                "Arrow Crypt LAN packet exceeds transport budget.");
            LanMessage wire = JsonUtility.FromJson<LanMessage>(wireJson);
            receiver.ApplyMeshes(wire.meshes);
            receiver.ApplyVisuals(wire);
            Require(receiver.ReplicatedMeshCount == 2, "Arrow Crypt LAN mesh round-trip failed.");
        }
        finally
        {
            sender.Dispose();
            receiver.Dispose();
            UnityEngine.Object.Destroy(root);
        }
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        if (error != null)
        {
            Debug.LogException(error);
            Debug.Log("ARROW_CRYPT_PLAY_FAIL");
        }
        EditorApplication.ExitPlaymode();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
