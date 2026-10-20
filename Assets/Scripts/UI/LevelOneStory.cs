using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Khám phá khế ước, hội thoại tùy chọn và đoạn kết. Host sở hữu tiến trình, Client chỉ gửi ý định.</summary>
public sealed partial class LevelOneStory : MonoBehaviour
{
    private static readonly string[] Speakers = { "DÒNG CHỮ TRÊN BIA", "", "KỴ SĨ" };
    private static readonly string[] Shared = {
        "Nếu ngươi cũng lạc vào đây, đừng phí sức tìm lại cửa vào. Muốn trở về, hãy tìm kẻ giữ lối ra. Ta đã để dấu trên đường xuống.",
        "", "Có người từng mắc kẹt ở đây… Họ để lại dấu trên đường xuống. Ta nên nhìn kỹ quanh đây."
    };

    private GameManager manager;
    private GameObject overlay;
    private TMP_FontAsset font;
    private TMP_Text title, speaker, body, progress, hint, nextLabel;
    public int StoryLevel { get; private set; } = 1;
    private CampaignStoryChapter chapter;
    private Button next, skip, leave;
    private GameObject previousSelection;
    private bool visible;
    private int renderedLine = -1;
    private int lastActionFrame = -1;
    private float finishAt;
    private float nextRequestAt;
    public int LineIndex { get; private set; }
    public int LineCount => Opening ? OpeningLines.Length : Ending ? EndLines.Length : chapter != null ? chapter.LineCount : Shared.Length;
    public bool IsComplete => LineIndex >= LineCount;
    public bool IsVisible => visible;
    private bool IsAuthority => LanLaunch.Mode != LanMode.Client;
    private bool CanAdvance => LanLaunch.Mode == LanMode.Solo ||
        (LanSessionBridge.Active != null && LanSessionBridge.Active.IsReady);

    public static bool GameplayButtonsReleased =>
        (Mouse.current == null || (!Mouse.current.leftButton.isPressed && !Mouse.current.rightButton.isPressed)) &&
        (Keyboard.current == null || (!Keyboard.current.spaceKey.isPressed && !Keyboard.current.leftAltKey.isPressed &&
            !Keyboard.current.qKey.isPressed && !Keyboard.current.eKey.isPressed && !Keyboard.current.rKey.isPressed &&
            !Keyboard.current.leftShiftKey.isPressed)) &&
        (Gamepad.current == null || (!Gamepad.current.rightTrigger.isPressed && !Gamepad.current.rightShoulder.isPressed));

    public void Initialize(GameManager owner) => manager = owner;

    public void PrepareLevel(int level)
    {
        Opening = false;
        StoryLevel = Mathf.Clamp(level, 1, 10);
        chapter = CampaignStoryChapter.ForLevel(StoryLevel);
        LineIndex = 0;
        finishAt = float.PositiveInfinity;
        nextRequestAt = 0f;
        renderedLine = -1;
        if (stoneVisual != null) stoneVisual.SetActive(false);
        StoneRead = false;
        interactionReadyAt = 0; GuideAvailable = false; journalDetailPage = 0;
        StonePosition = manager.PlayerMovement.transform.position + new Vector3(1.2f, 0.8f, 0f);
        PrepareNarrative();
    }

    public void ApplyRemote(int level, int line)
    {
        if (IsAuthority) return;
        if (StoryLevel != level) PrepareLevel(level);
        LineIndex = Mathf.Clamp(line, 0, LineCount);
    }

    public string SpeakerAt(int line, bool cooperative)
    {
        if (Opening || Ending) return line == 2 ? "KỴ SĨ" : "LỜI KỂ";
        if (chapter != null)
        {
            // Câu quan sát tình huống hiện tại không phải chữ người xưa khắc trên đá.
            if (ContextualChapterText(line, cooperative) != null)
                return line == 0 ? "LỜI KỂ" : line % 2 == 0 ? "KỴ SĨ" :
                    cooperative ? "NGƯỜI ĐỒNG HÀNH" : "LỜI KỂ";
            return chapter.SpeakerAt(line, cooperative);
        }
        return string.IsNullOrEmpty(Speakers[line])
            ? (cooperative ? "NGƯỜI ĐỒNG HÀNH" : "GHI CHÉP CÒN LẠI") : Speakers[line];
    }

