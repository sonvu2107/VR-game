// Đạn bay theo hướng khi Launch; raycast từng bước chống xuyên tường và
// trigger chỉ gây sát thương đúng một lần trước khi tự hủy.
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    private Vector2 direction;
    private int damage;
    private float speed;
    private float expiresAt;
    private LayerMask playerLayers;
    private LayerMask wallLayers;
    private GameObject owner;
    private bool hit;
    private CombatImpactKind impactKind;

    public void Launch(Vector2 travelDirection, int damageAmount, float travelSpeed, float lifetime,
        LayerMask targetLayers, LayerMask obstacleLayers, GameObject source,
        CombatImpactKind kind = CombatImpactKind.Arrow)
    {
        direction = travelDirection.normalized;
        damage = Mathf.Max(0, damageAmount);
        speed = Mathf.Max(0.1f, travelSpeed);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        playerLayers = targetLayers;
        wallLayers = obstacleLayers;
        owner = source;
        impactKind = kind;
        Collider2D projectileCollider = GetComponent<Collider2D>();
        projectileCollider.isTrigger = true;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Update()
    {
        if (hit || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        RaycastHit2D wall = Physics2D.Raycast(transform.position, direction, step, wallLayers);
        if (wall.collider != null)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)(direction * step);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hit || other.gameObject == owner || other.transform.IsChildOf(owner != null ? owner.transform : transform)) return;
        int bit = 1 << other.gameObject.layer;
        if ((playerLayers.value & bit) != 0)
        {
            HealthController target = other.GetComponentInParent<HealthController>();
            if (target != null && target.currentHealth > 0)
            {
                hit = true;
                target.TakeDamage(damage);
                CombatHitVfx.Spawn(other.bounds.center, impactKind,
                    impactKind == CombatImpactKind.Boss ? 1.3f : 0.8f);
                Destroy(gameObject);
            }
        }
        else if (wallLayers.value != 0 && (wallLayers.value & bit) != 0)
        {
            hit = true;
            Destroy(gameObject);
        }
    }
}
