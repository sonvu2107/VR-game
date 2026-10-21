using UnityEditor;
using UnityEngine;

public sealed class OrnateUIImporter : AssetPostprocessor
{
    public override uint GetVersion() => 1;
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/UI/Ornate/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        importer.spriteBorder = Border(name);
    }

    public static Vector4 Border(string name)
    {
        if (name.StartsWith("button_") || name.StartsWith("widget_") ||
            name == "panel_ornate" || name == "panel_hollow") return new Vector4(12,12,12,12);
        if (name.StartsWith("panel_")) return new Vector4(8,8,8,8);
        if (name == "scroll_track") return new Vector4(4,3,4,3);
        if (name.StartsWith("scroll_thumb")) return new Vector4(6,8,6,8);
        if (name == "stat_frame") return new Vector4(7,6,7,6);
        if (name.StartsWith("fill_")) return new Vector4(0,3,0,2);
        return Vector4.zero;
    }
}
