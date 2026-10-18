using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class CourtyardPrototypeTools
{
    public const string ScenePath = "Assets/Scenes/DungeonCourtyardPrototype.unity";
    public const string ThemePath = "Assets/Scripts/ScriptableObjects/CourtyardTheme.asset";
    private const string AtlasPath = "Assets/Imports/2D Pixel Dungeon Asset Pack/character and tileset/Dungeon_Tileset.png";
    private static string Output => Environment.GetEnvironmentVariable("LABYRINTH_MAP_ARTIFACTS") ?? "Build/CourtyardPreview";

    [MenuItem("Tools/The Labyrinth/Create Courtyard Prototype")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before creating the prototype.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Courtyard prototype", "Recreate only the prototype scene?", "Recreate", "Cancel")) return;

        Directory.CreateDirectory(Output);
        ExportAtlasGuide();
        ValidateLayouts();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        Sprite[] Pick(params int[] ids) => ids.Select(id => sprites["Dungeon_Tileset_" + id]).ToArray();
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/ScriptableObjects")) AssetDatabase.CreateFolder("Assets/Scripts", "ScriptableObjects");
        CourtyardTheme theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<CourtyardTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        theme.storageProps = Pick(80, 81, 82, 83, 84, 85);
        theme.potteryProps = Pick(38, 82, 84, 85);
        theme.remainsProps = Pick(64, 68, 77, 92, 96);
        theme.wallDetails = Pick(75, 76);
        theme.wallBanner = Pick(74)[0];
        theme.entranceSteps = Pick(49)[0];
        theme.pavingSprites = Pick(6, 7, 8, 9);
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssetIfDirty(theme);

        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        // Opening a scene can unload unreferenced assets created above.
        // Reacquire the persistent theme after that unload boundary.
        theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(ThemePath);
        if (theme == null) throw new InvalidOperationException("Courtyard theme did not survive import.");
        InfiniteWorldGenerator generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        generator.courtyardTheme = theme;
        // Prototype only: seven rooms even on its first level.
        var managerData = new SerializedObject(manager);
        managerData.FindProperty("levels").GetArrayElementAtIndex(0).FindPropertyRelative("roomCount").intValue = 7;
        managerData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(generator.gameObject.scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        var sequence = Sequence(7);
        var random = new SeededRandom(12345);
        DungeonLayout layout = CourtyardLayout.Generate(sequence, random);
        generator.tilemapVisualizer.Clear();
        generator.tilemapVisualizer.PaintFloorTiles(layout.FloorTiles);
        WallGenerator.CreateWalls(layout.FloorTiles, generator.tilemapVisualizer);
        var occupied = new HashSet<Vector2Int>();
        foreach (DungeonRoom room in layout.Rooms)
            for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++) occupied.Add(room.SpawnPoint + new Vector2Int(x, y));
        var spawned = new List<GameObject>();
        DungeonDecorator.DecorateDungeon(layout, random, occupied, spawned, 1, LevelType.Standard, true);
        CourtyardDecorator.Decorate(layout, theme, random, occupied, spawned);
        if (GameObject.Find("Courtyard Atlas Details") == null)
            throw new InvalidOperationException("Atlas preview decorations were not created.");
        var preview = new GameObject("Courtyard Preview (replaced on Play)");
        preview.tag = "EditorOnly";
        preview.AddComponent<CourtyardPreviewOnly>();
        foreach (GameObject obj in spawned)
            if (obj != null && obj.transform.parent == null) obj.transform.SetParent(preview.transform, true);
        manager.PlayerMovement.transform.position = PrefabPlacer.GetWorldPosition(layout.Rooms[0].SpawnPoint);
        EditorSceneManager.SaveScene(generator.gameObject.scene, ScenePath);
        CapturePreview(layout);
        Debug.Log("COURTYARD_CREATED " + ScenePath + " preview=" + Output);
    }

    public static void ValidateLayouts()
    {
        for (int seed = 0; seed < 100; seed++)
        for (int rooms = 6; rooms <= 7; rooms++)
        {
            DungeonLayout layout = CourtyardLayout.Generate(Sequence(rooms), new SeededRandom(seed));
            if (!DungeonValidator.IsValid(layout, out string reason)) throw new Exception($"seed {seed}: {reason}");
            var repeat = CourtyardLayout.Generate(Sequence(rooms), new SeededRandom(seed));
            if (!layout.FloorTiles.SetEquals(repeat.FloorTiles)) throw new Exception("Non-deterministic layout.");
            foreach (DungeonRoom room in layout.Rooms)
                if (!PrefabPlacer.TryClaimSpawnPoint(room.FloorTiles, new SeededRandom(seed), new HashSet<Vector2Int>(), 1, out _))
                    throw new Exception("No safe room spawn.");
        }
        Debug.Log("COURTYARD_LAYOUT_PASS cases=200 connected=ok noOverlap=ok deterministic=ok safeSpawns=ok");
    }

    public static void BuildTrialWindows()
    {
        string output = Environment.GetEnvironmentVariable("LABYRINTH_LAN_BUILD");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("LABYRINTH_LAN_BUILD is required.");
        // Separate trial executable starts directly in the new map. Main menu
        // and original Dungeon remain included for the existing exit/menu paths.
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
        {
            scenes = new[] { ScenePath, "Assets/Scenes/StartMenu.unity", "Assets/Scenes/Dungeon.unity" },
            locationPathName = output, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Courtyard build failed: " + report.summary.result);
        Debug.Log("COURTYARD_BUILD_PASS " + output);
    }

    private static List<RoomType> Sequence(int count)
    {
        var result = new List<RoomType> { RoomType.Start };
        for (int i = 0; i < count - 4; i++) result.Add(RoomType.Combat);
        result.AddRange(new[] { RoomType.Treasure, RoomType.Boss, RoomType.Exit });
        return result;
    }

    private static void ExportAtlasGuide()
    {
        Texture2D original = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        RenderTexture rt = RenderTexture.GetTemporary(original.width, original.height);
        Graphics.Blit(original, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();
        var enlarged = new Texture2D(original.width * 6, original.height * 6, TextureFormat.RGBA32, false);
        for (int y = 0; y < enlarged.height; y++)
        for (int x = 0; x < enlarged.width; x++) enlarged.SetPixel(x, y, readable.GetPixel(x / 6, y / 6));
        enlarged.Apply();
        File.WriteAllBytes(Path.Combine(Output, "atlas-guide.png"), enlarged.EncodeToPNG());
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.DestroyImmediate(readable);
        UnityEngine.Object.DestroyImmediate(enlarged);
    }

    public static void CapturePreview(DungeonLayout layout)
    {
        var cameraObject = new GameObject("Prototype Capture Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.035f, 0.065f);
        camera.cullingMask = ~(1 << 5);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        float minX = layout.FloorTiles.Min(cell => cell.x), maxX = layout.FloorTiles.Max(cell => cell.x);
        float minY = layout.FloorTiles.Min(cell => cell.y), maxY = layout.FloorTiles.Max(cell => cell.y);
        camera.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -10);
        camera.orthographicSize = Mathf.Max((maxY - minY) * 0.5f + 4, (maxX - minX) * 0.375f + 4);
        SaveCamera(camera, "courtyard-overview.png", 1920, 1440);
        DungeonRoom start = layout.Rooms[0];
        camera.transform.position = new Vector3((start.FloorTiles.Min(cell => cell.x) + start.FloorTiles.Max(cell => cell.x)) * 0.5f,
            (start.FloorTiles.Min(cell => cell.y) + start.FloorTiles.Max(cell => cell.y)) * 0.5f, -10);
        camera.orthographicSize = 11;
        SaveCamera(camera, "courtyard-detail.png", 1280, 960);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static void SaveCamera(Camera camera, string file, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Output, file), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
    }
}
