#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

// Chỉ chạy khi chủ động truyền -lanNameSmoke, không nằm trong bản Release.
public sealed class LanNameSmokeProbe : MonoBehaviour
{
    private string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lanNameSmoke") < 0 || FindObjectOfType<LanNameSmokeProbe>() != null) return;
        var go = new GameObject("LAN name smoke probe");
        DontDestroyOnLoad(go); go.AddComponent<LanNameSmokeProbe>();
    }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        output = Environment.GetEnvironmentVariable("LABYRINTH_NAME_ARTIFACTS");
        Directory.CreateDirectory(output);
        bool host = LanLaunch.Mode == LanMode.Host;
        string role = host ? "host" : "client";
        float deadline = Time.realtimeSinceStartup + 150;
        GameManager gm;
        while (true)
        {
            gm = FindObjectOfType<GameManager>();
            if (host && gm != null && gm.Story != null && gm.CurrentState == GameState.Story) gm.Story.RequestAdvance(true);
            if (gm != null && gm.CurrentState == GameState.Playing && LanSessionBridge.Active != null && LanSessionBridge.Active.IsReady) break;
            if (Time.realtimeSinceStartup > deadline) { Finish(role, "FAIL connection/play timeout"); yield break; }
            yield return null;
        }
        PlayerMovement guest = null;
        Vector3 origin = gm.PlayerMovement.transform.position;
        if (host)
        {
            guest = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).First(p => p != gm.PlayerMovement);
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.enabled = false;
            foreach (EnemyBrain enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
            foreach (EnemyMovement enemy in FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None)) enemy.StopImmediately();
            foreach (EnemyCombat enemy in FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None)) { enemy.CancelAttack(); enemy.enabled = false; }
        }
        yield return new WaitForSecondsRealtime(2);
        int samples = 0;
        float maxError = 0;
        var previous = new Vector3[2];
        var traveled = new float[2];
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 12)
        {
            float t = Time.realtimeSinceStartup - start;
            if (host)
            {
                Move(gm.PlayerMovement, origin + Vector3.right * Mathf.Sin(t) * .4f);
                Move(guest, origin + Vector3.right * (2 + Mathf.Sin(t) * .4f));
            }
            yield return new WaitForEndOfFrame();
            var plates = FindObjectsByType<LanPlayerNameplate>(FindObjectsSortMode.None).OrderBy(p => p.name).ToArray();
            if (plates.Length != 2) { Finish(role, "FAIL visible plates=" + plates.Length); yield break; }
            for (int i = 0; i < 2; i++)
            {
                var label = plates[i].GetComponentInChildren<TMP_Text>();
                string expected = i == 0 ? "Minh Đức" : "Ngọc Sơn";
                if (label.text != expected || label.textInfo.characterCount != expected.Length) { Finish(role, "FAIL text=" + label.text); yield break; }
                if (samples > 0) traveled[i] += Vector3.Distance(previous[i], plates[i].transform.position);
                previous[i] = plates[i].transform.position;
                var bodies = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.sprite != null &&
                    (host ? r.GetComponent<PlayerMovement>() != null : r.name.StartsWith("LAN/") && (r.name == "LAN/LAN Player 2" || r.name == "LAN/" + gm.PlayerMovement.name))).ToArray();
                if (bodies.Length != 2) { Finish(role, "FAIL player renderers=" + bodies.Length); yield break; }
                // Vị trí tên phải ở ngay trên phần sprite có alpha, không theo canvas rỗng.
                float error = bodies.Min(r => Vector3.Distance(plates[i].transform.position, r.transform.TransformPoint(new Vector3(0, Head(r), 0)) + Vector3.up * .28f));
                maxError = Mathf.Max(maxError, error);
            }
            samples++;
            if (samples == 120) Capture(role);
        }
        string result = maxError < .15f && traveled.All(d => d > .5f) ? "PASS" : "FAIL";
        File.WriteAllText(Path.Combine(output, role + ".txt"), result + " samples=" + samples + " names=Minh Đức|Ngọc Sơn maxAnchorError=" + maxError + " traveled=" + string.Join(",", traveled) + " snapshots=" + LanSessionBridge.Active.ReceivedSnapshots);
        // Giữ Host sống đủ lâu để Client hoàn tất đo và chụp.
        yield return new WaitForSecondsRealtime(host ? 15 : 2);
        Application.Quit(result == "PASS" ? 0 : 1);
    }
    private static void Move(PlayerMovement player, Vector3 position)
    {
        player.transform.position = position;
        player.GetComponent<Rigidbody2D>().position = position;
    }
    private readonly System.Collections.Generic.Dictionary<Sprite, float> heights = new();
    private float Head(SpriteRenderer body)
    {
        if (!heights.TryGetValue(body.sprite, out float height)) heights[body.sprite] = height = LanPlayerNameplate.MeasureHead(body);
        return height;
    }
    private void Capture(string role)
    {
        var camera = Camera.main;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            // URP 14 chỉ gán sorting layers mặc định cho đèn tạo động trong Editor.
            // Khởi tạo riêng fixture chụp để camera chạy được ở Player ẩn.
            var layers = typeof(UnityEngine.Rendering.Universal.Light2D).GetField("m_ApplyToSortingLayers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var light in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
                if (layers.GetValue(light) == null) layers.SetValue(light, SortingLayer.layers.Select(l => l.id).ToArray());
            foreach (var plate in FindObjectsByType<LanPlayerNameplate>(FindObjectsSortMode.None)) plate.GetComponentInChildren<TMP_Text>().ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, role + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Destroy(image);
        }
    }
    private void Finish(string role, string message)
    {
        File.WriteAllText(Path.Combine(output, role + ".txt"), message);
        Debug.LogError(message); Application.Quit(1);
    }
}
#endif
