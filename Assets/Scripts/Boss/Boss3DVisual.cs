using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hệ thống thị giác giả lập 3D (Pseudo-3D & Depth Visuals) chuyên sâu cho Boss và Mini-Boss.
/// Giữ nguyên vẹn toàn bộ hình ảnh gốc (original sprite/texture) của nhân vật,
/// nhưng biến hình ảnh phẳng (2D cardboard) thành một thực thể sống động trong không gian 3 chiều:
/// 1. Dynamic 3D Soft Drop Shadow: Bóng đổ elip mềm mại bám sát mặt đất, co giãn và mờ dần theo độ cao giả lập (pseudo-Z height).
/// 2. 3D Rotating Rune Magic Circle: Vòng ma pháp trận elip 3D xoay tròn liên tục trên sàn dưới chân Boss, phát sáng theo nhịp thở.
/// 3. Pseudo-3D Tilting & Velocity Banking: Thân hình nghiêng theo quán tính khi đổi hướng di chuyển và co dãn phối cảnh (perspective foreshortening).
/// 4. Organic Multi-axis Breathing & Levitation: Nhịp thở sinh học đa chiều (nén dãn thể tích) và quỹ đạo lơ lửng hình số 8 (Lissajous curve).
/// 5. Parallax Floating Crown / Ornaments: Vương miện hoàng kim đa tầng bay lơ lửng trên đầu với độ trễ thị sai (Parallax delay).
/// 6. Dynamic 2D/3D Boss Light & Aura: Ánh sáng Light2D ma mị và các hạt năng lượng (embers/motes) bốc lên từ mặt đất.
/// 7. Combat Anticipation & Afterimages: Gồng người tích lực (squash back), lao vút (lunge stretch) kèm tàn ảnh ma quái, rung chấn 3D khi trúng đòn.
/// </summary>
[DisallowMultipleComponent]
public class Boss3DVisual : MonoBehaviour
{
    public enum BossVisualKind
    {
        SkeletonKing,
        BoneSentinel,
        DarkMage,
        Bat,
        GeneralBoss
    }

    [Header("Boss Identity")]
    [SerializeField] private BossVisualKind bossKind = BossVisualKind.GeneralBoss;
    [SerializeField] private Color primaryAuraColor = new Color(0.95f, 0.25f, 0.35f, 0.85f);
    [SerializeField] private Color secondaryAuraColor = new Color(1f, 0.82f, 0.2f, 0.95f);

    [Header("3D Perspective & Motion")]
    [SerializeField, Range(2f, 15f)] private float maxTiltAngle = 6.5f;
    [SerializeField, Range(0.02f, 0.3f)] private float hoverBobHeight = 0.09f;
    [SerializeField, Range(0.5f, 5f)] private float hoverFrequency = 1.8f;
    [SerializeField] private bool isHovering;

    [Header("Shadow & Ground Dais")]
    [SerializeField] private float shadowScaleX = 1.4f;
    [SerializeField] private float shadowScaleY = 0.58f;
    [SerializeField] private float shadowGroundOffsetY = -0.32f;
    [SerializeField] private float runeCircleRadius = 1.6f;

    [Header("Lighting")]
    [SerializeField] private bool enableDynamicLight = true;
    [SerializeField] private float lightIntensity = 0.95f;
    [SerializeField] private float lightRadius = 4.2f;

    // Component references
    private EnemyMovement movement;
    private EnemyHealth health;
    private EnemyCombat combat;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer displayRenderer;
    private SpriteRenderer leftLegRenderer;
    private SpriteRenderer rightLegRenderer;
    private Transform leftLegTransform;
    private Transform rightLegTransform;
    private Sprite rigSourceSprite;
    private Sprite rigTorsoSprite;

    // Hierarchy transforms
    private Transform visualPivot;
    private Transform shadowTransform;
    private SpriteRenderer shadowRenderer;
    private Transform runeCircleTransform;
    private SpriteRenderer runeCircleRenderer;
    private Transform crownTransform;
    private SpriteRenderer crownRenderer;
    private Light2D bossLight;

