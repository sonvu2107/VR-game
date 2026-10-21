using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class KeyDaisUIValidation
{
    static KeyDaisUIValidation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Temp/KeyDais.capture";
        if (!File.Exists(request) || !Application.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.isUpdating) return;
        var dais = Resources.FindObjectsOfTypeAll<RectTransform>()
            .FirstOrDefault(t => t.name == "Bệ đá hai chìa — âm sàn" && t.gameObject.scene.IsValid());
        if (dais == null || Camera.main == null) return;
        File.Delete(request);
        Directory.CreateDirectory("Captures/KeyDais");
        // Render a disposable copy of the real world UI, showing both revealed halves.
        // Never change the player's mechanism stage or visibility.
        var preview = Object.Instantiate(dais.parent.gameObject);
        preview.name = "Temporary Key Dais Preview";
        preview.hideFlags = HideFlags.DontSave;
        preview.transform.position = Vector3.zero;
        preview.SetActive(true);
        foreach (Transform child in preview.transform)
        {
            var title = child.GetComponent<TMP_Text>();
            child.gameObject.SetActive(child.name == dais.name || title != null);
            if (title != null)
            {
                title.text = "Hai nửa lời giải";
                title.fontSize = 22;
                title.rectTransform.anchoredPosition = new Vector2(0, 68);
            }
        }
        dais = (RectTransform)preview.transform.Find(dais.name);
        foreach (var child in dais.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
        foreach (var child in preview.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var labels = dais.GetComponentsInChildren<TMP_Text>();
        foreach (var label in labels) label.ForceMeshUpdate();
        File.WriteAllText("Captures/KeyDais/validation.txt",
            labels.All(t => !t.isTextOverflowing && t.preferredWidth <= t.rectTransform.rect.width)
                ? "PASS: both inscription labels fit in a copy of the real world UI; story reveal state unchanged."
                : "FAIL: inscription overflow.");
        var go = new GameObject("Temporary Key Dais Capture", typeof(Camera), typeof(UniversalAdditionalCameraData));
        var camera = go.GetComponent<Camera>();
        var target = RenderTexture.GetTemporary(512, 512, 24);
        var oldActive = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .05f, .04f);
            camera.transform.position = new Vector3(dais.position.x, dais.position.y - .15f, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
            File.WriteAllBytes("Captures/KeyDais/in-game.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(preview);
        }
    }
}
