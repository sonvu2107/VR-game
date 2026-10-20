using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cảnh dựng bằng UI Unity và sprite sẵn có. Host sở hữu nhịp truyện;
// mưa, lửa và ánh cổng chỉ là chuyển động trang trí cục bộ.
public sealed partial class LevelOneStory
{
    public bool Opening { get; private set; }
    public bool PresentationVisible => scenery != null && scenery.activeInHierarchy;
    public bool FlameVisible => flameImage != null && flameImage.sprite != null && flameImage.gameObject.activeInHierarchy;
    public int VisibleKeyHalves => StoryLevel == 8 && keyHalves != null
        ? (keyHalves[0].gameObject.activeInHierarchy ? 1 : 0) + (keyHalves[1].gameObject.activeInHierarchy ? 1 : 0) : 0;
    public bool PresentationArtReady => rainyBackdrop != null && dawnBackdrop != null &&
        actorOne != null && actorOne.GetComponent<Image>().sprite != null;
    public bool ReturnPortalReady => StoryLevel == 10 &&
        LevelCompletionRules.AreRequirementsMet(manager.enemyCount, manager.KeysCollected, manager.KeysRequired);
    private static readonly string[] OpeningLines = {
        "Cơn mưa đổ xuống giữa đường. Một phế tích bên sườn đồi vẫn còn mái che. Kỵ sĩ bước vào, chỉ định chờ mưa ngớt.",
        "Nhưng vừa qua ngưỡng cửa, tiếng mưa đã tắt. Phía sau chỉ còn một bức tường đá lạnh ngắt.",
        "Cửa đâu rồi? Ta vừa đi qua đây mà… Có ánh sáng phía trước. Phải tìm xem nơi này còn ai không."
    };
    private GameObject scenery, rainRoot, flameRoot, keyDais, returnPortal;
    private Image skyImage, backdropImage, flameImage, portalImage;
    private Sprite rainyBackdrop, dawnBackdrop;
    private RectTransform actorOne, actorTwo;
    private Image[] keyHalves;
    private TMP_Text sceneryCaption, returnCaption, inscriptionLeft, inscriptionRight;
    private readonly List<RectTransform> rainDrops = new();
    private readonly List<Sprite> presentationSprites = new();
    private Sprite[] fireFrames, returnFrames;
    private bool cinematicLayout;
    private readonly Dictionary<Renderer, bool> cinematicRenderers = new();
    private readonly Dictionary<Canvas, bool> cinematicCanvases = new();
    public bool WorldHiddenForPresentation => cinematicRenderers.Count > 0;

    private void LateUpdate()
    {
        bool cinematic = manager != null && manager.CurrentState == GameState.Story && (Opening || Ending);
        if (!cinematic) { RestoreWorldPresentation(); return; }
        // Chỉ ẩn phần hiển thị, không tắt actor/collider/logic mạng.
        // Quét cả đối tượng mới do snapshot LAN tạo trong chính frame này.
        foreach (var renderer in FindObjectsOfType<Renderer>())
        {
            if (!cinematicRenderers.ContainsKey(renderer)) cinematicRenderers.Add(renderer, renderer.forceRenderingOff);
            // Giữ enabled để bộ chụp world LAN vẫn gửi đủ sprite/mesh cho người vào lại.
            renderer.forceRenderingOff = true;
        }
        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            if (overlay != null && canvas.transform.IsChildOf(overlay.transform)) continue;
            if (!cinematicCanvases.ContainsKey(canvas)) cinematicCanvases.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
    }

