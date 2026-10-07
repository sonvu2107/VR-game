using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount);
}

public static class DamageableLookup
{
    public static IDamageable FindInParents(Component source)
    {
        if (source == null) return null;
        MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
            if (behaviour is IDamageable damageable) return damageable;
        return null;
    }
}
