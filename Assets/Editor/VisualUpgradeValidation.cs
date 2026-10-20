using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Checks the retained title, boss assets and corridor dash safety.</summary>
public static class VisualUpgradeValidation
{
    public static void Run()
    {
        try
        {
            RequireSprite("UI/Menu/LabyrinthLobbyBackground");
            RequireSprite("UI/Menu/LabyrinthTitleLogo");
            RequireSprite("UI/Menu/LabyrinthTagline");
            RequireSprite("UI/Menu/LabyrinthCoursePlaque");
            RequireSprite("UI/Menu/LabyrinthEnterButton");
            RequireSprite("UI/Menu/LabyrinthCreditsPanel");
            RequireSprite("Boss/LegRig/BoneSentinelTorso");
            RequireSprite("Boss/LegRig/BoneSentinelLeftLeg");
            RequireSprite("Boss/LegRig/BoneSentinelRightLeg");
            RequireSprite("Boss/LegRig/SkeletonKingTorso");
            RequireSprite("Boss/LegRig/SkeletonKingLeftLeg");
            RequireSprite("Boss/LegRig/SkeletonKingRightLeg");
            RequireSprite("Characters/AzureWarden/Frames/Idle_6f/Frame_00");
            RequireSprite("Characters/Chronoblade/Frames/Idle_6f/Frame_00");
            foreach (string path in new[] {
                "UI/Skills/AzureWarden/IconAegisLance",
                "UI/Skills/AzureWarden/IconWardbreaker",
                "UI/Skills/AzureWarden/IconCitadelVerdict",
                "UI/Skills/Chronoblade/IconRiftCutter",
                "UI/Skills/Chronoblade/IconTimeFracture",
                "UI/Skills/Chronoblade/IconZeroHour" })
                RequireSprite(path);
            foreach (string ability in new[] { "AzureAegisLance_v2", "AzureWardbreaker_v2",
                         "AzureCitadelVerdict_v2", "ChronoRiftCutter", "ChronoTimeFracture",
                         "ChronoZeroHour" })
                for (int i = 0; i < 6; i++)
                    if (Resources.Load<Texture2D>($"Combat/Frames/{ability}_6f/Frame_{i:00}") == null)
                        throw new Exception($"Missing restored skill frame: {ability}/{i:00}");
            foreach (string family in new[] { "InfernoBloom", "VoidHalo", "PrismBurst" })
                for (int i = 0; i < 12; i++)
                    RequireSprite($"Combat/Boss/{family}_12f/Frame_{i:00}");

            // A one-tile corridor must allow a dash along its axis while
            // refusing a step into a non-floor cell.
            var corridor = new HashSet<Vector2Int>();
            for (int x = 0; x < 8; x++) corridor.Add(new Vector2Int(x, 0));
            Vector2 along = WalkableAreaConstraint.ConstrainDelta(corridor,
                new Vector2(1.5f, 0.5f), new Vector2(3f, 0f), Vector2.zero, Vector2.zero);
            if (along.x < 2.9f) throw new Exception("Corridor dash sweep was blocked.");
            Vector2 intoWall = WalkableAreaConstraint.ConstrainDelta(corridor,
                new Vector2(1.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            if (intoWall.y > 0.51f) throw new Exception("Corridor dash crossed the wall.");

            EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
            StartMenuControls menu = UnityEngine.Object.FindObjectOfType<StartMenuControls>();
            if (menu == null) throw new Exception("StartMenu scene has no menu controller.");
            menu.ShowTitleScreen();
            GameObject titleScreen = GameObject.Find("Labyrinth Title Screen");
            if (titleScreen == null || titleScreen.GetComponent<Canvas>() == null)
                throw new Exception("StartMenu title screen was not created.");
            foreach (string objectName in new[] { "Game title", "Course caption", "Course name", "Group credits" })
            {
                Transform part = titleScreen.transform.Find(objectName);
                if (part == null || part.GetComponent<UnityEngine.UI.Image>().sprite == null)
                    throw new Exception("Missing lobby artwork: " + objectName);
            }
            Transform enterLobby = titleScreen.transform.Find("Enter lobby");
            if (enterLobby == null || enterLobby.GetComponent<UnityEngine.UI.Button>() == null ||
                enterLobby.GetComponent<UnityEngine.UI.Image>().sprite == null)
                throw new Exception("Lobby button artwork or interaction is missing.");

            Debug.Log("VISUAL_UPGRADE_VALIDATION_OK: original character assets, title scene, boss assets and corridor sweep.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogError("VISUAL_UPGRADE_VALIDATION_FAILED: " + error);
            EditorApplication.Exit(1);
        }
    }

    private static void RequireSprite(string path)
    {
        if (Resources.Load<Sprite>(path) == null)
            throw new Exception("Missing sprite: Resources/" + path);
    }

}
