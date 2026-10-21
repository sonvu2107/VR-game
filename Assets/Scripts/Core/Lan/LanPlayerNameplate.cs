using TMPro;
using UnityEngine;

/// <summary>Local world-space text; names and anchors come from authoritative player state.</summary>
public sealed class LanPlayerNameplate : MonoBehaviour
{
    private TMP_Text label;

    public static LanPlayerNameplate Create(Transform owner, int slot)
    {
        var go = new GameObject("LAN Player Name " + slot, typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(owner, false);
        go.transform.localScale = Vector3.one * .01f;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        var plate = go.AddComponent<LanPlayerNameplate>();
        var text = new GameObject("Display Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(go.transform, false);
        plate.label = text.GetComponent<TMP_Text>();
        plate.label.rectTransform.sizeDelta = new Vector2(260, 30);
        plate.label.font = Resources.Load<TMP_FontAsset>("Story/StoryFont") ?? TMP_Settings.defaultFontAsset;
        plate.label.fontSize = 24;
        plate.label.enableAutoSizing = true;
        plate.label.fontSizeMin = 12;
        plate.label.fontSizeMax = 24;
        plate.label.alignment = TextAlignmentOptions.Center;
        plate.label.enableWordWrapping = false;
        plate.label.richText = false;
        plate.label.raycastTarget = false;
        plate.label.color = slot == 0 ? new Color(1f,.89f,.63f) : new Color(.65f,.91f,1f);
        plate.label.outlineColor = new Color32(12,12,16,255);
        plate.label.outlineWidth = .25f;
        go.SetActive(false);
        return plate;
    }

    // Đo một lần ở tư thế ban đầu, bỏ vùng trong suốt của canvas sprite.
    // Giữ chiều cao ổn định khi chạy/đánh để tên không nhấp nhô theo vũ khí.
    public static float MeasureHead(SpriteRenderer body)
    {
        Sprite sprite = body.sprite;
        Texture2D source = sprite.texture;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            Color32[] pixels = copy.GetPixels32();
            Rect rect = sprite.rect;
            for (int y = (int)rect.yMax - 1; y >= (int)rect.yMin; y--)
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                    if (pixels[y * source.width + x].a > 32)
                        return (y + 1 - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
            return sprite.bounds.max.y;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Destroy(copy);
        }
    }

    public void Present(string name, Vector3 anchor, bool visible, bool interpolate)
    {
        bool wasVisible = gameObject.activeSelf;
        gameObject.SetActive(visible && !string.IsNullOrEmpty(name));
        if (!gameObject.activeSelf) return;
        if (label.text != name)
        {
            label.font.TryAddCharacters(name);
            label.text = name;
        }
        transform.position = !interpolate || !wasVisible || (anchor - transform.position).sqrMagnitude > 16f
            ? anchor : Vector3.Lerp(transform.position, anchor, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        transform.rotation = Quaternion.identity;
    }
}
