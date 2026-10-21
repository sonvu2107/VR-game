using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class LanMenuUI
{
    private enum MenuStep { Mode, Character, Destination }
    private static readonly Color Muted = new Color(.76f, .69f, .65f);
    private static readonly Color Gold = new Color(.85f, .16f, .18f);
    private Button originalButton;
    private Color originalButtonColor;
    private readonly GameObject[] pages = new GameObject[3];
    private readonly TextMeshProUGUI[] steps = new TextMeshProUGUI[3];
    private readonly Button[] characterChoices = new Button[4];
    private readonly Button[] levelChoices = new Button[LanLaunch.CampaignLevelCount];
    private StartMenuControls startMenu;
    private MenuStep menuStep;
    private LanMode chosenMode;
    private int chosenLevel;
    private TextMeshProUGUI heading, characterName, characterDescription, selectionSummary;
    private TextMeshProUGUI nextText, backText;
    private Button nextButton;
    private RectTransform levelGroup, networkGroup, hostPortGroup, nameGroup;
    private TMP_InputField hostField, portField, hostPortField, nameField;
    private MenuCharacterPreview preview;
    private GameObject previousFocus;
    private bool launching;
    private void BuildMenu(StartMenuControls menu)
    {
        startMenu = menu;
        var sceneRoots = menu.gameObject.scene.GetRootGameObjects();
        originalButton = sceneRoots.SelectMany(g => g.GetComponentsInChildren<Button>(true))
            .FirstOrDefault(b => b.name == "Start Button");
        if (originalButton != null) originalButtonColor = originalButton.GetComponent<Image>().color;
        previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        chosenMode = string.IsNullOrEmpty(LanLaunch.LastError) ? LanMode.Solo : LanLaunch.Mode;
        chosenLevel = Mathf.Clamp(LanLaunch.StartingLevel, 1, LanLaunch.CampaignLevelCount);
        RectTransform backdrop = Panel("Backdrop", transform, OrnateUITheme.Background);
        Stretch(backdrop);
        RectTransform shell = Panel("Adventure Setup", backdrop, Color.clear);
        Center(shell, new Vector2(1120, 656), Vector2.zero);
        Label(shell, "THE LABYRINTH", 25, new Vector2(0, 300), new Vector2(600, 40)).color = Gold;
        for (int i = 0; i < steps.Length; i++)
            steps[i] = Label(shell, $"Bước {i + 1} / 3", 16,
                new Vector2(450, 300), new Vector2(150, 30));
        heading = Label(shell, "", 34, new Vector2(0, 235), new Vector2(1020, 48));
        for (int i = 0; i < pages.Length; i++)
        {
            var page = new GameObject(((MenuStep)i) + " Page", typeof(RectTransform));
            page.transform.SetParent(shell, false);
            Center((RectTransform)page.transform, new Vector2(1040, 360), new Vector2(0, -15));
            pages[i] = page;
        }
        BuildModePage();
        BuildCharacterPage();
        BuildDestinationPage();
        status = Label(shell, "", 17, new Vector2(0, -228), new Vector2(1020, 48));
        status.color = new Color(1f, .76f, .55f);
        var back = Button(shell, "Quay lại", new Vector2(-410, -286), new Vector2(220, 48), GoBack);
        backText = back.GetComponentInChildren<TextMeshProUGUI>();
        var next = Button(shell, "Tiếp tục", new Vector2(370, -286), new Vector2(300, 48), GoNext);
        nextButton = next.GetComponent<Button>();
        nextText = next.GetComponentInChildren<TextMeshProUGUI>();
        ShowStep(string.IsNullOrEmpty(LanLaunch.LastError) ? MenuStep.Mode : MenuStep.Destination);
        if (!string.IsNullOrEmpty(LanLaunch.LastError)) status.text = LanLaunch.LastError;
    }

    private void BuildModePage()
    {
        var modes = new[] { LanMode.Solo, LanMode.Host, LanMode.Client };
        var titles = new[] { "Chơi đơn", "Tạo phòng LAN", "Tham gia LAN" };
        for (int i = 0; i < modes.Length; i++)
        {
            LanMode mode = modes[i];
            RectTransform button = Button(pages[0].transform, titles[i], new Vector2(0, 125 - i * 112),
                new Vector2(420, 64), () => { chosenMode = mode; ShowStep(MenuStep.Character); });
            button.name = "Mode " + mode;
        }
    }

    private void BuildCharacterPage()
    {
        for (int i = 0; i < characterChoices.Length; i++)
        {
            var id = (PlayableCharacterId)i;
            var card = Button(pages[1].transform, PlayableCharacterNames.DisplayName(id),
                new Vector2(-350, 125 - i * 79), new Vector2(326, 65), () => SelectMenuCharacter(id));
            card.name = "Character " + id;
            card.GetComponentInChildren<TextMeshProUGUI>().fontSize = 20;
            characterChoices[i] = card.GetComponent<Button>();
        }
        RectTransform stage = Panel("Character Preview Stage", pages[1].transform, Color.clear);
        Center(stage, new Vector2(650, 284), new Vector2(188, 28));
        stage.gameObject.AddComponent<RectMask2D>();
        var ground = Panel("Ground", stage, new Color(.30f, .34f, .39f));
        Center(ground, new Vector2(210, 2), new Vector2(-135, -96));
        var imageObject = new GameObject("Original Sprite Preview", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(stage, false);
        var image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        Center(image.rectTransform, new Vector2(600, 400), new Vector2(-135, 104));
        preview = imageObject.AddComponent<MenuCharacterPreview>();
        characterName = Label(stage, "", 24, new Vector2(148, 78), new Vector2(280, 70));
        characterName.color = Gold;
        characterDescription = Label(stage, "", 19, new Vector2(148, -9), new Vector2(270, 90));
        characterDescription.color = Muted;
        string[] actions = { "Đứng yên", "Chạy", "Tấn công" };
        var actionButtons = new Button[3];
        for (int i = 0; i < actions.Length; i++)
        {
            int action = i;
            var rect = Button(pages[1].transform, actions[i], new Vector2(-30 + i * 218, -140),
                new Vector2(208, 44), () => SetAction(action));
            rect.name = "Preview " + i;
            rect.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            actionButtons[i] = rect.GetComponent<Button>();
        }
        SelectMenuCharacter(LanLaunch.SelectedCharacter);
        SetAction(0);
        void SetAction(int action)
        {
            preview.SetAction(action);
            for (int i = 0; i < actionButtons.Length; i++)
                MarkChoice(actionButtons[i], i == action);
        }
    }

    private void BuildDestinationPage()
    {
        var parent = pages[2].transform;
        selectionSummary = Label(parent, "", 19, new Vector2(0, 157), new Vector2(1020, 32));
        selectionSummary.color = Gold;
        nameGroup = Panel("LAN Display Name", parent, Color.clear);
        Center(nameGroup, new Vector2(700, 44), new Vector2(0, 108));
        Label(nameGroup, "Tên hiển thị", 18, new Vector2(-220, 0), new Vector2(220, 30));
        nameField = Field(nameGroup, "Display Name", LanLaunch.DisplayName, new Vector2(105, 0), new Vector2(410, 44));
        nameField.characterLimit = LanPlayerNames.MaxLength;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        levelGroup = Panel("Level Choices", parent, Color.clear);
        Center(levelGroup, new Vector2(1040, 260), new Vector2(0, -5));
        for (int i = 0; i < levelChoices.Length; i++)
        {
            int level = i + 1;
            var card = Button(levelGroup, $"{level:00}\n{MapNames[i]}",
                new Vector2((i % 5 - 2) * 206, 70 - i / 5 * 90), new Vector2(194, 78),
                () => SelectMenuLevel(level));
            card.name = "Level " + level;
            card.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            levelChoices[i] = card.GetComponent<Button>();
        }
        networkGroup = Panel("LAN Connection", parent, Color.clear);
        Center(networkGroup, new Vector2(840, 170), new Vector2(0, 10));
        Label(networkGroup, "IP chủ phòng", 18, new Vector2(-145, 40), new Vector2(450, 28));
        hostField = Field(networkGroup, "Host IPv4", LanLaunch.Address == "127.0.0.1" ? "" : LanLaunch.Address,
            new Vector2(-145, -10), new Vector2(450, 48));
        hostField.characterLimit = 15;
        var placeholder = Label(hostField.transform, "Ví dụ: 192.168.1.10", 19, Vector2.zero, new Vector2(430, 44));
        placeholder.color = Muted;
        hostField.placeholder = placeholder;
        Label(networkGroup, "Cổng", 18, new Vector2(255, 40), new Vector2(190, 28));
        portField = Field(networkGroup, "Port", LanLaunch.Port.ToString(), new Vector2(255, -10), new Vector2(190, 48));
        portField.contentType = TMP_InputField.ContentType.IntegerNumber;
        portField.characterLimit = 5;
        Label(networkGroup, "Chủ phòng chọn màn chơi.", 17,
            new Vector2(0, -62), new Vector2(780, 28)).color = Muted;
        hostPortGroup = Panel("Host Port", parent, Color.clear);
        Center(hostPortGroup, new Vector2(700, 44), new Vector2(0, -158));
        Label(hostPortGroup, "Cổng", 17, new Vector2(-100, 0), new Vector2(260, 40)).color = Muted;
        hostPortField = Field(hostPortGroup, "Host Port Number", LanLaunch.Port.ToString(),
            new Vector2(120, 0), new Vector2(170, 44));
        hostPortField.contentType = TMP_InputField.ContentType.IntegerNumber;
        hostPortField.characterLimit = 5;
        SelectMenuLevel(chosenLevel);
    }

    private void SelectMenuCharacter(PlayableCharacterId id)
    {
        LanLaunch.SelectCharacter(id);
        characterName.text = PlayableCharacterNames.DisplayName(id);
        characterDescription.text = id switch {
            PlayableCharacterId.AzureWarden => "Giáp nặng · Kiếm năng lượng",
            PlayableCharacterId.Chronoblade => "Kiếm sĩ thời gian",
            PlayableCharacterId.GravewindRanger => "Lãng khách u phong",
            _ => "Kiếm sĩ tro tàn"
        };
        preview.SetCharacter(id);
        for (int i = 0; i < characterChoices.Length; i++)
        {
            var button = characterChoices[i];
            MarkChoice(button, i == (int)id);
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                (i == (int)id ? "→  " : "") + PlayableCharacterNames.DisplayName((PlayableCharacterId)i);
        }
    }

    private void SelectMenuLevel(int level)
    {
        chosenLevel = level;
        for (int i = 0; i < levelChoices.Length; i++)
            MarkChoice(levelChoices[i], i == level - 1);
    }

    private void ShowStep(MenuStep step)
    {
        menuStep = step;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == (int)step);
            steps[i].gameObject.SetActive(i == (int)step);
        }
        status.text = "";
        nextButton.gameObject.SetActive(step != MenuStep.Mode);
        backText.text = step == MenuStep.Mode ? "Menu chính" : "Quay lại";
        heading.text = step == MenuStep.Mode ? "Chọn chế độ" :
            step == MenuStep.Character ? "Chọn nhân vật" :
            chosenMode == LanMode.Client ? "Vào phòng LAN" : "Chọn màn chơi";
        nextText.text = step == MenuStep.Character ? "Tiếp tục" :
            chosenMode == LanMode.Client ? "Kết nối" : chosenMode == LanMode.Host ? "Tạo phòng" : "Bắt đầu";
        levelGroup.gameObject.SetActive(chosenMode != LanMode.Client);
        networkGroup.gameObject.SetActive(chosenMode == LanMode.Client);
        hostPortGroup.gameObject.SetActive(chosenMode == LanMode.Host);
        nameGroup.gameObject.SetActive(chosenMode != LanMode.Solo);
        levelGroup.anchoredPosition = new Vector2(0, chosenMode == LanMode.Host ? -35 : -5);
        selectionSummary.text = PlayableCharacterNames.DisplayName(LanLaunch.SelectedCharacter) + "   /   " +
            (chosenMode == LanMode.Solo ? "Chơi đơn" : chosenMode == LanMode.Host ? "Chủ phòng LAN" : "Tham gia LAN");
        // Explicit focus prevents keyboard/controller navigation returning to a hidden page.
        var first = pages[(int)step].GetComponentInChildren<Selectable>();
        if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private void GoNext()
    {
        if (launching) return;
        if (menuStep == MenuStep.Character) { ShowStep(MenuStep.Destination); return; }
        if (menuStep != MenuStep.Destination) return;
        try
        {
            int port = 7777;
            if (chosenMode != LanMode.Solo &&
                !int.TryParse(chosenMode == LanMode.Client ? portField.text : hostPortField.text, out port))
                throw new ArgumentException("Nhập cổng hợp lệ từ 1024 đến 65535.");
            if (chosenMode != LanMode.Solo) LanLaunch.SetDisplayName(nameField.text);
            LanLaunch.Configure(chosenMode, chosenMode == LanMode.Client ? hostField.text.Trim() : "127.0.0.1",
                port, chosenLevel);
            launching = true;
            nextButton.interactable = false;
            status.text = "Đang vào mê cung…";
            startMenu.LaunchDungeon();
            gameObject.SetActive(false);
        }
        catch (Exception ex) { status.text = ex.Message; }
    }

    private void GoBack()
    {
        if (launching) return;
        if (menuStep != MenuStep.Mode) { ShowStep(menuStep - 1); return; }
        gameObject.SetActive(false);
        startMenu.ShowTitleScreen();
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void MarkChoice(Button button, bool selected)
    {
        OrnateUITheme.Mark(button, selected);
    }

    private void UpdateMenuInput()
    {
        if (startMenu == null) return;
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            GoBack();
    }
}
