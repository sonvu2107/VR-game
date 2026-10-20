using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArrowCryptIntegration
{
    private const string ResourceFolder = "Assets/Resources/MapThemes/ArrowCrypt";
    private const string ThemeFolder = "Assets/Art/MapThemes/ArrowCrypt";
    private const string ThemePath = ThemeFolder + "/ArrowCryptTheme.asset";
    private const string ScenePath = "Assets/Scenes/Dungeon.unity";
    private const string CatalogPath = "Assets/Resources/LanAssetCatalog.asset";

    private static readonly string[] TextureNames =
    {
        "floor_stone", "wall_stone", "archery_target", "bow_rack", "weapon_debris", "crypt_gate",
        "arrow_trap_00_idle", "arrow_trap_01_warning", "arrow_trap_02_fire", "arrow_trap_03_reset"
    };

    [MenuItem("Tools/The Labyrinth/Configure Level 3 Arrow Crypt")]
    public static void Configure()
    {
        EnsureFolder("Assets/Art", "MapThemes");
        EnsureFolder("Assets/Art/MapThemes", "ArrowCrypt");

        List<Texture2D> textures = new();
        foreach (string textureName in TextureNames)
        {
            string path = ResourceFolder + "/" + textureName + ".png";
            ConfigureTexture(path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Arrow Crypt texture failed to import: " + path);
            textures.Add(texture);
        }

        Material floor = CreateOrUpdateMaterial(ThemeFolder + "/ArrowCryptFloor.mat", textures[0]);
        Material walls = CreateOrUpdateMaterial(ThemeFolder + "/ArrowCryptWalls.mat", textures[1]);
        CourtyardTheme theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<CourtyardTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.arrowCryptFloor = floor;
        theme.arrowCryptWalls = walls;
        theme.arrowCryptTrapsPerCombatRoom = 2;
        EditorUtility.SetDirty(theme);

        AddToLanCatalog(textures, new[] { floor, walls });
        AssignThemeToDungeon(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateImportedAssets(theme);
        Debug.Log("ARROW_CRYPT_INTEGRATION_PASS level=3 textures=10 frames=4 individualPng=true");
    }

    private static void ConfigureTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("No TextureImporter for " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateMaterial(string path, Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.mainTexture = texture;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddToLanCatalog(IEnumerable<Texture2D> textures, IEnumerable<Material> materials)
    {
        UnityEngine.Object catalog = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("LAN asset catalog is missing.");
        SerializedObject serialized = new(catalog);
        SerializedProperty textureArray = serialized.FindProperty("textures");
        SerializedProperty materialArray = serialized.FindProperty("materials");
        foreach (Texture2D texture in textures) AddUnique(textureArray, texture);
        foreach (Material material in materials) AddUnique(materialArray, material);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void AssignThemeToDungeon(CourtyardTheme theme)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        InfiniteWorldGenerator generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        if (generator == null) throw new InvalidOperationException("Dungeon scene has no InfiniteWorldGenerator.");
        SerializedObject serialized = new(generator);
        SerializedProperty property = serialized.FindProperty("campaignThirdMap");
        if (property == null) throw new InvalidOperationException("campaignThirdMap field is missing.");
        property.objectReferenceValue = theme;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ValidateImportedAssets(CourtyardTheme theme)
    {
        if (theme == null || !theme.UsesArrowCryptArt)
            throw new InvalidOperationException("Arrow Crypt theme materials are incomplete.");
        foreach (string textureName in TextureNames)
        {
            Sprite sprite = Resources.Load<Sprite>("MapThemes/ArrowCrypt/" + textureName);
            if (sprite == null) throw new InvalidOperationException("Arrow Crypt sprite is missing: " + textureName);
            if (sprite.texture.filterMode != FilterMode.Point || sprite.texture.mipmapCount != 1)
                throw new InvalidOperationException("Arrow Crypt sprite importer is not pixel-safe: " + textureName);
        }
    }

    private static void AddUnique(SerializedProperty array, UnityEngine.Object value)
    {
        for (int index = 0; index < array.arraySize; index++)
            if (array.GetArrayElementAtIndex(index).objectReferenceValue == value) return;
        int newIndex = array.arraySize;
        array.InsertArrayElementAtIndex(newIndex);
        array.GetArrayElementAtIndex(newIndex).objectReferenceValue = value;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
