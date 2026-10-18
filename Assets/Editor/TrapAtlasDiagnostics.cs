using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Kiểm tra một lần theo yêu cầu; chỉ đọc renderer, không sửa scene/asset/runtime.</summary>
[InitializeOnLoad]
public static class TrapAtlasDiagnostics
{
    private const string Request = "Temp/TrapAtlas.inspect";
    private static double nextCheck;
    static TrapAtlasDiagnostics() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (File.Exists("Temp/TrapAtlas.validate") && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Move("Temp/TrapAtlas.validate", "Temp/TrapAtlas.validate.started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            try { ValidateReload(); }
            catch (Exception error) { File.WriteAllText("Temp/TrapAtlas.validation.txt", "FAIL: " + error); Debug.LogException(error); }
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying || UnityEngine.Object.FindObjectsOfType<SpikeTrap>().Length == 0) return;
        File.Move(Request, Request + ".started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        Inspect();
    }

    [MenuItem("Tools/The Labyrinth/Diagnostics/Inspect Trap Atlas Renderers")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine($"play={EditorApplication.isPlaying} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");
        foreach (var generator in UnityEngine.Object.FindObjectsOfType<InfiniteWorldGenerator>())
        {
            var theme = generator.ActiveTheme;
            report.AppendLine($"generator={generator.name} theme={(theme == null ? "original" : AssetDatabase.GetAssetPath(theme))}");
            if (theme != null)
                for (int i = 12; i < 16; i++) report.AppendLine($"frameRect[{i}]={theme.AtlasUv(i).ToString("F5")}");
        }
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            var material = renderer.sharedMaterial;
            var texture = material == null ? null : material.mainTexture;
            var sprite = renderer as SpriteRenderer;
            var trap = renderer.GetComponentInParent<SpikeTrap>();
            bool atlas = texture != null && (AssetDatabase.GetAssetPath(texture).Contains("MapThemes") || texture.name.Contains("Atlas"));
            if (!atlas && trap == null) continue;
            report.AppendLine($"renderer={PathOf(renderer.transform)} enabled={renderer.enabled} position={renderer.transform.position:F3} material={AssetDatabase.GetAssetPath(material)} shader={material?.shader?.name} texture={AssetDatabase.GetAssetPath(texture)}");
            if (sprite != null) report.AppendLine($"sprite={sprite.sprite?.name} rect={sprite.sprite?.rect} spriteTexture={AssetDatabase.GetAssetPath(sprite.sprite == null ? null : sprite.sprite.texture)}");
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null) continue;
            var uv = mesh.uv;
            report.AppendLine($"mesh={mesh.name} vertices={mesh.vertexCount} uvCount={uv.Length}");
            if (trap != null || mesh.vertexCount == 4)
                report.AppendLine("uv=" + string.Join(";", uv.Select(p => p.ToString("F5"))));
            else
            {
                // Chỉ xuất quad bất thường trùm >1 cell trong atlas 4x4; không dump toàn bộ map.
                for (int i = 0; i + 3 < uv.Length; i += 4)
                {
                    float width = uv.Skip(i).Take(4).Max(p => p.x) - uv.Skip(i).Take(4).Min(p => p.x);
                    float height = uv.Skip(i).Take(4).Max(p => p.y) - uv.Skip(i).Take(4).Min(p => p.y);
                    if (width > .26f || height > .26f)
                        report.AppendLine($"wideQuad={i / 4} uv=" + string.Join(";", uv.Skip(i).Take(4).Select(p => p.ToString("F5"))));
                }
            }
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/TrapAtlas.report.txt", report.ToString());
        Debug.Log("TRAP_ATLAS_INSPECTION_READY Temp/TrapAtlas.report.txt (read-only)");
    }

    private static string PathOf(Transform target) => target.parent == null ? target.name : PathOf(target.parent) + "/" + target.name;

    [MenuItem("Tools/The Labyrinth/Diagnostics/Validate Trap Atlas Reload")]
    public static void ValidateReload()
    {
        int cases = 0;
        var cache = typeof(ArcaneSpikeTrapVisual).GetField("frameUvs", BindingFlags.Instance | BindingFlags.NonPublic);
        var legacy = (Vector2[][])typeof(ArcaneSpikeTrapVisual).GetField("Frames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var themes = AssetDatabase.FindAssets("t:CourtyardTheme").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CourtyardTheme>).Where(t => t.UsesArcaneArt && t.arcaneSpike != null).ToArray();
        foreach (var theme in themes)
        {
            var root = new GameObject("Trap atlas regression fixture"); root.SetActive(false);
            var copyRoot = new GameObject("Trap atlas reload fixture"); copyRoot.SetActive(false);
            try
            {
                var visual = root.AddComponent<ArcaneSpikeTrapVisual>();
                var copy = copyRoot.AddComponent<ArcaneSpikeTrapVisual>();
                Rect[] rects = theme.usesCampaignAtlas ? Enumerable.Range(12, 4).Select(theme.AtlasUv).ToArray() : null;
                visual.Initialize(theme.arcaneSpike, rects);
                copy.Initialize(theme.arcaneSpike); // Mô phỏng instance/cache mặc định sau reload.
                Vector3[] vertices = root.GetComponent<MeshFilter>().sharedMesh.vertices;
                string snapshot = JsonUtility.ToJson(visual);
                JsonUtility.FromJsonOverwrite(snapshot, copy);
                for (int frame = 0; frame < 4; frame++)
                {
                    visual.SetFrame(frame);
                    Vector2[] expected = rects == null ? legacy[frame] : new[] {
                        new Vector2(rects[frame].xMin, rects[frame].yMin), new Vector2(rects[frame].xMin, rects[frame].yMax),
                        new Vector2(rects[frame].xMax, rects[frame].yMax), new Vector2(rects[frame].xMax, rects[frame].yMin) };
                    cache.SetValue(copy, null);
                    copy.SetFrame(frame);
                    if (!root.GetComponent<MeshFilter>().sharedMesh.uv.SequenceEqual(expected) ||
                        !copyRoot.GetComponent<MeshFilter>().sharedMesh.uv.SequenceEqual(expected))
                        throw new InvalidOperationException($"UV lệch sau reload: {theme.name}, frame {frame}");
                    // Đổi enable/disable cũng không làm quay về UV Arcane cũ.
                    typeof(ArcaneSpikeTrapVisual).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(copy, null);
                    if (!copyRoot.GetComponent<MeshFilter>().sharedMesh.uv.SequenceEqual(expected) ||
                        !root.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(vertices))
                        throw new InvalidOperationException("Kích thước hoặc UV bị đổi sau enable.");
                    cases++;
                }
            }
            finally
            {
                // OnDestroy của runtime dùng Destroy; trong Edit Mode giải phóng mesh trước.
                foreach (var go in new[] { root, copyRoot })
                {
                    var filter = go.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
        if (cases < 36) throw new InvalidOperationException("Thiếu theme để kiểm thử đủ Arcane và tám map mới.");
        File.WriteAllText("Temp/TrapAtlas.validation.txt", $"PASS: {cases} theme/frame cases; correct UV before/after serialized Rect[] roundtrip, cache reset and OnEnable; fixed geometry. No gameplay/save changes.");
        Debug.Log($"TRAP_ATLAS_RELOAD_PASS cases={cases} fixedGeometry=true");
    }
}
