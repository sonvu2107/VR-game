// Điều phối đòn đánh theo windup/recovery; mỗi đòn chỉ gây sát thương một lần.
// Đạn cung thủ dùng 4 khung hình mũi tên trong Resources/Enemy/Arrows.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private EnemyConfigSO config;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private Animator animator;
    [SerializeField] private LayerMask playerLayers;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private Transform attackPoint;

    private Coroutine attackRoutine;
    private HealthController currentTarget;
    private readonly List<GameObject> ownedEffects = new List<GameObject>();
    private bool hitResolved;
    private float nextAttackTime;
    private bool isAttacking;

    public event Action AttackFinished;
    public static event Action<EnemyCombat, HealthController> AttackTelegraph;
    public bool IsAttacking => isAttacking;
    public float NextAttackTime => nextAttackTime;

    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (movement == null) movement = GetComponent<EnemyMovement>();
        if (animator == null) animator = GetComponent<Animator>();
        if (playerLayers.value == 0) playerLayers = LayerMask.GetMask("Player");
        if (wallLayers.value == 0) wallLayers = ResolveWallLayers();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (health != null) health.Died += OnOwnerDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnOwnerDied;
        CancelAttack();
        CleanupOwnedEffects();
    }

    private void OnOwnerDied(EnemyHealth deadOwner)
    {
        CleanupOwnedEffects();
        if (config != null && config.archetype == EnemyArchetype.Poison)
            SpawnPoison(transform.position);
        CancelAttack();
    }

    public void CleanupOwnedEffects()
    {
        foreach (GameObject effect in ownedEffects)
            if (effect != null) Destroy(effect);
        ownedEffects.Clear();
    }

    public void Configure(EnemyConfigSO enemyConfig)
    {
        config = enemyConfig;
        if (health != null) health.Configure(enemyConfig);
    }

    public bool TryAttack(HealthController target, bool special = false)
    {
        if (target == null || target.currentHealth <= 0 || health == null || health.IsDead || isAttacking)
            return false;
        if (Time.time < nextAttackTime) return false;

        currentTarget = target;
        hitResolved = false;
        isAttacking = true;
        AttackTelegraph?.Invoke(this, target);
        float cooldown = config == null ? 1.5f : (special ? config.specialCooldown : config.attackCooldown);
        nextAttackTime = Time.time + Mathf.Max(0.1f, cooldown);
        if (movement != null) movement.Stop();

        if (animator != null)
        {
            string trigger = special && HasTrigger(animator, "Special") ? "Special" : "Attack";
            if (HasTrigger(animator, trigger)) animator.SetTrigger(trigger);
        }
        attackRoutine = StartCoroutine(ResolveAttack(special));
        return true;
    }

    private IEnumerator ResolveAttack(bool special)
    {
        yield return new WaitForSeconds(config != null ? config.attackWindup : 0.35f);
        if (!hitResolved && health != null && !health.IsDead)
        {
            if (config != null && (config.archetype == EnemyArchetype.Ranged || config.archetype == EnemyArchetype.Summoner))
                FireProjectile(currentTarget, config.attackDamage, config.projectileSpeed, config.projectileLifetime);
            else if (special && config != null && config.archetype == EnemyArchetype.Poison)
                SpawnPoison(currentTarget != null ? currentTarget.transform.position : transform.position);
            else
                ApplyMeleeHit();
        }

        yield return new WaitForSeconds(config != null ? config.attackRecovery : 0.5f);
        attackRoutine = null;
        isAttacking = false;
        currentTarget = null;
        AttackFinished?.Invoke();
    }

    // Compatible with the existing Skeleton attack Animation Event.
    public void HurtPlayer()
    {
        if (config != null && config.archetype is EnemyArchetype.Ranged or EnemyArchetype.Summoner)
            return; // Animation Event cũ không được gây thêm đòn cận chiến cho quái bắn.
        ApplyMeleeHit();
    }

    public void ApplyAttackDamage()
    {
        HurtPlayer();
    }

    private void ApplyMeleeHit()
    {
        if (hitResolved || health == null || health.IsDead || currentTarget == null || currentTarget.currentHealth <= 0)
            return;
        hitResolved = true;

        Vector2 direction = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        Vector2 center = attackPoint != null
            ? (Vector2)attackPoint.position
            : (Vector2)transform.position + direction * (config != null ? config.attackRange * 0.65f : 0.5f);
        float radius = config != null ? config.attackRange : 0.5f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, playerLayers);
        HashSet<HealthController> damagedPlayers = new HashSet<HealthController>();
        foreach (Collider2D hit in hits)
        {
            HealthController player = hit.GetComponentInParent<HealthController>();
            if (player != null && player.currentHealth > 0 && damagedPlayers.Add(player))
            {
                player.TakeDamage(config != null ? config.attackDamage : 1);
                CombatHitVfx.Spawn(player.transform.position, GetImpactKind(),
                    GetImpactKind() == CombatImpactKind.Boss ? 1.3f : 0.8f);
            }
        }
    }

    public void FireProjectile(HealthController target, int damage, float speed, float lifetime)
    {
        if (target == null || health == null || health.IsDead) return;
        FireProjectileDirection(((Vector2)target.transform.position - (Vector2)transform.position).normalized, damage, speed, lifetime);
    }

    public void FireProjectileDirection(Vector2 direction, int damage, float speed, float lifetime)
    {
        if (health == null || health.IsDead) return;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        // Projectile tự xử lý va chạm tường khi bay. Quét trước cả detectionRange
        // sẽ chặn bắn nếu tường ở *sau* người chơi.

        GameObject projectileObject;
        bool needsDefaultVisual = false;
        if (config != null && config.projectilePrefab != null)
            projectileObject = Instantiate(config.projectilePrefab, transform.position, Quaternion.identity);
        else
        {
            needsDefaultVisual = true;
            projectileObject = new GameObject("EnemyProjectile");
            projectileObject.transform.position = transform.position;
            CircleCollider2D circle = projectileObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        if (needsDefaultVisual)
        {
            SpriteRenderer visual = projectileObject.AddComponent<SpriteRenderer>();
            bool isArrow = config != null && config.archetype == EnemyArchetype.Ranged;
            Sprite[] arrowFrames = isArrow ? EnemyArrowAnimation.LoadFrames() : null;
            visual.sprite = arrowFrames != null ? arrowFrames[0] : EnemyEffectSpriteFactory.Disc;
            visual.sortingOrder = 2;
            visual.color = isArrow ? Color.white : config != null && config.archetype == EnemyArchetype.Summoner
                ? new Color(0.78f, 0.36f, 1f)
                : config != null && config.archetype == EnemyArchetype.MiniBoss
                    ? new Color(0.35f, 1f, 0.72f)
                    : new Color(1f, 0.78f, 0.22f);
            projectileObject.transform.rotation = Quaternion.FromToRotation(
                isArrow && arrowFrames != null ? Vector3.up : Vector3.right, direction);
            projectileObject.transform.localScale = isArrow && arrowFrames != null
                ? Vector3.one : Vector3.one * 0.24f;
            if (arrowFrames != null)
                projectileObject.AddComponent<EnemyArrowAnimation>().SetFrames(arrowFrames);
        }

        EnemyProjectile projectile = projectileObject.GetComponent<EnemyProjectile>();
        if (projectile == null) projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, damage, speed, lifetime, playerLayers, wallLayers,
            gameObject, GetImpactKind());
        ownedEffects.RemoveAll(effect => effect == null);
        ownedEffects.Add(projectileObject);
    }

    public void FireProjectileDirection(Vector2 direction, float speed)
    {
        FireProjectileDirection(direction, config != null ? config.attackDamage : 1, speed,
            config != null ? config.projectileLifetime : 4f);
    }

    private CombatImpactKind GetImpactKind()
    {
        if (GetComponent<SkeletonKingController>() != null || GetComponent<MiniBossController>() != null)
            return CombatImpactKind.Boss;
        if (config == null) return CombatImpactKind.EnemyMelee;
        switch (config.archetype)
        {
            case EnemyArchetype.Ranged: return CombatImpactKind.Arrow;
            case EnemyArchetype.Summoner: return CombatImpactKind.Mage;
            case EnemyArchetype.Charger: return CombatImpactKind.Bat;
            case EnemyArchetype.MiniBoss: return CombatImpactKind.Boss;
            default: return CombatImpactKind.EnemyMelee;
        }
    }

    public void SpawnPoison(Vector3 position)
    {
        SpawnPoison(position, config != null ? config.specialRange : 1.5f);
    }

    public void SpawnPoison(Vector3 position, float radius)
    {
        if (health == null) return;
        GameObject area = new GameObject("PoisonArea");
        area.transform.position = new Vector3(position.x, position.y, transform.position.z);
        CircleCollider2D circle = area.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = Mathf.Max(0.2f, radius);
        PoisonArea poison = area.AddComponent<PoisonArea>();
        poison.Initialize(config != null ? config.attackDamage : 1,
            config != null ? config.poisonDuration : 4f,
            config != null ? config.poisonTickInterval : 1f,
            playerLayers);
        // Độc sau khi Slime chết phải tồn tại hết thời lượng của nó; không sở hữu
        // bởi EnemyCombat, vì CleanupOwnedEffects sẽ chạy khi quái bị hủy.
    }

    public void CancelAttack()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        isAttacking = false;
        currentTarget = null;
    }

    private static bool HasTrigger(Animator targetAnimator, string name)
    {
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    public static LayerMask ResolveWallLayers()
    {
        LayerMask mask = LayerMask.GetMask("Wall");
        return mask.value != 0 ? mask : LayerMask.GetMask("Default");
    }

    private void OnDrawGizmosSelected()
    {
        float range = config != null ? config.attackRange : 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint != null ? attackPoint.position : transform.position, range);
    }
}

/// <summary>Small runtime pixel-art shapes for attacks whose prefabs have no authored VFX.</summary>
public static class EnemyEffectSpriteFactory
{
    private static Sprite disc;

    public static Sprite Disc
    {
        get
        {
            if (disc != null) return disc;

            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "EnemyEffectDisc";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.47f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                byte alpha = distance <= 0.72f ? (byte)255 : distance <= 1f ? (byte)170 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            disc = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            disc.name = "EnemyEffectDisc";
            disc.hideFlags = HideFlags.HideAndDontSave;
            return disc;
        }
    }
}
