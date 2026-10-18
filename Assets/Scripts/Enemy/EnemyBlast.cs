using System.Collections.Generic;
using UnityEngine;

public static class EnemyBlast
{
    public static int Apply(Vector3 center, float radius, int damage, LayerMask targets, LayerMask walls)
    {
        if (LanLaunch.Mode == LanMode.Client || radius <= 0f || damage <= 0) return 0;
        var hit = new HashSet<HealthController>();
        int dealt = 0;
        foreach (var collider in Physics2D.OverlapCircleAll(center, radius, targets))
        {
            var player = collider.GetComponentInParent<HealthController>();
            if (player == null || player.currentHealth <= 0 || !hit.Add(player)) continue;
            if (Physics2D.Linecast(center, collider.bounds.center, walls).collider != null) continue;
            int before = player.currentHealth;
            player.TakeDamage(damage);
            dealt += Mathf.Max(0, before - player.currentHealth);
        }
        CombatHitVfx.Spawn(center, CombatImpactKind.EnemyMelee, Mathf.Max(1f, radius));
        return dealt;
    }
}
