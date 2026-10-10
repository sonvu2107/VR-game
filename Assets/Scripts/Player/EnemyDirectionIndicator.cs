using UnityEngine;

/// <summary>
/// Small pixel triangle at the player's feet. It points to the closest living
/// enemy on the current floor and hides when none remain. No scene wiring is needed.
/// </summary>
public sealed class EnemyDirectionIndicator : MonoBehaviour
{
    private const float ScanInterval = 0.2f;
    private SpriteRenderer playerVisual;
    private SpriteRenderer arrow;
    private Transform target;
    private float nextScan;

    private void Start()
    {
        playerVisual = GetComponent<SpriteRenderer>();
        GameObject marker = new GameObject("Nearest Enemy Direction");
        arrow = marker.AddComponent<SpriteRenderer>();
        arrow.sprite = CreateTriangle();
        arrow.sortingLayerID = playerVisual != null ? playerVisual.sortingLayerID : 0;
        arrow.sortingOrder = playerVisual != null ? playerVisual.sortingOrder + 4 : 20;
        marker.transform.localScale = Vector3.one * 0.38f;
    }

    private void Update()
    {
        if (arrow == null) return;
        if (Time.time >= nextScan || target == null || !target.gameObject.activeInHierarchy)
        {
            nextScan = Time.time + ScanInterval;
            target = FindNearestLivingEnemy();
        }
        arrow.enabled = target != null && !GameManager.isGameOver && !GameManager.isWin;
        if (!arrow.enabled) return;

        Vector3 feet = playerVisual != null ? playerVisual.bounds.min : transform.position;
        arrow.transform.position = new Vector3(transform.position.x, feet.y - 0.19f, transform.position.z);
        Vector2 direction = target.position - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            arrow.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    private Transform FindNearestLivingEnemy()
    {
        Transform closest = null;
        float best = float.PositiveInfinity;
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            closest = enemy.transform;
        }
        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || enemy.GetComponent<EnemyHealth>() != null) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            closest = enemy.transform;
        }
        foreach (BossController boss in FindObjectsByType<BossController>(FindObjectsSortMode.None))
        {
            if (boss.IsDead || boss.GetComponent<EnemyHealth>() != null) continue;
            float distance = (boss.transform.position - transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            closest = boss.transform;
        }
        return closest;
    }

    private static Sprite CreateTriangle()
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Enemy Direction Triangle";
        texture.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[size * size];
        for (int y = 2; y <= 14; y++)
        {
            int halfWidth = Mathf.Max(1, (15 - y) / 2);
            for (int x = 8 - halfWidth; x <= 8 + halfWidth; x++)
            {
                bool edge = x == 8 - halfWidth || x == 8 + halfWidth || y == 2;
                // The upper half is the pointing tip: dark red stays readable against the gold body.
                bool tip = y >= 8;
                pixels[y * size + x] = tip
                    ? (edge ? new Color32(100, 9, 20, 255) : new Color32(185, 23, 35, 255))
                    : (edge ? new Color32(14, 22, 29, 255) : new Color32(255, 208, 61, 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 16f);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