    // Motion states
    private Vector3 basePivotPosition;
    private Vector3 basePivotScale = Vector3.one;
    private float currentTiltZ;
    private float walkCycle;
    private float hurtUntil;
    private float nextGhostTime;
    private float currentPhaseScaleMultiplier = 1f;
    private Color originalDisplayColor = Color.white;
    private Vector3 crownVelocity;
    private Vector3 targetCrownLocalPos;

    // Procedural Sprites Cache
    private static Sprite cachedSoftShadow;
    private static Sprite cachedRuneCircle;
    private static Sprite cachedKingCrown;
    private static Sprite cachedMoteSprite;

    public SpriteRenderer DisplayRenderer => displayRenderer != null ? displayRenderer : sourceRenderer;

    public void Setup(BossVisualKind kind, Color auraColor, float scaleMultiplier = 1f)
    {
        bossKind = kind;
        primaryAuraColor = auraColor;
        currentPhaseScaleMultiplier = scaleMultiplier;
        ApplyThemeColors();
    }

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        sourceRenderer = GetComponent<SpriteRenderer>();

        DetectBossKind();
        BuildVisualRig();
    }

    private void DetectBossKind()
    {
        if (GetComponent<SkeletonKingController>() != null || gameObject.name.Contains("SkeletonKing"))
        {
            bossKind = BossVisualKind.SkeletonKing;
            primaryAuraColor = new Color(0.95f, 0.22f, 0.28f, 0.9f); // Đỏ vương giả
            secondaryAuraColor = new Color(1f, 0.84f, 0.15f, 0.95f); // Hoàng kim
            shadowScaleX = 2.2f;
            shadowScaleY = 0.85f;
            runeCircleRadius = 2.6f;
            isHovering = false;
            shadowGroundOffsetY = -0.75f;
        }
        else if (GetComponent<MiniBossController>() != null || gameObject.name.Contains("BoneSentinel"))
        {
            bossKind = BossVisualKind.BoneSentinel;
            primaryAuraColor = new Color(0.04f, 0.95f, 0.62f, 0.9f); // Lục bảo ấn chú (khớp với ngọc rune xanh trên BoneSentinel.png)
            secondaryAuraColor = new Color(0.2f, 1f, 0.8f, 0.95f);
            shadowScaleX = 2.2f;
            shadowScaleY = 0.85f;
            runeCircleRadius = 2.6f;
            isHovering = false;
            shadowGroundOffsetY = -0.72f;
        }
        else if (gameObject.name.Contains("DarkMage"))
        {
            bossKind = BossVisualKind.DarkMage;
            primaryAuraColor = new Color(0.7f, 0.3f, 1f, 0.85f); // Tím ma pháp
            secondaryAuraColor = new Color(0.2f, 0.9f, 0.8f, 0.9f);
            isHovering = true;
            shadowGroundOffsetY = -0.3f;
        }
        else if (gameObject.name.Contains("Bat"))
        {
            bossKind = BossVisualKind.Bat;
            primaryAuraColor = new Color(0.85f, 0.15f, 0.15f, 0.75f);
            isHovering = true;
            hoverBobHeight = 0.15f;
            hoverFrequency = 3.5f;
            shadowGroundOffsetY = -0.28f;
        }
    }

    private void BuildVisualRig()
    {
        if (sourceRenderer == null) return;

        // 1. Dời sprite gốc sang Pivot độc lập để thỏa sức biến dạng 3D mà không làm lệch Collider/NavMesh
        GameObject pivotObj = new GameObject("Boss 3D Visual Pivot");
        pivotObj.transform.SetParent(transform, false);
        visualPivot = pivotObj.transform;
        basePivotPosition = visualPivot.localPosition;
        basePivotScale = visualPivot.localScale;

        displayRenderer = pivotObj.AddComponent<SpriteRenderer>();
        displayRenderer.sprite = sourceRenderer.sprite;
        displayRenderer.color = sourceRenderer.color;
        originalDisplayColor = sourceRenderer.color;
        displayRenderer.flipX = sourceRenderer.flipX;
        displayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        displayRenderer.sortingOrder = Mathf.Max(5, sourceRenderer.sortingOrder);
        displayRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

        if (bossKind == BossVisualKind.BoneSentinel || bossKind == BossVisualKind.SkeletonKing)
            BuildWalkingLegs();

        // Giữ sourceRenderer ẩn đi nhưng lưu dữ liệu
        sourceRenderer.enabled = false;

        // 2. Tạo Dynamic 3D Soft Drop Shadow dưới chân
        CreateDynamicShadow();

        // 3. Tạo 3D Magic Rune Circle xoay tròn trên mặt đất
        CreateRuneMagicCircle();

        // 4. Nếu là Skeleton King, tạo vương miện 3D có độ trễ thị sai (Parallax Crown)
        if (bossKind == BossVisualKind.SkeletonKing)
        {
            CreateParallaxCrown();
        }

        // 5. Tạo nguồn sáng Light2D ma quái chiếu rọi hầm ngục
        if (enableDynamicLight)
        {
            CreateBossLight();
        }

        // Đăng ký bộ renderer mới cho các script controller khác
        movement?.SetVisualRenderer(displayRenderer);
        GetComponent<MiniBossController>()?.SetVisualRenderer(displayRenderer);
        GetComponent<SkeletonKingController>()?.SetVisualRenderer(displayRenderer);
    }

    private void BuildWalkingLegs()
    {
        string path = bossKind == BossVisualKind.SkeletonKing ? "Boss/LegRig/SkeletonKing" : "Boss/LegRig/BoneSentinel";
        rigTorsoSprite = Resources.Load<Sprite>(path + "Torso");
        Sprite left = Resources.Load<Sprite>(path + "LeftLeg");
        Sprite right = Resources.Load<Sprite>(path + "RightLeg");
        if (rigTorsoSprite == null || left == null || right == null) return;
        rigSourceSprite = sourceRenderer.sprite;
        displayRenderer.sprite = rigTorsoSprite;
        leftLegRenderer = CreateLeg("Left walking leg", left, out leftLegTransform);
        rightLegRenderer = CreateLeg("Right walking leg", right, out rightLegTransform);
        // The shared legacy Animator swaps the large boss art for a tiny
        // UndeadKing sheet. The rig owns locomotion and attack motion instead.
        Animator legacyAnimator = GetComponent<Animator>();
        if (legacyAnimator != null) legacyAnimator.enabled = false;
    }

    private SpriteRenderer CreateLeg(string objectName, Sprite sprite, out Transform legTransform)
    {
        GameObject leg = new GameObject(objectName);
        leg.transform.SetParent(visualPivot, false);
        legTransform = leg.transform;
        SpriteRenderer renderer = leg.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = sourceRenderer.color;
        renderer.flipX = sourceRenderer.flipX;
        renderer.sortingLayerID = displayRenderer.sortingLayerID;
        renderer.sortingOrder = displayRenderer.sortingOrder + 1;
        renderer.sharedMaterial = displayRenderer.sharedMaterial;
        return renderer;
    }

    private void CreateDynamicShadow()
    {
        GameObject shadowObj = new GameObject("3D Ground Shadow");
        shadowObj.transform.SetParent(transform, false);
        shadowTransform = shadowObj.transform;
        shadowTransform.localPosition = new Vector3(0f, shadowGroundOffsetY, 0f);
        shadowTransform.localScale = new Vector3(shadowScaleX, shadowScaleY, 1f);

        shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetSoftShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.52f);
        shadowRenderer.sortingLayerID = displayRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = 0; // Luôn nằm sát mặt sàn bên dưới mọi thứ
    }

    private void CreateRuneMagicCircle()
    {
        GameObject runeObj = new GameObject("3D Ground Rune Circle");
        runeObj.transform.SetParent(transform, false);
        runeCircleTransform = runeObj.transform;
        runeCircleTransform.localPosition = new Vector3(0f, shadowGroundOffsetY + 0.02f, 0f);
        runeCircleTransform.localScale = new Vector3(runeCircleRadius, runeCircleRadius * 0.44f, 1f);

        runeCircleRenderer = runeObj.AddComponent<SpriteRenderer>();
        runeCircleRenderer.sprite = GetRuneCircleSprite();
        runeCircleRenderer.color = primaryAuraColor;
        runeCircleRenderer.sortingLayerID = displayRenderer.sortingLayerID;
        runeCircleRenderer.sortingOrder = 1; // Nằm trên bóng đổ nhưng dưới chân Boss
    }

    private void CreateParallaxCrown()
    {
        // Xóa crown cũ (nếu từng tạo bởi SkeletonKingController cũ)
        Transform oldCrown = transform.Find("King Crown");
        if (oldCrown != null && oldCrown != transform)
            Destroy(oldCrown.gameObject);

        GameObject crownObj = new GameObject("3D Parallax Royal Crown");
        crownObj.transform.SetParent(transform, false);
        crownTransform = crownObj.transform;
        targetCrownLocalPos = new Vector3(0.02f, 1.05f, 0f);
        crownTransform.localPosition = targetCrownLocalPos;
        crownTransform.localScale = Vector3.one * 0.85f;

        crownRenderer = crownObj.AddComponent<SpriteRenderer>();
        crownRenderer.sprite = GetKingCrownSprite();
        crownRenderer.sortingLayerID = displayRenderer.sortingLayerID;
        crownRenderer.sortingOrder = displayRenderer.sortingOrder + 2; // Nổi bật phía trước đầu Boss
    }

    private void CreateBossLight()
    {
        GameObject lightObj = new GameObject("Boss 3D Ambient Light");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        bossLight = lightObj.AddComponent<Light2D>();
        bossLight.lightType = Light2D.LightType.Point;
        bossLight.pointLightOuterRadius = lightRadius;
        bossLight.pointLightInnerRadius = lightRadius * 0.28f;
        bossLight.intensity = lightIntensity;
        bossLight.color = primaryAuraColor;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void OnDamaged(int amount)
    {
        hurtUntil = Time.time + 0.18f;
        StartCoroutine(FlashDamageRoutine());
    }

    private void OnDied(EnemyHealth enemy)
    {
        if (runeCircleRenderer != null) runeCircleRenderer.enabled = false;
        if (shadowRenderer != null) shadowRenderer.enabled = false;
        if (bossLight != null) bossLight.enabled = false;
        if (crownRenderer != null) crownRenderer.enabled = false;
    }

    private IEnumerator FlashDamageRoutine()
    {
        if (displayRenderer == null) yield break;
        // Chớp sáng trắng tương phản cực mạnh như các game hành động đỉnh cao
        displayRenderer.color = new Color(1.8f, 0.5f, 0.5f, 1f);
        yield return new WaitForSeconds(0.08f);
        if (displayRenderer != null && (health == null || !health.IsDead))
            displayRenderer.color = originalDisplayColor;
    }

    private void Update()
    {
        // The procedural display renderer is separate from the prefab renderer. Keep
        // it in sync so runtime sprite-sheet animations remain visible on bosses.
        if (sourceRenderer != null && displayRenderer != null)
        {
            bool rigReady = rigTorsoSprite != null && sourceRenderer.sprite == rigSourceSprite;
            displayRenderer.sprite = rigReady ? rigTorsoSprite : sourceRenderer.sprite;
            // EnemyMovement owns facing through the display renderer.
            if (leftLegRenderer != null)
            {
                leftLegRenderer.enabled = rigReady;
                leftLegRenderer.flipX = displayRenderer.flipX;
                leftLegRenderer.color = displayRenderer.color;
            }
            if (rightLegRenderer != null)
            {
                rightLegRenderer.enabled = rigReady;
                rightLegRenderer.flipX = displayRenderer.flipX;
                rightLegRenderer.color = displayRenderer.color;
            }
        }
        if (visualPivot == null || (health != null && health.IsDead)) return;

        Vector2 velocity = movement != null ? movement.Velocity : Vector2.zero;
        float speed = velocity.magnitude;
        bool isMoving = speed > 0.12f;

        // 1. Quay Ma Pháp Trận 3D trên mặt đất (Perspective Ellipse Rotation)
        UpdateRuneMagicCircle(isMoving, speed);

        // 2. Cập nhật góc nghiêng 3D quán tính (Velocity Banking Tilt)
        Update3DTilt(velocity, isMoving);

        // 3. Tính toán độ cao giả lập (Pseudo-Z Height) & Nhịp thở sinh học
        float pseudoZHeight = 0f;
        Vector3 dynamicScale = basePivotScale * currentPhaseScaleMultiplier;

        if (isHovering)
        {
            // Quỹ đạo lơ lửng 3D hình số 8 (Lissajous Curve)
            float t = Time.time * hoverFrequency;
            float hoverY = Mathf.Sin(t) * hoverBobHeight;
            float hoverX = Mathf.Cos(t * 0.5f) * (hoverBobHeight * 0.35f);
            pseudoZHeight = hoverY;

            // Nhịp thở nén dãn thể tích nhẹ nhàng
            float breathe = Mathf.Sin(Time.time * 2.8f) * 0.045f;
            dynamicScale = new Vector3(dynamicScale.x * (1f + breathe), dynamicScale.y * (1f - breathe), 1f);
            visualPivot.localPosition = basePivotPosition + new Vector3(hoverX, hoverY, 0f);
        }
        else
        {
            // Quái / Boss đi bộ: Nhún nảy bước chân đàn hồi
            if (isMoving)
            {
                walkCycle += Time.deltaTime * Mathf.Max(speed * 4.2f, 7f);
                float stepSin = Mathf.Sin(walkCycle);
                float stepLift = Mathf.Abs(stepSin) * 0.035f;
                pseudoZHeight = stepLift;

                float squash = stepSin * 0.05f;
                dynamicScale = new Vector3(dynamicScale.x * (1f - squash), dynamicScale.y * (1f + squash), 1f);
                visualPivot.localPosition = basePivotPosition + new Vector3(0f, stepLift, 0f);
                UpdateWalkingLegs(stepSin, stepLift);
            }
            else
            {
                // Nhịp thở thư thái khi đứng chờ
                float breathe = Mathf.Sin(Time.time * 2.6f);
                pseudoZHeight = breathe * 0.02f;
                dynamicScale = new Vector3(dynamicScale.x * (1f + breathe * 0.035f), dynamicScale.y * (1f - breathe * 0.035f), 1f);
                visualPivot.localPosition = basePivotPosition + new Vector3(0f, breathe * 0.02f, 0f);
                UpdateWalkingLegs(0f, 0f);
            }
        }

        // 4. Phản ứng chấn động 3D khi trúng đòn
        if (Time.time < hurtUntil)
        {
            float shakeX = Mathf.Sin(Time.time * 65f) * 0.065f;
            float shakeY = Mathf.Cos(Time.time * 55f) * 0.045f;
            visualPivot.localPosition += new Vector3(shakeX, shakeY, 0f);
        }

        // 5. Phản ứng công kích (Combat Lunge / Anticipation)
        if (combat != null && combat.IsAttacking)
        {
            bool isFacingLeft = displayRenderer != null && displayRenderer.flipX;
            float flipSign = isFacingLeft ? -1f : 1f;
            float lungeCycle = Mathf.Sin(Time.time * 18f);
            Vector3 lungeOffset = new Vector3(flipSign * Mathf.Max(0f, lungeCycle) * 0.22f, 0f, 0f);
            visualPivot.localPosition += lungeOffset;
            dynamicScale.x *= (1f + Mathf.Max(0f, lungeCycle) * 0.12f);
        }

        visualPivot.localScale = dynamicScale;

        // 6. Cập nhật Bóng Đổ 3D: Co giãn & mờ dần theo độ cao Z tương đối
        UpdateDynamicShadow(pseudoZHeight);

        // 7. Cập nhật Vương Miện Thị Sai Parallax (nếu có)
        UpdateParallaxCrown();

        // 8. Cập nhật Nguồn Sáng & Tàn Ảnh (Ghost Trails)
        UpdateLightAndAfterimages(isMoving, speed);
    }

    private void UpdateWalkingLegs(float step, float torsoLift)
    {
        if (leftLegTransform == null || rightLegTransform == null) return;
        // Alternating support foot: one leg swings forward as the other stays
        // low, while the torso lift is canceled at each foot anchor.
        leftLegTransform.localPosition = new Vector3(step * 0.035f,
            Mathf.Max(0f, step) * 0.055f - torsoLift, 0f);
        rightLegTransform.localPosition = new Vector3(-step * 0.035f,
            Mathf.Max(0f, -step) * 0.055f - torsoLift, 0f);
    }

    private void UpdateRuneMagicCircle(bool isMoving, float speed)
    {
        if (runeCircleTransform == null || runeCircleRenderer == null) return;

        float rotateSpeed = isMoving ? 35f : 18f;
        // Xoay tròn đều ma pháp trận
        runeCircleTransform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        // Hào quang ma pháp trận dập dờn theo nhịp đập sinh mệnh
        float pulse = 0.72f + Mathf.Sin(Time.time * 3.2f) * 0.28f;
        Color c = primaryAuraColor;
        c.a = primaryAuraColor.a * pulse;
        runeCircleRenderer.color = c;
    }

    private void Update3DTilt(Vector2 velocity, bool isMoving)
    {
        float targetTilt = 0f;
        if (isMoving)
        {
            // Nghiêng thân vào hướng di chuyển (Banking tilt)
            targetTilt = -Mathf.Clamp(velocity.x * 1.8f, -1f, 1f) * maxTiltAngle;
        }

        currentTiltZ = Mathf.Lerp(currentTiltZ, targetTilt, Time.deltaTime * 10f);
        visualPivot.localRotation = Quaternion.Euler(0f, 0f, currentTiltZ);
    }

    private void UpdateDynamicShadow(float pseudoZHeight)
    {
        if (shadowTransform == null || shadowRenderer == null) return;

        // Khi nhảy / lơ lửng lên cao: Bóng thu nhỏ lại và mờ dần
        float heightFactor = Mathf.Clamp01(pseudoZHeight / 0.35f);
        float scaleMod = 1f - heightFactor * 0.25f;
        shadowTransform.localScale = new Vector3(shadowScaleX * scaleMod, shadowScaleY * scaleMod, 1f);

        Color shadowCol = shadowRenderer.color;
        shadowCol.a = Mathf.Lerp(0.52f, 0.22f, heightFactor);
        shadowRenderer.color = shadowCol;
    }

    private void UpdateParallaxCrown()
    {
        if (crownTransform == null) return;

        // Đầu nhún nhảy, vương miện lơ lửng đi theo với độ trễ (Spring latency) tạo chiều sâu 3D
        Vector3 targetPos = targetCrownLocalPos;
        // Hiệu ứng bồng bềnh độc lập của vương miện
        targetPos.y += Mathf.Sin(Time.time * 2.2f + 1f) * 0.035f;

        crownTransform.localPosition = Vector3.SmoothDamp(crownTransform.localPosition, targetPos, ref crownVelocity, 0.08f);
        // Vương miện hơi nghiêng nhẹ theo góc nghiêng của thân nhưng có độ trễ
        crownTransform.localRotation = Quaternion.Slerp(crownTransform.localRotation, Quaternion.Euler(0f, 0f, currentTiltZ * 0.65f), Time.deltaTime * 12f);
    }

    private void UpdateLightAndAfterimages(bool isMoving, float speed)
    {
        if (bossLight != null)
        {
            // Lửa đuốc ma pháp lập lòe nhẹ
            float flicker = Mathf.PerlinNoise(Time.time * 4f, 0.5f) * 0.25f;
            bossLight.intensity = lightIntensity * (0.88f + flicker);
        }

        // Sinh tàn ảnh (Ghost Trail) khi di chuyển nhanh hoặc đang tung đòn
        if (isMoving && speed > 2.2f && Time.time >= nextGhostTime)
        {
            nextGhostTime = Time.time + 0.12f;
            SpawnGhostTrail();
        }
    }

    private void SpawnGhostTrail()
    {
        if (displayRenderer == null || displayRenderer.sprite == null) return;

        GameObject ghostObj = new GameObject("Boss 3D Ghost Trail");
        ghostObj.transform.position = visualPivot.position;
        ghostObj.transform.rotation = visualPivot.rotation;
        ghostObj.transform.localScale = visualPivot.lossyScale;

        SpriteRenderer sr = ghostObj.AddComponent<SpriteRenderer>();
        sr.sprite = displayRenderer.sprite;
        sr.flipX = displayRenderer.flipX;
        sr.sortingLayerID = displayRenderer.sortingLayerID;
        sr.sortingOrder = displayRenderer.sortingOrder - 1;
        sr.color = new Color(primaryAuraColor.r, primaryAuraColor.g, primaryAuraColor.b, 0.38f);

        StartCoroutine(FadeAndDestroyGhost(sr, 0.25f));
    }

    private static IEnumerator FadeAndDestroyGhost(SpriteRenderer sr, float duration)
    {
        float elapsed = 0f;
        Color initColor = sr.color;
        while (sr != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(initColor.a, 0f, elapsed / duration);
            sr.color = new Color(initColor.r, initColor.g, initColor.b, alpha);
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    public void ApplyThemeColors()
    {
        if (runeCircleRenderer != null) runeCircleRenderer.color = primaryAuraColor;
        if (bossLight != null) bossLight.color = primaryAuraColor;
    }

    public void SetPhaseEnrage(int phase)
    {
        if (phase == 2)
        {
            primaryAuraColor = new Color(1f, 0.35f, 0.1f, 0.95f); // Hỏa diệm rực cháy
            currentPhaseScaleMultiplier = 1.08f;
        }
        else if (phase >= 3)
        {
            primaryAuraColor = new Color(1f, 0.05f, 0.05f, 1f); // Huyết nộ cuồng sát
            currentPhaseScaleMultiplier = 1.15f;
        }
        ApplyThemeColors();
    }

    // ==========================================
    // PROCEDURAL PIXEL-ART & SHADER ASSETS
    // ==========================================

    public static Sprite GetSoftShadowSprite()
    {
        if (cachedSoftShadow != null) return cachedSoftShadow;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "SoftGroundShadow";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (dist <= 1f)
                {
                    // Gaussian / Cosine falloff mềm mại
                    float alpha = Mathf.Pow(Mathf.Cos(dist * Mathf.PI * 0.5f), 1.6f);
                    byte a = (byte)(alpha * 255f);
                    pixels[y * size + x] = new Color32(0, 0, 0, a);
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(pixels);
        // These small static textures survive a Solo -> Host menu transition.
        // Keep CPU pixels so a later LAN session can still transmit them.
        tex.Apply(false, false);
        cachedSoftShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedSoftShadow;
    }

    public static Sprite GetRuneCircleSprite()
    {
        if (cachedRuneCircle != null) return cachedRuneCircle;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "RuneMagicCircle";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxRadius = size * 0.47f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 diff = new Vector2(x, y) - center;
                float dist = diff.magnitude / maxRadius;
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;

                float intensity = 0f;

                // 1. Vòng tròn ngoài viền sáng
                if (dist >= 0.88f && dist <= 0.98f)
                {
                    float ringDist = Mathf.Abs(dist - 0.93f) / 0.05f;
                    intensity = Mathf.Max(intensity, 1f - ringDist);
                }

                // 2. Vòng tròn mảnh bên trong
                if (dist >= 0.68f && dist <= 0.74f)
                {
                    float ringDist = Mathf.Abs(dist - 0.71f) / 0.03f;
                    intensity = Mathf.Max(intensity, (1f - ringDist) * 0.9f);
                }

                // 3. 8 tia ấn chú ma thuật (Rune Rays)
                float rayMod = Mathf.Abs(Mathf.Sin(angle * 4f * Mathf.Deg2Rad));
                if (rayMod > 0.94f && dist >= 0.45f && dist <= 0.96f)
                {
                    intensity = Mathf.Max(intensity, 0.85f);
                }

                // 4. Vòng tròn tâm
                if (dist >= 0.28f && dist <= 0.35f)
                {
                    float ringDist = Mathf.Abs(dist - 0.315f) / 0.035f;
                    intensity = Mathf.Max(intensity, (1f - ringDist) * 0.8f);
                }

                byte val = (byte)(Mathf.Clamp01(intensity) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, val);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        cachedRuneCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedRuneCircle;
    }

    public static Sprite GetKingCrownSprite()
    {
        if (cachedKingCrown != null) return cachedKingCrown;

        const int w = 24;
        const int h = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "KingCrown3D";
        tex.filterMode = FilterMode.Point; // Pixel-art sắc nét
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32 goldBright = new Color32(255, 225, 53, 255);
        Color32 goldMid = new Color32(235, 175, 25, 255);
        Color32 goldDark = new Color32(165, 105, 12, 255);
        Color32 rubyRed = new Color32(235, 35, 45, 255);
        Color32 rubyGlint = new Color32(255, 185, 195, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // Vẽ vương miện 3 chóp hoàng gia
        // Base band (hàng 2, 3, 4)
        for (int x = 4; x <= 19; x++)
        {
            pixels[2 * w + x] = goldDark;
            pixels[3 * w + x] = goldMid;
            pixels[4 * w + x] = goldBright;
        }

        // Chóp giữa (cao nhất, x: 10 - 13, y: 5 - 13)
        for (int y = 5; y <= 13; y++)
        {
            for (int x = 10; x <= 13; x++)
            {
                pixels[y * w + x] = x == 10 || x == 13 ? goldMid : goldBright;
            }
        }
        pixels[14 * w + 11] = goldBright;
        pixels[14 * w + 12] = goldBright;

        // Chóp trái (x: 5 - 7, y: 5 - 10)
        for (int y = 5; y <= 10; y++)
        {
            for (int x = 5; x <= 7; x++)
                pixels[y * w + x] = x == 5 ? goldDark : goldMid;
        }
        pixels[11 * w + 6] = goldBright;

        // Chóp phải (x: 16 - 18, y: 5 - 10)
        for (int y = 5; y <= 10; y++)
        {
            for (int x = 16; x <= 18; x++)
                pixels[y * w + x] = x == 18 ? goldDark : goldMid;
        }
        pixels[11 * w + 17] = goldBright;

        // Viên Hồng Ngọc Ruby đính giữa vương miện (x: 11, 12; y: 3, 4, 5)
        pixels[3 * w + 11] = rubyRed;
        pixels[3 * w + 12] = rubyRed;
        pixels[4 * w + 11] = rubyGlint;
        pixels[4 * w + 12] = rubyRed;
        pixels[5 * w + 11] = rubyRed;
        pixels[5 * w + 12] = rubyRed;

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        cachedKingCrown = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.2f), 16);
        return cachedKingCrown;
    }
}
