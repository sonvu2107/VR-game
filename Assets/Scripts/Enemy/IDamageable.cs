// Giao diện nhận sát thương chung cho quái và boss; người tấn công không cần
// biết component máu cụ thể nào được gắn vào đối tượng.
using UnityEngine;

// Giao diện chung để đòn đánh tác động lên cả quái mới và boss mà không phụ
// thuộc vào lớp MonoBehaviour cụ thể.
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
