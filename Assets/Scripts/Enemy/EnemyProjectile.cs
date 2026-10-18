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
    private float blastRadius;
    public void SetBlastRadius(float radius) => blastRadius = Mathf.Max(0f, radius);

    private void ResolveImpact(Vector3 position, HealthController target = null)
    {
        if (hit) return;
        hit = true;
        if (blastRadius > 0f) EnemyBlast.Apply(position, blastRadius, damage, playerLayers, wallLayers);
        else if (target != null)
        {
            target.TakeDamage(damage);
            CombatHitVfx.Spawn(position, impactKind, impactKind == CombatImpactKind.Boss ? 1.3f : 0.8f);
        }
        Destroy(gameObject);
    }

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
            if (!hit && blastRadius > 0f) ResolveImpact(transform.position); else Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        // Kiểm tra trúng người chơi theo đường bay raycast để chống đạn xuyên người giữa các frame
        if (playerLayers.value != 0)
        {
            RaycastHit2D playerHit = Physics2D.Raycast(transform.position, direction, step, playerLayers);
            if (playerHit.collider != null)
            {
                HealthController target = playerHit.collider.GetComponentInParent<HealthController>();
                if (target != null && target.currentHealth > 0)
                {
                    ResolveImpact(playerHit.point, target);
                    return;
                }
            }
        }

        RaycastHit2D wall = Physics2D.Raycast(transform.position, direction, step, wallLayers);
        if (wall.collider != null && !wall.collider.isTrigger)
        {
            ResolveImpact(wall.point);
            return;
        }

        transform.position += (Vector3)(direction * step);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (hit || other == null || other.gameObject == owner ||
            other.transform.IsChildOf(owner != null ? owner.transform : transform)) return;

        int bit = 1 << other.gameObject.layer;
        if ((playerLayers.value & bit) != 0)
        {
            HealthController target = other.GetComponentInParent<HealthController>();
            if (target != null && target.currentHealth > 0)
            {
                ResolveImpact(other.bounds.center, target);
            }
        }
        else if (wallLayers.value != 0 && (wallLayers.value & bit) != 0 && !other.isTrigger)
        {
            ResolveImpact(transform.position);
        }
    }
}
