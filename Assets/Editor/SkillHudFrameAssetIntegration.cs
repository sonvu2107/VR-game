using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class SkillHudFrameAssetIntegration : AssetPostprocessor
{
    private const string FrameAssetPath =
        "Assets/Resources/UI/SkillHud/SkillSlotFrame_ObsidianCyan_v2.png";
    private static readonly string[] CharacterIconPaths =
    {
        "Assets/Resources/UI/Skills/AzureWarden/IconAegisRush.png",
        "Assets/Resources/UI/Skills/AzureWarden/IconAegisLance.png",
        "Assets/Resources/UI/Skills/AzureWarden/IconWardbreaker.png",
        "Assets/Resources/UI/Skills/AzureWarden/IconCitadelVerdict.png",
        "Assets/Resources/UI/Skills/Chronoblade/IconRiftStep.png",
        "Assets/Resources/UI/Skills/Chronoblade/IconRiftCutter.png",
        "Assets/Resources/UI/Skills/Chronoblade/IconTimeFracture.png",
        "Assets/Resources/UI/Skills/Chronoblade/IconZeroHour.png",
        "Assets/Resources/UI/Skills/GravewindRanger/IconMistStep.png",
        "Assets/Resources/UI/Skills/GravewindRanger/IconWraithFan.png",
        "Assets/Resources/UI/Skills/GravewindRanger/IconGraveSnare.png",
        "Assets/Resources/UI/Skills/GravewindRanger/IconNightfall.png"
    };

    static SkillHudFrameAssetIntegration()
    {
        EditorApplication.delayCall += ConfigureAndValidate;
    }

    private static bool IsHudAsset(string path)
    {
        if (string.Equals(path, FrameAssetPath, StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (string iconPath in CharacterIconPaths)
            if (string.Equals(path, iconPath, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void OnPreprocessTexture()
    {
        if (IsHudAsset(assetPath))
            Apply((TextureImporter)assetImporter);
    }

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    private static bool NeedsConfiguration(TextureImporter importer) =>
        importer.textureType != TextureImporterType.Sprite ||
        importer.spriteImportMode != SpriteImportMode.Single ||
        importer.mipmapEnabled || importer.filterMode != FilterMode.Point ||
        importer.wrapMode != TextureWrapMode.Clamp ||
        importer.textureCompression != TextureImporterCompression.Uncompressed ||
        !importer.alphaIsTransparency;

    [MenuItem("Tools/The Labyrinth/Configure Skill HUD Frame")]
    public static void ConfigureAndValidate()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConfigureAndValidate;
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(FrameAssetPath) as TextureImporter;
        if (importer == null)
            return;

        bool changed = NeedsConfiguration(importer);
        if (changed)
        {
            Apply(importer);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameAssetPath);
        if (sprite == null || sprite.texture.width != 132 || sprite.texture.height != 132)
            throw new InvalidOperationException("Skill HUD frame must import as one 132x132 sprite.");

        foreach (string iconPath in CharacterIconPaths)
        {
            TextureImporter iconImporter = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (iconImporter == null)
                throw new InvalidOperationException($"Missing character HUD icon: {iconPath}");
            if (NeedsConfiguration(iconImporter))
            {
                Apply(iconImporter);
                iconImporter.SaveAndReimport();
            }
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null || icon.texture.width != icon.texture.height || icon.texture.width < 256)
                throw new InvalidOperationException($"Invalid character HUD icon: {iconPath}");
        }

        Debug.Log($"SKILL_HUD_FRAME_PASS size=132x132 characterIcons={CharacterIconPaths.Length} " +
                  "sprite=true point=true transparent=true");
    }
}
