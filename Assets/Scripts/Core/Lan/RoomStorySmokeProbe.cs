#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed partial class StorySmokeProbe
{
    private IEnumerator RoomJourney()
    {
        bool client = LanLaunch.Mode == LanMode.Client;
        if (!string.IsNullOrEmpty(output)) System.IO.Directory.CreateDirectory(output);
        if (LanLaunch.Mode == LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu") SceneManager.LoadScene("Dungeon");
        yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().Story.Opening);
        var gm = FindObjectOfType<GameManager>(); var story = gm.Story;
        if (LanSessionBridge.Active != null) yield return Wait(() => LanSessionBridge.Active.IsReady);
        story.RequestAdvance(true);
        yield return Wait(() => gm.CurrentState == GameState.Playing);
        if (!client)
        {
            var start = gm.dungeonGenerator.GeneratedRooms.First(r => r.Type == RoomType.Start);
            var combat = gm.dungeonGenerator.GeneratedRooms.First(r => r.Type == RoomType.Combat);
            var enemies = FindObjectsOfType<EnemyHealth>();
            var legacy = FindObjectsOfType<Enemy>();
            var actors = enemies.Select(e => e.transform).Concat(legacy.Where(e => e.GetComponent<EnemyHealth>() == null).Select(e => e.transform)).ToArray();
            Check(!actors.Any(e => start.FloorTiles.Contains(Vector2Int.FloorToInt(e.position))), "start room contains no spawned enemies including legacy warrior");
            Check(actors.Count(e => combat.FloorTiles.Contains(Vector2Int.FloorToInt(e.position))) >= 4, "start enemy moved to next combat room");
            // Địch giữ vị trí sinh thật; chỉ dừng AI trong probe để kiểm tra tuyến đi và HUD.
            foreach (var enemy in FindObjectsOfType<EnemyBrain>()) enemy.enabled = false;
            foreach (var enemy in FindObjectsOfType<EnemyCombat>()) enemy.enabled = false;
            foreach (var movement in FindObjectsOfType<EnemyMovement>()) movement.Stop();
            foreach (var enemy in legacy)
            {
                enemy.enabled = false;
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            yield return new WaitForSecondsRealtime(2f);
            Check(!story.HasBeat(1, LevelOneStory.Beat.Encounter), "no encounter dialogue consumed in start room");
            Check(story.TryReadStone(gm.PlayerMovement.GetComponent<HealthController>()), "read stone from initial safe room");
            yield return new WaitForSecondsRealtime(.4f); story.RequestAdvance(true);
            yield return Wait(() => gm.CurrentState == GameState.Playing);
            var target = combat.FloorTiles.OrderBy(c => ((Vector2)c - (Vector2)combat.SpawnPoint).sqrMagnitude)
                .Select(c => new Vector3(c.x + .5f, c.y + .5f, 0))
                .First(p => !enemies.Any(e => Vector2.Distance(e.transform.position, p) < 1.5f));
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(gm.PlayerMovement.transform.position, target, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "route from start to next room exists");
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            float until = Time.realtimeSinceStartup + 75;
            foreach (var corner in path.corners.Skip(1))
            {
                while (Vector2.Distance(gm.PlayerMovement.transform.position, corner) > .25f && !story.HasBeat(1, LevelOneStory.Beat.Encounter))
                {
                    if (Time.realtimeSinceStartup >= until) { Check(false, "keyboard walk stuck at " + gm.PlayerMovement.transform.position + " toward " + corner); yield break; }
                    Vector2 delta = corner - gm.PlayerMovement.transform.position;
                    var keys = new System.Collections.Generic.List<Key>();
                    if (Mathf.Abs(delta.x) > .13f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                    if (Mathf.Abs(delta.y) > .13f) keys.Add(delta.y > 0 ? Key.W : Key.S);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                    yield return null;
                }
                if (story.HasBeat(1, LevelOneStory.Beat.Encounter)) break;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
        yield return Wait(() => story.HasBeat(1, LevelOneStory.Beat.Encounter) && story.CueRemaining > 0);
        yield return new WaitForSecondsRealtime(.25f);
        var cue = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Lời dẫn trong trận");
        Check(cue.gameObject.activeInHierarchy && cue.text.Contains("Cả căn phòng"), "room encounter text visible on actual HUD " + LanLaunch.Mode);
        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "room-entry-" + LanLaunch.Mode + ".png"));
        yield return new WaitForSecondsRealtime(1f);
        Check(cue.gameObject.activeInHierarchy && story.CueRemaining > 0, "room dialogue stays visible while playing");
        if (client) story.DismissCue(story.CueVersion); // Client không được tự thay đổi authority.
        if (LanLaunch.Mode == LanMode.Host) yield return new WaitForSecondsRealtime(1.5f);
        Pass("ROOM_JOURNEY " + LanLaunch.Mode + " safe start keyboard path next room visible dialogue");
    }
}
#endif
