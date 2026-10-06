using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player movement, attack, power-up (charged) attack and Dash.
/// All input is routed through Unity Input System — no legacy Input API.
/// Reads runtime stats from a <see cref="PlayerStats"/> ScriptableObject
/// so that item upgrades take effect immediately.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    // ────────────────────────── Inspector Fields ──────────────────────────
    [Header("Stats (ScriptableObject)")]
    [Tooltip("Drag the PlayerStats asset here. Bonuses accumulate at runtime.")]
    public PlayerStats playerStats;

    [Header("Attack Timing")]
    [Tooltip("Matches the 0.58 second attack animation and prevents the swing from restarting mid-animation.")]
    [Min(0.05f)] public float attackDuration = 0.60f;
    [Tooltip("A click made just before the swing ends starts the next swing immediately after it.")]
    [Min(0f)] public float attackInputBuffer = 0.12f;

    [Header("Attack References")]
    public Transform attackPoint;
    public float attackRangeScale;
    public float powerUpAttackRadiusRate = 1f;
    public LayerMask enemyLayers;
    public AudioSource audioSource;

    // ────────────────────────── Private State ─────────────────────────────
    private Rigidbody2D rb;
    private Animator animator;
    private PowerupCircleController powerupCircleController;
    private PowerupController powerupController;

    // Movement
    private Vector2 _movement;
    private Vector2 facingLeft;
    private bool isFacingLeft;

    // Attack
    private bool isAttacking;
    private float attackEndsAt;
    private float bufferedAttackExpiresAt = float.NegativeInfinity;

    // Power-up (charged) attack
    private bool powerUpHeld;
    private float powerUpHeldTime;
    private float maxPowerupRadius = 35.0f;
    private bool isPoweredUp;
    private float powerUpCooldownRemaining;

    // Dash
    private bool isDashing;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private Vector2 dashDirection;

    // Stamina
    private float currentStamina;

    // Animator safety
    private bool hasDashAnimParam;

    // Input System
    public PlayerControls playerControl;
    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction dashAction;
    private InputAction powerUpAction;

    // ────────────────────────── Lifecycle ─────────────────────────────────
    private void Awake()
    {
        EnsurePlayerControls();
        audioSource = GetComponent<AudioSource>();
        powerupController = GetComponent<PowerupController>();
    }

    private void OnEnable()
    {
        EnsurePlayerControls();

        moveAction = playerControl.Player.Move;
        attackAction = playerControl.Player.Fire;
        dashAction = playerControl.Player.Dash;
        powerUpAction = playerControl.Player.PowerUp;

        moveAction.Enable();
        attackAction.Enable();
        dashAction.Enable();
        powerUpAction.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        attackAction?.Disable();
        dashAction?.Disable();
        powerUpAction?.Disable();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Check if Animator has a "Dash" parameter to avoid warning spam
        hasDashAnimParam = false;
        foreach (var param in animator.parameters)
        {
            if (param.name == "Dash" && param.type == AnimatorControllerParameterType.Trigger)
            {
                hasDashAnimParam = true;
                break;
            }
        }

        GameObject powerupObj = GameObject.FindGameObjectWithTag("Powerup Attack");
        if (powerupObj != null)
            powerupCircleController = powerupObj.GetComponent<PowerupCircleController>();

        facingLeft = new Vector2(-transform.localScale.x, transform.localScale.y);
        isPoweredUp = false;
        powerUpHeld = false;

        if (playerStats != null)
            currentStamina = playerStats.MaxStamina;
    }

    // ────────────────────────── Update ────────────────────────────────────
    private void Update()
    {
        // --- Power-up cooldown ---
        if (isPoweredUp)
        {
            float cd = playerStats != null ? playerStats.PowerUpCooldown : 15f;
            powerUpCooldownRemaining -= Time.deltaTime;
            if (powerUpCooldownRemaining <= 0)
            {
                isPoweredUp = false;
                powerupController?.ShowPoweredUp();
            }
            else
            {
                powerupController?.UpdateCooldown(powerUpCooldownRemaining);
            }
        }

        // --- Dash cooldown ---
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

        // --- Stamina regen ---
        if (playerStats != null && !isDashing)
        {
            currentStamina = Mathf.Min(
                currentStamina + playerStats.StaminaRegenRate * Time.deltaTime,
                playerStats.MaxStamina);
        }

        // --- Read movement input ---
        _movement = moveAction.ReadValue<Vector2>();
        HandleFacing();

        // --- Handle Dash input (via Input System) ---
        if (dashAction.WasPressedThisFrame())
            TryDash();

        // --- Handle Attack input ---
        HandleAttackInput();

        // --- Handle Power-up hold / release (via Input System) ---
        HandlePowerUpInput();
    }

    // ────────────────────────── Facing / Animation ────────────────────────
    private void HandleFacing()
    {
        if (isDashing) return;

        if (_movement == Vector2.zero)
        {
            animator.SetFloat("Speed", 0);
        }
        else if (_movement.x > 0 && isFacingLeft)
        {
            isFacingLeft = false;
            Flip();
            if (!isAttacking) animator.SetFloat("Speed", 5);
        }
        else if (_movement.x < 0 && !isFacingLeft)
        {
            isFacingLeft = true;
            Flip();
            if (!isAttacking) animator.SetFloat("Speed", 5);
        }
        else
        {
            if (!isAttacking) animator.SetFloat("Speed", 5);
        }
    }

    private void Flip()
    {
        if (isFacingLeft)
            transform.localScale = facingLeft;
        else
            transform.localScale = new Vector2(-transform.localScale.x, transform.localScale.y);
    }

    // ────────────────────────── Dash ──────────────────────────────────────
    private void TryDash()
    {
        if (isDashing) return;
        if (isAttacking) return;

        float cd = playerStats != null ? playerStats.DashCooldown : 1.5f;
        float cost = playerStats != null ? playerStats.DashStaminaCost : 25f;

        if (dashCooldownRemaining > 0f) return;
        if (currentStamina < cost) return;

        // Direction: movement input or current facing
        dashDirection = _movement.normalized;
        if (dashDirection == Vector2.zero)
            dashDirection = isFacingLeft ? Vector2.left : Vector2.right;

        currentStamina -= cost;
        isDashing = true;
        dashTimeRemaining = playerStats != null ? playerStats.DashDuration : 0.15f;
        dashCooldownRemaining = cd;

        if (hasDashAnimParam)
            animator.SetTrigger("Dash");
    }

    // ────────────────────────── Attack ────────────────────────────────────
    private void HandleAttackInput()
    {
        if (isDashing) return;

        if (attackAction.WasPressedThisFrame())
        {
            if (isAttacking)
                bufferedAttackExpiresAt = Time.time + attackInputBuffer;
            else
                StartAttack();
        }

        if (!isAttacking || Time.time < attackEndsAt)
            return;

        isAttacking = false;
        if (Time.time <= bufferedAttackExpiresAt)
        {
            bufferedAttackExpiresAt = float.NegativeInfinity;
            StartAttack();
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackEndsAt = Time.time + attackDuration;
        animator.SetTrigger("Attack");

        float range = playerStats != null ? playerStats.AttackRange : 0.5f;
        int damage = playerStats != null ? playerStats.AttackDamage : 1;

        // Chỉ đánh 1 con gần nhất – tránh đánh trúng nhiều con cùng lúc
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, range, enemyLayers);
        Collider2D closest = null;
        float closestDist = float.MaxValue;
        foreach (Collider2D col in hitEnemies)
        {
            float d = Vector2.Distance(attackPoint.position, col.transform.position);
            if (d < closestDist) { closestDist = d; closest = col; }
        }
        if (closest != null)
        {
            Enemy e = closest.GetComponent<Enemy>();
            if (e != null) e.TakeDamage(damage);

            SmartEnemy se = closest.GetComponent<SmartEnemy>();
            if (se != null) se.TakeDamage(damage);

            RangedEnemy re = closest.GetComponent<RangedEnemy>();
            if (re != null) re.TakeDamage(damage);
        }
    }

    // ────────────────────────── Power-up (charged) attack ─────────────────
    private void HandlePowerUpInput()
    {
        if (isDashing) return;

        if (powerUpAction.IsPressed())
        {
            if (!isPoweredUp)
            {
                if (GetRadius(powerUpHeldTime) <= maxPowerupRadius)
                    powerUpHeldTime += Time.deltaTime;

                powerUpHeld = true;
                powerupCircleController?.setRadius(GetRadius(powerUpHeldTime));
            }
        }
        else if (powerUpHeld)
        {
            PowerUpAttack();
            powerupController?.HidePoweredUp();
        }
    }

    private void PowerUpAttack()
    {
        float radius = GetRadius(powerUpHeldTime);
        animator.SetTrigger("Powerup Attack");

        int damage = playerStats != null ? playerStats.PowerUpDamage : 3;

        if (powerupCircleController != null)
        {
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
                powerupCircleController.transform.position, radius * attackRangeScale, enemyLayers);

            foreach (Collider2D enemy in hitEnemies)
            {
                Enemy e = enemy.GetComponent<Enemy>();
                if (e != null) e.TakeDamage(damage);

                SmartEnemy se = enemy.GetComponent<SmartEnemy>();
                if (se != null) se.TakeDamage(damage);

                RangedEnemy re = enemy.GetComponent<RangedEnemy>();
                if (re != null) re.TakeDamage(damage);
            }
        }

        float cd = playerStats != null ? playerStats.PowerUpCooldown : 15f;
        float range = playerStats != null ? playerStats.AttackRange : 0.5f;

        powerUpCooldownRemaining = cd;
        powerupCircleController?.setRadius(range);
        powerUpHeld = false;
        isPoweredUp = true;
        powerUpHeldTime = 0f;
    }

    private float GetRadius(float timeHeld)
    {
        float range = playerStats != null ? playerStats.AttackRange : 0.5f;
        return range / attackRangeScale + (timeHeld * powerUpAttackRadiusRate);
    }

    // ────────────────────────── FixedUpdate (Physics) ─────────────────────
    private void FixedUpdate()
    {
        if (isDashing)
        {
            float dashSpeed = playerStats != null ? playerStats.DashSpeed : 18f;
            rb.MovePosition(rb.position + dashDirection * (dashSpeed * Time.fixedDeltaTime));
            dashTimeRemaining -= Time.fixedDeltaTime;

            if (dashTimeRemaining <= 0f)
                isDashing = false;

            return;
        }

        if (isAttacking)
            return;

        float speed = playerStats != null ? playerStats.Speed : 5f;
        rb.MovePosition(rb.position + _movement * (speed * Time.fixedDeltaTime));
    }

    // ────────────────────────── Gizmos ────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        float range = playerStats != null ? playerStats.AttackRange : 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, range);

        if (powerUpHeld && powerupCircleController != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(powerupCircleController.transform.position,
                GetRadius(powerUpHeldTime) * attackRangeScale);
        }
    }

    // ────────────────────────── Helpers / Public API ──────────────────────
    public void PlaySFX(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.PlayOneShot(clip);
    }

    /// <summary>Current stamina for UI display (0 → MaxStamina).</summary>
    public float CurrentStamina => currentStamina;

    /// <summary>Is the dash currently in progress?</summary>
    public bool IsDashing => isDashing;

    /// <summary>Remaining dash cooldown for UI (seconds).</summary>
    public float DashCooldownRemaining => dashCooldownRemaining;

    private void EnsurePlayerControls()
    {
        playerControl ??= new PlayerControls();
    }
}
