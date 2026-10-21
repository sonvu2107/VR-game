using UnityEngine;

/// <summary>Small solid collision point that makes the visible bone ring a real escape puzzle.</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class BoneCageBarrier : MonoBehaviour
{
    public static GameObject Create(Vector2 position, float radius, Transform parent)
    {
        GameObject barrier = new GameObject("Bone Cage Spike Barrier");
        barrier.layer = LayerMask.NameToLayer("Default");
        barrier.transform.SetParent(parent, true);
        barrier.transform.position = position;
        CircleCollider2D collider = barrier.AddComponent<CircleCollider2D>();
        collider.radius = radius;
        collider.isTrigger = false;
        barrier.AddComponent<BoneCageBarrier>();
        return barrier;
    }
}
