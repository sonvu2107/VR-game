using UnityEngine;

/// <summary>
/// Creates and manages a floating health bar above the player's head.
/// The bar is rendered using two SpriteRenderers (background + fill)
/// and automatically handles player flipping.
/// Attach to the Player GameObject — it creates visuals automatically.
/// </summary>
public class FloatingHealthBar : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Offset above the player's pivot point.")]
    public Vector3 offset = new Vector3(0f, 1.3f, 0f);

    [Header("Size")]
    public float barWidth = 1.2f;
    public float barHeight = 0.14f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    public Color fullHealthColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    public Color lowHealthColor = new Color(0.95f, 0.15f, 0.15f, 1f);
    public Color borderColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);

    [Header("Sorting")]
    [Tooltip("Sorting order to ensure bar is above other sprites.")]
    public int sortingOrder = 100;

    // Internal references
    private GameObject barRoot;      // Separate root so flipping doesn't affect it
    private SpriteRenderer bgRenderer;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer borderRenderer;

    private Sprite whiteSprite;
    private float currentFill = 1f;

    private void Start()
    {
        CreateBarVisuals();
    }

    private void CreateBarVisuals()
    {
        // Create a 1x1 white pixel texture for all bar elements
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        // Sprite with pivot at LEFT-CENTER (0, 0.5) for fill scaling from left
        Sprite leftPivot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        // Sprite with pivot at CENTER for background & border
        Sprite centerPivot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        whiteSprite = leftPivot;

        // Create a root object that is NOT parented to player (avoids flip issues)
        barRoot = new GameObject("FloatingHealthBar_Root");

        // --- Border (slightly larger than BG) ---
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(barRoot.transform);
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.06f, 1f);
        borderRenderer = borderObj.AddComponent<SpriteRenderer>();
        borderRenderer.sprite = centerPivot;
        borderRenderer.color = borderColor;
        borderRenderer.sortingOrder = sortingOrder;

        // --- Background ---
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(barRoot.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = centerPivot;
        bgRenderer.color = backgroundColor;
        bgRenderer.sortingOrder = sortingOrder + 1;

        // --- Fill (anchored at left edge) ---
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barRoot.transform);
        fillObj.transform.localPosition = new Vector3(-barWidth / 2f, 0f, 0f);
        fillObj.transform.localScale = new Vector3(barWidth, barHeight - 0.02f, 1f);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = leftPivot;
        fillRenderer.color = fullHealthColor;
        fillRenderer.sortingOrder = sortingOrder + 2;
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        // Follow the player's position + offset (independent of flip)
        barRoot.transform.position = transform.position + offset;
    }

    /// <summary>
    /// Update the health bar fill amount. 
    /// normalizedHealth = currentHP / maxHP (0 to 1).
    /// </summary>
    public void UpdateBar(float normalizedHealth)
    {
        currentFill = Mathf.Clamp01(normalizedHealth);

        if (fillRenderer != null)
        {
            // Scale X to match fill
            fillRenderer.transform.localScale = new Vector3(barWidth * currentFill, barHeight - 0.02f, 1f);
            // Color: green when full, red when low
            fillRenderer.color = Color.Lerp(lowHealthColor, fullHealthColor, currentFill);
        }
    }

    private void OnDestroy()
    {
        if (barRoot != null)
            Destroy(barRoot);
    }

    private void OnDisable()
    {
        if (barRoot != null)
            barRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (barRoot != null)
            barRoot.SetActive(true);
    }
}
