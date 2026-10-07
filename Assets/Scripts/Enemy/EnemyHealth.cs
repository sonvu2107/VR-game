using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public enum DeathKind { Enemy, MiniBoss, Boss }

    [SerializeField] private EnemyConfigSO config;
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D[] collidersToDisableOnDeath;
    [SerializeField] private DeathKind deathKind = DeathKind.Enemy;
    [SerializeField] private bool countsForLegacyCounter = true;

    private int currentHealth;
    private bool isDead;
    private bool deathRaised;
    private bool hasRuntimeConfig;
    private bool isInvulnerable;

    public event Action<EnemyHealth> Died;
    public event Action<int> Damaged;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => config != null ? config.maxHealth : 1;
    public float HealthRatio => MaxHealth > 0 ? (float)currentHealth / MaxHealth : 0f;
    public EnemyConfigSO Config => config;
    public bool CountsForLegacyCounter => countsForLegacyCounter;

    public void SetCountsForLegacyCounter(bool value)
    {
        countsForLegacyCounter = value;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
            collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>();
        currentHealth = MaxHealth;
    }

    public void Configure(EnemyConfigSO enemyConfig)
    {
        if (enemyConfig == null) return;
        config = enemyConfig;
        if (!hasRuntimeConfig)
        {
            currentHealth = config.maxHealth;
            hasRuntimeConfig = true;
        }
        else currentHealth = Mathf.Clamp(currentHealth, 1, config.maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (isDead || isInvulnerable || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth == 0)
        {
            Die();
            return;
        }

        Damaged?.Invoke(amount);

        if (animator != null && HasParameter(animator, "Hurt", AnimatorControllerParameterType.Trigger))
            animator.SetTrigger("Hurt");
    }

    public void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
    }

    public void ApplyReplicatedState(int replicatedHealth, bool replicatedDead)
    {
        currentHealth = Mathf.Clamp(replicatedHealth, 0, MaxHealth);
        if (!replicatedDead || isDead) return;
        isDead = true;
        deathRaised = true;
        EnemyBrain brain = GetComponent<EnemyBrain>();
        if (brain != null) brain.enabled = false;
        EnemyMovement movement = GetComponent<EnemyMovement>();
        if (movement != null) movement.StopImmediately();
        EnemyCombat combat = GetComponent<EnemyCombat>();
        if (combat != null) combat.CancelAttack();
        if (collidersToDisableOnDeath != null)
            foreach (Collider2D hitCollider in collidersToDisableOnDeath)
                if (hitCollider != null) hitCollider.enabled = false;
        if (animator != null && HasParameter(animator, "isDead", AnimatorControllerParameterType.Bool))
            animator.SetBool("isDead", true);
    }

    public void Die()
    {
        if (deathRaised) return;

        isDead = true;
        deathRaised = true;

        EnemyBrain brain = GetComponent<EnemyBrain>();
        if (brain != null) brain.enabled = false;
        EnemyMovement movement = GetComponent<EnemyMovement>();
        if (movement != null) movement.StopImmediately();
        EnemyCombat combat = GetComponent<EnemyCombat>();
        if (combat != null) combat.CancelAttack();
        EnemySummoner summoner = GetComponent<EnemySummoner>();
        if (summoner != null) summoner.DespawnSummons();

        if (collidersToDisableOnDeath != null)
            foreach (Collider2D hitCollider in collidersToDisableOnDeath)
                if (hitCollider != null) hitCollider.enabled = false;

        if (animator != null)
        {
            if (HasParameter(animator, "isDead", AnimatorControllerParameterType.Bool))
                animator.SetBool("isDead", true);
            else if (HasParameter(animator, "Dead", AnimatorControllerParameterType.Trigger))
                animator.SetTrigger("Dead");
        }

        Died?.Invoke(this);
        switch (deathKind)
        {
            case DeathKind.Boss: EnemySignals.RaiseBossDefeated(this); break;
            case DeathKind.MiniBoss: EnemySignals.RaiseMiniBossDefeated(this); break;
            default: EnemySignals.RaiseEnemyDied(this); break;
        }
    }

    private static bool HasParameter(Animator target, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.name == parameterName && parameter.type == type) return true;
        return false;
    }
}
