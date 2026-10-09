using UnityEngine;

/// <summary>One green potion restores one heart (two health points).</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class HealingPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int healAmount = 2;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.12f;
    [SerializeField, Min(0.1f)] private float bobFrequency = 2f;

    private static Sprite potionSprite;
    private Vector3 anchorPosition;
    private bool collected;

    public static HealingPickup Create(Vector3 position)
    {
        GameObject pickup = new("Green Healing Potion");
        pickup.transform.position = position;
        return pickup.AddComponent<HealingPickup>();
    }

    private void Awake()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.45f;

        SpriteRenderer visual = GetComponent<SpriteRenderer>();
        if (visual == null)
            visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = GetPotionSprite();
        visual.sortingOrder = 8;
    }

    private void Start()
    {
        anchorPosition = transform.position;
    }

    private void Update()
    {
        if (!collected)
            transform.position = anchorPosition + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected)
            return;

        HealthController health = other.GetComponentInParent<HealthController>();
        if (health == null || !health.TryHeal(healAmount))
            return;

        collected = true;
        Destroy(gameObject);
    }

    private static Sprite GetPotionSprite()
    {
        if (potionSprite != null)
            return potionSprite;

        Texture2D sheet = Resources.Load<Texture2D>("Pickups/PotionSet16x16");
        if (sheet == null || sheet.width < 64 || sheet.height < 16)
        {
            Debug.LogError("Green potion art is missing from Resources/Pickups/PotionSet16x16.");
            return null;
        }

        sheet.filterMode = FilterMode.Point;
        potionSprite = Sprite.Create(sheet, new Rect(48f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        potionSprite.name = "Green Healing Potion";
        return potionSprite;
    }
}
