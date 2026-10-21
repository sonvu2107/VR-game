using UnityEngine;

/// <summary>World-space health bar that follows the enlarged campaign final boss.</summary>
[DisallowMultipleComponent]
public sealed class FinalBossHealthBar : MonoBehaviour
{
    // WorldDepthSort can place actors and combat VFX as high as 29008.
    // Keep the boss bar above that range so the body can never cover it.
    private const int BarOrder = 32000;

    private EnemyHealth health;
    private Boss3DVisual bossVisual;
    private SpriteRenderer[] bossRenderers;
    private SpriteRenderer background;
    private SpriteRenderer fill;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        bossVisual = GetComponent<Boss3DVisual>();
        bossRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        CreateBar();
    }

    private void LateUpdate()
    {
        if (health == null || health.IsDead)
        {
            SetVisible(false);
            return;
        }

        UpdateBar();
    }

    private void CreateBar()
    {
        Sprite white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f), 1f);
        white.name = "FinalBossHealthBarPixel";

        background = CreateRenderer("Final Boss Health Bar Background", white,
            new Color(0.10f, 0.015f, 0.025f, 0.96f), BarOrder);
        fill = CreateRenderer("Final Boss Health Bar Fill", white,
            new Color(0.96f, 0.10f, 0.18f, 1f), BarOrder + 1);
        UpdateBar();
    }

    private static SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject bar = new GameObject(objectName);
        SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateBar()
    {
        if (background == null || fill == null || health == null) return;

        Bounds bounds = GetVisualBounds();
        float width = Mathf.Clamp(bounds.size.x * 1.25f, 2.8f, 6.5f);
        float headClearance = Mathf.Max(0.72f, bounds.size.y * 0.18f);
        Vector3 position = new Vector3(bounds.center.x, bounds.max.y + headClearance, transform.position.z);
        float ratio = Mathf.Clamp01(health.HealthRatio);

        SpriteRenderer body = GetBodyRenderer();
        if (body != null)
        {
            background.sortingLayerID = body.sortingLayerID;
            fill.sortingLayerID = body.sortingLayerID;
        }
        background.sortingOrder = BarOrder;
        fill.sortingOrder = BarOrder + 1;

        background.transform.position = position;
        background.transform.localScale = new Vector3(width, 0.30f, 1f);
        fill.transform.position = position + Vector3.left * width * (1f - ratio) * 0.5f;
        fill.transform.localScale = new Vector3(Mathf.Max(0.001f, width * ratio), 0.18f, 1f);
        SetVisible(true);
    }

    private Bounds GetVisualBounds()
    {
        SpriteRenderer body = GetBodyRenderer();
        if (body != null)
            return body.bounds;

        // Runtime rigs can add their display renderer after this component's
        // Awake. Refresh the fallback list so the bar does not follow stale
        // source sprites or ground decorations.
        bossRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        foreach (SpriteRenderer visual in bossRenderers)
        {
            if (visual == null || !visual.enabled || visual.sprite == null) continue;
            if (visual.name.Contains("Ground Shadow") || visual.name.Contains("Rune Circle")) continue;
            if (!found) { bounds = visual.bounds; found = true; }
            else bounds.Encapsulate(visual.bounds);
        }
        return bounds;
    }

    private SpriteRenderer GetBodyRenderer()
    {
        bossVisual ??= GetComponent<Boss3DVisual>();
        SpriteRenderer body = bossVisual != null ? bossVisual.DisplayRenderer : null;
        if (body != null && body.enabled && body.sprite != null)
            return body;

        SpriteRenderer root = GetComponent<SpriteRenderer>();
        return root != null && root.enabled && root.sprite != null ? root : null;
    }

    private void SetVisible(bool value)
    {
        if (background != null) background.enabled = value;
        if (fill != null) fill.enabled = value;
    }

    private void OnDestroy()
    {
        if (background != null) Destroy(background.gameObject);
        if (fill != null) Destroy(fill.gameObject);
    }
}
