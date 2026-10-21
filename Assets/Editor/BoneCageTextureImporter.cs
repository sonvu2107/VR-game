using UnityEditor;
using UnityEngine;

/// <summary>Keeps imported boss-effect frames crisp, with one sprite per PNG frame.</summary>
[InitializeOnLoad]
public sealed class BoneCageTextureImporter : AssetPostprocessor
{
    private const string Root = "Assets/Resources/Enemy/BossEffects";

    static BoneCageTextureImporter()
    {
        // This also corrects the initial import that occurred before this newly
        // added postprocessor had compiled in the currently open Unity editor.
        EditorApplication.delayCall += ReimportExistingFrames;
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        importer.alphaIsTransparency = true;
    }

    private static void ReimportExistingFrames()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null ||
                (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                 importer.textureCompression == TextureImporterCompression.Uncompressed &&
                 importer.maxTextureSize == 512 && importer.spriteImportMode == SpriteImportMode.Single))
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