    private void RestoreWorldPresentation()
    {
        foreach (var entry in cinematicRenderers) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
        foreach (var entry in cinematicCanvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
        cinematicRenderers.Clear(); cinematicCanvases.Clear();
    }

    public void BeginOpening()
    {
        if (!IsAuthority || StoryLevel != 1 || manager.CurrentState != GameState.Playing) return;
        Opening = true; Ending = false; StorySession++;
        LineIndex = 0; renderedLine = -1; finishAt = float.PositiveInfinity;
        manager.BeginStoneStory();
    }

    public void FinishOpeningPresentation()
    {
        if (!IsAuthority || !Opening || !IsComplete) return;
        Opening = false; LineIndex = 0; renderedLine = -1;
        finishAt = float.PositiveInfinity;
    }

    private Image Art(Transform parent, string name, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
    {
        var rect = Panel(name, parent, color); Place(rect, size, position);
        var image = rect.GetComponent<Image>(); image.raycastTarget = false;
        if (sprite != null) { image.sprite = sprite; image.preserveAspect = true; }
        return image;
    }

    private Sprite Frame(Texture2D texture, Rect rect)
    {
        if (texture == null) return null;
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 64, 0, SpriteMeshType.FullRect);
        presentationSprites.Add(sprite); return sprite;
    }

    private void LoadPresentationFrames()
    {
        if (fireFrames != null) return;
        fireFrames = new Sprite[7]; returnFrames = new Sprite[7];
        var fire = Resources.Load<Texture2D>("VFX/Combat/Fire");
        for (int i = 0; i < 7; i++)
        {
            if (fire != null) fireFrames[i] = Frame(fire, new Rect(i * fire.width / 7f, 0, fire.width / 7f, fire.height));
            var portal = Resources.Load<Texture2D>("Portal/Frames/portal1_frame_" + (i + 1));
            if (portal != null) returnFrames[i] = Frame(portal, new Rect(0, 0, portal.width, portal.height));
        }
    }

    private void BuildScenery()
    {
        LoadPresentationFrames();
        scenery = new GameObject("Phế tích — mưa và bình minh", typeof(RectTransform));
        scenery.transform.SetParent(overlay.transform, false);
        scenery.transform.SetAsFirstSibling();
        skyImage = Art(scenery.transform, "Nền tối ngoài cảnh", new Vector2(2560, 1440), Vector2.zero, new Color(.025f, .045f, .06f));
        var rainTexture = Resources.Load<Texture2D>("Story/RuinsRain");
        var dawnTexture = Resources.Load<Texture2D>("Story/RuinsDawn");
        if (rainTexture != null) rainyBackdrop = Frame(rainTexture, new Rect(0, 0, rainTexture.width, rainTexture.height));
        if (dawnTexture != null) dawnBackdrop = Frame(dawnTexture, new Rect(0, 0, dawnTexture.width, dawnTexture.height));
        backdropImage = Art(scenery.transform, "Phế tích pixel art", new Vector2(1280, 854), new Vector2(0, -10), Color.white, rainyBackdrop);
        Sprite knight = null;
        foreach (var renderer in manager.PlayerMovement.GetComponentsInChildren<SpriteRenderer>())
            if (renderer.sprite != null) { knight = renderer.sprite; break; }
        actorOne = Art(scenery.transform, "Kỵ sĩ tìm đường", new Vector2(540, 360), new Vector2(-280, 70), Color.white, knight).rectTransform;
        actorTwo = Art(scenery.transform, "Người đồng hành", new Vector2(540, 360), new Vector2(-390, 70), Color.white, knight).rectTransform;
        rainRoot = new GameObject("Mưa", typeof(RectTransform)); rainRoot.transform.SetParent(scenery.transform, false);
        for (int i = 0; i < 55; i++)
        {
            var drop = Art(rainRoot.transform, "Giọt mưa", new Vector2(2, 16 + i % 4 * 3), Vector2.zero, new Color(.65f, .79f, .88f, .45f)).rectTransform;
            drop.localRotation = Quaternion.Euler(0, 0, -17); rainDrops.Add(drop);
        }
        Art(scenery.transform, "Nền tên cảnh", new Vector2(480, 34), new Vector2(0, 333), new Color(.02f, .04f, .06f, .8f));
        sceneryCaption = Label(scenery.transform, "", 19, new Vector2(0, 333), new Vector2(1000, 34), Color.white);
        sceneryCaption.alignment = TextAlignmentOptions.Center;
    }

    private void TickScenery(bool show)
    {
        if (overlay == null) return;
        if (scenery == null) BuildScenery();
        bool active = show && (Opening || Ending);
        scenery.SetActive(active);
        SetCinematicLayout(active);
        var shade = overlay.transform.Find("Nền tối")?.GetComponent<Image>();
        if (shade != null) shade.color = new Color(.015f, .02f, .035f, active ? .08f : .72f);
        if (!active) return;
        int shot = Mathf.Min(LineIndex, 2);
        backdropImage.sprite = Opening && shot == 0 ? rainyBackdrop : dawnBackdrop;
        backdropImage.color = Opening && shot > 0 ? new Color(.27f, .36f, .48f) : Color.white;
        rainRoot.SetActive(Opening && shot == 0);
        actorOne.gameObject.SetActive(!Opening || shot == 0);
        actorTwo.gameObject.SetActive(actorOne.gameObject.activeSelf && LanLaunch.Mode != LanMode.Solo);
        actorOne.anchoredPosition = new Vector2(Opening ? -180 : 25 + shot * 70, 135);
        actorTwo.anchoredPosition = actorOne.anchoredPosition + new Vector2(-110, -5);
        // Cùng sprite của nhân vật khi chơi, nhận sắc trời của từng cảnh.
        actorOne.GetComponent<Image>().color = Opening ? new Color(.72f, .81f, .87f) : new Color(1f, .94f, .82f);
        actorTwo.GetComponent<Image>().color = Opening ? new Color(.67f, .79f, .85f) : new Color(.91f, .91f, .82f);
        sceneryCaption.text = Opening ? shot == 0 ? "MỘT CHỖ TRÚ MƯA" : "LỐI VÀO ĐÃ BIẾN MẤT" :
            shot < 2 ? "ÁNH SÁNG BÊN NGOÀI" : "CUỐI CÙNG CŨNG TRỞ VỀ";
        for (int i = 0; i < rainDrops.Count; i++)
            rainDrops[i].anchoredPosition = new Vector2(-640 + (i * 113 % 1280), 360 - Mathf.Repeat(Time.unscaledTime * 180 + i * 73, 310));
    }

    private void SetCinematicLayout(bool cinematic)
    {
        if (cinematicLayout == cinematic) return;
        cinematicLayout = cinematic;
        var card = body.transform.parent.GetComponent<RectTransform>();
        Place(card, new Vector2(1080, cinematic ? 286 : 376), new Vector2(0, cinematic ? -202 : -130));
        title.rectTransform.anchoredPosition = new Vector2(-50, cinematic ? 105 : 145);
        progress.rectTransform.anchoredPosition = new Vector2(445, cinematic ? 105 : 145);
        var rule = card.Find("Vệt sáng khế ước").GetComponent<RectTransform>();
        rule.anchoredPosition = new Vector2(0, cinematic ? 75 : 100);
        speaker.rectTransform.anchoredPosition = new Vector2(0, cinematic ? 45 : 68);
        body.rectTransform.sizeDelta = new Vector2(984, cinematic ? 90 : 120);
        body.fontSize = cinematic ? 24 : 28;
        hint.rectTransform.anchoredPosition = new Vector2(-210, cinematic ? -103 : -135);
        next.GetComponent<RectTransform>().anchoredPosition = new Vector2(388, cinematic ? -103 : -135);
        skip.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, cinematic ? -103 : -135);
    }

