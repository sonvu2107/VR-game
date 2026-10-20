using System.Collections;
using UnityEngine;

/// <summary>
/// Hệ thống hoạt ảnh chuyển động 2D phong cách pixel-art.
/// Giữ thân hình đứng thẳng vững chãi, không lắc lư nghiêng ngả (theo yêu cầu người chơi),
/// tạo nhịp thở nhẹ nhàng khi đứng yên và nhịp nhún chân / đập cánh / bật nảy khi di chuyển.
/// Nếu đối tượng đã có Animator với WalkSpeed (như Skeleton, Skeleton King, Bone Sentinel),
/// hệ thống sẽ nhường quyền hoàn toàn cho Animator để chạy hoạt ảnh 2D khung hình đích thực.
/// </summary>
public sealed class EnemyLocomotionVisual : MonoBehaviour
{
    private EnemyMovement movement;
    private EnemyHealth health;
    private EnemyCombat combat;
    private Transform visualPivot;
    private Transform shadowTransform;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer source;
    private SpriteRenderer display;
    private Vector3 basePosition;
    private Vector3 baseScale = Vector3.one;

    private EnemyArchetype archetype = EnemyArchetype.Melee;
    private bool isSlime;
    private bool isBat;
    private bool isHover;

    private float walkPhase;
    private float hurtUntil;
    private Color originalDisplayColor = Color.white;

    public SpriteRenderer Renderer => display != null ? display : source;

    public void Initialize(EnemyMovement owner)
    {
        movement = owner;
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        // Nếu đối tượng là Boss hoặc MiniBoss, chuyển quyền hoàn toàn cho hệ thống Boss3DVisual chuyên sâu
        if (GetComponent<Boss3DVisual>() != null || GetComponent<SkeletonKingController>() != null || GetComponent<MiniBossController>() != null)
        {
            if (GetComponent<Boss3DVisual>() == null)
                gameObject.AddComponent<Boss3DVisual>();
            Destroy(this);
            return;
        }

        // Nếu đối tượng đã có Animator với WalkSpeed ĐANG BẬT (như Skeleton thường),
        // nhường quyền cho Animator chạy hoạt ảnh frame 2D gốc
        Animator anim = GetComponent<Animator>();
        if (anim != null && anim.enabled && anim.runtimeAnimatorController != null && HasParameter(anim, "WalkSpeed", AnimatorControllerParameterType.Float))
        {
            movement?.SetVisualRenderer(source);
            Destroy(this);
            return;
        }

        if (display != null) return;

        GameObject pivotObject = new GameObject("Animated Enemy Art");
        pivotObject.transform.SetParent(transform, false);
        visualPivot = pivotObject.transform;
        basePosition = visualPivot.localPosition;
        baseScale = visualPivot.localScale;

        display = pivotObject.AddComponent<SpriteRenderer>();
        display.sprite = source.sprite;
        display.color = source.color;
        originalDisplayColor = source.color;
        display.flipX = source.flipX;
        display.sortingLayerID = source.sortingLayerID;
        display.sortingOrder = Mathf.Max(2, source.sortingOrder);
        display.sharedMaterial = source.sharedMaterial;
        source.enabled = false;

        // Tạo bóng đổ 3D mềm mại dưới chân cho quái vật
        GameObject shadowObj = new GameObject("3D Ground Shadow");
        shadowObj.transform.SetParent(transform, false);
        shadowTransform = shadowObj.transform;
        shadowTransform.localPosition = new Vector3(0f, -0.22f, 0f);
        shadowTransform.localScale = new Vector3(0.85f, 0.36f, 1f);
        shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = Boss3DVisual.GetSoftShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.42f);
        shadowRenderer.sortingLayerID = display.sortingLayerID;
        shadowRenderer.sortingOrder = 0;

        EnemyConfigSO config = GetComponent<EnemyBrain>()?.Config;
        if (config != null)
        {
            archetype = config.archetype;
            isSlime = archetype == EnemyArchetype.Poison;
            isBat = archetype == EnemyArchetype.Charger;
            isHover = archetype == EnemyArchetype.Summoner || archetype == EnemyArchetype.MiniBoss;
        }

        if (health != null)
            health.Damaged += OnDamaged;

