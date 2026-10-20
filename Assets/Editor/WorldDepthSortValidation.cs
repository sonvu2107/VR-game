using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Kiểm tra không lưu scene, không vào Play và không đụng dữ liệu người dùng.</summary>
[InitializeOnLoad]
public static class WorldDepthSortValidation
{
    private const string Request = "Temp/WorldDepthSort.validate";
    static WorldDepthSortValidation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Move(Request, Request + ".started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try { Run(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/WorldDepthSort.result.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/The Labyrinth/Validate World Depth Sorting")]
    public static void Run()
    {
        if (Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Dừng Play và chờ Unity thoát Play hoàn toàn trước khi kiểm tra.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var checks = new WorldDepthSortTests();
            foreach (float y in new[] { -120f, 0f, 136f }) checks.BehindAndInFront_SortOnOppositeSidesOfProp(y);
            checks.MovingActor_ChangesOrderAndKeepsSlashAboveBody();
            checks.TwoActors_CanStandOnOppositeSidesOfSameProp();
            int maps = 0, maxMeshes = 0;
            foreach (string id in CampaignMapsBuilder.Ids)
            {
                var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>(CampaignMapsBuilder.ArtRoot + id + "/Theme.asset");
                var sequence = new List<RoomType> { RoomType.Start, RoomType.Combat, RoomType.Combat, RoomType.Combat, RoomType.Treasure, RoomType.Boss, RoomType.Exit };
                var layout = CampaignDungeonLayout.Generate(sequence, new SeededRandom(12345), theme.layoutStyle);
                var root = ArcaneCitadelGeometry.Build(layout, theme, new HashSet<Vector2Int>());
                var filters = root.GetComponentsInChildren<MeshFilter>();
                maxMeshes = Mathf.Max(maxMeshes, filters.Length);
                if (filters.Length + 64 > 256) throw new InvalidOperationException("Không đủ ngân sách mesh LAN dự phòng: " + id);
                var upright = filters.Where(f => f.name.Contains(" Depth ")).ToArray();
                if (upright.Length == 0 || upright.Select(f => f.name).Distinct().Count() != upright.Length ||
                    upright.Any(f => f.sharedMesh.vertexCount != 4 || f.GetComponent<MeshRenderer>().sortingOrder < 100))
                    throw new InvalidOperationException("Mesh vật trang trí chưa tách riêng đúng: " + id);
                foreach (var filter in filters) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(root);
                maps++;
            }
            string result = $"PASS: 5 depth assertions; {maps} campaign layouts have individually sorted upright props; maxMeshes={maxMeshes}, LAN budget includes 64 reserved meshes. No scene saved. Not a visual gameplay or two-machine LAN test.";
            File.WriteAllText("Temp/WorldDepthSort.result.txt", result);
            Debug.Log("WORLD_DEPTH_SORT_PASS " + result);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
