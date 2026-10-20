using UnityEngine;

/// <summary>Four separate PNG frames for the level-three arrow trap.</summary>
public sealed class ArrowTrapFrameVisual : MonoBehaviour
{
    private static Sprite[] cachedFrames;
    private SpriteRenderer spriteRenderer;

    public int CurrentFrame { get; private set; } = -1;

    public void Initialize()
    {
        if (cachedFrames == null || cachedFrames.Length != 4)
        {
            cachedFrames = new[]
            {
                Resources.Load<Sprite>("MapThemes/ArrowCrypt/arrow_trap_00_idle"),
                Resources.Load<Sprite>("MapThemes/ArrowCrypt/arrow_trap_01_warning"),
                Resources.Load<Sprite>("MapThemes/ArrowCrypt/arrow_trap_02_fire"),
                Resources.Load<Sprite>("MapThemes/ArrowCrypt/arrow_trap_03_reset")
            };
        }

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 3;
        SetFrame(0);
    }

    public void SetFrame(int index)
    {
        if (spriteRenderer == null || cachedFrames == null || cachedFrames.Length == 0)
            return;

        int frame = Mathf.Clamp(index, 0, cachedFrames.Length - 1);
        if (cachedFrames[frame] == null || CurrentFrame == frame)
            return;

        CurrentFrame = frame;
        spriteRenderer.sprite = cachedFrames[frame];
        spriteRenderer.color = Color.white;
    }
}
