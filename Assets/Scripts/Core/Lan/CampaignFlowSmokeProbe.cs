#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class StorySmokeProbe
{
    private void VerifyHistoricalJournal(GameManager gm, LevelOneStory story)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(LevelOneStory);
        var queue = (System.Collections.Generic.Queue<int>)type.GetField("cueQueue", flags).GetValue(story);
        var savedQueue = queue.ToArray();
        var masks = (int[])story.NarrativeMasks.Clone();
        var history = story.NarrativeHistory.ToArray();
        int level = gm.CurrentLevel, id = story.CueId, version = story.CueVersion;
        float remaining = story.CueRemaining;
        try
        {
            foreach (int floor in new[] { 6, 8, 9, 10 })
            {
                System.Array.Clear(story.NarrativeMasks, 0, 10);
                story.NarrativeHistory.Clear(); queue.Clear();
                typeof(GameManager).GetProperty("CurrentLevel").SetValue(gm, floor);
                type.GetProperty("StoryLevel").SetValue(story, floor);
                story.Record(LevelOneStory.Beat.Enter);
                story.Record(LevelOneStory.Beat.Exit);
                string before = story.JournalPageText(floor, false, 0, out _);
                Check(!before.Contains("Để ngọn lửa") && !before.Contains("Ta đã có lời giải") &&
                    !before.Contains("Vua Xương giữ lối về") && !before.Contains("Nếu lời giải trên bệ đúng"),
                    "journal never invents skipped clues floor=" + floor);
                story.NarrativeMasks[4] |= 1 << (int)LevelOneStory.Beat.Read;
                story.NarrativeMasks[5] |= 1 << (int)LevelOneStory.Beat.Activated;
                story.NarrativeMasks[7] |= 1 << (int)LevelOneStory.Beat.Assembled;
                Check(before == story.JournalPageText(floor, false, 0, out _),
                    "later discoveries never rewrite journal floor=" + floor);
                var wire = JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(new LanMessage {
                    narrativeHistoryIncluded = true, narrativeHistory = story.NarrativeHistory.ToArray()
                }));
                Check(wire.narrativeHistoryIncluded && wire.narrativeHistory.Length == 2 &&
                    wire.narrativeHistory[1].text == story.NarrativeHistory[1].text,
                    "historical Vietnamese text survives LAN serialization floor=" + floor);
            }
            Check(!CampaignStoryChapter.ForLevel(6).TextAt(4, false).Contains("Ta nghe thấy rồi"),
                "solo frozen vault response follows written clue");
        }
        finally
        {
            typeof(GameManager).GetProperty("CurrentLevel").SetValue(gm, level);
            type.GetProperty("StoryLevel").SetValue(story, level);
            for (int i = 0; i < 10; i++) story.NarrativeMasks[i] = masks[i];
            story.NarrativeHistory.Clear(); story.NarrativeHistory.AddRange(history);
            queue.Clear(); foreach (int value in savedQueue) queue.Enqueue(value);
            type.GetProperty("CueId").SetValue(story, id);
            type.GetProperty("CueVersion").SetValue(story, version);
            type.GetField("cueRemaining", flags).SetValue(story, remaining);
        }
    }

    private void VerifyJournalReplication(LevelOneStory story, int level)
    {
        var entries = story.NarrativeHistory.Where(e => e.level == level).ToArray();
        int count = System.Enum.GetValues(typeof(LevelOneStory.Beat)).Cast<LevelOneStory.Beat>().Count(b => story.HasBeat(level, b));
        Check(entries.Length == count && entries.Select(e => e.beat).Distinct().Count() == count,
            "journal history contains each actual event once level=" + level);
        Check(entries.Length > 0 && entries[0].beat == LevelOneStory.Beat.Enter,
            "journal preserves event order level=" + level);
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", entries.Select(e => e.beat + ":" + e.text)));
            Debug.Log("JOURNAL_DIGEST level=" + level + " sha256=" + System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""));
        }
    }

    private void VerifyFlowPolicy(GameManager gm, LevelOneStory story)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(LevelOneStory);
        var queue = (System.Collections.Generic.Queue<int>)type.GetField("cueQueue", flags).GetValue(story);
        var savedQueue = queue.ToArray(); var savedMasks = (int[])story.NarrativeMasks.Clone();
        int id = story.CueId, version = story.CueVersion;
        float remaining = story.CueRemaining;
        Vector3 position = gm.PlayerMovement.transform.position;
        try
        {
            System.Array.Clear(story.NarrativeMasks, 0, 10);
            var text = type.GetMethod("FlowText", flags);
            Check(!((string)text.Invoke(story, new object[] { 9, LevelOneStory.Beat.Enter })).Contains("Vua Xương"), "unassembled answer is not assumed on floor 9");
            Check(!((string)text.Invoke(story, new object[] { 10, LevelOneStory.Beat.Enter })).Contains("lời giải"), "unassembled answer is not assumed on floor 10");
            Check(!((string)text.Invoke(story, new object[] { 6, LevelOneStory.Beat.Enter })).Contains("Thư viện"), "unread library clue is not assumed");
            story.NarrativeMasks[7] = 1 << (int)LevelOneStory.Beat.Assembled;
            Check(((string)text.Invoke(story, new object[] { 9, LevelOneStory.Beat.Enter })).Contains("Vua Xương"), "assembled answer connects into next floor");
            story.NarrativeMasks[0] = 1 << (int)LevelOneStory.Beat.Exit;
            var obsolete = type.GetMethod("FlowCueObsolete", flags);
            Check(!(bool)obsolete.Invoke(story, new object[] { LevelOneStory.Beat.Assembled }), "exit never discards assembled revelation");
            Check((bool)obsolete.Invoke(story, new object[] { LevelOneStory.Beat.LockedExit }), "unlocked exit removes old locked hint");
            story.NarrativeMasks[0] = 0;
            gm.PlayerMovement.transform.position = FindObjectsOfType<EnemyHealth>().First(e => !e.IsDead).transform.position;
            queue.Clear(); queue.Enqueue(101); queue.Enqueue(100 + (int)LevelOneStory.Beat.KeyOne + 1);
            type.GetMethod("ShowNextFlowCue", flags).Invoke(story, null);
            Check(story.CueId == 100 + (int)LevelOneStory.Beat.KeyOne + 1, "quiet reflection cannot block key event in combat");
        }
        finally
        {
            for (int i = 0; i < 10; i++) story.NarrativeMasks[i] = savedMasks[i];
            queue.Clear(); foreach (int value in savedQueue) queue.Enqueue(value);
            type.GetProperty("CueId").SetValue(story, id);
            type.GetProperty("CueVersion").SetValue(story, version);
            type.GetField("cueRemaining", flags).SetValue(story, remaining);
            gm.PlayerMovement.transform.position = position; Physics2D.SyncTransforms();
        }
    }

    private void VerifyEncounterHud(LevelOneStory story, int level)
    {
        var label = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Lời dẫn trong trận");
        Check(label.gameObject.activeInHierarchy && label.text.Contains(LevelOneStory.BeatText(level, LevelOneStory.Beat.Encounter)), "floor-specific encounter visible " + LanLaunch.Mode + " level=" + level);
        label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "encounter fitted text level=" + level);
        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "encounter-" + LanLaunch.Mode + "-" + level + ".png"));
    }

    private IEnumerator VerifyFloorEncounter(GameManager gm, LevelOneStory story, HealthController hp, int level)
    {
        var rooms = gm.dungeonGenerator.GeneratedRooms;
        var start = rooms.First(r => r.Type == RoomType.Start);
        var combat = rooms.First(r => r.Type == RoomType.Combat);
        var modern = FindObjectsOfType<EnemyHealth>(); var legacy = FindObjectsOfType<Enemy>();
        var actors = modern.Select(e => e.transform).Concat(legacy.Where(e => e.GetComponent<EnemyHealth>() == null).Select(e => e.transform)).ToArray();
        Check(!actors.Any(e => start.FloorTiles.Contains(Vector2Int.FloorToInt(e.position))), "safe start level=" + level);
        Check(actors.Count(e => combat.FloorTiles.Contains(Vector2Int.FloorToInt(e.position))) >= 4, "transferred start enemy level=" + level);
        Check(!story.HasBeat(level, LevelOneStory.Beat.Encounter), "no premature encounter in start level=" + level);
        Vector3 original = hp.transform.position;
        bool positioned = false;
        foreach (var tile in combat.FloorTiles.OrderBy(p => (p - combat.SpawnPoint).sqrMagnitude))
        {
            Vector3 target = new Vector3(tile.x + .5f, tile.y + .5f, .1f);
            if (!NavMesh.SamplePosition(target, out var hit, .25f, NavMesh.AllAreas) ||
                !combat.FloorTiles.Contains(Vector2Int.FloorToInt(hit.position)) ||
                actors.Any(e => Vector2.Distance(e.position, hit.position) < 1.4f)) continue;
            hp.transform.position = hit.position; positioned = true; break;
        }
        Check(positioned, "navigable combat-room fixture level=" + level);
        Physics2D.SyncTransforms();
        yield return Wait(() => story.CueId == level * 100 + (int)LevelOneStory.Beat.Encounter + 1 && story.CueRemaining > 0);
        yield return null;
        VerifyEncounterHud(story, level);
        int version = story.CueVersion;
        yield return new WaitForSecondsRealtime(1.2f);
        Check(story.CueVersion == version && story.CueRemaining > 0, "one stable encounter cue per floor level=" + level);
        hp.transform.position = original; Physics2D.SyncTransforms();
    }
}
#endif
