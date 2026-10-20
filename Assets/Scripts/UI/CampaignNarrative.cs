using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tiến trình thuộc lượt chơi, do Host xác nhận. Bỏ qua chỉ đóng phần trình bày.
public sealed partial class LevelOneStory
{
    public enum Beat { Enter, Discover, Read, Kill, Ranged, KeyOne, KeyTwo, GuardianSeen, GuardianDead, Poison, Heal, TwoMinutes, OneMinute, Exit, Ending, Activated, Assembled, FirstSteps, AfterReading, Encounter, LockedExit }
    public int[] NarrativeMasks { get; private set; } = new int[10];
    [Serializable]
    public sealed class NarrativeEntry
    {
        public int level;
        public Beat beat;
        public string text;
    }
    public List<NarrativeEntry> NarrativeHistory { get; private set; } = new();
    public int CueId { get; private set; }
    public int CueVersion { get; private set; }
    public bool Ending { get; private set; }
    public Vector3 ExitPosition { get; private set; }
    public float CueRemaining => Mathf.Max(0, cueRemaining);
    private float cueRemaining, scanAt;
    private readonly Queue<int> cueQueue = new();
    private GameObject narrativeHud, journalPanel, sealVisual;
    private TMP_Text cueText, objectiveText, journalText;
    private string fittedCue, fittedObjective;
    private Vector2 fittedCanvasSize;
    private Image[] sealIcons, sealCenters;
    private bool journalOpen;
    private int journalPage;
    private EnemyHealth[] narrativeEnemies;
    private static readonly string[] EndLines = {
        "Vua Xương đã ngã xuống. Khi tên lính canh cuối cùng tan thành bụi, ánh sáng bên kia cổng không còn dẫn tới một căn phòng khác.",
        "Qua khỏi cổng là gió sớm và nền đất ướt. Cơn mưa đã tạnh. Sau lưng, lối vào mê cung khép lại giữa những phiến đá cũ.",
        "Bầu trời… Ta tưởng sẽ không còn được nhìn thấy nữa. Về thôi. Lần tới, thà dầm mưa còn hơn bước qua một cánh cửa lạ."
    };

    public bool HasBeat(int level, Beat beat) => level >= 1 && level <= 10 && (NarrativeMasks[level - 1] & (1 << (int)beat)) != 0;

    private void PrepareNarrative()
    {
        Ending = false;
        CueId = 0; cueRemaining = 0; cueQueue.Clear(); journalOpen = false;
        narrativeEnemies = null; scanAt = 0;
        ResetStoryFlow();
        PrepareDecorations();
        if (!IsAuthority) return;
        if (StoryLevel == 1) { NarrativeMasks = new int[10]; DialogueArchive = new string[10]; NarrativeHistory.Clear(); }
        var exit = FindObjectOfType<FloorExit>();
        ExitPosition = exit != null ? exit.transform.position : StonePosition;
        // Chữ khắc trên nền không chiếm đường đi. Đặt tại tâm phòng để có thể tới gần.
        var rooms = manager.dungeonGenerator.GeneratedRooms;
        if (StoryLevel > 1 && rooms != null && rooms.Count > 1)
            StonePosition = (Vector2)rooms[Mathf.Clamp(StoryLevel / 2, 1, rooms.Count - 1)].SpawnPoint + new Vector2(.5f, .5f);
        Record(Beat.Enter);
    }

