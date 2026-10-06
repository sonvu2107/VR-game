using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ranged enemy that keeps distance from the player and fires projectiles.
/// Used from Level 3 onwards. Retreats when player gets too close.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class RangedEnemy : MonoBehaviour
{
    [Header("Detection & Range")]
    public float detectRange = 12f;
    public float preferredRange = 6f;
    public float tooCloseRange = 3f;
    public float retreatSpeed = 4f;

    [Header("Movement")]
    public float wanderSpeed = 2f;
    public float repositionSpeed = 3f;

    [Header("Combat")]
    public int maxHealth = 15;
    public int projectileDamage = 2;
    public float fireRate = 2f;
    public float projectileSpeed = 8f;
    public float projectileLifetime = 4f;

    [Header("Projectile Visual")]
    [Tooltip("Optional projectile prefab. If null, a default circle is created.")]
    public GameObject projectilePrefab;
    public Color projectileColor = new Color(0.3f, 0.8f, 1f, 1f);
    public float projectileSize = 0.2f;

    [Header("Health Bar")]
    public bool showHealthBar = true;
    public Vector3 healthBarOffset = new Vector3(0f, 1.3f, 0f);

    [Header("Floor Scaling")]
    public int hpPerFloor = 3;
    public int damagePerFloor = 1;

    [Header("References")]
    public LayerMask playerLayer;
    public LayerMask wallLayer;

    // Internal
    private NavMeshAgent agent;
    private Animator animator;
    private GameManager manager;
    private GameObject target;
    private HealthController playerHealth;

    private int currentHealth;
    private int scaledMaxHealth;
    private bool isDead;
    private float fireCooldown;
    private FloatingHealthBar healthBar;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        manager = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();

        int floor = manager != null ? manager.CurrentFloor : 1;
        int bonus = Mathf.Max(0, floor - 1);
        scaledMaxHealth = maxHealth + hpPerFloor * bonus;
        currentHealth = scaledMaxHealth;
        projectileDamage += damagePerFloor * bonus;

        target = GameObject.Find("Player");
        if (target != null)
            playerHealth = target.GetComponent<HealthController>();

        if (showHealthBar)
        {
            healthBar = gameObject.AddComponent<FloatingHealthBar>();
            healthBar.offset = healthBarOffset;
            healthBar.barWidth = 0.8f;
            healthBar.fullHealthColor = new Color(0.3f, 0.5f, 1f, 1f);   // Blue for ranged
            healthBar.lowHealthColor = new Color(0.1f, 0.1f, 0.4f, 1f);
        }
    }

    private void Update()
    {
        if (isDead || target == null) return;
        if (playerHealth != null && playerHealth.CurrentHP <= 0 && playerHealth.CurrentLives <= 0)
        {
            enabled = false;
            return;
        }

        float dist = Vector2.Distance(transform.position, target.transform.position);
        fireCooldown -= Time.deltaTime;

        if (dist > detectRange)
        {
            // Idle / Wander
            animator.SetFloat("WalkSpeed", 0);
            return;
        }

        FaceTarget();

        if (dist < tooCloseRange)
        {
            // Too close — retreat!
            Retreat();
        }
        else if (dist > preferredRange + 1f)
        {
            // Too far — move closer
            MoveTowards();
        }
        else
        {
            // In range — stop and shoot
            agent.ResetPath();
            animator.SetFloat("WalkSpeed", 0);

            if (fireCooldown <= 0f && HasLineOfSight())
            {
                FireProjectile();
                fireCooldown = fireRate;
            }
        }
    }

    private void Retreat()
    {
        Vector2 awayDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
        Vector3 retreatPos = transform.position + (Vector3)awayDir * 4f;
        agent.speed = retreatSpeed;
        agent.SetDestination(retreatPos);
        animator.SetFloat("WalkSpeed", retreatSpeed);
    }

    private void MoveTowards()
    {
        agent.speed = repositionSpeed;
        agent.SetDestination(target.transform.position);
        animator.SetFloat("WalkSpeed", repositionSpeed);
    }

    private bool HasLineOfSight()
    {
        Vector2 dir = (target.transform.position - transform.position).normalized;
        float dist = Vector2.Distance(transform.position, target.transform.position);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, wallLayer);
        return hit.collider == null; // No wall in the way
    }

    private void FireProjectile()
    {
        if (animator != null)
            animator.SetTrigger("Attack");

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        GameObject proj;
        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Create a simple circle projectile
            proj = new GameObject("EnemyProjectile");
            proj.transform.position = transform.position;

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sr.color = projectileColor;
            sr.sortingOrder = 10;
            proj.transform.localScale = Vector3.one * projectileSize;

            CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
        }

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = proj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
        }

        rb.linearVelocity = dir * projectileSpeed;

        // Add projectile behavior
        EnemyProjectile ep = proj.AddComponent<EnemyProjectile>();
        ep.damage = projectileDamage;
        ep.lifetime = projectileLifetime;

        Destroy(proj, projectileLifetime);
    }

    // ──────────── Combat ────────────

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;
        if (animator != null) animator.SetTrigger("Hurt");
        if (healthBar != null)
            healthBar.UpdateBar((float)currentHealth / scaledMaxHealth);
        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        manager?.EnemyDefeated();

        ItemDropper dropper = GetComponent<ItemDropper>();
        if (dropper != null) dropper.TryDrop(transform.position);

        if (animator != null) animator.SetTrigger("isDead");
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (agent != null) agent.enabled = false;
        
        Destroy(gameObject, 1.5f); // Thực sự xóa quái khỏi màn hình
        enabled = false;
    }

    // This method is called by the Animation Event "HurtPlayer" on the 'attack' animation.
    public void HurtPlayer()
    {
        // Ranged enemy shoots projectiles instead of melee hitting here,
        // but this empty method prevents the Unity console error.
    }

    private void FaceTarget()
    {
        if (target.transform.position.x < transform.position.x)
            transform.localScale = new Vector2(-Mathf.Abs(transform.localScale.x), transform.localScale.y);
        else
            transform.localScale = new Vector2(Mathf.Abs(transform.localScale.x), transform.localScale.y);
    }

    public void PlaySFX(AudioClip clip)
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) audio.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, preferredRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, tooCloseRange);
    }
}
