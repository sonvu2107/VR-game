using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared pixel artwork, without changing callbacks, values, or gameplay state.</summary>
public static class OrnateUITheme
{
    public static readonly Color Text = new Color(.96f, .91f, .80f);
    public static readonly Color Gold = new Color(.96f, .48f, .48f);
    public static readonly Color Background = new Color(.075f, .027f, .035f);
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Sprite Sprite(string name)
    {
        if (!sprites.TryGetValue(name, out var value) || value == null)
            sprites[name] = value = Resources.Load<Sprite>("UI/Ornate/" + name);
        return value;
    }

    public static void Panel(Image image, string name = "panel_ornate", float pixelScale = 2)
    {
        if (image == null) return;
        var sprite = Sprite(name);
        if (sprite == null) return;
        image.sprite = sprite;
        image.overrideSprite = null;
        image.color = Color.white;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 1 / pixelScale;
    }

    public static void Button(Button button, bool widget = false)
    {
        if (button == null) return;
        var image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        if (image == null) return;
        string prefix = widget ? "widget_" : "button_";
        Panel(image, prefix + "normal", 1.5f);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState {
            highlightedSprite = Sprite(prefix + "hover"),
            selectedSprite = Sprite(prefix + "hover"),
            pressedSprite = Sprite(widget ? "widget_selected" : "button_pressed"),
            disabledSprite = Sprite(prefix + "disabled")
        };
        foreach (var child in button.GetComponentsInChildren<Image>(true))
            if (child.transform.parent == button.transform && child != image &&
                child.name == "Image" && child.sprite == null)
                child.enabled = false; // Old scene cover for the baked-in "Play" lettering.
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = Text;
            label.font = Resources.Load<TMP_FontAsset>("Story/StoryFont") ?? label.font;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = Vector4.zero;
            var rect = label.rectTransform;
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14, 6);
            rect.offsetMax = new Vector2(-14, -6);
        }
    }

    public static void Mark(Button button, bool selected)
    {
        var image = button.targetGraphic as Image;
        if (image != null) image.sprite = Sprite(selected ? "button_hover" : "button_normal");
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = selected ? Gold : Text;
    }

    public static void Input(TMP_InputField field)
    {
        Panel(field.GetComponent<Image>(), "widget_normal", 1);
        field.textComponent.color = Text;
        field.selectionColor = new Color(.65f, .18f, .25f, .6f);
        field.caretColor = Gold;
    }

    public static void Slider(Slider slider)
    {
        var background = slider.transform.Find("Background");
        if (background != null) Panel(background.GetComponent<Image>(), "stat_frame", 1);
        if (slider.fillRect != null) Panel(slider.fillRect.GetComponent<Image>(), "fill_mana", 1);
        if (slider.handleRect != null)
        {
            var image = slider.handleRect.GetComponent<Image>();
            Panel(image, "scroll_thumb", 1);
            slider.targetGraphic = image;
            slider.transition = Selectable.Transition.SpriteSwap;
            slider.spriteState = new SpriteState {
                highlightedSprite = Sprite("scroll_thumb_hover"), selectedSprite = Sprite("scroll_thumb_hover"),
                pressedSprite = Sprite("scroll_thumb_hover"), disabledSprite = Sprite("scroll_thumb") };
        }
    }

    public static void Scrollbar(Scrollbar bar)
    {
        Panel(bar.GetComponent<Image>(), "scroll_track", 1);
        if (bar.handleRect == null) return;
        Panel(bar.handleRect.GetComponent<Image>(), "scroll_thumb", 1);
        bar.targetGraphic = bar.handleRect.GetComponent<Image>();
        bar.transition = Selectable.Transition.SpriteSwap;
        bar.spriteState = new SpriteState { highlightedSprite = Sprite("scroll_thumb_hover"),
            selectedSprite = Sprite("scroll_thumb_hover"), pressedSprite = Sprite("scroll_thumb_hover"),
            disabledSprite = Sprite("scroll_thumb") };
    }
}
