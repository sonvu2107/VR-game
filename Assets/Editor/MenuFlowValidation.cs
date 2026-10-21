using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises real menu buttons and renders the actual uGUI, not a mockup.</summary>
public static class MenuFlowValidation
{
    private static int assertions;
    private static readonly string Output = "Captures/MenuFlow";
    private static LanMenuUI ui;

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Output);
            ValidateLobbyFont();
            LanPlayerNameValidation.Run();
            if (Application.isBatchMode) MenuPreviewLibraryBuilder.Build();
            OrnateUIEditorValidation.OpenFixture("StartMenu");
            var menu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<StartMenuControls>(true)).Single();
            LanLaunch.LastError = null;
            typeof(StartMenuControls).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, null);
            Check(Object.FindObjectOfType<LanMenuUI>() == null, "new lobby is visible before Start");
            Check(GameObject.Find("Labyrinth Title Screen") != null, "new lobby exists");
            Capture("00-main", 1280, 720);
            menu.StartButtonControl();
            ui = Object.FindObjectOfType<LanMenuUI>();
            var original = menu.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Button>(true)).First(b => b.name == "Start Button");
            Check(ui.GetComponentsInChildren<Button>(true).All(b =>
                b.GetComponent<Image>().sprite == OrnateUITheme.Sprite("button_normal") ||
                b.GetComponent<Image>().sprite == OrnateUITheme.Sprite("button_hover")),
                "setup buttons use the red theme");
            Check(ui.GetComponentsInChildren<Button>(true).All(b => b.onClick.GetPersistentEventCount() == 0),
                "cloned buttons do not retain the original Start callback");
            menu.StartButtonControl();
            Check(Object.FindObjectsOfType<LanMenuUI>().Length == 1, "repeated Start does not stack menus");
            Page("Mode");
            Capture("01-mode", 1280, 720);
            Click("Mode Solo");
            Page("Character");
            var library = Resources.Load<MenuPreviewLibrary>("MenuPreviewLibrary");
            Check(library.idle.Length == 10 && library.run.Length == 10 && library.attack.Length == 7,
                "Knight preview references all original clip frames");
            foreach (PlayableCharacterId id in Enum.GetValues(typeof(PlayableCharacterId)))
            {
                Click("Character " + id);
                Check(LanLaunch.SelectedCharacter == id, "select " + id);
                for (int action = 0; action < 3; action++)
                {
                    Click("Preview " + action);
                    var preview = ui.GetComponentInChildren<MenuCharacterPreview>();
                    var frames = (Sprite[][])typeof(MenuCharacterPreview)
                        .GetField("frames", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
                    Check(frames[action].Length > 0 && frames[action].All(frame => frame != null), id + " preview " + action);
                    foreach (var frame in frames[action])
                        Check(frame.texture.filterMode == FilterMode.Point, "original pixel filtering");
                    var image = preview.GetComponent<RawImage>();
                    Check(image.enabled && image.texture != null, "visible preview " + id);
                    typeof(MenuCharacterPreview).GetMethod("RenderFrame", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(preview, new object[] { 1 });
                    Check(image.texture == frames[action][1].texture, "advance animation frame");
                }
                Click("Preview 0");
                Capture("02-character-" + id, 1280, 720);
            }
            ClickText("Tiếp tục");
            Page("Destination");
            Check(Active("Level Choices") && !Active("LAN Connection") && !Active("Host Port"), "solo has only levels");
            Click("Level 7");
            Capture("03-solo", 1280, 720);
            ClickText("Quay lại");
            Page("Character");
            Check(LanLaunch.SelectedCharacter == PlayableCharacterId.GravewindRanger, "Back preserves character");
            ClickText("Tiếp tục");
            Check((int)typeof(LanMenuUI).GetField("chosenLevel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(ui) == 7, "Back preserves level");
            ClickText("Quay lại");
            ClickText("Quay lại");
            Click("Mode Host");
            ClickText("Tiếp tục");
            Check(Active("Level Choices") && Active("Host Port") && !Active("LAN Connection"), "host has levels and port");
            Check(Active("LAN Display Name"), "host can choose a display name");
            var displayName = ui.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "Display Name");
            displayName.text = " ";
            ClickText("Tạo phòng");
            Check(ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Nhập tên hiển thị")), "blank name cannot create a room");
            displayName.text = "Minh Đức";
            ui.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "Host Port Number").text = "0";
            ClickText("Tạo phòng");
            Check(ui.gameObject.activeSelf && ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("65535")),
                "invalid host port stays on form");
            ui.GetComponentsInChildren<TMP_InputField>().Single(f => f.name == "Host Port Number").text = "7777";
            ClickText("Quay lại");
            ClickText("Tiếp tục");
            Capture("04-host", 1280, 720);
            ClickText("Quay lại");
            ClickText("Quay lại");
            Click("Mode Client");
            ClickText("Tiếp tục");
            Check(!Active("Level Choices") && Active("LAN Connection") && !Active("Host Port"), "join hides level selection");
            Check(Active("LAN Display Name") && displayName.text == "Minh Đức", "join keeps the entered name across menu steps");
            ClickText("Kết nối");
            Check(ui.gameObject.activeSelf && ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("IPv4")),
                "invalid address stays on form with error");
            Capture("05-join-error", 1280, 720);
            ClickText("Quay lại");
            ClickText("Tiếp tục");
            Capture("05-join", 1280, 720);
            Capture("05-join-1024x768", 1024, 768);
            Capture("05-join-1920x1080", 1920, 1080);
            ClickText("Quay lại");
            Capture("02-character-1024x768", 1024, 768);
            Capture("02-character-1920x1080", 1920, 1080);
            ClickText("Quay lại");
            ClickText("Menu chính");
            Check(Object.FindObjectOfType<LanMenuUI>() == null, "Back closes local setup");
            Check(GameObject.Find("Labyrinth Title Screen") != null, "Back returns to new lobby");
            menu.StartButtonControl();
            ui = Object.FindObjectOfType<LanMenuUI>();
            Page("Mode");
            Check(Object.FindObjectsOfType<LanMenuUI>().Length == 1, "reopen reuses menu without stacking");
            // Validation of configuration is independent of loading a dungeon.
            LanLaunch.Configure(LanMode.Host, "127.0.0.1", 7788, 7);
            Check(LanLaunch.StartingLevel == 7 && LanLaunch.Port == 7788, "host keeps selected port and level");
            LanLaunch.Configure(LanMode.Client, "192.168.1.10", 7788, 7);
            Check(LanLaunch.StartingLevel == 1 && LanLaunch.Address == "192.168.1.10", "join defers level to host");
            // Returning from a failed LAN connection must keep the local destination form.
            Object.DestroyImmediate(ui.gameObject);
            LanLaunch.LastError = "Không thể kết nối máy chủ. Vui lòng thử lại.";
            typeof(StartMenuControls).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, null);
            ui = Object.FindObjectOfType<LanMenuUI>();
            Page("Destination");
            Check(GameObject.Find("Labyrinth Title Screen") == null, "LAN error is not covered by the lobby");
            Check(ui.GetComponentsInChildren<TMP_Text>().Any(x => x.text == LanLaunch.LastError), "LAN error stays visible");
            Capture("06-lan-return", 1280, 720);
            LanLaunch.LastError = null;
            OrnateUIValidation.ValidateAndCapture();
            File.WriteAllText(Output + "/validation.txt", $"PASS: {assertions} menu assertions; ornate UI checks passed.\nActual Unity uGUI renders; no two-machine LAN session exercised.\n");
            Debug.Log($"MENU_FLOW_PASS {assertions} assertions");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            File.WriteAllText(Output + "/validation.txt", "FAIL\n" + ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void ValidateLobbyFont()
    {
        var font = Resources.Load<TMP_FontAsset>("Story/StoryFont");
        Check(font != null, "local Vietnamese font is present");
        var original = font.characterTable.Select(c => c.unicode).ToArray();
        const string required = "THE LABYRINTH · MÊ CUNG KHẾ ƯỚC Bắt đầu Chơi đơn Tạo phòng LAN Tham gia LAN Quay lại Menu chính Chọn nhân vật Tiếp tục Kết nối Không thể kết nối máy chủ. Vui lòng thử lại. ←→•▼…";
        string all = required + "\u00c3\u00ca\u00d3\u1ea2\u1ea6\u1eae\u1ec5\u1ef0";
        string toAdd = new string(all.Distinct().Where(c => !font.HasCharacter(c)).ToArray());
        if (toAdd.Length > 0) font.TryAddCharacters(toAdd);
        string missing = new string(all.Distinct().Where(c => !font.HasCharacter(c)).ToArray());
        Check(string.IsNullOrEmpty(missing), "lobby characters available: " + missing);
        Check(original.All(c => font.characterLookupTable.ContainsKey(c)), "all existing local glyphs preserved");
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output + "/font-validation.txt", "PASS: local font retained; existing glyphs=" + original.Length +
            "; final glyphs=" + font.characterTable.Count + "; missing=0");
    }

    private static bool Active(string name) => ui.GetComponentsInChildren<Transform>(true)
        .Single(t => t.name == name).gameObject.activeInHierarchy;
    private static void Page(string name)
    {
        Check(Active(name + " Page"), name + " page active");
        Check(ui.GetComponentsInChildren<Transform>().Count(t => t.name.EndsWith(" Page")) == 1, "only one page visible");
    }
    private static void Click(string name) => ui.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
    private static void ClickText(string text) => ui.GetComponentsInChildren<Button>()
        .Single(b => b.GetComponentInChildren<TMP_Text>().text == text).onClick.Invoke();
    private static void Check(bool pass, string message)
    {
        assertions++;
        if (!pass) throw new InvalidOperationException(message);
    }

    private static void Capture(string name, int width, int height)
    {
        var go = new GameObject("UI Capture", typeof(Camera), typeof(UniversalAdditionalCameraData));
        var camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.allowHDR = false;
        go.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var target = RenderTexture.GetTemporary(width, height, 24);
        var old = RenderTexture.active;
        var canvas = ui != null ? ui.GetComponent<Canvas>() : GameObject.Find("Labyrinth Title Screen").GetComponent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        var mode = scaler.uiScaleMode;
        var renderMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        float previousPlane = canvas.planeDistance;
        float factor = ui != null ? Mathf.Min(width / 1280f, height / 720f) : width / scaler.referenceResolution.x;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            // Mirror CanvasScaler.Expand for this offscreen target (Screen still belongs to the Editor).
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvas.scaleFactor = factor;
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                if (ui == null) continue; // Existing scene layout is deliberately unchanged.
                Check(!text.isTextOverflowing, "text fits: " + text.text);
                Check(text.preferredHeight <= text.rectTransform.rect.height + 1,
                    "text height fits: " + text.text);
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible))
                    Check(character.bottomLeft.x >= text.rectTransform.rect.xMin - 1 &&
                        character.topRight.x <= text.rectTransform.rect.xMax + 1, "glyph stays inside label: " + text.text);
            }
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
        }
        finally
        {
            canvas.worldCamera = previousCamera;
            canvas.renderMode = renderMode;
            canvas.planeDistance = previousPlane;
            scaler.uiScaleMode = mode;
            camera.targetTexture = null;
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(go);
        }
    }
}
