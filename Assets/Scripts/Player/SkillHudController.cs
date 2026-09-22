using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the combat skill HUD at runtime. The illustrated frame is layered over the
/// skill icon and cooldown fill, keeping a consistent premium look in every scene.
/// </summary>
public class SkillHudController : MonoBehaviour
{
    private const float SlotSize = 132f;
    private const float SlotGap = 14f;
    private const float LabelHeight = 22f;
    private const string FrameResourcePath = "UI/SkillHud/SkillSlotFrame_ObsidianCyan";

    private PlayerMovement movement;
    private PlayerSkillController skills;
    private HudSlot dashSlot;
    private HudSlot swordWaveSlot;
    private HudSlot spinSlashSlot;
    private HudSlot ultimateSlot;

    private void Start()
    {
        movement = GetComponent<PlayerMovement>();
        skills = GetComponent<PlayerSkillController>();
        CreateHud();
    }

    private void Update()
    {
        if (movement == null || skills == null)
            return;

        dashSlot.SetCooldown(movement.DashCooldownNormalized, new Color(0.20f, 0.85f, 1f));
        swordWaveSlot.SetCooldown(skills.SwordWaveCooldownNormalized, new Color(0.20f, 0.85f, 1f));
        spinSlashSlot.SetCooldown(skills.SpinSlashCooldownNormalized, new Color(0.33f, 0.92f, 1f));
        ultimateSlot.SetUltimate(skills.UltimateNormalized);
    }

