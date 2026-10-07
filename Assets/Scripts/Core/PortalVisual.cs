using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PortalVisual : MonoBehaviour
{
    private const int FrameCount = 7;
    private const string FrameResourcePrefix = "Portal/Frames/portal1_frame_";
    private const float PixelsPerUnit = 64f;

    [SerializeField, Min(0.02f)] private float secondsPerFrame = 0.1f;
    [SerializeField, Range(0.05f, 1f)] private float lockedSpeedMultiplier = 0.35f;

    private static Sprite[] cachedFrames;
    private static bool frameLoadAttempted;
    private static bool missingFramesWarningLogged;

    private SpriteRenderer portalRenderer;
    private int currentFrame;
    private float frameTimer;
    private float speedMultiplier = 1f;

    public bool IsUsingPortalFrames => cachedFrames != null && cachedFrames.Length == FrameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        cachedFrames = null;
        frameLoadAttempted = false;
        missingFramesWarningLogged = false;
    }

    private void Awake()
    {
        portalRenderer = GetComponent<SpriteRenderer>();
        ApplyFirstFrame();
    }

    private void OnEnable()
    {
        currentFrame = 0;
        frameTimer = 0f;
        ApplyFirstFrame();
    }

    private void Update()
    {
        Sprite[] frames = GetOrLoadFrames();
        if (frames == null || frames.Length == 0 || portalRenderer == null)
            return;

        frameTimer += Time.deltaTime * speedMultiplier;
        if (frameTimer < secondsPerFrame)
            return;

        int elapsedFrames = Mathf.FloorToInt(frameTimer / secondsPerFrame);
        frameTimer -= elapsedFrames * secondsPerFrame;
        currentFrame = (currentFrame + elapsedFrames) % frames.Length;
        portalRenderer.sprite = frames[currentFrame];
    }

    public void SetUnlocked(bool unlocked)
    {
        speedMultiplier = unlocked ? 1f : lockedSpeedMultiplier;
    }

    private void ApplyFirstFrame()
    {
        Sprite[] frames = GetOrLoadFrames();
        if (frames == null || frames.Length == 0 || portalRenderer == null)
            return;

        currentFrame = Mathf.Clamp(currentFrame, 0, frames.Length - 1);
        portalRenderer.sprite = frames[currentFrame];
    }

    private static Sprite[] GetOrLoadFrames()
    {
        if (frameLoadAttempted)
            return cachedFrames;

        frameLoadAttempted = true;
        Sprite[] frames = new Sprite[FrameCount];
        for (int index = 0; index < FrameCount; index++)
        {
            string resourcePath = $"{FrameResourcePrefix}{index + 1}";
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                DestroyCreatedFrames(frames);
                cachedFrames = new Sprite[0];
                LogMissingFramesWarning(resourcePath);
                return cachedFrames;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Sprite frame = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            frame.name = $"Portal Frame {index + 1}";
            frames[index] = frame;
        }

        cachedFrames = frames;
        return cachedFrames;
    }

    private static void DestroyCreatedFrames(Sprite[] frames)
    {
        foreach (Sprite frame in frames)
        {
            if (frame != null)
                Destroy(frame);
        }
    }

    private static void LogMissingFramesWarning(string missingResourcePath)
    {
        if (missingFramesWarningLogged)
            return;

        missingFramesWarningLogged = true;
        Debug.LogWarning(
            $"Portal frame '{missingResourcePath}' was not found. " +
            "The floor exit will use its built-in fallback visual.");
    }
}
