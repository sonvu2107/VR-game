using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArrowCryptPreview
{
    private const string PreviewActive = "ArrowCryptPreview.Active";
    private static double started;

    [MenuItem("Tools/The Labyrinth/Preview Level 3 Arrow Crypt")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting the Level 3 preview.");

        SessionState.SetBool(PreviewActive, true);
        started = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload()
    {
        if (!SessionState.GetBool(PreviewActive, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;

        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (manager == null || manager.CurrentState != GameState.Playing)
        {
            if (EditorApplication.timeSinceStartup - started > 90)
            {
                Debug.LogError("ARROW_CRYPT_PREVIEW_FAIL timeout");
                StopWaiting();
            }
            return;
        }

        typeof(GameManager).GetProperty("CurrentLevel").GetSetMethod(true)
            .Invoke(manager, new object[] { 3 });
        typeof(GameManager).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, new object[] { GameState.Generating });
        typeof(GameManager).GetMethod("BeginLevelGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);

        Debug.Log("ARROW_CRYPT_PREVIEW_STARTED level=3");
        StopWaiting();
    }

    private static void StopWaiting()
    {
        SessionState.SetBool(PreviewActive, false);
        EditorApplication.update -= Tick;
    }
}
