using System;
using UnityEngine;

/// <summary>Plays one-frame-per-file Bone Cage pixel VFX stored under Resources.</summary>
[DisallowMultipleComponent]
public sealed class BoneCageEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameDuration;
    private float expiresAt;
    private bool loop;

    public static GameObject Spawn(string resourceFolder, Vector3 position, float worldSize,
        float duration, int sortingOrder, bool loop)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourceFolder);
        Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));

        GameObject effect = new GameObject("BoneCage " + resourceFolder.Substring(resourceFolder.LastIndexOf('/') + 1));
        effect.transform.position = position;
        BoneCageEffect player = effect.AddComponent<BoneCageEffect>();
        player.Initialize(sprites, duration, sortingOrder, loop);
        // Authored frames use the project PPU; fit their actual sprite bounds to the
        // gameplay diameter rather than treating a transform scale as world units.
        float sourceSize = player.spriteRenderer.sprite != null
            ? Mathf.Max(player.spriteRenderer.sprite.bounds.size.x, player.spriteRenderer.sprite.bounds.size.y)
            : 1f;
        effect.transform.localScale = Vector3.one * (worldSize / Mathf.Max(0.01f, sourceSize));
        return effect;
    }

    private void Initialize(Sprite[] sprites, float duration, int order, bool shouldLoop)
    {
        frames = sprites;
        frameDuration = Mathf.Max(0.025f, duration / Mathf.Max(1, frames.Length));
        expiresAt = Time.time + Mathf.Max(0.05f, duration);
        loop = shouldLoop;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = order;
        spriteRenderer.sprite = frames.Length > 0 ? frames[0] : EnemyEffectSpriteFactory.Ring;
        spriteRenderer.color = Color.white;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        float elapsed = Mathf.Max(0f, Time.time - (expiresAt - frameDuration * frames.Length));
        int frame = Mathf.FloorToInt(elapsed / frameDuration);
        if (loop) frame %= frames.Length;
        else frame = Mathf.Min(frame, frames.Length - 1);
        spriteRenderer.sprite = frames[frame];
        if (!loop && Time.time >= expiresAt) Destroy(gameObject);
    }
}

/// <summary>Short-lived bone fissure left behind by the Skeleton King's charge.</summary>
[DisallowMultipleComponent]
public sealed class BoneChargeCrack : MonoBehaviour
{
    private float expiresAt;
    private float slowMultiplier;

    public static GameObject Spawn(Vector3 position, float radius, float duration, float slow)
    {
        GameObject crack = new GameObject("Bone Charge Crack");
        crack.transform.position = new Vector3(position.x, position.y, position.z + 0.03f);
        CircleCollider2D trigger = crack.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, radius);

        BoneChargeCrack controller = crack.AddComponent<BoneChargeCrack>();
        controller.expiresAt = Time.time + Mathf.Max(0.1f, duration);
        controller.slowMultiplier = Mathf.Clamp(slow, 0.1f, 1f);
        BoneCageEffect.Spawn("Enemy/BossEffects/BoneCharge/Crack", position, radius * 2f,
            duration, 7, true).transform.SetParent(crack.transform, true);
        return crack;
    }

    private void Update()
    {
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        if (player != null) player.ApplyMovementSlow(0.16f, slowMultiplier);
    }
}
