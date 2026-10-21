using System.Collections;
using UnityEngine;

/// <summary>
/// Detached death feedback shared by configured enemies, legacy skeletons and
/// runtime bosses. It survives even when the defeated object is destroyed.
/// </summary>
public sealed class EnemyDefeatFeedback : MonoBehaviour
{
    public enum Tier { Enemy, MiniBoss, Boss }

    private static Sprite pixelSprite;
    private static AudioClip enemyClip;
    private static AudioClip miniBossClip;
    private static AudioClip bossClip;
    private static AudioClip skeletonKingDeathClip;
    private static AudioClip bossCollapseClip;

    private Transform[] shards;
    private SpriteRenderer[] shardRenderers;
    private Vector2[] velocities;
    private float[] angularVelocities;
    private Vector3[] initialScales;
    private Tier tier;
    private bool skeletonKing;
    private Vector3 burstPosition;

    public static void Spawn(Vector3 position, Tier defeatTier, SpriteRenderer reference = null)
    {
        GameObject effect = new GameObject($"{defeatTier} Defeat Feedback");
        effect.transform.position = new Vector3(position.x, position.y, -0.6f);

        EnemyDefeatFeedback feedback = effect.AddComponent<EnemyDefeatFeedback>();
        feedback.tier = defeatTier;
        feedback.skeletonKing = reference != null && reference.GetComponentInParent<SkeletonKingController>() != null;
        feedback.burstPosition = position;
        feedback.BuildShards(reference);
        feedback.PlayAudio();
        feedback.StartCoroutine(feedback.Animate());

        CombatImpactKind impactKind = defeatTier == Tier.Boss ? CombatImpactKind.Boss : CombatImpactKind.EnemyMelee;
        float scale = defeatTier == Tier.Boss ? 1.8f : defeatTier == Tier.MiniBoss ? 1.25f : 0.92f;
        CombatHitVfx.Spawn(position, impactKind, scale);
    }

    private void BuildShards(SpriteRenderer reference)
    {
        int count = tier == Tier.Boss ? 28 : tier == Tier.MiniBoss ? 20 : 12;
        shards = new Transform[count];
        shardRenderers = new SpriteRenderer[count];
        velocities = new Vector2[count];
        angularVelocities = new float[count];
        initialScales = new Vector3[count];

        Color baseColor = tier == Tier.Boss
            ? new Color(0.94f, 0.84f, 0.58f, 1f)
            : tier == Tier.MiniBoss
                ? new Color(0.72f, 0.42f, 1f, 1f)
                : new Color(1f, 0.34f, 0.20f, 1f);
        if (reference != null)
            baseColor = Color.Lerp(reference.color, baseColor, 0.72f);

        for (int i = 0; i < count; i++)
        {
            GameObject shard = new GameObject("Defeat Pixel");
            shard.transform.SetParent(transform, false);
            shard.transform.localPosition = Random.insideUnitCircle * (tier == Tier.Boss ? 0.42f : 0.22f);
            float size = Random.Range(0.055f, tier == Tier.Boss ? 0.16f : 0.12f);
            shard.transform.localScale = new Vector3(size * Random.Range(0.65f, 1.35f), size, 1f);
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            if (tier == Tier.Boss)
            {
                // Alternate ivory bone, ember and blood-red fragments so the finale
                // reads as a king shattering apart rather than a generic green burst.
                renderer.color = i % 3 == 0
                    ? new Color(1f, 0.92f, 0.68f, 1f)
                    : i % 3 == 1
                        ? new Color(1f, 0.34f, 0.12f, 1f)
                        : new Color(0.72f, 0.08f, 0.12f, 1f);
            }
            else renderer.color = Color.Lerp(baseColor, Color.white, Random.Range(0f, 0.42f));
            renderer.sortingLayerID = reference != null ? reference.sortingLayerID : 0;
            renderer.sortingOrder = (reference != null ? reference.sortingOrder : 0) + 8;

            Vector2 direction = tier == Tier.Boss
                ? new Vector2(Mathf.Cos(i * Mathf.PI * 2f / count), Mathf.Sin(i * Mathf.PI * 2f / count)).normalized
                : Random.insideUnitCircle.normalized;
            direction.y = Mathf.Abs(direction.y) + 0.28f;
            velocities[i] = direction.normalized * Random.Range(1.25f, tier == Tier.Boss ? 4.4f : 3.1f);
            angularVelocities[i] = Random.Range(-420f, 420f);
            shards[i] = shard.transform;
            shardRenderers[i] = renderer;
            initialScales[i] = shard.transform.localScale;
        }
    }

