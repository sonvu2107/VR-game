using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls procedural animation, tree sprouting and growth, foliage swaying,
/// and wavering, floating torch fire with dynamic light flickering.
/// </summary>
[DisallowMultipleComponent]
public class AnimatedDecor : MonoBehaviour
{
    [Header("Sprite Animation")]
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float framesPerSecond = 6f;

    [Header("Sprout & Growth")]
    [SerializeField] private bool isSprouting;
    [SerializeField] private float sproutDuration = 1.2f;
    [SerializeField] private bool continuousBreathing = true;
    [SerializeField] private Sprite[] transitionIdleFrames;

    [Header("Foliage Sway (Optional)")]
    [SerializeField] private bool enableSway;
    [SerializeField, Min(0.1f)] private float swaySpeed = 2.2f;
    [SerializeField, Range(0.5f, 15f)] private float swayAngle = 3.5f;

    [Header("Floating Flame (Torch)")]
    [SerializeField] private bool enableFlameBobbing;
    [SerializeField, Min(0.1f)] private float flameBobSpeed = 4.5f;
    [SerializeField, Range(0.01f, 0.1f)] private float flameBobHeight = 0.035f;

    [Header("Light Flicker & Halos")]
    [SerializeField] private bool isLightSource;
    [SerializeField] private Light2D lightComponent;
    [SerializeField, Range(0.1f, 3f)] private float baseLightIntensity = 1.15f;
    [SerializeField, Range(0.5f, 10f)] private float baseLightRadius = 2.2f;
    [SerializeField, Range(0.01f, 0.5f)] private float flickerAmount = 0.22f;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float frameTimer;
    private float noiseOffset;
    private float swayOffset;
    private float flameOffset;
    private float pulseOffset;
    private Vector3 basePosition;
    private Vector3 targetScale;
    private Quaternion baseRotation;
    private float growthTimer;
    private bool growthCompleted;