        movement?.SetVisualRenderer(display);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Damaged -= OnDamaged;
        if (shadowTransform != null)
            Destroy(shadowTransform.gameObject);
    }

    private void OnDamaged(int amount)
    {
        hurtUntil = Time.time + 0.15f;
        StartCoroutine(FlashHurt());
    }

    private IEnumerator FlashHurt()
    {
        if (display == null) yield break;
        display.color = new Color(1f, 0.25f, 0.25f, originalDisplayColor.a);
        yield return new WaitForSeconds(0.09f);
        if (display != null && (health == null || !health.IsDead))
            display.color = originalDisplayColor;
    }

    public void TriggerAttackLunge(Vector2 targetDirection)
    {
        // Có thể mở rộng nếu cần
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            if (shadowRenderer != null) shadowRenderer.enabled = false;
            return;
        }
        if (visualPivot == null) return;

        float speed = movement != null ? movement.Velocity.magnitude : 0f;
        bool isMoving = speed > 0.15f;

        // 1. Phản ứng chấn động nhẹ khi bị trúng đòn
        Vector3 hurtShake = Vector3.zero;
        if (Time.time < hurtUntil)
        {
            hurtShake = new Vector3(Mathf.Sin(Time.time * 50f) * 0.05f, 0f, 0f);
        }

        // 2. Phản ứng lao tới khi đánh
        Vector3 attackOffset = Vector3.zero;
        if (combat != null && combat.IsAttacking)
        {
            bool isFacingLeft = display != null && display.flipX;
            float flipSign = isFacingLeft ? -1f : 1f;
            float lungeCycle = Mathf.Sin(Time.time * 16f);
            attackOffset = new Vector3(flipSign * Mathf.Max(0f, lungeCycle) * 0.18f, 0f, 0f);
        }

        // Nghiêng nhẹ 3D theo hướng di chuyển (Banking tilt)
        float targetTilt = isMoving && movement != null ? -Mathf.Clamp(movement.Velocity.x * 1.5f, -1f, 1f) * 3.5f : 0f;
        visualPivot.localRotation = Quaternion.Euler(0f, 0f, targetTilt);

        if (isSlime)
        {
            UpdateSlimeAnimation(isMoving, speed, hurtShake, attackOffset);
        }
        else if (isBat)
        {
            UpdateBatAnimation(isMoving, speed, hurtShake, attackOffset);
        }
        else if (isHover)
        {
            UpdateHoverAnimation(isMoving, speed, hurtShake, attackOffset);
        }
        else
        {
            UpdateBipedWalkerAnimation(isMoving, speed, hurtShake, attackOffset);
        }
    }

    // Slime nhún nhảy đàn hồi theo phương thẳng đứng
    private void UpdateSlimeAnimation(bool isMoving, float speed, Vector3 hurtShake, Vector3 attackOffset)
    {
        if (isMoving)
        {
            walkPhase += Time.deltaTime * Mathf.Max(speed * 3.8f, 6f);
            float hopSin = Mathf.Sin(walkPhase);

            if (hopSin >= 0f)
            {
                float hopArc = hopSin * 0.22f;
                float stretchY = hopSin * 0.28f;
                visualPivot.localPosition = basePosition + new Vector3(0f, hopArc, 0f) + hurtShake + attackOffset;
                visualPivot.localScale = new Vector3(baseScale.x * (1f - stretchY * 0.4f), baseScale.y * (1f + stretchY), 1f);
            }
            else
            {
                float squash = Mathf.Abs(hopSin) * 0.22f;
                visualPivot.localPosition = basePosition + hurtShake + attackOffset;
                visualPivot.localScale = new Vector3(baseScale.x * (1f + squash * 0.5f), baseScale.y * (1f - squash), 1f);
            }
        }
        else
        {
            float breathe = Mathf.Sin(Time.time * 3.5f);
            visualPivot.localPosition = basePosition + new Vector3(0f, breathe * 0.015f, 0f) + hurtShake + attackOffset;
            visualPivot.localScale = new Vector3(baseScale.x * (1f + breathe * 0.07f), baseScale.y * (1f - breathe * 0.07f), 1f);
        }
    }

    // Dơi đập cánh liên tục và lơ lửng, không nghiêng lảo đảo
    private void UpdateBatAnimation(bool isMoving, float speed, Vector3 hurtShake, Vector3 attackOffset)
    {
        float flapSpeed = isMoving ? 22f : 18f;
        float flap = Mathf.Cos(Time.time * flapSpeed);
        float hoverBob = Mathf.Sin(Time.time * 8f) * 0.12f;

        visualPivot.localPosition = basePosition + new Vector3(0f, hoverBob, 0f) + hurtShake + attackOffset;

        float wingSpan = 1f + flap * 0.3f;
        float bodyHeight = 1f - flap * 0.15f;
        visualPivot.localScale = new Vector3(baseScale.x * wingSpan, baseScale.y * bodyHeight, 1f);
    }

    // Pháp sư lơ lửng nhẹ nhàng
    private void UpdateHoverAnimation(bool isMoving, float speed, Vector3 hurtShake, Vector3 attackOffset)
    {
        float hoverCycle = Mathf.Sin(Time.time * 3.5f);
        float hoverHeight = hoverCycle * 0.1f;

        visualPivot.localPosition = basePosition + new Vector3(0f, hoverHeight, 0f) + hurtShake + attackOffset;
        visualPivot.localScale = new Vector3(baseScale.x * (1f - hoverCycle * 0.03f), baseScale.y * (1f + hoverCycle * 0.04f), 1f);
    }

    // Quái người đi bộ: Nhún nảy chân bước nhẹ nhàng thẳng đứng, không nghiêng lắc lư
    private void UpdateBipedWalkerAnimation(bool isMoving, float speed, Vector3 hurtShake, Vector3 attackOffset)
    {
        if (isMoving)
        {
            walkPhase += Time.deltaTime * Mathf.Max(speed * 4f, 6.5f);
            float stepLift = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.09f;
            visualPivot.localPosition = basePosition + new Vector3(0f, stepLift, 0f) + hurtShake + attackOffset;
            visualPivot.localScale = baseScale;
        }
        else
        {
            float breathe = Mathf.Sin(Time.time * 3f);
            visualPivot.localPosition = basePosition + new Vector3(0f, breathe * 0.025f, 0f) + hurtShake + attackOffset;
            visualPivot.localScale = baseScale;
        }
    }

    private static bool HasParameter(Animator target, string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.name == parameterName && parameter.type == type) return true;
        return false;
    }
}
