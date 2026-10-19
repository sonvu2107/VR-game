using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Local presentation of the ten-level story. Gameplay cues originate on the
/// authoritative machine and are mirrored by the existing LAN snapshots.
/// </summary>
public sealed class CampaignStory : MonoBehaviour
{
    private readonly Queue<Sequence> pending = new();
    private GameManager manager;
    private CanvasGroup cardGroup;
    private RectTransform cardRect;
    private Image transition;
    private Image dim;
    private Image portrait;
    private TMP_Text title;
    private TMP_Text speaker;
    private TMP_Text dialogue;
    private TMP_Text hint;
    private Sprite knightPortrait;
    private Sprite companionPortrait;
    private Sprite kingPortrait;
    private Coroutine presentation;
    private int presentationVersion;
    private int presentedLevel;
    private int seenCueSerial;
    private bool midShown;
    private bool bossShown;
    private bool exitHintShown;
    private float levelReadyAt;
    private float transitionTarget;

    private readonly struct Sequence
    {
        public readonly StoryLine[] Lines;
        public readonly bool Intro;
        public readonly string Heading;
        public Sequence(StoryLine[] lines, bool intro, string heading)
        { Lines = lines; Intro = intro; Heading = heading; }
    }

    private bool IsCoop => LanLaunch.Mode == LanMode.Client ||
                           (LanLaunch.Mode == LanMode.Host && LanSessionBridge.Active != null && LanSessionBridge.Active.HasGuest);
    private bool HasAuthority => LanLaunch.Mode != LanMode.Client;

