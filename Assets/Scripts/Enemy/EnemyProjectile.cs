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
        // Kiểm tra trúng người chơi theo đường bay raycast để chống đạn xuyên người giữa các frame
        if (playerLayers.value != 0)
        {
            RaycastHit2D playerHit = Physics2D.Raycast(transform.position, direction, step, playerLayers);
            if (playerHit.collider != null)
            {
                HealthController target = playerHit.collider.GetComponentInParent<HealthController>();
                if (target != null && target.currentHealth > 0)
                {
                    hit = true;
                    target.TakeDamage(damage);
                    CombatHitVfx.Spawn(playerHit.point, impactKind,
                        impactKind == CombatImpactKind.Boss ? 1.3f : 0.8f);
                    Destroy(gameObject);
                    return;
                }
            }
        }

        RaycastHit2D wall = Physics2D.Raycast(transform.position, direction, step, wallLayers);
        if (wall.collider != null && !wall.collider.isTrigger)
        {
            Destroy(gameObject);
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
                hit = true;
                target.TakeDamage(damage);
                CombatHitVfx.Spawn(other.bounds.center, impactKind,
                    impactKind == CombatImpactKind.Boss ? 1.3f : 0.8f);
                Destroy(gameObject);
            }
        }
        else if (wallLayers.value != 0 && (wallLayers.value & bit) != 0 && !other.isTrigger)
        {
            hit = true;
            Destroy(gameObject);
        }
    }
}