    private void CreateHud()
    {
        GameObject canvasObject = new GameObject("Combat Skill HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject holder = new GameObject("Skill Slots");
        holder.transform.SetParent(canvasObject.transform, false);
        RectTransform holderRect = holder.AddComponent<RectTransform>();
        holderRect.anchorMin = new Vector2(0.5f, 0f);
        holderRect.anchorMax = new Vector2(0.5f, 0f);
        holderRect.pivot = new Vector2(0.5f, 0f);
        holderRect.sizeDelta = new Vector2(SlotSize * 4f + SlotGap * 3f, SlotSize + LabelHeight);
        holderRect.anchoredPosition = new Vector2(0f, 28f);

        Sprite frameSprite = LoadSprite(FrameResourcePath);
        dashSlot = CreateSlot(holder.transform, 0, "SHIFT", "DASH", "UI/Skills/IconDash", frameSprite);
        swordWaveSlot = CreateSlot(holder.transform, 1, "Q", "SWORD WAVE", "UI/Skills/IconSwordWave", frameSprite);
        spinSlashSlot = CreateSlot(holder.transform, 2, "E", "SPIN SLASH", "UI/Skills/IconSpinSlash", frameSprite);
        ultimateSlot = CreateSlot(holder.transform, 3, "R", "ASHEN JUDGMENT", "UI/Skills/IconAshenJudgment", frameSprite);
    }

    private static HudSlot CreateSlot(Transform parent, int index, string key, string label, string iconPath, Sprite frameSprite)
    {
        GameObject slot = new GameObject($"Skill Slot {key}");
        slot.transform.SetParent(parent, false);
        RectTransform slotRect = slot.AddComponent<RectTransform>();
        slotRect.anchorMin = Vector2.zero;
        slotRect.anchorMax = Vector2.zero;
        slotRect.pivot = Vector2.zero;
        slotRect.sizeDelta = new Vector2(SlotSize, SlotSize + LabelHeight);
        slotRect.anchoredPosition = new Vector2(index * (SlotSize + SlotGap), 0f);

        Image baseImage = CreateImage(slot.transform, "Obsidian Backplate", new Color(0.015f, 0.028f, 0.04f, 0.97f));
        SetAnchors(baseImage.rectTransform, new Vector2(0f, LabelHeight / (SlotSize + LabelHeight)), Vector2.one, 7f);

        CreateIcon(slot.transform, iconPath);

        Image cooldown = CreateImage(slot.transform, "Cooldown Fill", new Color(0f, 0.01f, 0.02f, 0.72f));
        SetAnchors(cooldown.rectTransform, new Vector2(0.18f, 0.18f + LabelHeight / (SlotSize + LabelHeight)), new Vector2(0.82f, 0.82f), 0f);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Vertical;
        cooldown.fillOrigin = 1;

        Image frame = CreateImage(slot.transform, "Obsidian Cyan Frame", Color.white);
        frame.sprite = frameSprite;
        frame.preserveAspect = true;
        SetAnchors(frame.rectTransform, new Vector2(0f, LabelHeight / (SlotSize + LabelHeight)), Vector2.one, 0f);

        Image keyBadge = CreateImage(slot.transform, "Key Badge", new Color(0.018f, 0.075f, 0.10f, 0.96f));
        SetAnchors(keyBadge.rectTransform, new Vector2(0.10f, 0.69f), new Vector2(0.40f, 0.89f), 0f);
        Text keyText = CreateText(keyBadge.transform, key, 17, FontStyle.Bold, new Color(0.58f, 0.93f, 1f));
        Stretch(keyText.rectTransform, 1f);

        Image labelPlate = CreateImage(slot.transform, "Skill Name Plate", new Color(0.006f, 0.014f, 0.022f, 0.92f));
        SetAnchors(labelPlate.rectTransform, Vector2.zero, new Vector2(1f, LabelHeight / (SlotSize + LabelHeight)), 0f);
        Text labelText = CreateText(labelPlate.transform, label, 11, FontStyle.Bold, new Color(0.63f, 0.88f, 0.96f));
        Stretch(labelText.rectTransform, 1f);

        return new HudSlot(baseImage, cooldown, frame, keyBadge, keyText, labelText);
    }

    private static void CreateIcon(Transform parent, string resourcePath)
    {
        Sprite sprite = LoadSprite(resourcePath);
        if (sprite == null)
        {
            Debug.LogWarning($"Missing skill HUD icon: {resourcePath}");
            return;
        }

        Image icon = CreateImage(parent, "Skill Icon", Color.white);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        SetAnchors(icon.rectTransform, new Vector2(0.19f, 0.22f + LabelHeight / (SlotSize + LabelHeight)), new Vector2(0.81f, 0.79f), 0f);
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        return texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string value, int size, FontStyle style, Color color)
    {
        GameObject textObject = new GameObject(value);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, float inset)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private sealed class HudSlot
    {
        private readonly Image background;
        private readonly Image cooldown;
        private readonly Image frame;
        private readonly Image keyBadge;
        private readonly Text key;
        private readonly Text label;

        public HudSlot(Image background, Image cooldown, Image frame, Image keyBadge, Text key, Text label)
        {
            this.background = background;
            this.cooldown = cooldown;
            this.frame = frame;
            this.keyBadge = keyBadge;
            this.key = key;
            this.label = label;
        }

        public void SetCooldown(float normalizedCooldown, Color readyColor)
        {
            bool ready = normalizedCooldown <= 0.01f;
            cooldown.fillAmount = Mathf.Clamp01(normalizedCooldown);
            Color contentColor = ready ? readyColor : new Color(0.53f, 0.61f, 0.66f, 1f);
            key.color = contentColor;
            label.color = contentColor;
            keyBadge.color = ready ? new Color(readyColor.r * 0.12f, readyColor.g * 0.12f, readyColor.b * 0.12f, 0.98f) : new Color(0.025f, 0.03f, 0.035f, 0.96f);
            background.color = ready ? new Color(readyColor.r * 0.035f, readyColor.g * 0.045f, readyColor.b * 0.055f, 0.98f) : new Color(0.012f, 0.016f, 0.02f, 0.98f);
            frame.color = ready ? Color.white : new Color(0.45f, 0.50f, 0.54f, 0.95f);
        }

        public void SetUltimate(float charge)
        {
            float clampedCharge = Mathf.Clamp01(charge);
            bool ready = clampedCharge >= 0.999f;
            Color color = ready ? new Color(1f, 0.58f, 0.18f, 1f) : new Color(0.62f, 0.76f, 0.86f, 1f);
            cooldown.fillAmount = 1f - clampedCharge;
            key.color = color;
            label.color = color;
            keyBadge.color = ready ? new Color(0.22f, 0.07f, 0.015f, 0.98f) : new Color(0.035f, 0.05f, 0.065f, 0.96f);
            background.color = ready ? new Color(0.12f, 0.025f, 0.008f, 0.98f) : new Color(0.025f, 0.02f, 0.035f, 0.98f);
            frame.color = ready ? new Color(1f, 0.75f, 0.32f, 1f) : new Color(0.77f, 0.84f, 0.90f, 1f);
        }
    }
}
