using System.Collections;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Wander,
    Chase,
    Attack,
    Hurt,
    Dead
}

[DisallowMultipleComponent]
public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private EnemyConfigSO config;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemySummoner summoner;
    [SerializeField] private MiniBossController miniBoss;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    private HealthController target;
    private float nextTargetScan;
    private float nextWanderTime;
    private float hurtUntil;
    private bool initialized;
    private Coroutine dashRoutine;
    private bool isDashing;

    public EnemyState CurrentState => currentState;
    public EnemyConfigSO Config => config;
    public event System.Action<EnemyState> StateChanged;
    public static event System.Action<EnemyBrain, HealthController> ChargeTelegraph;

    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (movement == null) movement = GetComponent<EnemyMovement>();
        if (combat == null) combat = GetComponent<EnemyCombat>();
        if (summoner == null) summoner = GetComponent<EnemySummoner>();
        if (miniBoss == null) miniBoss = GetComponent<MiniBossController>();
        if (animator == null) animator = GetComponent<Animator>();
        ApplyConfiguration();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        if (combat != null) combat.AttackFinished += OnAttackFinished;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (combat != null) combat.AttackFinished -= OnAttackFinished;
        if (dashRoutine != null) StopCoroutine(dashRoutine);
        isDashing = false;
    }

    private void Start()
    {
        initialized = true;
        ChangeState(EnemyState.Idle);
    }

    private void Update()
    {
        if (health == null || health.IsDead || currentState == EnemyState.Dead || !initialized) return;

        if (currentState == EnemyState.Hurt)
        {
            if (Time.time < hurtUntil) return;
            ChangeState(EnemyState.Idle);
        }

        if (Time.time >= nextTargetScan)
        {
            target = EnemyTargeting.FindClosestLivingPlayer(transform.position);
            nextTargetScan = Time.time + 0.35f;
        }

        if (target == null || target.currentHealth <= 0)
        {
            target = null;
            Wander();
            return;
        }

        float distance = Vector2.Distance(transform.position, target.transform.position);
        movement?.FaceTarget(target.transform.position);
        if (miniBoss != null && miniBoss.IsUsingSpecial)
        {
            ChangeState(EnemyState.Attack);
            movement?.Stop();
            SetWalkAnimation(false, false);
            return;
        }
        if (isDashing) return;
        if (distance > config.detectionRange)
        {
            Wander();
            return;
        }

        bool keepsRange = config.archetype == EnemyArchetype.Ranged || config.archetype == EnemyArchetype.Summoner;
        if (keepsRange && distance < config.preferredRange * 0.65f)
        {
            ChangeState(EnemyState.Wander);
            movement?.TryRetreatFrom(target.transform.position, config.preferredRange, config.chaseSpeed, config.repathInterval);
            SetWalkAnimation(true, false);
            return;
        }

        // Chargers telegraph and rush from beyond normal melee range, making their
        // attack visibly different and giving the player time/space to dodge.
        bool isCharger = config.archetype == EnemyArchetype.Charger || config.archetype == EnemyArchetype.Elite;
        if (isCharger && combat != null && dashRoutine == null && !combat.IsAttacking &&
            distance > config.attackRange * 0.75f && distance <= config.specialRange &&
            Time.time >= combat.NextAttackTime)
        {
            ChangeState(EnemyState.Attack);
            SetWalkAnimation(false, false);
            dashRoutine = StartCoroutine(DashAttack());
            return;
        }

        // Bone Sentinel's special is a delayed three-shard volley while approaching;
        // up close it still uses its heavy melee slam.
        if (miniBoss != null && distance > config.attackRange * 1.1f &&
            distance <= config.specialRange && miniBoss.TrySpecial(target))
        {
            ChangeState(EnemyState.Attack);
            movement?.Stop();
            SetWalkAnimation(false, false);
            return;
        }

        float engagementRange = keepsRange ? config.preferredRange : config.attackRange;
        if (distance > engagementRange)
        {
            ChangeState(EnemyState.Chase);
            movement?.SetDestination(target.transform.position, config.chaseSpeed, config.repathInterval);
            SetWalkAnimation(true, true);
            if (config.archetype == EnemyArchetype.Summoner) summoner?.TrySummon();
            return;
        }

        ChangeState(EnemyState.Attack);
        movement?.Stop();
        SetWalkAnimation(false, false);
        if (config.archetype == EnemyArchetype.Summoner) summoner?.TrySummon();

        combat?.TryAttack(target, config.archetype == EnemyArchetype.Poison);
    }

    private void ApplyConfiguration()
    {
        if (config == null) return;
        health?.Configure(config);
        combat?.Configure(config);
        summoner?.Configure(config);
        movement?.SetSpeed(config.wanderSpeed);
    }

    private void Wander()
    {
        if (currentState == EnemyState.Attack && combat != null && combat.IsAttacking) return;
        if (Time.time >= nextWanderTime)
        {
            nextWanderTime = Time.time + Random.Range(1.5f, 3.5f);
            ChangeState(EnemyState.Wander);
            movement?.TryWander(3f, config.wanderSpeed, config.repathInterval);
        }
        if (movement == null || movement.IsStopped)
        {
            ChangeState(EnemyState.Idle);
            SetWalkAnimation(false, false);
        }
        else SetWalkAnimation(true, false);
    }

    private IEnumerator DashAttack()
    {
        isDashing = true;
        float cooldown = config.specialCooldown;
        if (target == null) { isDashing = false; dashRoutine = null; yield break; }

        movement?.Stop();
        SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>();
        Color originalColor = visual != null ? visual.color : Color.white;
        if (visual != null) visual.color = new Color(1f, 0.35f, 0.22f);
        ChargeTelegraph?.Invoke(this, target);
        yield return new WaitForSeconds(Mathf.Min(config.attackWindup, 0.45f));
        if (health == null || health.IsDead || target == null || target.currentHealth <= 0)
        {
            if (visual != null) visual.color = originalColor;
            isDashing = false;
            dashRoutine = null;
            yield break;
        }
        if (visual != null) visual.color = originalColor;
        movement?.SetDestination(target.transform.position, config.dashSpeed, 0.05f, true);
        if (animator != null && HasTrigger(animator, "Special")) animator.SetTrigger("Special");
        yield return new WaitForSeconds(config.dashDuration);
        isDashing = false;
        if (health != null && !health.IsDead && target != null && target.currentHealth > 0)
        {
            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist <= config.attackRange + 0.5f) combat?.TryAttack(target, true);
        }
        yield return new WaitForSeconds(cooldown);
        dashRoutine = null;
    }

    private void OnDamaged(int amount)
    {
        if (health == null || health.IsDead) return;
        combat?.CancelAttack();
        hurtUntil = Time.time + 0.2f;
        ChangeState(EnemyState.Hurt);
        movement?.Stop();
    }

    private void OnDied(EnemyHealth deadEnemy)
    {
        ChangeState(EnemyState.Dead);
    }

    private void OnAttackFinished()
    {
        if (currentState == EnemyState.Attack && (health == null || !health.IsDead))
            ChangeState(EnemyState.Idle);
    }

    private void ChangeState(EnemyState nextState)
    {
        if (currentState == EnemyState.Dead || currentState == nextState) return;
        currentState = nextState;
        StateChanged?.Invoke(currentState);
    }

    private void SetWalkAnimation(bool walking, bool chasing)
    {
        if (animator == null) return;
        if (HasFloat(animator, "WalkSpeed")) animator.SetFloat("WalkSpeed", walking ? (chasing ? 101f : 1f) : 0f);
        if (HasFloat(animator, "Speed")) animator.SetFloat("Speed", walking ? 1f : 0f);
    }

    private static bool HasFloat(Animator targetAnimator, string name)
    {
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Float) return true;
        return false;
    }

    private static bool HasTrigger(Animator targetAnimator, string name)
    {
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }
}