    public string TextAt(int line, bool cooperative)
    {
        if (Opening) return OpeningLines[line];
        if (Ending) return EndLines[line];
        if (chapter != null)
            return ContextualChapterText(line, cooperative) ?? chapter.TextAt(line, cooperative);
        if (line == 1) return cooperative
            ? "Ta vừa kiểm tra phía sau. Không còn cánh cửa nào cả. Có người để lại lời nhắn ở đây… thử xem họ đã đi đâu."
            : "Dòng cuối bị khắc vội: ‘Khi lính canh ngã xuống, cổng mới mở. Ta chưa biết nó dẫn đi đâu, nhưng ở lại đây không còn gì để ăn.’";
        return Shared[line];
    }

    private string ContextualChapterText(int line, bool cooperative)
    {
            if (HasBeat(StoryLevel, Beat.GuardianDead) && (StoryLevel == 5 || StoryLevel == 10))
            {
                if (line == 0) return null; // Giữ nguyên lời khắc lịch sử.
                if (line == 1) return manager.enemyCount > 0
                    ? "Kẻ giữ cửa đã ngã, nhưng vẫn còn lính canh ở những phòng khác."
                    : "Kẻ giữ cửa và đám lính đều đã gục. Lối qua cổng không còn bị chặn.";
                if (line == 2) return StoryLevel == 5
                    ? "Qua được hắn rồi mà vẫn phải đi xuống… Còn lời nhắn nào chỉ đường không?"
                    : "Cuối cùng hắn cũng không giữ được ta nữa. Phải xem cánh cổng kia.";
                if (StoryLevel == 5 && line == 3) return null; // Manh mối thư viện trên bia.
                if (line == 3) return manager.enemyCount > 0
                    ? "Còn đám lính chặn lối. Dọn đường rồi mới qua cổng được."
                    : "Ánh sáng bên kia khác hẳn những tầng trước. Có lẽ ngoài trời ở ngay đó.";
                return StoryLevel == 5 ? "Thư viện ở phía dưới. Ta sẽ tìm lời giải ở đó."
                    : manager.enemyCount > 0 ? "Chỉ còn đám lính này thôi. Ta sẽ mở nốt đường về."
                    : "Đường về đã mở. Ta không ở lại đây thêm nữa.";
            }
            if (StoryLevel == 8 && MechanismStage == 2)
                return new[] {
                    "Hai chìa đã khớp với bệ. Dòng chữ hiện rõ: ‘Mạng sống nhà vua là khóa của mê cung.’",
                    "Vua Xương giữ lối ra bằng chính mạng sống của hắn.",
                    "Vậy muốn trở về, ta phải đánh bại hắn.",
                    "Dấu sáng trên bệ đang chỉ hướng cổng xuống.",
                    "Không còn phải mò mẫm nữa. Ta biết mình đang tìm ai."
                }[line];
            if (StoryLevel == 8 && line == 4 && manager.KeysCollected > 0)
                return manager.KeysCollected >= 2 ? "Đủ hai chiếc rồi. Ghép chúng vào bệ xem lời giải là gì." : "Đã có một chiếc. Ta phải tìm nốt chiếc còn lại.";
            if (StoryLevel == 8 && line == 3 && manager.KeysCollected > 0)
                return manager.KeysCollected >= 2 ? "Hai chiếc chìa đều ở đây. Phần chữ còn thiếu nằm trên chúng; đặt vào bệ là đọc được."
                    : MechanismStage == 1 ? "Một nửa chữ đã hiện. Dấu sáng chỉ tới chiếc chìa còn lại."
                    : "Đã có một chiếc chìa. Đặt nó vào bệ trước, rồi tìm chiếc còn lại.";
            if (StoryLevel == 6 && MechanismStage > 0 && (line == 1 || line == 2))
                return line == 1 ? "Ngọn lửa đã cháy ở đây. Có thể dùng nó làm mốc khi quay lại." : "Có ánh sáng rồi. Tiếp tục tìm lối xuống thôi.";
            if (StoryLevel == 4 && MechanismStage > 0 && manager.KeysCollected == 0 && line == 1)
                return "Ấn dò đường đã sáng. Nó chỉ hướng chiếc chìa, còn lối đi phải tự tìm.";
            if (StoryLevel == 4 && line == 1 && manager.KeysCollected > 0)
                return manager.enemyCount > 0 ? "Chìa đã tìm được. Giờ phải phá nốt vòng canh giữ mới mở được lối xuống." : "Chìa đã tìm được, lính canh cũng không còn. Lối xuống đã mở rồi.";
            if (StoryLevel == 8 && line == 1 && manager.KeysCollected > 0)
                return manager.KeysCollected >= manager.KeysRequired
                    ? "Cả hai chìa đã đủ. Nhấn G bên dấu khắc để ghép trọn thông điệp và tìm hướng về cổng."
                    : MechanismStage == 1 ? "Chiếc chìa đầu tiên đã nằm trên bệ. Dấu sáng đang chỉ hướng chiếc còn lại."
                    : "Đã có một chìa. Nhấn G bên dấu khắc để ghép nửa đầu thông điệp và dò hướng chiếc còn lại.";
            if (StoryLevel == 9 && line == 1)
                return "Dòng chữ chỉ xuống điện của Vua Xương. Đường qua đây chỉ giữ được bốn phút từ lúc bước vào; phải nhìn thời gian còn lại.";
            return null;
    }

