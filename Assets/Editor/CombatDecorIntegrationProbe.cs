using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in batch probe against the real Dungeon scene.</summary>
[InitializeOnLoad]
public static class CombatDecorIntegrationProbe
{
    private const string SessionKey = "Labyrinth.CombatDecorProbe";
    private static double deadline;
    private static double nextCheck;
    private static int phase;
    private static HealthController health;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    static CombatDecorIntegrationProbe()
    {
        if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        deadline = 0; nextCheck = 0; phase = 0;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 35;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "timeout");
            GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (manager == null || manager.CurrentState != GameState.Playing) return;
            if (phase == 0)
            {
                // Playing can be published before a component added in Player.Start
                // receives its own Start/Update. Wait for a stable gameplay frame.
                if (nextCheck == 0) nextCheck = EditorApplication.timeSinceStartup + 0.5;
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                foreach (EnemyBrain brain in UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) brain.enabled = false;
                foreach (EnemyMovement movement in UnityEngine.Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None)) movement.StopImmediately();
                foreach (EnemyCombat combat in UnityEngine.Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None)) { combat.CancelAttack(); combat.enabled = false; }
                health = manager.PlayerMovement.GetComponent<HealthController>();
                Require(health.MaxHealth == 10 && health.startHeart == 5, "five hearts missing");
                Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None)
                    .Any(image => image.isActiveAndEnabled && image.texture is RenderTexture), "minimap missing");
                Require(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Any(renderer => renderer.enabled && renderer.name == "Nearest Enemy Direction"), "direction indicator missing");
                Require(GameObject.Find("Dungeon Decorations") != null, "decor root missing");
                Require(UnityEngine.Object.FindObjectsByType<AnimatedDecor>(FindObjectsSortMode.None).Length > 0, "animated decor missing");
                Require(UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
                    .Any(light => light.GetComponent<AnimatedDecor>() != null && light.intensity > 0), "torch light missing");
                health.RestoreFullHealth();
                health.TakeDamage(100);
                Require(health.currentHealth == 9, "half-heart cap");
                health.TakeDamage(100);
                Require(health.currentHealth == 9, "i-frames did not block second hit");
                nextCheck = EditorApplication.timeSinceStartup + 1.0;
                phase = 1;
            }
            else if (phase == 1 && EditorApplication.timeSinceStartup >= nextCheck)
            {
                health.TakeDamage(100);
                Require(health.currentHealth == 8, "i-frames did not expire");
                health.RestoreFullHealth();
                health.TakeDamage(100);
                Require(health.currentHealth == 9, "new level retained stale i-frames");
                health.RestoreFullHealth();
                PlayerSkillController skills = manager.PlayerMovement.GetComponent<PlayerSkillController>();
                Sprite[] cachedSprites = { Boss3DVisual.GetSoftShadowSprite(), Boss3DVisual.GetRuneCircleSprite(),
                    Boss3DVisual.GetKingCrownSprite(), EnemyEffectSpriteFactory.Disc };
                LanLaunch.Configure(LanMode.Host);
                Require(cachedSprites.All(sprite => sprite != null && sprite.texture.isReadable), "Solo -> Host lost cached texture pixels");
                LanLaunch.Configure(LanMode.Solo);
                Require(skills.UltimateNormalized > 0.99f, "R not ready at start");
                skills.ultimateCharge = 0;
                typeof(PlayerSkillController).GetField("isSkillCasting", PrivateInstance).SetValue(skills, true);
                typeof(PlayerSkillController).GetMethod("AddSkillHitCharge", PrivateInstance).Invoke(skills, new object[] { 15f });
                Require(Mathf.Approximately(skills.ultimateCharge, 15f), "Q/E hit charge blocked during cast");
                skills.AddUltimateCharge(30f);
                Require(Mathf.Approximately(skills.ultimateCharge, 15f), "generic cast damage recharged R");
                typeof(PlayerSkillController).GetField("isSkillCasting", PrivateInstance).SetValue(skills, false);
                Vector3 aimOrigin = new Vector3(10000, 10000, 0);
                var target = new GameObject("Probe Aim Target");
                target.transform.position = aimOrigin + Vector3.up * 3;
                target.AddComponent<EnemyHealth>();
                Vector2 direction = (Vector2)typeof(PlayerSkillController).GetMethod("GetSwordWaveDirection", PrivateInstance)
                    .Invoke(skills, new object[] { aimOrigin });
                Require(Vector2.Dot(direction, Vector2.up) > 0.999f, "Q did not auto-aim at nearest target");
                UnityEngine.Object.Destroy(target);
                Debug.Log("COMBAT_DECOR_PLAYMODE_PASS minimap=ok hearts=5 indicator=ok decor=ok torchLight=ok damageCap=half-heart iframes=ok reset=ok QAutoAim=ok Q/ECharge=ok RReady=ok SoloToHostPixels=ok");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("COMBAT_DECOR_PLAYMODE_FAIL " + exception);
            Finish(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
