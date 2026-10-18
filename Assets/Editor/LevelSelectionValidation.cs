using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in Edit Mode checks; no scene loads into gameplay, network sockets or save changes.</summary>
[InitializeOnLoad]
public static class LevelSelectionValidation
{
    private static double nextCheck;
    static LevelSelectionValidation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists("Temp/LevelSelect.validate") || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Move("Temp/LevelSelect.validate", "Temp/LevelSelect.validate.started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try { Validate(); }
        catch (Exception error) { File.WriteAllText("Temp/LevelSelect.result.txt", "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/The Labyrinth/Validate Menu Level Selection")]
    public static void Validate()
    {
        // Batch Editor khởi động với scene Untitled; Unity cấm Additive khi scene này chưa lưu.
        // Chỉ batch mới mở scene có sẵn; không thay scene/đóng chỉnh sửa chưa lưu của người dùng.
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Object.FindObjectOfType<LanMenuUI>() != null)
            throw new InvalidOperationException("Dừng Play và đóng lobby trước khi kiểm thử UI.");
        var previousMode = LanLaunch.Mode;
        string previousAddress = LanLaunch.Address, previousError = LanLaunch.LastError;
        int previousPort = LanLaunch.Port, previousLevel = LanLaunch.StartingLevel;
        Scene previousScene = SceneManager.GetActiveScene();
        Scene fixture = default;
        int cases = 0;
        if (File.Exists("Temp/LevelSelect.validate"))
            File.Move("Temp/LevelSelect.validate", "Temp/LevelSelect.validate.started-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        try
        {
            foreach (var mode in new[] { LanMode.Solo, LanMode.Host })
            for (int level = 1; level <= 10; level++)
            {
                LanLaunch.Configure(mode, startingLevel: level);
                Require(LanLaunch.StartingLevel == level, "Launch configuration lost selected level."); cases++;
            }
            LanLaunch.Configure(LanMode.Client, "127.0.0.1", startingLevel: 9);
            Require(LanLaunch.StartingLevel == 1, "Client must defer starting level to Host."); cases++;
            LanLaunch.Configure(LanMode.Solo);
            Require(LanLaunch.StartingLevel == 1, "Normal game no longer defaults to level 1."); cases++;
            foreach (int level in new[] { 0, 11 })
            {
                bool rejected = false;
                try { LanLaunch.Configure(LanMode.Solo, startingLevel: level); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && LanLaunch.StartingLevel == 1, "Invalid level was accepted or changed state."); cases++;
            }

            // All temporary UI lives in an additive fixture, never in the user's scene.
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var menu = new GameObject("Level selection menu fixture").AddComponent<StartMenuControls>();
            for (int pass = 0; pass < 2; pass++)
            {
                LanMenuUI.Show(menu);
                var ui = UnityEngine.Object.FindObjectOfType<LanMenuUI>();
                LanMenuUI.Show(menu);
                Require(UnityEngine.Object.FindObjectsOfType<LanMenuUI>().Length == 1, "Repeated open duplicates lobby.");
                var canvas = ui.GetComponent<Canvas>();
                Require(canvas != null && ui.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Menu cannot receive pointer input.");
                Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1, "EventSystem must exist exactly once.");
                var label = ui.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == "Selected Level");
                var buttons = ui.GetComponentsInChildren<UnityEngine.UI.Button>();
                Require(buttons.Length == 14, "Expected 10 level buttons and 4 launch/back buttons.");
                for (int level = 1; level <= 10; level++)
                {
                    var button = buttons.Single(b => b.name == "LV " + level);
                    button.onClick.Invoke();
                    Require(label.text.StartsWith($"LV {level:00} · ", StringComparison.Ordinal), "Wrong level button callback.");
                    var rect = button.GetComponent<RectTransform>();
                    var panel = (RectTransform)rect.parent;
                    Require(rect.sizeDelta.x > 0 && rect.sizeDelta.y > 0 &&
                        Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x / 2 <= panel.sizeDelta.x / 2 &&
                        Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y / 2 <= panel.sizeDelta.y / 2,
                        "Level button outside panel.");
                    cases++;
                }
                UnityEngine.Object.DestroyImmediate(ui.gameObject);
            }
            LanBuildTools.RefreshCatalog();
            File.WriteAllText("Temp/LevelSelect.result.txt", $"PASS: {cases} checks; Solo/Host level 1-10, Client authority, bounds/defaults, all 10 UI callbacks, repeated menu open, button layout, EventSystem/raycaster. Edit Mode only; no gameplay/transport test or saves modified.");
            Debug.Log($"LEVEL_SELECTION_PASS checks={cases} menuButtons=10 hostAuthority=true");
        }
        finally
        {
            if (fixture.IsValid()) EditorSceneManager.CloseScene(fixture, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            LanLaunch.Configure(previousMode, previousAddress, previousPort, previousLevel);
            LanLaunch.LastError = previousError;
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
