using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class AbilityVfxAssetIntegration : AssetPostprocessor
{
    private static readonly string[] Folders =
    {
        "E_SpinSlash_6f",
        "AshenJudgmentRunePixel_6f",
        "AshenJudgmentBladesPixel_6f",
        "AshenJudgmentFinalPixel_6f"
    };

    static AbilityVfxAssetIntegration()
    {
        // Validation is read-only. It catches missing/unreadable frames as soon
        // as scripts reload without forcing all textures to reimport again.
        EditorApplication.delayCall += ValidateAfterReload;
    }

    private static void ValidateAfterReload()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ValidateAfterReload;
            return;
        }

        try
        {
            Validate();
        }
        catch (Exception exception)
        {
            Debug.LogError($"SWORD_ABILITY_VFX_RUNTIME_FAIL {exception.Message}");
        }
    }

    private static bool IsAbilityFrame(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (string folder in Folders)
        {
            if (path.Contains($"/Combat/Frames/{folder}/"))
                return true;
        }

        return false;
    }

    private void OnPreprocessTexture()
    {
        if (IsAbilityFrame(assetPath))
            ApplySettings((TextureImporter)assetImporter);
    }

    private static void ApplySettings(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    [MenuItem("Tools/The Labyrinth/Configure E and R Sword VFX")]
    public static void Configure()
    {
        int configured = 0;
        foreach (string folder in Folders)
        {
            for (int frame = 0; frame < 6; frame++)
            {
                string path = $"Assets/Resources/Combat/Frames/{folder}/Frame_{frame:00}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"Missing sword ability frame: {path}");
                    continue;
                }

                bool changed = importer.textureType != TextureImporterType.Default ||
                               !importer.isReadable || importer.mipmapEnabled ||
                               importer.filterMode != FilterMode.Point ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               !importer.alphaIsTransparency;
                if (changed)
                {
                    ApplySettings(importer);
                    importer.SaveAndReimport();
                }

                configured++;
            }
        }

        Debug.Log($"SWORD_ABILITY_VFX_IMPORT_PASS frames={configured} individualPng=true");
    }

    [MenuItem("Tools/The Labyrinth/Validate E and R Sword VFX")]
    public static void Validate()
    {
        int validated = 0;
        foreach (string folder in Folders)
        {
            string resourcePath = $"Combat/Frames/{folder}";
            for (int frame = 0; frame < 6; frame++)
            {
                string assetPath = $"Assets/Resources/{resourcePath}/Frame_{frame:00}.png";
                Texture2D texture = Resources.Load<Texture2D>($"{resourcePath}/Frame_{frame:00}");
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (texture == null || importer == null || !importer.isReadable || importer.mipmapEnabled ||
                    importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    throw new InvalidOperationException($"Invalid sword ability frame: {assetPath}");
                }

                validated++;
            }

            Type controllerType = typeof(PlayerSkillController);
            Type anchorType = controllerType.GetNestedType("FrameAnchorMode", BindingFlags.NonPublic);
            MethodInfo loader = controllerType.GetMethod("LoadPixelFrames",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (anchorType == null || loader == null)
                throw new InvalidOperationException("Could not locate the sword ability frame loader.");

            object cellCenter = Enum.Parse(anchorType, "CellCenter");
            Sprite[] sprites = loader.Invoke(null, new[] { resourcePath, (object)6, cellCenter, 64f }) as Sprite[];
            if (sprites == null || sprites.Length != 6)
                throw new InvalidOperationException($"Could not build sword ability sprites: {resourcePath}");

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null || sprite.name.Contains("Fallback"))
                    throw new InvalidOperationException($"Fallback used for sword ability sprites: {resourcePath}");

                Texture2D generatedTexture = sprite.texture;
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(generatedTexture);
            }
        }

        Debug.Log($"SWORD_ABILITY_VFX_RUNTIME_PASS frames={validated} sequences={Folders.Length} fallback=false");
    }
}
