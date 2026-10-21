using UnityEngine;

/// <summary>Collectible required by selected levels before their exit can open.</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class DungeonKey : MonoBehaviour
{
    [SerializeField, Min(0f)] private float bobAmplitude = 0.12f;
    [SerializeField, Min(0.1f)] private float bobFrequency = 2f;
    [SerializeField] private Color keyColor = new(1f, 0.78f, 0.12f, 1f);
    [SerializeField, Min(0.1f)] private float visualScale = 0.65f;

    private static Sprite runtimeSprite;
    private static Sprite[] spinFrames;
    private const float FrameDuration = .08f;
    public static Sprite IconSprite => GetSpinFrames().Length > 0 ? spinFrames[0] : GetRuntimeSprite();
    private GameManager gameManager;
    private SpriteRenderer spriteRenderer;
    private Vector3 anchorPosition;
    private Vector3 baseScale;
    private bool collected;
    private float spinTime;

    public void Initialize(GameManager owner)
    {
        gameManager = owner;
    }

    private void Awake()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = IconSprite;

        spriteRenderer.color = GetSpinFrames().Length > 0 ? Color.white : keyColor;
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 8);
        baseScale = transform.localScale * visualScale;
        transform.localScale = baseScale;
    }

    private void Start()
    {
        anchorPosition = transform.position;
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void Update()
    {
        if (collected)
            return;

        float wave = Mathf.Sin(Time.time * bobFrequency);
        transform.position = anchorPosition + Vector3.up * (wave * bobAmplitude);
        spinTime += Time.deltaTime;
        var frames = GetSpinFrames();
        if (frames.Length > 0)
        {
            // Also refresh keys already alive when the editor reloads this script.
            spriteRenderer.color = Color.white;
            transform.localScale = baseScale;
            spriteRenderer.sprite = frames[Mathf.FloorToInt(spinTime / FrameDuration) % frames.Length];
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || other.GetComponentInParent<PlayerMovement>() == null)
            return;

        if (gameManager == null || !gameManager.CollectKey())
            return;

        collected = true;
        Destroy(gameObject);
    }

    private static Sprite GetRuntimeSprite()
    {
        if (runtimeSprite != null)
            return runtimeSprite;

        Texture2D texture = new(8, 12, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "Runtime Dungeon Key"
        };

        Color clear = Color.clear;
        Color white = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool ring = y >= 7 && (x == 2 || x == 5 || y == 7 || y == 11) && x >= 2 && x <= 5;
                bool stem = x >= 3 && x <= 4 && y >= 2 && y <= 7;
                bool tooth = y >= 1 && y <= 3 && x >= 4 && x <= 6;
                texture.SetPixel(x, y, ring || stem || tooth ? white : clear);
            }
        }

        texture.Apply();
        runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 8f, 0, SpriteMeshType.FullRect);
        runtimeSprite.name = "Runtime Dungeon Key";
        return runtimeSprite;
    }

    private static Sprite[] GetSpinFrames()
    {
        if (spinFrames != null && spinFrames.Length > 0 && spinFrames[0] != null) return spinFrames;
        var frames = new Sprite[16];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Resources.Load<Sprite>($"Pickups/KeySpin/KeySpin_{i:00}");
            if (frames[i] == null) return System.Array.Empty<Sprite>();
        }
        spinFrames = frames;
        return spinFrames;
    }
}