    public void Record(Beat beat, string historyText = null)
    {
        if (!IsAuthority || StoryLevel != manager.CurrentLevel || HasBeat(StoryLevel, beat) ||
            (beat != Beat.Enter && manager.CurrentState != GameState.Playing)) return;
        // Đòn đánh từ xa có thể hạ quái trước lượt quét nhìn thấy đầu tiên.
        if (beat == Beat.Kill && !HasBeat(StoryLevel, Beat.Encounter))
            Record(Beat.Encounter);
        NarrativeMasks[StoryLevel - 1] |= 1 << (int)beat;
        // Lưu ngay khi sự kiện xảy ra; manh mối tìm sau không viết lại quá khứ.
        NarrativeHistory.Add(new NarrativeEntry { level = StoryLevel, beat = beat, text = historyText ?? FlowText(StoryLevel, beat) });
        int id = StoryLevel * 100 + (int)beat + 1;
        // Đọc bia lưu nhật ký; phản ứng chỉ xuất hiện khi đã đóng bia và đi tiếp.
        if (beat == Beat.Read || beat == Beat.Ending) return;
        if (beat == Beat.Kill && CueRemaining > 0 && CueId % 100 - 1 == (int)Beat.Encounter) return;
        int priority = FlowPriority(beat);
        if (priority >= 70 && (cueRemaining <= 0 || priority > FlowPriority((Beat)(CueId % 100 - 1))))
        {
            if (cueRemaining > 0 && !FlowCueObsolete((Beat)(CueId % 100 - 1)) && !cueQueue.Contains(CueId)) cueQueue.Enqueue(CueId);
            ShowFlowCue(id);
        }
        else cueQueue.Enqueue(id);
    }

    public void RecordGuardianDeath(EnemyHealth enemy)
    {
        if (enemy == null || !enemy.IsDead || manager.CurrentState != GameState.Playing) return;
        if (enemy.GetComponent<CampaignFinalBoss>() != null || enemy.GetComponent<MiniBossController>() != null ||
            enemy.Config != null && enemy.Config.archetype == EnemyArchetype.MiniBoss)
            // Handler nhận sự kiện trước khi GameManager trừ chính quái vừa chết.
            Record(Beat.GuardianDead, StoryLevel == 10
                ? GuardianDeathText(Mathf.Max(0, manager.enemyCount - (enemy.CountsForLegacyCounter ? 1 : 0)))
                : null);
    }

    public void BeginEnding()
    {
        if (!IsAuthority) return;
        StorySession++; Ending = true; LineIndex = 0; renderedLine = -1; finishAt = float.PositiveInfinity;
        Record(Beat.Ending);
        manager.BeginStoneStory();
    }

    public void ApplyNarrativeRemote(LanMessage message)
    {
        if (IsAuthority) return;
        if (StoryLevel != message.level) PrepareLevel(message.level);
        if (message.narrativeMasks != null && message.narrativeMasks.Length == 10)
            NarrativeMasks = (int[])message.narrativeMasks.Clone();
        if (message.narrativeHistoryIncluded && message.narrativeHistory != null)
            NarrativeHistory = new List<NarrativeEntry>(message.narrativeHistory);
        if (Ending != message.storyEnding) renderedLine = -1;
        if (StorySession != message.storySession) renderedLine = -1;
        StorySession = message.storySession;
        if (message.dialogueArchive != null && message.dialogueArchive.Length == 10)
            DialogueArchive = (string[])message.dialogueArchive.Clone();
        GuideAvailable = message.guideAvailable; GuideTarget = message.guideTarget;
        if (Opening != message.storyOpening) renderedLine = -1;
        Opening = message.storyOpening;
        Ending = message.storyEnding;
        CueId = message.narrativeCue; CueVersion = message.narrativeVersion;
        cueRemaining = message.narrativeSeconds;
        ExitPosition = message.narrativeExit;
        ApplyDecorationsRemote(message);
    }

    public void DismissCue(int version)
    {
        if (IsAuthority && version == CueVersion && manager.CurrentState == GameState.Playing) { cueRemaining = 0; nextFlowCueAt = Time.time + 1.2f; }
    }

    private bool PlayerNear(Vector3 point, float distance)
    {
        var player = LanSessionBridge.Active != null ? LanSessionBridge.Active.NearestAlivePlayer(point)?.transform : manager.PlayerMovement.transform;
        return player != null && player.GetComponent<HealthController>().currentHealth > 0 && Vector2.Distance(player.position, point) <= distance;
    }

