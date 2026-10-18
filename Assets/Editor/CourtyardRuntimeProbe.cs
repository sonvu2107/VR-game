using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class CourtyardRuntimeProbe
{
    private const string Key = "Labyrinth.CourtyardRuntimeProbe";
    private static double deadline;
    private static double stableAt;

    static CourtyardRuntimeProbe()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        deadline = stableAt = 0;
        EditorSceneManager.OpenScene(CourtyardPrototypeTools.ScenePath);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 45;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "timeout");
            var manager = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (manager == null || manager.CurrentState != GameState.Playing) return;
            if (stableAt == 0) stableAt = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.timeSinceStartup < stableAt) return;
            var generator = manager.dungeonGenerator;
            Require(generator.courtyardTheme != null && generator.GeneratedRooms.Count == 7, "prototype layout not active");
            Require(UnityEngine.Object.FindObjectsByType<CourtyardPreviewOnly>(FindObjectsSortMode.None).Length == 0, "editor preview leaked into runtime");
            Require(GameObject.Find("Courtyard Atlas Details") != null, "atlas decor missing");
            var playerHealth = manager.PlayerMovement.GetComponent<HealthController>();
            Require(playerHealth.startHeart == 5 && playerHealth.MaxHealth == 10, "five hearts missing");
            Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None)
                .Any(image => image.isActiveAndEnabled && image.texture is RenderTexture), "minimap missing");
            Require(UnityEngine.Object.FindObjectOfType<FloorExit>() != null, "portal missing");
            Require(NavMesh.SamplePosition(manager.PlayerMovement.transform.position, out NavMeshHit start, 3, NavMesh.AllAreas), "player not on NavMesh");
            foreach (DungeonRoom room in generator.GeneratedRooms)
            {
                Require(NavMesh.SamplePosition(PrefabPlacer.GetWorldPosition(room.SpawnPoint), out NavMeshHit destination, 3, NavMesh.AllAreas), "room not on NavMesh");
                var path = new NavMeshPath();
                Require(NavMesh.CalculatePath(start.position, destination.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete, "room unreachable by NavMesh");
            }
            var layout = new DungeonLayout();
            foreach (DungeonRoom room in generator.GeneratedRooms) layout.AddRoom(room);
            CourtyardPrototypeTools.CapturePreview(layout);
            Debug.Log("COURTYARD_RUNTIME_PASS rooms=7 navmesh=allRooms minimap=ok hearts=5 portal=ok previewCleanup=ok atlasDecor=ok");
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("COURTYARD_RUNTIME_FAIL " + exception);
            Finish(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
