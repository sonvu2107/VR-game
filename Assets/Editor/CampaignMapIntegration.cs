using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignMapIntegration
{
    private const string Active = "CampaignMapProbe.Active", Background = "CampaignMapProbe.Background";
    private static GameManager manager;
    private static int phase, playerId, managerId, score;
    private static double started;
    static CampaignMapIntegration() { if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick; }

    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save active scene first.");
        var theme = AssetDatabase.LoadAssetAtPath<CourtyardTheme>("Assets/Art/MapThemes/ArcaneCitadel/ArcaneCitadelTheme.asset");
        if (theme == null || !theme.UsesArcaneArt) throw new InvalidOperationException("Arcane map assets missing.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        var generator = UnityEngine.Object.FindObjectOfType<InfiniteWorldGenerator>();
        var serialized = new SerializedObject(generator);
        serialized.FindProperty("campaignSecondMap").objectReferenceValue = theme;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        LanBuildTools.RefreshCatalog();
        Debug.Log("CAMPAIGN_MAP_CONFIGURED level1=Dungeon level2plus=Arcane sameSession=true");
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        SessionState.SetBool(Active, true); SessionState.SetBool(Background, Application.runInBackground);
        phase = 0; started = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
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
            Require(now - started < 90, "Campaign map probe timed out.");
            if (phase == 0)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                Require(manager.CurrentLevel == 1 && manager.dungeonGenerator.courtyardTheme == null, "Level 1 lost original map.");
                Require(UnityEngine.Object.FindObjectsOfType<ArcaneCitadelGeometry>().Length == 0, "Arcane geometry appears on level 1.");
                playerId = manager.PlayerMovement.GetInstanceID(); managerId = manager.GetInstanceID();
                manager.TryAdvanceLevel();
                Require(manager.CurrentLevel == 1 && manager.CurrentState == GameState.Playing, "Advanced without clearing enemies.");
                manager.Pause(); manager.Resume();
                foreach (var brain in UnityEngine.Object.FindObjectsOfType<EnemyBrain>()) brain.enabled = false;
                foreach (var combat in UnityEngine.Object.FindObjectsOfType<EnemyCombat>()) combat.enabled = false;
                foreach (var legacy in UnityEngine.Object.FindObjectsOfType<Enemy>()) { legacy.enabled = false; legacy.TakeDamage(int.MaxValue); }
                foreach (var health in UnityEngine.Object.FindObjectsOfType<EnemyHealth>()) health.Die();
                Require(manager.enemyCount == 0, "Clearing enemies did not update counter.");
                var exit = UnityEngine.Object.FindObjectOfType<FloorExit>();
                Require(exit.IsUnlocked, "Portal did not open after clear.");
                score = UnityEngine.Object.FindObjectOfType<ScoreManager>()?.CurrentScore ?? 0;
                var trigger = typeof(FloorExit).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
                trigger.Invoke(exit, new object[] { manager.PlayerMovement.GetComponent<Collider2D>() });
                trigger.Invoke(exit, new object[] { manager.PlayerMovement.GetComponent<Collider2D>() });
                phase = 1;
            }
            else if (phase == 1)
            {
                if (manager.CurrentState != GameState.Playing || manager.CurrentLevel == 1) return;
                Require(manager.CurrentLevel == 2, "Repeated portal trigger skipped level 2.");
                Require(manager.GetInstanceID() == managerId && manager.PlayerMovement.GetInstanceID() == playerId, "Scene switch reset session objects.");
                Require(manager.dungeonGenerator.ActiveTheme != null && manager.dungeonGenerator.ActiveTheme.UsesArcaneArt, "Level 2 has no Arcane theme.");
                Require(manager.dungeonGenerator.courtyardTheme == null, "Switching maps overwrote the original theme configuration.");
                Require(UnityEngine.Object.FindObjectsOfType<ArcaneCitadelGeometry>().Length == 1, "Level 2 geometry missing/duplicated.");
                Require((UnityEngine.Object.FindObjectOfType<ScoreManager>()?.CurrentScore ?? 0) >= score, "Transition lost score.");
                var hp = manager.PlayerMovement.GetComponent<HealthController>();
                Require(hp.currentHealth == hp.MaxHealth && manager.enemyCount > 0 && !GameManager.isWin, "Bad new-level state.");
                CheckMeshRoundTrip();
                manager.Pause(); manager.Resume();
                phase = 2;
                manager.RestartGame();
            }
            else if (phase == 2)
            {
                manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (manager == null || manager.CurrentState != GameState.Playing) return;
                Require(manager.CurrentLevel == 1 && manager.dungeonGenerator.courtyardTheme == null, "Restart did not restore original level 1.");
                Require(UnityEngine.Object.FindObjectsOfType<ArcaneCitadelGeometry>().Length == 0, "Restart kept old geometry.");
                Debug.Log("CAMPAIGN_MAP_PLAY_PASS originalLv1=true portalLv2Arcane=true samePlayer=true sameManager=true scorePreserved=true pause=true restartLv1=true");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CheckMeshRoundTrip()
    {
        var catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog"); catalog.Initialize();
        var root = new GameObject("LAN map probe replica"); root.SetActive(false);
        var sender = new LanWorldReplication(catalog, root.transform);
        var receiver = new LanWorldReplication(catalog, root.transform);
        try
        {
            var message = new LanMessage { meshes = sender.CaptureMeshes(), maps = sender.CaptureMap() };
            sender.CaptureVisuals(message);
            string json = JsonUtility.ToJson(message);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
            Require(bytes < LanTransport.MaxMessageBytes, "LAN map packet exceeds transport budget.");
            var wire = JsonUtility.FromJson<LanMessage>(json);
            receiver.ApplyMeshes(wire.meshes); receiver.ApplyVisuals(wire);
            Require(receiver.ReplicatedMeshCount == message.meshes.Length && message.meshes.Length >= 3, "LAN mesh count mismatch.");
            var views = (Dictionary<int, MeshFilter>)typeof(LanWorldReplication).GetField("clientMeshes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(receiver);
            foreach (var data in message.meshes)
            {
                var filter = views[data.id];
                Require(filter.sharedMesh.vertexCount == data.vertices.Length && filter.sharedMesh.triangles.SequenceEqual(data.triangles) &&
                    filter.sharedMesh.uv.SequenceEqual(data.uv) && filter.GetComponent<MeshRenderer>().sharedMaterial == catalog.materials[data.material], "LAN mesh geometry/material differs from Host.");
            }
            foreach (var trap in UnityEngine.Object.FindObjectsOfType<ArcaneSpikeTrapVisual>()) trap.SetFrame(3);
            sender.CaptureVisuals(message); wire = JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(message)); receiver.ApplyVisuals(wire);
            foreach (var frame in wire.meshFrames) Require(views[frame.id].sharedMesh.uv.SequenceEqual(frame.uv), "LAN trap frame mismatch.");
            receiver.ApplyMeshes(Array.Empty<LanMesh>());
            Require(receiver.ReplicatedMeshCount == 0, "Old mesh not cleared on map change.");
            Debug.Log($"CAMPAIGN_MAP_LAN_VISUAL_PASS meshes={message.meshes.Length} packetBytes={bytes} roundTrip=true trapFrames=true cleanup=true protocol={LanMessage.CurrentProtocol}");
        }
        finally { sender.Dispose(); receiver.Dispose(); UnityEngine.Object.Destroy(root); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
        Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Background, false);
        if (error != null) { Debug.LogException(error); Debug.Log("CAMPAIGN_MAP_PLAY_FAIL"); }
        EditorApplication.ExitPlaymode();
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
