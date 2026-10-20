#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports every playable-character animation image as one complete frame.
/// Character animation is intentionally never sliced from a sprite sheet.
/// </summary>
internal sealed class PlayableCharacterFrameTools : AssetPostprocessor
{
    // Bump this whenever the importer contract changes so Unity reimports all
    // affected character frames instead of keeping stale pivot/scale data.
    public override uint GetVersion() => 2;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Characters/"))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Match Ashen Knight exactly: 100 PPU with the feet anchored at the
        // Player transform. Center-pivoted frames visibly slide around the
        // unchanged Rigidbody2D and feel as if movement is getting stuck.
        importer.spritePixelsPerUnit = 100f;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
#endif
