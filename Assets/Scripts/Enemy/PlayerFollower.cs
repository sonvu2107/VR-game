// AI Skeleton Warrior cũ. NavMeshAgent chịu trách nhiệm di chuyển; class này
// vẫn phục vụ prefab Warrior trong các level đầu.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class Enemy : MonoBehaviour
{
    public float maxDistance;
    public float minDistance;
    public float chaseSpeed;
    public float wanderSpeed;
    public float attackCooldown = 5f;
    public int maxHealth;
    public Transform attackPoint;
    public float attackRange;
    public LayerMask playerLayer;
    public int attackDamage;
    [SerializeField] private bool countsForLevelClear = true;
    public AudioSource audioSource;
    public GameManager manager;
    
    private GameObject target;
    private NavMeshAgent agent;
    private Animator animator;
    private HealthController playerHealthController;
    
    private float changeDirectionCooldown = 2f;
    private float angleChange;
    private float distance;
    private float wanderSpeedActual;
    private bool attackBlocked;
    private bool isDead;
    private int currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
        manager = managerObject != null ? managerObject.GetComponent<GameManager>() : null;
        
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = chaseSpeed;
        wanderSpeedActual = wanderSpeed;
        currentHealth = maxHealth;

        target = GameObject.Find("Player");
        playerHealthController = target != null ? target.GetComponent<HealthController>() : null;
    }

    void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || target == null ||
            playerHealthController == null || playerHealthController.currentHealth <= 0)
        {
            this.enabled = false;
            return;
        }
        
        GetRandomDirectionChange();
        distance = Vector2.Distance(transform.position, target.transform.position);

        if (distance < maxDistance && distance > minDistance)
        {
            animator.SetFloat("WalkSpeed", 101);
            agent.speed = chaseSpeed;
            agent.SetDestination(target.transform.position);
        }
        else if (distance > maxDistance)
        {
            animator.SetFloat("WalkSpeed", wanderSpeedActual);
            agent.speed = wanderSpeedActual;
            Vector3 toMove = new Vector3(Mathf.Cos(angleChange * Mathf.Deg2Rad),
                Mathf.Sin(angleChange * Mathf.Deg2Rad), 0f);
            // Agent, khong phai Transform, di chuyen de tranh lech NavMesh/xuyen tuong.
            if ((!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance) &&
                NavMesh.SamplePosition(transform.position + toMove * 3f,
                    out NavMeshHit wanderPoint, 2f, NavMesh.AllAreas))
                agent.SetDestination(wanderPoint.position);
        }
        else
        {
            animator.SetFloat("WalkSpeed", 0);
            agent.ResetPath();
            AttackPlayer();
        }
        
        if (target.transform.position.x < transform.position.x)
            transform.localScale = new Vector2(-Mathf.Abs(transform.localScale.x), transform.localScale.y);
        else if (target.transform.position.x > transform.position.x)
            transform.localScale = new Vector2(Mathf.Abs(transform.localScale.x), transform.localScale.y);
        
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
        StartCoroutine(DelayAttack());
    }

    private void HurtPlayer()
    {
        Collider2D hitPlayer = Physics2D.OverlapCircle(
            attackPoint != null ? attackPoint.position : transform.position, attackRange, playerLayer);
        if (hitPlayer != null)
        {
            playerHealthController.TakeDamage(attackDamage);
            CombatHitVfx.Spawn(hitPlayer.bounds.center, CombatImpactKind.EnemyMelee, 0.8f);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        animator.SetTrigger("Hurt");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void SetCountsForLevelClear(bool value)
    {
        countsForLevelClear = value;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        if (countsForLevelClear)
            manager?.EnemyDefeated();
        
        if (animator != null) animator.SetBool("isDead", true);
        Collider2D bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (agent != null && agent.enabled) agent.enabled = false;

        this.enabled = false;
    }
    
    private IEnumerator DelayAttack()
    {
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
