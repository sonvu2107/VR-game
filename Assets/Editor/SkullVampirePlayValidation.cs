using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Probe Play Mode: frame loop, kích thước, pause, hurt/death và LAN visual round-trip.</summary>
[InitializeOnLoad]
public static class SkullVampirePlayValidation
{
    private const string ActiveKey = "SkullVampirePlayValidation.Active";
    private const string BackgroundKey = "SkullVampirePlayValidation.Background";
    private static readonly HashSet<int>[] seen = { new(), new() };
    private static EnemySpriteAnimation[] samples;
    private static Vector3[] initialScale;
    private static Vector2[] initialCollider;
    private static int[] pausedFrames;
    private static GameManager manager;
    private static int phase;
    private static double started;
    private static double phaseStarted;

    static SkullVampirePlayValidation()
    {
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode is already active.");
        SessionState.SetBool(BackgroundKey, Application.runInBackground);
        SessionState.SetBool(ActiveKey, true);
        phase = 0;
        started = 0d;
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
            if (started == 0d) started = now;
            if (now - started > 120d) throw new InvalidOperationException("Play validation timed out.");
            if (phase == 0)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                foreach (var brain in UnityEngine.Object.FindObjectsOfType<EnemyBrain>()) brain.enabled = false;
                foreach (var combat in UnityEngine.Object.FindObjectsOfType<EnemyCombat>()) combat.enabled = false;
                foreach (var enemy in UnityEngine.Object.FindObjectsOfType<EnemyMovement>()) enemy.Stop();
                samples = new EnemySpriteAnimation[2];
                initialScale = new Vector3[2];
                initialCollider = new Vector2[2];
                for (int i = 0; i < samples.Length; i++)
                {
                    string name = i == 0 ? "Skull" : "Vampire";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkullVampireIntegration.PrefabFolder + name + ".prefab");
                    Vector3 desired = manager.PlayerMovement.transform.position + Vector3.right * (2f + i * 2f);
                    if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                        throw new InvalidOperationException("No NavMesh position for " + name);
                    GameObject root = UnityEngine.Object.Instantiate(prefab, hit.position, Quaternion.identity);
                    root.GetComponent<EnemyBrain>().enabled = false;
                    root.GetComponent<EnemyCombat>().enabled = false;
                    root.GetComponent<EnemyHealth>().SetCountsForLegacyCounter(false);
                    samples[i] = root.GetComponent<EnemySpriteAnimation>();
                    initialScale[i] = samples[i].Renderer.transform.localScale;
                    initialCollider[i] = root.GetComponent<BoxCollider2D>().size;
                    seen[i].Clear();
                }
                phase = 1;
                phaseStarted = now;
                foreach (var sample in samples)
                {
                    var mover = sample.GetComponent<EnemyMovement>();
                    Vector3 destination = sample.transform.position + Vector3.right * 3f;
                    Require(mover.TrySetNearbyDestination(destination, 2f,
                        sample.GetComponent<EnemyHealth>().Config.chaseSpeed, 0.1f), "Mob cannot navigate on map.");
                }
                return;
            }
            if (phase == 1)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    seen[i].Add(samples[i].CurrentFrame);
                    Require(samples[i].Renderer.sprite != null && samples[i].Renderer.enabled,
                        "Missing visible animation frame.");
                    Require(samples[i].Renderer.transform.localScale == initialScale[i] &&
                        samples[i].GetComponent<BoxCollider2D>().size == initialCollider[i],
                        "Animation changes visual/collider dimensions.");
                    Require(samples[i].GetComponent<EnemyLocomotionVisual>() == null,
                        "Two systems are writing the mob visual.");
                }
                if (now - phaseStarted < 2d || seen.Any(frames => frames.Count != 4)) return;
                CheckLanRoundTrip();
                manager.Pause();
                Require(manager.CurrentState == GameState.Paused, "Pause failed.");
                pausedFrames = samples.Select(sample => sample.CurrentFrame).ToArray();
                phase = 2;
                phaseStarted = now;
                Debug.Log("SKULL_VAMPIRE_ANIMATION_PASS frames=0,1,2,3 loop=true fixedScale=true fixedCollider=true");
            }
            else if (phase == 2)
            {
                Require(samples.Select((sample, i) => sample.CurrentFrame == pausedFrames[i]).All(value => value),
                    "Mob animation keeps advancing while paused.");
                if (now - phaseStarted < 0.6d) return;
                manager.Resume();
                Require(manager.CurrentState == GameState.Playing, "Resume failed.");
                phase = 3;
                phaseStarted = now;
            }
            else if (phase == 3)
            {
                if (now - phaseStarted < 0.18d) return;
                if (samples.Any(sample =>
                    sample.CurrentFrame == pausedFrames[Array.IndexOf(samples, sample)])) return;
                foreach (var sample in samples) sample.Renderer.color = new Color(1f, 0.35f, 0.22f);
                foreach (var sample in samples) sample.TriggerAttackLunge(Vector2.right);
                phase = 31;
                phaseStarted = now;
            }
            else if (phase == 31)
            {
                if (now - phaseStarted < 0.2d) return;
                foreach (var sample in samples)
                {
                    Require(sample.CurrentPose == EnemySpriteAnimation.Pose.Attacking,
                        "Attack pose did not activate.");
                    Require(sample.Renderer.color == new Color(1f, 0.35f, 0.22f),
                        "Frame animation overwrote charge telegraph color.");
                    sample.Renderer.color = Color.white;
                }
                phase = 32;
                phaseStarted = now;
            }
            else if (phase == 32)
            {
                if (now - phaseStarted < 0.65d) return;
                foreach (var sample in samples)
                {
                    EnemyHealth health = sample.GetComponent<EnemyHealth>();
                    int before = health.CurrentHealth;
                    health.TakeDamage(1);
                    Require(health.CurrentHealth == before - 1, "Damage did not reach enemy health.");
                }
                phase = 4;
                phaseStarted = now;
                Debug.Log("SKULL_VAMPIRE_PAUSE_RESUME_PASS");
                Debug.Log("SKULL_VAMPIRE_TELEGRAPH_COLOR_PASS");
            }
            else if (phase == 4)
            {
                if (now - phaseStarted < 0.4d) return;
                CheckLifeSteal();
                foreach (var sample in samples)
                {
                    sample.GetComponent<EnemyHealth>().Die();
                    Require(sample.GetComponent<EnemyHealth>().Heal(100) == 0, "Dead mob was healed.");
                    Require(!sample.GetComponent<BoxCollider2D>().enabled, "Dead mob still collides.");
                }
                phase = 5;
                phaseStarted = now;
            }
            else if (phase == 5 && now - phaseStarted > 1.5d)
            {
                Require(samples.All(sample => sample == null), "Dead mobs did not despawn.");
                Debug.Log("SKULL_VAMPIRE_DAMAGE_DEATH_PASS");
                Finish(true, null);
            }
        }
        catch (Exception exception) { Finish(false, exception); }
    }

    private static void CheckLanRoundTrip()
    {
        LanAssetCatalog catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        catalog.Initialize();
        var replica = new GameObject("Skull Vampire LAN Validation");
        replica.SetActive(false);
        var sender = new LanWorldReplication(catalog, replica.transform);
        var receiver = new LanWorldReplication(catalog, replica.transform);
        try
        {
            Sprite[] original = samples.Select(sample => sample.Renderer.sprite).ToArray();
            try
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    foreach (var sample in samples)
                    {
                        sample.Renderer.sprite = (Sprite)new SerializedObject(sample).FindProperty("frames")
                            .GetArrayElementAtIndex(frame).objectReferenceValue;
                    }
                    var message = new LanMessage();
                    sender.CaptureVisuals(message);
                    // Đi qua đúng serialization dùng cho LAN, không chỉ truyền object trực tiếp.
                    receiver.ApplyVisuals(JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(message)));
                    var views = (Dictionary<int, SpriteRenderer>)typeof(LanWorldReplication)
                        .GetField("clientViews", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(receiver);
                    foreach (var sample in samples)
                    {
                        Require(views.TryGetValue(sample.Renderer.GetInstanceID(), out SpriteRenderer view),
                            "LAN did not create mob view.");
                        Require(view.sprite != null && view.sprite.texture == sample.Renderer.sprite.texture &&
                            view.sprite.rect == sample.Renderer.sprite.rect &&
                            view.sprite.pivot == sample.Renderer.sprite.pivot &&
                            Vector3.Distance(view.transform.lossyScale, sample.Renderer.transform.lossyScale) < 0.001f,
                            "LAN mob frame/scale differs from Host.");
                    }
                }
            }
            finally
            {
                for (int i = 0; i < samples.Length; i++) samples[i].Renderer.sprite = original[i];
            }
            Debug.Log("SKULL_VAMPIRE_LAN_VISUAL_PASS mobs=2 frames=8 serializedRoundTrip=true");
        }
        finally
        {
            sender.Dispose();
            receiver.Dispose();
            UnityEngine.Object.Destroy(replica);
        }
    }

    private static void CheckLifeSteal()
    {
        EnemyHealth vampire = samples[1].GetComponent<EnemyHealth>();
        EnemyCombat combat = samples[1].GetComponent<EnemyCombat>();
        var fixture = new GameObject("Life steal damage target");
        HealthController target = fixture.AddComponent<HealthController>();
        target.enabled = false;
        target.currentHealth = 10;
        typeof(HealthController).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 10);
        LanMode previousMode = LanLaunch.Mode;
        string previousAddress = LanLaunch.Address;
        int previousPort = LanLaunch.Port;
        string previousError = LanLaunch.LastError;
        try
        {
            vampire.TakeDamage(30);
            int before = vampire.CurrentHealth;
            Require(combat.DealDamageToPlayer(target, 1) == 1 && target.currentHealth == 9 &&
                vampire.CurrentHealth == before + 12, "Vampire did not steal 12 HP from real damage.");
            before = vampire.CurrentHealth;
            Require(combat.DealDamageToPlayer(target, 1) == 0 && vampire.CurrentHealth == before,
                "Invulnerability incorrectly granted life steal.");
            Require(combat.DealDamageToPlayer(null, 1) == 0 && combat.DealDamageToPlayer(target, 0) == 0 &&
                vampire.CurrentHealth == before, "Miss/zero damage granted life steal.");
            LanLaunch.Configure(LanMode.Client);
            Require(combat.DealDamageToPlayer(target, 1) == 0 && vampire.Heal(12) == 0 &&
                vampire.CurrentHealth == before, "Client mutated authoritative health.");
            LanLaunch.Configure(previousMode, previousAddress, previousPort);
            int restored = vampire.Heal(1000);
            Require(restored == vampire.MaxHealth - before && vampire.CurrentHealth == vampire.MaxHealth &&
                vampire.Heal(1) == 0, "Healing exceeded maximum HP.");
            Require(samples[0].GetComponent<EnemyHealth>().Config.chaseSpeed > 2.5f &&
                samples[0].GetComponent<EnemyHealth>().MaxHealth == 65, "Skull balance was not applied.");
            Debug.Log("SKULL_VAMPIRE_LIFESTEAL_PASS heal=12 blocked=0 miss=0 client=0 capped=true skullHP=65 skullSpeed=3.2");
        }
        finally
        {
            LanLaunch.Configure(previousMode, previousAddress, previousPort);
            LanLaunch.LastError = previousError;
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static void Finish(bool success, Exception exception)
    {
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
        if (exception != null) Debug.LogException(exception);
        Debug.Log(success ? "SKULL_VAMPIRE_PLAY_PASS" : "SKULL_VAMPIRE_PLAY_FAIL");
        EditorApplication.ExitPlaymode();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
