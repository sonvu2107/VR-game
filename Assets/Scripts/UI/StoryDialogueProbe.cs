#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in integration check for every solo intro and dialogue cue.</summary>
public sealed class StoryDialogueProbe : MonoBehaviour
{
    public static StoryDialogueProbe Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-storyProbe") < 0) return;
        GameObject go = new("Story Dialogue Probe");
        DontDestroyOnLoad(go);
        Active = go.AddComponent<StoryDialogueProbe>();
        LanLaunch.Configure(LanMode.Solo);
        if (SceneManager.GetActiveScene().name != "Dungeon") SceneManager.LoadScene("Dungeon");
    }

    private IEnumerator Start()
    {
        GameManager manager = null;
        float deadline = Time.realtimeSinceStartup + 30f;
        while (manager == null && Time.realtimeSinceStartup < deadline)
        {
            manager = FindObjectOfType<GameManager>();
            yield return null;
        }
        if (manager == null) { Fail("No GameManager"); yield break; }
        CampaignStory story = manager.GetComponent<CampaignStory>();
        if (story == null) { Fail("No CampaignStory"); yield break; }

        int sequences = 0;
        for (int level = 1; level <= manager.TotalLevels; level++)
        {
            yield return WaitForPlaying(manager, level);
            if (manager.CurrentState != GameState.Playing || manager.CurrentLevel != level) yield break;
            sequences++; // Intro completed.

            // Keep the live enemies active so the freeze checks measure their
            // position, but place the player beyond their attack range.
            manager.PlayerMovement.transform.position = new Vector3(10000f, 10000f, 0f);
            Physics2D.SyncTransforms();

            foreach (StoryCue cue in Enum.GetValues(typeof(StoryCue)))
            {
                if (CampaignStoryText.Cue(level, cue, false).Length == 0) continue;
                if (!story.PlayProbeCue(cue)) { Fail($"Cue did not start: level={level} cue={cue}"); yield break; }
                if (manager.CurrentState != GameState.Story || Time.timeScale != 0f || manager.PlayerMovement.enabled)
                { Fail($"Gameplay not frozen: level={level} cue={cue}"); yield break; }
                EnemyHealth sample = FindObjectOfType<EnemyHealth>();
                Vector3 position = sample != null ? sample.transform.position : Vector3.zero;
                yield return new WaitForSecondsRealtime(0.12f);
                if (sample != null && Vector3.Distance(position, sample.transform.position) > 0.001f)
                { Fail($"Enemy moved during dialogue: level={level} cue={cue}"); yield break; }
                yield return WaitForPlaying(manager, level);
                if (manager.CurrentState != GameState.Playing) yield break;
                sequences++;
            }

            manager.SetEnemyCount(0);
            while (manager.KeysCollected < manager.KeysRequired)
            {
                if (!manager.CollectKey()) { Fail($"Could not collect key at level {level}"); yield break; }
                yield return WaitForPlaying(manager, level);
                if (manager.CurrentState != GameState.Playing) yield break;
            }
            manager.AuthorizeLevelAdvance(level);
        }
        if (manager.CurrentState != GameState.Victory) { Fail("No Victory after level 10"); yield break; }
        Debug.Log($"STORY_DIALOGUE_PROBE_PASS levels=10 sequences={sequences} freeze=ok bossEntry=ok release=ok");
        Application.Quit(0);
    }

    private static IEnumerator WaitForPlaying(GameManager manager, int level)
    {
        float deadline = Time.realtimeSinceStartup + 25f;
        while ((manager.CurrentLevel != level || manager.CurrentState != GameState.Playing) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (manager.CurrentLevel != level || manager.CurrentState != GameState.Playing)
            Fail($"Story stuck: expected level={level}, current={manager.CurrentLevel}, state={manager.CurrentState}");
    }

    private static void Fail(string message)
    {
        Debug.LogError("STORY_DIALOGUE_PROBE_FAIL " + message);
        Application.Quit(2);
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}
#endif
