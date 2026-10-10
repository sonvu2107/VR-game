// Điều khiển kỹ năng riêng và biến thể mini-boss; dùng chung EnemyHealth/EnemyCombat.
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MiniBossController : MonoBehaviour
{
    public void SetVisualRenderer(SpriteRenderer renderer)
    {
        if (renderer != null) spriteRenderer = renderer;
    }
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemySummoner summoner;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemyConfigSO config;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Range(0f, 1f)] [SerializeField] private float summonThreshold = 0.5f;
    private bool summonUsed;
    private float nextSpecialTime;
    private Coroutine specialRoutine;
    private Color normalColor = Color.white;

    public event Action MiniBossDefeated;
    public static event Action<MiniBossController, HealthController> SpecialTelegraph;
    public bool IsUsingSpecial { get; private set; }

    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (summoner == null) summoner = GetComponent<EnemySummoner>();
        if (combat == null) combat = GetComponent<EnemyCombat>();
        if (config == null) config = GetComponent<EnemyBrain>()?.Config;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) normalColor = spriteRenderer.color;
    }

    public void SetVariantColor(Color color)
    {
        normalColor = color;
        if (spriteRenderer != null) spriteRenderer.color = color;
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        if (specialRoutine != null) StopCoroutine(specialRoutine);
        specialRoutine = null;
        IsUsingSpecial = false;
        if (spriteRenderer != null) spriteRenderer.color = normalColor;
    }

    public bool TrySpecial(HealthController target)
    {
        if (target == null || target.currentHealth <= 0 || health == null || health.IsDead ||
            combat == null || IsUsingSpecial || Time.time < nextSpecialTime)
            return false;

        nextSpecialTime = Time.time + (config != null ? config.specialCooldown : 5f);
        specialRoutine = StartCoroutine(FireShardFan(target));
        return true;
    }

    private IEnumerator FireShardFan(HealthController target)
    {
        IsUsingSpecial = true;
        Vector2 aim = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        if (spriteRenderer != null) spriteRenderer.color = new Color(0.4f, 1f, 0.55f);
        SpecialTelegraph?.Invoke(this, target);
        yield return new WaitForSeconds(config != null ? config.attackWindup : 0.55f);

        if (health != null && !health.IsDead && target != null && target.currentHealth > 0)
        {
            float speed = config != null ? config.projectileSpeed : 6f;
            combat.FireProjectileDirection(aim, speed);
            combat.FireProjectileDirection(Rotate(aim, 22f), speed);
            combat.FireProjectileDirection(Rotate(aim, -22f), speed);
        }

        IsUsingSpecial = false;
        if (spriteRenderer != null) spriteRenderer.color = normalColor;
        specialRoutine = null;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
    }

    private void OnDamaged(int amount)
    {
        if (!summonUsed && health != null && health.HealthRatio <= summonThreshold)
            summonUsed = summoner != null && summoner.TrySummon();
    }

    private void OnDied(EnemyHealth dead)
    {
        MiniBossDefeated?.Invoke();
    }
}
