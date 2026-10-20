using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Probe chủ động: nhận yêu cầu trong Temp hoặc qua menu, không chạy trong bản Player.
[InitializeOnLoad]
public static class CampaignMapsPlayValidation
{
    private const string Active = "CampaignMaps.PlayValidation.Active";
    private const string Request = "Temp/CampaignMaps.play";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly HashSet<int> visited = new();
    private static readonly HashSet<string> themes = new();
    private static double started;
    private static int phase, largestPacket, originalPlayerId;
    private static bool background;
    static CampaignMapsPlayValidation()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/The Labyrinth/Campaign Maps/Play Through All Ten Maps")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Dừng Play và lưu scene trước khi kiểm tra.");
        visited.Clear(); themes.Clear(); phase=0; started=0; largestPacket=0;
        background=Application.runInBackground;
        File.WriteAllText("Temp/CampaignMaps.play.result.txt","RUNNING: ten refreshed map structures.");
        SessionState.SetBool(Active,true);
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Active,false))
        {
            if (SessionState.GetBool("CampaignMaps.OpenPreview",false) && !EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isCompiling && !EditorSceneManager.GetActiveScene().isDirty)
            {
                SessionState.SetBool("CampaignMaps.OpenPreview",false);
                EditorSceneManager.OpenScene("Assets/Scenes/Level03_MossCrypt.unity");
                var geometry=UnityEngine.Object.FindObjectOfType<ArcaneCitadelGeometry>();
                if(geometry!=null) { Selection.activeGameObject=geometry.gameObject; if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.FrameSelected(); }
            }
            if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode && !EditorSceneManager.GetActiveScene().isDirty)
            {
                File.Move(Request,Request+".started-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                Run();
            }
            return;
        }
        if (!EditorApplication.isPlaying) return;
        try
        {
            Application.runInBackground=true;
            if(started==0) started=EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup-started<180,"Campaign Play probe timed out.");
            var manager=UnityEngine.Object.FindObjectOfType<GameManager>();
            if(manager==null) return;
            if(phase==1)
            {
                if(manager.CurrentState!=GameState.Victory) return;
                Require(visited.Count==10 && themes.Count==10,"Campaign skipped or reused a map.");
                manager.RestartGame(); phase=2; return;
            }
            if(manager.CurrentState!=GameState.Playing) return;
            if(phase==2)
            {
                Require(manager.CurrentLevel==1 && manager.dungeonGenerator.ActiveTheme==null,"Restart did not restore original map.");
                Debug.Log($"TEN_MAPS_PLAY_PASS levels={visited.Count} uniqueThemes={themes.Count} realPortals=true pause=true victory=true restartOriginal=true largestWorldBytes={largestPacket}");
                File.WriteAllText("Temp/CampaignMaps.play.result.txt",$"PASS: 10 unique maps, real portal callbacks, pause/resume, Victory, Restart; LAN mesh/UV round trip. Largest world packet {largestPacket} bytes. Not a two-machine Wi-Fi test.");
                SessionState.SetBool("CampaignMaps.OpenPreview",true);
                Finish(); return;
            }
            int level=manager.CurrentLevel;
            if(!visited.Add(level)) return;
            if(level==1) originalPlayerId=manager.PlayerMovement.GetInstanceID();
            Require(manager.PlayerMovement.GetInstanceID()==originalPlayerId,"Changing map replaced the player/session.");
            Require(manager.PlayerMovement.HasWalkableAreaConstraint,
                "Player map-boundary constraint was not configured at level "+level);
            var theme=manager.dungeonGenerator.ActiveTheme;
            string identity=theme==null?"OriginalDungeon":AssetDatabase.GetAssetPath(theme);
            Require(themes.Add(identity),"Level reused a previous theme: "+level);
            Require(level==1?theme==null:level==2?theme!=null && !theme.usesCampaignAtlas:
                theme!=null && theme.usesCampaignAtlas && (int)theme.layoutStyle==level-3,"Wrong theme at level "+level);
            Require(!GameManager.isWin,"Premature Victory.");
            foreach(var brain in UnityEngine.Object.FindObjectsOfType<EnemyBrain>()) brain.enabled=false;
            foreach(var combat in UnityEngine.Object.FindObjectsOfType<EnemyCombat>()) combat.enabled=false;
            manager.Pause(); Require(manager.CurrentState==GameState.Paused,"Pause failed.");
            manager.Resume(); Require(manager.CurrentState==GameState.Playing,"Resume failed.");
            TacticalTrapValidation.ValidateCurrent(manager.dungeonGenerator);
            CheckLan(level);
            foreach(var enemy in UnityEngine.Object.FindObjectsOfType<Enemy>()) { enemy.enabled=false; enemy.TakeDamage(int.MaxValue); }
            foreach(var health in UnityEngine.Object.FindObjectsOfType<EnemyHealth>()) health.Die();
            Require(manager.enemyCount==0,"Enemy count did not clear at level "+level);
            // Key levels still require keys; exercise the real pickup callback.
            foreach(var key in UnityEngine.Object.FindObjectsOfType<DungeonKey>())
            {
                var method=typeof(DungeonKey).GetMethod("OnTriggerEnter2D",Private);
                method.Invoke(key,new object[]{manager.PlayerMovement.GetComponent<Collider2D>()});
            }
            var exit=UnityEngine.Object.FindObjectOfType<FloorExit>();
            Require(exit!=null && exit.IsUnlocked,"Portal is locked after clearing level "+level);
            typeof(FloorExit).GetMethod("OnTriggerEnter2D",Private).Invoke(exit,new object[]{manager.PlayerMovement.GetComponent<Collider2D>()});
            Debug.Log($"TEN_MAPS_PORTAL_PASS level={level} theme={identity}");
            if(level==10) phase=1;
        }
        catch(Exception error)
        {
            File.WriteAllText("Temp/CampaignMaps.play.result.txt","FAIL: "+error);
            Debug.LogException(error); Finish();
        }
    }

    private static void CheckLan(int level)
    {
        var catalog=Resources.Load<LanAssetCatalog>("LanAssetCatalog"); catalog.Initialize();
        var root=new GameObject("Campaign LAN mesh fixture"); root.SetActive(false);
        var sender=new LanWorldReplication(catalog,root.transform);
        var receiver=new LanWorldReplication(catalog,root.transform);
        var traps=UnityEngine.Object.FindObjectsOfType<ArcaneSpikeTrapVisual>();
        var frames=traps.Select(trap=>trap.CurrentFrame).ToArray();
        try
        {
            var message=new LanMessage { meshes=sender.CaptureMeshes(), maps=sender.CaptureMap() };
            sender.CaptureVisuals(message);
            string json=JsonUtility.ToJson(message);
            int bytes=System.Text.Encoding.UTF8.GetByteCount(json); largestPacket=Math.Max(largestPacket,bytes);
            Require(bytes<=LanTransport.MaxMessageBytes,"World packet exceeds LAN budget at level "+level);
            var wire=JsonUtility.FromJson<LanMessage>(json);
            receiver.ApplyMeshes(wire.meshes); receiver.ApplyVisuals(wire);
            Require(receiver.ReplicatedMeshCount==message.meshes.Length,"LAN mesh count differs.");
            Require(level==1 || message.meshes.Length>=3,"Missing floor/wall/prop meshes.");
            var views=(Dictionary<int,MeshFilter>)typeof(LanWorldReplication).GetField("clientMeshes",Private).GetValue(receiver);
            foreach(var mesh in message.meshes)
                Require(views[mesh.id].sharedMesh.vertices.SequenceEqual(mesh.vertices) &&
                    views[mesh.id].sharedMesh.uv.SequenceEqual(mesh.uv),"LAN map geometry/UV differs.");
            foreach(var trap in traps) trap.SetFrame(3);
            sender.CaptureVisuals(message);
            wire=JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(message)); receiver.ApplyVisuals(wire);
            foreach(var frame in wire.meshFrames)
                Require(views[frame.id].sharedMesh.uv.SequenceEqual(frame.uv),"LAN trap frame differs.");
            receiver.ApplyMeshes(Array.Empty<LanMesh>());
            Require(receiver.ReplicatedMeshCount==0,"Old map geometry was not cleaned up.");
        }
        finally
        {
            for(int i=0;i<traps.Length;i++) if(traps[i]!=null) traps[i].SetFrame(frames[i]);
            sender.Dispose(); receiver.Dispose(); UnityEngine.Object.Destroy(root);
        }
    }
    private static void Finish()
    {
        SessionState.SetBool(Active,false); Application.runInBackground=background;
        Time.timeScale=1; EditorApplication.ExitPlaymode();
    }
    private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
