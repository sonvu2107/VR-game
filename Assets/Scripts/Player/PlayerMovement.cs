using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private const int HeavyStrikeFrameCount = 12;
    // PrefabPlacer maps a logical tile (x,y) to world (x,y+1). The half-cell
    // shift makes each spawn coordinate the centre of its allowed tile region.
    private static readonly Vector2 WalkableCellLookupOffset = new(0.5f, -0.5f);
    // Campaign corridors and room tiles use different visual origins. Probe
    // the valid logical anchors at the dash position; physics still resolves
    // the actual collider against walls after this floor-only safety sweep.
    private static readonly Vector2[] DashCellOffsets =
    {
        WalkableCellLookupOffset, Vector2.zero, new Vector2(0.5f, 0.5f),
        new Vector2(-0.5f, -0.5f), new Vector2(-0.5f, 0.5f)
    };
    // Frames 5 and 6 in the imported HeavyStrikeBody sheet contain detached
    // sword fragments.  Hold the last clean raised-sword pose while charging
    // instead of exposing that broken artwork.
    private static readonly int[] HeavyChargeFrameMap = { 0, 1, 2, 3, 4, 4, 4 };
    private Rigidbody2D rb;
    private Collider2D movementCollider;
    private PhysicsMaterial2D frictionlessMovementMaterial;
    private HashSet<Vector2Int> walkableFloorTiles;
    private PowerupCircleController powerupCircleController;
    private PowerupChargeVfx powerupChargeVfx;
    private Animator animator;
    
    public float speed = 5f;
    [Header("Attack Movement")]
    [Range(0f, 1f)] public float attackMoveSpeedMultiplier = 0.85f;
    [Header("Dash")]
    [Tooltip("Press Left Shift, right mouse button, or the gamepad right shoulder button to dash.")]
    [Min(0.05f)] public float dashDuration = 0.16f;
    [Min(0.1f)] public float dashDistance = 4.5f;
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
    [SerializeField] private bool logPlayerVisualInvariant;
    [Header("Three-Hit Combo")]
    [Tooltip("Each value should match its own animation clip once the three attack animations are added.")]
    [FormerlySerializedAs("attackDuration")]
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
    [Header("Combat Audio")]
    [Tooltip("Shared combat clip used by dash and abilities until bespoke SFX are supplied.")]
    public AudioClip abilityFallbackSfx;
    private float powerUpAttackCooldownActual;
    public AudioSource audioSource;
    private AudioSource swingAudioSource;
    private AudioSource impactAudioSource;
    private AudioSource abilityAudioSource;
    private AudioClip[] comboSwingClips;
    private AudioClip swordImpactClang;
    private AudioClip dashSfx;
    private AudioClip azureDashSfx;
    private AudioClip[] azureComboSwingClips;
    private AudioClip azureShieldImpactSfx;
    private AudioClip chronoDashSfx;
    private AudioClip[] chronoComboSwingClips;
    private AudioClip chronoImpactSfx;
    private AudioClip gravewindDashSfx;

    private Vector2 _movement;
    private float movementSlowUntil = -1f;
    private float movementSlowMultiplier = 1f;
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
    private int attackSequence;
    private readonly Collider2D[] enemyHitBuffer = new Collider2D[16];
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private readonly HashSet<BossController> hitBosses = new HashSet<BossController>();
    private readonly HashSet<EnemyHealth> hitConfiguredEnemies = new HashSet<EnemyHealth>();
    private SpriteRenderer slashVfxRenderer;
    private Sprite[][] slashVfxFrames;
    private PlayableCharacterId slashVfxCharacter = (PlayableCharacterId)(-1);
    private float slashVfxShownAt;
    private float slashVfxHideAt;
    private float slashVfxBaseScale;
    private int activeVfxHit;
    private SpriteRenderer playerSpriteRenderer;
    private SpriteRenderer dashVfxRenderer;
    private Sprite[] dashVfxFrames;
    private PlayableCharacterId dashVfxCharacter = PlayableCharacterId.AshenKnight;
    private Vector2 dashDirection;
    private bool isDashing;
    private bool skillPositionLocked;
    private Vector2 skillLockedPosition;
    private float dashEndsAt;
    private float nextDashAt;
    private float dashVfxShownAt;
    private float dashVfxHideAt;
    private float activeDashDuration;
    private float activeDashDistance;
    private Sprite[] heavyStrikeFrames;
    private bool heavyManualSpriteActive;
    private bool heavyInputPending;
    private bool isHeavyCharging;
    private bool isHeavyAttacking;
    private Coroutine heavyStrikeRoutine;
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
    private bool hasComboIndexParameter;
    private LanPlayerInput lanInput;
    public bool HasRemoteInput => lanInput != null;
    public float PowerUpCooldownRemaining => Mathf.Max(0f, powerUpAttackCooldownActual);

    /// <summary>Applies a temporary floor-hazard slow; dash movement remains an escape option.</summary>
    public void ApplyMovementSlow(float duration, float multiplier)
    {
        if (duration <= 0f) return;
        if (Time.time >= movementSlowUntil) movementSlowMultiplier = 1f;
        movementSlowUntil = Mathf.Max(movementSlowUntil, Time.time + duration);
        movementSlowMultiplier = Mathf.Min(movementSlowMultiplier, Mathf.Clamp(multiplier, 0.1f, 1f));
    }

    /// <summary>Small constrained displacement used by clearly telegraphed enemy impacts.</summary>
    public void ApplyExternalKnockback(Vector2 direction, float distance)
    {
        if (direction.sqrMagnitude < 0.001f || distance <= 0f) return;
        rb ??= GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            transform.position += (Vector3)(direction.normalized * distance);
            return;
        }
        Vector2 delta = ConstrainToWalkableArea(direction.normalized * distance);
        rb.position += delta;
        rb.velocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    public void CancelLanActions()
    {
        StopAllCoroutines();
        _movement = Vector2.zero;
        powerupChargeVfx?.Hide();
        skillController?.ResetForLanRun();
        if (rb != null) rb.velocity = Vector2.zero;
    }

    public void ResetForLanRun()
    {
        StopAllCoroutines();
        _movement = Vector2.zero;
        isAttacking = isDashing = isHeavyCharging = isHeavyAttacking = heavyInputPending = false;
        attackQueued = spaceHeld = skillPositionLocked = heavyPositionLocked = false;
        spaceHeldTime = powerUpAttackCooldownActual = nextDashAt = 0f;
        isPoweredUp = false;
        activeComboHit = nextComboHit = 0;
        attackEndsAt = hitAt = comboExpiresAt = dashEndsAt = slashVfxHideAt = dashVfxHideAt = 0f;
        powerupChargeVfx?.Hide();
        RestoreAnimatorAfterHeavyStrike();
        if (rb != null) rb.velocity = Vector2.zero;
        if (animator != null) { animator.Rebind(); animator.Update(0f); }
        skillController?.ResetForLanRun();
    }

    public bool IsAttacking => isAttacking;
    public bool IsDashing => isDashing;
    public bool IsHeavyCharging => isHeavyCharging;
    public bool IsHeavyAttacking => isHeavyAttacking;
    public bool IsHeavyLocked => isHeavyCharging || isHeavyAttacking;
    public bool IsHeavyInputPending => heavyInputPending;
    public int AttackSequence => attackSequence;
    public int ActiveComboHit => activeComboHit;
    public float ActiveComboDuration => GetComboDuration(activeComboHit);
    public Vector2 FacingDirection => isFacingLeft ? Vector2.left : Vector2.right;
    private PlayableCharacterId CurrentCharacter
    {
        get
        {
            PlayerCharacterFrameAnimator frameAnimator = GetComponent<PlayerCharacterFrameAnimator>();
            return frameAnimator != null ? frameAnimator.Character : PlayableCharacterId.AshenKnight;
        }
    }
    // The Player object's transform is the movement/collider anchor, not
    // necessarily the visual centre of every animation frame.
    public Vector3 VisualCenter => playerSpriteRenderer != null
        ? playerSpriteRenderer.bounds.center
        : transform.position;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashAt - Time.time);
    public bool HasWalkableAreaConstraint => walkableFloorTiles != null && walkableFloorTiles.Count > 0;
    public float DashCooldownNormalized => LanLaunch.Mode == LanMode.Client && LanSessionBridge.Active?.LocalState != null
        ? LanSessionBridge.Active.LocalState.dash : GetDashProfile().cooldown <= 0f ? 0f :
            DashCooldownRemaining / GetDashProfile().cooldown;

    /// <summary>
    /// Repositions the player for a skill while preserving the same floor sweep
    /// used by the normal dash. The returned position is always inside the
    /// generated map's walkable area.
    /// </summary>
    public Vector2 BlinkForSkill(Vector2 direction, float distance)
    {
        rb ??= GetComponent<Rigidbody2D>();
        if (rb == null || direction.sqrMagnitude < 0.001f || distance <= 0f)
            return transform.position;

        Vector2 start = rb.position;
        Vector2 requestedDelta = direction.normalized * distance;
        Vector2 allowedDelta = ConstrainToWalkableArea(requestedDelta);
        Vector2 destination = start + allowedDelta;
        rb.position = destination;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        // Skill casts pin the body in FixedUpdate. Update that anchor too so
        // a blink cannot be pulled back to its pre-cast position next frame.
        skillLockedPosition = destination;
        skillPositionLocked = true;
        Physics2D.SyncTransforms();
        return destination;
    }

    private void Awake()
    {
        playerControl ??= new PlayerControls();
        // Most production levels instantiate PlayerMovement without an
        // inspector-assigned fallback. Load the actual recorded sword clip
        // here so skill audio can never silently receive a null clip.
        abilityFallbackSfx ??= Resources.Load<AudioClip>("Audio/Combat/PlayerSwordSlash");
        audioSource = GetComponent<AudioSource>();
        swingAudioSource = CreateCombatAudioSource("Combo Swing Audio");
        impactAudioSource = CreateCombatAudioSource("Combat Impact Audio");
        abilityAudioSource = CreateCombatAudioSource("Ability Audio");
        comboSwingClips = new[]
        {
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing1"),
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing2"),
            Resources.Load<AudioClip>("Audio/Combat/SwordSwing3")
        };
        swordImpactClang = Resources.Load<AudioClip>("Audio/Combat/SwordImpactClang");
        dashSfx = Resources.Load<AudioClip>("Audio/Combat/DashBurst") ?? abilityFallbackSfx;
        azureDashSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/Dash");
        azureComboSwingClips = new[]
        {
            Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/BasicShieldSweep"),
            Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/BasicLanceThrust"),
            Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/BasicShieldSlam")
        };
        azureShieldImpactSfx = Resources.Load<AudioClip>(
            "Audio/Combat/Characters/AzureWarden/BasicShieldImpact");
        chronoDashSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/Dash");
        chronoComboSwingClips = new[]
        {
            Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/BasicRisingCut"),
            Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/BasicReverseCut"),
            Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/BasicCrossFinish")
        };
        chronoImpactSfx = Resources.Load<AudioClip>(
            "Audio/Combat/Characters/Chronoblade/BasicChronoImpact");
        gravewindDashSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/Dash_MistStep");
        player = gameObject;
        powerupController = player.GetComponent<PowerupController>();
    }

    private AudioSource CreateCombatAudioSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.SetActive(false);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = audioSource != null ? audioSource.spatialBlend : 0f;
        sourceObject.SetActive(true);
        return source;
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
        if (heavyStrikeRoutine != null)
        {
            StopCoroutine(heavyStrikeRoutine);
            heavyStrikeRoutine = null;
        }
        heavyInputPending = false;
        isHeavyCharging = false;
        isHeavyAttacking = false;
        powerupChargeVfx?.Hide();
        RestoreAnimatorAfterHeavyStrike();
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = Color.white;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        movementCollider = GetComponent<Collider2D>();
        // Movement is applied in FixedUpdate while Cinemachine renders every frame.
        // Interpolation prevents the camera from following discrete physics steps.
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Dash speed can exceed 40 units/second. Continuous collision keeps
            // the physical wall response reliable between fixed updates.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        ConfigureFrictionlessMovement();
        ConfigureSmoothCameraFollow();
        animator = GetComponent<Animator>();
        hasComboIndexParameter = animator != null && System.Array.Exists(animator.parameters,
            parameter => parameter.name == "ComboIndex" && parameter.type == AnimatorControllerParameterType.Int);
        lanInput = GetComponent<LanPlayerInput>();
        powerupCircleController = GetComponentInChildren<PowerupCircleController>();
        if (powerupCircleController == null)
            powerupCircleController = GameObject.FindGameObjectWithTag("Powerup Attack").GetComponent<PowerupCircleController>();
        powerupChargeVfx = GetComponent<PowerupChargeVfx>();
        if (powerupChargeVfx == null)
            powerupChargeVfx = gameObject.AddComponent<PowerupChargeVfx>();
        powerupChargeVfx.SetAnchor(powerupCircleController.transform);
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
        if (GetComponent<EnemyDirectionIndicator>() == null)
            gameObject.AddComponent<EnemyDirectionIndicator>();
        AssertSingleAuthoritativePlayerVisual("startup");
    }

    /// <summary>Called after every generated level places the persistent player.</summary>
    public void ConfigureWalkableArea(IEnumerable<Vector2Int> floorTiles, Vector2 spawnPosition)
    {
        walkableFloorTiles = floorTiles != null
            ? new HashSet<Vector2Int>(floorTiles)
            : null;
        rb ??= GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.velocity = Vector2.zero;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Physics2D.SyncTransforms();
        }
    }

    private void ConfigureFrictionlessMovement()
    {
        if (movementCollider == null)
            return;

        frictionlessMovementMaterial = new PhysicsMaterial2D("Player Movement - No Friction")
        {
            friction = 0f,
            bounciness = 0f
        };
        movementCollider.sharedMaterial = frictionlessMovementMaterial;
    }

    private static void ConfigureSmoothCameraFollow()
    {
        CinemachineBrain brain = FindObjectOfType<CinemachineBrain>();
        if (brain != null)
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;

        foreach (CinemachineVirtualCamera camera in FindObjectsOfType<CinemachineVirtualCamera>())
        {
            CinemachineFramingTransposer framing =
                camera.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framing == null) continue;
            framing.m_LookaheadTime = 0f;
            framing.m_LookaheadSmoothing = 0f;
        }
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

    public void SetFacing(bool faceLeft)
    {
        if (isFacingLeft != faceLeft)
        {
            isFacingLeft = faceLeft;
            Flip();
        }
    }

    private void Update()
    {
        UpdateSlashVfx();
        UpdateDashVfx();
        UpdateHeavyChargeVisual();

        if (GameManager.isGamePaused || GameManager.isGameOver || GameManager.isWin)
        {
            _movement = Vector2.zero;
            powerupChargeVfx?.Hide();
            return;
        }

        if(isPoweredUp)
        {
            powerUpAttackCooldownActual -= Time.deltaTime;
            if(powerUpAttackCooldownActual <= 0)
            {
                isPoweredUp = false;
                if (!HasRemoteInput) powerupController.ShowPoweredUp();
            } else {
                if (!HasRemoteInput) powerupController.UpdateCooldown(powerUpAttackCooldownActual);
            }
            
        }
        

        _movement = HasRemoteInput ? lanInput.Frame.movement : ReadLocalMovement();

        if (isHeavyAttacking)
        {
            HandleDashInput();
            if (isDashing) return;
            _movement = Vector2.zero;
            powerupChargeVfx?.Hide();
            return;
        }

        // Skills have a fixed casting position.  Previously only attack/dash
        // input was blocked; FixedUpdate still applied the held movement axis,
        // so the player (and E's attached VFX) slid sideways during the cast.
        if (skillController != null && skillController.IsSkillCasting)
        {
            _movement = Vector2.zero;
            powerupChargeVfx?.Hide();
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

        if (!IsHeavyLocked && (HasRemoteInput ? lanInput.Frame.powerHeld : Input.GetKey(KeyCode.Space)))
        {
            if(!isPoweredUp) 
            {
                if(GetRadius(spaceHeldTime) <= maxPowerupRadius)
                {
                    spaceHeldTime += Time.deltaTime;
                }
                spaceHeld = true;
                powerupCircleController.setRadius(GetRadius(spaceHeldTime));
                powerupChargeVfx?.Show(GetPowerupChargeNormalized());
            }
            else
            {
                powerupChargeVfx?.Hide();
            }
        }
        else if(!IsHeavyLocked && spaceHeld)
        {
            powerupChargeVfx?.Hide();
            PowerUpAttack();
            if (!HasRemoteInput) powerupController.HidePoweredUp();
        }
        else
        {
            powerupChargeVfx?.Hide();
        }
    }

    private void HandleAttackInput()
    {
        if (isDashing || isHeavyAttacking || (skillController != null && skillController.IsSkillCasting))
            return;

        if (heavyInputPending)
        {
            if (!(HasRemoteInput ? lanInput.Frame.attackHeld : attack.IsPressed()))
            {
                float heldTime = Time.time - heavyInputStartedAt;
                heavyInputPending = false;
                if (heldTime >= heavyChargeThreshold)
                    heavyStrikeRoutine = StartCoroutine(PerformHeavyStrike(HeavyChargeNormalized));
                else
                    StartOrQueueComboAttack();
            }
            else if (!isHeavyCharging && Time.time - heavyInputStartedAt >= heavyChargeThreshold)
            {
                BeginHeavyCharge();
            }

            return;
        }

        if (HasRemoteInput ? lanInput.Frame.attackPressed : attack.WasPressedThisFrame())
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

        if (Time.time < nextDashAt || !WasDashPressed() ||
            (skillController != null && skillController.IsSkillCasting))
            return;

        // Dash takes priority over a held or repeating left-click attack.
        InterruptAttackForSkill();

        dashDirection = _movement.sqrMagnitude > 0.01f
            ? _movement.normalized
            : isFacingLeft ? Vector2.left : Vector2.right;
        isDashing = true;
        DashProfile profile = GetDashProfile();
        activeDashDuration = profile.duration;
        activeDashDistance = profile.distance;
        dashEndsAt = Time.time + activeDashDuration;
        nextDashAt = Time.time + profile.cooldown;
        // The run clip is the dash body pose.  Previously this was forced to
        // zero, leaving the character visibly idle while moving several tiles.
        animator.SetFloat("Speed", 5f);
        ShowDashVfx();
        PlayAbilitySfx(GetDashSfxForCharacter(), volume: 0.84f, pitch: 1f);
    }

    private AudioClip GetDashSfxForCharacter()
    {
        return CurrentCharacter switch
        {
            PlayableCharacterId.AzureWarden => azureDashSfx ?? dashSfx,
            PlayableCharacterId.Chronoblade => chronoDashSfx ?? dashSfx,
            PlayableCharacterId.GravewindRanger => gravewindDashSfx ?? dashSfx,
            _ => dashSfx
        };
    }

    private bool WasDashPressed()
    {
        if (HasRemoteInput) return lanInput.Frame.dash;
        return (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
    }

    /// <summary>Cancel only the current attack so a newly pressed skill can start immediately.</summary>
    public void InterruptAttackForSkill()
    {
        if (heavyStrikeRoutine != null)
        {
            StopCoroutine(heavyStrikeRoutine);
            heavyStrikeRoutine = null;
        }
        if (isAttacking && !hitApplied && Time.time >= hitAt)
            DealComboDamage();
        isAttacking = false;
        attackQueued = false;
        hitApplied = true;
        nextComboHit = 0;
        comboExpiresAt = 0f;
        heavyInputPending = false;
        isHeavyCharging = false;
        isHeavyAttacking = false;
        heavyPositionLocked = false;
        isDashing = false;
        if (rb != null) rb.velocity = Vector2.zero;
        if (slashVfxRenderer != null) slashVfxRenderer.enabled = false;
        if (dashVfxRenderer != null) dashVfxRenderer.enabled = false;
        if (playerSpriteRenderer != null) playerSpriteRenderer.color = Color.white;
        RestoreAnimatorAfterHeavyStrike();
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
        heavyStrikeFrames = new Sprite[HeavyStrikeFrameCount];
        for (int frame = 0; frame < heavyStrikeFrames.Length; frame++)
        {
            Texture2D texture = Resources.Load<Texture2D>($"Combat/Frames/HeavyStrikeBody_12f/Frame_{frame:00}");
            if (texture == null)
            {
                heavyStrikeFrames = null;
                return;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            heavyStrikeFrames[frame] = frame == 4
                ? CreateCleanHeavyRaiseFrame(texture)
                : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0f), 100f);
            heavyStrikeFrames[frame].name = $"HeavyStrikeBody_{frame:00}";
        }
    }

    // The otherwise usable raised-sword frame has three small detached islands
    // on its right side. They are source-art debris, not part of the character.
    // Strip only disconnected components below this size so the sword/body stay
    // untouched while the visible stray pixels disappear.
    private static Sprite CreateCleanHeavyRaiseFrame(Texture2D source)
    {
        const byte visibleAlphaThreshold = 8;
        const int detachedFragmentMaxPixels = 96;
        int width = source.width;
        int height = source.height;
        Color32[] sourcePixels = source.GetPixels32();
        Color32[] framePixels = sourcePixels;

        bool[] visited = new bool[framePixels.Length];
        Queue<int> queue = new Queue<int>();
        List<int> component = new List<int>();
        for (int pixelIndex = 0; pixelIndex < framePixels.Length; pixelIndex++)
        {
            if (visited[pixelIndex] || framePixels[pixelIndex].a < visibleAlphaThreshold)
                continue;

            component.Clear();
            queue.Enqueue(pixelIndex);
            visited[pixelIndex] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                component.Add(current);
                int x = current % width;
                int y = current / width;
                TryQueueHeavyPixel(current - 1, x > 0, framePixels, visited, queue, visibleAlphaThreshold);
                TryQueueHeavyPixel(current + 1, x < width - 1, framePixels, visited, queue, visibleAlphaThreshold);
                TryQueueHeavyPixel(current - width, y > 0, framePixels, visited, queue, visibleAlphaThreshold);
                TryQueueHeavyPixel(current + width, y < height - 1, framePixels, visited, queue, visibleAlphaThreshold);
            }

            if (component.Count >= detachedFragmentMaxPixels)
                continue;

            foreach (int fragmentPixel in component)
                framePixels[fragmentPixel].a = 0;
        }

        Texture2D cleanedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HeavyStrikeBody_04_Clean",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        cleanedTexture.SetPixels32(framePixels);
        cleanedTexture.Apply(false, LanLaunch.Mode == LanMode.Solo);
        return Sprite.Create(cleanedTexture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
    }

    private static void TryQueueHeavyPixel(int pixelIndex, bool isInsideFrame, Color32[] pixels, bool[] visited,
        Queue<int> queue, byte visibleAlphaThreshold)
    {
        if (!isInsideFrame || visited[pixelIndex] || pixels[pixelIndex].a < visibleAlphaThreshold)
            return;

        visited[pixelIndex] = true;
        queue.Enqueue(pixelIndex);
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

        int chargeStep = Mathf.Min(6, Mathf.FloorToInt(HeavyChargeNormalized * 7f));
        SetHeavyStrikeFrame(HeavyChargeFrameMap[chargeStep]);
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
        heavyStrikeRoutine = null;
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
        PlayerCharacterFrameAnimator frameAnimator = GetComponent<PlayerCharacterFrameAnimator>();
        if (frameAnimator != null && frameAnimator.Character != PlayableCharacterId.AshenKnight)
        {
            frameAnimator.PlaySkillMotion(usePowerupPose);
            return;
        }

        if (animator == null)
            return;

        animator.SetFloat("Speed", 0f);
        if (usePowerupPose)
        {
            animator.SetTrigger("Powerup Attack");
            return;
        }

        if (hasComboIndexParameter)
            animator.SetInteger("ComboIndex", Mathf.Clamp(comboIndex, 1, 3));
        animator.SetTrigger("Attack");
    }

    private void StartAttack(int comboHit)
    {
        activeComboHit = Mathf.Clamp(comboHit, 0, 2);
        attackSequence++;
        isAttacking = true;
        hitApplied = false;
        float duration = GetComboDuration(activeComboHit);
        attackEndsAt = Time.time + duration;
        hitAt = Time.time + duration * hitImpactNormalizedTime;

        // The current controller still uses one clip. ComboIndex is ready for Attack_1/2/3 transitions.
        if (hasComboIndexParameter)
            animator.SetInteger("ComboIndex", activeComboHit + 1);
        animator.SetTrigger("Attack");
        if (CurrentCharacter == PlayableCharacterId.GravewindRanger)
        {
            // Gravewind's basic attack is resolved as an arrow at the impact
            // frame. Never flash the shared Ashen Knight sword arc on top.
            if (slashVfxRenderer != null)
                slashVfxRenderer.enabled = false;
        }
        else
        {
            ShowSlashVfx(activeComboHit);
            if (CurrentCharacter == PlayableCharacterId.AzureWarden ||
                CurrentCharacter == PlayableCharacterId.Chronoblade)
                PlayCurrentComboSwing();
        }
    }

    private void CreateSlashVfx()
    {
        GameObject slashVfx = new GameObject("Slash VFX");
        slashVfx.transform.SetParent(transform, false);
        slashVfxRenderer = slashVfx.AddComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
        {
            slashVfxRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            slashVfxRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
        }

        slashVfxRenderer.enabled = false;
        EnsureSlashVfxForCharacter(true);
    }

    private void EnsureSlashVfxForCharacter(bool force = false)
    {
        PlayableCharacterId character = CurrentCharacter;
        if (!force && slashVfxCharacter == character && slashVfxFrames != null)
            return;

        slashVfxCharacter = character;
        if (character == PlayableCharacterId.GravewindRanger)
        {
            slashVfxFrames = null;
            return;
        }

        slashVfxFrames = new Sprite[3][];
        if (character == PlayableCharacterId.AzureWarden)
        {
            string[] azureFolders =
            {
                "AzureBasicShieldSweep_6f",
                "AzureBasicLanceThrust_6f",
                "AzureBasicShieldSlam_6f"
            };
            for (int hit = 0; hit < azureFolders.Length; hit++)
                slashVfxFrames[hit] = PlayerSkillController.LoadPixelFrames(
                    $"Combat/Frames/{azureFolders[hit]}", 6,
                    PlayerSkillController.FrameAnchorMode.CellCenter, 256f);
            return;
        }

        if (character == PlayableCharacterId.Chronoblade)
        {
            string[] chronoFolders =
            {
                "ChronoBasicRisingCut_6f",
                "ChronoBasicReverseCut_6f",
                "ChronoBasicCrossFinish_6f"
            };
            for (int hit = 0; hit < chronoFolders.Length; hit++)
                slashVfxFrames[hit] = PlayerSkillController.LoadPixelFrames(
                    $"Combat/Frames/{chronoFolders[hit]}", 6,
                    PlayerSkillController.FrameAnchorMode.CellCenter, 256f);
            return;
        }

        for (int hit = 0; hit < slashVfxFrames.Length; hit++)
        {
            // Hit 2 is the five-frame Ashen sequence. The folder keeps its
            // legacy name so existing Resources paths remain valid.
            int frameCount = hit == 1 ? 5 : 6;
            slashVfxFrames[hit] = PlayerSkillController.LoadPixelFrames(
                $"Combat/Frames/SlashVfxHit{hit + 1}_6f", frameCount);
        }
    }

    private void ShowSlashVfx(int comboHit)
    {
        EnsureSlashVfxForCharacter();
        if (slashVfxRenderer == null || slashVfxFrames == null || attackPoint == null)
            return;

        activeVfxHit = comboHit;
        if (CurrentCharacter == PlayableCharacterId.AzureWarden)
            slashVfxBaseScale = comboHit == 0 ? 1.05f : comboHit == 1 ? 1.22f : 1.35f;
        else if (CurrentCharacter == PlayableCharacterId.Chronoblade)
            slashVfxBaseScale = comboHit == 0 ? 1.08f : comboHit == 1 ? 1.16f : 1.28f;
        else
            slashVfxBaseScale = comboHit == 0 ? hit1VfxScale : comboHit == 1 ? hit2VfxScale : hit3VfxScale;
        slashVfxRenderer.sprite = slashVfxFrames[comboHit][0];
        // Chronoblade's generated combo frames do not share one source-facing
        // direction: Rising Cut is already authored forward, while Reverse Cut
        // and Cross Finish need one extra horizontal flip. The effect then
        // inherits the Player transform for the actual left/right direction.
        // Other characters keep their authored orientation.
        slashVfxRenderer.flipX = CurrentCharacter == PlayableCharacterId.Chronoblade && comboHit != 0;
        slashVfxRenderer.flipY = false;
        slashVfxRenderer.color = Color.white;
        slashVfxRenderer.transform.localPosition = GetSlashVfxLocalPosition(comboHit);
        slashVfxRenderer.transform.localRotation = Quaternion.identity;
        slashVfxRenderer.transform.localScale = Vector3.one * slashVfxBaseScale;
        slashVfxRenderer.enabled = true;
        slashVfxShownAt = Time.time;
        slashVfxHideAt = Time.time + slashVfxDuration;
    }

    private Vector3 GetSlashVfxLocalPosition(int comboHit)
    {
        Vector3 localAttackPoint = transform.InverseTransformPoint(attackPoint.position);

        // Rising Cut and Reverse Cut travel into the target, so they belong at
        // the forward attack point. Cross Finish is a centred X-shaped strike;
        // anchoring it to that same forward point visibly shifts its final hit
        // away from the character. Centre only its horizontal pivot while
        // retaining the authored vertical weapon height and the existing
        // gameplay hit circle.
        if (CurrentCharacter == PlayableCharacterId.Chronoblade && comboHit == 2)
            localAttackPoint.x = 0f;

        return localAttackPoint;
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
        EnsureDashVfxForCharacter(true);
        WorldDepthSort.Attach(dashVfx, dashVfx.transform.position.y);
    }

    private void ShowDashVfx()
    {
        EnsureDashVfxForCharacter();
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

    private void EnsureDashVfxForCharacter(bool force = false)
    {
        PlayerCharacterFrameAnimator frameAnimator = GetComponent<PlayerCharacterFrameAnimator>();
        PlayableCharacterId character = frameAnimator != null ? frameAnimator.Character : PlayableCharacterId.AshenKnight;
        if (!force && character == dashVfxCharacter && dashVfxFrames != null && dashVfxFrames.Length > 0)
            return;

        dashVfxCharacter = character;
        string path = character switch
        {
            PlayableCharacterId.AzureWarden => "Combat/Frames/AzureWardenDash_6f",
            PlayableCharacterId.Chronoblade => "Combat/Frames/ChronobladeDash_6f",
            PlayableCharacterId.GravewindRanger => "Combat/Frames/GravewindRangerDash_6f",
            _ => "Combat/Frames/DashBurstPixel_6f"
        };
        dashVfxFrames = PlayerSkillController.LoadPixelFrames(path, 6);
    }

    private DashProfile GetDashProfile()
    {
        PlayerCharacterFrameAnimator frameAnimator = GetComponent<PlayerCharacterFrameAnimator>();
        return frameAnimator != null ? frameAnimator.Character switch
        {
            PlayableCharacterId.AzureWarden => new DashProfile(0.20f, 3.8f, 0.86f),
            PlayableCharacterId.Chronoblade => new DashProfile(0.12f, 5.35f, 0.64f),
            PlayableCharacterId.GravewindRanger => new DashProfile(0.15f, 4.85f, 0.72f),
            _ => new DashProfile(dashDuration, dashDistance, dashCooldown)
        } : new DashProfile(dashDuration, dashDistance, dashCooldown);
    }

    private readonly struct DashProfile
    {
        public readonly float duration;
        public readonly float distance;
        public readonly float cooldown;

        public DashProfile(float duration, float distance, float cooldown)
        {
            this.duration = duration;
            this.distance = distance;
            this.cooldown = cooldown;
        }
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

        int expectedPlayers = LanLaunch.Mode == LanMode.Host && LanSessionBridge.Active != null &&
            LanSessionBridge.Active.HasGuest ? 2 : 1;
        bool valid = players.Length == expectedPlayers && authoritativeVisuals == expectedPlayers && copiedBodyVisuals == 0;
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
        if (frictionlessMovementMaterial != null)
            Destroy(frictionlessMovementMaterial);
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

        int damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * damageMultiplier));
        if (CurrentCharacter == PlayableCharacterId.GravewindRanger && skillController != null)
        {
            skillController.FireGravewindBasicArrow(damage, activeComboHit);
            return;
        }

        if (CurrentCharacter == PlayableCharacterId.AzureWarden)
        {
            DamageEnemiesInRadius(attackPoint.position, attackRange * rangeMultiplier, damage,
                playSwordImpact: true, impactKind: CombatImpactKind.Azure);
            return;
        }

        if (CurrentCharacter == PlayableCharacterId.Chronoblade)
        {
            DamageEnemiesInRadius(attackPoint.position, attackRange * rangeMultiplier, damage,
                playSwordImpact: true, impactKind: CombatImpactKind.Chrono);
            return;
        }

        DamageEnemies(attackPoint.position, attackRange * rangeMultiplier, damage, true);
    }

    private void DamageEnemies(Vector2 center, float radius, int damage, bool playSwordImpact = false)
    {
        DamageEnemiesInRadius(center, radius, damage, null, null, playSwordImpact);
    }

    public int DamageEnemiesInRadius(Vector2 center, float radius, int damage,
        HashSet<Enemy> alreadyHitEnemies = null, HashSet<BossController> alreadyHitBosses = null,
        bool playSwordImpact = false, HashSet<EnemyHealth> alreadyHitConfiguredEnemies = null,
        CombatImpactKind impactKind = CombatImpactKind.Sword)
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
        if (alreadyHitConfiguredEnemies == null)
        {
            hitConfiguredEnemies.Clear();
            alreadyHitConfiguredEnemies = hitConfiguredEnemies;
        }

        bool damagedEnemy = false;
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            if (enemyHitBuffer[i] == null) continue;
            EnemyHealth configured = enemyHitBuffer[i].GetComponentInParent<EnemyHealth>();
            if (configured != null)
            {
                if (!configured.IsDead && alreadyHitConfiguredEnemies.Add(configured))
                {
                    configured.TakeDamage(damage);
                    CombatHitVfx.Spawn(enemyHitBuffer[i].bounds.center, impactKind,
                        configured.GetComponent<MiniBossController>() != null ||
                        configured.GetComponent<SkeletonKingController>() != null ? 1.5f : 0.85f);
                    damagedEnemy = true;
                    damagedCount++;
                }
                continue;
            }

            Enemy enemy = enemyHitBuffer[i].GetComponentInParent<Enemy>();
            if (enemy != null && alreadyHitEnemies.Add(enemy))
            {
                enemy.TakeDamage(damage);
                CombatHitVfx.Spawn(enemyHitBuffer[i].bounds.center, impactKind, 0.85f);
                damagedEnemy = true;
                damagedCount++;
            }

            BossController boss = enemyHitBuffer[i].GetComponentInParent<BossController>();
            if (boss != null && alreadyHitBosses.Add(boss))
            {
                boss.TakeDamage(damage);
                CombatHitVfx.Spawn(enemyHitBuffer[i].bounds.center, impactKind, 1.4f);
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
        int damage, bool playSwordImpact = false, CombatImpactKind impactKind = CombatImpactKind.Heavy)
    {
        if (direction.sqrMagnitude < 0.001f || range <= 0f)
            return 0;

        direction.Normalize();
        float minDot = Mathf.Cos(Mathf.Clamp(halfAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, range, enemyHitBuffer, enemyLayers);
        hitEnemies.Clear();
        hitBosses.Clear();
        hitConfiguredEnemies.Clear();
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

            EnemyHealth configured = targetCollider.GetComponentInParent<EnemyHealth>();
            if (configured != null)
            {
                if (!configured.IsDead && hitConfiguredEnemies.Add(configured))
                {
                    configured.TakeDamage(damage);
                    CombatHitVfx.Spawn(targetCollider.bounds.center, impactKind, 1f);
                    damagedEnemy = true;
                    damagedCount++;
                }
                continue;
            }

            Enemy enemy = targetCollider.GetComponentInParent<Enemy>();
            if (enemy != null && hitEnemies.Add(enemy))
            {
                enemy.TakeDamage(damage);
                CombatHitVfx.Spawn(targetCollider.bounds.center, impactKind, 1f);
                damagedEnemy = true;
                damagedCount++;
            }

            BossController boss = targetCollider.GetComponentInParent<BossController>();
            if (boss != null && hitBosses.Add(boss))
            {
                boss.TakeDamage(damage);
                CombatHitVfx.Spawn(targetCollider.bounds.center, impactKind, 1.4f);
                damagedEnemy = true;
                damagedCount++;
            }
        }

        if (playSwordImpact && damagedEnemy)
        {
            if (impactKind == CombatImpactKind.Azure)
                PlaySwordImpact();
            else
                PlayHeavyImpact();
        }

        if (damagedCount > 0 && skillController != null)
            skillController.AddUltimateCharge(damagedCount * 8f);

        return damagedCount;
    }

    void PowerUpAttack()
    {
        float radius = GetRadius(spaceHeldTime);
        powerupChargeVfx?.Hide();
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
        if (rb == null)
            return;

        if (isDashing)
        {
            float duration = activeDashDuration > 0f ? activeDashDuration : dashDuration;
            float distance = activeDashDistance > 0f ? activeDashDistance : dashDistance;
            Vector2 requestedDelta = dashDirection * (distance / duration) * Time.fixedDeltaTime;
            Vector2 allowedDelta = ConstrainToWalkableArea(requestedDelta);
            rb.MovePosition(rb.position + allowedDelta);
            if (allowedDelta.sqrMagnitude < 0.0001f)
            {
                isDashing = false;
                rb.velocity = Vector2.zero;
                if (animator != null)
                    animator.SetFloat("Speed", _movement == Vector2.zero ? 0f : 5f);
            }
            return;
        }

        if (isHeavyAttacking)
        {
            if (!heavyPositionLocked)
            {
                heavyLockedPosition = rb.position;
                heavyPositionLocked = true;
            }

            rb.velocity = Vector2.zero;
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
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MovePosition(skillLockedPosition);
            return;
        }

        skillPositionLocked = false;

        // A Dynamic Rigidbody2D can occasionally drop a MovePosition request while
        // it is resolving the generated wall collider. Drive its velocity instead:
        // collision remains fully physical, while continuous keyboard movement is
        // no longer lost between physics ticks.
        float movementMultiplier = isHeavyCharging ? heavyChargeMoveSpeedMultiplier :
            isAttacking ? attackMoveSpeedMultiplier : 1f;
        if (Time.time >= movementSlowUntil) movementSlowMultiplier = 1f;
        else movementMultiplier *= movementSlowMultiplier;
        // Ordinary movement remains fully physics-driven. Applying the tile
        // sweep to every tiny keyboard step made a valid spawn look blocked
        // on maps whose art/collider pivot is offset from the tile centre.
        rb.velocity = _movement * (speed * movementMultiplier);
    }

    private Vector2 ConstrainToWalkableArea(Vector2 desiredDelta)
    {
        if (!HasWalkableAreaConstraint)
            return desiredDelta;

        // The generator's logical player anchor is stable across every
        // character sprite and collider pivot. Sweep that anchor for dash;
        // Continuous Rigidbody2D collision still protects the full collider.
        Vector2 best = Vector2.zero;
        float bestDistance = -1f;
        for (int i = 0; i < DashCellOffsets.Length; i++)
        {
            Vector2 offset = DashCellOffsets[i];
            if (!WalkableAreaConstraint.ContainsFootprint(walkableFloorTiles, rb.position,
                    Vector2.zero, Vector2.zero, cellLookupOffset: offset))
                continue;
            Vector2 allowed = WalkableAreaConstraint.ConstrainDelta(
                walkableFloorTiles, rb.position, desiredDelta, Vector2.zero, Vector2.zero, offset);
            float forwardDistance = Vector2.Dot(allowed, desiredDelta.normalized);
            if (forwardDistance <= bestDistance) continue;
            best = allowed;
            bestDistance = forwardDistance;
        }
        return best;
    }

    /// <summary>
    /// PlayerInput / UI action-map changes must never leave the offline player
    /// without movement. The generated Move action remains the primary source
    /// (so gamepad and remapped controls still work); raw keyboard polling is
    /// only a fallback for the standard WASD / arrow-key controls.
    /// </summary>
    private Vector2 ReadLocalMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                               (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                             (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            Vector2 keyboardMovement = new Vector2(horizontal, vertical);
            if (keyboardMovement.sqrMagnitude > 0.0001f)
                return keyboardMovement.sqrMagnitude > 1f ? keyboardMovement.normalized : keyboardMovement;
        }

        // Preserve remapped controls and gamepad support when no keyboard key is held.
        return move != null ? move.ReadValue<Vector2>() : Vector2.zero;
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

    private float GetPowerupChargeNormalized()
    {
        float minimumRadius = GetRadius(0f);
        float chargeRange = maxPowerupRadius - minimumRadius;
        if (chargeRange <= Mathf.Epsilon)
            return 1f;

        return Mathf.Clamp01((GetRadius(spaceHeldTime) - minimumRadius) / chargeRange);
    }

    public void PlaySFX(AudioClip clip)
    {
        // Q/E use the same body clip as a normal attack but play their own
        // ability SFX at cast time. Never layer the legacy animation event on
        // top of the dedicated sound.
        if (skillController != null && skillController.IsSkillCasting)
            return;

        // The legacy attack clip raises sword-swing audio events. Gravewind
        // uses a projectile and must not inherit that melee sound layer.
        if (CurrentCharacter == PlayableCharacterId.GravewindRanger)
            return;

        if (!isAttacking)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
                LanSessionBridge.Active?.PublishSound(clip);
            }
            return;
        }

        AudioClip comboClip = GetComboSwingClip(activeComboHit) ?? clip;
        if (comboClip == null)
            return;

        swingAudioSource.pitch = activeComboHit == 0 ? hit1SwingPitch : activeComboHit == 1 ? hit2SwingPitch : hit3SwingPitch;
        float volume = activeComboHit == 0 ? hit1SwingVolume : activeComboHit == 1 ? hit2SwingVolume : hit3SwingVolume;
        swingAudioSource.PlayOneShot(comboClip, volume);
        LanSessionBridge.Active?.PublishSound(comboClip, volume, swingAudioSource.pitch);
    }

    /// <summary>
    /// Plays a gameplay-triggered ability sound and replicates it to the LAN guest.
    /// The clip remains configurable on the Player so future bespoke SFX can replace
    /// the temporary shared sword sound without changing ability code.
    /// </summary>
    public void PlayAbilitySfx(AudioClip clip = null, float volume = 1f, float pitch = 1f)
    {
        clip ??= abilityFallbackSfx;
        if (clip == null || abilityAudioSource == null)
        {
            Debug.LogWarning($"[{name}] Tried to play ability SFX but no clip/source is available.", this);
            return;
        }

        abilityAudioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        float clampedVolume = Mathf.Clamp01(volume);
        abilityAudioSource.PlayOneShot(clip, clampedVolume);
        LanSessionBridge.Active?.PublishSound(clip, clampedVolume, abilityAudioSource.pitch);
    }

    private AudioClip GetComboSwingClip(int comboHit)
    {
        AudioClip[] clips = CurrentCharacter switch
        {
            PlayableCharacterId.AzureWarden => azureComboSwingClips,
            PlayableCharacterId.Chronoblade => chronoComboSwingClips,
            _ => comboSwingClips
        };
        if (clips == null || comboHit < 0 || comboHit >= clips.Length)
            return null;

        return clips[comboHit];
    }

    private void PlayCurrentComboSwing()
    {
        AudioClip clip = GetComboSwingClip(activeComboHit);
        if (clip == null || swingAudioSource == null)
            return;

        float pitch = CurrentCharacter == PlayableCharacterId.Chronoblade
            ? (activeComboHit == 0 ? 1.18f : activeComboHit == 1 ? 1.28f : 1.02f)
            : (activeComboHit == 0 ? 1.02f : activeComboHit == 1 ? 1.10f : 0.88f);
        float volume = activeComboHit == 2 ? 1f : 0.86f;
        swingAudioSource.pitch = pitch;
        swingAudioSource.PlayOneShot(clip, volume);
        LanSessionBridge.Active?.PublishSound(clip, volume, pitch);
    }

    private void PlaySwordImpact()
    {
        AudioClip impactClip = CurrentCharacter switch
        {
            PlayableCharacterId.AzureWarden => azureShieldImpactSfx,
            PlayableCharacterId.Chronoblade => chronoImpactSfx,
            _ => swordImpactClang
        };
        if (impactClip == null || impactAudioSource == null)
            return;

        impactAudioSource.pitch = CurrentCharacter switch
        {
            PlayableCharacterId.AzureWarden => activeComboHit == 2 ? 0.82f : 1f,
            PlayableCharacterId.Chronoblade => activeComboHit == 2 ? 0.92f : 1.14f,
            _ => activeComboHit == 2 ? 0.9f : 1.05f
        };
        float volume = activeComboHit == 2 ? 1f : 0.75f;
        impactAudioSource.PlayOneShot(impactClip, volume);
        LanSessionBridge.Active?.PublishSound(impactClip, volume, impactAudioSource.pitch);
    }

    private void PlayHeavyImpact()
    {
        if (swordImpactClang == null || impactAudioSource == null)
            return;

        impactAudioSource.pitch = 0.78f;
        impactAudioSource.PlayOneShot(swordImpactClang, 1f);
        LanSessionBridge.Active?.PublishSound(swordImpactClang, 1f, impactAudioSource.pitch);
    }
}
