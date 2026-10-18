using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArcaneCitadelMapTools
{
    public const string ScenePath = "Assets/Scenes/DungeonArcaneCitadel.unity";
    private const string Art = "Assets/Art/MapThemes/ArcaneCitadel/";

    [MenuItem("Tools/The Labyrinth/Create Arcane Citadel Map")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the active scene before creating a new map.");
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Arcane map already exists; open it instead of overwriting edits.");
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Required unlit atlas shader not found.");
        var theme = ScriptableObject.CreateInstance<CourtyardTheme>();
        theme.arcaneFloor = MakeMaterial("ArcaneCitadel_Floors_v1.png", "ArcaneFloor.mat", shader);
        theme.arcaneWalls = MakeMaterial("ArcaneCitadel_Walls_v1.png", "ArcaneWalls.mat", shader);
        theme.arcaneProps = MakeMaterial("ArcaneCitadel_Props_v1.png", "ArcaneProps.mat", shader);
        ConfigureBlueTorch(theme);
        ConfigureTrapArt(theme);
        AssetDatabase.CreateAsset(theme, Art + "ArcaneCitadelTheme.asset");
        EditorSceneManager.OpenScene(CourtyardPrototypeTools.ScenePath);
        // Save as a NEW scene before touching anything in the loaded source scene.
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(Art + "ArcaneCitadelTheme.asset");
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        generator.courtyardTheme = theme;
        var oldPreview = UnityEngine.Object.FindObjectOfType<CourtyardPreviewOnly>();
        if (oldPreview != null) UnityEngine.Object.DestroyImmediate(oldPreview.gameObject);
        generator.tilemapVisualizer.SetMapArtVisible(false);
        DungeonLayout layout = Layout();
        generator.tilemapVisualizer.Clear();
        generator.tilemapVisualizer.PaintFloorTiles(layout.FloorTiles);
        WallGenerator.CreateWalls(layout.FloorTiles, generator.tilemapVisualizer);
        GameObject preview = ArcaneCitadelGeometry.Build(layout, theme);
        preview.tag = "EditorOnly";
        preview.AddComponent<CourtyardPreviewOnly>();
        // Scene preview meshes must be persistent assets, not lost on scene reload.
        foreach (var filter in preview.GetComponentsInChildren<MeshFilter>())
            AssetDatabase.CreateAsset(filter.sharedMesh, Art + filter.name.Replace(" ", "") + "Preview.asset");
        generator.playerTransform.position = PrefabPlacer.GetWorldPosition(layout.Rooms[0].SpawnPoint);
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        Capture();
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), Quaternion.identity, 14, true);
        Selection.activeGameObject = preview;
        Debug.Log("ARCANE_MAP_CREATED rooms=7 newAtlases=3 scene=" + ScenePath);
    }

    private static Material MakeMaterial(string image, string output, Shader shader)
    {
        string path = Art + image;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        // Texture-only settings; Sprite slicing/pivots/metadata remain untouched.
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(output) };
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        AssetDatabase.CreateAsset(material, Art + output);
        return material;
    }

    public static DungeonLayout Layout() => CourtyardLayout.Generate(new List<RoomType>
    {
        RoomType.Start, RoomType.Combat, RoomType.Combat, RoomType.Combat,
        RoomType.Treasure, RoomType.Boss, RoomType.Exit
    }, new SeededRandom(12345));

    public static void RefreshPreview()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved Arcane scene in Edit Mode before refreshing its preview.");
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        ConfigureBlueTorch(generator.courtyardTheme);
        ConfigureTrapArt(generator.courtyardTheme);
        var previous = UnityEngine.Object.FindObjectOfType<CourtyardPreviewOnly>();
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var root = ArcaneCitadelGeometry.Build(Layout(), generator.courtyardTheme);
        root.tag = "EditorOnly";
        root.AddComponent<CourtyardPreviewOnly>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            string path = Art + filter.name.Replace(" ", "") + "Preview.asset";
            Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (persistent == null) AssetDatabase.CreateAsset(filter.sharedMesh, path);
            else
            {
                Mesh transient = filter.sharedMesh;
                EditorUtility.CopySerialized(transient, persistent);
                filter.sharedMesh = persistent;
                EditorUtility.SetDirty(persistent);
                UnityEngine.Object.DestroyImmediate(transient);
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture();
    }

    private static void ConfigureBlueTorch(CourtyardTheme theme)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Art + "ArcaneTorch.mat");
        if (material == null)
        {
            Shader shader = Shader.Find("TheLabyrinth/Arcane Torch Palette");
            if (shader == null) throw new InvalidOperationException("Import the Arcane torch shader first.");
            material = new Material(shader) { name = "Arcane Torch" };
            material.SetColor("_FlameColor", new Color(0.12f, 0.95f, 1f));
            AssetDatabase.CreateAsset(material, Art + "ArcaneTorch.mat");
        }
        theme.arcaneTorch = material;
        EditorUtility.SetDirty(theme);
    }

    private static void ConfigureTrapArt(CourtyardTheme theme)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Art + "ArcaneSpikes.mat");
        if (material == null)
            material = MakeMaterial("ArcaneCitadel_Spikes_v1.png", "ArcaneSpikes.mat", Shader.Find("Sprites/Default"));
        theme.arcaneSpike = material;
        EditorUtility.SetDirty(theme);
    }

    public static void Capture()
    {
        string previous = Environment.GetEnvironmentVariable("LABYRINTH_MAP_ARTIFACTS");
        string folder = Path.GetFullPath("Build/ArcaneCitadelPreview");
        Directory.CreateDirectory(folder);
        try
        {
            Environment.SetEnvironmentVariable("LABYRINTH_MAP_ARTIFACTS", folder);
            CourtyardPrototypeTools.CapturePreview(Layout());
        }
        finally { Environment.SetEnvironmentVariable("LABYRINTH_MAP_ARTIFACTS", previous); }
    }
}
