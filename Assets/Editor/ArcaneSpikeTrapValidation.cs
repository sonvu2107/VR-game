using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Controlled physics regression; never moves or damages the real player.</summary>
public static class ArcaneSpikeTrapValidation
{
    public static string RunDamageChecks()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        SpikeTrap original = UnityEngine.Object.FindObjectOfType<SpikeTrap>();
        if (original == null) throw new InvalidOperationException("No generated trap found.");
        Scene scene = SceneManager.CreateScene("Arcane Spike Physics Probe", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        try
        {
            GameObject clone = UnityEngine.Object.Instantiate(original.gameObject);
            SceneManager.MoveGameObjectToScene(clone, scene);
            clone.transform.position = Vector3.zero;
            var trap = clone.GetComponent<SpikeTrap>();
            var sourceVisual = original.GetComponentInChildren<ArcaneSpikeTrapVisual>();
            if (sourceVisual != null)
            {
                Material art = sourceVisual.GetComponent<MeshRenderer>().sharedMaterial;
                foreach (var oldVisual in clone.GetComponentsInChildren<ArcaneSpikeTrapVisual>())
                    UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                trap.ConfigureAtlasVisual(art);
            }
            var target = new GameObject("Isolated Trap Target");
            target.layer = GameObject.Find("Player").layer;
            SceneManager.MoveGameObjectToScene(target, scene);
            var body = target.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            target.AddComponent<BoxCollider2D>().size = new Vector2(0.39f, 0.49f);
            var health = target.AddComponent<HealthController>();
            health.RestoreFullHealth();
            PhysicsScene2D physics = scene.GetPhysicsScene2D();
            var observations = new List<int>();
            MethodInfo setState = typeof(SpikeTrap).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic);
            Type stateType = setState.GetParameters()[0].ParameterType;
            Action<string> phase = name => setState.Invoke(trap, new[] { Enum.Parse(stateType, name) });
            Action step = () => { physics.Simulate(0.02f); physics.Simulate(0.02f); };

            phase("Safe"); step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 10, "Safe phase damaged target.");
            phase("Warning"); step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 10, "Warning phase damaged target.");
            // Reset only the clone's cooldown to remove its pre-clone phase history.
            FieldInfo cooldown = typeof(SpikeTrap).GetField("nextDamageAt", BindingFlags.Instance | BindingFlags.NonPublic);
            cooldown.SetValue(trap, -1f);
            phase("Active"); step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 9, "Active phase did not deal exactly 1 HP.");
            health.RestoreFullHealth(); // Clear target i-frames to test TRAP cooldown independently.
            step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 10, "Trap cooldown allowed an immediate second hit.");
            body.position = Vector2.right * 3;
            cooldown.SetValue(trap, -1f);
            step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 10, "Target outside trap was damaged.");
            body.position = Vector2.zero;
            step(); observations.Add(health.currentHealth);
            Require(health.currentHealth == 9, "Re-entry after cooldown did not damage target.");
            string result = "ARCANE_TRAP_DAMAGE_PASS hp=" + string.Join(",", observations);
            Debug.Log(result);
            return result;
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
    }

    public static void ObserveCycle()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        // Ignore the physics clone if its asynchronous scene unload is pending.
        Scene active = SceneManager.GetActiveScene();
        SpikeTrap trap = Array.Find(UnityEngine.Object.FindObjectsOfType<SpikeTrap>(),
            candidate => candidate.gameObject.scene == active);
        if (trap == null) throw new InvalidOperationException("No generated trap found.");
        var visual = trap.GetComponentInChildren<ArcaneSpikeTrapVisual>();
        Bounds plateBounds = visual.GetComponent<MeshRenderer>().bounds;
        Bounds triggerBounds = trap.GetComponent<BoxCollider2D>().bounds;
        Vector3 fixedScale = trap.transform.localScale;
        var frames = new HashSet<int>();
        double deadline = EditorApplication.timeSinceStartup + 12;
        EditorApplication.CallbackFunction observe = null;
        observe = () =>
        {
            if (!Application.isPlaying || trap == null) { EditorApplication.update -= observe; return; }
            Bounds now = visual.GetComponent<MeshRenderer>().bounds;
            Bounds triggerNow = trap.GetComponent<BoxCollider2D>().bounds;
            if ((now.center - plateBounds.center).sqrMagnitude > 0.000001f ||
                (now.size - plateBounds.size).sqrMagnitude > 0.000001f ||
                (triggerNow.size - triggerBounds.size).sqrMagnitude > 0.000001f ||
                (trap.transform.localScale - fixedScale).sqrMagnitude > 0.000001f)
            {
                EditorApplication.update -= observe;
                Debug.LogError("ARCANE_TRAP_ALIGNMENT_FAIL plate/trigger moved or resized.");
                return;
            }
            frames.Add(trap.GetComponentInChildren<ArcaneSpikeTrapVisual>().CurrentFrame);
            if (frames.Count == 4)
            {
                EditorApplication.update -= observe;
                Debug.Log("ARCANE_TRAP_CYCLE_PASS frames=" + string.Join(",", frames));
                Debug.Log("ARCANE_TRAP_ALIGNMENT_PASS fixedCenter=true fixedPlate=true fixedTrigger=true scale=" + fixedScale);
            }
            else if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= observe;
                Debug.LogError("ARCANE_TRAP_CYCLE_FAIL frames=" + string.Join(",", frames));
            }
            else EditorApplication.QueuePlayerLoopUpdate();
        };
        EditorApplication.update += observe;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