    private void PlayAudio()
    {
        enemyClip ??= Resources.Load<AudioClip>("Audio/Combat/Enemies/EnemyDefeat");
        miniBossClip ??= Resources.Load<AudioClip>("Audio/Combat/Enemies/MiniBossDefeat");
        bossClip ??= Resources.Load<AudioClip>("Audio/Combat/Enemies/BossDefeat");
        skeletonKingDeathClip ??= Resources.Load<AudioClip>(
            "Audio/Combat/Enemies/SkeletonKing/SkeletonKingDeathVoice");
        bossCollapseClip ??= Resources.Load<AudioClip>(
            "Audio/Combat/Enemies/SkeletonKing/SkeletonKingSoulDeath");
        AudioClip clip = tier == Tier.Boss
            ? (skeletonKing && skeletonKingDeathClip != null ? skeletonKingDeathClip : bossClip)
            : tier == Tier.MiniBoss ? miniBossClip : enemyClip;
        if (clip == null)
        {
            Debug.LogWarning($"Missing defeat SFX for {tier}.");
            return;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = tier == Tier.Boss ? 1f : tier == Tier.MiniBoss ? 0.90f : 0.72f;
        source.pitch = tier == Tier.Enemy ? Random.Range(0.94f, 1.07f) : 1f;
        source.clip = clip;
        source.Play();

        if (tier == Tier.Boss && skeletonKing && bossCollapseClip != null)
            StartCoroutine(PlayBossCollapseAccent());
    }

    private IEnumerator PlayBossCollapseAccent()
    {
        yield return new WaitForSeconds(0.24f);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossDeathCollapse, 0.90f, 0.72f);
    }

    private IEnumerator Animate()
    {
        float duration = tier == Tier.Boss ? 1.42f : tier == Tier.MiniBoss ? 0.82f : 0.62f;
        float elapsed = 0f;
        bool secondaryBurstPlayed = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null) continue;
                velocities[i] += Vector2.down * (4.2f * Time.deltaTime);
                shards[i].localPosition += (Vector3)(velocities[i] * Time.deltaTime);
                shards[i].Rotate(0f, 0f, angularVelocities[i] * Time.deltaTime);
                shards[i].localScale = initialScales[i] * Mathf.Lerp(1f, 0.08f, progress);
                Color color = shardRenderers[i].color;
                color.a = Mathf.Clamp01((1f - progress) * 1.7f);
                shardRenderers[i].color = color;
            }

            if (!secondaryBurstPlayed && progress >= 0.22f)
            {
                secondaryBurstPlayed = true;
                SpawnSecondaryBurst();
            }
            yield return null;
        }

        AudioSource source = GetComponent<AudioSource>();
        float audioTail = source != null && source.clip != null ? source.clip.length : 0f;
        if (audioTail > duration)
            yield return new WaitForSeconds(audioTail - duration);
        Destroy(gameObject);
    }

    private void SpawnSecondaryBurst()
    {
        CombatImpactKind kind = tier == Tier.Boss ? CombatImpactKind.Boss : CombatImpactKind.EnemyMelee;
        float scale = tier == Tier.Boss ? 1.25f : tier == Tier.MiniBoss ? 0.85f : 0.58f;
        CombatHitVfx.Spawn(burstPosition + (Vector3)(Random.insideUnitCircle * 0.18f), kind, scale);
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
            return pixelSprite;

        Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false)
        {
            name = "Defeat Pixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[9];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 3f, 3f), new Vector2(0.5f, 0.5f), 3f);
        pixelSprite.name = "Defeat Pixel Sprite";
        return pixelSprite;
    }
}
