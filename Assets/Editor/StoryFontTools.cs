using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Builds a persistent atlas for every campaign line and repairs TMP's dynamic fallback.</summary>
public static class StoryFontTools
{
    private const string FontPath = "Assets/Resources/Story/StoryFont.asset";
    private const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [MenuItem("Tools/The Labyrinth/Refresh Story Font")]
    public static void Refresh()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (source == null) throw new InvalidOperationException("LiberationSans.ttf is missing.");

        // The Editor can serialize a newly allocated second atlas as null while
        // keeping its index at 1. Clearing it removes the invalid reference.
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback != null)
        {
            fallback.ClearFontAssetData();
            fallback.isMultiAtlasTexturesEnabled = false;
            EditorUtility.SetDirty(fallback);
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null)
            AssetDatabase.DeleteAsset(FontPath);
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 72, 5,
            GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        if (font == null) throw new InvalidOperationException("Could not create campaign font.");
        font.name = "Story Vietnamese SDF";
        AssetDatabase.CreateAsset(font, FontPath);

        Texture2D atlas = font.atlasTextures[0];
        atlas.name = "Story Vietnamese SDF Atlas";
        Material material = font.material;
        material.name = "Story Vietnamese SDF Material";
        AssetDatabase.AddObjectToAsset(atlas, font);
        AssetDatabase.AddObjectToAsset(material, font);

        string characters = GatherCharacters();
        if (!font.TryAddCharacters(characters, out string missing))
            Debug.LogWarning("Story font could not include: " + missing);
        SerializedObject serialized = new(font);
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(font);
        EditorUtility.SetDirty(atlas);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"STORY_FONT_READY characters={characters.Length} missing={missing?.Length ?? 0} atlas={font.atlasTextures.Length}");
    }

    private static string GatherCharacters()
    {
        StringBuilder result = new("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 · / : - ! ? , . % ( )");
        result.Append("LỜI DẪN KỴ SĨ TRO TÀN ĐỒNG HÀNH TIẾNG VUA XƯƠNG TIẾP TỤC CHỌN MÀN Dừng chơi Tạo phòng LAN Tham gia Chơi một mình Quay lại Cổng IP máy Host");
        for (int level = 1; level <= LanLaunch.CampaignLevelCount; level++)
        {
            result.Append(CampaignStoryText.Title(level));
            Append(CampaignStoryText.Intro(level, false));
            Append(CampaignStoryText.Intro(level, true));
            foreach (StoryCue cue in Enum.GetValues(typeof(StoryCue)))
                foreach (bool coop in new[] { false, true })
                    for (int collector = 1; collector <= 2; collector++)
                        Append(CampaignStoryText.Cue(level, cue, coop, collector));
        }
        Append(CampaignStoryText.Ending(false));
        Append(CampaignStoryText.Ending(true));
        return result.ToString();

        void Append(StoryLine[] lines)
        {
            foreach (StoryLine line in lines) result.Append(line.Text);
        }
    }
}
