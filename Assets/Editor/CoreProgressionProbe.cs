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
    private static bool portalVerified;
    private static string portalMode;
    private static bool powerupVfxVerified;
    private static string powerupVfxMode;
    private static double powerupVfxStartedAt;
    private static PowerupChargeVfx powerupVfxUnderTest;
    private static PlayerMovement powerupPlayerUnderTest;
    private static bool powerupPlayerWasEnabled;
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
        portalVerified = false;
        portalMode = "unchecked";
        powerupVfxVerified = false;
        powerupVfxMode = "unchecked";
        powerupVfxStartedAt = 0d;
        powerupVfxUnderTest = null;
        powerupPlayerUnderTest = null;
        powerupPlayerWasEnabled = false;
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

        if (!portalVerified && !VerifyPortalVisual())
            return;

        if (!powerupVfxVerified && !VerifyPowerupChargeVfx())
            return;

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
            $"gameover=ok restart=ok victory=ok portal={portalMode} " +
            $"powerupVfx={powerupVfxMode}");
        CleanupAndExit(0);
    }

    private static bool VerifyPortalVisual()
    {
        PortalVisual portalVisual = UnityEngine.Object.FindObjectOfType<PortalVisual>();
        if (portalVisual == null)
        {
            Fail("The generated floor exit has no PortalVisual component.");
            return false;
        }

        int installedFrameCount = 0;
        for (int frameNumber = 1; frameNumber <= 7; frameNumber++)
        {
            if (Resources.Load<Texture2D>($"Portal/Frames/portal1_frame_{frameNumber}") != null)
                installedFrameCount++;
        }

        if (installedFrameCount != 0 && installedFrameCount != 7)
        {
            Fail($"Portal asset installation is incomplete: {installedFrameCount}/7 frames found.");
            return false;
        }

        if (installedFrameCount == 7 && !portalVisual.IsUsingPortalFrames)
        {
            Fail("Portal frames are installed but the runtime visual did not load them.");
            return false;
        }

        portalMode = installedFrameCount == 7 ? "animated" : "fallback";
        portalVerified = true;
        return true;
    }

    private static bool VerifyPowerupChargeVfx()
    {
        PlayerMovement player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
        if (player == null)
            return false;

        PowerupChargeVfx chargeVfx = player.GetComponent<PowerupChargeVfx>();
        if (chargeVfx == null)
        {
            Fail("Player has no PowerupChargeVfx component after startup.");
            return false;
        }

        Texture2D sheet = Resources.Load<Texture2D>(
            "VFX/Pipoya/TimeMagic/pipo-btleffect213_192");
        if (sheet == null)
        {
            powerupVfxMode = "fallback";
            powerupVfxVerified = true;
            return true;
        }

        if (sheet.width != 960 || sheet.height != 768)
        {
            Fail($"Pipoya Time Magic sheet has unexpected size {sheet.width}x{sheet.height}.");
            return false;
        }

        PowerupCircleController legacyCircle =
            UnityEngine.Object.FindObjectOfType<PowerupCircleController>();
        if (legacyCircle == null || legacyCircle.lineRenderer.enabled ||
            legacyCircle.circleSpriteRenderer.enabled)
        {
            Fail("The legacy white power-up circle is still visible.");
            return false;
        }

        if (powerupVfxUnderTest == null)
        {
            powerupVfxUnderTest = chargeVfx;
            powerupPlayerUnderTest = player;
            powerupPlayerWasEnabled = player.enabled;
            player.enabled = false;
            powerupVfxStartedAt = EditorApplication.timeSinceStartup;
            powerupVfxUnderTest.Show(0.5f);
            return false;
        }

        if (EditorApplication.timeSinceStartup - powerupVfxStartedAt < 1.6d)
            return false;

        if (!powerupVfxUnderTest.IsUsingTimeMagicFrames || !powerupVfxUnderTest.IsVisible)
        {
            Fail("Pipoya Time Magic VFX stopped before the hold state ended.");
            return false;
        }

        if (powerupVfxUnderTest.CurrentFrameIndex < 5 ||
            powerupVfxUnderTest.CurrentFrameIndex > 19)
        {
            Fail($"Ground-circle VFX left its full color loop: " +
                $"{powerupVfxUnderTest.CurrentFrameIndex}.");
            return false;
        }

        if (powerupVfxUnderTest.AnimationStepCount <= 10 ||
            powerupVfxUnderTest.CompletedLoopCount < 1)
        {
            Fail("Ground-circle VFX did not complete a continuous loop during hold.");
            return false;
        }

        Transform chargeVisual = player.transform.Find("Powerup Charge VFX");
        Vector2 expectedGroundCenter = new Vector2(
            legacyCircle.transform.position.x,
            player.transform.position.y);
        if (chargeVisual == null ||
            Vector2.Distance(chargeVisual.position, expectedGroundCenter) > 0.01f)
        {
            Fail("Single-circle VFX is not aligned below the player.");
            return false;
        }

        SpriteRenderer chargeRenderer = chargeVisual.GetComponent<SpriteRenderer>();
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        if (chargeRenderer == null || playerRenderer == null)
        {
            Fail("Ground-circle VFX or player SpriteRenderer is missing.");
            return false;
        }

        if (chargeRenderer.sprite == null ||
            chargeRenderer.sprite.rect.width != 192f ||
            chargeRenderer.sprite.rect.height != 192f)
        {
            Fail("Clock-circle VFX frame was cropped instead of using the full 192x192 frame.");
            return false;
        }

        if (chargeRenderer.sortingLayerID != playerRenderer.sortingLayerID ||
            chargeRenderer.sortingOrder >= playerRenderer.sortingOrder)
        {
            Fail("Ground-circle VFX is not rendered below the player.");
            return false;
        }

        if (chargeRenderer.bounds.size.y < playerRenderer.bounds.size.y * 1.35f)
        {
            Fail("Charge VFX is still too small compared with the player.");
            return false;
        }

        powerupVfxUnderTest.Hide();
        if (powerupVfxUnderTest.IsVisible)
        {
            Fail("Charge VFX stayed visible after the hold state ended.");
            return false;
        }

        if (powerupPlayerUnderTest != null)
            powerupPlayerUnderTest.enabled = powerupPlayerWasEnabled;

        powerupVfxMode = "animated";
        powerupVfxVerified = true;
        powerupVfxUnderTest = null;
        powerupPlayerUnderTest = null;
        return true;
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
