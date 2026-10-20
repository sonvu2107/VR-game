#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class StorySmokeProbe
{
    private IEnumerator VerifyDecorationInteractions(GameManager gm, LevelOneStory story, HealthController hp)
    {
        int level = gm.CurrentLevel, enemies = gm.enemyCount, keys = gm.KeysCollected;
        var original = hp.transform.position;
        var cooldown = typeof(LevelOneStory).GetField("inspectReadyAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Check(story.Decorations.Length > 0, "decoration targets exist level=" + level);
        Check(story.Decorations.Select(d => d.id).Distinct().Count() == story.Decorations.Length, "unique decoration IDs level=" + level);
        var layout = (DungeonLayout)typeof(InfiniteWorldGenerator).GetField("dungeonLayout",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(gm.dungeonGenerator);
        // Đuốc còn nằm ở hành lang, không chỉ trong GeneratedRooms.
        var tiles = layout.FloorTiles.ToArray();
        bool obstructionChecked = false;
        foreach (var item in story.Decorations)
        {
            bool reachable = false;
            foreach (var tile in tiles.OrderBy(t => ((Vector2)t + Vector2.one * .5f - item.point).sqrMagnitude))
            {
                Vector3 point = new Vector3(tile.x + .5f, tile.y + .5f, 0);
                if (Vector2.Distance(point, item.point) > DecorationInspection.Range(item)) break;
                if (!NavMesh.SamplePosition(point, out var hit, .35f, NavMesh.AllAreas)) continue;
                if (Physics2D.OverlapCircleAll(hit.position, .18f).Any(c => !c.isTrigger &&
                    c.GetComponentInParent<HealthController>() == null && c.GetComponentInParent<EnemyHealth>() == null &&
                    c.GetComponentInParent<Enemy>() == null)) continue;
                hp.transform.position = hit.position;
                if (!story.CanInspect(hp, item)) continue;
                reachable = true; break;
            }
            Check(reachable, $"reachable decoration level={level} id={item.id} kind={item.kind} point={item.point}");
            if (!reachable) yield break;
            if (!obstructionChecked && !item.wall && Vector2.Distance(hp.transform.position, item.point) > .5f)
            {
                var obstruction = new GameObject("Inspection obstacle fixture", typeof(BoxCollider2D));
                obstruction.transform.position = Vector2.Lerp(hp.transform.position, item.point, .5f);
                obstruction.GetComponent<BoxCollider2D>().size = new Vector2(.3f, .3f);
                Physics2D.SyncTransforms();
                Check(!story.CanInspect(hp, item), "solid obstacle blocks decoration interaction");
                obstruction.SetActive(false); Destroy(obstruction); Physics2D.SyncTransforms();
                obstructionChecked = true;
            }
            cooldown.SetValue(story, 0f); // Bỏ thời gian chống spam để kiểm tra mọi vật trong cùng frame.
            Check(story.TryInspectDecoration(hp, item.id), "inspect actual decoration " + level + "/" + item.id);
            Check(!story.TryInspectDecoration(hp, item.id), "inspection spam rejected");
            Check(!string.IsNullOrEmpty(story.InspectionText), "inspection has Vietnamese text");
        }
        Check(story.DecorationNotes.Count(n => n.level == level) == story.Decorations.Length, "every decoration recorded level=" + level);
        Check(enemies == gm.enemyCount && keys == gm.KeysCollected && gm.CurrentState == GameState.Playing, "inspection preserves gameplay counters");
        Vector3 lastPosition = hp.transform.position;
        cooldown.SetValue(story, 0f);
        typeof(LevelOneStory).GetField("selectedDecoration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(story, story.Decorations.Last().id);
        int beforeKey = story.InspectionVersion;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
            new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
        yield return null; yield return null;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return null;
        Check(story.InspectionVersion > beforeKey, "F key inspects selected decoration");
        hp.transform.position = original; Physics2D.SyncTransforms();
        cooldown.SetValue(story, 0f);
        Check(!story.TryInspectDecoration(hp, int.MaxValue), "unknown decoration ID rejected");
        var distant = story.Decorations.FirstOrDefault(d => Vector2.Distance(d.point, original) > 3);
        if (distant != null) Check(!story.TryInspectDecoration(hp, distant.id), "distant decoration rejected");
        int version = story.InspectionVersion;
        var guest = FindObjectsOfType<HealthController>().FirstOrDefault(h => h != hp);
        Vector3 guestOriginal = guest != null ? guest.transform.position : Vector3.zero;
        if (LanLaunch.Mode == LanMode.Host && guest != null) guest.transform.position = lastPosition;
        if (LanLaunch.Mode == LanMode.Host && level == 1 && guest != null)
        {
            var last = story.Decorations.Last();
            // Vật treo tường cho phép đoạn ray cuối 0,8m đi vào mặt tường.
            // Đặt người đọc đủ xa để vật cản thử nghiệm nằm ngoài đoạn đó.
            bool positioned = false;
            foreach (var tile in tiles.OrderBy(t => Mathf.Abs(Vector2.Distance((Vector2)t + Vector2.one * .5f, last.point) - 2f)))
            {
                var point = (Vector2)tile + Vector2.one * .5f;
                if (Vector2.Distance(point, last.point) < 1.6f ||
                    !NavMesh.SamplePosition(point, out var hit, .25f, NavMesh.AllAreas)) continue;
                guest.transform.position = hit.position;
                if (Vector2.Distance(hit.position, last.point) < 1.6f || !story.CanInspect(guest, last)) continue;
                positioned = true; break;
            }
            Check(positioned, "Client marker fixture has clear approach outside wall tolerance");
            yield return new WaitForSecondsRealtime(1.5f);
            var obstruction = new GameObject("Host-only marker obstacle", typeof(BoxCollider2D));
            obstruction.transform.position = Vector2.Lerp(guest.transform.position, last.point, .25f);
            obstruction.GetComponent<BoxCollider2D>().size = new Vector2(.3f, .3f);
            Physics2D.SyncTransforms();
            Check(!story.CanInspect(guest, last), "Host obstacle blocks Client target");
            yield return new WaitForSecondsRealtime(2f);
            obstruction.SetActive(false); Destroy(obstruction); Physics2D.SyncTransforms();
        }
        yield return null;
        var label = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Quan sát đồ vật");
        label.ForceMeshUpdate();
        Check(label.gameObject.activeInHierarchy && !label.isTextOverflowing, "inspection HUD visible and fitted");
        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "decoration-" + LanLaunch.Mode + "-" + level + ".png"));
        if (LanLaunch.Mode == LanMode.Solo)
        {
            int cueId = story.CueId;
            var seconds = typeof(LevelOneStory).GetField("cueRemaining", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float remaining = story.CueRemaining;
            typeof(LevelOneStory).GetProperty("CueId").SetValue(story, level * 100 + (int)LevelOneStory.Beat.OneMinute + 1);
            seconds.SetValue(story, 4f);
            yield return null; yield return null;
            var cue = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Lời dẫn trong trận");
            var cueCard = cue.transform.parent.GetComponent<RectTransform>();
            var inspectCard = label.transform.parent.GetComponent<RectTransform>();
            Check(cue.gameObject.activeInHierarchy && label.gameObject.activeInHierarchy &&
                cueCard.anchoredPosition.y >= inspectCard.anchoredPosition.y + inspectCard.rect.height,
                "urgent cue and inspection both visible without overlap");
            if (level == 1 && !string.IsNullOrEmpty(output))
                CaptureStory(story, System.IO.Path.Combine(output, "inspection-urgent-stack.png"));
            typeof(LevelOneStory).GetProperty("CueId").SetValue(story, cueId);
            seconds.SetValue(story, remaining);
        }
        yield return new WaitForSecondsRealtime(.6f);
        if (LanLaunch.Mode == LanMode.Host)
        {
            yield return Wait(() => story.InspectionVersion > version);
            yield return Wait(() => story.InspectionSeconds == 0);
            Check(story.DecorationNotes.Count(n => n.level == level) == story.Decorations.Length, "client reread does not duplicate notes");
            if (guest != null) guest.transform.position = guestOriginal;
            yield break;
        }
        story.DismissInspection(version - 1); Check(story.InspectionSeconds > 0, "stale inspection skip rejected");
        story.DismissInspection(version); Check(story.InspectionSeconds == 0, "inspection skip accepted");
    }

    private IEnumerator VerifyClientDecorationInteraction(GameManager gm, LevelOneStory story)
    {
        yield return Wait(() => story.Decorations.Length > 0 &&
            story.DecorationNotes.Count(n => n.level == gm.CurrentLevel) == story.Decorations.Length && story.InspectionSeconds > 0);
        var last = story.Decorations[story.Decorations.Length - 1];
        yield return Wait(() => Vector2.Distance(gm.PlayerMovement.transform.position, last.point) <= DecorationInspection.Range(last));
        if (gm.CurrentLevel == 1)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(LevelOneStory);
            var allowed = (System.Collections.Generic.HashSet<int>)type.GetField("remoteInspectableIds", flags).GetValue(story);
            yield return Wait(() => allowed.Contains(last.id));
            yield return Wait(() => !allowed.Contains(last.id));
            var selected = type.GetField("selectedDecoration", flags);
            var update = type.GetMethod("UpdateDecorationPrompt", flags);
            selected.SetValue(story, last.id);
            update.Invoke(story, new object[] { false, true });
            Check((int)selected.GetValue(story) != last.id, "Client hides blocked target F using Host visibility");
            yield return Wait(() => allowed.Contains(last.id));
            selected.SetValue(story, last.id);
            update.Invoke(story, new object[] { false, true });
            Check((int)selected.GetValue(story) == last.id, "Client restores F when Host path clears");
        }
        yield return new WaitForSecondsRealtime(.5f);
        int version = story.InspectionVersion;
        LanSessionBridge.Active.RequestDecorationInspect(last.id);
        yield return Wait(() => story.InspectionVersion > version);
        yield return null;
        var label = story.GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.name == "Quan sát đồ vật");
        Check(label.gameObject.activeInHierarchy && label.text.Contains(DecorationInspection.Title(last.kind)), "client requested inspection visible");
        if (!string.IsNullOrEmpty(output)) CaptureStory(story, System.IO.Path.Combine(output, "decoration-Client-" + gm.CurrentLevel + ".png"));
        LanSessionBridge.Active.RequestDecorationDismiss(story.InspectionVersion);
        yield return Wait(() => story.InspectionSeconds == 0);
    }
}
#endif
