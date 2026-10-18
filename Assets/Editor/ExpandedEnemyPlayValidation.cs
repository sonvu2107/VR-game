using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Probe có sở hữu fixture riêng, không sửa prefab/config hay scene trên đĩa.
[InitializeOnLoad]
public static class ExpandedEnemyPlayValidation
{
    private const string Active = "ExpandedEnemyValidation.Active";
    private const string Background = "ExpandedEnemyValidation.Background";
    private static GameObject[] mobs;
    private static SpriteRenderer[] visuals;
    private static HashSet<int>[] seen;
    private static Vector3[] scales;
    private static int phase;
    private static double start, phaseStart;
    private static GameManager manager;
    private static HealthController target;
    private static EnemyProjectile projectile;
    static ExpandedEnemyPlayValidation()
    {
        if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool(Background, Application.runInBackground);
        SessionState.SetBool(Active, true);
        phase = 0; start = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/DungeonArcaneCitadel.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            double now = EditorApplication.timeSinceStartup;
            if (start == 0) start = now;
            Require(now - start < 90, "Expanded mob validation timed out.");
            if (phase == 0)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                foreach (var brain in UnityEngine.Object.FindObjectsOfType<EnemyBrain>()) brain.enabled = false;
                foreach (var combat in UnityEngine.Object.FindObjectsOfType<EnemyCombat>()) combat.enabled = false;
                foreach (var enemy in UnityEngine.Object.FindObjectsOfType<Enemy>()) enemy.enabled = false;
                foreach (var mover in UnityEngine.Object.FindObjectsOfType<EnemyMovement>()) mover.StopImmediately();
                int count = ExpandedEnemyIntegration.Names.Length;
                mobs = new GameObject[count]; visuals = new SpriteRenderer[count]; scales = new Vector3[count]; seen = new HashSet<int>[count];
                for (int i = 0; i < count; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkullVampireIntegration.PrefabFolder + ExpandedEnemyIntegration.Names[i] + ".prefab");
                    Vector3 desired = manager.PlayerMovement.transform.position + Vector3.right * (3 + i);
                    Require(NavMesh.SamplePosition(desired, out NavMeshHit hit, 8f, NavMesh.AllAreas), "Missing test NavMesh.");
                    mobs[i] = UnityEngine.Object.Instantiate(prefab, hit.position, Quaternion.identity);
                    mobs[i].GetComponent<EnemyHealth>().SetCountsForLegacyCounter(false);
                    mobs[i].GetComponent<EnemyBrain>().enabled = false;
                    mobs[i].GetComponent<EnemyCombat>().enabled = false;
                    visuals[i] = mobs[i].GetComponent<EnemySheetAnimation>()?.Renderer ?? mobs[i].GetComponent<EnemySpriteAnimation>().Renderer;
                    scales[i] = visuals[i].transform.localScale; seen[i] = new HashSet<int>();
                    Require(Vector3.Distance(scales[i], Vector3.one * ExpandedEnemyIntegration.VisualScale(ExpandedEnemyIntegration.Names[i])) < 0.001f, "Goblin scale differs from approved size.");
                }
                phase = 1; phaseStart = now;
                return;
            }
            if (phase == 1)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    var sheet = mobs[i].GetComponent<EnemySheetAnimation>();
                    int frame = sheet != null ? sheet.CurrentFrame : mobs[i].GetComponent<EnemySpriteAnimation>().CurrentFrame;
                    seen[i].Add(frame);
                    Require(visuals[i].sprite != null && visuals[i].enabled, "Invisible mob: " + mobs[i].name);
                    Require(visuals[i].transform.localScale == scales[i], "Frame rescaled mob.");
                    Require(mobs[i].GetComponent<EnemyLocomotionVisual>() == null, "Duplicate visual writer.");
                }
                if (now - phaseStart < 2) return;
                for (int i = 0; i < mobs.Length; i++) Require(seen[i].Count >= mobs[i].GetComponent<EnemySheetAnimation>().Art.idle.count, "Idle frames missing: " + mobs[i].name);
                CheckLan();
                CheckBlast();
                CheckBossVariants();
                foreach (var mob in mobs)
                {
                    var sheet = mob.GetComponent<EnemySheetAnimation>();
                    if (sheet != null) sheet.TriggerAttack(Vector2.right, 0.55f);
                }
                manager.Pause();
                phase = 2; phaseStart = now;
            }
            else if (phase == 2)
            {
                Require(Time.timeScale == 0f, "Pause failed.");
                if (now - phaseStart < 0.5) return;
                manager.Resume(); phase = 3; phaseStart = now;
            }
            else if (phase == 3)
            {
                if (now - phaseStart < 0.2) return;
                for (int i = 0; i < mobs.Length; i++)
                {
                    var sheet = mobs[i].GetComponent<EnemySheetAnimation>();
                    if (sheet != null) Require(sheet.Renderer.sprite.rect.y == sheet.Art.Frame(sheet.Variant, sheet.Art.attack, 0).rect.y, "Attack row not playing.");
                }
                target = MakeTarget(new Vector3(10000, 10000));
                var barrel = mobs[Array.IndexOf(ExpandedEnemyIntegration.Names, "GoblinBarrel")];
                barrel.GetComponent<NavMeshAgent>().enabled = false;
                barrel.transform.position = target.transform.position + Vector3.left * 0.7f;
                barrel.GetComponent<EnemyCombat>().enabled = true;
                Physics2D.SyncTransforms();
                Require(barrel.GetComponent<EnemyCombat>().TryAttack(target), "Barrel attack rejected.");
                phase = 4; phaseStart = now;
            }
            else if (phase == 4)
            {
                if (now - phaseStart < 1) return;
                Require(target.currentHealth == 9, "Barrel explosion damage incorrect.");
                var barrel = mobs[Array.IndexOf(ExpandedEnemyIntegration.Names, "GoblinBarrel")];
                Require(barrel == null || barrel.GetComponent<EnemyHealth>().IsDead, "Barrel did not self destruct.");
                target.RestoreFullHealth();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyBoss/Projectiles/Dynamite.prefab");
                projectile = UnityEngine.Object.Instantiate(prefab, target.transform.position + Vector3.left * 0.8f, Quaternion.identity).GetComponent<EnemyProjectile>();
                projectile.Launch(Vector2.up, 1, 0.1f, 0.2f, LayerMask.GetMask("Player"), 0, null);
                projectile.SetBlastRadius(1.4f);
                phase = 5; phaseStart = now;
            }
            else if (phase == 5)
            {
                if (now - phaseStart < 0.6) return;
                Require(projectile == null && target.currentHealth == 9, "Dynamite expiry/AoE failed.");
                foreach (var mob in mobs) if (mob != null) mob.GetComponent<EnemyHealth>().Die();
                phase = 6; phaseStart = now;
            }
            else if (phase == 6 && now - phaseStart > 1.2)
            {
                Require(mobs.All(mob => mob == null), "Dead mob not despawned.");
                Finish(null);
            }
        }
        catch (Exception exception) { Finish(exception); }
    }
    private static HealthController MakeTarget(Vector3 position)
    {
        var root = new GameObject("Expanded enemy test target"); root.layer = LayerMask.NameToLayer("Player"); root.transform.position = position;
        var health = root.AddComponent<HealthController>(); health.enabled = false; health.RestoreFullHealth();
        root.AddComponent<CircleCollider2D>().radius = 0.15f;
        root.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
        return health;
    }
    private static void CheckBlast()
    {
        var health = MakeTarget(new Vector3(10000, 10000));
        var wall = new GameObject("Blast LOS fixture"); wall.transform.position = health.transform.position + Vector3.left * 0.5f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 2);
        LanMode old = LanLaunch.Mode;
        string address = LanLaunch.Address, error = LanLaunch.LastError; int port = LanLaunch.Port;
        try
        {
            Vector3 center = health.transform.position + Vector3.left;
            Physics2D.SyncTransforms();
            Require(EnemyBlast.Apply(center, 1.8f, 1, LayerMask.GetMask("Player"), 1) == 0 && health.currentHealth == 10, "Blast went through wall.");
            wall.SetActive(false); Physics2D.SyncTransforms();
            LanLaunch.Configure(LanMode.Client);
            Require(EnemyBlast.Apply(center, 1.8f, 1, LayerMask.GetMask("Player"), 0) == 0, "Client applied blast.");
            LanLaunch.Configure(old, address, port);
            Require(EnemyBlast.Apply(center, 1.8f, 1, LayerMask.GetMask("Player"), 0) == 1 && health.currentHealth == 9, "Blast duplicated colliders or missed target.");
            Require(EnemyBlast.Apply(center, 1.8f, 1, LayerMask.GetMask("Player"), 0) == 0, "Blast ignored player invulnerability.");
            Debug.Log("EXPANDED_BLAST_PASS wall=true clientRejected=true dedup=true invulnerability=true");
        }
        finally { LanLaunch.Configure(old, address, port); LanLaunch.LastError = error; UnityEngine.Object.Destroy(health.gameObject); UnityEngine.Object.Destroy(wall); }
    }
    private static void CheckLan()
    {
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog"); catalog.Initialize();
        var root = new GameObject("Expanded LAN test"); root.SetActive(false);
        var sender = new LanWorldReplication(catalog, root.transform); var receiver = new LanWorldReplication(catalog, root.transform);
        Sprite[] originals = visuals.Select(v => v.sprite).ToArray();
        try
        {
            for (int variant = 0; variant < 4; variant++)
            for (int frame = 0; frame < 7; frame++)
            {
                foreach (var mob in mobs)
                {
                    var sheet = mob.GetComponent<EnemySheetAnimation>();
                    if (sheet != null) sheet.Renderer.sprite = sheet.Art.Frame(variant, sheet.Art.attack, frame);
                }
                var message = new LanMessage(); sender.CaptureVisuals(message);
                receiver.ApplyVisuals(JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(message)));
                var views = (Dictionary<int, SpriteRenderer>)typeof(LanWorldReplication).GetField("clientViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(receiver);
                foreach (var visual in visuals)
                    Require(views.TryGetValue(visual.GetInstanceID(), out var view) && view.sprite != null && view.sprite.texture == visual.sprite.texture && view.sprite.rect == visual.sprite.rect && view.sprite.pivot == visual.sprite.pivot, "LAN sheet frame mismatch.");
            }
            Debug.Log("EXPANDED_LAN_VISUAL_PASS types=3 colors=4 serializedRoundTrip=true");
        }
        finally { for (int i = 0; i < visuals.Length; i++) visuals[i].sprite = originals[i]; sender.Dispose(); receiver.Dispose(); UnityEngine.Object.Destroy(root); }
    }

    private static void CheckBossVariants()
    {
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        var room = generator.GeneratedRooms.First(item => item.Type == RoomType.Boss);
        var candidates = generator.GetFinalBossCandidates();
        Require(candidates.Count >= 11, "Final boss pool lost active enemy species.");
        foreach (var prefab in candidates)
        {
            Require(prefab.name != "SkeletonScout" && prefab.name != "SkeletonGuard", "Removed mob entered boss pool.");
            var originalConfig = prefab.GetComponent<EnemyHealth>()?.Config;
            int originalHP = originalConfig != null ? originalConfig.maxHealth : 0;
            bool originalSuicide = originalConfig != null && originalConfig.selfDestructOnAttack;
            var boss = CampaignFinalBoss.Create(prefab, room, PrefabPlacer.GetWorldPosition(room.SpawnPoint));
            var health = boss.GetComponent<EnemyHealth>();
            health.SetCountsForLegacyCounter(false);
            Require(health.MaxHealth >= 600 && health.CurrentHealth == health.MaxHealth, "Boss did not start with full upgraded health.");
            Require(!health.Config.selfDestructOnAttack, "Boss can self destruct.");
            Require(boss.SizeMultiplier >= 1f && boss.SizeMultiplier <= 4f, "Boss size out of safe range.");
            var sheet = boss.GetComponent<EnemySheetAnimation>();
            var visual = sheet != null ? sheet.Renderer : boss.GetComponent<EnemySpriteAnimation>()?.Renderer ?? boss.GetComponentInChildren<SpriteRenderer>();
            var bounds = visual.bounds;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                Require(room.FloorTiles.Contains(new Vector2Int(Mathf.RoundToInt(bounds.center.x + x * bounds.extents.x), Mathf.RoundToInt(bounds.center.y + y * bounds.extents.y - 1))), "Boss artwork outside room: " + prefab.name);
            var brain = boss.GetComponent<EnemyBrain>(); if (brain != null) brain.enabled = false;
            var king = boss.GetComponent<SkeletonKingController>(); if (king != null) king.enabled = false;
            var mini = boss.GetComponent<MiniBossController>(); if (mini != null) mini.enabled = false;
            boss.GetComponent<EnemyMovement>().StopImmediately();
            boss.GetComponent<EnemyCombat>().enabled = false;
            int deaths = 0; health.Died += _ => deaths++;
            health.TakeDamage(1);
            Require(health.CurrentHealth == health.MaxHealth - 1, "Boss does not receive damage.");
            health.Die(); health.Die();
            Require(deaths == 1 && health.IsDead, "Boss death was duplicated.");
            Require(originalConfig == null || originalConfig.maxHealth == originalHP && originalConfig.selfDestructOnAttack == originalSuicide, "Prefab config unexpectedly changed.");
        }
        Debug.Log($"FINAL_BOSS_VARIANTS_PLAY_PASS candidates={candidates.Count} fullHP=true damage=true deathOnce=true bounds=true");
    }
    private static void Finish(Exception exception)
    {
        SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
        Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Background, false);
        if (exception != null) Debug.LogException(exception);
        Debug.Log(exception == null ? "EXPANDED_ENEMIES_PLAY_PASS animation=3 scaled=true pause=true barrel=true dynamite=true death=true" : "EXPANDED_ENEMIES_PLAY_FAIL");
        EditorApplication.ExitPlaymode();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
