using UnityEngine;

/// <summary>
/// Attach to enemy projectiles. Deals damage to the player on contact
/// and is destroyed on hitting walls or the player.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [HideInInspector] public int damage = 1;
    [HideInInspector] public float lifetime = 4f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Hit player
        HealthController hc = other.GetComponent<HealthController>();
        if (hc != null)
        {
            hc.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Hit wall (anything that isn't on the Enemy layer)
        if (other.GetComponent<Enemy>() == null &&
            other.GetComponent<SmartEnemy>() == null &&
            other.GetComponent<RangedEnemy>() == null &&
            other.GetComponent<EnemyProjectile>() == null)
        {
            Destroy(gameObject);
        }
    }
}
