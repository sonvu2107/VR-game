using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Unity generates the catalog/meta files, including unsliced VFX textures.</summary>
[InitializeOnLoad]
public sealed class LanBuildTools : IPreprocessBuildWithReport
{
    private const string CatalogPath = "Assets/Resources/LanAssetCatalog.asset";
    public int callbackOrder => 0;
    static LanBuildTools()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            LanAssetCatalog existing = AssetDatabase.LoadAssetAtPath<LanAssetCatalog>(CatalogPath);
            // Combat audio is loaded dynamically from Resources, so it is not a
            // scene dependency. Keep the shared LAN catalog in sync whenever a
            // newly imported combat clip is missing from the existing catalog.
            AudioClip[] combatAudio = Resources.LoadAll<AudioClip>("Audio/Combat");
            bool missingCombatAudio = existing != null && existing.audioClips != null &&
                combatAudio.Any(clip => clip != null && !existing.audioClips.Contains(clip));
            if (existing == null || existing.audioClips == null || missingCombatAudio) RefreshCatalog();
        };
    }
    public void OnPreprocessBuild(BuildReport report) => RefreshCatalog();

    [MenuItem("Tools/The Labyrinth/Refresh LAN Catalog")]
    public static void RefreshCatalog()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        var paths = new SortedSet<string>(AssetDatabase.GetDependencies(scenes, true), StringComparer.Ordinal);
        string[] assetPaths = AssetDatabase.GetAllAssetPaths();
        string[] resources = assetPaths.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
            path.Contains("/Resources/") && path != CatalogPath).ToArray();
        foreach (string path in AssetDatabase.GetDependencies(resources, true)) paths.Add(path);
        foreach (string path in resources) paths.Add(path);
        foreach (string path in assetPaths)
            if (path.StartsWith("Assets/Scripts/", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Input/", StringComparison.Ordinal))
                paths.Add(path);
        var sprites = new List<Sprite>();
        var textures = new List<Texture2D>();
        var tiles = new List<TileBase>();
        var materials = new List<Material>();
        var audioClips = new List<AudioClip>();
        var identity = new StringBuilder("lan-v1\n");
        identity.Append(File.ReadAllText("Packages/manifest.json"));
        identity.Append(File.ReadAllText("Packages/packages-lock.json"));
        foreach (string path in paths)
        {
            if (path == CatalogPath || AssetDatabase.IsValidFolder(path)) continue;
            // Hai bản thử cũ chỉ có ở local và được .gitignore loại ra.
            // Không để catalog LAN tham chiếu asset mà máy clone mới không có.
            if (path == "Assets/Resources/VFX/Pipoya/TimeMagic/pipo-btleffect210_192.png" ||
                path == "Assets/Resources/VFX/Pipoya/TimeMagic/pipo-btleffect212_192.png") continue;
            identity.Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".asset" && extension != ".mat" &&
                extension != ".wav" && extension != ".ogg" && extension != ".mp3") continue;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path).OrderBy(AssetKey, StringComparer.Ordinal))
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
                else if (asset is Texture2D texture) textures.Add(texture);
                else if (asset is TileBase tile) tiles.Add(tile);
                else if (asset is Material material) materials.Add(material);
                else if (asset is AudioClip audioClip) audioClips.Add(audioClip);
            }
        }
        LanAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<LanAssetCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            catalog = ScriptableObject.CreateInstance<LanAssetCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        using (SHA256 hash = SHA256.Create()) catalog.fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-", "");
        catalog.sprites = sprites.Distinct().ToArray(); catalog.textures = textures.Distinct().ToArray();
        catalog.tiles = tiles.Distinct().ToArray(); catalog.materials = materials.Distinct().ToArray();
        catalog.audioClips = audioClips.Distinct().ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        Debug.Log($"LAN_CATALOG_READY sprites={catalog.sprites.Length} textures={catalog.textures.Length} tiles={catalog.tiles.Length} fingerprint={catalog.fingerprint}");
    }
    private static string AssetKey(UnityEngine.Object asset)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id);
        return guid + ":" + id.ToString("D20");
    }
    public static void BuildWindows()
    {
        string output = Environment.GetEnvironmentVariable("LABYRINTH_LAN_BUILD");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("LABYRINTH_LAN_BUILD is required.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("LAN build failed: " + report.summary.result);
        Debug.Log("LAN_BUILD_SUCCESS " + output);
    }

    public static void ValidateAndBuildWindows()
    {
        LanTransportProbe.Run();
        HealthAndLevelValidation.Validate();
        EnemyDistributionValidation.Validate();
        BuildWindows();
    }
}
