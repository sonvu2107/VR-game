using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoreProgressionProbe
{
    private const string ScenePath = "Assets/Scenes/Dungeon.unity";
    private const string ActiveSessionKey = "TheLabyrinth.CoreProgressionProbe.Active";
    private const double TimeoutSeconds = 90d;

    private static readonly HashSet<int> completedLevels = new();
    private static double startedAt;
    private static bool finishing;
    private static bool restartRequested;
    private static bool restartVerified;
    private static int restartRequestedAtFrame;

    static CoreProgressionProbe()
    {
        if (SessionState.GetBool(ActiveSessionKey, false))
            HookCallbacks();
    }

    public static void Run()
    {
        completedLevels.Clear();
        finishing = false;
        restartRequested = false;
        restartVerified = false;
        restartRequestedAtFrame = -1;
        startedAt = 0d;
        SessionState.SetBool(ActiveSessionKey, true);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HookCallbacks();
        EditorApplication.EnterPlaymode();
    }

    private static void HookCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (finishing)
            return;

        if (!EditorApplication.isPlaying)
            return;

        if (startedAt <= 0d)
            startedAt = EditorApplication.timeSinceStartup;

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Fail("Timed out while verifying the 10-level campaign.");
            return;
        }

        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (manager == null || manager.CurrentState != GameState.Playing)
            return;

        if (manager.TotalLevels != 10)
        {
            Fail($"Expected 10 levels but found {manager.TotalLevels}.");
            return;
        }

        int currentLevel = manager.CurrentLevel;
        if (currentLevel < 1 || currentLevel > manager.TotalLevels || completedLevels.Contains(currentLevel))
            return;

        if (manager.CurrentLevelDefinition == null || manager.CurrentLevelDefinition.LevelNumber != currentLevel)
        {
            Fail($"Level {currentLevel} does not have a matching definition.");
            return;
        }

        if (currentLevel == 1 && !restartVerified)
        {
            if (!restartRequested)
            {
                manager.Pause();
                if (manager.CurrentState != GameState.Paused)
                {
                    Fail("Pause did not enter the Paused state.");
                    return;
                }

                manager.Resume();
                if (manager.CurrentState != GameState.Playing)
                {
                    Fail("Resume did not return to the Playing state.");
                    return;
                }

                manager.GameOver();
                if (manager.CurrentState != GameState.GameOver)
                {
                    Fail("GameOver did not enter the GameOver state.");
                    return;
                }

                restartRequested = true;
                restartRequestedAtFrame = Time.frameCount;
                manager.RestartGame();
                return;
            }

            if (Time.frameCount <= restartRequestedAtFrame)
                return;

            restartVerified = true;
        }

        completedLevels.Add(currentLevel);
        manager.SetEnemyCount(0);
        manager.AuthorizeLevelAdvance(currentLevel);

        if (currentLevel < manager.TotalLevels && manager.CurrentState != GameState.LevelCompleted)
        {
            Fail($"Level {currentLevel} did not enter LevelCompleted.");
            return;
        }

        if (currentLevel == manager.TotalLevels)
        {
            if (manager.CurrentState != GameState.Victory)
            {
                Fail("The final level did not enter Victory.");
                return;
            }

            Succeed(manager);
        }
    }

    private static void Succeed(GameManager manager)
    {
        finishing = true;
        Debug.Log(
            $"CORE_PROGRESS_PLAYMODE_PASS: levels={manager.TotalLevels} " +
            $"completed={completedLevels.Count} pause=ok resume=ok " +
            $"gameover=ok restart=ok victory=ok");
        CleanupAndExit(0);
    }

    private static void Fail(string message)
    {
        finishing = true;
        Debug.LogError($"CORE_PROGRESS_PLAYMODE_FAIL: {message}");
        CleanupAndExit(1);
    }

    private static void CleanupAndExit(int exitCode)
    {
        SessionState.SetBool(ActiveSessionKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(exitCode);
    }
}
