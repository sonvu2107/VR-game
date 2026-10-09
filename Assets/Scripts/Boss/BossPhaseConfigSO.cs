// Dữ liệu ngưỡng máu và đòn đánh của từng phase Skeleton King.
using System;
using UnityEngine;

[Serializable]
public class BossPhaseDefinition
{
    [Range(0f, 1f)] public float healthThreshold = 1f;
    [Min(0.1f)] public float moveSpeed = 2.5f;
    [Min(0.1f)] public float attackCooldown = 1.5f;
    public bool summonSkeletons;
    public bool createHazards;
    public bool fireSwordWaves;
    public bool repositionAfterAttack;
}

[CreateAssetMenu(fileName = "BossPhaseConfig", menuName = "The Labyrinth/Boss Phase Config")]
public class BossPhaseConfigSO : ScriptableObject
{
    public BossPhaseDefinition[] phases =
    {
        new BossPhaseDefinition { healthThreshold = 1f, moveSpeed = 2.3f, attackCooldown = 1.8f },
        new BossPhaseDefinition { healthThreshold = 0.65f, moveSpeed = 2.7f, attackCooldown = 1.45f, summonSkeletons = true, createHazards = true },
        new BossPhaseDefinition { healthThreshold = 0.30f, moveSpeed = 3.4f, attackCooldown = 1.1f, createHazards = true, fireSwordWaves = true, repositionAfterAttack = true }
    };
    [Min(0.1f)] public float transitionDuration = 1.25f;
    [Min(1)] public int maximumSummons = 4;
    [Min(0.1f)] public float hazardCooldown = 6f;
    [Min(0.1f)] public float waveSpeed = 6f;
}
