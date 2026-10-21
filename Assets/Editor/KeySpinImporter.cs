using UnityEditor;
using UnityEngine;

public sealed class KeySpinImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Pickups/KeySpin/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.spritePivot = new Vector2(.5f, .5f);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.isReadable = true; // LAN can transmit frames absent from an older asset catalog.
    }
}
