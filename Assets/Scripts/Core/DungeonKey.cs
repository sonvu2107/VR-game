using UnityEngine;
using TMPro;

/// <summary>
/// Pickup item that adds a key to the GameManager.
/// Hidden near decorations but glows/pulses when the player is nearby.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DungeonKey : MonoBehaviour
{
    [Header("Visual")]
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2f;
    [Tooltip("Scale multiplier to make key more visible.")]
    public float keyScale = 2.5f;

    [Header("Proximity Hint")]
    [Tooltip("Distance at which the key starts glowing to hint the player.")]
    public float hintRadius = 6f;
    [Tooltip("Distance at which the key glows at full brightness.")]
    public float fullGlowRadius = 3f;
    [Tooltip("Glow color when near the player.")]
    public Color glowColor = new Color(1f, 0.9f, 0.3f, 1f);

    private Vector3 startPos;
    private bool collected;
    private Transform playerTransform;
    private SpriteRenderer spriteRenderer;
    private GameObject glowChild;
    private SpriteRenderer glowRenderer;

    private void Start()
    {
        startPos = transform.position;
        GetComponent<Collider2D>().isTrigger = true;

        // Make key bigger so it's clearly visible
        transform.localScale = Vector3.one * keyScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = 5; // Visible above floor

        // Find the player
        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
            playerTransform = pm.transform;

        // Create a glow child sprite that renders ABOVE decorations
        CreateGlow();
    }

    private void CreateGlow()
    {
        glowChild = new GameObject("KeyGlow");
        glowChild.transform.SetParent(transform);
        glowChild.transform.localPosition = Vector3.zero;
        glowChild.transform.localScale = Vector3.one * 2f;

        glowRenderer = glowChild.AddComponent<SpriteRenderer>();

        // Use the same sprite as the key, but larger and glowing
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            glowRenderer.sprite = spriteRenderer.sprite;
        }
        else
        {
            // Fallback: create a simple white circle
            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                    float alpha = Mathf.Clamp01(1f - dist / 16f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            glowRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
        }

        glowRenderer.sortingOrder = 10; // Above everything
        glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f); // Start invisible
        glowRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (collected) return;

        // Gentle floating bob
        float y = startPos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(startPos.x, y, startPos.z);

        // Proximity glow effect
        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glowRenderer == null || playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > hintRadius)
        {
            // Too far — completely invisible
            glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            return;
        }

        // Calculate glow intensity: 0 at hintRadius, 1 at fullGlowRadius
        float t = Mathf.InverseLerp(hintRadius, fullGlowRadius, dist);

        // Pulsing effect that gets stronger as player gets closer
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        float alpha = Mathf.Lerp(0.1f, 0.6f, t) * Mathf.Lerp(0.6f, 1f, pulse);

        glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);

        // Scale pulse — glow gets slightly bigger when pulsing
        float scale = Mathf.Lerp(1.5f, 2.2f, pulse * t);
        glowChild.transform.localScale = Vector3.one * scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        collected = true;

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.KeyCollected();
        }

        // Show pickup notification
        SpawnPickupText();

        Destroy(gameObject);
    }

    /// <summary>
    /// Creates a floating "+1 KEY!" text that rises and fades out.
    /// </summary>
    private void SpawnPickupText()
    {
        GameObject popup = new GameObject("KeyPickupPopup");
        popup.transform.position = transform.position + Vector3.up * 0.5f;

        TextMeshPro tmp = popup.AddComponent<TextMeshPro>();
        tmp.text = "<color=#FFD700>+1 KEY!</color>";
        tmp.fontSize = 5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;

        // Animate: rise up and fade out
        popup.AddComponent<FloatingPopup>();

        Destroy(popup, 1.5f);
    }
}

/// <summary>
/// Simple component to animate a text popup rising and fading.
/// </summary>
public class FloatingPopup : MonoBehaviour
{
    private float elapsed;
    private Vector3 startPos;
    private TextMeshPro tmp;

    void Start()
    {
        startPos = transform.position;
        tmp = GetComponent<TextMeshPro>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position = startPos + Vector3.up * (elapsed * 1.5f);

        if (tmp != null)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / 1.5f);
            tmp.alpha = alpha;
        }
    }
}
