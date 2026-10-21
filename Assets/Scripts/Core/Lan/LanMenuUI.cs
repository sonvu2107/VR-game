using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;

/// <summary>Runtime uGUI entry point; existing scene references stay intact.</summary>
public sealed partial class LanMenuUI : MonoBehaviour
{
    private LanSessionBridge session;
    private TextMeshProUGUI status;
    private TextMeshProUGUI cooldowns;
    private TMP_FontAsset font;
    private float nextRefresh;
    private string hostAddresses;
    private float nextAddressRefresh;
    private static readonly string[] MapNames = {
        "Hầm ngục gốc", "Thành cổ Arcane", "Hầm Rêu Cổ", "Ngục Ngập Nước", "Lò Rèn Tro Tàn",
        "Kho Băng Cổ", "Hầm Mộ Xương", "Thư Viện Cấm", "Điện Vực Sâu", "Điện Huyết Vương" };

    public static void Show(StartMenuControls menu)
    {
        if (FindObjectOfType<LanMenuUI>() != null) return;
        LanMenuUI ui = Create();
        ui.BuildMenu(menu);
    }

    public static void CreateSessionBar(LanSessionBridge session)
    {
        LanMenuUI ui = Create();
        ui.session = session;
        ui.BuildSessionBar();
    }

    private static LanMenuUI Create()
    {
        GameObject root = new GameObject("LAN UI", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        LanMenuUI ui = root.AddComponent<LanMenuUI>();
        // Do not inherit a font from an arbitrary scene label here. The
        // imported FeGPsc2 font can have a missing TMP atlas/material in
        // Unity 2022, which made the dynamically created LAN menu crash
        // while opening. TMP's default font is present in this project.
        ui.font = Resources.Load<TMP_FontAsset>("Story/StoryFont") ?? TMP_Settings.defaultFontAsset;
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        return ui;
    }

    private void BuildSessionBar()
    {
        RectTransform panel = Panel("LAN Session", transform, new Color(0.045f, 0.035f, 0.08f, 0.92f));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(780, 78);
        panel.anchoredPosition = new Vector2(0, -10);
        status = Label(panel, "", 18, new Vector2(-65, -22), new Vector2(620, 38));
        status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        cooldowns = Label(panel, "", 16, new Vector2(-65, -54), new Vector2(620, 26));
        cooldowns.rectTransform.anchorMin = cooldowns.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        var leave = Button(panel, "Rời phòng", new Vector2(320, -38), new Vector2(110, 44), session.Leave);
        leave.anchorMin = leave.anchorMax = new Vector2(0.5f, 1f);
        try
        {
            hostAddresses = LanNetworkAddresses.Describe();
        }
        catch (Exception) { hostAddresses = "Xem IPv4 bằng ipconfig"; }
    }

    private void Update()
    {
        if (session == null) { UpdateMenuInput(); return; }
        if (Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + 0.2f;
        status.text = session.Status + (session.IsReady ? " · HP đồng đội: " + session.TeammateHealth : "");
        if (session.HasStateAuthority)
        {
            if (Time.realtimeSinceStartup >= nextAddressRefresh)
            {
                nextAddressRefresh = Time.realtimeSinceStartup + 3f;
                try { hostAddresses = LanNetworkAddresses.Describe(); } catch (Exception) { }
            }
            cooldowns.text = "IP máy chủ: " + hostAddresses + " · cổng " + LanLaunch.Port;
        }
        else if (session.LocalState != null)
        {
            LanPlayerState state = session.LocalState;
            cooldowns.text = "HP " + state.health + " · Phím cách " + Mathf.CeilToInt(state.power) + "s · Q " +
                Mathf.CeilToInt(state.q * 2.2f) + "s · E " + Mathf.CeilToInt(state.e * 4.5f) +
                "s · R " + Mathf.RoundToInt(state.ultimate * 100f) + "% · Chơi lại: Chủ phòng";
        }
    }

    private RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        if (name == "Adventure Setup") OrnateUITheme.Panel(image);
        return go.GetComponent<RectTransform>();
    }
    private TextMeshProUGUI Label(Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text; label.fontSize = size; label.color = new Color(.96f, .95f, .92f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        Center(label.rectTransform, dimensions, position);
        return label;
    }
    private RectTransform Button(Transform parent, string text, Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action)
    {
        if (originalButton != null)
        {
            // Clone the actual StartMenu control, including its sprite-swap state
            // and the original inner cover. Never retain its scene launch callback.
            var clone = Instantiate(originalButton, parent, false);
            clone.name = text;
            clone.transform.localScale = Vector3.one;
            var nativeRect = (RectTransform)clone.transform;
            Center(nativeRect, dimensions, position);
            clone.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            clone.onClick.AddListener(action);
            foreach (var childImage in clone.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if (childImage.transform == clone.transform) continue;
                var inner = childImage.rectTransform;
                inner.anchorMin = new Vector2(.12f, .22f);
                inner.anchorMax = new Vector2(.88f, .90f);
                inner.offsetMin = inner.offsetMax = Vector2.zero;
                childImage.raycastTarget = false;
            }
            var nativeLabel = clone.GetComponentInChildren<TextMeshProUGUI>();
            nativeLabel.font = font;
            nativeLabel.text = text;
            nativeLabel.fontSize = 20;
            nativeLabel.color = new Color(.96f, .92f, .88f);
            nativeLabel.enableWordWrapping = true;
            nativeLabel.raycastTarget = false;
            Stretch(nativeLabel.rectTransform);
            nativeLabel.rectTransform.offsetMin = new Vector2(14, 6);
            nativeLabel.rectTransform.offsetMax = new Vector2(-14, -6);
            var sprites = clone.spriteState;
            sprites.selectedSprite = sprites.highlightedSprite;
            clone.spriteState = sprites;
            OrnateUITheme.Button(clone);
            return nativeRect;
        }
        RectTransform rect = Panel(text, parent, new Color(.18f, .23f, .29f));
        Center(rect, dimensions, position);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        button.onClick.AddListener(action);
        var colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        colors.selectedColor = new Color(1.5f, 1.5f, 1.5f);
        colors.pressedColor = new Color(.75f, .75f, .75f);
        button.colors = colors;
        Stretch(Label(rect, text, 20, Vector2.zero, dimensions).rectTransform);
        if (startMenu != null) OrnateUITheme.Button(button);
        return rect;
    }
    private TMP_InputField Field(Transform parent, string hint, string value, Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = Panel(hint, parent, new Color(.015f, .025f, .04f));
        Center(rect, dimensions, position);
        var field = rect.gameObject.AddComponent<TMP_InputField>();
        var label = Label(rect, value, 22, Vector2.zero, dimensions - new Vector2(16, 0));
        field.textViewport = rect; field.textComponent = label; field.text = value;
        OrnateUITheme.Input(field);
        return field;
    }
    private static void Center(RectTransform rect, Vector2 dimensions, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions; rect.anchoredPosition = position;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
