using UnityEngine;

/// <summary>
/// Gold coin pickup. Walk near it to collect and gain gold.
/// Attach to a GameObject with a Collider2D (Is Trigger) and a SpriteRenderer.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoldPickup : MonoBehaviour
{
    [Header("Gold Settings")]
    [Tooltip("Amount of gold gained on pickup.")]
    public int goldAmount = 100;

    [Header("Visual")]
    [Tooltip("Gentle bob amplitude.")]
    public float bobAmplitude = 0.1f;
    [Tooltip("Bob frequency.")]
    public float bobFrequency = 3f;
    [Tooltip("Sparkle color.")]
    public Color sparkleColor = new Color(1f, 0.85f, 0f, 1f);

    private Vector3 startPos;
    private bool collected;
    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        startPos = transform.position;
        GetComponent<Collider2D>().isTrigger = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = 5;
    }

    private void Update()
    {
        if (collected) return;

        // Gentle bob
        float y = startPos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(startPos.x, y, startPos.z);

        // Subtle color pulse
        if (spriteRenderer != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
            spriteRenderer.color = new Color(1f, 1f, 1f, pulse);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        collected = true;

        // Add gold to player stats
        if (player.playerStats != null)
        {
            player.playerStats.AddGold(goldAmount);
            Debug.Log($"Picked up {goldAmount} gold! Total: {player.playerStats.Gold}");
        }

        // TODO: Play gold pickup SFX & particle effect
        Destroy(gameObject);
    }
}
