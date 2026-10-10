// Vùng độc tồn tại ngắn sau đòn đánh hoặc khi Slime chết. Chỉ gây sát thương
// sau một nhịp báo trước, tối đa một lần/người chơi trong mỗi vùng độc.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PoisonArea : MonoBehaviour
{
    private readonly Dictionary<HealthController, float> nextDamageByPlayer = new Dictionary<HealthController, float>();
    private int damage;
    private float duration;
    private float tickInterval;
    private LayerMask playerLayers;
    private float expiresAt;
    private float firstDamageAt;

    public void Initialize(int damagePerTick, float areaDuration, float interval, LayerMask targets)
    {
        damage = Mathf.Max(0, damagePerTick);
        duration = Mathf.Max(0.1f, areaDuration);
        tickInterval = Mathf.Max(0.1f, interval);
        playerLayers = targets;
        expiresAt = Time.time + duration;
        firstDamageAt = Time.time + tickInterval;
        Collider2D areaCollider = GetComponent<Collider2D>();
        areaCollider.isTrigger = true;
        CircleCollider2D circle = areaCollider as CircleCollider2D;
        if (circle != null)
        {
            GameObject visualObject = new GameObject("PoisonAreaVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localScale = Vector3.one * circle.radius * 2f;
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = EnemyEffectSpriteFactory.Disc;
            visual.color = new Color(0.35f, 1f, 0.12f, 0.42f);
            visual.sortingOrder = -1;
        }
    }

    private void Update()
    {
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (playerLayers.value != 0 && (playerLayers.value & (1 << other.gameObject.layer)) == 0) return;
        HealthController target = other.GetComponentInParent<HealthController>();
        if (target == null || target.currentHealth <= 0 || Time.time < firstDamageAt ||
            Time.time >= expiresAt) return;
        if (!nextDamageByPlayer.TryGetValue(target, out float nextTime) || Time.time >= nextTime)
        {
            target.TakeDamage(damage);
            nextDamageByPlayer[target] = Time.time + tickInterval;
        }
    }
}
