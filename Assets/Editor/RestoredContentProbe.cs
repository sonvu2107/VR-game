using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RestoredContentProbe
{
    private const string SessionKey = "Labyrinth.RestoredContentProbe.Active";
    private static double deadline;
    static RestoredContentProbe()
    {
        if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        deadline = 0d;
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (deadline == 0d) deadline = EditorApplication.timeSinceStartup + 30d;
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (manager != null && manager.CurrentState == GameState.Playing)
        {
            HealthController health = manager.PlayerMovement.GetComponent<HealthController>();
            bool minimap = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None)
                .Any(image => image.enabled && image.gameObject.activeInHierarchy && image.texture is RenderTexture);
            bool arrow = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Any(renderer => renderer.enabled && renderer.sprite != null && renderer.name == "Nearest Enemy Direction");
            bool fiveHearts = health != null && health.startHeart == 5 && health.MaxHealth == 10 && health.heartImages.Count(image => image != null && image.enabled) == 5;
            if (minimap && arrow && fiveHearts)
            {
                Debug.Log("RESTORED_CONTENT_PLAYMODE_PASS minimap=ok hearts=5 indicator=ok solo=ok");
                Finish(0); return;
            }
        }
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("RESTORED_CONTENT_PLAYMODE_FAIL missing minimap, five hearts or enemy direction indicator");
            Finish(1);
        }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
