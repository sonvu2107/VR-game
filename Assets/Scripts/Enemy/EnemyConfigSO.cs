using UnityEngine;

public enum EnemyArchetype
{
    Melee,
    Ranged,
    Poison,
    Charger,
    Summoner,
    Elite,
    MiniBoss
}

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "The Labyrinth/Enemy Config")]
public class EnemyConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Enemy";
    public EnemyArchetype archetype = EnemyArchetype.Melee;

    [Header("Vitals")]
    [Min(1)] public int maxHealth = 100;
    [Min(0)] public int contactDamage = 1;

    [Header("Movement and detection")]
    [Min(0.1f)] public float chaseSpeed = 2.5f;
    [Min(0.1f)] public float wanderSpeed = 1f;
    [Min(0.1f)] public float detectionRange = 6f;
    [Min(0.1f)] public float attackRange = 1.2f;
    [Min(0.1f)] public float preferredRange = 1.2f;
    [Min(0.05f)] public float repathInterval = 0.25f;

    [Header("Attack")]
    [Min(0)] public int attackDamage = 1;
    [Min(0.1f)] public float attackCooldown = 1.5f;
    [Min(0f)] public float attackWindup = 0.35f;
    [Min(0.05f)] public float attackRecovery = 0.55f;
    [Min(0.1f)] public float projectileSpeed = 5f;
    [Min(0.1f)] public float projectileLifetime = 3f;

    [Header("Special behavior")]
    [Min(0.1f)] public float specialCooldown = 5f;
    [Min(0.1f)] public float specialRange = 3f;
    [Min(0.1f)] public float dashSpeed = 7f;
    [Min(0.1f)] public float dashDuration = 0.45f;
    [Min(0.1f)] public float poisonDuration = 4f;
    [Min(0.1f)] public float poisonTickInterval = 1f;
    [Min(1)] public int maxAliveSummons = 3;
    public GameObject summonPrefab;
    public GameObject projectilePrefab;

    [Header("Reward")]
    [Min(0)] public int scoreValue = 10;
}
