using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in rendering in copied additive scenes; never saves or closes a user's scene.</summary>
[InitializeOnLoad]
public static class OrnateUIEditorValidation
{
    private const string Request = "Temp/OrnateUI.validate";
    private const string FixtureFolder = "Assets/Editor/OrnateUITestScenes";
    private static Scene fixture;
    private static double nextCheck;
    static OrnateUIEditorValidation() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;
        File.Delete(Request);
        var previous = SceneManager.GetActiveScene();
        var state = typeof(LanLaunch).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => !f.IsLiteral && !f.IsInitOnly).ToDictionary(f => f, f => f.GetValue(null));
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var focus = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        bool ownsFolder = false;
        try
        {
            if (Directory.Exists(FixtureFolder)) throw new IOException("Fixture directory already exists; refusing to overwrite.");
            Directory.CreateDirectory(FixtureFolder);
            ownsFolder = true;
            File.WriteAllText("Temp/OrnateUI.started", DateTime.UtcNow.ToString("O"));
            MenuFlowValidation.Run();
        }
        catch (Exception error)
        {
            File.WriteAllText("Captures/OrnateUI/editor-error.txt", error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            foreach (var pair in state) pair.Key.SetValue(null, pair.Value);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(focus);
            // Only the fixed folder created by this invocation may be removed.
            if (ownsFolder && Directory.Exists(FixtureFolder))
                AssetDatabase.DeleteAsset(FixtureFolder);
        }
    }

    public static void OpenFixture(string name)
    {
        if (Application.isBatchMode)
        {
            EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
            return;
        }
        if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
        string path = $"{FixtureFolder}/{name}.unity";
        File.Copy($"Assets/Scenes/{name}.unity", path, false);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        fixture = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
    }
}
