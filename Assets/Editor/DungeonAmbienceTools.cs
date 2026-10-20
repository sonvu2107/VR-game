using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in: chỉ xử lý yêu cầu Temp trên scene sạch, không tự chạy Play hoặc sửa save.</summary>
[InitializeOnLoad]
public static class DungeonAmbienceTools
{
    private const string Root = "Assets/Art/MapThemes/Ambience/";
    private const string Request = "Temp/DungeonAmbience.install";
    private const string SetPath = "Assets/Resources/DungeonAmbienceSet.asset";
    private static double nextCheck;
    static DungeonAmbienceTools() { EditorApplication.update += Tick; }

    private static bool IsReady() => !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
        !EditorApplication.isPlayingOrWillChangePlaymode && Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
        .All(i => !UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty);

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request) || !IsReady()) return;
        File.Move(Request, Request + ".started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try { InstallAndRefresh(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/DungeonAmbience.result.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Install Ambience and Refresh Chest-Free Previews")]
    public static void InstallAndRefresh()
    {
        if (!IsReady()) throw new InvalidOperationException("Dừng Play và lưu tất cả scene trước khi cập nhật preview.");
        string previous = EditorSceneManager.GetActiveScene().path;
        string backup = "Temp/DungeonAmbienceBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        Directory.CreateDirectory(backup);
        // Lưu cả atlas preview mesh cũ trước khi CopySerialized giữ GUID.
        foreach (string path in Directory.GetFiles("Assets/Art/MapThemes/ArcaneCitadel", "*Preview.asset"))
            File.Copy(path, backup + "/" + Path.GetFileName(path));
        File.Copy(ArcaneCitadelMapTools.ScenePath, backup + "/DungeonArcaneCitadel.unity");
        try
        {
            InstallArt();
            EditorSceneManager.OpenScene(ArcaneCitadelMapTools.ScenePath);
            ArcaneCitadelMapTools.RefreshPreview();
            ValidatePreview(ArcaneCitadelMapTools.ScenePath);
            CampaignMapsBuilder.RebuildGeneratedPreviews();
            for (int i = 0; i < CampaignMapsBuilder.Ids.Length; i++)
            {
                string path = $"Assets/Scenes/Level{i + 3:00}_{CampaignMapsBuilder.Ids[i]}.unity";
                EditorSceneManager.OpenScene(path);
                ValidatePreview(path);
                CaptureRoom(i);
            }
            CampaignMapsBuilder.ValidateLayouts();
            CampaignMapsBuilder.ValidateGeneratedCampaign();
            ValidateGpuLoop();
            LanBuildTools.RefreshCatalog();
            File.WriteAllText("Temp/DungeonAmbience.result.txt",
                "PASS: 9 chest-free previews; 10 themed palettes; 384 layouts; generated levels 1-10; GPU fire loop 0-4-0 identical, four frames visible; soul movement; no ambient colliders; LAN materials registered. No Play Mode/two-machine LAN test.");
            Debug.Log("DUNGEON_AMBIENCE_PASS previews=9 palettes=10 chestFootprints=0 GPUloop=true colliders=0");
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        }
    }

    private static void InstallArt()
    {
        // Chỉ import texture, không sửa sprite metadata hoặc slicing của asset có sẵn.
        string path = Root + "DungeonAmbience_Atlas_v1.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048; importer.SaveAndReimport();
        var shader = Shader.Find("TheLabyrinth/Pixel Dungeon Ambience");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ambience shader chưa biên dịch thành công.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var set = AssetDatabase.LoadAssetAtPath<DungeonAmbienceSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<DungeonAmbienceSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        var colors = new[] {
            new Color(1,.57f,.2f), new Color(.15f,.9f,1), new Color(.6f,1,.3f), new Color(.2f,.88f,.9f),
            new Color(1,.38f,.1f), new Color(.5f,.83f,1), new Color(.72f,.5f,1), new Color(1,.78f,.38f),
            new Color(.68f,.28f,1), new Color(1,.24f,.34f) };
        int[] accents = { 15,13,12,15,14,13,15,14,13,14 };
        int[] souls = { 1,1,2,2,0,2,3,1,3,2 };
        set.palettes = new DungeonAmbienceSet.Palette[10];
        for (int i = 0; i < 10; i++)
        {
            set.palettes[i] = new DungeonAmbienceSet.Palette {
                fire = Material($"Level{i + 1:00}_Fire", shader, texture, colors[i], 4, 5, 0, 1),
                souls = Material($"Level{i + 1:00}_Souls", shader, texture, Color.Lerp(colors[i], Color.white, .25f), 4, 4, .12f, 1),
                details = Material($"Level{i + 1:00}_Details", shader, texture,
                    i == 2 ? new Color(.68f,1,.65f) : Color.white, 1, 0, 0, i == 2 ? .6f : 0),
                accentCell = accents[i], soulsPerRoom = souls[i], websPerRoom = i == 6 || i == 7 ? 3 : i == 4 || i == 5 ? 1 : 2 };
        }
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }

    private static Material Material(string name, Shader shader, Texture2D texture, Color tint, int frames, float fps, float bob, float strength)
    {
        string path = Root + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.mainTexture = texture;
        material.SetColor("_Color", tint); material.SetFloat("_Frames", frames); material.SetFloat("_FPS", fps);
        material.SetFloat("_Bob", bob); material.SetFloat("_Tint", strength); material.SetFloat("_PreviewTime", -1);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static void ValidatePreview(string path)
    {
        var root = UnityEngine.Object.FindObjectOfType<CourtyardPreviewOnly>();
        if (root == null) throw new InvalidOperationException("Missing owned preview: " + path);
        if (root.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Treasure Chest")))
            throw new InvalidOperationException("Chest footprint still present: " + path);
        var filters = root.GetComponentsInChildren<MeshFilter>();
        if (!filters.Any(f => f.name.StartsWith("Ambient Fire", StringComparison.Ordinal)) || !filters.Any(f => f.name == "Ambient Details"))
            throw new InvalidOperationException("Missing ambient meshes: " + path);
        foreach (var filter in filters.Where(f => f.name.StartsWith("Ambient ", StringComparison.Ordinal)))
        {
            if (filter.sharedMesh == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(filter.sharedMesh)))
                throw new InvalidOperationException("Preview mesh not persistent: " + path);
            if (filter.GetComponent<Collider2D>() != null) throw new InvalidOperationException("Ambient collider added.");
        }
        var staticBounds = filters.Where(f => f.name == "Arcane Props" ||
            (!f.name.StartsWith("Ambient ", StringComparison.Ordinal) && f.name.Contains(" Depth "))).SelectMany(QuadBounds).ToArray();
        var fireBounds = filters.Where(f => f.name.StartsWith("Ambient Fire", StringComparison.Ordinal)).SelectMany(QuadBounds).ToArray();
        foreach (var fire in fireBounds)
        {
            var clearance = new Rect(fire.xMin - 1.25f, fire.yMin - 1.25f, fire.width + 2.5f, fire.height + 2.5f);
            if (staticBounds.Any(other => clearance.Overlaps(other)))
                throw new InvalidOperationException("Brazier is too close to a solid prop: " + path);
        }
        var occupied = new List<Rect>(staticBounds);
        foreach (var filter in filters.Where(f => f.name.StartsWith("Ambient ", StringComparison.Ordinal)))
        foreach (var quad in QuadBounds(filter))
        {
            Rect bounds = filter.name.StartsWith("Ambient Soul", StringComparison.Ordinal) ? new Rect(quad.x, quad.y - .12f, quad.width, quad.height + .24f) : quad;
            if (occupied.Any(other => bounds.Overlaps(other)))
                throw new InvalidOperationException("Ambient visual overlap: " + path + "/" + filter.name);
            if (!filter.name.StartsWith("Ambient Fire", StringComparison.Ordinal))
            {
                var clearance = new Rect(bounds.xMin - 1.25f, bounds.yMin - 1.25f, bounds.width + 2.5f, bounds.height + 2.5f);
                if (fireBounds.Any(other => clearance.Overlaps(other)))
                    throw new InvalidOperationException("Decoration crowds a brazier: " + path + "/" + filter.name);
            }
            occupied.Add(bounds);
        }
    }

    private static IEnumerable<Rect> QuadBounds(MeshFilter filter)
    {
        var vertices = filter.sharedMesh.vertices;
        for (int i = 0; i + 3 < vertices.Length; i += 4)
            yield return Rect.MinMaxRect(vertices[i].x, vertices[i].y, vertices[i + 2].x, vertices[i + 2].y);
    }

    private static void CaptureRoom(int theme)
    {
        var root = UnityEngine.Object.FindObjectOfType<CourtyardPreviewOnly>();
        var filter = root.GetComponentsInChildren<MeshFilter>().First(f => f.name.StartsWith("Ambient Fire", StringComparison.Ordinal));
        // Phòng hẹp có thể chủ động bỏ chậu nếu không đủ khoảng thoáng.
        Vector3 anchor = filter.sharedMesh.vertexCount > 0 ? filter.sharedMesh.vertices[0]
            : UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>().playerTransform.position;
        if (theme == 7)
        {
            var throne = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Royal Throne Footprint");
            if (throne != null) anchor = throne.position + new Vector3(-2, -2, 0);
        }
        var go = new GameObject("Ambience Preview Capture");
        try
        {
            var camera = go.AddComponent<Camera>(); camera.orthographic = true;
            camera.orthographicSize = 4.5f; camera.aspect = 4f / 3f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.025f,.045f);
            camera.cullingMask = ~(1 << 5); camera.allowMSAA = false; camera.allowHDR = false;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.transform.position = new Vector3(anchor.x + 2, anchor.y - 1, -10);
            var pixels = Render(camera, 640, 480);
            Directory.CreateDirectory("Captures/Ambience");
            File.WriteAllBytes($"Captures/Ambience/Level{theme + 3:00}.png", pixels.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(pixels);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Texture2D Render(Camera camera, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width,height,24);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(width,height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); return image;
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    private static void ValidateGpuLoop()
    {
        var source = Resources.Load<DungeonAmbienceSet>("DungeonAmbienceSet").palettes[1];
        var material = new Material(source.fire);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var cameraGo = new GameObject("Ambience GPU Validation");
        var captures = new List<Texture2D>();
        try
        {
            go.transform.position = new Vector3(10000,10000,0); go.transform.localScale = new Vector3(2,2,1);
            var mesh = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            // Primitive quad UV order is version-dependent; map from vertex coordinates instead.
            mesh.uv = mesh.vertices.Select(v => new Vector2((v.x + .5f) * .248f + .001f, (v.y + .5f) * .248f + .751f)).ToArray();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowMSAA = false; camera.allowHDR = false;
            cameraGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.transform.position = new Vector3(10000,10000,-10);
            for (int frame = 0; frame <= 4; frame++)
            {
                material.SetFloat("_PreviewTime", frame / 5f + .01f);
                captures.Add(Render(camera,128,128));
            }
            foreach (var capture in captures)
                if (capture.GetPixels32().Count(p => p.r + p.g + p.b > 60) < 80) throw new InvalidOperationException("Blank animation frame.");
            if (!captures[0].GetRawTextureData().SequenceEqual(captures[4].GetRawTextureData()))
                throw new InvalidOperationException("Fire loop does not return to first frame.");
            if (captures[0].GetRawTextureData().SequenceEqual(captures[1].GetRawTextureData()))
                throw new InvalidOperationException("Fire frames do not animate.");
            var first = captures[0].GetPixels32();
            var framePixels = captures.Take(5).Select(capture => capture.GetPixels32()).ToArray();
            int bodyPixels = 0;
            for (int y = 18; y < 48; y++) for (int x = 16; x < 112; x++)
            {
                int pixel = y * 128 + x;
                if (first[pixel].r + first[pixel].g + first[pixel].b > 30) bodyPixels++;
                for (int frame = 1; frame < 5; frame++)
                    if (!framePixels[frame][pixel].Equals(first[pixel]))
                        throw new InvalidOperationException("Brazier body shifts between frames.");
            }
            if (bodyPixels < 40) throw new InvalidOperationException("No visible brazier body in stability probe.");
            Debug.Log("AMBIENCE_BRAZIER_STABILITY_PASS bodyPixels=" + bodyPixels);
            Debug.Log("AMBIENCE_GPU_LOOP_PASS visibleFrames=4 wrapToFirst=true");
            material.CopyPropertiesFromMaterial(source.souls);
            mesh.uv = mesh.vertices.Select(v => new Vector2((v.x + .5f) * .248f + .001f, (v.y + .5f) * .248f + .501f)).ToArray();
            material.SetFloat("_PreviewTime", .01f); captures.Add(Render(camera,128,128));
            material.SetFloat("_PreviewTime", 1.01f); captures.Add(Render(camera,128,128));
            if (captures[5].GetRawTextureData().SequenceEqual(captures[6].GetRawTextureData()))
                throw new InvalidOperationException("Soul shader does not float.");
            Debug.Log("AMBIENCE_GPU_SOUL_PASS sameFrame=true floating=true");
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        finally
        {
            foreach (var capture in captures) UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cameraGo); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
