using UnityEngine;

// Compatibility bridge for older prefabs/scenes. New enemy prefabs use EnemyHealth + EnemyBrain.
// Keeping the class/GUID lets existing scene references migrate without a missing script.
public class Enemy : MonoBehaviour, IDamageable
{
    private EnemyHealth health;

    public bool IsDead => health == null || health.IsDead;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    public void TakeDamage(int amount)
    {
        if (health == null)
        {
            Debug.LogWarning("Legacy Enemy component has no EnemyHealth. Migrate this prefab to the Enemy/Boss authoring setup.", this);
            return;
        }
        health.TakeDamage(amount);
    }
}
