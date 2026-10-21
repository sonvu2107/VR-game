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
    [Header("Vong Xuong Phan Quyet (phase 2+)")]
    [Min(0.1f)] public float boneCageTelegraphDuration = 0.8f;
    [Min(0.1f)] public float boneCageImpactDelay = 1.1f;
    [Min(0.1f)] public float boneCageCooldownMin = 8f;
    [Min(0.1f)] public float boneCageCooldownMax = 10f;
    [Min(0.5f)] public float boneCageRadius = 3.25f;

    [Header("Xung Phong Bat Tu (phase 3)")]
    [Min(0.1f)] public float boneChargeTelegraphDuration = 0.65f;
    [Min(0.1f)] public float boneChargeDashDuration = 0.32f;
    [Min(0.1f)] public float boneChargeRecoveryDuration = 0.65f;
    [Min(1f)] public float boneChargeDistance = 5.5f;
    [Min(0.1f)] public float boneChargeCooldownMin = 9f;
    [Min(0.1f)] public float boneChargeCooldownMax = 11f;
    [Min(0.1f)] public float boneChargeImpactRadius = 1.05f;
    [Min(0.1f)] public float boneChargeCrackDuration = 2f;
    [Range(0.1f, 1f)] public float boneChargeSlowMultiplier = 0.55f;
}
