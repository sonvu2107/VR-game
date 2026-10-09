// Chạy một lần sau khi thêm PNG: cấu hình import đúng kiểu Sprite, tắt nén để
// pixel-art sắc nét, rồi gắn Slime mới vào prefab mà không đổi các component AI.
using UnityEditor;
using UnityEngine;

public static class EnemyArtSetup
{
    [MenuItem("Tools/Enemy Boss/Apply Readable Art")]
    public static void Apply()
    {
        const string slimePath = "Assets/Art/Enemies/TinyCreatures/PoisonSlimeReadable.png";
        // 512 px / 400 PPU ~= 1.28 Unity units, close to the player silhouette.
        ConfigureSprite(slimePath, 400f);
        for (int i = 1; i <= 4; i++)
            ConfigureSprite($"Assets/Resources/Enemy/Arrows/arrow_{i}.png", 64f);

        string[] iconNames = { "IconDash", "IconSwordWave", "IconSpinSlash", "IconAshenJudgment" };
        foreach (string icon in iconNames)
            ConfigureSprite($"Assets/Resources/UI/Skills/{icon}.png", 100f);

        const string prefabPath = "Assets/Prefabs/EnemyBoss/PoisonSlime.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(slimePath);
            if (sprite == null) throw new System.InvalidOperationException("Poison Slime sprite import failed.");
            root.GetComponent<SpriteRenderer>().sprite = sprite;
            root.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_ART_SETUP_OK: slime, arrow animation and four skill icons imported.");
    }

    private static void ConfigureSprite(string assetPath, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException($"Missing PNG: {assetPath}");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = assetPath == "Assets/Art/Enemies/TinyCreatures/PoisonSlimeReadable.png"
            ? 512 : pixelsPerUnit >= 100f ? 256 : 64;
        importer.SaveAndReimport();
    }
}
