using System.Collections;
using UnityEngine;

/// <summary>
/// Spike trap that periodically activates and damages the player.
/// Auto-creates visuals if no sprite is assigned.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage dealt when spikes are active and player touches them.")]
    public int damage = 2;
    [Tooltip("Cooldown between damage ticks to prevent instant death.")]
    public float damageCooldown = 1f;

    [Header("Timing")]
    [Tooltip("Seconds the spikes stay retracted (safe).")]
    public float retractedTime = 2f;
    [Tooltip("Warning time before spikes extend (visual cue).")]
    public float warningTime = 0.5f;
    [Tooltip("Seconds the spikes stay extended (dangerous).")]
    public float extendedTime = 1.5f;

    [Header("Visual")]
    public float trapScale = 3.0f; // Made trap much bigger
    [Tooltip("Color when spikes are retracted (safe).")]
    public Color safeColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
    [Tooltip("Color during warning phase.")]
    public Color warningColor = new Color(1f, 0.7f, 0.1f, 0.9f);
    [Tooltip("Color when spikes are extended (danger).")]
    public Color dangerColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Floor Scaling")]
    public int extraDamagePerFloor = 0;

    private SpriteRenderer spriteRenderer;
    private bool isActive;
    private float damageCooldownTimer;
    private Vector3 baseScale;

    private void Start()
    {
        // Ensure we have a SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Auto-create spike sprite if none assigned
        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = CreateSpikeSprite();
        }

        // Make trap clearly visible
        spriteRenderer.sortingOrder = 4; // Above floor tiles
        transform.localScale = Vector3.one * trapScale;
        baseScale = transform.localScale;

        // Collider setup
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // Floor scaling
        GameManager gm = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();
        if (gm != null)
            damage += extraDamagePerFloor * Mathf.Max(0, gm.CurrentFloor - 1);

        StartCoroutine(TrapCycle());
    }

    private void Update()
    {
        if (damageCooldownTimer > 0)
            damageCooldownTimer -= Time.deltaTime;
    }

    private IEnumerator TrapCycle()
    {
        while (true)
        {
            // Retracted (safe)
            isActive = false;
            SetVisual(safeColor, 1f);
            yield return new WaitForSeconds(retractedTime);

            // Warning — shake to alert player
            SetVisual(warningColor, 1f);
            float elapsed = 0f;
            while (elapsed < warningTime)
            {
                float shake = Mathf.Sin(elapsed * 40f) * 0.05f;
                transform.localScale = baseScale * (1f + shake);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Extended (danger!)
            isActive = true;
            SetVisual(dangerColor, 1.2f);
            yield return new WaitForSeconds(extendedTime);
        }
    }

    private void SetVisual(Color color, float scaleMultiplier)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;

        transform.localScale = baseScale * scaleMultiplier;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;
        if (damageCooldownTimer > 0) return;

        // Use GetComponentInParent in case the collider is on a child object (hitbox)
        HealthController hc = other.GetComponentInParent<HealthController>();
        if (hc != null)
        {
            hc.TakeDamage(damage);
            damageCooldownTimer = damageCooldown;
        }
    }

    /// <summary>
    /// Creates a visible spike-like diamond sprite at runtime.
    /// </summary>
    private Sprite CreateSpikeSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0, 0, 0, 0);
        Color spike = new Color(0.8f, 0.2f, 0.2f, 1f);
        Color outline = new Color(0.3f, 0.05f, 0.05f, 1f);

        // Clear
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                tex.SetPixel(x, y, clear);

        // Draw a diamond/spike shape
        int cx = size / 2;
        int cy = size / 2;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = Mathf.Abs(x - cx) + Mathf.Abs(y - cy); // Manhattan distance = diamond
                if (dist < 12)
                    tex.SetPixel(x, y, spike);
                else if (dist < 14)
                    tex.SetPixel(x, y, outline);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }
}
