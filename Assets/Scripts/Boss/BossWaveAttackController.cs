using System.Collections;
using UnityEngine;

/// <summary>Cycles clear, telegraphed projectile patterns for campaign bosses.</summary>
[DisallowMultipleComponent]
public sealed class BossWaveAttackController : MonoBehaviour
{
    private EnemyHealth health;
    private EnemyCombat combat;
    private EnemyMovement movement;
    private int pattern;
    private float nextWaveTime;
    private bool attacking;
    private Sprite[][] waveFrames;

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
        nextWaveTime = Time.time + 1.7f;
        waveFrames = new Sprite[3][];
        string[] names = { "InfernoBloom", "VoidHalo", "PrismBurst" };
        for (int family = 0; family < names.Length; family++)
        {
            waveFrames[family] = new Sprite[12];
            for (int frame = 0; frame < 12; frame++)
                waveFrames[family][frame] = Resources.Load<Sprite>(
                    $"Combat/Boss/{names[family]}_12f/Frame_{frame:00}");
        }
    }

    private void Update()
    {
        if (health == null || health.IsDead || combat == null || attacking ||
            LanLaunch.Mode == LanMode.Client || Time.time < nextWaveTime) return;
        HealthController target = EnemyTargeting.FindClosestLivingPlayer(transform.position);
        if (target == null || target.currentHealth <= 0) return;
        if (Vector2.Distance(transform.position, target.transform.position) > 11f) return;

        nextWaveTime = Time.time + 4.1f;
        StartCoroutine(DoWave(target, pattern++ % 5));
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
        StartCoroutine(PlayWaveEffect(kind % 3, transform.position, windup + 0.32f, 0.62f));
        CombatHitVfx.Spawn(transform.position, CombatImpactKind.Boss, 0.9f);
        EnemyCombatAudio.Play(gameObject, EnemyCombatAudio.Cue.BossWave, 0.98f, 0.96f + kind * 0.04f);
        yield return new WaitForSeconds(windup);
        if (health == null || health.IsDead || target == null || target.currentHealth <= 0)
        {
            attacking = false;
            yield break;
        }

        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        int damage = config != null ? Mathf.Max(1, config.attackDamage) : 1;
        float speed = config != null ? Mathf.Max(4.2f, config.projectileSpeed) : 6f;
        float life = config != null ? Mathf.Max(2f, config.projectileLifetime) : 3.5f;

        GameManager manager = FindObjectOfType<GameManager>();
        int level = manager != null ? manager.CurrentLevel : LanLaunch.StartingLevel;
        Color ember = level >= 10 ? new Color(1f, 0.24f, 0.32f) : new Color(1f, 0.4f, 0.12f);
        Color voidColor = level == 9 ? new Color(0.70f, 0.35f, 1f) : new Color(0.57f, 0.38f, 0.95f);
        if (kind == 0)
        {
            for (int i = -3; i <= 3; i++)
                combat.FireProjectileDirection(Rotate(aim, i * 13f), damage, speed, life, ember);
            CombatHitVfx.Spawn(target.transform.position, CombatImpactKind.Boss, 1.1f);
        }
        else if (kind == 1)
        {
            for (int i = 0; i < 10; i++)
            {
                combat.FireProjectileDirection(Rotate(aim, i * 36f), damage, speed * 0.88f, life, voidColor);
                if (i == 4) yield return new WaitForSeconds(0.09f);
            }
        }
        else if (kind == 2)
        {
            Color[] colors = { new Color(0.18f, 0.95f, 1f), new Color(0.9f, 0.3f, 1f), new Color(1f, 0.78f, 0.15f) };
            for (int burst = 0; burst < 3; burst++)
            {
                Vector2 lead = target != null
                    ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
                    : aim;
                combat.FireProjectileDirection(Rotate(lead, -8f), damage, speed * 1.12f, life, colors[burst]);
                combat.FireProjectileDirection(lead, damage, speed * 1.12f, life, colors[burst]);
                combat.FireProjectileDirection(Rotate(lead, 8f), damage, speed * 1.12f, life, colors[burst]);
                yield return new WaitForSeconds(0.16f);
            }
        }
        else if (kind == 3)
        {
            // Two opposing spiral arms open a rotating safe lane each pulse.
            for (int pulse = 0; pulse < 5; pulse++)
            {
                float angle = pulse * 18f;
                for (int arm = 0; arm < 2; arm++)
                    combat.FireProjectileDirection(Rotate(aim, angle + arm * 180f),
                        damage, speed * 1.03f, life, arm == 0 ? ember : voidColor);
                StartCoroutine(PlayWaveEffect(1, transform.position, 0.35f, 0.32f));
                yield return new WaitForSeconds(0.17f);
            }
        }
        else
        {
            // Three delayed mirrored volleys force a change of direction.
            for (int volley = 0; volley < 3; volley++)
            {
                Vector2 updatedAim = target != null
                    ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized : aim;
                float spread = 24f - volley * 7f;
                Color tint = volley == 1 ? voidColor : new Color(0.24f, 0.92f, 1f);
                for (int i = -2; i <= 2; i++)
                    combat.FireProjectileDirection(Rotate(updatedAim, i * spread),
                        damage, speed * (1f + volley * 0.08f), life, tint);
                StartCoroutine(PlayWaveEffect(2, transform.position, 0.36f, 0.4f));
                yield return new WaitForSeconds(0.24f);
            }
        }

        CombatHitVfx.Spawn(transform.position, CombatImpactKind.Boss, 0.7f);
        attacking = false;
    }

    private IEnumerator PlayWaveEffect(int family, Vector3 position, float duration, float scale)
    {
        if (waveFrames == null || family < 0 || family >= waveFrames.Length ||
            waveFrames[family] == null || waveFrames[family][0] == null) yield break;
        GameObject effect = new GameObject("Boss attack animation");
        effect.transform.position = position;
        effect.transform.localScale = Vector3.one * scale;
        SpriteRenderer sprite = effect.AddComponent<SpriteRenderer>();
        sprite.sortingOrder = 18;
        float elapsed = 0f;
        while (elapsed < duration && effect != null)
        {
            elapsed += Time.deltaTime;
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / duration * 12), 0, 11);
            sprite.sprite = waveFrames[family][index];
            yield return null;
        }
        if (effect != null) Destroy(effect);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }
}
