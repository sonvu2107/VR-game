#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Kiểm thử opt-in bản Development; không có trong Release.</summary>
public sealed partial class StorySmokeProbe : MonoBehaviour
{
    private float deadline;
    private bool failed;
    private bool reconnect;
    private string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if ((Array.IndexOf(Environment.GetCommandLineArgs(), "-roomStorySmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-storySmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-narrativeSmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-campaignStorySmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-stoneSmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-stoneCollisionSmoke") < 0 &&
            Array.IndexOf(Environment.GetCommandLineArgs(), "-decorCollisionSmoke") < 0) || FindObjectOfType<StorySmokeProbe>() != null) return;
        var go = new GameObject("Story smoke probe");
        DontDestroyOnLoad(go);
        go.AddComponent<StorySmokeProbe>();
    }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        deadline = Time.realtimeSinceStartup + 180f;
        output = Environment.GetEnvironmentVariable("LABYRINTH_STORY_ARTIFACTS");
        reconnect = Array.IndexOf(Environment.GetCommandLineArgs(), "-storyReconnect") >= 0;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-roomStorySmoke") >= 0) { deadline += 180f; yield return RoomJourney(); yield break; }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-narrativeSmoke") < 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-campaignStorySmoke") < 0) StartCoroutine(SkipOpeningForRegression());
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-narrativeSmoke") >= 0) { deadline += 420f; yield return Narrative(); yield break; }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-decorCollisionSmoke") >= 0) { deadline += 300f; yield return DecorCollision(); yield break; }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-stoneCollisionSmoke") >= 0) { yield return StoneCollision(); yield break; }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-stoneSmoke") >= 0) { yield return Stone(); yield break; }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-campaignStorySmoke") >= 0)
        {
            deadline = Time.realtimeSinceStartup + 420f;
            yield return Campaign();
            yield break;
        }
        yield return Stone();
    }

    private IEnumerator SkipOpeningForRegression()
    {
        while (true)
        {
            var gm = FindObjectOfType<GameManager>();
            if (gm != null && gm.Story.Opening && gm.CurrentState == GameState.Story)
                gm.Story.RequestAdvance(true);
            yield return null;
        }
    }

    private IEnumerator DecorCollision()
    {
        bool client = LanLaunch.Mode == LanMode.Client;
        if (LanLaunch.Mode == LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu") SceneManager.LoadScene("Dungeon");
        yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState != GameState.Generating && !FindObjectOfType<GameManager>().Story.Opening);
        var gm = FindObjectOfType<GameManager>();
        for (int level = 1; level <= 10; level++)
        {
            int current = level;
            yield return Wait(() => gm.CurrentLevel == current && (gm.CurrentState == GameState.Story || gm.CurrentState == GameState.Playing));
            if (LanSessionBridge.Active != null) yield return Wait(() => LanSessionBridge.Active.IsReady);
            yield return new WaitForSecondsRealtime(.25f);
            if (!client)
            {
                var layout = (DungeonLayout)typeof(InfiniteWorldGenerator).GetField("dungeonLayout",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(gm.dungeonGenerator);
                var blockers = FindObjectsByType<DecorationCollision>(FindObjectsSortMode.None);
                Check(blockers.Length > 0, "solid decorations present level " + level);
                Check(DecorationCollision.IsConnected(layout.FloorTiles, DecorationCollision.BlockedCells(layout)), "connected floor after decoration level " + level);
                Check(UnityEngine.AI.NavMesh.SamplePosition(gm.PlayerMovement.transform.position, out var spawnHit, 2f, UnityEngine.AI.NavMesh.AllAreas), "player has navigable spawn level " + level);
                var exit = FindObjectOfType<FloorExit>();
                Check(UnityEngine.AI.NavMesh.SamplePosition(exit.transform.position, out var exitHit, 2f, UnityEngine.AI.NavMesh.AllAreas), "exit has navigable approach level " + level);
                var route = new UnityEngine.AI.NavMeshPath();
                Check(UnityEngine.AI.NavMesh.CalculatePath(spawnHit.position, exitHit.position, UnityEngine.AI.NavMesh.AllAreas, route) &&
                    route.status == UnityEngine.AI.NavMeshPathStatus.PathComplete, "navmesh route from spawn to exit level " + level);
                foreach (var blocker in blockers)
                {
                    var col = blocker.GetComponent<BoxCollider2D>();
                    Check(col != null && col.enabled && !col.isTrigger, "solid " + blocker.name);
                    Check(blocker.GetComponent<NavMeshPlus.Components.NavMeshModifierVolume>() != null, "navigation excludes " + blocker.name);
                    Check(!UnityEngine.AI.NavMesh.SamplePosition(col.bounds.center, out var hit, .03f, UnityEngine.AI.NavMesh.AllAreas), "no navmesh inside " + blocker.name);
                    bool swept = false;
                    foreach (var dir in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                    {
                        var hits = Physics2D.BoxCastAll((Vector2)col.bounds.center - dir * 2f, Vector2.one * .25f, 0f, dir, 4f);
                        foreach (var contact in hits) if (contact.collider == col) swept = true;
                    }
                    Check(swept, "movement sweep hits " + blocker.name);
                }
                foreach (var sprite in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (sprite.name == "Crates" || sprite.name == "Atlas Border Prop" || sprite.name == "Atlas Supply Cluster")
                        Check(sprite.GetComponentInChildren<DecorationCollision>() != null, "visible prop has footprint");
                Debug.Log("DECOR_LEVEL_PASS level=" + level + " footprints=" + blockers.Length);
                if (gm.CurrentState == GameState.Story) gm.Story.RequestAdvance(true);
                yield return Wait(() => gm.CurrentState == GameState.Playing);
                yield return new WaitForSecondsRealtime(.7f);
                gm.SetEnemyCount(0);
                while (gm.KeysCollected < gm.KeysRequired) gm.CollectKey();
                gm.TryAdvanceLevel();
            }
            else
            {
                Check(FindObjectsByType<DecorationCollision>(FindObjectsSortMode.None).Length == 0, "Client receives visuals without duplicate physics level " + level);
                Debug.Log("DECOR_CLIENT_LEVEL_PASS level=" + level);
                yield return Wait(() => gm.CurrentLevel > current || gm.CurrentState == GameState.Victory);
            }
        }
        if (!client && gm.Story.Ending) gm.Story.RequestAdvance(true);
        yield return Wait(() => gm.CurrentState == GameState.Victory);
        if (!client && LanSessionBridge.Active != null) yield return new WaitForSecondsRealtime(1.5f);
        Pass("DECOR_COLLISION " + (client ? "Client" : LanLaunch.Mode.ToString()) + " levels=1-10 solid sweeps navigation connectivity progression");
    }

    private IEnumerator StoneCollision()
    {
        bool client = LanLaunch.Mode == LanMode.Client;
        if (LanLaunch.Mode == LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu") SceneManager.LoadScene("Dungeon");
        yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState == GameState.Playing);
        var gm = FindObjectOfType<GameManager>();
        if (LanSessionBridge.Active != null) yield return Wait(() => LanSessionBridge.Active.IsReady);
        yield return null;
        var stone = gm.Story.StoneCollider;
        if (client)
        {
            Check(stone == null, "Client uses Host collision authority");
            yield return Wait(() => gm.CurrentState == GameState.Story);
            Check(gm.Story.StoneRead, "F remains available after Host collision checks");
            yield return Wait(() => gm.CurrentLevel == 2);
            Pass("STONE_COLLISION Client snapshot and transition");
            yield break;
        }
        Check(stone != null && stone.enabled && !stone.isTrigger, "solid stone footprint exists");
        Physics2D.SyncTransforms();
        Check(Mathf.Abs(stone.bounds.size.x - 1f) < 0.01f && Mathf.Abs(stone.bounds.size.y - 0.38f) < 0.01f,
            "footprint respects scaled canvas");
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            var body = Array.Find(player.GetComponents<Collider2D>(), c => !c.isTrigger);
            if (rb == null || body == null) continue;
            bool enabledBefore = player.enabled;
            Vector2 original = rb.position;
            player.enabled = false;
            Physics2D.SyncTransforms();
            Vector2 offset = (Vector2)body.bounds.center - rb.position;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                foreach (bool dash in new[] { false, true })
                {
                    Vector2 center = stone.bounds.center;
                    rb.position = center - direction * 1.2f - offset;
                    rb.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                    float startDistance = Vector2.Dot((Vector2)body.bounds.center - center, direction);
                    for (int tick = 0; tick < 22; tick++)
                    {
                        if (dash) rb.MovePosition(rb.position + direction * (player.dashDistance / player.dashDuration) * Time.fixedDeltaTime);
                        else rb.linearVelocity = direction * player.speed;
                        yield return new WaitForFixedUpdate();
                    }
                    rb.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                    float distance = Vector2.Dot((Vector2)body.bounds.center - center, direction);
                    Check(distance > startDistance + 0.05f && distance < -0.1f,
                        "body stops at stone " + player.name + " direction=" + direction + " dash=" + dash);
                    Check(body.IsTouching(stone), "contact is with stone, not nearby wall");
                }
            }
            rb.position = original;
            rb.linearVelocity = Vector2.zero;
            player.enabled = enabledBefore;
        }
        Physics2D.SyncTransforms();
        Check(gm.Story.TryReadStone(gm.PlayerMovement.GetComponent<HealthController>()), "reading still reachable outside footprint");
        yield return new WaitForSecondsRealtime(0.7f);
        gm.Story.RequestAdvance(true);
        yield return Wait(() => gm.CurrentState == GameState.Playing);
        Check(stone.enabled && stone.gameObject.activeInHierarchy, "skip keeps physical stone");
        gm.SetEnemyCount(0); gm.TryAdvanceLevel();
        yield return Wait(() => gm.CurrentLevel == 2 && gm.CurrentState == GameState.Playing);
        yield return null;
        Check(!stone.enabled, "floor 2 has no leftover stone collider");
        yield return new WaitForSecondsRealtime(0.6f);
        Pass("STONE_COLLISION " + LanLaunch.Mode + " walk dash four sides all players skip transition");
    }

    private IEnumerator Stone()
    {
        bool client = LanLaunch.Mode == LanMode.Client;
        if (client && reconnect)
        {
            yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState == GameState.Story);
            var restored = FindObjectOfType<GameManager>();
            yield return Wait(() => LanSessionBridge.Active.IsReady);
            Check(restored.Story.StoneRead && restored.Story.LineIndex == 1, "rejoin restores stone and current line");
            restored.Story.RequestAdvance(true);
            yield return Wait(() => restored.CurrentState == GameState.Playing);
            yield return Wait(() => restored.CurrentLevel == 2);
            Pass("STONE Client rejoin restores cursor and skip");
            yield break;
        }
        if (LanLaunch.Mode == LanMode.Solo && SceneManager.GetActiveScene().name == "StartMenu") SceneManager.LoadScene("Dungeon");
        yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState == GameState.Playing);
        var gm = FindObjectOfType<GameManager>();
        var story = gm.Story;
        var session = LanSessionBridge.Active;
        if (session != null) yield return Wait(() => session.IsReady);
        Check(!story.StoneRead && !story.IsVisible && Time.timeScale == 1, "floor 1 starts playable without intro");
        var stoneSprite = Resources.Load<Sprite>("Story/OathStone");
        Check(stoneSprite != null && stoneSprite.texture.filterMode == FilterMode.Point, "stone sprite imported with point filtering");
        if (!client)
        {
            var hp = gm.PlayerMovement.GetComponent<HealthController>();
            Vector3 start = hp.transform.position;
            hp.transform.position = story.StonePosition + Vector3.right * 10;
            Check(!story.TryReadStone(hp), "reject distant reader");
            hp.transform.position = start;
            gm.TryAdvanceLevel();
            Check(gm.CurrentLevel == 1 && gm.CurrentState == GameState.Playing, "enemies still block exit");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            CaptureStory(story, Path.Combine(output, "stone-explore-" + LanLaunch.Mode + ".png"));
        }
        if (client || session == null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }
        yield return Wait(() => gm.CurrentState == GameState.Story);
        yield return new WaitForSecondsRealtime(0.3f);
        Check(story.StoneRead && story.IsVisible && Time.timeScale == 0, "F opens shared stone dialogue");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            CaptureStory(story, Path.Combine(output, "stone-" + LanLaunch.Mode + ".png"));
        }
        foreach (var label in story.GetComponentsInChildren<TMP_Text>())
        {
            Check(label.font.HasCharacters(label.text, out uint[] missing, true, true), "stone Vietnamese glyphs");
            label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "stone text fits");
        }
        int hpBefore = gm.PlayerMovement.GetComponent<HealthController>().currentHealth;
        if (!client)
        {
            Check(!story.TryReadStone(gm.PlayerMovement.GetComponent<HealthController>()), "duplicate stone request rejected");
            gm.PlayerMovement.GetComponent<HealthController>().TakeDamage(1);
            Check(gm.PlayerMovement.GetComponent<HealthController>().currentHealth == hpBefore, "reading protects player");
            story.AdvanceAuthoritative(0, false);
            story.AdvanceAuthoritative(0, true);
            Check(story.LineIndex == 1, "stale advance rejected");
        }
        yield return Wait(() => story.LineIndex == 1);
        if (session != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-stoneDisconnect") >= 0)
        {
            if (client) { Debug.Log("STONE_CLIENT_DISCONNECT"); Application.Quit(0); yield break; }
            yield return Wait(() => !session.IsReady);
            story.AdvanceAuthoritative(1, true);
            Check(story.LineIndex == 1 && gm.CurrentState == GameState.Story, "disconnect cannot skip story");
            Debug.Log("STONE_HOST_REJOIN_READY");
            yield return Wait(() => session.IsReady);
        }
        yield return new WaitForSecondsRealtime(0.3f);
        if (client || session == null) story.RequestAdvance(true);
        yield return Wait(() => gm.CurrentState == GameState.Playing);
        Check(story.IsComplete && Time.timeScale == 1, "skip resumes gameplay on both peers");
        Check(!string.IsNullOrEmpty(story.DialogueArchive[0]), "skipped stone retained in journal");
        if (!client)
        {
            // Fixture chỉ dọn điều kiện cũ để chứng minh bia đá không là điều kiện qua tầng.
            gm.SetEnemyCount(0);
            gm.TryAdvanceLevel();
        }
        yield return Wait(() => gm.CurrentLevel == 2 && gm.CurrentState == GameState.Playing);
        Check(story.StoryLevel == 2 && story.LineIndex == 0, "floor 2 chapter unchanged");
        if (session == null)
        {
            gm.RestartGame();
            yield return Wait(() => FindObjectOfType<GameManager>() != null && FindObjectOfType<GameManager>().CurrentState == GameState.Playing);
            gm = FindObjectOfType<GameManager>();
            Check(!gm.Story.StoneRead, "restart resets stone");
            gm.SetEnemyCount(0); gm.TryAdvanceLevel();
            yield return Wait(() => gm.CurrentLevel == 2);
            Check(!gm.Story.StoneRead, "exit works without reading stone");
        }
        else yield return new WaitForSecondsRealtime(1f);
        Pass("STONE " + LanLaunch.Mode + " F proximity skip sync optional exit restart");
    }

    private IEnumerator Campaign() => Narrative();

    private IEnumerator Wait(Func<bool> condition)
    {
        while (!failed && !condition())
        {
            if (Time.realtimeSinceStartup > deadline) { Check(false, "timeout"); yield break; }
            yield return null;
        }
    }
    private void CaptureStory(LevelOneStory story, string path)
    {
        // Batchmode không có backbuffer trình bày; render camera vào texture riêng.
        var canvases = story.GetComponentsInChildren<Canvas>()
            .Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay)
            .Select(c => (canvas: c, mode: c.renderMode, camera: c.worldCamera, distance: c.planeDistance, order: c.sortingOrder)).ToArray();
        foreach (var label in story.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Camera camera = Camera.main;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        // Ảnh này kiểm tra UI, không kiểm tra ánh sáng map. Batchmode không khởi tạo
        // cache sorting-layer của URP qua frame render thông thường.
        var lights = FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
        var lightStates = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++) { lightStates[i] = lights[i].enabled; lights[i].enabled = false; }
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            foreach (var saved in canvases)
            {
                saved.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                saved.canvas.worldCamera = camera;
                saved.canvas.planeDistance = camera.nearClipPlane + 1f;
                // Giữ đúng nghĩa Overlay khi chụp: đồ vật có depth sort không đè lên chữ.
                saved.canvas.sortingOrder = 30000 + saved.order;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            bool hasContent = false;
            foreach (Color32 pixel in pixels.GetPixels32())
                if (pixel.r > 80 || pixel.g > 80 || pixel.b > 80) { hasContent = true; break; }
            Check(hasContent, "UI capture contains rendered content");
        }
        finally
        {
            camera.targetTexture = previousTarget;
            foreach (var saved in canvases)
            {
                saved.canvas.renderMode = saved.mode; saved.canvas.worldCamera = saved.camera;
                saved.canvas.planeDistance = saved.distance; saved.canvas.sortingOrder = saved.order;
            }
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) Destroy(pixels);
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = lightStates[i];
            Canvas.ForceUpdateCanvases();
        }
    }
    private void Check(bool condition, string name)
    {
        if (condition) { Debug.Log("STORY_CHECK " + name); return; }
        failed = true; Debug.LogError("STORY_SMOKE_FAIL " + name); Application.Quit(1);
    }
    private void Pass(string checks)
    {
        if (failed) return;
        Debug.Log("STORY_SMOKE_PASS " + checks); Application.Quit(0);
    }
}
#endif
