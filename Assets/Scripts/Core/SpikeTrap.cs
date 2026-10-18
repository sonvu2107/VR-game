using UnityEngine;
using System.Collections.Generic;

/// <summary>Cycles between safe, warning and damaging states.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class SpikeTrap : MonoBehaviour
{
    private enum TrapState
    {
        Safe,
        Warning,
        Active
    }

    [Header("Damage")]
    [SerializeField, Min(1)] private int baseDamage = 1;
    [SerializeField, Min(0)] private int extraDamagePerLevel = 0;
    [SerializeField, Min(0.05f)] private float damageCooldown = 0.75f;

    [Header("Cycle")]
    [SerializeField, Min(0.1f)] private float safeDuration = 2f;
    [SerializeField, Min(0.1f)] private float warningDuration = 0.55f;
    [SerializeField, Min(0.1f)] private float activeDuration = 1.25f;

    [Header("Visual")]
    [SerializeField] private Color safeColor = new(0.32f, 0.32f, 0.36f, 0.65f);
    [SerializeField] private Color warningColor = new(1f, 0.65f, 0.08f, 0.9f);
    [SerializeField] private Color activeColor = new(0.9f, 0.12f, 0.16f, 1f);

    private static Sprite runtimeSprite;
    private ArcaneSpikeTrapVisual atlasVisual;
    private SpriteRenderer spriteRenderer;
    private TrapState state;
    private float stateTimeRemaining;
    private float nextDamageAt;
    private int damage;
    private Vector3 baseScale;
    private readonly HashSet<HealthController> damagedThisActivation = new();

    public bool IsDangerous => state == TrapState.Active;
    public bool IsWarning => state == TrapState.Warning;
    public float WarningSeconds => warningDuration;

    public void ConfigureTacticalCycle(float rest, float warning, float active)
    {
        EnsureComponents();
        safeDuration = Mathf.Max(2f, rest);
        warningDuration = Mathf.Max(0.8f, warning);
        activeDuration = Mathf.Clamp(active, 0.5f, 1f);
        SetState(TrapState.Safe);
    }

    // Optional art only: legacy levels keep the existing generated visual.
    public void ConfigureAtlasVisual(Material material, Rect[] frameRects = null)
    {
        EnsureComponents();
        if (material == null || atlasVisual != null) return;
        var child = new GameObject("Arcane Spike Plate Visual");
        child.transform.SetParent(transform, false);
        atlasVisual = child.AddComponent<ArcaneSpikeTrapVisual>();
        atlasVisual.Initialize(material, frameRects);
        spriteRenderer.enabled = false;
    }

    public void Initialize(int levelNumber)
    {
        damage = baseDamage + Mathf.Max(0, levelNumber - 1) * extraDamagePerLevel;
    }

    private void Awake()
    {
        EnsureComponents();
        damage = baseDamage;
    }

    private void EnsureComponents()
    {
        if (spriteRenderer != null) return;
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(0.85f, 0.85f);

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = GetRuntimeSprite();

        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 3);
        baseScale = transform.localScale;
    }

    private void Start()
    {
        SetState(TrapState.Safe);
    }

    private void Update()
    {
        if (LanLaunch.Mode == LanMode.Client || Time.deltaTime <= 0f) return;
        stateTimeRemaining -= Time.deltaTime;
        if (stateTimeRemaining > 0f)
        {
            if (state == TrapState.Warning)
            {
                if (atlasVisual == null)
                    transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * 35f) * 0.06f);
                atlasVisual?.SetFrame(stateTimeRemaining > warningDuration * 0.5f ? 1 : 2);
            }
            return;
        }

        switch (state)
        {
            case TrapState.Safe:
                SetState(TrapState.Warning);
                break;
            case TrapState.Warning:
                SetState(TrapState.Active);
                break;
            default:
                SetState(TrapState.Safe);
                break;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (LanLaunch.Mode == LanMode.Client || Time.timeScale <= 0f || state != TrapState.Active || Time.time < nextDamageAt)
            return;

        HealthController health = other.GetComponentInParent<HealthController>();
        if (health == null || damagedThisActivation.Contains(health))
            return;

        damagedThisActivation.Add(health);
        health.TakeDamage(damage);
        nextDamageAt = Time.time + damageCooldown;
    }

    private void SetState(TrapState newState)
    {
        state = newState;
        transform.localScale = baseScale;

        switch (state)
        {
            case TrapState.Safe:
                stateTimeRemaining = safeDuration;
                spriteRenderer.color = safeColor;
                atlasVisual?.SetFrame(0);
                break;
            case TrapState.Warning:
                stateTimeRemaining = warningDuration;
                spriteRenderer.color = warningColor;
                atlasVisual?.SetFrame(1);
                break;
            default:
                damagedThisActivation.Clear();
                stateTimeRemaining = activeDuration;
                spriteRenderer.color = activeColor;
                atlasVisual?.SetFrame(3);
                if (atlasVisual == null) transform.localScale = baseScale * 1.15f;
                break;
        }
    }

    private static Sprite GetRuntimeSprite()
    {
        if (runtimeSprite != null)
            return runtimeSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "Runtime Spike Trap"
        };

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int localX = x % 4;
                int height = localX <= 1 ? localX + 2 : 5 - localX;
                bool spike = y < height * 2 && y >= Mathf.Abs(localX - 2);
                texture.SetPixel(x, y, spike ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.25f), 16f, 0, SpriteMeshType.FullRect);
        runtimeSprite.name = "Runtime Spike Trap";
        return runtimeSprite;
    }
}
