using UnityEngine;

/// <summary>World-space health bar that follows the enlarged campaign final boss.</summary>
[DisallowMultipleComponent]
public sealed class FinalBossHealthBar : MonoBehaviour
{
    private const int BarOrder = 50;

    private EnemyHealth health;
    private SpriteRenderer[] bossRenderers;
    private SpriteRenderer background;
    private SpriteRenderer fill;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
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
        Vector3 position = new Vector3(bounds.center.x, bounds.max.y + 0.48f, transform.position.z);
        float ratio = Mathf.Clamp01(health.HealthRatio);

        background.transform.position = position;
        background.transform.localScale = new Vector3(width, 0.24f, 1f);
        fill.transform.position = position + Vector3.left * width * (1f - ratio) * 0.5f;
        fill.transform.localScale = new Vector3(Mathf.Max(0.001f, width * ratio), 0.14f, 1f);
        SetVisible(true);
    }

    private Bounds GetVisualBounds()
    {
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        foreach (SpriteRenderer visual in bossRenderers)
        {
            if (visual == null || !visual.enabled || visual.sprite == null) continue;
            if (!found) { bounds = visual.bounds; found = true; }
            else bounds.Encapsulate(visual.bounds);
        }
        return bounds;
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
