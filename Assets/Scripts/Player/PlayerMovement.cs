using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private const int HeavyStrikeFrameCount = 12;
    private Rigidbody2D rb;
    private PowerupCircleController powerupCircleController;
    private Animator animator;
    
    public float speed = 5f;
    [Header("Attack Movement")]
    [Range(0f, 1f)] public float attackMoveSpeedMultiplier = 0.65f;
    [Header("Dash")]
    [Tooltip("Press Left Shift or the gamepad right shoulder button to dash.")]
    [Min(0.05f)] public float dashDuration = 0.16f;
    [Min(0.1f)] public float dashDistance = 3f;
    [Min(0f)] public float dashCooldown = 0.7f;
    [Min(0.05f)] public float dashVfxDuration = 0.22f;
    [Min(0f)] public float dashVfxScale = 1.7f;
    [Header("Heavy Strike (Hold Fire)")]
    [Tooltip("Release before this time for a normal combo hit. Hold longer to charge Heavy Strike.")]
    [Min(0.05f)] public float heavyChargeThreshold = 0.35f;
    [Min(0.1f)] public float heavyMaxChargeTime = 1.05f;
    [Range(0f, 1f)] public float heavyChargeMoveSpeedMultiplier = 0.2f;
    [Min(0.05f)] public float heavyStrikeWindup = 0.18f;
    [Min(0.05f)] public float heavyStrikeRecovery = 0.32f;
    [Min(0.1f)] public float heavyMinRange = 1.35f;
    [Min(0.1f)] public float heavyMaxRange = 1.75f;
    [Range(5f, 90f)] public float heavyArcHalfAngle = 50f;
    [Min(0f)] public float heavyMinDamageMultiplier = 2.4f;
    [Min(0f)] public float heavyMaxDamageMultiplier = 3.2f;
    [Header("Player Visual Invariant")]
    [Tooltip("Writes the Player-instance and authoritative-visual counts at each skill-cast boundary.")]
    [SerializeField] private bool logPlayerVisualInvariant = true;
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
    [Header("Slash VFX")]
    [Min(0.05f)] public float slashVfxDuration = 0.36f;
    [Min(0f)] public float hit1VfxScale = 0.8f;
    [Min(0f)] public float hit2VfxScale = 1f;
    [Min(0f)] public float hit3VfxScale = 0.85f;
    [Header("Combo Audio")]
    [Range(0f, 1f)] public float hit1SwingVolume = 0.75f;
    [Range(0f, 1f)] public float hit2SwingVolume = 0.9f;
    [Range(0f, 1f)] public float hit3SwingVolume = 1f;
    [Range(0.5f, 2f)] public float hit1SwingPitch = 1.05f;
    [Range(0.5f, 2f)] public float hit2SwingPitch = 1.18f;
    [Range(0.5f, 2f)] public float hit3SwingPitch = 0.82f;
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
    private AudioSource swingAudioSource;
    private AudioSource impactAudioSource;
    private AudioClip[] comboSwingClips;
    private AudioClip swordImpactClang;

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
    private readonly HashSet<BossController> hitBosses = new HashSet<BossController>();
    private SpriteRenderer slashVfxRenderer;
    private Sprite[][] slashVfxFrames;
    private float slashVfxShownAt;
    private float slashVfxHideAt;
    private float slashVfxBaseScale;
    private int activeVfxHit;
    private SpriteRenderer playerSpriteRenderer;
    private SpriteRenderer dashVfxRenderer;
    private Sprite[] dashVfxFrames;
    private Vector2 dashDirection;
    private bool isDashing;
    private bool skillPositionLocked;
    private Vector2 skillLockedPosition;
    private float dashEndsAt;
    private float nextDashAt;
    private float dashVfxShownAt;
    private float dashVfxHideAt;
    private Sprite[] heavyStrikeFrames;
    private bool heavyManualSpriteActive;
    private bool heavyInputPending;
    private bool isHeavyCharging;
    private bool isHeavyAttacking;
    private bool heavyPositionLocked;
    private float heavyInputStartedAt;
    private Vector2 heavyDirection;
    private Vector2 heavyLockedPosition;
    private bool spaceHeld;
    private float spaceHeldTime = 0.0f;
    private float maxPowerupRadius = 35.0f;
    private bool isPoweredUp = true;

    public PlayerControls playerControl;
    private InputAction move, attack;

    private GameObject player;
    private PowerupController powerupController;
    private PlayerSkillController skillController;

    public bool IsAttacking => isAttacking;
    public bool IsDashing => isDashing;
    public bool IsHeavyCharging => isHeavyCharging;
    public bool IsHeavyAttacking => isHeavyAttacking;
    public bool IsHeavyLocked => isHeavyCharging || isHeavyAttacking;
    public bool IsHeavyInputPending => heavyInputPending;
    public Vector2 FacingDirection => isFacingLeft ? Vector2.left : Vector2.right;
    // The Player object's transform is the movement/collider anchor, not
    // necessarily the visual centre of every animation frame.
    public Vector3 VisualCenter => playerSpriteRenderer != null
        ? playerSpriteRenderer.bounds.center
        : transform.position;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashAt - Time.time);
    public float DashCooldownNormalized => dashCooldown <= 0f ? 0f : DashCooldownRemaining / dashCooldown;

    private void Awake()
    {
        playerControl = new PlayerControls();
        audioSource = GetComponent<AudioSource>();
        swingAudioSource = gameObject.AddComponent<AudioSource>();
        swingAudioSource.playOnAwake = false;
        swingAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 0f;
        impactAudioSource = gameObject.AddComponent<AudioSource>();
        impactAudioSource.playOnAwake = false;
        impactAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 0f;
        comboSwingClips = new[]
        {
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing1"),
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing2"),
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing3")
        };
        swordImpactClang = Resources.Load<AudioClip>("Audio/Combat/SwordImpactClang");
        player = GameObject.Find("Player");
        powerupController = player.GetComponent<PowerupController>();
    }

    private void OnEnable()
    {
        if (playerControl == null)
            playerControl = new PlayerControls();

        move = playerControl.Player.Move;
        attack = playerControl.Player.Fire;
        
        move.Enable();
        attack.Enable();
    }

    private void OnDisable()
    {
        move?.Disable();
        attack?.Disable();
        heavyInputPending = false;
        isHeavyCharging = false;
        isHeavyAttacking = false;
        RestoreAnimatorAfterHeavyStrike();
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = Color.white;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        powerupCircleController = GameObject.FindGameObjectWithTag("Powerup Attack").GetComponent<PowerupCircleController>();
        facingLeft = new Vector2(-transform.localScale.x, transform.localScale.y);
        spaceHeld = false;
        isPoweredUp = false;
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
        CreateSlashVfx();
        CreateDashVfx();
        LoadHeavyStrikeFrames();
        skillController = GetComponent<PlayerSkillController>();
        if (skillController == null)
            skillController = gameObject.AddComponent<PlayerSkillController>();
        AssertSingleAuthoritativePlayerVisual("startup");
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
        UpdateSlashVfx();
        UpdateDashVfx();
        UpdateHeavyChargeVisual();

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

        if (isHeavyAttacking)
        {
            _movement = Vector2.zero;
            return;
        }

        // Skills have a fixed casting position.  Previously only attack/dash
        // input was blocked; FixedUpdate still applied the held movement axis,
        // so the player (and E's attached VFX) slid sideways during the cast.
        if (skillController != null && skillController.IsSkillCasting)
        {
            _movement = Vector2.zero;
            return;
        }

        if (_movement == Vector2.zero)
        {
            if (!isAttacking && !isDashing && !IsHeavyLocked && (skillController == null || !skillController.IsSkillCasting))
                animator.SetFloat("Speed", 0);
        }
        else if (_movement.x > 0 && isFacingLeft)
        {
            isFacingLeft = false;
            Flip();
            if(!isAttacking && !isDashing && !IsHeavyLocked && (skillController == null || !skillController.IsSkillCasting))
                animator.SetFloat("Speed", 5);
        }
        else if (_movement.x < 0 && !isFacingLeft)
        {
            isFacingLeft = true;
            Flip();
            if(!isAttacking && !isDashing && !IsHeavyLocked && (skillController == null || !skillController.IsSkillCasting))
                animator.SetFloat("Speed", 5);
        }
        else
        {
            if(!isAttacking && !isDashing && !IsHeavyLocked && (skillController == null || !skillController.IsSkillCasting))
                animator.SetFloat("Speed", 5);
        }

        HandleDashInput();
        HandleAttackInput();

        if (!IsHeavyLocked && Input.GetKey(KeyCode.Space))
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
        else if(!IsHeavyLocked && spaceHeld)
        {
            PowerUpAttack();
            powerupController.HidePoweredUp();
        }
    }

    private void HandleAttackInput()
    {
        if (isDashing || isHeavyAttacking || (skillController != null && skillController.IsSkillCasting))
            return;

        if (heavyInputPending)
        {
            if (!attack.IsPressed())
            {
                float heldTime = Time.time - heavyInputStartedAt;
                heavyInputPending = false;
                if (heldTime >= heavyChargeThreshold)
                    StartCoroutine(PerformHeavyStrike(HeavyChargeNormalized));
                else
                    StartOrQueueComboAttack();
            }
            else if (!isHeavyCharging && Time.time - heavyInputStartedAt >= heavyChargeThreshold)
            {
                BeginHeavyCharge();
            }

            return;
        }

        if (attack.WasPressedThisFrame())
        {
            if (isAttacking)
            {
                // One queued input is enough: repeated clicks do not restart the current swing.
                attackQueued = true;
            }
            else
            {
                // The basic strike is confirmed on release, letting the same Fire
                // binding distinguish a quick combo click from a charged heavy hit.
                heavyInputPending = true;
                heavyInputStartedAt = Time.time;
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

    private void StartOrQueueComboAttack()
    {
        if (isAttacking)
        {
            attackQueued = true;
            return;
        }

        if (Time.time > comboExpiresAt)
            nextComboHit = 0;

        StartAttack(nextComboHit);
    }

    private void HandleDashInput()
    {
        if (isDashing)
        {
            if (Time.time >= dashEndsAt)
            {
                isDashing = false;
                animator.SetFloat("Speed", _movement == Vector2.zero ? 0f : 5f);
            }

            return;
        }

        if (isAttacking || IsHeavyLocked || heavyInputPending || (skillController != null && skillController.IsSkillCasting) ||
            Time.time < nextDashAt || !WasDashPressed())
            return;

        dashDirection = _movement.sqrMagnitude > 0.01f
            ? _movement.normalized
            : isFacingLeft ? Vector2.left : Vector2.right;
        isDashing = true;
        dashEndsAt = Time.time + dashDuration;
        nextDashAt = Time.time + dashCooldown;
        // The run clip is the dash body pose.  Previously this was forced to
        // zero, leaving the character visibly idle while moving several tiles.
        animator.SetFloat("Speed", 5f);
        ShowDashVfx();
    }

    private static bool WasDashPressed()
    {
        return (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
    }

    private float HeavyChargeNormalized
    {
        get
        {
            if (heavyMaxChargeTime <= heavyChargeThreshold)
                return 1f;

            return Mathf.Clamp01((Time.time - heavyInputStartedAt - heavyChargeThreshold) /
                (heavyMaxChargeTime - heavyChargeThreshold));
        }
    }

    private void LoadHeavyStrikeFrames()
    {
        Texture2D texture = Resources.Load<Texture2D>("Combat/HeavyStrikeBody_12f");
        if (texture == null)
        {
            Debug.LogWarning("Could not load Heavy Strike body frames: Combat/HeavyStrikeBody_12f");
            return;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        heavyStrikeFrames = new Sprite[HeavyStrikeFrameCount];
        for (int frame = 0; frame < heavyStrikeFrames.Length; frame++)
        {
            int left = Mathf.RoundToInt(frame * texture.width / (float)heavyStrikeFrames.Length);
            int right = Mathf.RoundToInt((frame + 1) * texture.width / (float)heavyStrikeFrames.Length);
            heavyStrikeFrames[frame] = Sprite.Create(texture,
                new Rect(left, 0f, right - left, texture.height), new Vector2(0.5f, 0f), 100f);
            heavyStrikeFrames[frame].name = $"HeavyStrikeBody_{frame:00}";
        }
    }

    private void BeginHeavyCharge()
    {
        isHeavyCharging = true;
        heavyDirection = FacingDirection;
        animator.SetFloat("Speed", 0f);
        SetHeavyStrikeFrame(0);
        AssertSingleAuthoritativePlayerVisual("heavy-charge");
    }

    private void UpdateHeavyChargeVisual()
    {
        if (!isHeavyCharging)
            return;

        int frame = Mathf.Min(6, Mathf.FloorToInt(HeavyChargeNormalized * 7f));
        SetHeavyStrikeFrame(frame);
        float glow = 0.08f + 0.11f * (1f + Mathf.Sin(Time.time * 16f)) * 0.5f;
        playerSpriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.28f), glow);
    }

    private IEnumerator PerformHeavyStrike(float chargeNormalized)
    {
        isHeavyCharging = false;
        isHeavyAttacking = true;
        attackQueued = false;
        nextComboHit = 0;
        heavyDirection = FacingDirection;
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = Color.white;
        // HeavyStrikeBody is sampled onto the existing authoritative Player
        // renderer. It is animation data, never an instantiated body/VFX.
        SetHeavyStrikeFrame(7);
        AssertSingleAuthoritativePlayerVisual("heavy-strike-start");

        yield return new WaitForSeconds(heavyStrikeWindup * 0.34f);
        SetHeavyStrikeFrame(8);
        AssertSingleAuthoritativePlayerVisual("heavy-strike-windup-1");
        yield return new WaitForSeconds(heavyStrikeWindup * 0.33f);
        SetHeavyStrikeFrame(9);
        AssertSingleAuthoritativePlayerVisual("heavy-strike-windup-2");
        yield return new WaitForSeconds(heavyStrikeWindup * 0.33f);

        SetHeavyStrikeFrame(10);
        AssertSingleAuthoritativePlayerVisual("heavy-strike-impact");
        float range = Mathf.Lerp(heavyMinRange, heavyMaxRange, chargeNormalized);
        int damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage *
            Mathf.Lerp(heavyMinDamageMultiplier, heavyMaxDamageMultiplier, chargeNormalized)));
        DamageEnemiesInArc(transform.position, heavyDirection, range, heavyArcHalfAngle, damage, true);

        yield return new WaitForSeconds(heavyStrikeRecovery * 0.55f);
        SetHeavyStrikeFrame(11);
        yield return new WaitForSeconds(heavyStrikeRecovery * 0.45f);

        isHeavyAttacking = false;
        heavyPositionLocked = false;
        RestoreAnimatorAfterHeavyStrike();
        animator.SetFloat("Speed", _movement == Vector2.zero ? 0f : 5f);
        AssertSingleAuthoritativePlayerVisual("heavy-strike-complete");
    }

    private void SetHeavyStrikeFrame(int frame)
    {
        if (playerSpriteRenderer == null || heavyStrikeFrames == null ||
            frame < 0 || frame >= heavyStrikeFrames.Length)
            return;

        if (!heavyManualSpriteActive)
        {
            // Disable the Animator before the first manual frame. This leaves
            // exactly one renderer writing the body sprite during the strike.
            animator.enabled = false;
            heavyManualSpriteActive = true;
        }

        playerSpriteRenderer.sprite = heavyStrikeFrames[frame];
    }

    private void RestoreAnimatorAfterHeavyStrike()
    {
        if (!heavyManualSpriteActive || animator == null)
            return;

        heavyManualSpriteActive = false;
        animator.enabled = true;
        animator.Play(_movement == Vector2.zero ? "idle" : "run", 0, 0f);
    }

    /// <summary>
    /// Makes an ability use the existing player action clips rather than
    /// casting from idle.  Q uses the first sword pose, E uses the second,
    /// and R uses the dedicated power-up pose.
    /// </summary>
    public void PlaySkillBodyMotion(int comboIndex, bool usePowerupPose = false)
    {
        if (animator == null)
            return;

        animator.SetFloat("Speed", 0f);
        if (usePowerupPose)
        {
            animator.SetTrigger("Powerup Attack");
            return;
        }

        animator.SetInteger("ComboIndex", Mathf.Clamp(comboIndex, 1, 3));
        animator.SetTrigger("Attack");
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
        ShowSlashVfx(activeComboHit);
    }

    private void CreateSlashVfx()
    {
        slashVfxFrames = new Sprite[3][];
        for (int hit = 0; hit < slashVfxFrames.Length; hit++)
        {
            Sprite[] frames = Resources.LoadAll<Sprite>($"Combat/SlashVfxHit{hit + 1}_6f");
            System.Array.Sort(frames, (first, second) => string.CompareOrdinal(first.name, second.name));
            if (frames.Length < 6)
            {
                slashVfxFrames = null;
                return;
            }

            slashVfxFrames[hit] = frames;
        }

        GameObject slashVfx = new GameObject("Slash VFX");
        slashVfx.transform.SetParent(transform, false);
        slashVfxRenderer = slashVfx.AddComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
        {
            slashVfxRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            slashVfxRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
        }

        slashVfxRenderer.enabled = false;
    }

    private void ShowSlashVfx(int comboHit)
    {
        if (slashVfxRenderer == null || slashVfxFrames == null || attackPoint == null)
            return;

        activeVfxHit = comboHit;
        slashVfxBaseScale = comboHit == 0 ? hit1VfxScale : comboHit == 1 ? hit2VfxScale : hit3VfxScale;
        slashVfxRenderer.sprite = slashVfxFrames[comboHit][0];
        // Every source sheet shares the same right-facing orientation. The parent
        // mirrors the complete effect together with the Player when facing left.
        slashVfxRenderer.flipX = false;
        slashVfxRenderer.flipY = false;
        slashVfxRenderer.color = Color.white;
        slashVfxRenderer.transform.localPosition = transform.InverseTransformPoint(attackPoint.position);
        slashVfxRenderer.transform.localRotation = Quaternion.identity;
        slashVfxRenderer.transform.localScale = Vector3.one * slashVfxBaseScale;
        slashVfxRenderer.enabled = true;
        slashVfxShownAt = Time.time;
        slashVfxHideAt = Time.time + slashVfxDuration;
    }

    private void UpdateSlashVfx()
    {
        if (slashVfxRenderer == null || !slashVfxRenderer.enabled)
            return;

        if (Time.time >= slashVfxHideAt)
        {
            slashVfxRenderer.enabled = false;
            return;
        }

        float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(slashVfxShownAt, slashVfxHideAt, Time.time));
        int frameIndex = Mathf.Min((int)(progress * slashVfxFrames[activeVfxHit].Length), slashVfxFrames[activeVfxHit].Length - 1);
        slashVfxRenderer.sprite = slashVfxFrames[activeVfxHit][frameIndex];
        slashVfxRenderer.transform.localScale = Vector3.one * Mathf.Lerp(slashVfxBaseScale * 0.95f, slashVfxBaseScale, progress);
        Color color = slashVfxRenderer.color;
        color.a = 1f - progress * progress;
        slashVfxRenderer.color = color;
    }

    private void CreateDashVfx()
    {
        Texture2D texture = Resources.Load<Texture2D>("Combat/DashBurstPixel_6f");
        if (texture == null)
        {
            Debug.LogWarning("Could not load dash VFX: Combat/DashBurstPixel_6f");
            return;
        }

        dashVfxFrames = PlayerSkillController.LoadPixelFrames("Combat/DashBurstPixel_6f", 6);

        GameObject dashVfx = new GameObject("Dash Burst VFX");
        dashVfxRenderer = dashVfx.AddComponent<SpriteRenderer>();
        Material vfxMaterial = PlayerSkillController.GetVfxMaterial();
        if (vfxMaterial != null)
            dashVfxRenderer.sharedMaterial = vfxMaterial;
        if (playerSpriteRenderer != null)
        {
            dashVfxRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            dashVfxRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
        }

        dashVfxRenderer.enabled = false;
    }

    private void ShowDashVfx()
    {
        if (dashVfxRenderer == null || dashVfxFrames == null || dashVfxFrames.Length == 0)
            return;

        dashVfxRenderer.sprite = dashVfxFrames[0];
        dashVfxRenderer.transform.position = transform.position - (Vector3)dashDirection * 0.2f;
        dashVfxRenderer.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg);
        dashVfxRenderer.transform.localScale = Vector3.one * dashVfxScale;
        dashVfxRenderer.color = Color.white;
        dashVfxRenderer.enabled = true;
        dashVfxShownAt = Time.time;
        dashVfxHideAt = Time.time + dashVfxDuration;
    }

    private void UpdateDashVfx()
    {
        if (dashVfxRenderer == null || !dashVfxRenderer.enabled)
            return;

        if (Time.time >= dashVfxHideAt)
        {
            dashVfxRenderer.enabled = false;
            return;
        }

        float progress = Mathf.InverseLerp(dashVfxShownAt, dashVfxHideAt, Time.time);
        int frameIndex = Mathf.Min((int)(progress * dashVfxFrames.Length), dashVfxFrames.Length - 1);
        dashVfxRenderer.sprite = dashVfxFrames[frameIndex];
        dashVfxRenderer.transform.position = transform.position - (Vector3)dashDirection * 0.2f;
    }

    /// <summary>
    /// Runtime proof that gameplay has one Player object and one authoritative
    /// body renderer. Any renderer copying the current body sprite is treated
    /// as an illegal player-visual clone (VFX must use its own art instead).
    /// </summary>
    public void AssertSingleAuthoritativePlayerVisual(string phase)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int authoritativeVisuals = 0;
        int copiedBodyVisuals = 0;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.GetComponent<PlayerMovement>() != null)
                authoritativeVisuals++;
            else if (playerSpriteRenderer != null && playerSpriteRenderer.sprite != null &&
                     renderer.sprite == playerSpriteRenderer.sprite)
                copiedBodyVisuals++;
        }

        bool valid = players.Length == 1 && authoritativeVisuals == 1 && copiedBodyVisuals == 0;
        string message = $"[PlayerVisual] {phase}: Player instances={players.Length}, " +
            $"authoritative visuals={authoritativeVisuals}, copied body visuals={copiedBodyVisuals}.";
        Debug.Assert(valid, message);
        if (logPlayerVisualInvariant)
            Debug.Log(message);
    }

    private void OnDestroy()
    {
        // This is a reusable VFX renderer, not a player visual. It is kept by
        // reference and explicitly released with its owner.
        if (dashVfxRenderer != null)
            Destroy(dashVfxRenderer.gameObject);
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
            Mathf.Max(1, Mathf.RoundToInt(attackDamage * damageMultiplier)), true);
    }

    private void DamageEnemies(Vector2 center, float radius, int damage, bool playSwordImpact = false)
    {
        DamageEnemiesInRadius(center, radius, damage, null, null, playSwordImpact);
    }

    public int DamageEnemiesInRadius(Vector2 center, float radius, int damage,
        HashSet<Enemy> alreadyHitEnemies = null, HashSet<BossController> alreadyHitBosses = null,
        bool playSwordImpact = false)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, enemyHitBuffer, enemyLayers);
        if (alreadyHitEnemies == null)
        {
            hitEnemies.Clear();
            alreadyHitEnemies = hitEnemies;
        }

        if (alreadyHitBosses == null)
        {
            hitBosses.Clear();
            alreadyHitBosses = hitBosses;
        }

        bool damagedEnemy = false;
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = enemyHitBuffer[i].GetComponentInParent<Enemy>();
            if (enemy != null && alreadyHitEnemies.Add(enemy))
            {
                enemy.TakeDamage(damage);
                damagedEnemy = true;
                damagedCount++;
            }

            BossController boss = enemyHitBuffer[i].GetComponentInParent<BossController>();
            if (boss != null && alreadyHitBosses.Add(boss))
            {
                boss.TakeDamage(damage);
                damagedEnemy = true;
                damagedCount++;
            }
        }

        if (playSwordImpact && damagedEnemy)
            PlaySwordImpact();

        if (damagedCount > 0 && skillController != null)
            skillController.AddUltimateCharge(damagedCount * 8f);

        return damagedCount;
    }

    /// <summary>
    /// Applies one hit to each target inside a forward cone.  Radius attacks
    /// are used by E/R and Power-up; Heavy Strike deliberately uses this arc
    /// so enemies behind the player are never struck.
    /// </summary>
    public int DamageEnemiesInArc(Vector2 origin, Vector2 direction, float range, float halfAngleDegrees,
        int damage, bool playSwordImpact = false)
    {
        if (direction.sqrMagnitude < 0.001f || range <= 0f)
            return 0;

        direction.Normalize();
        float minDot = Mathf.Cos(Mathf.Clamp(halfAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, range, enemyHitBuffer, enemyLayers);
        hitEnemies.Clear();
        hitBosses.Clear();
        bool damagedEnemy = false;
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D targetCollider = enemyHitBuffer[i];
            if (targetCollider == null)
                continue;

            Vector2 targetOffset = (Vector2)targetCollider.bounds.center - origin;
            if (targetOffset.sqrMagnitude > range * range || targetOffset.sqrMagnitude < 0.0001f ||
                Vector2.Dot(direction, targetOffset.normalized) < minDot)
                continue;

            Enemy enemy = targetCollider.GetComponentInParent<Enemy>();
            if (enemy != null && hitEnemies.Add(enemy))
            {
                enemy.TakeDamage(damage);
                damagedEnemy = true;
                damagedCount++;
            }

            BossController boss = targetCollider.GetComponentInParent<BossController>();
            if (boss != null && hitBosses.Add(boss))
            {
                boss.TakeDamage(damage);
                damagedEnemy = true;
                damagedCount++;
            }
        }

        if (playSwordImpact && damagedEnemy)
            PlayHeavyImpact();

        if (damagedCount > 0 && skillController != null)
            skillController.AddUltimateCharge(damagedCount * 8f);

        return damagedCount;
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
        if (isDashing)
        {
            rb.MovePosition(rb.position + dashDirection * (dashDistance / dashDuration) * Time.fixedDeltaTime);
            return;
        }

        if (isHeavyAttacking)
        {
            if (!heavyPositionLocked)
            {
                heavyLockedPosition = rb.position;
                heavyPositionLocked = true;
            }

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MovePosition(heavyLockedPosition);
            return;
        }

        // Stop the physics body as well as the input value.  This covers the
        // Update/FixedUpdate timing gap on the first physics tick of a cast.
        if (skillController != null && skillController.IsSkillCasting)
        {
            if (!skillPositionLocked)
            {
                skillLockedPosition = rb.position;
                skillPositionLocked = true;
            }

            // Rigidbody2D is Dynamic and has no linear drag. Clear any velocity
            // left by the previous MovePosition, then pin it to the cast point
            // so collisions cannot create a final-frame horizontal slip.
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MovePosition(skillLockedPosition);
            return;
        }

        skillPositionLocked = false;

        float movementMultiplier = isHeavyCharging ? heavyChargeMoveSpeedMultiplier :
            isAttacking ? attackMoveSpeedMultiplier : 1f;
        rb.MovePosition(rb.position + _movement * (speed * movementMultiplier * Time.fixedDeltaTime));
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
        if (!isAttacking)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip);
            return;
        }

        AudioClip comboClip = GetComboSwingClip(activeComboHit) ?? clip;
        if (comboClip == null)
            return;

        swingAudioSource.pitch = activeComboHit == 0 ? hit1SwingPitch : activeComboHit == 1 ? hit2SwingPitch : hit3SwingPitch;
        float volume = activeComboHit == 0 ? hit1SwingVolume : activeComboHit == 1 ? hit2SwingVolume : hit3SwingVolume;
        swingAudioSource.PlayOneShot(comboClip, volume);
    }

    private AudioClip GetComboSwingClip(int comboHit)
    {
        if (comboSwingClips == null || comboHit < 0 || comboHit >= comboSwingClips.Length)
            return null;

        return comboSwingClips[comboHit];
    }

    private void PlaySwordImpact()
    {
        if (swordImpactClang == null || impactAudioSource == null)
            return;

        impactAudioSource.pitch = activeComboHit == 2 ? 0.9f : 1.05f;
        float volume = activeComboHit == 2 ? 1f : 0.75f;
        impactAudioSource.PlayOneShot(swordImpactClang, volume);
    }

    private void PlayHeavyImpact()
    {
        if (swordImpactClang == null || impactAudioSource == null)
            return;

        impactAudioSource.pitch = 0.78f;
        impactAudioSource.PlayOneShot(swordImpactClang, 1f);
    }
}
