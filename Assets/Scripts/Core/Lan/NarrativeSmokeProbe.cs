#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed partial class StorySmokeProbe
{
    private IEnumerator CollectRealKey(GameManager gm)
    {
        var key = FindObjectsOfType<DungeonKey>().FirstOrDefault();
        Check(key != null, "required key exists in generated floor");
        if (key == null) yield break;
        int before = gm.KeysCollected;
        gm.PlayerMovement.transform.position = key.transform.position + Vector3.right * 2;
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate();
        gm.PlayerMovement.transform.position = key.transform.position;
        Physics2D.SyncTransforms();
        yield return Wait(() => gm.KeysCollected > before);
        yield return null;
        Check(gm.Story.HasBeat(gm.CurrentLevel, before == 0 ? LevelOneStory.Beat.KeyOne : LevelOneStory.Beat.KeyTwo), "actual pickup records exactly one key beat");
    }

    private IEnumerator Narrative()
    {
        var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        if (!string.IsNullOrEmpty(output)) System.IO.Directory.CreateDirectory(output);
        bool client = LanLaunch.Mode == LanMode.Client;
        bool disconnect = Array.IndexOf(Environment.GetCommandLineArgs(), "-narrativeDisconnect") >= 0;
        if (LanLaunch.Mode == LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu") SceneManager.LoadScene("Dungeon");
        yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState != GameState.Generating);
        var gm = FindObjectOfType<GameManager>();
        var story = gm.Story;
        if (!reconnect)
        {
            yield return Wait(() => story.Opening && gm.CurrentState == GameState.Story);
            if (LanSessionBridge.Active != null) yield return Wait(() => LanSessionBridge.Active.IsReady);
            yield return new WaitForSecondsRealtime(.4f);
            Check(story.PresentationVisible && !story.StoneRead && Time.timeScale == 0, "opening scene pauses gameplay without reading stone");
            Check(story.PresentationArtReady, "both pixel art backgrounds loaded");
            Check(story.WorldHiddenForPresentation && FindObjectsOfType<Renderer>().All(r => !r.enabled || r.forceRenderingOff), "opening hides every world renderer");
            if (client && !string.IsNullOrEmpty(output))
                CaptureStory(story, System.IO.Path.Combine(output, "opening-Client-" + story.LineIndex + ".png"));
            bool introDisconnect = Array.IndexOf(Environment.GetCommandLineArgs(), "-openingDisconnect") >= 0;
            if (introDisconnect && LanLaunch.Mode != LanMode.Solo)
            {
                if (client)
                {
                    yield return Wait(() => story.LineIndex == 1);
                    Debug.Log("OPENING_CLIENT_DISCONNECT_READY"); Application.Quit(0); yield break;
                }
                story.RequestAdvance(false);
                yield return Wait(() => !LanSessionBridge.Active.IsReady);
                Check(story.Opening && story.LineIndex == 1, "opening holds its shot during disconnect");
                Debug.Log("OPENING_HOST_REJOIN_READY");
                yield return Wait(() => LanSessionBridge.Active.IsReady);
            }
            if (LanLaunch.Mode == LanMode.Solo)
            {
                yield return VerifyDialogueWrapping(story);
                for (int shot = 0; shot < 3; shot++)
                {
                    Check(story.LineIndex == shot && story.Opening, "opening shot " + shot);
                    foreach (var label in story.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Check(!label.isTextOverflowing, "opening layout fits");
                    }
                    if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "opening-" + shot + ".png"));
                    yield return new WaitForSecondsRealtime(.3f);
                    story.RequestAdvance(false);
                    yield return new WaitForSecondsRealtime(.3f);
                }
            }
            else if (client) { yield return new WaitForSecondsRealtime(.5f); story.RequestAdvance(true); }
            yield return Wait(() => gm.CurrentState == GameState.Playing && !story.Opening);
            yield return null;
            Check(!story.StoneRead, "opening skip leaves inscription unread");
            Check(!story.WorldHiddenForPresentation && FindObjectsOfType<Renderer>().Any(r => r.enabled && !r.forceRenderingOff), "gameplay world restored after opening");
        }
        for (int level = reconnect ? 8 : 1; level <= 10; level++)
        {
            int floor = level;
            yield return Wait(() => gm.CurrentLevel == floor && gm.CurrentState == GameState.Playing);
            if (LanSessionBridge.Active != null) yield return Wait(() => LanSessionBridge.Active.IsReady);
            yield return new WaitForSecondsRealtime(.4f);
            Check(Time.timeScale == 1, "nonblocking entry " + level);
            Check(story.HasBeat(level, LevelOneStory.Beat.Enter), "replicated entry " + level);
            if (level > 1) Check(story.HasBeat(1, LevelOneStory.Beat.Read), "journal persists across levels " + level);
            if (client)
            {
                if (!(reconnect && level == 8))
                {
                    yield return Wait(() => story.CueId == level * 100 + (int)LevelOneStory.Beat.Encounter + 1 && story.CueRemaining > 0);
                    yield return null;
                    VerifyEncounterHud(story, level);
                    yield return VerifyClientDecorationInteraction(gm, story);
                }
                if (level == 8 && disconnect && !reconnect)
                {
                    yield return Wait(() => story.HasBeat(8, LevelOneStory.Beat.Activated));
                    Debug.Log("NARRATIVE_CLIENT_DISCONNECT_READY"); Application.Quit(0); yield break;
                }
                if (reconnect && level == 8)
                    Check(story.HasBeat(8, LevelOneStory.Beat.KeyOne) && gm.KeysCollected >= 1, "rejoin restores key and journal");
                if (reconnect && level == 8) Check(story.MechanismStage == 1 && !string.IsNullOrEmpty(story.DialogueArchive[0]), "rejoin restores mechanism and dialogue archive");
                if (reconnect && level == 8) Check(story.VisibleKeyHalves == 1, "rejoin restores half assembled dais");
                if (reconnect && level == 8)
                    for (int previous = 1; previous < 8; previous++) VerifyJournalReplication(story, previous);
                yield return Wait(() => gm.CurrentState == GameState.Story);
                Check(story.Decorations.Length > 0 && story.DecorationNotes.Count(n => n.level == level) == story.Decorations.Length,
                    "all decoration discoveries replicated level=" + level);
                Check(story.HasBeat(level, LevelOneStory.Beat.Read), "read milestone replicated " + level);
                yield return new WaitForSecondsRealtime(.3f);
                story.RequestAdvance(true);
                yield return Wait(() => gm.CurrentState == GameState.Playing);
                if (story.HasMechanism) yield return Wait(() => story.MechanismStage > 0);
                if (level == 6) { yield return null; Check(story.BeaconLight.enabled && story.FlameVisible, "client beacon light and flame enabled"); }
                if (level == 8) yield return Wait(() => story.MechanismStage == 2);
                Check(!string.IsNullOrEmpty(story.DialogueArchive[level - 1]), "client receives full dialogue archive");
                yield return Wait(() => story.HasBeat(level, LevelOneStory.Beat.Exit));
                if (level == 4 || level == 8) Check(story.HasBeat(level, LevelOneStory.Beat.KeyOne), "key milestone replicated");
                if (level == 8) Check(story.HasBeat(level, LevelOneStory.Beat.KeyTwo), "second key milestone replicated");
                if (level == 7) Check(story.HasBeat(7, LevelOneStory.Beat.Poison) && story.HasBeat(7, LevelOneStory.Beat.Heal), "actual poison and healing replicated");
                if (level == 9) Check(story.HasBeat(9, LevelOneStory.Beat.TwoMinutes) && story.HasBeat(9, LevelOneStory.Beat.OneMinute), "timer warnings replicated");
            }
            else
            {
                if (level == 1)
                {
                    // Tái hiện khung tồn tại từ trước khi sửa mã trong Unity Play.
                    var hintHud = story.GetComponentsInChildren<Canvas>(true).First(c => c.name == "Lời nhắc khám phá");
                    var legacyCard = new GameObject("Nền lời nhắc", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    legacyCard.transform.SetParent(hintHud.transform, false);
                    yield return null; yield return null;
                    Check(legacyCard == null, "legacy interaction background removed after hot reload");
                    VerifyFlowPolicy(gm, story);
                    VerifyHistoricalJournal(gm, story);
                    var cue = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Lời dẫn trong trận");
                    var flowText = typeof(LevelOneStory).GetMethod("FlowText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    string unreadExit = (string)flowText.Invoke(story, new object[] { 1, LevelOneStory.Beat.Exit });
                    Check(!unreadExit.Contains("bia"), "unread branch never cites inscription");
                    string old = cue.text;
                    foreach (int sampleLevel in Enumerable.Range(1, 10))
                    foreach (LevelOneStory.Beat beat in Enum.GetValues(typeof(LevelOneStory.Beat)))
                    {
                        cue.text = LevelOneStory.BeatText(sampleLevel, beat) + "\n<size=16>[Backspace] Bỏ qua lời nhắc · [J] Nhật ký</size>";
                        typeof(LevelOneStory).GetMethod("FitNarrativeCards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(story, null);
                        cue.ForceMeshUpdate(true);
                        Check(!cue.isTextOverflowing, "cue text fits " + sampleLevel + "/" + beat);
                        Check(cue.font.HasCharacters(cue.text, out uint[] missingGlyphs, true, true), "cue glyphs " + sampleLevel + "/" + beat);
                    }
                    cue.text = old;
                    float beforeSkip = story.CueRemaining;
                    story.DismissCue(story.CueVersion - 1);
                    Check(story.CueRemaining == beforeSkip, "stale cue skip rejected");
                    story.DismissCue(story.CueVersion);
                    Check(story.CueRemaining == 0 && story.HasBeat(1, LevelOneStory.Beat.Enter), "cue skip preserves journal");
                }
                // Các fixture chỉ điều khiển vị trí, thời gian và sát thương để đi hết chiến dịch.
                // Milestone vẫn phải phát từ các handler thật của game.
                foreach (var enemy in FindObjectsOfType<EnemyBrain>()) enemy.enabled = false;
                foreach (var enemy in FindObjectsOfType<EnemyCombat>()) enemy.enabled = false;
                foreach (var enemy in FindObjectsOfType<MiniBossController>()) enemy.enabled = false;
                foreach (var enemy in FindObjectsOfType<SkeletonKingController>()) enemy.enabled = false;
                foreach (var enemy in FindObjectsOfType<EnemySummoner>()) enemy.enabled = false;
                foreach (var enemy in FindObjectsOfType<Enemy>()) enemy.enabled = false;
                foreach (var movement in FindObjectsOfType<EnemyMovement>()) movement.Stop();
                var hp = gm.PlayerMovement.GetComponent<HealthController>(); hp.RestoreFullHealth();
                yield return VerifyFloorEncounter(gm, story, hp, level);
                yield return VerifyDecorationInteractions(gm, story, hp);
                if (level == 10)
                {
                    var bosses = FindObjectsOfType<CampaignFinalBoss>();
                    Check(bosses.Length == 1 && bosses[0].SourceName == "SkeletonKing", "final floor contains exactly the Skeleton King");
                    var king = bosses[0].GetComponent<SkeletonKingController>();
                    var kingHealth = bosses[0].GetComponent<EnemyHealth>();
                    Check(king != null && kingHealth.Config.displayName == "Vua Xương" && kingHealth.MaxHealth == 1400, "king identity and health match narrative");
                    king.enabled = true;
                    kingHealth.TakeDamage(500);
                    yield return Wait(() => king.CurrentPhase == 2 && !king.IsTransitioning);
                    Check(!kingHealth.IsDead && !story.HasBeat(10, LevelOneStory.Beat.GuardianDead), "phase two is not a defeated king");
                    kingHealth.TakeDamage(500);
                    yield return Wait(() => king.CurrentPhase == 3 && !king.IsTransitioning);
                    Check(!kingHealth.IsDead, "king reaches phase three through actual damage");
                    king.enabled = false;
                    hp.RestoreFullHealth();
                }
                gm.TryAdvanceLevel();
                Check(gm.CurrentState == GameState.Playing && gm.CurrentLevel == level, "reading never bypasses exit requirements " + level);
                if (level == 8 && disconnect)
                {
                    yield return CollectRealKey(gm);
                    hp.transform.position = story.StonePosition + Vector3.right; Physics2D.SyncTransforms();
                    Check(story.TryActivateMechanism(hp) && story.MechanismStage == 1, "first key assembles half before reconnect");
                    yield return Wait(() => !LanSessionBridge.Active.IsReady);
                    Debug.Log("NARRATIVE_HOST_REJOIN_READY");
                    yield return Wait(() => LanSessionBridge.Active.IsReady && gm.CurrentState == GameState.Playing);
                    yield return new WaitForSecondsRealtime(1f);
                }
                hp.transform.position = story.StonePosition + Vector3.right;
                Physics2D.SyncTransforms();
                yield return new WaitForSecondsRealtime(.4f);
                Check(story.HasBeat(level, LevelOneStory.Beat.Discover), "discovery follows player position " + level);
                if (level == 6 && !string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "discovery-" + LanLaunch.Mode + ".png"));
                if (level == 1)
                {
                    var wall = new GameObject("Interaction wall fixture", typeof(BoxCollider2D));
                    wall.transform.position = story.StonePosition + Vector3.right * .5f;
                    wall.GetComponent<BoxCollider2D>().size = new Vector2(.2f, 1f); Physics2D.SyncTransforms();
                    Check(!story.TryReadStone(hp), "solid obstacle blocks reading");
                    Destroy(wall); yield return null; Physics2D.SyncTransforms();

                }
                if (level == 4 || level == 6)
                {
                    int enemies = gm.enemyCount, keys = gm.KeysCollected;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                    Check(story.MechanismStage == 1, "G activates optional mechanism " + level);
                    Check(!story.TryActivateMechanism(hp), "activation is idempotent");
                    Check(enemies == gm.enemyCount && keys == gm.KeysCollected, "activation preserves exit counters");
                    if (level == 4) Check(story.GuideAvailable, "key compass has real target");
                    if (level == 6) Check(story.BeaconLight.enabled && story.FlameVisible && story.BeaconLight.pointLightOuterRadius == 4, "fixed light and animated flame enabled");
                    yield return new WaitForSecondsRealtime(.4f);
                    if (level == 6 && !string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "flame-lit.png"));
                }
                if (level == 8 && gm.KeysCollected == 0) Check(!story.TryActivateMechanism(hp), "no key cannot assemble oath");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Wait(() => gm.CurrentState == GameState.Story);
                Check(story.StoneRead && Time.timeScale == 0, "F reads optional inscription " + level);
                int readMask = story.NarrativeMasks[level - 1];
                yield return new WaitForSecondsRealtime(.25f);
                if (LanLaunch.Mode == LanMode.Solo) story.RequestAdvance(true);
                yield return Wait(() => gm.CurrentState == GameState.Playing);
                Check((story.NarrativeMasks[level - 1] & readMask) == readMask, "skip keeps discovered progress " + level);
                Check(!string.IsNullOrEmpty(story.DialogueArchive[level - 1]), "skip retains full transcript");
                if (LanLaunch.Mode == LanMode.Solo)
                {
                    yield return new WaitForSecondsRealtime(.4f);
                    int session = story.StorySession;
                    Check(story.TryReadStone(hp) && story.LineIndex == 0 && story.StorySession == session + 1, "reread resets cursor with new session");
                    yield return new WaitForSecondsRealtime(.25f); story.RequestAdvance(true);
                    yield return Wait(() => gm.CurrentState == GameState.Playing);
                }
                if (level == 1)
                {
                    Check(!story.HasBeat(1, LevelOneStory.Beat.AfterReading), "reflection waits for leaving inscription");
                    hp.transform.position += Vector3.right * 3f;
                    yield return new WaitForSecondsRealtime(.5f);
                    Check(story.HasBeat(1, LevelOneStory.Beat.AfterReading), "leaving inscription records connected reflection");
                    // Gặp quái theo ranh phòng được kiểm tra riêng bằng RoomJourney.
                }
                if (level == 8)
                    Check(!story.DialogueArchive[7].Contains("Vua Xương"), "inscription does not reveal unassembled answer");
                if (level == 3)
                {
                    var archer = FindObjectsOfType<EnemyHealth>().FirstOrDefault(e => e.Config != null && e.Config.archetype == EnemyArchetype.Ranged);
                    Check(archer != null, "floor 3 has ranged enemy");
                    if (archer != null) hp.transform.position = archer.transform.position + Vector3.right * 3;
                    yield return new WaitForSecondsRealtime(.5f);
                    Check(story.HasBeat(3, LevelOneStory.Beat.Ranged), "ranged cue follows encounter");
                }
                if (level == 7)
                {
                    hp.RestoreFullHealth();
                    var poison = new GameObject("Narrative poison fixture", typeof(CircleCollider2D), typeof(PoisonArea));
                    poison.transform.position = hp.transform.position;
                    poison.GetComponent<CircleCollider2D>().radius = 1;
                    poison.GetComponent<PoisonArea>().Initialize(1, 3, 5, 0);
                    yield return Wait(() => story.HasBeat(7, LevelOneStory.Beat.Poison));
                    Destroy(poison);
                    HealingPickup.Create(hp.transform.position);
                    yield return Wait(() => story.HasBeat(7, LevelOneStory.Beat.Heal));
                    Check(hp.currentHealth > 0, "poison then actual potion heal");
                }
                if (level == 9)
                {
                    Check(gm.LevelTimeRemaining < 240 && gm.LevelTimeRemaining > 210, "240 second budget starts during exploration");
                    typeof(GameManager).GetProperty("LevelTimeRemaining").SetValue(gm, 119f);
                    yield return Wait(() => story.HasBeat(9, LevelOneStory.Beat.TwoMinutes));
                    typeof(GameManager).GetProperty("LevelTimeRemaining").SetValue(gm, 59f);
                    yield return Wait(() => story.HasBeat(9, LevelOneStory.Beat.OneMinute));
                }
                while (gm.KeysCollected < gm.KeysRequired)
                {
                    yield return CollectRealKey(gm);
                    gm.TryAdvanceLevel();
                    Check(gm.CurrentState == GameState.Playing, "key alone cannot bypass remaining enemies " + level);
                    if (level == 8)
                    {
                        hp.transform.position = story.StonePosition + Vector3.right; Physics2D.SyncTransforms();
                        yield return new WaitForSecondsRealtime(.4f);
                        Check(story.TryActivateMechanism(hp) && story.MechanismStage == gm.KeysCollected, "collected key assembles next oath stage");
                        yield return null;
                        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "dais-" + gm.KeysCollected + ".png"));
                    }
                }
                foreach (var enemy in FindObjectsOfType<EnemyHealth>()) if (!enemy.IsDead) enemy.TakeDamage(1000000);
                gm.SetEnemyCount(0); // Quái legacy chưa có EnemyHealth cũng được dọn trong fixture.
                if (gm.KeysCollected < gm.KeysRequired)
                {
                    gm.TryAdvanceLevel(); Check(gm.CurrentState == GameState.Playing, "missing key still blocks exit");
                    while (gm.KeysCollected < gm.KeysRequired) gm.CollectKey();
                }
                yield return Wait(() => story.HasBeat(level, LevelOneStory.Beat.Exit));
                if (level == 5 || level == 9 || level == 10) Check(story.HasBeat(level, LevelOneStory.Beat.GuardianDead), "guardian death comes from real damage " + level);
                yield return new WaitForSecondsRealtime(1f);
            }
            if (level == 8) Check(story.VisibleKeyHalves == 2, "both keys visible on dais");
            if (level == 10) Check(story.ReturnPortalReady, "return portal opens only after clear");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            foreach (var label in story.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Check(!label.isTextOverflowing, "narrative UI fits " + level);
                Check(label.font.HasCharacters(label.text, out uint[] missing, true, true), "Vietnamese narrative glyphs " + level);
            }
            var journalLabel = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Nhật ký");
            foreach (bool transcript in new[] { false, true })
            {
                story.JournalPageText(level, transcript, 0, out int pages);
                for (int page = 0; page < pages; page++)
                {
                    journalLabel.text = story.JournalPageText(level, transcript, page, out _);
                    journalLabel.ForceMeshUpdate(true); Check(!journalLabel.isTextOverflowing, "all journal pages fit");
                }
            }
            if (level == 8 && !string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "journal-" + LanLaunch.Mode + ".png"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            VerifyJournalReplication(story, level);
            Debug.Log("NARRATIVE_LEVEL_PASS " + LanLaunch.Mode + " level=" + level + " mask=" + story.NarrativeMasks[level - 1]);
            if (!client) gm.TryAdvanceLevel();
            if (level < 10) yield return Wait(() => gm.CurrentLevel > floor);
        }
        yield return Wait(() => gm.CurrentState == GameState.Story && story.Ending);
        Check(story.HasBeat(10, LevelOneStory.Beat.Ending), "ending journal synchronized");
        yield return new WaitForSecondsRealtime(.5f);
        Check(story.PresentationVisible && !story.Opening, "ending renders outside ruins scene");
        Check(story.WorldHiddenForPresentation && FindObjectsOfType<Renderer>().All(r => !r.enabled || r.forceRenderingOff), "ending hides every world renderer");
        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "ending-" + LanLaunch.Mode + ".png"));
        if (client || LanLaunch.Mode == LanMode.Solo) story.RequestAdvance(true);
        yield return Wait(() => gm.CurrentState == GameState.Victory);
        Check(!GameManager.isStoryPlaying, "ending skip reaches victory");
        if (!client && LanSessionBridge.Active != null) yield return new WaitForSecondsRealtime(1.5f);
        Pass("NARRATIVE " + LanLaunch.Mode + (reconnect ? " levels=8-10 rejoin" : " levels=1-10") + " actual-events optional-F skip journal keys timer ending");
    }
}
#endif
