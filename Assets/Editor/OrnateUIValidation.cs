using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class OrnateUIValidation
{
    public static void ValidateAndCapture()
    {
        Directory.CreateDirectory("Captures/OrnateUI");
        string[] paths = Directory.GetFiles("Assets/Resources/UI/Ornate", "*.png");
        Require(paths.Length == 25, "25 standalone assets");
        foreach (string path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null && importer.spriteImportMode == SpriteImportMode.Single, path);
            Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed, "pixel import " + path);
            Require(sprite.border == OrnateUIImporter.Border(Path.GetFileNameWithoutExtension(path)), "slice border " + path);
        }
        OrnateUIEditorValidation.OpenFixture("Dungeon");
        var scene = SceneManager.GetActiveScene();
        var images = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Image>(true));
        Require(images.All(i => i.sprite == null || !AssetDatabase.GetAssetPath(i.sprite).Contains("UI/Ornate")),
            "Dungeon retains original artwork");
        File.WriteAllText("Captures/OrnateUI/validation.txt",
            "PASS: 25 pixel sprites imported correctly; Dungeon retains original artwork. No scene-wide theme hook.\n");
        Debug.Log("ORNATE_UI_PASS");
    }

    private static void Require(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
    }

    public static void Capture(Canvas canvas, string file, int width, int height)
    {
        var go = new GameObject("UI Capture",typeof(Camera),typeof(UniversalAdditionalCameraData));
        var camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = OrnateUITheme.Background;
        camera.cullingMask = 1 << 5;
        camera.transform.position = new Vector3(0,0,-10);
        camera.allowHDR = false;
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var scaler = canvas.GetComponent<CanvasScaler>();
        var scaleMode = scaler.uiScaleMode;
        bool scalerEnabled = scaler.enabled;
        var target = RenderTexture.GetTemporary(width,height,24);
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.enabled = false;
            canvas.scaleFactor = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
            Canvas.ForceUpdateCanvases();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            File.WriteAllBytes(file,pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; scaler.uiScaleMode = scaleMode;
            scaler.enabled = scalerEnabled;
            camera.targetTexture = null; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(go);
        }
    }
}
