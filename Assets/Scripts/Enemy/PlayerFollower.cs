using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Base enemy AI. Supports optional floating health bar (for bosses),
/// floor-based stat scaling, and item drops on death.
/// </summary>

public class Enemy : MonoBehaviour
{
    public float maxDistance;
    public float minDistance;
    public float chaseSpeed;
    public float wanderSpeed;
    public float attackCooldown = 1.5f;
    public int maxHealth = 2;
    public Transform attackPoint;
    public float attackRange;
    public LayerMask playerLayer;
    public int attackDamage = 1;
    public AudioSource audioSource;
    public GameManager manager;

    [Header("Health Bar (Boss/Elite)")]
    [Tooltip("Show a floating HP bar above this enemy.")]
    public bool showHealthBar = false;
    [Tooltip("Offset of health bar above enemy.")]
    public Vector3 healthBarOffset = new Vector3(0f, 1.3f, 0f);
    [Tooltip("Width of the health bar.")]
    public float healthBarWidth = 1.0f;

    [Header("Floor Scaling")]
    [Tooltip("Extra HP per floor beyond floor 1.")]
    public int hpPerFloor = 0;
    [Tooltip("Extra damage per floor beyond floor 1.")]
    public int damagePerFloor = 0;
    
    private GameObject target;
    private NavMeshAgent agent;
    private Animator animator;
    private HealthController playerHealthController;
    
    // Phản công khi bị đánh
    private float retaliationTimer = 0f;
    private const float retaliationWindow = 1.5f;
    
    // Circle-strafe: bao vây quanh player thay vì đứng im
    private float strafeAngle;           // Góc hiện tại xung quanh player
    private float strafeDir = 1f;        // 1 = trái, -1 = phải
    private float strafeSwitchTimer = 0f; // Đổi hướng định kỳ
    
    private float changeDirectionCooldown = 2f;
    private double angleChange;
    private float distance;
    private float wanderSpeedActual;
    private bool attackBlocked;
    private bool isDead;
    private int currentHealth;
    private FloatingHealthBar healthBar;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        manager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = chaseSpeed;
        wanderSpeedActual = wanderSpeed;

        // Apply floor scaling
        int floor = manager != null ? manager.CurrentFloor : 1;
        int floorBonus = Mathf.Max(0, floor - 1);
        currentHealth = maxHealth + (hpPerFloor * floorBonus);
        attackDamage += damagePerFloor * floorBonus;

        target = GameObject.Find("Player");
        playerHealthController = target.GetComponent<HealthController>();

