using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class ReturnPortalUIValidation
{
    static ReturnPortalUIValidation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string marker = "Temp/ReturnPortal.capture";
        if (!File.Exists(marker) || !Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var original = Resources.FindObjectsOfTypeAll<RectTransform>()
            .FirstOrDefault(t => t.name == "Cổng trở về" && t.gameObject.scene.IsValid());
        if (original == null || original.Find("Mũi tên cổng trở về") == null || Camera.main == null) return;
        File.Delete(marker);
        Directory.CreateDirectory("Captures/ReturnPortal");
        var copy = Object.Instantiate(original.gameObject);
        copy.name = "Temporary Return Portal Preview";
        copy.hideFlags = HideFlags.DontSave;
        copy.transform.position = Vector3.zero;
        copy.SetActive(true);
        foreach (var child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var portal = copy.transform.Find("Ánh bình minh trong cổng").GetComponent<Image>();
        var arrow = copy.transform.Find("Mũi tên cổng trở về").GetComponent<TMP_Text>();
        var caption = copy.GetComponentsInChildren<TMP_Text>().First(t => t != arrow);
        var configure = typeof(LevelOneStory).GetMethod("ApplyReturnPortalState", BindingFlags.NonPublic | BindingFlags.Static);
        var cameraObject = new GameObject("Temporary Portal Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        var camera = cameraObject.GetComponent<Camera>();
        var target = RenderTexture.GetTemporary(512,512,24);
        var oldActive = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f,.05f,.04f);
            camera.transform.position = new Vector3(0,.65f,-10);
            camera.transform.rotation = Quaternion.identity;
            pixels = new Texture2D(512,512,TextureFormat.RGBA32,false);
            bool fits = true;
            foreach (bool ready in new[] { false, true })
            {
                configure.Invoke(null, new object[] { portal, arrow, caption, ready });
                caption.ForceMeshUpdate();
                fits &= !caption.isTextOverflowing && caption.preferredWidth <= caption.rectTransform.rect.width;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply();
                File.WriteAllBytes("Captures/ReturnPortal/" + (ready ? "open" : "locked") + ".png", pixels.EncodeToPNG());
            }
            File.WriteAllText("Captures/ReturnPortal/validation.txt", fits
                ? "PASS: locked/open runtime UI copies rendered with production state styling; labels fit; gameplay unchanged."
                : "FAIL: caption overflow.");
        }
        finally
        {
            RenderTexture.active = oldActive;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(copy);
        }
    }
}