    private void Awake()
    {
        manager = GetComponent<GameManager>();
        BuildInterface();
        Texture2D kingTexture = Resources.Load<Texture2D>("Story/SkeletonKing");
        if (kingTexture != null)
        {
            kingTexture.filterMode = FilterMode.Point;
            kingPortrait = Sprite.Create(kingTexture,
                new Rect(0f, 0f, kingTexture.width, kingTexture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        seenCueSerial = manager.StoryCueSerial;
    }

    private void OnEnable()
    {
        if (manager == null) return;
        manager.StateChanged += OnStateChanged;
        manager.KeyPickedUp += OnKeyPickedUp;
        EnemySignals.MiniBossDefeated += OnMiniBossDefeated;
    }

    private void OnDisable()
    {
        presentationVersion++;
        if (manager == null) return;
        manager.StateChanged -= OnStateChanged;
        manager.KeyPickedUp -= OnKeyPickedUp;
        EnemySignals.MiniBossDefeated -= OnMiniBossDefeated;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.LevelCompleted) transitionTarget = 1f;
        if (state == GameState.Generating)
        {
            presentedLevel = 0;
            midShown = bossShown = exitHintShown = false;
        }
        if (state is GameState.Playing or GameState.Story)
        {
            transitionTarget = 0f;
            // The first frame of a new level must already be frozen. Starting
            // here prevents an enemy FixedUpdate before the intro appears.
            if (presentedLevel != manager.CurrentLevel &&
                (LanLaunch.Mode != LanMode.Host || IsCoop) &&
                (LanLaunch.Mode != LanMode.Client || state == GameState.Story))
                BeginLevel();
        }
        if (state == GameState.GameOver) ClearPresentation();
        if (state == GameState.Victory)
        {
            ClearPresentation();
            Enqueue(CampaignStoryText.Ending(IsCoop), true, "THE LABYRINTH  ·  PHONG ẤN ĐÃ KHÉP LẠI");
        }
    }

    private void Update()
    {
        if (transition != null)
        {
            Color c = transition.color;
            c.a = Mathf.MoveTowards(c.a, transitionTarget, Time.unscaledDeltaTime * 2.5f);
            transition.color = c;
        }
        if (manager == null) return;
        if (portrait != null && portrait.gameObject.activeSelf && cardGroup.alpha > 0f)
        {
            RectTransform portraitRect = portrait.rectTransform;
            Vector2 position = portraitRect.anchoredPosition;
            position.y = Mathf.Sin(Time.unscaledTime * 2.8f) * 3f;
            portraitRect.anchoredPosition = position;
        }
        if (LanLaunch.Mode == LanMode.Client && manager.CurrentState == GameState.Story &&
            presentedLevel != manager.CurrentLevel)
            BeginLevel();
        if (LanLaunch.Mode == LanMode.Client && manager.StoryCueSerial != seenCueSerial &&
            manager.CurrentState is GameState.Playing or GameState.Story)
        {
            seenCueSerial = manager.StoryCueSerial;
            ShowCue((StoryCue)manager.StoryCueKind, manager.StoryCueCollector);
        }
        if (manager.CurrentState != GameState.Playing) return;
        if (HasAuthority && presentedLevel == manager.CurrentLevel)
            CheckWorldCues();
    }

    private void BeginLevel()
    {
        presentedLevel = manager.CurrentLevel;
        midShown = bossShown = exitHintShown = false;
        levelReadyAt = Time.unscaledTime;
        Sprite source = manager.PlayerMovement != null ? manager.PlayerMovement.GetComponent<SpriteRenderer>()?.sprite : null;
        if (source != null)
        {
            knightPortrait = CroppedPortrait(source);
            companionPortrait = CroppedPortrait(CompanionVisual.Alternate(source));
        }
        ClearPresentation();
        Enqueue(CampaignStoryText.Intro(presentedLevel, IsCoop), true,
            $"LEVEL {presentedLevel:00}  ·  {CampaignStoryText.Title(presentedLevel)}");
    }

    private void OnKeyPickedUp(int count, PlayerMovement collector)
    {
        if (!HasAuthority) return;
        StoryCue cue = count == 1 ? StoryCue.KeyOne : StoryCue.KeyTwo;
        PublishAndShow(cue);
    }

    private void OnMiniBossDefeated(EnemyHealth enemy)
    {
        if (HasAuthority && manager.CurrentState == GameState.Playing && manager.CurrentLevel == 5)
            PublishAndShow(StoryCue.BossDefeated);
    }

    private void CheckWorldCues()
    {
        if (Time.unscaledTime - levelReadyAt < 4.5f || manager.dungeonGenerator == null) return;
        IReadOnlyList<DungeonRoom> rooms = manager.dungeonGenerator.GeneratedRooms;
        if (rooms == null) return;
        Vector3 playerPosition = manager.PlayerMovement.transform.position;
        Vector2Int cell = new(Mathf.RoundToInt(playerPosition.x), Mathf.RoundToInt(playerPosition.y - 1f));
        foreach (DungeonRoom room in rooms)
        {
            if (!room.FloorTiles.Contains(cell)) continue;
            if (room.Type == RoomType.Start) break;
            if (room.Type == RoomType.Boss && !bossShown && manager.CurrentLevel is 9 or 10)
            {
                bossShown = true;
                if (manager.CurrentLevel == 9) midShown = true; // Prefer the timed boss warning.
                PublishAndShow(StoryCue.BossEntry);
            }
            else if (!midShown && room.Type != RoomType.Exit &&
                     (manager.CurrentLevel != 5 || room.Type == RoomType.Boss) &&
                     (manager.CurrentLevel != 8 || room.Type == RoomType.Treasure) &&
                     manager.CurrentLevel != 9)
            {
                midShown = true;
                PublishAndShow(StoryCue.Mid);
            }
            break;
        }
        if (manager.CurrentLevel == 1 && !exitHintShown && manager.enemyCount > 0)
        {
            FloorExit exit = FindObjectOfType<FloorExit>();
            if (exit != null && !exit.IsUnlocked &&
                Vector2.Distance(playerPosition, exit.transform.position) < 3f)
            {
                exitHintShown = true;
                PublishAndShow(StoryCue.ExitLocked);
            }
        }
    }

    private void PublishAndShow(StoryCue cue)
    {
        manager.PublishStoryCue((int)cue);
        seenCueSerial = manager.StoryCueSerial;
        ShowCue(cue, manager.StoryCueCollector);
    }

    private void ShowCue(StoryCue cue, int collector)
    {
        StoryLine[] lines = CampaignStoryText.Cue(manager.CurrentLevel, cue, IsCoop, collector);
        Enqueue(lines, false, null);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool PlayProbeCue(StoryCue cue)
    {
        if (!HasAuthority || manager.CurrentState != GameState.Playing) return false;
        if (CampaignStoryText.Cue(manager.CurrentLevel, cue, IsCoop).Length == 0) return false;
        PublishAndShow(cue);
        return true;
    }
#endif

    private void Enqueue(StoryLine[] lines, bool intro, string heading)
    {
        if (lines == null || lines.Length == 0) return;
        pending.Enqueue(new Sequence(lines, intro, heading));
        if (HasAuthority && manager.CurrentState == GameState.Playing)
            manager.BeginStory();
        if (presentation == null) presentation = StartCoroutine(Present(presentationVersion));
    }

    private void ClearPresentation()
    {
        presentationVersion++;
        pending.Clear();
        if (presentation != null) StopCoroutine(presentation);
        presentation = null;
        if (cardGroup != null) cardGroup.alpha = 0f;
        if (dim != null) dim.color = new Color(0f, 0f, 0f, 0f);
        if (title != null) title.gameObject.SetActive(false);
    }

    public void ResetForNewRun()
    {
        ClearPresentation();
        presentedLevel = 0;
        midShown = bossShown = exitHintShown = false;
        seenCueSerial = manager.StoryCueSerial;
    }

    private IEnumerator Present(int version)
    {
        // Start on the next frame so StartCoroutine has returned and the handle
        // is stored before any font or layout error can occur.
        yield return null;
        try
        {
        while (pending.Count > 0)
        {
            Sequence sequence = pending.Dequeue();
            title.text = sequence.Heading ?? "";
            title.gameObject.SetActive(sequence.Intro);
            hint.text = sequence.Intro ? "SPACE / CLICK  ·  TIẾP TỤC" : "";
            float targetDim = sequence.Intro ? 0.53f : 0f;
            foreach (StoryLine line in sequence.Lines)
            {
                SetPortrait(line.Speaker);
                speaker.text = SpeakerName(line.Speaker);
                dialogue.text = line.Text;
                dialogue.maxVisibleCharacters = 0;
                dialogue.ForceMeshUpdate();
                int length = dialogue.textInfo.characterCount;
                float elapsed = 0f;
                while (elapsed < 0.24f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cardGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.24f));
                    cardRect.anchoredPosition = new Vector2(0f, -16f + 16f * cardGroup.alpha);
                    Color shade = dim.color;
                    shade.a = Mathf.MoveTowards(shade.a, targetDim, Time.unscaledDeltaTime * 3f);
                    dim.color = shade;
                    yield return null;
                }
                float reveal = 0f;
                while (dialogue.maxVisibleCharacters < length)
                {
                    reveal += Time.unscaledDeltaTime * 42f;
                    dialogue.maxVisibleCharacters = Mathf.Min(length, Mathf.FloorToInt(reveal));
                    if (AdvancePressed()) { dialogue.maxVisibleCharacters = length; break; }
                    yield return null;
                }
                float hold = sequence.Intro ? 1.6f : 2.2f;
                while (hold > 0f)
                {
                    if (AdvancePressed()) break;
                    hold -= Time.unscaledDeltaTime;
                    yield return null;
                }
                elapsed = 0f;
                while (elapsed < 0.17f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cardGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.17f));
                    yield return null;
                }
            }
            dim.color = new Color(0f, 0f, 0f, 0f);
            title.gameObject.SetActive(false);
        }
        }
        finally
        {
            // A rendering exception must never leave the campaign in Story.
            // ClearPresentation increments the version when intentionally
            // replacing or cancelling this presentation.
            if (version == presentationVersion)
            {
                cardGroup.alpha = 0f;
                title.gameObject.SetActive(false);
                dim.color = new Color(0f, 0f, 0f, 0f);
                presentation = null;
                manager.FinishLocalStory();
            }
        }
    }

    private static bool AdvancePressed() =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LanSmokeProbe.Active != null || StoryDialogueProbe.Active != null ||
