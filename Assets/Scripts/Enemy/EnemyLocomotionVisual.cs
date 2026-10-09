using UnityEngine;

/// <summary>
/// Animates the static enemy artwork on a child pivot while NavMeshAgent keeps
/// control of the root. This adds a light stride, hover or wing flap without
/// moving the collider or changing the enemy's gameplay position.
/// </summary>
public sealed class EnemyLocomotionVisual : MonoBehaviour
{
    private EnemyMovement movement;
    private EnemyHealth health;
    private Transform visualPivot;
    private SpriteRenderer source;
    private SpriteRenderer display;
    private Vector3 basePosition;
    private bool bat;
    private bool hover;

    public SpriteRenderer Renderer => display;

    public void Initialize(EnemyMovement owner)
    {
        movement = owner;
        health = GetComponent<EnemyHealth>();
        source = GetComponent<SpriteRenderer>();
        if (source == null || display != null) return;

        GameObject pivotObject = new GameObject("Animated Enemy Art");
        pivotObject.transform.SetParent(transform, false);
        visualPivot = pivotObject.transform;
        basePosition = visualPivot.localPosition;
        display = pivotObject.AddComponent<SpriteRenderer>();
        display.sprite = source.sprite;
        display.color = source.color;
        display.flipX = source.flipX;
        display.sortingLayerID = source.sortingLayerID;
        display.sortingOrder = source.sortingOrder;
        display.sharedMaterial = source.sharedMaterial;
        source.enabled = false;

        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        bat = config != null && config.archetype == EnemyArchetype.Charger;
        hover = config != null && config.archetype == EnemyArchetype.Summoner;
        movement?.SetVisualRenderer(display);
        GetComponent<MiniBossController>()?.SetVisualRenderer(display);
        GetComponent<SkeletonKingController>()?.SetVisualRenderer(display);
    }

    private void Update()
    {
        if (visualPivot == null || movement == null || (health != null && health.IsDead)) return;
        float speed = movement.Velocity.magnitude;
        float activity = Mathf.Clamp01(speed / 1.2f);
        float phase = Time.time * (bat ? 18f : hover ? 7f : 11f);
        float stride = Mathf.Sin(phase);
        float lift = Mathf.Abs(stride);
        if (bat)
        {
            visualPivot.localPosition = basePosition + Vector3.up * (0.018f + 0.025f * stride);
            visualPivot.localScale = new Vector3(1f + 0.07f * stride, 1f - 0.04f * stride, 1f);
            visualPivot.localRotation = Quaternion.Euler(0f, 0f, 2f * stride);
        }
        else if (hover)
        {
            visualPivot.localPosition = basePosition + Vector3.up * (0.015f + 0.014f * stride);
            visualPivot.localRotation = Quaternion.Euler(0f, 0f, 1.5f * stride);
        }
        else
        {
            // Alternating body weight and a slight lift makes each stride
            // readable, while the feet return to their original resting pose.
            visualPivot.localPosition = basePosition + new Vector3(
                0.008f * stride * activity, 0.022f * lift * activity, 0f);
            visualPivot.localRotation = Quaternion.Euler(0f, 0f, 2.8f * stride * activity);
            visualPivot.localScale = new Vector3(1f + 0.025f * activity * stride,
                1f - 0.015f * activity * stride, 1f);
        }
    }
}