    private void BuildWorldPresentation()
    {
        LoadPresentationFrames();
        // Bệ là đá âm sàn và hốc lửa, không có khối nổi chắn người chơi.
        flameRoot = new GameObject("Hốc lửa âm sàn", typeof(RectTransform));
        flameRoot.transform.SetParent(mechanismVisual.transform, false);
        Art(flameRoot.transform, "Viền đá", new Vector2(90, 40), new Vector2(0, -10), new Color(.48f, .48f, .43f), EnemyEffectSpriteFactory.Disc);
        Art(flameRoot.transform, "Lòng hốc", new Vector2(70, 27), new Vector2(0, -8), new Color(.08f, .06f, .04f), EnemyEffectSpriteFactory.Disc);
        flameImage = Art(flameRoot.transform, "Lửa đang cháy", new Vector2(85, 100), new Vector2(0, 28), Color.white);
        keyDais = new GameObject("Bệ đá hai chìa — âm sàn", typeof(RectTransform));
        keyDais.transform.SetParent(mechanismVisual.transform, false);
        var daisTexture = Resources.Load<Texture2D>("Story/KeyDais");
        var daisSprite = daisTexture != null ? Frame(daisTexture, new Rect(0, 0, daisTexture.width, daisTexture.height)) : null;
        Art(keyDais.transform, "Bệ đá pixel art", new Vector2(200, 134), Vector2.zero, Color.white, daisSprite);
        keyHalves = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            keyHalves[i] = Art(keyDais.transform, "Chìa đã đặt " + (i + 1), new Vector2(36, 54),
                new Vector2(i == 0 ? -30 : 30, 6), new Color(1, .77f, .27f), DungeonKey.IconSprite);
        }
        inscriptionLeft = Label(keyDais.transform, "MẠNG SỐNG NHÀ VUA", 11, new Vector2(0, -53), new Vector2(240, 20), new Color(1, .84f, .51f));
        inscriptionRight = Label(keyDais.transform, "LÀ KHÓA CỦA MÊ CUNG", 11, new Vector2(0, -72), new Vector2(240, 20), new Color(1, .84f, .51f));
        inscriptionLeft.alignment = inscriptionRight.alignment = TextAlignmentOptions.Center;
        mechanismLabel.rectTransform.anchoredPosition = new Vector2(0, 105);
        returnPortal = new GameObject("Cổng trở về", typeof(RectTransform), typeof(Canvas));
        returnPortal.transform.SetParent(transform, false);
        returnPortal.transform.localScale = Vector3.one * .01f;
        returnPortal.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        returnPortal.GetComponent<Canvas>().sortingOrder = 45;
        portalImage = Art(returnPortal.transform, "Ánh bình minh trong cổng", new Vector2(140, 180), new Vector2(0, 35), Color.white);
        for (int side = -1; side <= 1; side += 2)
            Art(returnPortal.transform, "Khung cổng", new Vector2(11, 145), new Vector2(side * 72, 32), new Color(.85f, .69f, .34f));
        Art(returnPortal.transform, "Đỉnh cổng", new Vector2(155, 11), new Vector2(0, 106), new Color(.95f, .82f, .47f));
        returnCaption = Label(returnPortal.transform, "", 18, new Vector2(0, 138), new Vector2(460, 32), new Color(1, .86f, .55f));
        returnCaption.alignment = TextAlignmentOptions.Center;
    }

    private void TickWorldPresentation(bool inWorld)
    {
        if (flameRoot == null) BuildWorldPresentation();
        int frame = Mathf.FloorToInt(Time.unscaledTime * 9) % 7;
        flameRoot.SetActive(StoryLevel == 6);
        flameImage.gameObject.SetActive(StoryLevel == 6 && MechanismStage > 0);
        flameImage.sprite = fireFrames[frame];
        keyDais.SetActive(StoryLevel == 8);
        for (int i = 0; i < 2; i++) keyHalves[i].gameObject.SetActive(MechanismStage > i);
        inscriptionLeft.gameObject.SetActive(MechanismStage > 0);
        inscriptionRight.gameObject.SetActive(MechanismStage > 1);
        returnPortal.SetActive(StoryLevel == 10 && inWorld);
        returnPortal.transform.position = ExitPosition;
        portalImage.sprite = returnFrames[frame];
        portalImage.color = ReturnPortalReady ? new Color(1, .87f, .50f) : new Color(.32f, .22f, .23f, .7f);
        returnCaption.text = ReturnPortalReady ? "TRỞ VỀ · BƯỚC QUA CỔNG" : "LỐI VỀ ĐANG BỊ KHÓA";
    }
}
