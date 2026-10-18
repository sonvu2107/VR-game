using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TacticalTrapPlayValidation
{
    private const string Active = "TacticalTrapProbe.Active", Background = "TacticalTrapProbe.Background";
    private static int phase;
    private static double started, phaseStarted;
    private static GameManager manager;
    private static SpikeTrap trap;
    private static HealthController target;
    private static bool sawWarning, sawActive;

    static TacticalTrapPlayValidation() { if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool(Active, true); SessionState.SetBool(Background, Application.runInBackground);
        phase = 0; started = 0; sawWarning = sawActive = false;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/DungeonArcaneCitadel.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            double now = EditorApplication.timeSinceStartup;
            if (started == 0) started = now;
            Require(now - started < 60, "Trap Play probe timed out.");
            if (phase == 0)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                typeof(GameManager).GetProperty("CurrentLevel").GetSetMethod(true).Invoke(manager, new object[] { 4 });
                typeof(GameManager).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { GameState.Generating });
                typeof(GameManager).GetMethod("BeginLevelGeneration", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                phase = 1; return;
            }
            if (phase == 1)
            {
                if (manager.CurrentState != GameState.Playing) return;
                foreach (var b in UnityEngine.Object.FindObjectsOfType<EnemyBrain>()) b.enabled = false;
                foreach (var c in UnityEngine.Object.FindObjectsOfType<EnemyCombat>()) c.enabled = false;
                foreach (var e in UnityEngine.Object.FindObjectsOfType<Enemy>()) e.enabled = false;
                foreach (var m in UnityEngine.Object.FindObjectsOfType<EnemyMovement>()) m.Stop();
                var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
                TacticalTrapValidation.ValidateCurrent(generator);
                Require(generator.LastTrapPlan.RoutesVerified, "Demo map has unverified routes.");
                trap = UnityEngine.Object.FindObjectsOfType<SpikeTrap>().FirstOrDefault();
                Require(trap != null, "No trap spawned in demo.");
                var fixture = new GameObject("Trap damage probe target"); fixture.transform.position = trap.transform.position;
                fixture.layer = LayerMask.NameToLayer("Player");
                target = fixture.AddComponent<HealthController>(); target.enabled = false; target.RestoreFullHealth();
                fixture.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
                fixture.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                fixture.GetComponent<Rigidbody2D>().gravityScale = 0;
                Physics2D.SyncTransforms();
                trap.ConfigureTacticalCycle(2f, 0.85f, 0.9f);
                phase = 2; phaseStarted = now;
            }
            else if (phase == 2)
            {
                Require(target.currentHealth == 10, "Trap dealt damage before activation.");
                if (!trap.IsWarning) return;
                sawWarning = true;
                manager.Pause(); phase = 3; phaseStarted = now;
            }
            else if (phase == 3)
            {
                Require(trap.IsWarning && !trap.IsDangerous && target.currentHealth == 10, "Pause advanced warning or caused damage.");
                if (now - phaseStarted < 0.5) return;
                manager.Resume(); phase = 4; phaseStarted = now;
            }
            else if (phase == 4)
            {
                if (!trap.IsDangerous) { Require(target.currentHealth == 10, "Warning caused damage."); return; }
                sawActive = true; phase = 5; phaseStarted = now;
            }
            else if (phase == 5)
            {
                if (trap.IsDangerous) return;
                Require(sawWarning && sawActive && target.currentHealth == 9, "Active trap must deal exactly one damage per cycle.");
                // Client không được tự xử lý sát thương, kể cả callback được gọi trực tiếp.
                var trigger = typeof(SpikeTrap).GetMethod("OnTriggerStay2D", BindingFlags.Instance | BindingFlags.NonPublic);
                LanMode old = LanLaunch.Mode; string address = LanLaunch.Address, error = LanLaunch.LastError; int port = LanLaunch.Port;
                var setter = typeof(SpikeTrap).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic);
                var stateType = typeof(SpikeTrap).GetNestedType("TrapState", BindingFlags.NonPublic);
                target.RestoreFullHealth();
                setter.Invoke(trap, new[] { Enum.Parse(stateType, "Active") });
                try { LanLaunch.Configure(LanMode.Client); trigger.Invoke(trap, new object[] { target.GetComponent<Collider2D>() }); Require(target.currentHealth == 10, "Client dealt trap damage."); }
                finally { LanLaunch.Configure(old, address, port); LanLaunch.LastError = error; }
                Debug.Log("TACTICAL_TRAPS_PLAY_PASS physicalRoutes=true warningSafe=true activeDamage=1 pause=true clientRejected=true");
                Finish(null);
            }
        }
        catch (Exception exception) { Finish(exception); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
        Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Background, false);
        if (error != null) { Debug.LogException(error); Debug.Log("TACTICAL_TRAPS_PLAY_FAIL"); }
        EditorApplication.ExitPlaymode();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
