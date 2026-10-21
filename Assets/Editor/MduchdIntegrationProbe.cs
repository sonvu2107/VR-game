using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Kiểm tra theo yêu cầu, không tự chạy và không lưu scene/prefab.
public static class MduchdIntegrationProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void ValidateAssets()
    {
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        Require(catalog != null, "Missing LAN catalog.");
        catalog.Initialize();
        string[] names = { "CasterBodyAuraPixel_6f", "DashBurstPixel_6f", "SlashVfxHit1_6f",
            "SlashVfxHit2_6f", "SlashVfxHit3_6f", "SwordWaveImpactPixel_6f", "SwordWavePixel_6f" };
        int checkedFrames = 0;
        foreach (string name in names)
        for (int frame = 0; frame < 6; frame++)
        {
            string resource = $"Combat/Frames/{name}/Frame_{frame:00}";
            var texture = Resources.Load<Texture2D>(resource);
            Require(texture != null && texture.isReadable, "Missing/unreadable frame: " + resource);
            Require(catalog.Id(texture) >= 0, "Frame missing from LAN catalog: " + resource);
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            Require(importer != null && !importer.mipmapEnabled && importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed, "Incorrect pixel import: " + resource);
            checkedFrames++;
        }
        foreach (var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>(true))
            Require(text.font != null && text.font.material != null && text.font.atlasTexture != null,
                "Incomplete TMP font: " + text.name);
        Debug.Log($"MDUCHD_ASSET_PASS frames={checkedFrames} readable=true pixelImport=true lanCatalog=true fonts=true");
    }

    public static void ValidatePlaying()
    {
        Require(EditorApplication.isPlaying, "Run in Play Mode after generation.");
        var manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        Require(manager != null && manager.CurrentState == GameState.Playing, "Wait for Playing state.");
        PlayerMovement player = manager.PlayerMovement;
        Require(!player.HasRemoteInput, "Run this fixture in Solo, not on an active LAN guest.");
        var body = player.GetComponent<Rigidbody2D>();
        var skills = player.GetComponent<PlayerSkillController>();
        bool playerEnabled = player.enabled, skillsEnabled = skills.enabled;
        Vector2 originalPosition = body.position, originalVelocity = body.linearVelocity;
        SimulationMode2D simulation = Physics2D.simulationMode;
        GameObject wall = null, bridgeRoot = null;
        LanPlayerInput remote = null;
        Keyboard keyboard = null;
        Mouse mouse = null;
        try
        {
            player.enabled = false;
            skills.enabled = false;
            player.ResetForLanRun();
            keyboard = InputSystem.AddDevice<Keyboard>("Mduchd probe keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Mduchd probe mouse");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            InputSystem.Update();
            Vector2 keyboardMove = (Vector2)Call(player, "ReadLocalMovement");
            Require(Vector2.Distance(keyboardMove, new Vector2(1, 1).normalized) < 0.001f,
                "Disabled action-map keyboard fallback/normalization failed.");
            Require((bool)Call(player, "WasDashPressed"), "Solo right mouse dash missing.");

            bridgeRoot = new GameObject("LAN input merge probe");
            bridgeRoot.SetActive(false);
            var bridge = bridgeRoot.AddComponent<LanSessionBridge>();
            Set(bridge, "manager", manager);
            Set(bridge, "<LocalState>k__BackingField", new LanPlayerState { health = 10 });
            Call(bridge, "SampleInput");
            var frame = (LanInputFrame)Get(bridge, "pendingInput");
            Require(frame.dash && frame.movement.sqrMagnitude > 0.99f, "LAN right mouse input missing.");

            InputSystem.RemoveDevice(keyboard); keyboard = null;
            InputSystem.RemoveDevice(mouse); mouse = null;
            remote = player.gameObject.AddComponent<LanPlayerInput>();
            Set(player, "lanInput", remote);
            remote.Apply(new LanInputFrame { movement = Vector2.right });
            Call(player, "Update");
            Require((Vector2)Get(player, "_movement") == Vector2.right, "Remote input lost authority.");

            Physics2D.simulationMode = SimulationMode2D.Script;
            body.position = new Vector2(10000, 10000);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            var collider = player.GetComponent<Collider2D>();
            float startX = body.position.x;
            wall = new GameObject("Merge probe wall", typeof(BoxCollider2D));
            wall.transform.position = collider.bounds.center + Vector3.right * 2f;
            wall.GetComponent<BoxCollider2D>().size = new Vector2(0.3f, 10f);
            Physics2D.SyncTransforms();
            for (int step = 0; step < 40; step++)
            {
                Call(player, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
            Require(body.position.x > startX + 0.2f, "Velocity movement did not advance.");
            Require(collider.bounds.max.x <= wall.GetComponent<Collider2D>().bounds.min.x + 0.04f,
                "Velocity movement passed through the wall.");
            remote.Apply(default);
            Call(player, "Update"); Call(player, "FixedUpdate");
            Require(body.linearVelocity == Vector2.zero, "Release input did not stop movement.");
            manager.Pause();
            remote.Apply(new LanInputFrame { movement = Vector2.right });
            Call(player, "Update"); Call(player, "FixedUpdate");
            Require(body.linearVelocity == Vector2.zero, "Paused input moved the player.");
            manager.Resume();
            Debug.Log("MDUCHD_INPUT_PHYSICS_PASS keyboardFallback=true normalized=true soloRightMouse=true lanRightMouse=true remoteAuthority=true velocity=true wallCollision=true release=true pause=true");
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (manager.CurrentState == GameState.Paused) manager.Resume();
            Physics2D.simulationMode = simulation;
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (bridgeRoot != null) UnityEngine.Object.DestroyImmediate(bridgeRoot);
            Set(player, "lanInput", null);
            if (remote != null) UnityEngine.Object.DestroyImmediate(remote);
            player.ResetForLanRun();
            body.position = originalPosition; body.linearVelocity = originalVelocity;
            Physics2D.SyncTransforms();
            player.enabled = playerEnabled; skills.enabled = skillsEnabled;
        }
    }

    public static void ValidateLanEffects()
    {
        Require(EditorApplication.isPlaying && LanLaunch.Mode == LanMode.Solo,
            "Run this isolated fixture in Solo Play Mode.");
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog"); catalog.Initialize();
        var root = new GameObject("VFX wire replica probe"); root.SetActive(false);
        var source = new GameObject("VFX wire source probe", typeof(SpriteRenderer));
        source.transform.position = new Vector3(10000, 10000, 0);
        var renderer = source.GetComponent<SpriteRenderer>();
        var sender = new LanWorldReplication(catalog, root.transform);
        var receiver = new LanWorldReplication(catalog, root.transform);
        var sprites = new List<Sprite>();
        string address = LanLaunch.Address, error = LanLaunch.LastError;
        int port = LanLaunch.Port, checkedFrames = 0, largestPacket = 0;
        try
        {
            LanLaunch.Configure(LanMode.Host, address, port);
            var load = typeof(PlayerSkillController).GetMethod("LoadPixelFrames", BindingFlags.Static | BindingFlags.NonPublic);
            object anchor = Enum.ToObject(load.GetParameters()[2].ParameterType, 0);
            string[] names = { "CasterBodyAuraPixel_6f", "DashBurstPixel_6f", "SlashVfxHit1_6f",
                "SlashVfxHit2_6f", "SlashVfxHit3_6f", "SwordWaveImpactPixel_6f", "SwordWavePixel_6f" };
            foreach (string name in names)
            {
                var frames = (Sprite[])load.Invoke(null, new object[] { "Combat/Frames/" + name, 6, anchor });
                sprites.AddRange(frames);
                foreach (var sprite in frames)
                {
                    Require(sprite.texture.width > 64 && sprite.texture.isReadable, "Fallback/unreadable VFX: " + name);
                    renderer.sprite = sprite;
                    var message = new LanMessage(); sender.CaptureVisuals(message);
                    Require(message.definitions.Any(d => d.id == sprite.GetInstanceID() && !string.IsNullOrEmpty(d.png)),
                        "Runtime VFX PNG not transmitted.");
                    string json = JsonUtility.ToJson(message);
                    int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
                    largestPacket = Math.Max(largestPacket, bytes);
                    Require(bytes <= LanTransport.MaxMessageBytes, "VFX snapshot exceeds transport budget.");
                    receiver.ApplyVisuals(JsonUtility.FromJson<LanMessage>(json));
                    var views = (Dictionary<int, SpriteRenderer>)Get(receiver, "clientViews");
                    Require(views.TryGetValue(renderer.GetInstanceID(), out var view) && view.sprite != null &&
                        view.sprite.rect == sprite.rect && view.sprite.pivot == sprite.pivot &&
                        view.sprite.texture.GetPixels32().SequenceEqual(sprite.texture.GetPixels32()),
                        "Client VFX pixels, pivot or rect changed.");
                    checkedFrames++;
                }
            }
            Debug.Log($"MDUCHD_LAN_VFX_PASS frames={checkedFrames} pngRoundTrip=true pixels=true pivot=true maxPacketBytes={largestPacket}");
        }
        finally
        {
            sender.Dispose(); receiver.Dispose();
            UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(root);
            foreach (var sprite in sprites)
                if (sprite != null) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
            LanLaunch.Configure(LanMode.Solo, address, port); LanLaunch.LastError = error;
        }
    }

    private static object Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
