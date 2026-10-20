using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives the LAN companion the Colour2 FreeKnight artwork while retaining the
/// original Animator, hit boxes, movement and skill controllers.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CompanionVisual : MonoBehaviour
{
    private static readonly Dictionary<Sprite, Sprite> variants = new();
    private static readonly Dictionary<string, Texture2D> sheets = new();
    private SpriteRenderer body;

    private void Awake() => body = GetComponent<SpriteRenderer>();

    private void LateUpdate()
    {
        if (body == null || body.sprite == null) return;
        Sprite source = body.sprite;
        if (source.name.StartsWith("Companion ")) return;
        Sprite replacement = Alternate(source);
        if (replacement != null) body.sprite = replacement;
    }

    public static Sprite Alternate(Sprite source)
    {
        if (source == null || source.texture == null) return null;
        if (variants.TryGetValue(source, out Sprite cached)) return cached;
        string sheetName = source.texture.name;
        if (!sheets.TryGetValue(sheetName, out Texture2D sheet))
        {
            sheet = Resources.Load<Texture2D>("Story/Companion/" + sheetName);
            if (sheet != null) sheet.filterMode = FilterMode.Point;
            sheets[sheetName] = sheet;
        }
        if (sheet == null || source.rect.xMax > sheet.width || source.rect.yMax > sheet.height)
            return null;

        Vector2 pivot = new(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
        Sprite variant = Sprite.Create(sheet, source.rect, pivot, source.pixelsPerUnit,
            0, SpriteMeshType.FullRect);
        variant.name = "Companion " + source.name;
        variants[source] = variant;
        return variant;
    }
}
