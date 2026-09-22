using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime ability layer for the player. Q/E/R intentionally use direct keyboard
/// reads until the team adds the bindings to PlayerControls.inputactions.
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    private enum FrameAnchorMode
    {
        CellCenter,
        VisualCenter,
        VisualCenterX
    }

    [Header("Sword Wave (Q)")]
    [Min(0f)] public float swordWaveCooldown = 2.2f;
    [Min(0.1f)] public float swordWaveDuration = 0.5f;
    [Min(0f)] public float swordWaveSpeed = 12f;
    [Min(0.1f)] public float swordWaveRadius = 0.65f;
    [Min(1)] public int swordWaveDamage = 55;
    [Header("Spin Slash (E)")]
    [Min(0f)] public float spinSlashCooldown = 4.5f;
    [Min(0.1f)] public float spinSlashDuration = 0.62f;
    [Tooltip("Visual-only tail for Spin Slash. This can outlive the damage window so the last frame fades naturally.")]
    [Min(0.1f)] public float spinSlashVfxDuration = 0.86f;
    [Min(0.1f)] public float spinSlashRadius = 1.75f;
    [Min(1)] public int spinSlashDamagePerPulse = 28;
    [Header("Ashen Judgment (R)")]
    [Range(0f, 100f)] public float ultimateCharge;
    [Min(1f)] public float ultimateChargeRequired = 100f;
    [Min(0.1f)] public float ultimateDuration = 1.25f;
    [Tooltip("Visual-only tail for Ashen Judgment's final impact. This does not delay damage or input recovery.")]
    [Min(0.1f)] public float ultimateFinalVfxDuration = 0.86f;
    [Min(0.1f)] public float ultimateRadius = 3.1f;
    [Min(1)] public int ultimateSlashDamage = 55;
    [Min(1)] public int ultimateImpactDamage = 110;

    private PlayerMovement player;
    private SpriteRenderer playerRenderer;
    private Sprite[] swordWaveCoreFrames;
    private Sprite[] swordWaveImpactFrames;
    private Sprite[] spinSlashFrames;
    private Sprite[] runeFrames;
    private Sprite[] ultimateSlashFrames;
    private Sprite[] ultimateImpactFrames;
    private Sprite[] casterAuraFrames;
    private float nextSwordWaveAt;
    private float nextSpinSlashAt;
    private bool isSkillCasting;
    private bool hitStopActive;
    private static Material pixelVfxMaterial;
    // Effects are deliberately tracked separately from Player. They are never
    // character clones and are destroyed both at their normal end and when the
    // player/controller is destroyed mid-cast.
    private readonly HashSet<GameObject> activeEffects = new HashSet<GameObject>();

    public bool IsSkillCasting => isSkillCasting;
    public float SwordWaveCooldownNormalized => swordWaveCooldown <= 0f ? 0f : Mathf.Clamp01((nextSwordWaveAt - Time.time) / swordWaveCooldown);
    public float SpinSlashCooldownNormalized => spinSlashCooldown <= 0f ? 0f : Mathf.Clamp01((nextSpinSlashAt - Time.time) / spinSlashCooldown);
    public float UltimateNormalized => ultimateChargeRequired <= 0f ? 0f : Mathf.Clamp01(ultimateCharge / ultimateChargeRequired);

    private void Start()
    {
        player = GetComponent<PlayerMovement>();
        playerRenderer = GetComponent<SpriteRenderer>();
        swordWaveCoreFrames = LoadPixelFrames("Combat/Abilities/SwordWavePixel_6f", 6);
        swordWaveImpactFrames = LoadPixelFrames("Combat/Abilities/SwordWaveImpactPixel_6f", 6);
        // E is a stationary spin, so every visual frame shares one centre.
        spinSlashFrames = LoadPixelFrames("Combat/Abilities/SpinSlashPixel_6f", 6, FrameAnchorMode.VisualCenter);
        runeFrames = LoadPixelFrames("Combat/Abilities/AshenJudgmentRunePixel_6f", 6, FrameAnchorMode.VisualCenter);
        // R descends vertically. Lock its horizontal centre only, preserving
        // the source animation's vertical progression from sky to ground.
        ultimateSlashFrames = LoadPixelFrames("Combat/Abilities/AshenJudgmentBladesPixel_6f", 6, FrameAnchorMode.VisualCenterX);
        ultimateImpactFrames = LoadPixelFrames("Combat/Abilities/AshenJudgmentFinalPixel_6f", 6, FrameAnchorMode.VisualCenterX);
        casterAuraFrames = LoadPixelFrames("Combat/Abilities/CasterBodyAuraPixel_6f", 6, FrameAnchorMode.VisualCenter);
        if (GetComponent<SkillHudController>() == null)
            gameObject.AddComponent<SkillHudController>();
    }

    private void Update()
    {
        if (!CanCast() || Keyboard.current == null)
            return;

        if (Keyboard.current.qKey.wasPressedThisFrame && Time.time >= nextSwordWaveAt)
        {
            nextSwordWaveAt = Time.time + swordWaveCooldown;
            StartCoroutine(CastSwordWave());
        }
        else if (Keyboard.current.eKey.wasPressedThisFrame && Time.time >= nextSpinSlashAt)
        {
            nextSpinSlashAt = Time.time + spinSlashCooldown;
            StartCoroutine(CastSpinSlash());
        }
        else if (Keyboard.current.rKey.wasPressedThisFrame && ultimateCharge >= ultimateChargeRequired)
        {
            ultimateCharge = 0f;
            StartCoroutine(CastAshenJudgment());
        }
    }

    public void AddUltimateCharge(float amount)
    {
        if (isSkillCasting || amount <= 0f)
            return;

        ultimateCharge = Mathf.Clamp(ultimateCharge + amount, 0f, ultimateChargeRequired);
    }

    private bool CanCast()
    {
        return player != null && !player.IsAttacking && !player.IsDashing &&
            !player.IsHeavyLocked && !player.IsHeavyInputPending && !isSkillCasting;
    }

    private IEnumerator CastSwordWave()
    {
        BeginCast("sword-wave");
        player.PlaySkillBodyMotion(1);
        Vector2 direction = player.FacingDirection;
        // Effects must use the rendered sprite centre.  The Player transform
        // is on the movement anchor, which made Q and the caster aura appear
        // below/away from the animated character.
        Vector3 bodyCenter = player.VisualCenter;
        Vector3 bodyOffset = bodyCenter - transform.position;
        Vector3 position = bodyCenter + (Vector3)direction * 0.65f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        CreateEffect("Sword Wave Caster Aura", casterAuraFrames, bodyCenter, 2.15f, 0.42f,
            Quaternion.identity, transform, 1, bodyOffset);
        GameObject core = CreateEffect("Sword Wave Core", swordWaveCoreFrames, position, 1.6f, swordWaveDuration,
            rotation, null);
        if (core != null && playerRenderer != null)
            core.GetComponent<SpriteRenderer>().sortingOrder = playerRenderer.sortingOrder + 3;
        AssertCastVisualInvariant("sword-wave-effects-created");

        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        HashSet<BossController> hitBosses = new HashSet<BossController>();
        float elapsed = 0f;

        while (elapsed < swordWaveDuration)
        {
            elapsed += Time.deltaTime;
            position += (Vector3)direction * swordWaveSpeed * Time.deltaTime;
            if (core != null)
                core.transform.position = position;

            int hitCount = player.DamageEnemiesInRadius(position, swordWaveRadius, swordWaveDamage, hitEnemies, hitBosses);
            if (hitCount > 0)
            {
                if (core != null)
                    Destroy(core);
                CreateEffect("Sword Wave Impact", swordWaveImpactFrames, position, 1.1f, 0.3f, rotation, null);
                StartCoroutine(PlayImpactFeedback(0.12f, 0.075f, 0.045f));
                EndCast("sword-wave-hit");
                yield break;
            }

            yield return null;
        }

        EndCast("sword-wave-complete");
    }

    private IEnumerator PlayImpactFeedback(float shakeDuration, float shakeMagnitude, float hitStopDuration)
    {
        StartCoroutine(ShakeMainCamera(shakeDuration, shakeMagnitude));
        if (hitStopActive)
            yield break;

        hitStopActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Min(previousTimeScale, 0.04f);
        yield return new WaitForSecondsRealtime(hitStopDuration);
        if (!GameManager.isGamePaused && !GameManager.isGameOver && !GameManager.isWin)
            Time.timeScale = previousTimeScale;
        hitStopActive = false;
    }

    private static IEnumerator ShakeMainCamera(float duration, float magnitude)
    {
        Camera camera = Camera.main;
        if (camera == null)
            yield break;

        Transform cameraTransform = camera.transform;
        Vector3 originalPosition = cameraTransform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = magnitude * (1f - elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * strength;
            cameraTransform.localPosition = originalPosition + (Vector3)offset;
            yield return null;
        }

        if (cameraTransform != null)
            cameraTransform.localPosition = originalPosition;
    }

    private IEnumerator CastSpinSlash()
    {
        BeginCast("spin-slash");
        float castStartedAt = Time.time;
        player.PlaySkillBodyMotion(2);
        Vector3 bodyCenter = player.VisualCenter;
        Vector3 bodyOffset = bodyCenter - transform.position;
        CreateEffect("Spin Slash Caster Aura", casterAuraFrames, bodyCenter, 2.6f, spinSlashDuration,
            Quaternion.identity, transform, 1, bodyOffset, smoothFrames: true);

        // Keep the slash at its cast origin. The visual tail is intentionally
        // longer than the movement lock; following the player made that tail
        // slide sideways as soon as movement became available again.
        CreateEffect("Spin Slash", spinSlashFrames, bodyCenter, 4.3f, spinSlashVfxDuration,
            Quaternion.identity, null, 2, smoothFrames: true);
        AssertCastVisualInvariant("spin-slash-effects-created");
        float[] pulseTimes = { 0.14f, 0.34f, 0.54f };
        int nextPulse = 0;
        float elapsed = 0f;

        while (elapsed < spinSlashDuration)
        {
            elapsed += Time.deltaTime;
            if (nextPulse < pulseTimes.Length && elapsed >= pulseTimes[nextPulse])
            {
                player.DamageEnemiesInRadius(transform.position, spinSlashRadius, spinSlashDamagePerPulse);
                nextPulse++;
            }
            yield return null;
        }

        // Do not reopen movement while the last slash frames are still on
        // screen; that was the remaining sideways slide at the end of E.
        float remainingVfxTime = spinSlashVfxDuration - (Time.time - castStartedAt);
        if (remainingVfxTime > 0f)
            yield return new WaitForSeconds(remainingVfxTime);
        EndCast("spin-slash-complete");
    }

    private IEnumerator CastAshenJudgment()
    {
        BeginCast("ashen-judgment");
        float castStartedAt = Time.time;
        player.PlaySkillBodyMotion(3, true);
        Vector3 strikePosition = transform.position;
        Vector3 bodyCenter = player.VisualCenter;
        Vector3 bodyOffset = bodyCenter - transform.position;
        CreateEffect("Ashen Judgment Caster Aura", casterAuraFrames, bodyCenter, 3.35f, 0.88f,
            Quaternion.identity, transform, 1, bodyOffset, smoothFrames: true);
        CreateEffect("Ashen Judgment Rune", runeFrames, strikePosition, 3.85f, 0.94f,
            Quaternion.identity, null, -1, smoothFrames: true);
        AssertCastVisualInvariant("ashen-judgment-effects-created");
        StartCoroutine(ShakeMainCamera(0.18f, 0.035f));
        yield return new WaitForSeconds(0.18f);
        CreateEffect("Ashen Judgment Blade Storm", ultimateSlashFrames, strikePosition, 3.1f, 0.76f,
            Quaternion.identity, null, 2, smoothFrames: true);

        for (int pulse = 0; pulse < 3; pulse++)
        {
            yield return new WaitForSeconds(0.16f);
            player.DamageEnemiesInRadius(strikePosition, ultimateRadius, ultimateSlashDamage);
            StartCoroutine(ShakeMainCamera(0.07f, 0.045f));
        }

        float finalStartedAt = Time.time;
        CreateEffect("Ashen Judgment Final", ultimateImpactFrames, strikePosition, 4.75f, ultimateFinalVfxDuration,
            Quaternion.identity, null, 3, smoothFrames: true);
        player.DamageEnemiesInRadius(strikePosition, ultimateRadius * 1.15f, ultimateImpactDamage);
        yield return StartCoroutine(PlayImpactFeedback(0.28f, 0.14f, 0.075f));

        // Keep the body pose and input lock in sync with the configured cast
        // duration instead of returning to locomotion midway through the VFX.
        float remainingCastTime = Mathf.Max(
            ultimateDuration - (Time.time - castStartedAt),
            ultimateFinalVfxDuration - (Time.time - finalStartedAt));
        if (remainingCastTime > 0f)
            yield return new WaitForSeconds(remainingCastTime);
        EndCast("ashen-judgment-complete");
    }

    private void BeginCast(string castName)
    {
        AssertCastVisualInvariant($"{castName}-before");
        isSkillCasting = true;
        AssertCastVisualInvariant($"{castName}-during");
    }

    private void EndCast(string castName)
    {
        isSkillCasting = false;
        AssertCastVisualInvariant($"{castName}-after");
    }

    private void AssertCastVisualInvariant(string phase)
    {
        if (player != null)
            player.AssertSingleAuthoritativePlayerVisual(phase);
    }

    private GameObject CreateEffect(string effectName, Sprite[] frames, Vector3 position, float scale,
        float duration, Quaternion rotation, Transform followTarget, int sortingOffset = 2,
        Vector3 followOffset = default, bool smoothFrames = false)
    {
        if (frames == null || frames.Length == 0)
            return null;

        GameObject effect = new GameObject(effectName);
        activeEffects.Add(effect);
        effect.transform.position = position;
        effect.transform.rotation = rotation;
        effect.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = Color.white;
        Material vfxMaterial = GetVfxMaterial();
        if (vfxMaterial != null)
            renderer.sharedMaterial = vfxMaterial;
        if (playerRenderer != null)
        {
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + sortingOffset;
        }

        SpriteRenderer frameTrail = null;
        if (smoothFrames)
        {
            GameObject trailObject = new GameObject("Frame Trail");
            trailObject.transform.SetParent(effect.transform, false);
            frameTrail = trailObject.AddComponent<SpriteRenderer>();
            frameTrail.sprite = frames[0];
            frameTrail.color = Color.clear;
            frameTrail.sharedMaterial = renderer.sharedMaterial;
            frameTrail.sortingLayerID = renderer.sortingLayerID;
            frameTrail.sortingOrder = renderer.sortingOrder - 1;
        }

        StartCoroutine(AnimateEffect(effect, renderer, frameTrail, frames, duration,
            followTarget, followOffset, smoothFrames));
        return effect;
    }

    private IEnumerator AnimateEffect(GameObject effect, SpriteRenderer renderer, SpriteRenderer frameTrail,
        Sprite[] frames, float duration, Transform followTarget, Vector3 followOffset, bool smoothFrames)
    {
        float elapsed = 0f;
        int previousFrameIndex = 0;
        while (effect != null && elapsed < duration)
        {
            if (followTarget != null)
            {
                // Reuse the centre offset captured when casting. Reading the
                // current sprite bounds here made the VFX wobble whenever an
                // action frame had different transparent margins.
                effect.transform.position = followTarget.position + followOffset;
            }

            float progress = Mathf.Clamp01(elapsed / duration);
            float framePosition = progress * frames.Length;
            int frameIndex = Mathf.Min(Mathf.FloorToInt(framePosition), frames.Length - 1);
            if (frameIndex != previousFrameIndex)
            {
                if (frameTrail != null)
                    frameTrail.sprite = renderer.sprite;
                renderer.sprite = frames[frameIndex];
                previousFrameIndex = frameIndex;
            }

            if (smoothFrames)
            {
                // A short envelope removes hard pop-in/out. The previous frame
                // lingers faintly behind the new one, bridging sparse 6-frame
                // sheets without blurring the main pixel-art silhouette.
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.24f));
                float envelope = Mathf.Min(fadeIn, fadeOut);
                renderer.color = new Color(1f, 1f, 1f, envelope);

                if (frameTrail != null)
                {
                    float framePhase = framePosition - Mathf.Floor(framePosition);
                    // Keep just a hint of motion. At 30% this read as a
                    // duplicate body during close-range casts.
                    float trailAlpha = 0.10f * (1f - Mathf.Clamp01(framePhase / 0.72f)) * envelope;
                    frameTrail.color = new Color(0.72f, 0.93f, 1f, trailAlpha);
                }
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        activeEffects.Remove(effect);
        if (effect != null)
            Destroy(effect);
    }

    private void OnDestroy()
    {
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null)
                Destroy(effect);
        }
        activeEffects.Clear();
    }

    /// <summary>
    /// Builds each VFX frame on a padded canvas. Several generated source sheets
    /// have glow pixels touching a cell boundary; sampling those cells directly
    /// produced a hard rectangular crop in-game. The transparent border and a
    /// small edge feather remove that artificial box while keeping every frame
    /// anchored at the original cell centre.
    /// </summary>
    internal static Sprite[] LoadPixelFrames(string resourcePath, int frameCount,
        FrameAnchorMode anchorMode = FrameAnchorMode.CellCenter)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null || frameCount <= 0)
        {
            Debug.LogWarning($"Could not load ability VFX: {resourcePath}");
            return System.Array.Empty<Sprite>();
        }

        // All skill sheets use a uniform horizontal grid. Point sampling keeps
        // their white blade edges as sharp as the existing combo slash sprites.
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = texture.GetPixels32();
        Sprite[] frames = new Sprite[frameCount];
        const int transparentPaddingPixels = 24;
        const int sourceEdgeFeatherPixels = 14;
        const byte visibleAlphaThreshold = 8;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int left = Mathf.RoundToInt(frame * texture.width / (float)frameCount);
            int right = Mathf.RoundToInt((frame + 1) * texture.width / (float)frameCount);
            int sourceWidth = right - left;
            int paddedWidth = sourceWidth + transparentPaddingPixels * 2;
            int paddedHeight = texture.height + transparentPaddingPixels * 2;
            Color32[] paddedPixels = new Color32[paddedWidth * paddedHeight];

            bool hasLeftEdgePixels = false;
            bool hasRightEdgePixels = false;
            int minX = sourceWidth;
            int maxX = -1;
            int minY = texture.height;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                int sourceRow = y * texture.width;
                hasLeftEdgePixels |= pixels[sourceRow + left].a >= visibleAlphaThreshold;
                hasRightEdgePixels |= pixels[sourceRow + right - 1].a >= visibleAlphaThreshold;
                for (int x = 0; x < sourceWidth; x++)
                {
                    if (pixels[sourceRow + left + x].a < visibleAlphaThreshold)
                        continue;

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            for (int y = 0; y < texture.height; y++)
            {
                int sourceRow = y * texture.width;
                int targetRow = (y + transparentPaddingPixels) * paddedWidth + transparentPaddingPixels;
                for (int x = 0; x < sourceWidth; x++)
                {
                    Color32 pixel = pixels[sourceRow + left + x];
                    if (pixel.a > 0)
                    {
                        float alphaMultiplier = 1f;
                        if (hasLeftEdgePixels && x < sourceEdgeFeatherPixels)
                            alphaMultiplier = Mathf.Min(alphaMultiplier, x / (float)sourceEdgeFeatherPixels);
                        if (hasRightEdgePixels && sourceWidth - 1 - x < sourceEdgeFeatherPixels)
                            alphaMultiplier = Mathf.Min(alphaMultiplier,
                                (sourceWidth - 1 - x) / (float)sourceEdgeFeatherPixels);
                        pixel.a = (byte)Mathf.RoundToInt(pixel.a * alphaMultiplier);
                    }
                    paddedPixels[targetRow + x] = pixel;
                }
            }

            Texture2D paddedTexture = new Texture2D(paddedWidth, paddedHeight, TextureFormat.RGBA32, false)
            {
                name = $"{resourcePath}_{frame:00}_Padded",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            paddedTexture.SetPixels32(paddedPixels);
            paddedTexture.Apply(false, true);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (maxX >= minX)
            {
                float visualCenterX = transparentPaddingPixels + (minX + maxX + 1) * 0.5f;
                float visualCenterY = transparentPaddingPixels + (minY + maxY + 1) * 0.5f;
                if (anchorMode == FrameAnchorMode.VisualCenter || anchorMode == FrameAnchorMode.VisualCenterX)
                    pivot.x = visualCenterX / paddedWidth;
                if (anchorMode == FrameAnchorMode.VisualCenter)
                    pivot.y = visualCenterY / paddedHeight;
            }

            frames[frame] = Sprite.Create(paddedTexture,
                new Rect(0f, 0f, paddedWidth, paddedHeight), pivot,
                512f, 0, SpriteMeshType.FullRect);
            frames[frame].name = $"{resourcePath}_{frame:00}";
        }

        return frames;
    }

    internal static Material GetVfxMaterial()
    {
        if (pixelVfxMaterial != null)
            return pixelVfxMaterial;

        Shader shader = Resources.Load<Shader>("Combat/PixelVFX");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("PixelVFX shader is unavailable; using the default sprite shader.");
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }
        if (shader == null)
            return null;

        pixelVfxMaterial = new Material(shader) { name = "Pixel Combat VFX" };
        return pixelVfxMaterial;
    }

}
