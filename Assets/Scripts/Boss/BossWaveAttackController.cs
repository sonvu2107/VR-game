using System.Collections;
using UnityEngine;

/// <summary>Cycles clear, telegraphed projectile patterns for campaign bosses.</summary>
[DisallowMultipleComponent]
public sealed class BossWaveAttackController : MonoBehaviour
{
    private EnemyHealth health;
    private EnemyCombat combat;
    private EnemyMovement movement;
    private bool isFinalBoss;
    private int pattern;
    private float nextWaveTime;
    private bool attacking;
    private BossAttackScheduler attackScheduler;

    public static void Attach(GameObject boss)
    {
        if (boss != null && boss.GetComponent<BossWaveAttackController>() == null)
            boss.AddComponent<BossWaveAttackController>();
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        movement = GetComponent<EnemyMovement>();
        attackScheduler = GetComponent<BossAttackScheduler>() ?? gameObject.AddComponent<BossAttackScheduler>();
        isFinalBoss = GetComponent<CampaignFinalBoss>() != null;
        // The level-10 boss is on the offensive immediately. Other bosses keep
        // their gentler opening delay and cadence.
        nextWaveTime = Time.time + (isFinalBoss ? 0.65f : 1.7f);
    }

    private void Update()
    {
        if (health == null || health.IsDead || combat == null || attacking ||
            LanLaunch.Mode == LanMode.Client || Time.time < nextWaveTime) return;
        HealthController target = EnemyTargeting.FindClosestLivingPlayer(transform.position);
        if (target == null || target.currentHealth <= 0) return;
        if (Vector2.Distance(transform.position, target.transform.position) > 11f) return;

        int kind = pattern % 3;
        float windup = kind == 0 ? 0.58f : 0.72f;
        if (attackScheduler == null || !attackScheduler.TryReserve("boss-wave", windup + 0.85f)) return;
        pattern++;
        // A final boss chains patterns nearly continuously, while the shared
        // scheduler still prevents untelegraphed overlap with its other moves.
        nextWaveTime = Time.time + (isFinalBoss ? 1.5f : 4.6f);
        StartCoroutine(DoWave(target, kind));
    }

    private IEnumerator DoWave(HealthController target, int kind)
    {
        attacking = true;
        movement?.Stop();
        Vector2 aim = target != null
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
            : Vector2.right;
        float windup = kind == 0 ? 0.58f : 0.72f;
        EnemyAttackCueVisual.Show(combat, target, windup, true);
        GetComponent<EnemyRuntimeSpriteAnimation>()?.TriggerAttack(windup + 0.4f);
        CombatHitVfx.Spawn(transform.position, CombatImpactKind.Boss, 0.9f);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossWave, 0.98f, 0.96f + kind * 0.04f);
        yield return new WaitForSeconds(windup);
        if (health == null || health.IsDead || target == null || target.currentHealth <= 0)
        {
            attacking = false;
            attackScheduler?.Release("boss-wave");
            yield break;
        }

        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        int damage = config != null ? Mathf.Max(1, config.attackDamage) : 1;
        float speed = config != null ? Mathf.Max(4.2f, config.projectileSpeed) : 6f;
        float life = config != null ? Mathf.Max(2f, config.projectileLifetime) : 3.5f;

        if (kind == 0)
        {
            Color color = new Color(1f, 0.28f, 0.12f);
            for (int i = -3; i <= 3; i++)
                FireWaveProjectile(Rotate(aim, i * 13f), damage, speed, life, color);
            CombatHitVfx.Spawn(target.transform.position, CombatImpactKind.Boss, 1.1f);
        }
        else if (kind == 1)
        {
            Color color = new Color(0.72f, 0.25f, 1f);
            for (int i = 0; i < 10; i++)
            {
                FireWaveProjectile(Rotate(aim, i * 36f), damage, speed * 0.88f, life, color);
                if (i == 4) yield return new WaitForSeconds(0.09f);
            }
        }
        else
        {
            Color[] colors = { new Color(0.18f, 0.95f, 1f), new Color(0.9f, 0.3f, 1f), new Color(1f, 0.78f, 0.15f) };
            for (int burst = 0; burst < 3; burst++)
            {
                Vector2 lead = target != null
                    ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
                    : aim;
                FireWaveProjectile(Rotate(lead, -8f), damage, speed * 1.12f, life, colors[burst]);
                FireWaveProjectile(lead, damage, speed * 1.12f, life, colors[burst]);
                FireWaveProjectile(Rotate(lead, 8f), damage, speed * 1.12f, life, colors[burst]);
                yield return new WaitForSeconds(0.16f);
            }
        }

        CombatHitVfx.Spawn(transform.position, CombatImpactKind.Boss, 0.7f);
        attacking = false;
        attackScheduler?.Release("boss-wave");
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }

    private void FireWaveProjectile(Vector2 direction, int damage, float speed, float life, Color color)
    {
        if (isFinalBoss)
            combat.FireBossProjectileDirection(direction, damage, speed, life);
        else
            combat.FireProjectileDirection(direction, damage, speed, life, color);
    }
}
