using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class NarrativeCueLayoutValidation
{
    static NarrativeCueLayoutValidation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string marker = "Temp/NarrativeCue.validate";
        if (!File.Exists(marker) || !Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var source = Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(t =>
            t.name == "Lời dẫn trong trận" && t.gameObject.scene.IsValid());
        if (source == null || Camera.main == null) return;
        File.Delete(marker);
        Directory.CreateDirectory("Captures/NarrativeCue");
        var copy = Object.Instantiate(source.gameObject);
        copy.name = "Temporary Narrative Cue Validation";
        copy.SetActive(true);
        copy.transform.position = Vector3.zero;
        copy.transform.localScale = Vector3.one * .01f;
        copy.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var label = copy.GetComponentInChildren<TMP_Text>();
        label.text = "‘Mạng sống nhà vua là khóa của mê cung.’ Vua Xương… Muốn trở về, ta phải đánh bại hắn. Cuối cùng cũng biết phải tìm ai.\n<size=16>[Backspace] Bỏ qua lời nhắc · [J] Nhật ký</size>";
        var fit = typeof(LevelOneStory).GetMethod("FitNarrativeCard", BindingFlags.NonPublic | BindingFlags.Static);
        var report = new StringBuilder();
        var go = new GameObject("Temporary Cue Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        var camera = go.GetComponent<Camera>();
        var target = RenderTexture.GetTemporary(1280,400,24);
        var previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.CopyFrom(Camera.main); camera.enabled = false;
            camera.targetTexture = target; camera.orthographic = true; camera.orthographicSize = 2;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f,.05f,.04f);
            camera.transform.rotation = Quaternion.identity;
            foreach (int width in new[] {818,720,480,320})
            {
                fit.Invoke(null,new object[] {label,(float)width,new Vector2(32,20)});
                Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
                bool fits = label.enableWordWrapping && !label.isTextOverflowing &&
                    label.textBounds.size.x <= label.rectTransform.rect.width + 1 &&
                    label.textBounds.size.y <= label.rectTransform.rect.height + 1 && label.textInfo.lineCount > 2;
                report.AppendLine((fits ? "PASS" : "FAIL") + " width=" + width + " lines=" + label.textInfo.lineCount +
                    " textBounds=" + label.textBounds.size + " rect=" + label.rectTransform.rect.size);
                if (width != 818) continue;
                camera.transform.position = new Vector3(0,copy.GetComponent<RectTransform>().rect.height * .005f,-10);
                camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(1280,400,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,1280,400),0,0); pixels.Apply();
                File.WriteAllBytes("Captures/NarrativeCue/wrapped.png",pixels.EncodeToPNG());
            }
            File.WriteAllText("Captures/NarrativeCue/validation.txt",report.ToString());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(go); Object.DestroyImmediate(copy);
        }
    }
}
