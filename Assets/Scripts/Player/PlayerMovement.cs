using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PowerupCircleController powerupCircleController;
    private Animator animator;
    
    public float speed = 5f;
    [Header("Three-Hit Combo")]
    [Tooltip("Each value should match its own animation clip once the three attack animations are added.")]
    [Min(0.05f)] public float hit1Duration = 0.60f;
    [Min(0.05f)] public float hit2Duration = 0.42f;
    [Min(0.05f)] public float hit3Duration = 0.42f;
    [Tooltip("How long after a completed swing the next click can continue the combo.")]
    [Min(0f)] public float comboResetDelay = 0.65f;
    [Range(0f, 1f)] public float hitImpactNormalizedTime = 0.42f;
    [Header("Combo Strength")]
    [Min(0f)] public float hit1DamageMultiplier = 1f;
    [Min(0f)] public float hit2DamageMultiplier = 1.15f;
    [Min(0f)] public float hit3DamageMultiplier = 1.6f;
    [Min(0f)] public float hit1RangeMultiplier = 1f;
    [Min(0f)] public float hit2RangeMultiplier = 1.08f;
    [Min(0f)] public float hit3RangeMultiplier = 1.25f;
    public float powerUpAttackCooldown = 15.0f;
    public Transform attackPoint;
    public float attackRange = 0.5f;
    public float attackRangeScale;
    public float powerUpAttackRadiusRate = 1f;
    public LayerMask enemyLayers;
    public int attackDamage;
    public int powerUpDamage;
    private float powerUpAttackCooldownActual;
    public AudioSource audioSource;

    private Vector2 _movement;
    private Vector2 facingLeft;
    private bool isFacingLeft;
    private bool isAttacking;
    private float attackEndsAt;
    private float hitAt;
    private float comboExpiresAt;
    private bool attackQueued;
    private bool hitApplied;
    private int activeComboHit;
    private int nextComboHit;
    private readonly Collider2D[] enemyHitBuffer = new Collider2D[16];
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private bool spaceHeld;
    private float spaceHeldTime = 0.0f;
    private float maxPowerupRadius = 35.0f;
    private bool isPoweredUp = true;

    public PlayerControls playerControl;
    private InputAction move, attack;

    private GameObject player;
    private PowerupController powerupController;

    private void Awake()
    {
        playerControl = new PlayerControls();
        audioSource = GetComponent<AudioSource>();
        player = GameObject.Find("Player");
        powerupController = player.GetComponent<PowerupController>();
    }

    private void OnEnable()
    {
        move = playerControl.Player.Move;
        attack = playerControl.Player.Fire;
        
        move.Enable();
        attack.Enable();
    }

    private void OnDisable()
    {
        move?.Disable();
        attack?.Disable();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        powerupCircleController = GameObject.FindGameObjectWithTag("Powerup Attack").GetComponent<PowerupCircleController>();
        facingLeft = new Vector2(-transform.localScale.x, transform.localScale.y);
        spaceHeld = false;
        isPoweredUp = false;
    }

    private void Flip()
    {
        if (isFacingLeft)
        {
            transform.localScale = facingLeft;
        }
        else
        {
            transform.localScale = new Vector2(-transform.localScale.x, transform.localScale.y);
        }
    }

    private void Update()
    {
        if(isPoweredUp)
        {
            powerUpAttackCooldownActual -= Time.deltaTime;
            if(powerUpAttackCooldownActual <= 0)
            {
                isPoweredUp = false;
                powerupController.ShowPoweredUp();
            } else {
                powerupController.UpdateCooldown(powerUpAttackCooldownActual);
            }
            
        }
        

        _movement = move.ReadValue<Vector2>();

        if (_movement == Vector2.zero)
        {
            animator.SetFloat("Speed", 0);
        }
        else if (_movement.x > 0 && isFacingLeft)
        {
            isFacingLeft = false;
            Flip();
            if(!isAttacking)
                animator.SetFloat("Speed", 5);
        }
        else if (_movement.x < 0 && !isFacingLeft)
        {
            isFacingLeft = true;
            Flip();
            if(!isAttacking)
                animator.SetFloat("Speed", 5);
        }
        else
        {
            if(!isAttacking)
                animator.SetFloat("Speed", 5);
        }

        HandleAttackInput();

        if(Input.GetKey(KeyCode.Space))
        {
            if(!isPoweredUp) 
            {
                if(GetRadius(spaceHeldTime) <= maxPowerupRadius)
                {
                    spaceHeldTime += Time.deltaTime;
                }
                spaceHeld = true;
                powerupCircleController.setRadius(GetRadius(spaceHeldTime));
            }
        }
        else if(spaceHeld)
        {
            PowerUpAttack();
            powerupController.HidePoweredUp();
        }
    }

    private void HandleAttackInput()
    {
        if (attack.WasPressedThisFrame())
        {
            if (isAttacking)
            {
                // One queued input is enough: repeated clicks do not restart the current swing.
                attackQueued = true;
            }
            else
            {
                if (Time.time > comboExpiresAt)
                    nextComboHit = 0;

                StartAttack(nextComboHit);
            }
        }

        if (!isAttacking)
            return;

        if (!hitApplied && Time.time >= hitAt)
        {
            hitApplied = true;
            DealComboDamage();
        }

        if (Time.time < attackEndsAt)
            return;

        isAttacking = false;
        nextComboHit = activeComboHit == 2 ? 0 : activeComboHit + 1;
        comboExpiresAt = Time.time + comboResetDelay;

        if (attackQueued)
        {
            attackQueued = false;
            StartAttack(nextComboHit);
        }
    }

    private void StartAttack(int comboHit)
    {
        activeComboHit = Mathf.Clamp(comboHit, 0, 2);
        isAttacking = true;
        hitApplied = false;
        float duration = GetComboDuration(activeComboHit);
        attackEndsAt = Time.time + duration;
        hitAt = Time.time + duration * hitImpactNormalizedTime;

        // The current controller still uses one clip. ComboIndex is ready for Attack_1/2/3 transitions.
        animator.SetInteger("ComboIndex", activeComboHit + 1);
        animator.SetTrigger("Attack");
    }

    private float GetComboDuration(int comboHit)
    {
        switch (comboHit)
        {
            case 1: return hit2Duration;
            case 2: return hit3Duration;
            default: return hit1Duration;
        }
    }

    private void DealComboDamage()
    {
        float damageMultiplier;
        float rangeMultiplier;
        switch (activeComboHit)
        {
            case 1:
                damageMultiplier = hit2DamageMultiplier;
                rangeMultiplier = hit2RangeMultiplier;
                break;
            case 2:
                damageMultiplier = hit3DamageMultiplier;
                rangeMultiplier = hit3RangeMultiplier;
                break;
            default:
                damageMultiplier = hit1DamageMultiplier;
                rangeMultiplier = hit1RangeMultiplier;
                break;
        }

        DamageEnemies(attackPoint.position, attackRange * rangeMultiplier,
            Mathf.Max(1, Mathf.RoundToInt(attackDamage * damageMultiplier)));
    }

    private void DamageEnemies(Vector2 center, float radius, int damage)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, enemyHitBuffer, enemyLayers);
        hitEnemies.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = enemyHitBuffer[i].GetComponentInParent<Enemy>();
            if (enemy != null && hitEnemies.Add(enemy))
                enemy.TakeDamage(damage);
        }
    }

    void PowerUpAttack()
    {
        float radius = GetRadius(spaceHeldTime);
        animator.SetTrigger("Powerup Attack");
        
        powerUpAttackCooldownActual = powerUpAttackCooldown;
        powerupCircleController.setRadius(attackRange);
        spaceHeld = false;
        isPoweredUp = true;
        spaceHeldTime = 0.0f;
 
        DamageEnemies(powerupCircleController.transform.position, radius * attackRangeScale, powerUpDamage);
    }

    private void FixedUpdate()
    {
        if (isAttacking)
            return;
                
        rb.MovePosition(rb.position + _movement * (speed * Time.fixedDeltaTime));
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        if(spaceHeld)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(powerupCircleController.transform.position, GetRadius(spaceHeldTime)*attackRangeScale);
        }
    }

    private float GetRadius(float timeHeld)
    {
        return attackRange/attackRangeScale + (timeHeld * powerUpAttackRadiusRate);
    }

    public void PlaySFX(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.PlayOneShot(clip);
    }
}