#endif
        (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

    private void SetPortrait(StorySpeaker who)
    {
        Sprite image = who switch
        {
            StorySpeaker.Knight => knightPortrait,
            StorySpeaker.Companion => companionPortrait,
            StorySpeaker.BoneKing => kingPortrait,
            _ => null
        };
        portrait.gameObject.SetActive(image != null);
        portrait.sprite = image;
        portrait.color = who == StorySpeaker.BoneKing ? new Color(1f, 0.68f, 0.74f) : Color.white;
        RectTransform rect = (RectTransform)portrait.transform;
        bool right = who is StorySpeaker.Companion or StorySpeaker.BoneKing;
        rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
        rect.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        rect.anchoredPosition = new Vector2(right ? -28f : 28f, 0f);
        RectTransform lineRect = (RectTransform)dialogue.transform;
        lineRect.offsetMin = new Vector2(right ? 36f : 170f, 30f);
        lineRect.offsetMax = new Vector2(right ? -170f : -36f, -84f);
        RectTransform nameRect = (RectTransform)speaker.transform;
        nameRect.offsetMin = new Vector2(right ? 36f : 170f, -82f);
        nameRect.offsetMax = new Vector2(right ? -170f : -36f, -23f);
    }

    private static string SpeakerName(StorySpeaker who) => who switch
    {
        StorySpeaker.Narrator => "LỜI DẪN",
        StorySpeaker.Knight => "KỴ SĨ TRO TÀN",
        StorySpeaker.Companion => "ĐỒNG HÀNH",
        _ => "TIẾNG VUA XƯƠNG"
    };

    private static Sprite CroppedPortrait(Sprite source)
    {
        if (source == null) return null;
        Rect frame = source.rect;
        if (frame.width >= 100f && frame.height >= 80f)
            frame = new Rect(frame.x + 31f, frame.y + 3f, 58f, 74f);
        Sprite crop = Sprite.Create(source.texture, frame, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        crop.name = "Portrait " + source.name;
        return crop;
    }

    private void BuildInterface()
    {
        GameObject root = new("Campaign Story Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.GetComponent<GraphicRaycaster>().enabled = false;

        dim = MakeImage(root.transform, "Story dim", new Color(0f, 0f, 0f, 0f));
        Stretch(dim.rectTransform, Vector2.zero, Vector2.zero);
        GameObject card = new("Dialogue card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        card.transform.SetParent(root.transform, false);
        cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0.08f, 0f);
        cardRect.anchorMax = new Vector2(0.92f, 0f);
        cardRect.pivot = new Vector2(0.5f, 0f);
        cardRect.sizeDelta = new Vector2(0f, 215f);
        cardRect.anchoredPosition = Vector2.zero;
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.035f, 0.065f, 0.11f, 0.96f);
        cardImage.raycastTarget = false;
        cardGroup = card.GetComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        Image accent = MakeImage(card.transform, "Top accent", new Color(0.38f, 0.89f, 0.91f));
        accent.rectTransform.anchorMin = new Vector2(0f, 1f);
        accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = new Vector2(0.5f, 1f);
        accent.rectTransform.sizeDelta = new Vector2(0f, 4f);
        accent.rectTransform.anchoredPosition = Vector2.zero;

        portrait = MakeImage(card.transform, "Speaker portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.rectTransform.sizeDelta = new Vector2(136f, 176f);
        speaker = MakeText(card.transform, "Speaker", 28f, FontStyles.Bold, new Color(0.52f, 0.96f, 0.98f));
        speaker.rectTransform.anchorMin = new Vector2(0f, 1f);
        speaker.rectTransform.anchorMax = new Vector2(1f, 1f);
        speaker.rectTransform.pivot = new Vector2(0f, 1f);
        speaker.rectTransform.offsetMin = new Vector2(170f, -82f);
        speaker.rectTransform.offsetMax = new Vector2(-35f, -23f);
        dialogue = MakeText(card.transform, "Dialogue", 34f, FontStyles.Normal, Color.white);
        Stretch(dialogue.rectTransform, new Vector2(170f, 28f), new Vector2(-36f, -85f));
        dialogue.enableWordWrapping = true;
        dialogue.overflowMode = TextOverflowModes.Ellipsis;
        hint = MakeText(card.transform, "Continue hint", 18f, FontStyles.Normal, new Color(0.56f, 0.7f, 0.74f));
        hint.alignment = TextAlignmentOptions.Right;
        Stretch(hint.rectTransform, new Vector2(35f, 4f), new Vector2(-35f, -183f));

        title = MakeText(root.transform, "Level title", 55f, FontStyles.Bold, Color.white);
        title.alignment = TextAlignmentOptions.Center;
        title.rectTransform.anchorMin = new Vector2(0.07f, 0.71f);
        title.rectTransform.anchorMax = new Vector2(0.93f, 0.84f);
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
        title.gameObject.SetActive(false);
        // This is deliberately last in hierarchy so the transition covers old map frames.
        transition = MakeImage(root.transform, "Level transition", new Color(0.015f, 0.025f, 0.045f, 0f));
        Stretch(transition.rectTransform, Vector2.zero, Vector2.zero);
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text MakeText(Transform parent, string name, float size, FontStyles style, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        TMP_FontAsset storyFont = Resources.Load<TMP_FontAsset>("Story/StoryFont");
        if (storyFont != null) text.font = storyFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}
