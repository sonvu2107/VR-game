using UnityEngine;

[DisallowMultipleComponent]
public sealed class PowerupChargeVfx : MonoBehaviour
{
    private const string ResourcePath = "VFX/Pipoya/TimeMagic/pipo-btleffect213_192";
    private const int FrameSize = 192;
    private const int Columns = 5;
    private const int Rows = 4;
    private const int FrameCount = Columns * Rows;
    private const int LoopStartFrame = 5;
    private const int LoopEndFrame = FrameCount - 1;
    private const float PixelsPerUnit = 192f;

    private static Sprite[] cachedFrames;
    private static bool frameLoadAttempted;

    [SerializeField, Min(0.01f)] private float frameDuration = 0.07f;
    [SerializeField] private Vector3 visualCenterOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float minimumHeightMultiplier = 1.4f;
    [SerializeField, Min(0.01f)] private float maximumHeightMultiplier = 1.65f;

    private SpriteRenderer effectRenderer;
    private SpriteRenderer playerRenderer;
    private Transform visualAnchor;
    private float frameTimer;
    private float normalizedCharge;
    private int frameIndex;
    private int animationStepCount;
    private int completedLoopCount;
    private bool charging;

    public bool IsUsingTimeMagicFrames =>
        cachedFrames != null && cachedFrames.Length == FrameCount && effectRenderer != null;
    public bool IsVisible => charging && effectRenderer != null && effectRenderer.enabled;
    public int CurrentFrameIndex => frameIndex;
    public int AnimationStepCount => animationStepCount;
    public int CompletedLoopCount => completedLoopCount;

    public void SetAnchor(Transform anchor)
    {
        visualAnchor = anchor;
        if (charging)
            AlignToPlayerVisual();
    }

    private void Awake()
    {
        EnsureRenderer();
        Hide();
    }

    private void Update()
    {
        if (!charging || effectRenderer == null || cachedFrames == null)
            return;

        frameTimer += Time.deltaTime;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            if (frameIndex < LoopStartFrame)
            {
                frameIndex++;
            }
            else
            {
                frameIndex++;
                if (frameIndex > LoopEndFrame)
                {
                    frameIndex = LoopStartFrame;
                    completedLoopCount++;
                }
            }

            animationStepCount++;
            effectRenderer.sprite = cachedFrames[frameIndex];
        }
    }

    private void LateUpdate()
    {
        if (!charging)
            return;

        AlignToPlayerVisual();
        ApplyScale();
    }

    private void OnDisable()
    {
        Hide();
    }

    public void Show(float normalizedCharge)
    {
        EnsureRenderer();
        Sprite[] frames = GetOrLoadFrames();
        if (effectRenderer == null || frames == null)
            return;

        if (!charging)
        {
            frameIndex = 0;
            frameTimer = 0f;
            animationStepCount = 0;
            completedLoopCount = 0;
            effectRenderer.sprite = frames[0];
        }

        charging = true;
        this.normalizedCharge = Mathf.Clamp01(normalizedCharge);
        effectRenderer.enabled = true;
        AlignToPlayerVisual();
        ApplyScale();
    }

    public void Hide()
    {
        charging = false;
        frameTimer = 0f;
        normalizedCharge = 0f;
        frameIndex = 0;
        animationStepCount = 0;
        completedLoopCount = 0;

        if (effectRenderer != null)
            effectRenderer.enabled = false;
    }

    private void EnsureRenderer()
    {
        if (effectRenderer != null)
            return;

        Transform existing = transform.Find("Powerup Charge VFX");
        GameObject effectObject;
        if (existing != null)
        {
            effectObject = existing.gameObject;
        }
        else
        {
            effectObject = new GameObject("Powerup Charge VFX");
            effectObject.transform.SetParent(transform, false);
        }

        effectObject.layer = gameObject.layer;
        effectObject.transform.localPosition = Vector3.zero;
        effectObject.transform.localRotation = Quaternion.identity;
        effectObject.transform.localScale = Vector3.one;
        effectRenderer = effectObject.GetComponent<SpriteRenderer>();
        if (effectRenderer == null)
            effectRenderer = effectObject.AddComponent<SpriteRenderer>();

        playerRenderer = GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            effectRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            effectRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
        }

        effectRenderer.color = Color.white;
        effectRenderer.enabled = false;
    }

    private void AlignToPlayerVisual()
    {
        if (effectRenderer == null || playerRenderer == null)
            return;

        float centerX = visualAnchor != null
            ? visualAnchor.position.x
            : playerRenderer.bounds.center.x;
        effectRenderer.transform.position = new Vector3(
            centerX + visualCenterOffset.x,
            transform.position.y + visualCenterOffset.y,
            transform.position.z + visualCenterOffset.z);
    }

    private void ApplyScale()
    {
        if (effectRenderer == null || playerRenderer == null)
            return;

        float playerHeight = Mathf.Max(0.01f, playerRenderer.bounds.size.y);
        float desiredWorldHeight = playerHeight * Mathf.Lerp(
            minimumHeightMultiplier,
            maximumHeightMultiplier,
            normalizedCharge);
        float parentScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float localScale = desiredWorldHeight / parentScale;
        effectRenderer.transform.localScale = new Vector3(localScale, localScale, 1f);
    }

    private static Sprite[] GetOrLoadFrames()
    {
        if (cachedFrames != null)
            return cachedFrames;

        if (frameLoadAttempted)
            return null;

        frameLoadAttempted = true;
        Texture2D sheet = Resources.Load<Texture2D>(ResourcePath);
        if (sheet == null || sheet.width != Columns * FrameSize || sheet.height != Rows * FrameSize)
        {
            Debug.LogWarning(
                $"Power-up VFX chưa sẵn sàng. Cần texture {ResourcePath} " +
                $"kích thước {Columns * FrameSize}x{Rows * FrameSize}px.");
            return null;
        }

        sheet.filterMode = FilterMode.Bilinear;
        sheet.wrapMode = TextureWrapMode.Clamp;

        Sprite[] frames = new Sprite[FrameCount];
        for (int index = 0; index < FrameCount; index++)
        {
            int column = index % Columns;
            int rowFromTop = index / Columns;
            float x = column * FrameSize;
            float y = sheet.height - ((rowFromTop + 1) * FrameSize);
            frames[index] = Sprite.Create(
                sheet,
                new Rect(x, y, FrameSize, FrameSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            frames[index].name = $"Pipoya TimeMagic Clock Circle {index + 1:00}";
        }

        cachedFrames = frames;
        return cachedFrames;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        cachedFrames = null;
        frameLoadAttempted = false;
    }
}
