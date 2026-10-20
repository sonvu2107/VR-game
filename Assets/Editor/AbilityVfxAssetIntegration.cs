using System;
using System.Collections.Generic;
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
        "AshenJudgmentFinalPixel_6f",
        "AzureWardenDash_6f",
        "AzureAegisLance_v2_6f",
        "AzureWardbreaker_v2_6f",
        "AzureCitadelVerdict_v2_6f",
        "AzureAegisRam_6f",
        "AzureBastionGuard_6f",
        "AzureCitadelCollapse_6f",
        "AzureBasicShieldSweep_6f",
        "AzureBasicLanceThrust_6f",
        "AzureBasicShieldSlam_6f",
        "ChronobladeDash_6f",
        "ChronoRiftCutter_6f",
        "ChronoTimeFracture_6f",
        "ChronoZeroHour_6f",
        "ChronoBasicRisingCut_6f",
        "ChronoBasicReverseCut_6f",
        "ChronoBasicCrossFinish_6f",
        "ChronoRiftCutter_v2_6f",
        "ChronoEchoRend_6f",
        "ChronoZeroHour_v2_6f",
        "GravewindRangerDash_6f",
        "GraveWraithFan_6f",
        "GraveSnare_6f",
        "GraveNightfall_6f",
        "GraveSnareRanged_6f",
        "GraveNightfallRain_6f"
    };

    static AbilityVfxAssetIntegration()
    {
        // New generated frames initially arrive with Unity's default texture
        // settings. Repair only mismatched imports before validation so the
        // runtime loader can safely call GetPixels32 on its first scene load.
        EditorApplication.delayCall += ConfigureAndValidateAfterReload;
    }

    private static void ConfigureAndValidateAfterReload()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConfigureAndValidateAfterReload;
            return;
        }

        try
        {
            Configure();
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

    [MenuItem("Tools/The Labyrinth/Configure Player Skill VFX")]
    public static void Configure()
    {
        int configured = 0;
        foreach (string folder in Folders)
        {
            for (int frame = 0; frame < 6; frame++)
            {
                string path = $"Assets/Resources/Combat/Frames/{folder}/Frame_{frame:00}.png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"Missing player skill frame: {path}");
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

        Debug.Log($"PLAYER_SKILL_VFX_IMPORT_PASS frames={configured} sequences={Folders.Length} individualPng=true");
    }

    [MenuItem("Tools/The Labyrinth/Validate Player Skill VFX")]
    public static void Validate()
    {
        int validated = 0;
        foreach (string folder in Folders)
        {
            string resourcePath = $"Combat/Frames/{folder}";
            HashSet<int> uniqueFrameHashes = new HashSet<int>();
            bool requiresDistinctFrames = folder.StartsWith("AzureBasic", StringComparison.Ordinal) ||
                                          folder == "AzureAegisRam_6f" ||
                                          folder == "AzureBastionGuard_6f" ||
                                          folder == "AzureCitadelCollapse_6f" ||
                                          folder.StartsWith("ChronoBasic", StringComparison.Ordinal) ||
                                          folder == "ChronoRiftCutter_v2_6f" ||
                                          folder == "ChronoEchoRend_6f" ||
                                          folder == "ChronoZeroHour_v2_6f";
            for (int frame = 0; frame < 6; frame++)
            {
                string assetPath = $"Assets/Resources/{resourcePath}/Frame_{frame:00}.png";
                Texture2D texture = Resources.Load<Texture2D>($"{resourcePath}/Frame_{frame:00}");
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (texture == null || importer == null || !importer.isReadable || importer.mipmapEnabled ||
                    importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    throw new InvalidOperationException($"Invalid player skill frame: {assetPath}");
                }

                if (requiresDistinctFrames)
                {
                    unchecked
                    {
                        int frameHash = 17;
                        foreach (Color32 pixel in texture.GetPixels32())
                            frameHash = frameHash * 31 + pixel.GetHashCode();
                        uniqueFrameHashes.Add(frameHash);
                    }
                }

                validated++;
            }

            if (requiresDistinctFrames && uniqueFrameHashes.Count != 6)
                throw new InvalidOperationException(
                    $"Sequence must contain six distinct standalone frames: {resourcePath}");

            Type controllerType = typeof(PlayerSkillController);
            Type anchorType = controllerType.GetNestedType("FrameAnchorMode", BindingFlags.NonPublic);
            MethodInfo loader = controllerType.GetMethod("LoadPixelFrames",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (anchorType == null || loader == null)
                throw new InvalidOperationException("Could not locate the player skill frame loader.");

            object cellCenter = Enum.Parse(anchorType, "CellCenter");
            Sprite[] sprites = loader.Invoke(null, new[] { resourcePath, (object)6, cellCenter, 64f }) as Sprite[];
            if (sprites == null || sprites.Length != 6)
                throw new InvalidOperationException($"Could not build player skill sprites: {resourcePath}");

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null || sprite.name.Contains("Fallback"))
                    throw new InvalidOperationException($"Fallback used for player skill sprites: {resourcePath}");

                Texture2D generatedTexture = sprite.texture;
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(generatedTexture);
            }
        }

        Debug.Log($"PLAYER_SKILL_VFX_RUNTIME_PASS frames={validated} sequences={Folders.Length} " +
                  "fallback=false azureStandaloneFrames=36 chronoStandaloneFrames=36 unique=true individualPng=true");
    }
}
