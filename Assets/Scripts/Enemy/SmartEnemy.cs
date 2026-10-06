using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Advanced enemy AI with smarter behaviors:
/// - Circles/flanks the player instead of charging straight
/// - Dodges away when player is nearby and attacking
/// - Charges in for quick hit-and-run attacks
/// - Teleport/dash short distances
/// Attach to an enemy prefab WITH NavMeshAgent + Animator + Collider2D.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SmartEnemy : MonoBehaviour
{
    public enum AIState { Idle, Circling, Charging, Attacking, Dodging, Retreating }

    [Header("Detection")]
    public float detectRange = 10f;
    public float attackRange = 1.5f;
    public float personalSpace = 2.5f;

    [Header("Movement")]
    public float circleSpeed = 3f;
    public float chargeSpeed = 8f;
    public float retreatSpeed = 5f;
    [Tooltip("How often the enemy changes circle direction (seconds).")]
    public float circleChangeInterval = 2f;

    [Header("Combat")]
    public int maxHealth = 30;
    public int attackDamage = 2;
    public float attackCooldown = 2f;
    public Transform attackPoint;
    public float attackHitRange = 0.8f;
    public LayerMask playerLayer;

    [Header("Dodge")]
    [Tooltip("Chance to dodge when player attacks nearby (0-1).")]
    [Range(0f, 1f)] public float dodgeChance = 0.5f;
    public float dodgeSpeed = 12f;
    public float dodgeDuration = 0.2f;
    public float dodgeCooldown = 1.5f;

    [Header("Charge Attack")]
    [Tooltip("How often the enemy dashes at the player (seconds).")]
    public float chargeInterval = 6f;
    public float chargeDuration = 0.4f;

    [Header("Health Bar")]
    public bool showHealthBar = true;
    public Vector3 healthBarOffset = new Vector3(0f, 1.3f, 0f);

    [Header("Floor Scaling")]
    public int hpPerFloor = 5;
    public int damagePerFloor = 1;

    // Internal
    private NavMeshAgent agent;
    private Animator animator;
    private GameManager manager;
    private GameObject target;
    private HealthController playerHealth;

    private AIState currentState = AIState.Idle;
    private int currentHealth;
    private int scaledMaxHealth;
    private bool isDead;
    private bool attackBlocked;
    private float circleAngle;
    private float circleTimer;
    private int circleDirection = 1; // 1 = clockwise, -1 = counter
    private float chargeTimer;
    private float dodgeCooldownTimer;
    private FloatingHealthBar healthBar;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        manager = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();

        // Floor scaling
        int floor = manager != null ? manager.CurrentFloor : 1;
        int bonus = Mathf.Max(0, floor - 1);
        scaledMaxHealth = maxHealth + hpPerFloor * bonus;
        currentHealth = scaledMaxHealth;
        attackDamage += damagePerFloor * bonus;

        target = GameObject.Find("Player");
        if (target != null)
            playerHealth = target.GetComponent<HealthController>();

        // Health bar
        if (showHealthBar)
        {
            healthBar = gameObject.AddComponent<FloatingHealthBar>();
            healthBar.offset = healthBarOffset;
            healthBar.barWidth = 1.0f;
            healthBar.fullHealthColor = new Color(0.85f, 0.4f, 0.9f, 1f);  // Purple for smart enemies
            healthBar.lowHealthColor = new Color(0.4f, 0.1f, 0.2f, 1f);
        }

        circleAngle = Random.Range(0f, 360f);
        chargeTimer = chargeInterval * 0.5f; // First charge comes sooner
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

        // Timers
        chargeTimer -= Time.deltaTime;
        circleTimer -= Time.deltaTime;
        if (dodgeCooldownTimer > 0) dodgeCooldownTimer -= Time.deltaTime;

        // State machine
        switch (currentState)
        {
            case AIState.Idle:
                HandleIdle(dist);
                break;
            case AIState.Circling:
                HandleCircling(dist);
                break;
            case AIState.Charging:
                HandleCharging(dist);
                break;
            case AIState.Attacking:
                // Attacking is handled via coroutine
                break;
            case AIState.Dodging:
                // Dodging is handled via coroutine
                break;
            case AIState.Retreating:
                HandleRetreating(dist);
                break;
        }

        // Face player
        FaceTarget();
    }

    // ──────────── State Handlers ────────────

    private void HandleIdle(float dist)
    {
        if (dist < detectRange)
        {
            SetState(AIState.Circling);
        }
        else
        {
            animator.SetFloat("WalkSpeed", 0);
        }
    }

    private void HandleCircling(float dist)
    {
        if (dist > detectRange)
        {
            SetState(AIState.Idle);
            return;
        }

        // Try dodge if player is attacking nearby
        if (dist < personalSpace && TryDodge())
            return;

        // Time to charge?
        if (chargeTimer <= 0f && dist > attackRange)
        {
            chargeTimer = chargeInterval + Random.Range(-1f, 1f);
            SetState(AIState.Charging);
            StartCoroutine(ChargeRoutine());
            return;
        }

        // In attack range? Attack!
        if (dist <= attackRange && !attackBlocked)
        {
            SetState(AIState.Attacking);
            StartCoroutine(AttackRoutine());
            return;
        }

        // Circle the player
        if (circleTimer <= 0f)
        {
            circleDirection = Random.value > 0.5f ? 1 : -1;
            circleTimer = circleChangeInterval;
        }

        Vector2 toPlayer = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x) * circleDirection;

        // Maintain distance: move closer if too far, away if too close
        Vector2 distanceAdjust = Vector2.zero;
        float idealDist = (attackRange + personalSpace) * 0.5f;
        if (dist > idealDist + 0.5f)
            distanceAdjust = toPlayer * 0.5f;
        else if (dist < idealDist - 0.5f)
            distanceAdjust = -toPlayer * 0.5f;

        Vector2 moveDir = (perpendicular + distanceAdjust).normalized;
        agent.speed = circleSpeed;

        Vector3 targetPos = transform.position + (Vector3)moveDir * 2f;
        agent.SetDestination(targetPos);
        animator.SetFloat("WalkSpeed", circleSpeed);
    }

    private void HandleCharging(float dist)
    {
        // Handled by coroutine
    }

    private void HandleRetreating(float dist)
    {
        if (dist > personalSpace)
        {
            SetState(AIState.Circling);
            return;
        }

        Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
        Vector3 retreatTarget = transform.position + (Vector3)awayFromPlayer * 3f;
        agent.speed = retreatSpeed;
        agent.SetDestination(retreatTarget);
        animator.SetFloat("WalkSpeed", retreatSpeed);
    }

    // ──────────── Coroutines ────────────

    private IEnumerator AttackRoutine()
    {
        attackBlocked = true;
        agent.ResetPath();
        animator.SetFloat("WalkSpeed", 0);
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.3f); // Wind-up

        // Deal damage
        if (attackPoint != null)
        {
            Collider2D hit = Physics2D.OverlapCircle(attackPoint.position, attackHitRange, playerLayer);
            if (hit != null)
            {
                HealthController hc = hit.GetComponent<HealthController>();
                if (hc != null) hc.TakeDamage(attackDamage);
            }
        }

        yield return new WaitForSeconds(0.3f);

        // Retreat after attacking
        SetState(AIState.Retreating);

        yield return new WaitForSeconds(attackCooldown);
        attackBlocked = false;
    }

    private IEnumerator ChargeRoutine()
    {
        agent.speed = chargeSpeed;
        agent.SetDestination(target.transform.position);
        animator.SetFloat("WalkSpeed", chargeSpeed * 2);

        float timer = chargeDuration;
        while (timer > 0f)
        {
            agent.SetDestination(target.transform.position);
            timer -= Time.deltaTime;

            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist <= attackRange)
            {
                SetState(AIState.Attacking);
                StartCoroutine(AttackRoutine());
                yield break;
            }

            yield return null;
        }

        SetState(AIState.Circling);
    }

    private bool TryDodge()
    {
        if (dodgeCooldownTimer > 0) return false;
        if (Random.value > dodgeChance) return false;

        // Check if player is in attack animation
        PlayerMovement pm = target.GetComponent<PlayerMovement>();
        if (pm == null) return false;

        Animator playerAnim = target.GetComponent<Animator>();
        if (playerAnim == null) return false;

        AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
        bool playerAttacking = stateInfo.IsName("Attack") || stateInfo.IsTag("Attack");
        if (!playerAttacking) return false;

        dodgeCooldownTimer = dodgeCooldown;
        StartCoroutine(DodgeRoutine());
        return true;
    }

    private IEnumerator DodgeRoutine()
    {
        SetState(AIState.Dodging);

        // Dodge perpendicular to player direction
        Vector2 toPlayer = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        Vector2 dodgeDir = new Vector2(-toPlayer.y, toPlayer.x);
        if (Random.value > 0.5f) dodgeDir = -dodgeDir;

        float timer = dodgeDuration;
        agent.ResetPath();

        while (timer > 0f)
        {
            transform.position += (Vector3)dodgeDir * (dodgeSpeed * Time.deltaTime);
            timer -= Time.deltaTime;
            yield return null;
        }

        SetState(AIState.Circling);
    }

    // ──────────── Combat ────────────

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        animator.SetTrigger("Hurt");

        if (healthBar != null)
            healthBar.UpdateBar((float)currentHealth / scaledMaxHealth);

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        manager?.EnemyDefeated();

        ItemDropper dropper = GetComponent<ItemDropper>();
        if (dropper != null)
            dropper.TryDrop(transform.position);

        animator.SetTrigger("isDead");
        GetComponent<Collider2D>().enabled = false;
        if (agent != null) agent.enabled = false;
        
        Destroy(gameObject, 1.5f); // Thực sự xóa quái khỏi màn hình
        enabled = false;
    }

    // Called by Animation Event on 'attack' animation
    public void HurtPlayer()
    {
        if (target == null) return;
        
        float dist = Vector2.Distance(transform.position, target.transform.position);
        if (dist <= attackRange)
        {
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }
    }

    // ──────────── Helpers ────────────

    private void SetState(AIState newState)
    {
        currentState = newState;
    }

    private void FaceTarget()
    {
        if (target == null) return;

        if (target.transform.position.x < transform.position.x)
            transform.localScale = new Vector2(-Mathf.Abs(transform.localScale.x), transform.localScale.y);
        else if (target.transform.position.x > transform.position.x)
            transform.localScale = new Vector2(Mathf.Abs(transform.localScale.x), transform.localScale.y);
    }

    public void PlaySFX(AudioClip clip)
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
            audio.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, personalSpace);

        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackHitRange);
        }
    }
}
