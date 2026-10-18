using UnityEngine;

/// <summary>
/// Frame gốc kết hợp chuyển động theo trạng thái; không kéo giãn sprite/collider.
/// Host chọn frame; LAN truyền SpriteRenderer qua cơ chế snapshot sẵn có.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpriteAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] alternateFrames;
    private Sprite[] primaryFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 8f;

    private EnemyHealth health;
    private float elapsed;
    private float hurtRemaining;
    private float healRemaining;
    private float attackRemaining;
    private float attackDuration;
    private float attackWindup;
    private float motionTime;
    private float deathTime;
    private Vector2 attackDirection;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private EnemyMovement movement;
    private EnemyCombat combat;
    private bool isSkull;
    private bool ownsColor;
    private Color baseColor;

    public SpriteRenderer Renderer => visual;
    public int CurrentFrame { get; private set; }
    public enum Pose { Idle, Moving, Attacking, Hurt, Dead }
    public Pose CurrentPose { get; private set; }

    private void Awake()
    {
        primaryFrames = frames;
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        combat = GetComponent<EnemyCombat>();
        isSkull = health != null && health.Config != null && health.Config.displayName == "Skull";
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        baseColor = visual != null ? visual.color : Color.white;
        basePosition = visual != null ? visual.transform.localPosition : Vector3.zero;
        baseRotation = visual != null ? visual.transform.localRotation : Quaternion.identity;
    }

    private void OnEnable()
    {
        if (primaryFrames != null && alternateFrames != null && alternateFrames.Length == primaryFrames.Length)
        {
            int hash = Mathf.FloorToInt(transform.position.x) ^ Mathf.FloorToInt(transform.position.y);
            frames = (hash & 1) == 0 ? primaryFrames : alternateFrames;
        }
        elapsed = 0f;
        hurtRemaining = 0f;
        healRemaining = attackRemaining = motionTime = deathTime = 0f;
        ownsColor = false;
        CurrentPose = Pose.Idle;
        CurrentFrame = 0;
        if (visual != null && frames != null && frames.Length > 0)
        {
            visual.sprite = frames[0];
            visual.color = baseColor;
            visual.transform.localPosition = basePosition;
            visual.transform.localRotation = baseRotation;
        }
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Healed += OnHealed;
        }
    }

    private void Start()
    {
        GetComponent<EnemyMovement>()?.SetVisualRenderer(visual);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Healed -= OnHealed;
        }
    }

    private void OnDamaged(int amount)
    {
        hurtRemaining = 0.12f;
        attackRemaining = 0f;
    }
    private void OnHealed(int amount) => healRemaining = 0.3f;

    public void TriggerAttackLunge(Vector2 direction, float windupOverride = -1f)
    {
        if (health != null && health.IsDead) return;
        attackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        attackWindup = windupOverride >= 0f ? windupOverride :
            health != null && health.Config != null ? health.Config.attackWindup : 0.4f;
        attackDuration = attackWindup + 0.15f;
        attackRemaining = attackDuration;
    }

    private void Update()
    {
        if (visual == null || frames == null || frames.Length == 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (health != null && health.IsDead)
        {
            CurrentPose = Pose.Dead;
            deathTime += dt;
            float fall = Mathf.Clamp01(deathTime / 0.45f);
            visual.transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, (visual.flipX ? 1f : -1f) * 85f * fall);
            visual.transform.localPosition = basePosition + Vector3.down * (0.18f * fall);
            return; // EnemyHealth sở hữu fade màu và thu dọn, không hồi sinh/ghi đè alpha.
        }
        float speed = movement != null ? movement.Velocity.magnitude : 0f;
        bool moving = speed > 0.15f;
        CurrentPose = hurtRemaining > 0f ? Pose.Hurt :
            attackRemaining > 0f || (combat != null && combat.IsAttacking) ? Pose.Attacking :
            moving ? Pose.Moving : Pose.Idle;
        float fps = Mathf.Max(1f, framesPerSecond) * (CurrentPose == Pose.Attacking ? 1.75f : moving ? 1.5f : 1f);
        float duration = frames.Length / fps;
        elapsed = (elapsed + Time.deltaTime) % duration;
        CurrentFrame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * fps));
        visual.sprite = frames[CurrentFrame];

        motionTime += dt * (moving ? Mathf.Max(6f, speed * 5f) : 3f);
        Vector3 offset = Vector3.zero;
        float tilt = 0f;
        if (CurrentPose == Pose.Hurt)
            offset.x = Mathf.Sin(motionTime * 16f) * 0.045f;
        else if (attackRemaining > 0f)
        {
            float progress = 1f - attackRemaining / attackDuration;
            float reach = progress < 0.65f ? -0.06f * Mathf.Sin(progress / 0.65f * Mathf.PI) :
                0.18f * Mathf.Sin((progress - 0.65f) / 0.35f * Mathf.PI);
            offset = (Vector3)attackDirection * reach;
            tilt = (visual.flipX ? -1f : 1f) * Mathf.Sin(progress * Mathf.PI) * 8f;
        }
        else if (isSkull)
        {
            offset.y = Mathf.Sin(motionTime) * (moving ? 0.055f : 0.025f);
            tilt = moving ? -Mathf.Clamp(movement.Velocity.x, -1f, 1f) * 6f : 0f;
        }
        else if (moving)
        {
            offset.y = Mathf.Abs(Mathf.Sin(motionTime)) * 0.045f;
            tilt = Mathf.Sin(motionTime) * 3f;
        }
        visual.transform.localPosition = basePosition + offset;
        visual.transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, tilt);
        hurtRemaining = Mathf.Max(0f, hurtRemaining - dt);
        healRemaining = Mathf.Max(0f, healRemaining - dt);
        attackRemaining = Mathf.Max(0f, attackRemaining - dt);
        // Không sở hữu màu ngoài hurt/heal để giữ màu telegraph của EnemyBrain.
        if (hurtRemaining > 0f || healRemaining > 0f)
        {
            ownsColor = true;
            visual.color = hurtRemaining > 0f ? new Color(1f, 0.35f, 0.35f, baseColor.a) :
                new Color(0.65f, 1f, 0.5f, baseColor.a);
        }
        else if (ownsColor) { visual.color = baseColor; ownsColor = false; }
    }

}
