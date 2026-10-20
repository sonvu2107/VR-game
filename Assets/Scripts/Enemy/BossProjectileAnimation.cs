using UnityEngine;

/// <summary>Plays the twelve separately-authored PNG frames used by the final boss projectile.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossProjectileAnimation : MonoBehaviour
{
    private const string ResourcePath = "Enemy/BossProjectiles/CrimsonSoulSmooth/frame_";
    private static Sprite[] cachedFrames;

    private Sprite[] frames;
    private SpriteRenderer spriteRenderer;
    private float elapsed;

    public static Sprite[] LoadFrames()
    {
        if (cachedFrames != null) return cachedFrames;

        Sprite[] loaded = new Sprite[12];
        for (int i = 0; i < loaded.Length; i++)
        {
            loaded[i] = Resources.Load<Sprite>($"{ResourcePath}{i + 1:00}");
            if (loaded[i] == null)
            {
                Debug.LogWarning($"Missing final-boss projectile frame {i + 1}. Using the default projectile visual.");
                return null;
            }
        }

        cachedFrames = loaded;
        return cachedFrames;
    }

    public void SetFrames(Sprite[] animationFrames)
    {
        frames = animationFrames;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || spriteRenderer == null) return;
        elapsed += Time.deltaTime;
        spriteRenderer.sprite = frames[Mathf.FloorToInt(elapsed * 12f) % frames.Length];
    }
}
