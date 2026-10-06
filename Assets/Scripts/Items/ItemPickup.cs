using UnityEngine;

/// <summary>
/// Attach to the item drop prefab. When the player walks over it the
/// item is collected: stats are applied to PlayerStats and, if a
/// HealthController exists, instant healing is triggered.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class ItemPickup : MonoBehaviour
{
    [Tooltip("Drag the ItemDataSO asset here, or let ItemDropper assign it at runtime.")]
    public ItemDataSO itemData;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 2f;

    [Header("Spawn")]
    [Tooltip("Seconds before this item can be picked up (prevents instant collection).")]
    [SerializeField] private float pickupDelay = 1.0f;
    [Tooltip("How far the item 'pops' away from spawn point.")]
    [SerializeField] private float spawnPopDistance = 1.2f;

    private Vector3 startPos;
    private bool collected;
    private float spawnTime;
    private bool ready; // can be picked up?

    private void Awake()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        spawnTime = Time.time;
        ready = false;

        // Pop the item out in a random direction so it doesn't sit on top of the player
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        transform.position += (Vector3)randomDir * spawnPopDistance;

        startPos = transform.position;

        // Start small and grow (spawn animation)
        transform.localScale = Vector3.zero;

        if (itemData != null && spriteRenderer != null)
        {
            if (itemData.icon != null)
                spriteRenderer.sprite = itemData.icon;

            spriteRenderer.color = itemData.glowColor;
        }
    }

    private void Update()
    {
        float elapsed = Time.time - spawnTime;

        // Spawn animation: scale up over 0.3 seconds
        if (elapsed < 0.3f)
        {
            float t = elapsed / 0.3f;
            float scale = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.one * scale;
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        // Enable pickup after delay
        if (!ready && elapsed >= pickupDelay)
        {
            ready = true;
        }

        // Gentle floating bob
        float y = startPos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(startPos.x, y, startPos.z);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !ready) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        collected = true;
        Collect(player);
    }

    // If the player is already standing nearby when the delay expires
    private void OnTriggerStay2D(Collider2D other)
    {
        if (collected || !ready) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        collected = true;
        Collect(player);
    }

    private void Collect(PlayerMovement player)
    {
        if (itemData == null)
        {
            Destroy(gameObject);
            return;
        }

        // Apply stat upgrades through PlayerStats
        PlayerStats stats = player.playerStats;
        if (stats != null)
            stats.ApplyUpgrade(itemData);

        HealthController hc = player.GetComponent<HealthController>();

        // Instant heal (absolute value)
        if (itemData.instantHeal > 0 && hc != null)
            hc.Heal(itemData.instantHeal);

        // Percentage heal (e.g., 0.35 = heal 35% of max HP)
        if (itemData.healPercent > 0f && hc != null)
            hc.HealPercent(itemData.healPercent);

        // Max HP bonus
        if (itemData.maxHPBonus > 0f && hc != null)
            hc.IncreaseMaxHP(itemData.maxHPBonus);

        // TODO: play pickup SFX / VFX here

        Destroy(gameObject);
    }
}

