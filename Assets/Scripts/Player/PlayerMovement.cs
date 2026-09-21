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
    private SpriteRenderer slashVfxRenderer;
    private Sprite[][] slashVfxFrames;
    private float slashVfxShownAt;
    private float slashVfxHideAt;
    private float slashVfxBaseScale;
    private int activeVfxHit;
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
        CreateSlashVfx();
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

        SpriteRenderer playerRenderer = GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            slashVfxRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            slashVfxRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
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
        int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, enemyHitBuffer, enemyLayers);
        hitEnemies.Clear();
        bool damagedEnemy = false;

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = enemyHitBuffer[i].GetComponentInParent<Enemy>();
            if (enemy != null && hitEnemies.Add(enemy))
            {
                enemy.TakeDamage(damage);
                damagedEnemy = true;
            }
        }

        if (playSwordImpact && damagedEnemy)
            PlaySwordImpact();
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
}
