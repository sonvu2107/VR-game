using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Play from the title screen even when Dungeon is the scene open in the editor.</summary>
[InitializeOnLoad]
public static class StartMenuPlayMode
{
    static StartMenuPlayMode()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            "Assets/Scenes/StartMenu.unity");
    }
}