    public void RequestAdvance(bool skipAll)
    {
        if (manager.CurrentState != GameState.Story || !CanAdvance || IsComplete ||
            lastActionFrame == Time.frameCount || Time.unscaledTime < nextRequestAt) return;
        lastActionFrame = Time.frameCount;
        nextRequestAt = Time.unscaledTime + 0.2f;
        if (IsAuthority) AdvanceAuthoritative(LineIndex, skipAll);
        else LanSessionBridge.Active.RequestStoryAdvance(LineIndex, skipAll);
    }

    public void AdvanceAuthoritative(int expectedLine, bool skipAll)
    {
        // Hai người cùng bấm một câu chỉ chuyển đúng một lần. Gói cũ không được bỏ qua câu mới.
        if (!IsAuthority || manager.CurrentState != GameState.Story || !CanAdvance ||
            expectedLine != LineIndex || IsComplete) return;
        LineIndex = skipAll ? LineCount : LineIndex + 1;
        if (IsComplete) finishAt = Time.unscaledTime + 0.2f;
    }

    private void Update()
    {
        if (manager == null) return;
        UpdateExploration();
        TickNarrative();
        bool show = manager.CurrentState == GameState.Story;
        if (show && overlay == null) BuildUI();
        if (show != visible)
        {
            visible = show;
            if (overlay != null) overlay.SetActive(show);
            if (EventSystem.current != null)
            {
                if (show)
                {
                    previousSelection = EventSystem.current.currentSelectedGameObject;
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
                }
                else EventSystem.current.SetSelectedGameObject(previousSelection);
            }
        }
        TickScenery(show);
        if (!show) return;
        if (IsComplete)
        {
            // Không để cú click đóng hội thoại trở thành nhát chém đầu tiên.
            if (IsAuthority && GameplayButtonsReleased && Time.unscaledTime >= finishAt) manager.FinishOpeningStory();
        }
        if (renderedLine != LineIndex)
        {
            renderedLine = LineIndex;
            title.text = Opening ? "NGƯỜI ĐI LẠC" : Ending ? "BÊN KIA MÊ CUNG" : $"TẦNG {StoryLevel}  ·  " + (chapter != null ? chapter.Title : "LỜI NHẮN CỦA NGƯỜI ĐI LẠC");
            int line = Mathf.Min(LineIndex, LineCount - 1);
            bool coop = LanLaunch.Mode != LanMode.Solo;
            speaker.text = SpeakerAt(line, coop);
            body.text = TextAt(line, coop);
            progress.text = $"{line + 1:00} / {LineCount:00}";
            nextLabel.text = line == LineCount - 1 ? (Opening ? "Tìm đường ra" : Ending ? "Trở về" : "Đọc xong") : "Tiếp tục";
        }
        next.interactable = skip.interactable = CanAdvance && !IsComplete;
        hint.text = !CanAdvance ? "Đang chờ người đồng hành kết nối…"
            : IsComplete ? (Ending ? "Cuối cùng cũng ra ngoài…" : "Trở lại hành trình…")
            : LanLaunch.Mode == LanMode.Solo ? "Tab: chọn nút · Enter: xác nhận · Esc: bỏ qua"
            : "Cả hai có thể tiếp tục hoặc bỏ qua\nTab: chọn nút · Enter: xác nhận · Esc: bỏ qua";
        // Submit của EventSystem xử lý Enter/gamepad. Escape chỉ bỏ qua truyện, không mở pause.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) RequestAdvance(true);
            if (Keyboard.current.tabKey.wasPressedThisFrame && EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                EventSystem.current.SetSelectedGameObject(selected == next.gameObject ? skip.gameObject :
                    selected == skip.gameObject && leave != null ? leave.gameObject : next.gameObject);
            }
        }
    }

    public bool StoneRead { get; private set; }
    public Vector3 StonePosition { get; private set; }
    private GameObject stoneVisual, explorationHud, groundRune;
    public BoxCollider2D StoneCollider { get; private set; }
    private Image stoneGlow;

    public void ApplyStoneRemote(Vector3 position, bool read)
    {
        if (IsAuthority) return;
        StonePosition = position;
        StoneRead = read;
    }

    public bool TryReadStone(HealthController reader)
    {
        if (!IsAuthority || manager.CurrentState != GameState.Playing || !CanAdvance ||
            Time.unscaledTime < interactionReadyAt || !CanReachInscription(reader)) return false;
        if (!StoneRead) { flowReader = reader.transform; flowReadPosition = reader.transform.position; }
        // Đọc bia tạm ẩn câu đang phát; mốc còn hợp lệ tiếp tục sau khi đóng.
        if (CueId > 0 && FlowCueObsolete((Beat)(CueId % 100 - 1))) cueRemaining = 0;
        nextFlowCueAt = Time.time + 1.2f;
        Record(Beat.Read);
        StoneRead = true;
        LineIndex = 0; renderedLine = -1; finishAt = float.PositiveInfinity;
        StorySession++; interactionReadyAt = Time.unscaledTime + .35f;
        ArchiveDialogue();
        manager.BeginStoneStory();
        return true;
    }

    public void RequestStoneRead()
    {
        if (manager.CurrentState != GameState.Playing || !CanAdvance) return;
        if (IsAuthority) TryReadStone(manager.PlayerMovement.GetComponent<HealthController>());
        else LanSessionBridge.Active.RequestStoneRead();
    }

    private void EnsureFont()
    {
        if (font != null) return;
        font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Story/StoryVietnamese"), 64, 7,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
        // Nạp trước chữ tiếng Việt để atlas không đổi giữa lúc bia và hội thoại cùng hiển thị.
        string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 =+·:…—[]/" +
            "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸÝ";
        font.TryAddCharacters(characters + characters.ToLowerInvariant());
        font.name = "Story Vietnamese Runtime";
    }

    private void BuildExploration()
    {
        EnsureFont();
        stoneVisual = new GameObject("Bia đá cũ", typeof(RectTransform), typeof(Canvas));
        stoneVisual.transform.SetParent(transform, false);
        var canvas = stoneVisual.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        stoneVisual.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 110);
        stoneVisual.transform.localScale = Vector3.one * 0.01f;
        var stone = Panel("Bia đá cũ — sprite", stoneVisual.transform, Color.white);
        Place(stone, new Vector2(150, 185), new Vector2(0, 15));
        stoneGlow = stone.GetComponent<Image>();
        stoneGlow.sprite = Resources.Load<Sprite>("Story/OathStone");
        stoneGlow.preserveAspect = true;
        stoneGlow.raycastTarget = false;
        groundRune = new GameObject("Nét khắc trên nền", typeof(RectTransform));
        groundRune.transform.SetParent(stoneVisual.transform, false);
        for (int i = 0; i < 4; i++)
        {
            var stroke = Panel("Nét khế ước", groundRune.transform, new Color(.55f, .92f, 1f));
            float x = (i % 2 == 0 ? -1 : 1) * 18f, y = (i < 2 ? 1 : -1) * 10f;
            Place(stroke, new Vector2(42, 4), new Vector2(x, y));
            stroke.localRotation = Quaternion.Euler(0, 0, (i == 0 || i == 3) ? 30 : -30);
            stroke.GetComponent<Image>().raycastTarget = false;
        }
        var stem = Panel("Lõi khế ước", groundRune.transform, new Color(.85f, .95f, 1f));
        Place(stem, new Vector2(3, 24), Vector2.zero); stem.GetComponent<Image>().raycastTarget = false;
        if (IsAuthority)
        {
            // Canvas có scale 0,01: chân bia rộng 1 m, sâu 0,38 m.
            // Chỉ chặn phần tiếp đất, để nhân vật đi phía sau thân bia như vật thể top-down.
            StoneCollider = stoneVisual.AddComponent<BoxCollider2D>();
            StoneCollider.size = new Vector2(100f, 38f);
            StoneCollider.offset = new Vector2(0f, -50f);
            StoneCollider.isTrigger = false;
        }
        explorationHud = new GameObject("Lời nhắc khám phá", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        explorationHud.transform.SetParent(transform, false);
        var hudCanvas = explorationHud.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay; hudCanvas.sortingOrder = 110;
        var scaler = explorationHud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private void UpdateExploration()
    {
        bool floorOne = manager.CurrentState != GameState.Generating;
        if (floorOne && stoneVisual == null) BuildExploration();
        if (stoneVisual == null) return;
        RemoveLegacyExplorationCards();
        stoneVisual.SetActive(floorOne);
        stoneVisual.transform.position = StonePosition;
        bool physicalStone = manager.CurrentLevel == 1;
        groundRune.SetActive(!physicalStone);
        if (StoneCollider != null) StoneCollider.enabled = physicalStone;
        stoneGlow.sprite = physicalStone ? Resources.Load<Sprite>("Story/OathStone") : EnemyEffectSpriteFactory.Disc;
        stoneGlow.rectTransform.sizeDelta = physicalStone ? new Vector2(150, 185) : new Vector2(85, 35);
        bool playing = floorOne && manager.CurrentState == GameState.Playing;
        bool exitReady = LevelCompletionRules.AreRequirementsMet(manager.enemyCount, manager.KeysCollected, manager.KeysRequired);
        stoneGlow.color = exitReady ? new Color(1f, 0.88f, 0.59f) : StoneRead ? new Color(0.68f, 0.71f, 0.76f)
            : Color.white * (0.94f + Mathf.Sin(Time.unscaledTime * 2f) * 0.06f);
        if (!physicalStone) stoneGlow.color = new Color(.2f, .7f, 1f, StoneRead ? .12f : .25f);
        bool near = Vector2.Distance(manager.PlayerMovement.transform.position, StonePosition) <= 1.8f;
        bool alive = manager.PlayerMovement.GetComponent<HealthController>().currentHealth > 0;
        bool prompt = playing && near && alive && CanAdvance &&
            (!IsAuthority || CanReachInscription(manager.PlayerMovement.GetComponent<HealthController>()));
        explorationHud.SetActive(playing && prompt && !journalOpen);
        if (UpdateDecorationPrompt(prompt, playing)) prompt = false;
        if (prompt && !journalOpen)
        {
            bool canActivate = HasMechanism && (StoryLevel == 8 ? manager.KeysCollected > MechanismStage : MechanismStage == 0);
            ShowInteractionMarker(StonePosition + Vector3.up * (physicalStone ? 1.2f : .75f), canActivate ? "F · G" : "F");
        }
        if (prompt && !journalOpen && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (IsAuthority) TryActivateMechanism(manager.PlayerMovement.GetComponent<HealthController>());
            else LanSessionBridge.Active.RequestMechanismActivate();
        }
        if (prompt && !journalOpen && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) RequestStoneRead();
    }

    private void RemoveLegacyExplorationCards()
    {
        // Unity hot reload giữ GameObject đã tạo trong Play, kể cả khi bỏ mã tạo khung.
        // Dọn đúng HUD cũ; không đụng tới khung nội dung sau khi đọc hoặc độc thoại.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var hud = transform.GetChild(i);
            if (hud.name != "Lời nhắc khám phá") continue;
            var oldCard = hud.Find("Nền lời nhắc");
            if (oldCard != null)
            {
                oldCard.gameObject.SetActive(false);
                Destroy(oldCard.gameObject);
            }
            if (hud.gameObject != explorationHud)
            {
                hud.gameObject.SetActive(false);
                Destroy(hud.gameObject);
            }
        }
    }

    private void BuildUI()
    {
        overlay = new GameObject("Hội thoại chiến dịch", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (EventSystem.current == null)
            new GameObject("Story EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        // Atlas riêng hỗ trợ tiếng Việt, không sửa font/atlas đang được chỉnh trong project.
        EnsureFont();
        var shade = Panel("Nền tối", overlay.transform, new Color(0.015f, 0.02f, 0.035f, 0.72f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        if (LanLaunch.Mode != LanMode.Solo)
            leave = MakeButton(shade, "Rời phòng", new Vector2(450, 240), new Vector2(180, 52), false,
                () => LanSessionBridge.Active?.Leave());
        var card = Panel("Hội thoại", shade, new Color(0.025f, 0.04f, 0.06f, 0.82f));
        Place(card, new Vector2(1080, 376), new Vector2(0, -130));
        // Viền bám theo khung khi chuyển giữa cảnh phim và đọc bia.
        for (int edge = 0; edge < 4; edge++)
        {
            var border = Panel("Viền hội thoại " + edge, card, new Color(.70f, .63f, .45f, .48f));
            border.GetComponent<Image>().raycastTarget = false;
            bool horizontal = edge < 2;
            float side = edge % 2;
            border.anchorMin = horizontal ? new Vector2(0, side) : new Vector2(side, 0);
            border.anchorMax = horizontal ? new Vector2(1, side) : new Vector2(side, 1);
            border.sizeDelta = horizontal ? new Vector2(0, 1) : new Vector2(1, 0);
            border.anchoredPosition = Vector2.zero;
        }
        var rule = Panel("Vệt sáng khế ước", card, new Color(0.78f, 0.63f, 0.36f));
        Place(rule, new Vector2(984, 2), new Vector2(0, 100));
        title = Label(card, "", 21, new Vector2(-50, 145), new Vector2(880, 38), new Color(0.85f, 0.74f, 0.51f));
        progress = Label(card, "", 19, new Vector2(445, 145), new Vector2(100, 38), Color.white);
        speaker = Label(card, "", 22, new Vector2(0, 68), new Vector2(984, 34), new Color(0.61f, 0.83f, 0.88f));
        body = Label(card, "", 28, new Vector2(0, -12), new Vector2(984, 120), new Color(0.96f, 0.95f, 0.92f));
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 6f;
        hint = Label(card, "", 18, new Vector2(-210, -135), new Vector2(565, 50), new Color(0.75f, 0.79f, 0.84f));
        skip = MakeButton(card, "Bỏ qua", new Vector2(180, -135), new Vector2(150, 52), false, () => RequestAdvance(true));
        next = MakeButton(card, "Tiếp tục", new Vector2(388, -135), new Vector2(240, 52), true, () => RequestAdvance(false));
        nextLabel = next.GetComponentInChildren<TMP_Text>();
        var nav = next.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnLeft = skip; nav.selectOnRight = skip; next.navigation = nav;
        nav = skip.navigation; nav.selectOnLeft = next; nav.selectOnRight = next; skip.navigation = nav;
    }

    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }
    private static void Place(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
    }
    private TMP_Text Label(Transform parent, string text, float size, Vector2 position, Vector2 bounds, Color color)
    {
        var go = new GameObject("Chữ", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), bounds, position);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        return label;
    }
    private Button MakeButton(Transform parent, string title, Vector2 position, Vector2 size, bool primary, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(title, parent, Color.white);
        Place(rect, size, position);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = primary ? new Color(0.42f, 0.31f, 0.15f) : new Color(0.18f, 0.23f, 0.29f);
        colors.highlightedColor = colors.selectedColor = new Color(0.14f, 0.36f, 0.42f);
        colors.pressedColor = new Color(0.28f, 0.48f, 0.54f);
        colors.disabledColor = new Color(0.18f, 0.19f, 0.21f);
        button.colors = colors;
        var text = Label(rect, title, 21, Vector2.zero, size - new Vector2(12, 4), Color.white);
        text.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(action);
        return button;
    }

    private void OnDestroy()
    {
        RestoreWorldPresentation();
        foreach (var sprite in presentationSprites) if (sprite != null) Destroy(sprite);
        if (font != null)
        {
            foreach (Texture2D texture in font.atlasTextures) if (texture != null) Destroy(texture);
            if (font.material != null) Destroy(font.material);
            Destroy(font);
        }
    }
}
