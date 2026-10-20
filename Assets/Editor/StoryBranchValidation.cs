using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class StoryBranchValidation
{
    static int assertions;
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("STORY_BRANCH_FAIL " + message);
        assertions++;
    }
    public static void RunAndBuild() { Run(); LanBuildTools.BuildWindows(); }

    static void Validate(Row row, LevelOneStory story)
    {
        string dialogue = string.Join("\n", row.dialogue);
        string after = row.beats[(int)LevelOneStory.Beat.AfterReading];
        if (row.dead && row.level == 5)
            Require(!dialogue.Contains("trước hết phải qua được nó"), "defeated forge guardian is not challenged again");
        if (row.dead && row.level == 10)
            Require(!dialogue.Contains("Cùng đánh bại hắn") && !dialogue.Contains("Ngươi giữ đường về"),
                "defeated king dialogue follows outcome");
        if (row.level == 8 && row.keys == 2)
            Require(!dialogue.Contains("phải tìm đủ hai") && !dialogue.Contains("Ta phải tìm nốt"),
                "two owned keys never require finding another");
        if (row.level == 8 && row.stage == 1 && row.keys == 1)
            Require(!dialogue.Contains("Nhấn G bên dấu khắc để ghép nửa đầu"), "half assembled is acknowledged");
        if (row.stage > 0 && (row.level == 4 || row.level == 6 || row.level == 8))
            Require(!after.Contains("Thử đánh thức") && !after.Contains("Thử nhóm") &&
                !(row.stage == 2 && after.Contains("phần chữ còn thiếu")), "reflection respects completed mechanism");
        if (row.level == 6)
            Require(dialogue.IndexOf("thư viện", StringComparison.OrdinalIgnoreCase) >= 0,
                "both modes receive library clue");
        if (row.level == 7 && row.prior == 0)
            Require(dialogue.IndexOf("thư viện", StringComparison.OrdinalIgnoreCase) < 0,
                "catacomb dialogue does not invent skipped clue");
        if (row.level == 9)
            Require(dialogue.Contains("Vua Xương") && dialogue.Contains("bốn phút"), "king clue survives timer reminder");
        for (int i = 0; i < story.LineCount; i++)
            if (Call(story, "ContextualChapterText", i, row.coop) != null)
                Require(story.SpeakerAt(i, row.coop) != "GHI CHÉP CÒN LẠI", "present observation is not historical writing");
    }
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Set(object o, string name, object value) => o.GetType().GetProperty(name, F).SetValue(o, value);
    static void Field(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    [Serializable] public class Row
    {
        public int level, keys, stage, prior;
        public bool coop, read, dead;
        public string[] dialogue, beats;
    }
    public static void Run()
    {
        assertions = 0;
        string dir = "D:/tl-story-eight-fixes-20261020";
        Directory.CreateDirectory(dir);
        var root = new GameObject("Isolated branch audit"); root.SetActive(false);
        var gm = root.AddComponent<GameManager>();
        var story = root.AddComponent<LevelOneStory>(); story.Initialize(gm);
        Set(gm, "Story", story); Set(gm, "CurrentState", GameState.Playing);
        Field(gm, "levels", LevelDefinition.CreateDefaultCampaign());
        int count = 0;
        try
        {
            using (var writer = new StreamWriter(dir + "/matrix.jsonl", false, new System.Text.UTF8Encoding(false)))
            for (int level = 1; level <= 10; level++)
            foreach (bool coop in new[] { false, true })
            foreach (bool read in new[] { false, true })
            foreach (bool dead in new[] { false, true })
            for (int prior = 0; prior < 8; prior++)
            {
                if (dead && level != 5 && level != 9 && level != 10) continue;
                int required = level == 4 ? 1 : level == 8 ? 2 : 0;
                for (int keys = 0; keys <= required; keys++)
                for (int stage = 0; stage <= (level == 8 ? keys : level == 4 || level == 6 ? 1 : 0); stage++)
                {
                    Array.Clear(story.NarrativeMasks, 0, 10);
                    Set(gm, "CurrentLevel", level); Set(gm, "KeysRequired", required); Set(gm, "KeysCollected", keys);
                    gm.enemyCount = dead ? 0 : 4;
                    Set(story, "StoryLevel", level); Set(story, "StoneRead", read);
                    Field(story, "chapter", CampaignStoryChapter.ForLevel(level));
                    if ((prior & 1) != 0) story.NarrativeMasks[4] |= 1 << (int)LevelOneStory.Beat.Read;
                    if ((prior & 2) != 0) story.NarrativeMasks[5] |= 1 << (int)LevelOneStory.Beat.Read;
                    if ((prior & 4) != 0 && level > 8) story.NarrativeMasks[7] |= 1 << (int)LevelOneStory.Beat.Assembled;
                    if (read) story.NarrativeMasks[level - 1] |= 1 << (int)LevelOneStory.Beat.Read;
                    if (dead) story.NarrativeMasks[level - 1] |= 1 << (int)LevelOneStory.Beat.GuardianDead;
                    if (stage > 0) story.NarrativeMasks[level - 1] |= 1 << (int)LevelOneStory.Beat.Activated;
                    if (stage == 2) story.NarrativeMasks[level - 1] |= 1 << (int)LevelOneStory.Beat.Assembled;
                    var row = new Row { level=level, keys=keys, stage=stage, prior=prior, coop=coop, read=read, dead=dead,
                        dialogue=Enumerable.Range(0, story.LineCount).Select(i => story.SpeakerAt(i, coop) + ": " + story.TextAt(i, coop)).ToArray(),
                        beats=Enum.GetValues(typeof(LevelOneStory.Beat)).Cast<LevelOneStory.Beat>().Select(b => b + ": " + (string)Call(story, "FlowText", level, b)).ToArray() };
                    if (level > 1) Validate(row, story);
                    writer.WriteLine(JsonUtility.ToJson(row)); count++;
                }
            }
            Array.Clear(story.NarrativeMasks, 0, 10); story.NarrativeHistory.Clear();
            Set(story, "StoryLevel", 10); Set(gm, "CurrentLevel", 10); Set(gm, "KeysRequired", 0); gm.enemyCount = 1;
            var bossRoot = new GameObject("Dead final boss fixture"); bossRoot.SetActive(false);
            var hp = bossRoot.AddComponent<EnemyHealth>(); bossRoot.AddComponent<CampaignFinalBoss>();
            Field(hp, "isDead", true);
            Call(gm, "OnConfiguredEnemyDied", hp);
            Require(gm.enemyCount == 0 && !story.NarrativeHistory.First(e => e.beat == LevelOneStory.Beat.GuardianDead).text.Contains("Còn lính"),
                "last boss death records zero remaining guards");
            File.WriteAllText(dir + "/last-boss.txt", "enemies=" + gm.enemyCount + "\n" +
                string.Join("\n", story.NarrativeHistory.Select(e => e.beat + ": " + e.text)));
            UnityEngine.Object.DestroyImmediate(bossRoot);
            Array.Clear(story.NarrativeMasks, 0, 10); story.NarrativeHistory.Clear(); gm.enemyCount = 4;
            var otherBoss = new GameObject("Dead boss with remaining guards"); otherBoss.SetActive(false);
            var otherHp = otherBoss.AddComponent<EnemyHealth>(); otherBoss.AddComponent<CampaignFinalBoss>();
            Field(otherHp, "isDead", true); Call(gm, "OnConfiguredEnemyDied", otherHp);
            Require(gm.enemyCount == 3 && story.NarrativeHistory.First(e => e.beat == LevelOneStory.Beat.GuardianDead).text.Contains("Còn lính"),
                "boss death preserves remaining guards");
            UnityEngine.Object.DestroyImmediate(otherBoss);
            using (var writer = new StreamWriter(dir + "/mechanism-history.txt", false, new System.Text.UTF8Encoding(false)))
            foreach (int floor in new[] { 4, 6, 8 })
            {
                Array.Clear(story.NarrativeMasks, 0, 10); story.NarrativeHistory.Clear();
                Set(story, "StoryLevel", floor); Set(gm, "CurrentLevel", floor);
                Field(story, "chapter", CampaignStoryChapter.ForLevel(floor)); Set(story, "StoneRead", false);
                Set(gm, "KeysRequired", floor == 8 ? 2 : floor == 4 ? 1 : 0);
                Set(gm, "KeysCollected", floor == 8 ? 2 : 0);
                Field(story, "interactionReadyAt", 0f);
                var readerRoot = new GameObject("Mechanism reader"); readerRoot.SetActive(false);
                var reader = readerRoot.AddComponent<HealthController>(); reader.currentHealth = 3;
                reader.transform.position = story.StonePosition;
                bool activated = story.TryActivateMechanism(reader);
                story.Record(LevelOneStory.Beat.Read);
                Set(story, "StoneRead", true);
                story.Record(LevelOneStory.Beat.AfterReading);
                Require(activated && story.MechanismStage == (floor == 8 ? 2 : 1), "real optional mechanism activates");
                string reflection = story.NarrativeHistory.First(e => e.beat == LevelOneStory.Beat.AfterReading).text;
                Require(!reflection.Contains("Thử đánh thức") && !reflection.Contains("Thử nhóm") &&
                    !reflection.Contains("phần chữ còn thiếu"), "journal after late reading never asks to redo mechanism");
                Require(story.NarrativeHistory.First(e => e.beat == LevelOneStory.Beat.Read).text.Contains(story.TextAt(0, false)),
                    "read summary matches current inscription");
                if (floor == 8)
                    Require(!story.NarrativeHistory.First(e => e.beat == LevelOneStory.Beat.Activated).text.Contains("thiếu nửa sau"),
                        "direct two-key assembly does not invent missing half");
                writer.WriteLine("floor=" + floor + " activated=" + activated + " stage=" + story.MechanismStage);
                writer.WriteLine(string.Join("\n", story.NarrativeHistory.Select(e => e.beat + ": " + e.text)));
                UnityEngine.Object.DestroyImmediate(readerRoot);
            }
            Require(!DecorationInspection.Describe("pillar", 1, false, false).Contains("lời nhắn trên bia"),
                "pillar before reading has no inscription knowledge");
            Require(!DecorationInspection.Describe("relic", 8, false, false).Contains("bệ"),
                "relic before discovery has no dais knowledge");
            Require(DecorationInspection.Describe("relic", 8, false, false, true, true, true).Contains("Lời giải trên bệ đã rõ"),
                "relic after solution acknowledges completion");
            Debug.Log("STORY_BRANCH_VALIDATION_PASS scenarios=" + count + " assertions=" + assertions);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
