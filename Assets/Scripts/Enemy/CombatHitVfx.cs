using System.Collections;
using UnityEngine;

public enum CombatImpactKind
{
    Sword,
    Heavy,
    SwordWave,
    SpinSlash,
    Ashen,
    Arrow,
    Bat,
    Mage,
    Boss,
    EnemyMelee
}

/// <summary>
/// One-shot impact animation shared by player skills and enemy hits. Frames are
/// sliced from the Tiny Swords effect sheets already included in this project.
/// The effect only draws art; damage remains with the existing combat scripts.
/// </summary>
public sealed class CombatHitVfx : MonoBehaviour
{
    private static Sprite[] explosionFrames;
    private static Sprite[] fireFrames;

    public static void Spawn(Vector3 position, CombatImpactKind kind, float scale = 1f)
    {
        Sprite[] frames = kind == CombatImpactKind.Ashen || kind == CombatImpactKind.Boss
            ? LoadFire() : LoadExplosion();
        if (frames == null || frames.Length == 0) return;

        GameObject effect = new GameObject($"{kind} Hit VFX");
        effect.transform.position = new Vector3(position.x, position.y, -0.5f);
        effect.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = Tint(kind);
        renderer.sortingOrder = 40;
        effect.AddComponent<CombatHitVfx>().StartCoroutine(Play(renderer, frames));
    }

    private static IEnumerator Play(SpriteRenderer renderer, Sprite[] frames)
    {
        const float duration = 0.36f;
        float elapsed = 0f;
        Color tint = renderer.color;
        while (renderer != null && elapsed < duration)
        {
            float progress = elapsed / duration;
            renderer.sprite = frames[Mathf.Min((int)(progress * frames.Length), frames.Length - 1)];
            renderer.color = new Color(tint.r, tint.g, tint.b,
                tint.a * Mathf.Clamp01((1f - progress) * 1.8f));
            yield return null;
            elapsed += Time.deltaTime;
        }
        if (renderer != null) Destroy(renderer.gameObject);
    }

    private static Sprite[] LoadExplosion()
    {
        if (explosionFrames == null)
            explosionFrames = Slice("VFX/Combat/Explosions", 9, 192);
        return explosionFrames;
    }

    private static Sprite[] LoadFire()
    {
        if (fireFrames == null)
            fireFrames = Slice("VFX/Combat/Fire", 7, 128);
        return fireFrames;
    }

    private static Sprite[] Slice(string path, int count, int cellSize)
    {
        Texture2D sheet = Resources.Load<Texture2D>(path);
        if (sheet == null || sheet.width != count * cellSize || sheet.height != cellSize)
        {
            Debug.LogWarning($"Impact sheet missing or wrong size: Resources/{path}");
            return System.Array.Empty<Sprite>();
        }
        sheet.filterMode = FilterMode.Point;
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Sprite.Create(sheet, new Rect(i * cellSize, 0, cellSize, cellSize),
                new Vector2(0.5f, 0.5f), cellSize);
        return frames;
    }

    private static Color Tint(CombatImpactKind kind)
    {
        switch (kind)
        {
            case CombatImpactKind.SwordWave: return new Color(0.35f, 0.91f, 1f);
            case CombatImpactKind.SpinSlash: return new Color(0.37f, 1f, 0.78f);
            case CombatImpactKind.Ashen: return new Color(1f, 0.54f, 0.22f);
            case CombatImpactKind.Arrow: return new Color(1f, 0.9f, 0.58f);
            case CombatImpactKind.Bat: return new Color(1f, 0.35f, 0.48f);
            case CombatImpactKind.Mage: return new Color(0.72f, 0.48f, 1f);
            case CombatImpactKind.Boss: return new Color(0.65f, 1f, 0.62f);
            case CombatImpactKind.EnemyMelee: return new Color(1f, 0.42f, 0.32f);
            case CombatImpactKind.Heavy: return new Color(1f, 0.73f, 0.36f);
            default: return Color.white;
        }
    }
}
