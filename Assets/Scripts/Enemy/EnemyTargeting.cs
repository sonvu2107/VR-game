// Tìm người chơi còn sống gần nhất; trả null khi không có mục tiêu hợp lệ.
using UnityEngine;

public static class EnemyTargeting
{
    public static HealthController FindClosestLivingPlayer(Vector3 origin)
    {
        HealthController[] players = Object.FindObjectsOfType<HealthController>();
        HealthController closest = null;
        float closestSqrDistance = float.MaxValue;
        foreach (HealthController player in players)
        {
            if (player == null || player.currentHealth <= 0 || !player.gameObject.activeInHierarchy) continue;
            float sqrDistance = (player.transform.position - origin).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closest = player;
                closestSqrDistance = sqrDistance;
            }
        }
        return closest;
    }
}