    private void TickNarrative()
    {
        if (narrativeHud == null) BuildNarrativeUI();
        TickMechanisms();
        bool playing = manager.CurrentState == GameState.Playing;
        narrativeHud.SetActive(playing);
        sealVisual.SetActive(manager.CurrentState != GameState.Generating && (manager.KeysRequired > 0 || StoryLevel == 10));
        sealVisual.transform.position = ExitPosition + Vector3.up * 1.3f;
        int seals = manager.KeysRequired > 0 ? manager.KeysRequired : 1;
        for (int i = 0; i < sealIcons.Length; i++)
        {
            bool active = i < seals;
            sealIcons[i].gameObject.SetActive(active);
            bool opened = manager.KeysRequired > 0 ? manager.KeysCollected > i : HasBeat(10, Beat.GuardianDead);
            sealIcons[i].color = opened ? new Color(.3f, .8f, .7f, .65f) : new Color(1f, .22f, .15f);
            sealIcons[i].rectTransform.anchoredPosition = new Vector2(seals == 1 ? 0 : i == 0 ? -40 : 40, 0);
            sealCenters[i].gameObject.SetActive(opened);
        }
        if (!playing) return;
        if (IsAuthority)
        {
            if (!InspectionVisible || UrgentNarrativeVisible) cueRemaining = Mathf.Max(0, cueRemaining - Time.deltaTime);
            if (CueId > 0 && FlowCueObsolete((Beat)(CueId % 100 - 1))) cueRemaining = 0;
            if (cueRemaining > 0) nextFlowCueAt = Time.time + 1.2f;
            TickStoryFlow();
            if (cueRemaining <= 0 && Time.time >= nextFlowCueAt) ShowNextFlowCue();
            if (Time.time >= scanAt)
            {
                scanAt = Time.time + .3f;
                RefreshGuide();
                if (PlayerNear(StonePosition, 2.2f)) Record(Beat.Discover);
                if (LevelCompletionRules.AreRequirementsMet(manager.enemyCount, manager.KeysCollected, manager.KeysRequired)) Record(Beat.Exit);
                if (StoryLevel == 9 && manager.HasLevelTimeLimit)
                {
                    if (manager.LevelTimeRemaining <= 120f) Record(Beat.TwoMinutes);
                    if (manager.LevelTimeRemaining <= 60f) Record(Beat.OneMinute);
                }
                narrativeEnemies = FindObjectsOfType<EnemyHealth>();
                foreach (var enemy in narrativeEnemies)
                {
                    if (enemy == null || enemy.IsDead || enemy.Config == null || !PlayerNear(enemy.transform.position, 6f)) continue;
                    if (StoryLevel == 3 && enemy.Config.archetype == EnemyArchetype.Ranged) Record(Beat.Ranged);
                    if (enemy.GetComponent<CampaignFinalBoss>() != null || enemy.GetComponent<MiniBossController>() != null || enemy.Config.archetype == EnemyArchetype.MiniBoss)
                        Record(Beat.GuardianSeen);
                }
            }
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.jKey.wasPressedThisFrame) { journalOpen = !journalOpen; journalPage = StoryLevel; journalDetailPage = 0; }
            if (journalOpen && Keyboard.current.leftArrowKey.wasPressedThisFrame) { journalPage = Mathf.Max(1, journalPage - 1); journalDetailPage = 0; }
            if (journalOpen && Keyboard.current.rightArrowKey.wasPressedThisFrame) { journalPage = Mathf.Min(StoryLevel, journalPage + 1); journalDetailPage = 0; }
            if (journalOpen && Keyboard.current.tabKey.wasPressedThisFrame) { journalTranscript = !journalTranscript; journalDetailPage = 0; }
            if (journalOpen && Keyboard.current.pageUpKey.wasPressedThisFrame) journalDetailPage = Mathf.Max(0, journalDetailPage - 1);
            if (journalOpen && Keyboard.current.pageDownKey.wasPressedThisFrame) journalDetailPage++;
            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                if (InspectionVisible)
                {
                    if (IsAuthority) DismissInspection(InspectionVersion);
                    else LanSessionBridge.Active?.RequestDecorationDismiss(InspectionVersion);
                }
                else if (IsAuthority) DismissCue(CueVersion); else LanSessionBridge.Active?.RequestNarrativeDismiss(CueVersion);
            }
        }
        cueText.transform.parent.gameObject.SetActive(cueRemaining > 0 && CueId > 0 && !journalOpen);
        if (CueId > 0) cueText.text = FlowText(CueId / 100, (Beat)(CueId % 100 - 1)) + "\n<size=16>[Backspace] Bỏ qua lời nhắc · [J] Nhật ký</size>";
        objectiveText.transform.parent.gameObject.SetActive(!journalOpen);
        objectiveText.text = Objective();
        FitNarrativeCards();
        TickInspection();
        journalPanel.SetActive(journalOpen);
        if (journalOpen)
        {
            journalText.text = JournalPageText(journalPage, journalTranscript, journalDetailPage, out int pages);
            journalDetailPage = Mathf.Clamp(journalDetailPage, 0, pages - 1);
        }
    }

    private string Objective()
    {
        if (manager.enemyCount == 0 && manager.KeysCollected >= manager.KeysRequired)
            return StoryLevel == 10 ? "Đường về đã mở. Hãy bước qua cổng để rời mê cung." : "Lối xuống đã mở. Hãy tìm cổng sang tầng tiếp theo.";
        string task = StoryLevel == 10 ? "Đánh bại Vua Xương và lính canh để trở về" : "Phá vòng canh giữ để mở lối xuống";
        return task + $" · Còn {manager.enemyCount} kẻ địch" + (manager.KeysRequired > 0 ? $" · Chìa {manager.KeysCollected}/{manager.KeysRequired}" : "");
    }

    private void FitNarrativeCards()
    {
        Vector2 canvasSize = narrativeHud.GetComponent<RectTransform>().rect.size;
        if (fittedCue == cueText.text && fittedObjective == objectiveText.text && fittedCanvasSize == canvasSize) return;
        fittedCue = cueText.text; fittedObjective = objectiveText.text; fittedCanvasSize = canvasSize;
        float availableWidth = Mathf.Max(100, canvasSize.x - 48);
        FitNarrativeCard(cueText, Mathf.Min(818, availableWidth - 32), new Vector2(32, 20));
        FitNarrativeCard(objectiveText, Mathf.Min(938, availableWidth - 32), new Vector2(32, 16));
    }

    private static void FitNarrativeCard(TMP_Text label, float maxTextWidth, Vector2 padding)
    {
        // Đo bằng TMP để tính đúng dấu tiếng Việt, rich text và dòng xuống hàng.
        float width = Mathf.Clamp(Mathf.Ceil(label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x) + 2, 1, maxTextWidth);
        float height = Mathf.Ceil(label.GetPreferredValues(label.text, width, Mathf.Infinity).y) + 2;
        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height) + padding;
    }

    private void BuildNarrativeUI()
    {
        EnsureFont(); font.TryAddCharacters("←→•");
        narrativeHud = new GameObject("Khế ước — diễn biến", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        narrativeHud.transform.SetParent(transform, false);
        narrativeHud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        narrativeHud.GetComponent<Canvas>().sortingOrder = 105;
        var scaler = narrativeHud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var cue = Panel("Lời dẫn trong trận", narrativeHud.transform, new Color(.025f, .04f, .065f, .9f));
        Place(cue, new Vector2(850, 110), new Vector2(0, -140)); cue.GetComponent<Image>().raycastTarget = false;
        cue.anchorMin = cue.anchorMax = new Vector2(.5f, 0);
        cue.pivot = new Vector2(.5f, 0);
        cue.anchoredPosition = new Vector2(0, 150);
        cueText = Label(cue, "", 20, Vector2.zero, new Vector2(810, 100), Color.white);
        var objective = Panel("Mục tiêu khế ước", narrativeHud.transform, new Color(.025f, .04f, .065f, .88f));
        Place(objective, new Vector2(1000, 44), new Vector2(0, 260)); objective.GetComponent<Image>().raycastTarget = false;
        objective.anchorMin = objective.anchorMax = new Vector2(.5f, 1);
        objective.pivot = new Vector2(.5f, 1);
        objective.anchoredPosition = new Vector2(0, -78);
        objectiveText = Label(objective, "", 18, Vector2.zero, new Vector2(970, 40), new Color(1, .85f, .6f));
        objectiveText.alignment = TextAlignmentOptions.Center;
        var journal = Panel("Nhật ký", narrativeHud.transform, new Color(.025f, .04f, .065f, .97f));
        Place(journal, new Vector2(1000, 610), Vector2.zero); journal.GetComponent<Image>().raycastTarget = false;
        journalPanel = journal.gameObject;
        journalText = Label(journal, "", 20, Vector2.zero, new Vector2(950, 570), Color.white);
        journalText.alignment = TextAlignmentOptions.TopLeft;
        journalText.enableAutoSizing = true; journalText.fontSizeMin = 16; journalText.fontSizeMax = 20;
        sealVisual = new GameObject("Ấn trên cổng", typeof(RectTransform), typeof(Canvas));
        sealVisual.transform.SetParent(transform, false);
        sealVisual.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        sealVisual.GetComponent<Canvas>().sortingOrder = 41;
        sealVisual.transform.localScale = Vector3.one * .01f;
        sealIcons = new Image[2]; sealCenters = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var icon = Panel("Ấn khóa " + (i + 1), sealVisual.transform, Color.red);
            Place(icon, new Vector2(42, 42), Vector2.zero); icon.localRotation = Quaternion.Euler(0, 0, 45);
            sealIcons[i] = icon.GetComponent<Image>(); sealIcons[i].raycastTarget = false;
            var center = Panel("Lõi đã tắt", icon, new Color(.025f, .05f, .06f, .95f));
            Place(center, new Vector2(30, 30), Vector2.zero);
            sealCenters[i] = center.GetComponent<Image>(); sealCenters[i].raycastTarget = false;
        }
    }

    public static string BeatText(int level, Beat beat)
    {
        string flow = WrittenFlowText(level, beat);
        if (flow != null) return flow;
        if (beat == Beat.Read) return level == 1 ? "Lời trên bia: ‘Muốn trở về, hãy tìm kẻ giữ lối ra.’ Người để lại dấu cũng đã mất đường quay lại." : "Lời khắc: " + CampaignStoryChapter.ForLevel(level).TextAt(0, false);
        if (beat == Beat.Ranged) return "Có kẻ bắn từ phía xa! Phải tránh đường bắn trước… Kiếm khí có thể với tới chúng.";
        if (beat == Beat.GuardianSeen) return level == 10 ? "Một bộ xương trên ngai… Vua Xương. Kẻ giữ cánh cổng cuối cùng là ngươi sao?" : "Kẻ canh cửa ở đằng kia. Nó khác những tên vừa rồi… Phải chừa chỗ để né.";
        if (beat == Beat.GuardianDead) return "Vua Xương đã gục… Cánh cổng kia có gì đang thay đổi.";
        if (beat == Beat.Poison) return "Độc! Phải ra khỏi vũng này ngay!";
        if (beat == Beat.Heal) return "Đỡ hơn rồi… Ta vẫn còn đi tiếp được. Lần sau phải nhìn kỹ chỗ đặt chân.";
        if (beat == Beat.TwoMinutes) return "Chỉ còn hai phút. Không thể dừng lâu ở đây, phải tìm đường qua cổng.";
        if (beat == Beat.OneMinute) return "Một phút nữa thôi! Phải nhanh lên, lối đi sắp khép lại!";
        return "Gió ngoài trời… Cuối cùng cũng thoát khỏi mê cung.";
    }
}
