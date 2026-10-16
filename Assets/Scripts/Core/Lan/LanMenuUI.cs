using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;

/// <summary>Runtime uGUI entry point; existing scene references stay intact.</summary>
public sealed class LanMenuUI : MonoBehaviour
{
    private LanSessionBridge session;
    private TextMeshProUGUI status;
    private TextMeshProUGUI cooldowns;
    private TMP_FontAsset font;
    private float nextRefresh;
    private string hostAddresses;
    private float nextAddressRefresh;

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
        scaler.matchWidthOrHeight = 0.5f;
        LanMenuUI ui = root.AddComponent<LanMenuUI>();
        var existing = FindObjectOfType<TextMeshProUGUI>();
        ui.font = existing != null ? existing.font : TMP_Settings.defaultFontAsset;
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        return ui;
    }

    private void BuildMenu(StartMenuControls menu)
    {
        RectTransform backdrop = Panel("Backdrop", transform, new Color(0.025f, 0.025f, 0.06f, 0.96f));
        Stretch(backdrop);
        RectTransform panel = Panel("LAN Lobby", backdrop, new Color(0.10f, 0.08f, 0.16f));
        Center(panel, new Vector2(580, 530), Vector2.zero);
        Label(panel, "THE LABYRINTH · CO-OP LAN", 29, new Vector2(0, 214), new Vector2(540, 48));
        Label(panel, "Hai người · hai máy · cùng mạng Wi-Fi/LAN", 20, new Vector2(0, 165), new Vector2(540, 42));
        TMP_InputField address = Field(panel, "IP máy Host", "192.168.1.10", new Vector2(-80, 102), new Vector2(310, 44));
        TMP_InputField port = Field(panel, "Cổng", "7777", new Vector2(180, 102), new Vector2(140, 44));
        port.contentType = TMP_InputField.ContentType.IntegerNumber;
        port.characterLimit = 5;
        address.characterLimit = 15;
        status = Label(panel, LanLaunch.LastError ?? "Host gửi IP của mình cho người tham gia.", 18,
            new Vector2(0, 34), new Vector2(530, 70));
        status.color = new Color(1f, 0.85f, 0.55f);
        Button(panel, "Tạo phòng LAN (Host)", new Vector2(0, -39), new Vector2(500, 48), () => Launch(LanMode.Host));
        Button(panel, "Tham gia phòng (Join)", new Vector2(0, -100), new Vector2(500, 48), () => Launch(LanMode.Client));
        Button(panel, "Chơi một mình", new Vector2(-130, -176), new Vector2(240, 48), () => Launch(LanMode.Solo));
        Button(panel, "Quay lại", new Vector2(130, -176), new Vector2(240, 48), () => Destroy(gameObject));

        void Launch(LanMode mode)
        {
            try
            {
                if (!int.TryParse(port.text, out int number)) throw new ArgumentException("Cổng không hợp lệ.");
                LanLaunch.Configure(mode, address.text.Trim(), number);
                gameObject.SetActive(false);
                menu.LaunchDungeon();
            }
            catch (Exception ex) { status.text = ex.Message; }
        }
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
        if (session == null || Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + 0.2f;
        status.text = session.Status + (session.IsReady ? " · HP đồng đội: " + session.TeammateHealth : "");
        if (session.HasStateAuthority)
        {
            if (Time.realtimeSinceStartup >= nextAddressRefresh)
            {
                nextAddressRefresh = Time.realtimeSinceStartup + 3f;
                try { hostAddresses = LanNetworkAddresses.Describe(); } catch (Exception) { }
            }
            cooldowns.text = "IP Host: " + hostAddresses + " · cổng " + LanLaunch.Port;
        }
        else if (session.LocalState != null)
        {
            LanPlayerState state = session.LocalState;
            cooldowns.text = "HP " + state.health + " · Space " + Mathf.CeilToInt(state.power) + "s · Q " +
                Mathf.CeilToInt(state.q * 2.2f) + "s · E " + Mathf.CeilToInt(state.e * 4.5f) +
                "s · R " + Mathf.RoundToInt(state.ultimate * 100f) + "% · Restart: Host";
        }
    }

    private RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<UnityEngine.UI.Image>().color = color;
        return go.GetComponent<RectTransform>();
    }
    private TextMeshProUGUI Label(Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Center(label.rectTransform, dimensions, position);
        return label;
    }
    private RectTransform Button(Transform parent, string text, Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Panel(text, parent, new Color(0.23f, 0.16f, 0.34f));
        Center(rect, dimensions, position);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        button.onClick.AddListener(action);
        Stretch(Label(rect, text, 20, Vector2.zero, dimensions).rectTransform);
        return rect;
    }
    private TMP_InputField Field(Transform parent, string hint, string value, Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = Panel(hint, parent, new Color(0.04f, 0.035f, 0.075f));
        Center(rect, dimensions, position);
        var field = rect.gameObject.AddComponent<TMP_InputField>();
        var label = Label(rect, value, 22, Vector2.zero, dimensions - new Vector2(16, 0));
        field.textViewport = rect; field.textComponent = label; field.text = value;
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