        // Create floating health bar if configured
        if (showHealthBar)
        {
            healthBar = gameObject.AddComponent<FloatingHealthBar>();
            healthBar.offset = healthBarOffset;
            healthBar.barWidth = healthBarWidth;
            healthBar.fullHealthColor = new Color(0.9f, 0.15f, 0.15f, 1f); // Red for enemies
            healthBar.lowHealthColor = new Color(0.3f, 0.05f, 0.05f, 1f);  // Dark red
        }
    }

    void Update()
    {
        if (playerHealthController.CurrentHP <= 0 && playerHealthController.CurrentLives <= 0)
        {
            this.enabled = false;
            return;
        }

        distance = Vector2.Distance(transform.position, target.transform.position);

        // Luôn quay mặt về phía player
        float facingX = target.transform.position.x < transform.position.x
            ? -Mathf.Abs(transform.localScale.x)
            :  Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector2(facingX, transform.localScale.y);

        if (distance > maxDistance)
        {
            // Ở xa: lang thang bình thường
            animator.SetFloat("WalkSpeed", wanderSpeedActual);
            GetRandomDirectionChange();
            Vector3 toMove = new Vector3((float)System.Math.Cos(angleChange), (float)System.Math.Sin(angleChange), 0);
            transform.position = Vector2.MoveTowards(transform.position, transform.position + toMove * 5, wanderSpeedActual * Time.deltaTime);
        }
        else if (distance > minDistance)
        {
            // Đang lao vào: đuổi theo trực tiếp
            animator.SetFloat("WalkSpeed", 101);
            agent.SetDestination(target.transform.position);
        }
        else
        {
            // Đang áp sát: kết hợp di chuyển VÀ tấn công
            agent.ResetPath();
            
            // Đổi chiều vòng định kỳ
            strafeSwitchTimer -= Time.deltaTime;
            if (strafeSwitchTimer <= 0f)
            {
                strafeDir = (Random.value > 0.5f) ? 1f : -1f;
                strafeSwitchTimer = Random.Range(0.8f, 2f);
            }

            // Di chuyển: xen kẽ giữa strafe nhẹ và lao thẳng vào player
            if (!attackBlocked)
            {
                // Lao thẳng vào player để đánh
                animator.SetFloat("WalkSpeed", 101);
                transform.position = Vector2.MoveTowards(transform.position, 
                    target.transform.position, chaseSpeed * Time.deltaTime);
                AttackPlayer();
            }
            else
            {
                // Đang chờ cooldown: di chuyển vòng nhẹ quanh player
                animator.SetFloat("WalkSpeed", 50);
                strafeAngle += strafeDir * 120f * Time.deltaTime;
                float rad = strafeAngle * Mathf.Deg2Rad;
                Vector3 strafePos = target.transform.position
                    + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * (minDistance * 0.8f);
                transform.position = Vector2.MoveTowards(transform.position, 
                    strafePos, chaseSpeed * 0.5f * Time.deltaTime);
            }
        }
    }
    
    private void GetRandomDirectionChange()
    {
        changeDirectionCooldown -= Time.deltaTime;

        if (!(changeDirectionCooldown <= 0)) return;
        
        if (Random.Range(0,10) >= 5)
        {
            wanderSpeedActual = wanderSpeed;
            angleChange = Random.Range(-180f, 180f);
        }
        else
            wanderSpeedActual = 0;
        
        changeDirectionCooldown = Random.Range(1f, 5f);
    }

    private void AttackPlayer()
    {
        if (attackBlocked)
            return;
        
        animator.SetTrigger("Attack");
        attackBlocked = true;
        StartCoroutine(AttackSequence());
    }

    private void HurtPlayer()
    {
        // Check trực tiếp khoảng cách tới player
        float distToPlayer = Vector2.Distance(transform.position, target.transform.position);
        if (distToPlayer <= attackRange + minDistance + 0.5f)
            playerHealthController.TakeDamage(attackDamage);
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        animator.SetTrigger("Hurt");

        // Update health bar
        int totalMaxHP = maxHealth + (hpPerFloor * Mathf.Max(0, (manager != null ? manager.CurrentFloor : 1) - 1));
        if (healthBar != null)
            healthBar.UpdateBar((float)currentHealth / totalMaxHP);
        
        // Phản công ngay khi bị đánh – reset cooldown và đuổi theo player
        retaliationTimer = retaliationWindow;
        attackBlocked = false; // Cho phép đánh ngay lập tức
        if (agent != null && target != null)
            agent.SetDestination(target.transform.position); // Lao vào đánh trả

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        manager?.EnemyDefeated();

        // --- Item drop ---
        ItemDropper dropper = GetComponent<ItemDropper>();
        if (dropper != null)
            dropper.TryDrop(transform.position);

        animator.SetBool("isDead", true);
        GetComponent<Collider2D>().enabled = false;

        this.enabled = false;
    }
    
    /// <summary>
    /// Gọi trực tiếp HurtPlayer sau 0.3s (không phụ thuộc animation event)
    /// rồi chờ cooldown xong mới cho đánh tiếp.
    /// </summary>
    private IEnumerator AttackSequence()
    {
        yield return new WaitForSeconds(0.3f); // Chờ animation chém tới
        HurtPlayer(); // GÂY SÁT THƯƠNG TRỰC TIẾP
        yield return new WaitForSeconds(attackCooldown);
        attackBlocked = false;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        changeDirectionCooldown = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;
        
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    public void PlaySFX(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.PlayOneShot(clip);
    }
}
