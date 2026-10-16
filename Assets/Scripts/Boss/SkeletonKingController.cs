// Điều khiển ba giai đoạn của Skeleton King theo tỷ lệ máu; cảnh báo trước
// đòn mạnh và chỉ phát tín hiệu thắng sau khi EnemyHealth xác nhận đã chết.
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonKingController : MonoBehaviour
{
    public void SetVisualRenderer(SpriteRenderer renderer)
    {
        if (renderer != null) spriteRenderer = renderer;
    }
    [SerializeField] private BossPhaseConfigSO phaseConfig;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemySummoner summoner;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int phaseIndex;

    private HealthController target;
    private float nextTargetScan;
    private float nextAttackTime;
    private float nextHazardTime;
    private Coroutine phaseRoutine;
    private bool isTransitioning;

    public event Action<int> PhaseChanged;
    public int CurrentPhase => phaseIndex + 1;
    public int CurrentHealth => health != null ? health.CurrentHealth : 0;
    public int MaxHealth => health != null ? health.MaxHealth : 1;
    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (movement == null) movement = GetComponent<EnemyMovement>();
        if (combat == null) combat = GetComponent<EnemyCombat>();
        if (summoner == null) summoner = GetComponent<EnemySummoner>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        if (phaseRoutine != null) StopCoroutine(phaseRoutine);
    }

    private void Start()
    {
        if (phaseConfig == null || phaseConfig.phases == null || phaseConfig.phases.Length == 0)
        {
            Debug.LogError("Skeleton King requires a BossPhaseConfigSO with at least one phase.", this);
            enabled = false;
            return;
        }

        phaseIndex = 0;
        ApplyPhaseSettings();
        SetupBossVisuals();
        PhaseChanged?.Invoke(CurrentPhase);
        EnemySignals.RaiseBossPhaseChanged(health, CurrentPhase);
    }

    private void SetupBossVisuals()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        Boss3DVisual visual3D = GetComponent<Boss3DVisual>();
        if (visual3D == null)
            visual3D = gameObject.AddComponent<Boss3DVisual>();

        visual3D.Setup(Boss3DVisual.BossVisualKind.SkeletonKing, new Color(0.95f, 0.22f, 0.28f, 0.88f));
    }

    private void Update()
    {
        if (health == null || health.IsDead || isTransitioning) return;
        if (Time.time >= nextTargetScan)
        {
            target = EnemyTargeting.FindClosestLivingPlayer(transform.position);
            nextTargetScan = Time.time + 0.3f;
        }
        if (target == null || target.currentHealth <= 0)
        {
            if (animator != null && HasParameter(animator, "WalkSpeed", AnimatorControllerParameterType.Float))
                animator.SetFloat("WalkSpeed", 0f);
            return;
        }

        BossPhaseDefinition phase = phaseConfig.phases[phaseIndex];
        float distance = Vector2.Distance(transform.position, target.transform.position);
        movement?.FaceTarget(target.transform.position);

        bool isMoving = distance > 1.35f;
        if (animator != null && HasParameter(animator, "WalkSpeed", AnimatorControllerParameterType.Float))
            animator.SetFloat("WalkSpeed", isMoving ? 101f : 0f);

        if (isMoving)
        {
            movement?.SetDestination(target.transform.position, phase.moveSpeed, 0.15f);
            if (phase.summonSkeletons) summoner?.TrySummon();
            return;
        }

        movement?.Stop();
        if (Time.time < nextAttackTime || (combat != null && combat.IsAttacking)) return;
        nextAttackTime = Time.time + phase.attackCooldown;

        if (animator != null && HasParameter(animator, "Attack", AnimatorControllerParameterType.Trigger))
            animator.SetTrigger("Attack");

        // Telegraphs are explicit events so UI/VFX/audio can react without owning boss logic.
        BossAttackTelegraph?.Invoke(this, phaseIndex + 1);
        if (phase.fireSwordWaves)
        {
            Vector2 aim = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            combat?.FireProjectileDirection(aim, phaseConfig.waveSpeed);
            combat?.FireProjectileDirection(Rotate(aim, 18f), phaseConfig.waveSpeed);
            combat?.FireProjectileDirection(Rotate(aim, -18f), phaseConfig.waveSpeed);
        }
        else combat?.TryAttack(target);

        if (phase.createHazards && Time.time >= nextHazardTime)
        {
            nextHazardTime = Time.time + phaseConfig.hazardCooldown;
            if (combat != null) combat.SpawnPoison(target.transform.position, 1.8f);
        }

        if (phase.repositionAfterAttack)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            movement?.TrySetNearbyDestination(transform.position + (Vector3)(away * 2.5f), 3f, phase.moveSpeed, 0.1f);
        }
        if (phase.summonSkeletons) summoner?.TrySummon();
    }

    public static event Action<SkeletonKingController, int> BossAttackTelegraph;

    private void OnDamaged(int amount)
    {
        if (health == null || phaseConfig == null || phaseConfig.phases == null ||
            phaseIndex + 1 >= phaseConfig.phases.Length || isTransitioning) return;
        if (health.HealthRatio <= phaseConfig.phases[phaseIndex + 1].healthThreshold)
            phaseRoutine = StartCoroutine(TransitionToNextPhase());
    }

    private IEnumerator TransitionToNextPhase()
    {
        isTransitioning = true;
        health.SetInvulnerable(true);
        combat?.CancelAttack();
        movement?.Stop();
        if (animator != null && HasTrigger(animator, "PhaseChange")) animator.SetTrigger("PhaseChange");

        yield return new WaitForSeconds(phaseConfig.transitionDuration);
        if (health == null || health.IsDead)
        {
            phaseRoutine = null;
            yield break;
        }

        phaseIndex = Mathf.Min(phaseIndex + 1, phaseConfig.phases.Length - 1);
        ApplyPhaseSettings();
        PhaseChanged?.Invoke(CurrentPhase);
        EnemySignals.RaiseBossPhaseChanged(health, CurrentPhase);
        health.SetInvulnerable(false);
        isTransitioning = false;
        phaseRoutine = null;

        // If one hit crossed more than one threshold, progress sequentially after this transition.
        if (phaseIndex + 1 < phaseConfig.phases.Length &&
            health.HealthRatio <= phaseConfig.phases[phaseIndex + 1].healthThreshold)
            phaseRoutine = StartCoroutine(TransitionToNextPhase());
    }

    private void ApplyPhaseSettings()
    {
        if (phaseIndex >= 0 && phaseIndex < phaseConfig.phases.Length)
            movement?.SetSpeed(phaseConfig.phases[phaseIndex].moveSpeed);
        if (spriteRenderer != null)
        {
            // Giữ nguyên hình ảnh vương giả, tăng sắc độ hào quang theo phase
            spriteRenderer.color = phaseIndex == 0 ? Color.white :
                (phaseIndex == 1 ? new Color(1f, 0.78f, 0.78f) : new Color(1f, 0.5f, 0.5f));
        }
        GetComponent<Boss3DVisual>()?.SetPhaseEnrage(CurrentPhase);
    }

    private void OnAttackFinished()
    {
        if (phaseConfig != null && health != null && !health.IsDead && !isTransitioning &&
            target != null && target.currentHealth > 0)
            movement?.Resume(phaseConfig.phases[phaseIndex].moveSpeed);
    }

    private void OnDied(EnemyHealth boss)
    {
        if (phaseRoutine != null) StopCoroutine(phaseRoutine);
        isTransitioning = false;
        health.SetInvulnerable(false);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
    }

    private static bool HasTrigger(Animator targetAnimator, string name)
    {
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    private static bool HasParameter(Animator target, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.name == parameterName && parameter.type == type) return true;
        return false;
    }
}