    public void ConfigureFrames(Sprite[] animationFrames, float fps = 6f)
    {
        frames = animationFrames;
        framesPerSecond = Mathf.Max(0.1f, fps);
        if (frames != null && frames.Length > 0)
        {
            currentFrame = Random.Range(0, frames.Length);
            EnsureRenderer();
            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    public void ConfigureSproutSequence(Sprite[] sproutSequence, Sprite[] loopIdleFrames, float duration = 1.6f)
    {
        frames = sproutSequence;
        transitionIdleFrames = loopIdleFrames;
        sproutDuration = Mathf.Max(0.2f, duration);
        framesPerSecond = sproutSequence != null && sproutSequence.Length > 0
            ? sproutSequence.Length / sproutDuration
            : 6f;
        isSprouting = true;
        continuousBreathing = true;
        growthTimer = 0f;
        growthCompleted = false;

        EnsureRenderer();
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];

        if (transform.localScale != Vector3.zero)
            targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    public void EnableSproutGrowth(float duration = 1.2f, bool breathing = true, Vector3? customTargetScale = null)
    {
        isSprouting = true;
        sproutDuration = Mathf.Max(0.1f, duration);
        continuousBreathing = breathing;
        growthTimer = 0f;
        growthCompleted = false;

        if (customTargetScale.HasValue && customTargetScale.Value != Vector3.zero)
            targetScale = customTargetScale.Value;
        else if (transform.localScale != Vector3.zero)
            targetScale = transform.localScale;

        transform.localScale = Vector3.zero;
    }

    public void EnableFoliageSway(float speed = 2.2f, float maxAngle = 3.5f)
    {
        enableSway = true;
        swaySpeed = speed;
        swayAngle = maxAngle;
        swayOffset = Random.Range(0f, 100f);
        baseRotation = transform.rotation;
    }

    public void EnableFloatingFlame(float bobSpeed = 4.5f, float bobHeight = 0.035f)
    {
        enableFlameBobbing = true;
        flameBobSpeed = bobSpeed;
        flameBobHeight = bobHeight;
        flameOffset = Random.Range(0f, 100f);
        basePosition = transform.position;
    }

    [SerializeField] private SpriteRenderer flameHaloRenderer;

    public void ConfigureTorchLight(Color lightColor, float outerRadius = 2.2f, float innerRadius = 0.3f, float intensity = 1.25f)
    {
        isLightSource = true;
        baseLightIntensity = intensity;
        baseLightRadius = outerRadius;

        if (lightComponent == null)
            lightComponent = GetComponent<Light2D>();

        if (lightComponent == null)
            lightComponent = gameObject.AddComponent<Light2D>();

        lightComponent.lightType = Light2D.LightType.Point;
        lightComponent.color = lightColor;
        lightComponent.pointLightOuterRadius = outerRadius;
        lightComponent.pointLightInnerRadius = innerRadius;
        lightComponent.intensity = intensity;
        noiseOffset = Random.Range(0f, 500f);

        // Tạo vầng hào quang ánh lửa ấm áp (Fire Halo) phát sáng lan toả quanh đuốc
        if (flameHaloRenderer == null)
        {
            GameObject haloObj = new("Torch Fire Glow");
            haloObj.transform.SetParent(transform, false);
            haloObj.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            flameHaloRenderer = haloObj.AddComponent<SpriteRenderer>();
            flameHaloRenderer.sprite = EnemyEffectSpriteFactory.Disc;
            flameHaloRenderer.sortingOrder = 2; // Nằm sau thân đuốc và trên nền tường
            flameHaloRenderer.color = new Color(lightColor.r, lightColor.g * 0.9f, lightColor.b * 0.4f, 0.28f);
            haloObj.transform.localScale = Vector3.one * (outerRadius * 0.65f);
        }

        // Turn on floating flame bobbing automatically for torches
        EnableFloatingFlame(4.2f, 0.02f);
    }

    public void ConfigureMushroomGlow(Color glowColor, float outerRadius = 1.6f, float innerRadius = 0.2f, float intensity = 0.85f)
    {
        isLightSource = true;
        baseLightIntensity = intensity;
        baseLightRadius = outerRadius;

        if (lightComponent == null)
            lightComponent = GetComponent<Light2D>();

        if (lightComponent == null)
            lightComponent = gameObject.AddComponent<Light2D>();

        lightComponent.lightType = Light2D.LightType.Point;
        lightComponent.color = glowColor;
        lightComponent.pointLightOuterRadius = outerRadius;
        lightComponent.pointLightInnerRadius = innerRadius;
        lightComponent.intensity = intensity;
        noiseOffset = Random.Range(0f, 500f);
    }

    private void Awake()
    {
        EnsureRenderer();
        baseRotation = transform.rotation;
        basePosition = transform.position;
        targetScale = transform.localScale;
        noiseOffset = Random.Range(0f, 500f);
        swayOffset = Random.Range(0f, 100f);
        flameOffset = Random.Range(0f, 100f);
        pulseOffset = Random.Range(0f, 100f);

        if (frames != null && frames.Length > 0)
        {
            currentFrame = Random.Range(0, frames.Length);
            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private void Start()
    {
        basePosition = transform.position;
    }

    private void Update()
    {
        // 1. Sprout Growth & Breathing
        HandleGrowthAndBreathing();

        // 2. Sprite Animation (Sprout -> Idle Sequence or Continuous Loop)
        HandleSpriteAnimation();

        // 3. Gentle Foliage Swaying
        if (enableSway)
        {
            float zAngle = Mathf.Sin((Time.time * swaySpeed) + swayOffset) * swayAngle;
            transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, zAngle);
        }

        // 4. Floating / Wavering Flame (Ngọn lửa bồng bềnh lơ lửng)
        if (enableFlameBobbing)
        {
            float yOffset = Mathf.Sin((Time.time * flameBobSpeed) + flameOffset) * flameBobHeight;
            transform.position = basePosition + new Vector3(0f, yOffset, 0f);
        }

        // 5. Dynamic Torch Flickering & Breathing Halo (Lửa cháy lấp lửng)
        if (isLightSource && lightComponent != null && lightComponent.enabled)
        {
            HandleTorchLightFlicker();
        }
    }

    private void HandleGrowthAndBreathing()
    {
        if (isSprouting && !growthCompleted)
        {
            growthTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(growthTimer / sproutDuration);

            // Smooth cubic ease out with slight bouncy pop at completion
            float scaleMultiplier;
            if (progress < 0.85f)
            {
                float t = progress / 0.85f;
                scaleMultiplier = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.08f;
            }
            else
            {
                float t = (progress - 0.85f) / 0.15f;
                scaleMultiplier = Mathf.Lerp(1.08f, 1.0f, t);
            }

            transform.localScale = targetScale * scaleMultiplier;

            if (progress >= 1f)
            {
                growthCompleted = true;
                transform.localScale = targetScale;

                // If this had an idle sequence transition, switch to idle loop now
                if (transitionIdleFrames != null && transitionIdleFrames.Length > 0)
                {
                    frames = transitionIdleFrames;
                    transitionIdleFrames = null;
                    framesPerSecond = 5f;
                    currentFrame = 0;
                    frameTimer = 0f;
                }
            }
        }
        else if (continuousBreathing)
        {
            // Organic living pulse: trees, bushes, and plants slowly breathe
            float pulse = Mathf.Sin((Time.time * 1.6f) + pulseOffset) * 0.045f;
            transform.localScale = targetScale * (1f + pulse);
        }
    }

    private void HandleSpriteAnimation()
    {
        if (frames == null || frames.Length <= 1 || spriteRenderer == null)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;

        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            if (isSprouting && !growthCompleted && transitionIdleFrames != null)
            {
                // Play sprout frames sequentially without looping until done
                currentFrame = Mathf.Min(currentFrame + 1, frames.Length - 1);
            }
            else
            {
                currentFrame = (currentFrame + 1) % frames.Length;
            }
            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private void HandleTorchLightFlicker()
    {
        // Multilayer organic flicker: rapid chaotic noise + gentle rhythmic breath
        float fastFlicker = (Mathf.PerlinNoise((Time.time * 7f) + noiseOffset, 0f) - 0.5f) * flickerAmount;
        float slowBreathing = Mathf.Sin((Time.time * 3.5f) + noiseOffset) * (flickerAmount * 0.45f);
        if (lightComponent != null)
        {
            lightComponent.intensity = baseLightIntensity + fastFlicker + slowBreathing;

            // Radius breathes slightly so the illuminated halo floats/wavers on surrounding dungeon walls
            float haloRadiusDrift = (Mathf.PerlinNoise((Time.time * 2.8f) + noiseOffset + 35f, 0f) - 0.5f) * 0.25f;
            lightComponent.pointLightOuterRadius = Mathf.Max(0.4f, baseLightRadius + haloRadiusDrift);
        }

        // Đồng bộ hiệu ứng hào quang lửa ấm áp
        if (flameHaloRenderer != null)
        {
            float haloAlpha = 0.26f + (fastFlicker + slowBreathing) * 0.15f;
            Color c = flameHaloRenderer.color;
            c.a = Mathf.Clamp(haloAlpha, 0.12f, 0.42f);
            flameHaloRenderer.color = c;
            float scaleMul = 1f + (fastFlicker + slowBreathing) * 0.10f;
            flameHaloRenderer.transform.localScale = Vector3.one * (baseLightRadius * 0.65f * scaleMul);
        }
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }
}
