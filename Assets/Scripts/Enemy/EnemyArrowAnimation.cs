// Hoạt ảnh 4 khung cho mũi tên cung thủ. Sprite được nạp một lần và tái dùng
// cho mọi phát bắn để tránh tải Resources liên tục trong chiến đấu.
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemyArrowAnimation : MonoBehaviour
{
    private static Sprite[] cachedFrames;
    private Sprite[] frames;
    private SpriteRenderer spriteRenderer;
    private float elapsed;

    public static Sprite[] LoadFrames()
    {
        if (cachedFrames != null) return cachedFrames;
        Sprite[] loaded = new Sprite[4];
        for (int i = 0; i < loaded.Length; i++)
        {
            loaded[i] = Resources.Load<Sprite>($"Enemy/Arrows/arrow_{i + 1}");
            if (loaded[i] == null)
            {
                Debug.LogWarning("Missing Enemy/Arrows arrow sprite. Using default projectile visual.");
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
        if (frames == null || frames.Length == 0) return;
        elapsed += Time.deltaTime;
        spriteRenderer.sprite = frames[Mathf.FloorToInt(elapsed * 10f) % frames.Length];
    }
}
