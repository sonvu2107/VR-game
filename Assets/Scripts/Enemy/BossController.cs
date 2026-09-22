using UnityEngine;

/// <summary>
/// Self-contained first boss used by the procedurally generated Boss room.
/// It deliberately builds its renderer, collider and health bar at runtime so
/// the dungeon can spawn a playable boss even before a hand-authored prefab exists.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("Ashen Warden Stats")]
    [Min(1)] public int maxHealth = 360;
    [Min(0f)] public float moveSpeed = 2.1f;
    [Min(0.1f)] public float attackRange = 2.1f;
    [Min(0.05f)] public float attackWindup = 0.55f;
    [Min(0f)] public float attackCooldown = 1.35f;
    [Min(0)] public int attackDamage = 2;
    [Header("Enraged Phase")]
    [Min(0.1f)] public float dashRange = 6f;
    [Min(0.1f)] public float dashDuration = 0.24f;
    [Min(0f)] public float dashDistance = 3.8f;
    [Min(0f)] public float enragedDashCooldown = 2.7f;
    [Header("World Health Bar")]
    [Min(0f)] public float healthBarHeight = 2.25f;
    [Min(0.1f)] public float healthBarWidth = 2.2f;

    private GameManager manager;
    private Transform player;
    private HealthController playerHealth;
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private SpriteRenderer healthBarBack;
    private SpriteRenderer healthBarFill;
    private int currentHealth;
    private bool isRegistered;
    private bool isDead;
    private bool isWindingUp;
    private bool isDashing;
    private float windupEndsAt;
    private float dashEndsAt;
    private float nextAttackAt;
    private float nextDashAt;
    private float flashEndsAt;
    private Vector2 dashDirection;
    private Vector3 baseScale;

    public static BossController CreateDefaultBoss(Vector3 position, GameManager gameManager)
    {
        GameObject boss = new GameObject("Ashen Warden");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        boss.layer = enemyLayer >= 0 ? enemyLayer : 6;
        boss.transform.position = position;
        BossController controller = boss.AddComponent<BossController>();
        controller.Initialize(gameManager);
        return controller;
    }

    public void Initialize(GameManager gameManager)
    {
        manager = gameManager;
        RegisterWithManager();
    }

    private void Start()
    {
        if (manager == null)
        {
            GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
            if (managerObject != null)
                manager = managerObject.GetComponent<GameManager>();
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<HealthController>();
        }

        CreateVisuals();
        RegisterWithManager();
    }

    private void Update()
    {
        if (isDead || player == null || playerHealth == null || playerHealth.currentHealth <= 0)
            return;

        UpdateHealthBar();
        UpdateFacing();
        UpdateVisual();

        if (isDashing)
        {
            transform.position += (Vector3)dashDirection * (dashDistance / dashDuration) * Time.deltaTime;
            if (Time.time >= dashEndsAt)
            {
                isDashing = false;
                TryDamagePlayer(attackRange * 0.9f);
                nextAttackAt = Time.time + attackCooldown;
            }
            return;
        }

        if (isWindingUp)
        {
            if (Time.time >= windupEndsAt)
            {
                isWindingUp = false;
                TryDamagePlayer(attackRange);
                nextAttackAt = Time.time + attackCooldown;
            }
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        bool enraged = currentHealth <= maxHealth / 2;
        if (enraged && distance > attackRange * 1.1f && distance <= dashRange && Time.time >= nextDashAt)
        {
            dashDirection = ((Vector2)(player.position - transform.position)).normalized;
            isDashing = true;
            dashEndsAt = Time.time + dashDuration;
            nextDashAt = Time.time + enragedDashCooldown;
            return;
        }

        if (distance > attackRange)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);
            return;
        }

        if (Time.time >= nextAttackAt)
        {
            isWindingUp = true;
            windupEndsAt = Time.time + attackWindup;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        flashEndsAt = Time.time + 0.1f;
        UpdateHealthBar();
        if (currentHealth <= 0)
            Die();
    }

    private void CreateVisuals()
    {
        currentHealth = maxHealth;
        baseScale = new Vector3(2.45f, 2.45f, 1f);
        transform.localScale = baseScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 5;

        frames = Resources.LoadAll<Sprite>("Bosses/AshenWarden_8f");
        System.Array.Sort(frames, (first, second) => string.CompareOrdinal(first.name, second.name));
        if (frames.Length > 0)
            spriteRenderer.sprite = frames[0];
        else
            Debug.LogWarning("Ashen Warden sprite sheet could not be loaded from Resources/Bosses.");

        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        if (collider == null)
            collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.42f;
        collider.isTrigger = true;

        CreateHealthBar();
    }

    private void CreateHealthBar()
    {
        Sprite whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        healthBarBack = CreateBarRenderer("Ashen Warden Health Back", whiteSprite, new Color(0.08f, 0.02f, 0.02f, 0.9f), 20);
        healthBarFill = CreateBarRenderer("Ashen Warden Health Fill", whiteSprite, new Color(0.9f, 0.18f, 0.08f, 1f), 21);
        UpdateHealthBar();
    }

    private static SpriteRenderer CreateBarRenderer(string name, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject bar = new GameObject(name);
        SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateHealthBar()
    {
        if (healthBarBack == null || healthBarFill == null)
            return;

        Vector3 position = transform.position + Vector3.up * healthBarHeight;
        float healthPercent = maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth;
        healthBarBack.transform.position = position;
        healthBarBack.transform.localScale = new Vector3(healthBarWidth, 0.12f, 1f);
        healthBarFill.transform.position = position + Vector3.left * healthBarWidth * (1f - healthPercent) * 0.5f;
        healthBarFill.transform.localScale = new Vector3(healthBarWidth * healthPercent, 0.08f, 1f);
    }

    private void UpdateFacing()
    {
        bool faceLeft = player.position.x < transform.position.x;
        transform.localScale = new Vector3(faceLeft ? -Mathf.Abs(baseScale.x) : Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
            return;

        if (Time.time < flashEndsAt)
            spriteRenderer.color = new Color(1f, 0.45f, 0.35f, 1f);
        else
            spriteRenderer.color = Color.white;

        int frameIndex;
        if (isDashing)
            frameIndex = 5;
        else if (isWindingUp)
            frameIndex = 3 + Mathf.Min(2, Mathf.FloorToInt((attackWindup - (windupEndsAt - Time.time)) / attackWindup * 3f));
        else if (Time.time < nextAttackAt - attackCooldown + 0.14f)
            frameIndex = 6;
        else
            frameIndex = Mathf.FloorToInt(Time.time * 5f) % 3;

        spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private void TryDamagePlayer(float range)
    {
        if (playerHealth != null && Vector2.Distance(transform.position, player.position) <= range)
            playerHealth.TakeDamage(attackDamage);
    }

    private void RegisterWithManager()
    {
        if (isRegistered || manager == null)
            return;

        isRegistered = true;
        manager.enemyCount++;
        manager.UpdateCounter();
    }

    private void Die()
    {
        isDead = true;
        if (healthBarBack != null)
            Destroy(healthBarBack.gameObject);
        if (healthBarFill != null)
            Destroy(healthBarFill.gameObject);

        if (isRegistered && manager != null)
        {
            manager.enemyCount = Mathf.Max(0, manager.enemyCount - 1);
            manager.UpdateCounter();
        }

        Destroy(gameObject);
    }
}
