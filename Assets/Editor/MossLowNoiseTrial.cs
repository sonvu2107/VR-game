using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Lọc nền/tường campaign; giữ PNG gốc và material props/trap nguyên trạng.</summary>
[InitializeOnLoad]
public static class MossLowNoiseTrial
{
    private static double nextCheck;
    static MossLowNoiseTrial() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        bool all = File.Exists("Temp/CampaignLowNoise.install");
        if ((!all && !File.Exists("Temp/MossLowNoise.install")) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) return;
        string request = all ? "Temp/CampaignLowNoise.install" : "Temp/MossLowNoise.install";
        File.Move(request, request + ".started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try { if (all) ApplyRemainingMaps(); else Apply(); }
        catch (Exception error) { File.WriteAllText(all ? "Temp/CampaignLowNoise.result.txt" : "Temp/MossLowNoise.result.txt", "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Try Low Noise Moss Floor and Walls")]
    public static void Apply()
    {
        ApplyMap(0, true);
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Apply Low Noise to Levels 4-10")]
    public static void ApplyRemainingMaps()
    {
        for (int index = 1; index < CampaignMapsBuilder.Ids.Length; index++) ApplyMap(index, false);
        LanBuildTools.RefreshCatalog();
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        catalog.Initialize();
        for (int index = 0; index < CampaignMapsBuilder.Ids.Length; index++)
        {
            string folder = CampaignMapsBuilder.ArtRoot + CampaignMapsBuilder.Ids[index] + "/";
            var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(folder + "Theme.asset");
            var original = AssetDatabase.LoadAssetAtPath<Material>(folder + "MapAtlas.mat");
            var filtered = AssetDatabase.LoadAssetAtPath<Material>(folder + "LowNoiseFloorWalls.mat");
            if (filtered == null || theme.arcaneFloor != filtered || theme.arcaneWalls != filtered ||
                filtered.GetFloat("_PixelsPerCell") != 48 || filtered.mainTexture != original.mainTexture ||
                theme.arcaneProps != original || theme.arcaneSpike != original || catalog.Id(filtered) < 0)
                throw new InvalidOperationException("Low-noise configuration/catalog mismatch: " + folder);
        }
        File.WriteAllText("Temp/CampaignLowNoise.result.txt", "PASS: levels 4-10 applied, level 3 retained; 8 floor/wall materials at 48 pixels/cell; props/traps/source texture unchanged; previews saved; LAN catalog contains all 8 materials. No movement comparison for levels 4-10 or two-machine LAN test.");
        Debug.Log("CAMPAIGN_LOW_NOISE_PASS applied=7 validated=8 pixelsPerCell=48 propsPreserved=true");
    }

    private static void ApplyMap(int index, bool refreshCatalog)
    {
        string Folder = CampaignMapsBuilder.ArtRoot + CampaignMapsBuilder.Ids[index] + "/";
        string ScenePath = $"Assets/Scenes/Level{index + 3:00}_{CampaignMapsBuilder.Ids[index]}.unity";
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Dừng Play và lưu scene trước khi áp dụng.");
        string previous = EditorSceneManager.GetActiveScene().path;
        string backup = "Temp/MossLowNoiseBackup/" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        Directory.CreateDirectory(backup);
        File.Copy(Folder + "Theme.asset", backup + "/Theme.asset");
        File.Copy(ScenePath, backup + "/" + Path.GetFileName(ScenePath));
        foreach (string file in Directory.GetFiles(Folder + "PreviewMeshes", "*.asset")) File.Copy(file, backup + "/" + Path.GetFileName(file));
        try
        {
            var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(Folder + "Theme.asset");
            var original = AssetDatabase.LoadAssetAtPath<Material>(Folder + "MapAtlas.mat");
            var shader = Shader.Find("TheLabyrinth/Low Noise Pixel Map");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader chưa biên dịch.");
            Material props = theme.arcaneProps, traps = theme.arcaneSpike;
            string path = Folder + "LowNoiseFloorWalls.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.mainTexture = original.mainTexture; material.SetFloat("_PixelsPerCell", 48);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            theme.arcaneFloor = theme.arcaneWalls = material;
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
            EditorSceneManager.OpenScene(ScenePath);
            var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
            if (generator.courtyardTheme != theme) throw new InvalidOperationException("Preview theme khác, không ghi đè.");
            foreach (var old in UnityEngine.Object.FindObjectsOfType<CourtyardPreviewOnly>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var sequence = new List<RoomType> { RoomType.Start, RoomType.Combat, RoomType.Combat, RoomType.Combat, RoomType.Treasure, RoomType.Boss, RoomType.Exit };
            var layout = CampaignDungeonLayout.Generate(sequence, new SeededRandom(12345), theme.layoutStyle);
            var root = ArcaneCitadelGeometry.Build(layout, theme, new HashSet<Vector2Int>());
            root.tag = "EditorOnly"; root.AddComponent<CourtyardPreviewOnly>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                string meshPath = Folder + "PreviewMeshes/" + filter.name.Replace(" ", "") + ".asset";
                var persistent = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (persistent == null) AssetDatabase.CreateAsset(filter.sharedMesh, meshPath);
                else
                {
                    Mesh transient = filter.sharedMesh; EditorUtility.CopySerialized(transient, persistent);
                    filter.sharedMesh = persistent; EditorUtility.SetDirty(persistent); AssetDatabase.SaveAssetIfDirty(persistent);
                    UnityEngine.Object.DestroyImmediate(transient);
                }
            }
            if (theme.arcaneProps != props || theme.arcaneSpike != traps || material.mainTexture != original.mainTexture)
                throw new InvalidOperationException("Unexpected prop/trap/source change.");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            if (refreshCatalog) LanBuildTools.RefreshCatalog();
            if (index == 0)
                File.WriteAllText("Temp/MossLowNoise.result.txt", "PASS: Moss floor/walls use separate 48 logical pixel/cell material with 9-tap prefilter and cell clamps; PNG, props, traps and other themes unchanged. Backup: " + backup + ". Needs visual movement comparison; not proof of temporal improvement.");
            Debug.Log($"LOW_NOISE_MAP_READY level={index + 3} pixelsPerCell=48 sourcePreserved=true propsPreserved=true backup={backup}");
        }
        finally { if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous); }
    }
}
