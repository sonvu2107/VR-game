#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports every playable-character animation image as one complete frame.
/// Character animation is intentionally never sliced from a sprite sheet.
/// </summary>
internal sealed class PlayableCharacterFrameTools : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Characters/"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 80f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
#endif
