using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only verification of the gameplay pixel-art allowlist after import.</summary>
[InitializeOnLoad]
public static class PixelArtTextureValidation
{
    private const string ListPath = "Assets/Editor/PixelArtTexturePaths.txt";
    static PixelArtTextureValidation() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        const string request = "Temp/PixelArtTextures.validate";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request);
        Validate();
    }

    [MenuItem("Tools/The Labyrinth/Validate Gameplay Pixel Textures")]
    public static void Validate()
    {
        var report = new System.Collections.Generic.List<string>();
        int count = 0;
        foreach (string path in File.ReadAllLines(ListPath).Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || texture == null) { report.Add("MISSING " + path); continue; }
            count++;
            if (path.StartsWith("Assets/Art/MapThemes/") &&
                (texture.width != 1254 || texture.height != 1254))
                report.Add("FAIL source resolution " + path + ": " + texture.width + "x" + texture.height);
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                texture.filterMode != FilterMode.Point || texture.mipmapCount != 1)
                report.Add("FAIL " + path + " filter=" + texture.filterMode + " mips=" + texture.mipmapCount);
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings.overridden && (settings.textureCompression != TextureImporterCompression.Uncompressed ||
                    settings.crunchedCompression))
                    report.Add("FAIL override " + platform + ": " + path);
            }
        }
        Directory.CreateDirectory("Captures/PixelTextures");
        CaptureEmblem();
        File.WriteAllLines("Captures/PixelTextures/live-map-textures.txt",
            UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Where(r => r.name.Contains("Arcane"))
                .Select(r => {
                    var m = r.sharedMaterial;
                    var t = m != null ? m.mainTexture : null;
                    return r.name + " / " + (m != null ? m.shader.name : "no material") + " / " +
                        (t != null ? AssetDatabase.GetAssetPath(t) + " " + t.width + "x" + t.height + " " + t.filterMode : "no texture");
                }));
        if (Application.isPlaying)
            ScreenCapture.CaptureScreenshot("Captures/PixelTextures/game-after.png");
        report.Insert(0, (report.Count == 0 ? "PASS" : "FAIL") + ": checked " + count + " gameplay pixel textures.");
        File.WriteAllLines("Captures/PixelTextures/validation.txt", report);
        if (report.Count == 1) Debug.Log(report[0]);
        else Debug.LogError(string.Join("\n", report));
    }

    private static void CaptureEmblem()
    {
        var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(
            "Assets/Art/MapThemes/Campaign/AbyssSanctum/Theme.asset");
        var uv = theme.AtlasUv(3);
        var target = RenderTexture.GetTemporary(320, 320, 0, RenderTextureFormat.ARGB32);
        var shaded = RenderTexture.GetTemporary(1254, 1254, 0, RenderTextureFormat.ARGB32);
        shaded.filterMode = FilterMode.Point;
        var previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            Graphics.Blit(theme.arcaneFloor.mainTexture, shaded, theme.arcaneFloor);
            Graphics.Blit(shaded, target,
                new Vector2(uv.width, uv.height), new Vector2(uv.x, uv.y));
            RenderTexture.active = target;
            pixels = new Texture2D(320, 320, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 320, 320), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Captures/PixelTextures/level9-emblem-imported.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(shaded);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
