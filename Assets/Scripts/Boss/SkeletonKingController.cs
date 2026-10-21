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
    private BossPhaseConfigSO runtimePhaseConfig;

    private HealthController target;
    private float nextTargetScan;
    private float nextAttackTime;
    private float nextHazardTime;
    private float nextBoneCageTime = float.PositiveInfinity;
    private float nextBoneChargeTime = float.PositiveInfinity;
    private Coroutine phaseRoutine;
    private Coroutine boneCageRoutine;
    private Coroutine boneChargeRoutine;
    private BossAttackScheduler attackScheduler;
    private GameObject activeBoneCage;
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
        attackScheduler = GetComponent<BossAttackScheduler>();
        if (attackScheduler == null) attackScheduler = gameObject.AddComponent<BossAttackScheduler>();
        if (phaseConfig != null)
        {
            runtimePhaseConfig = Instantiate(phaseConfig);
            runtimePhaseConfig.name = "Runtime_" + phaseConfig.name;
            if (runtimePhaseConfig.phases != null)
                foreach (BossPhaseDefinition phase in runtimePhaseConfig.phases)
                    if (phase != null) phase.attackCooldown = Mathf.Clamp(phase.attackCooldown * 0.72f, 1.1f, 1.8f);
            phaseConfig = runtimePhaseConfig;
        }
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
        if (boneCageRoutine != null) StopCoroutine(boneCageRoutine);
        if (boneChargeRoutine != null) StopCoroutine(boneChargeRoutine);
        CleanupBoneCage();
    }

    private void OnDestroy()
    {
        if (runtimePhaseConfig == null) return;
        if (Application.isPlaying) Destroy(runtimePhaseConfig);
        else DestroyImmediate(runtimePhaseConfig);
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
        if (GameManager.isStoryPlaying) return;
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

        if (CurrentPhase >= 3 && boneChargeRoutine == null && Time.time >= nextBoneChargeTime &&
            attackScheduler != null && attackScheduler.TryReserve("bone-charge", BoneChargeLockDuration()))
        {
            nextBoneChargeTime = Time.time + UnityEngine.Random.Range(phaseConfig.boneChargeCooldownMin,
                Mathf.Max(phaseConfig.boneChargeCooldownMin, phaseConfig.boneChargeCooldownMax));
            boneChargeRoutine = StartCoroutine(CastBoneCharge(target));
            return;
        }

        if (CurrentPhase >= 2 && boneCageRoutine == null && boneChargeRoutine == null && Time.time >= nextBoneCageTime &&
            attackScheduler != null && attackScheduler.TryReserve("bone-cage", BoneCageLockDuration()))
        {
            nextBoneCageTime = Time.time + UnityEngine.Random.Range(phaseConfig.boneCageCooldownMin,
                Mathf.Max(phaseConfig.boneCageCooldownMin, phaseConfig.boneCageCooldownMax));
            boneCageRoutine = StartCoroutine(CastBoneCage(target));
            return;
        }

        // BossWaveAttackController reserves the same slot.  This stops normal sword,
        // poison and sword-wave casts while its projectile telegraph is on screen.
        if (attackScheduler != null && attackScheduler.IsLocked)
        {
            movement?.Stop();
            return;
        }

        // Boss lớn phải có thể đánh người chơi đang đứng ngoài collider của nó.
        float attackDistance = health.Config != null ? Mathf.Max(1.35f, health.Config.attackRange - .15f) : 1.35f;
        bool isMoving = distance > attackDistance;
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
        if (attackScheduler != null && !attackScheduler.TryReserve("skeleton-king", phase.fireSwordWaves ? 0.95f : 0.72f))
            return;
        nextAttackTime = Time.time + phase.attackCooldown;

        if (animator != null && HasParameter(animator, "Attack", AnimatorControllerParameterType.Trigger))
            animator.SetTrigger("Attack");

        // Telegraphs are explicit events so UI/VFX/audio can react without owning boss logic.
        BossAttackTelegraph?.Invoke(this, phaseIndex + 1);
        if (phase.fireSwordWaves)
        {
            EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossWave, 0.96f, 0.98f + phaseIndex * 0.03f);
            Vector2 aim = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            combat?.FireProjectileDirection(aim, phaseConfig.waveSpeed);
            combat?.FireProjectileDirection(Rotate(aim, 18f), phaseConfig.waveSpeed);
            combat?.FireProjectileDirection(Rotate(aim, -18f), phaseConfig.waveSpeed);
        }
        else combat?.TryAttack(target);

        if (phase.createHazards && Time.time >= nextHazardTime)
        {
            nextHazardTime = Time.time + phaseConfig.hazardCooldown;
            if (combat != null)
            {
                EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.Poison, 0.82f, 0.88f);
                combat.SpawnPoison(target.transform.position, 1.8f);
            }
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
        if (boneCageRoutine != null) StopCoroutine(boneCageRoutine);
        boneCageRoutine = null;
        if (boneChargeRoutine != null) StopCoroutine(boneChargeRoutine);
        boneChargeRoutine = null;
        CleanupBoneCage();
        attackScheduler?.Release("bone-cage");
        attackScheduler?.Release("bone-charge");
        movement?.Stop();
        if (animator != null && HasTrigger(animator, "PhaseChange")) animator.SetTrigger("PhaseChange");
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossTelegraph, 0.86f,
            0.76f + phaseIndex * 0.08f);

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
        if (CurrentPhase >= 2 && float.IsPositiveInfinity(nextBoneCageTime))
            nextBoneCageTime = Time.time + 3.5f;
        if (CurrentPhase >= 3 && float.IsPositiveInfinity(nextBoneChargeTime))
            nextBoneChargeTime = Time.time + 2.5f;
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
        if (boneCageRoutine != null) StopCoroutine(boneCageRoutine);
        if (boneChargeRoutine != null) StopCoroutine(boneChargeRoutine);
        CleanupBoneCage();
        isTransitioning = false;
        health.SetInvulnerable(false);
    }

    private float BoneCageLockDuration()
    {
        return phaseConfig.boneCageTelegraphDuration + phaseConfig.boneCageImpactDelay + 0.8f;
    }

    private float BoneChargeLockDuration()
    {
        return phaseConfig.boneChargeTelegraphDuration + phaseConfig.boneChargeDashDuration +
            phaseConfig.boneChargeRecoveryDuration + 0.1f;
    }

    private IEnumerator CastBoneCharge(HealthController castTarget)
    {
        movement?.Stop();
        Vector2 start = transform.position;
        Vector2 direction = castTarget != null
            ? ((Vector2)castTarget.transform.position - start).normalized : Vector2.right;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
        movement?.FaceTarget(start + direction);

        float routeLength = phaseConfig.boneChargeDistance;
        if (movement != null && movement.TryGetForwardChargeDestination(start, direction,
                routeLength, out Vector3 safeDestination))
        {
            Vector2 safeRoute = (Vector2)safeDestination - start;
            if (safeRoute.sqrMagnitude > 0.25f)
            {
                direction = safeRoute.normalized;
                routeLength = safeRoute.magnitude;
                movement.FaceTarget(safeDestination);
            }
        }
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject telegraph = BoneCageEffect.Spawn("Enemy/BossEffects/BoneCharge/Telegraph",
            start + direction * (routeLength * 0.5f), routeLength, phaseConfig.boneChargeTelegraphDuration, 12, false);
        telegraph.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossTelegraph, 0.84f, 0.82f);
        yield return new WaitForSeconds(phaseConfig.boneChargeTelegraphDuration);

        if (health == null || health.IsDead) { FinishBoneCharge(); yield break; }
        GameObject dash = BoneCageEffect.Spawn("Enemy/BossEffects/BoneCharge/Dash",
            start + direction * (routeLength * 0.5f), routeLength, phaseConfig.boneChargeDashDuration, 14, false);
        dash.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossDash, 0.98f, 0.82f);

        var struck = new System.Collections.Generic.HashSet<HealthController>();
        float elapsed = 0f;
        while (elapsed < phaseConfig.boneChargeDashDuration && health != null && !health.IsDead)
        {
            float step = Mathf.Min(Time.deltaTime, phaseConfig.boneChargeDashDuration - elapsed);
            movement?.MoveDirect((Vector3)(direction * (routeLength / phaseConfig.boneChargeDashDuration * step)));
            DealBoneChargeHits(struck, direction);
            elapsed += step;
            yield return null;
        }

        Vector3 impactPosition = transform.position;
        BoneCageEffect.Spawn("Enemy/BossEffects/BoneCharge/Impact", impactPosition,
            phaseConfig.boneChargeImpactRadius * 2.5f, 0.3f, 16, false);
        BoneChargeCrack.Spawn(impactPosition, phaseConfig.boneChargeImpactRadius,
            phaseConfig.boneChargeCrackDuration, phaseConfig.boneChargeSlowMultiplier);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BoneImpact, 0.96f, 0.82f);
        yield return new WaitForSeconds(phaseConfig.boneChargeRecoveryDuration);
        FinishBoneCharge();
    }

    private void DealBoneChargeHits(System.Collections.Generic.HashSet<HealthController> struck, Vector2 direction)
    {
        LayerMask playerLayers = LayerMask.GetMask("Player");
        if (playerLayers.value == 0) playerLayers = 1 << 3;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, phaseConfig.boneChargeImpactRadius, playerLayers);
        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        int damage = config != null ? Mathf.Max(1, config.attackDamage) : 1;
        foreach (Collider2D hit in hits)
        {
            HealthController player = hit != null ? hit.GetComponentInParent<HealthController>() : null;
            if (player == null || player.currentHealth <= 0 || !struck.Add(player)) continue;
            combat?.DealDamageToPlayer(player, damage);
            player.GetComponent<PlayerMovement>()?.ApplyExternalKnockback(direction, 0.65f);
            CombatHitVfx.Spawn(player.transform.position, CombatImpactKind.Boss, 1.35f);
        }
    }

    private void FinishBoneCharge()
    {
        attackScheduler?.Release("bone-charge");
        boneChargeRoutine = null;
        // Do not immediately chain a phase-3 wave and its retreat onto the dash.
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.75f);
        if (health != null && !health.IsDead && !isTransitioning)
            movement?.Resume(phaseConfig.phases[phaseIndex].moveSpeed);
    }

    private IEnumerator CastBoneCage(HealthController castTarget)
    {
        movement?.Stop();
        Vector3 center = castTarget != null ? castTarget.transform.position : transform.position;
        center.z = transform.position.z;
        float diameter = phaseConfig.boneCageRadius * 2f;

        activeBoneCage = new GameObject("Bone Cage - Vong Xuong Phan Quyet");
        activeBoneCage.transform.position = center;
        BoneCageEffect.Spawn("Enemy/BossEffects/BoneCage/Telegraph", center, diameter,
            phaseConfig.boneCageTelegraphDuration, 12, true).transform.SetParent(activeBoneCage.transform, true);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossTelegraph, 0.72f, 0.74f);
        yield return new WaitForSeconds(phaseConfig.boneCageTelegraphDuration);

        if (health == null || health.IsDead) { CleanupBoneCage(); yield break; }
        GameObject spikes = BoneCageEffect.Spawn("Enemy/BossEffects/BoneCage/Spikes", center, diameter,
            0.56f, 13, false);
        spikes.transform.SetParent(activeBoneCage.transform, true);
        CreateBoneCageBarriers(center, phaseConfig.boneCageRadius, activeBoneCage.transform);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BoneRise, 0.86f, 0.76f);

        yield return new WaitForSeconds(phaseConfig.boneCageImpactDelay);
        if (health != null && !health.IsDead)
        {
            if (animator != null && HasParameter(animator, "Attack", AnimatorControllerParameterType.Trigger))
                animator.SetTrigger("Attack");
            BoneCageEffect.Spawn("Enemy/BossEffects/BoneCage/Impact", center, diameter * 0.86f,
                0.3f, 16, false);
            DealBoneCageImpact(center, phaseConfig.boneCageRadius);
            EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BoneImpact, 0.98f, 0.78f);
        }

        yield return new WaitForSeconds(0.35f);
        CleanupBoneCage();
        attackScheduler?.Release("bone-cage");
        boneCageRoutine = null;
        if (health != null && !health.IsDead)
            movement?.Resume(phaseConfig.phases[phaseIndex].moveSpeed);
    }

    private void CreateBoneCageBarriers(Vector2 center, float radius, Transform parent)
    {
        // 12 intended spike slots, with three consecutive slots omitted as the visible escape gap.
        const int slots = 12;
        const int gapStart = 9; // lower-right quadrant in the effect's top-down orientation.
        for (int i = 0; i < slots; i++)
        {
            if (i == gapStart || i == (gapStart + 1) % slots || i == (gapStart + 2) % slots) continue;
            float angle = i * 360f / slots * Mathf.Deg2Rad;
            Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            BoneCageBarrier.Create(position, 0.27f, parent);
        }
    }

    private void DealBoneCageImpact(Vector2 center, float radius)
    {
        LayerMask playerLayers = LayerMask.GetMask("Player");
        if (playerLayers.value == 0) playerLayers = 1 << 3;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, playerLayers);
        var damaged = new System.Collections.Generic.HashSet<HealthController>();
        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        int damage = config != null ? Mathf.Max(1, config.attackDamage) : 1;
        foreach (Collider2D hit in hits)
        {
            HealthController player = hit != null ? hit.GetComponentInParent<HealthController>() : null;
            if (player == null || player.currentHealth <= 0 || !damaged.Add(player)) continue;
            combat?.DealDamageToPlayer(player, damage);
            CombatHitVfx.Spawn(player.transform.position, CombatImpactKind.Boss, 1.25f);
        }
    }

    private void CleanupBoneCage()
    {
        if (activeBoneCage != null) Destroy(activeBoneCage);
        activeBoneCage = null;
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
