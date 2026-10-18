using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySheetAnimation : MonoBehaviour
{
    [SerializeField] private EnemySheetArtSO art;
    [SerializeField] private SpriteRenderer visual;
    private EnemyHealth health;
    private EnemyMovement movement;
    private float clock, attackClock, attackTime, attackWindup, hurtTime;
    private Vector2 direction = Vector2.right;
    private EnemySheetArtSO.Clip previous;
    public SpriteRenderer Renderer => visual;
    public EnemySheetArtSO Art => art;
    public int Variant { get; private set; }
    public int CurrentFrame { get; private set; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        uint hash = unchecked((uint)(Mathf.FloorToInt(transform.position.x) * 73856093 ^
            Mathf.FloorToInt(transform.position.y) * 19349663));
        Variant = art != null && art.variants.Length > 0 ? (int)(hash % (uint)art.variants.Length) : 0;
        if (art != null && visual != null) visual.sprite = art.Frame(Variant, art.idle, 0);
    }
    private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
    private void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }
    private void Start() => movement?.SetVisualRenderer(visual);
    private void OnDamaged(int amount) { hurtTime = 0.15f; attackTime = 0f; }
    public void TriggerAttack(Vector2 aim, float windup)
    {
        direction = aim;
        attackClock = 0f;
        attackWindup = Mathf.Max(0.01f, windup);
        attackTime = windup + 0.25f;
    }
    private void Update()
    {
        if (art == null || visual == null || Time.deltaTime <= 0f || health != null && health.IsDead) return;
        Vector2 velocity = movement != null ? movement.Velocity : Vector2.zero;
        if (velocity.sqrMagnitude > 0.02f && attackTime <= 0f) direction = velocity;
        var clip = velocity.sqrMagnitude > 0.02f ? art.move : art.idle;
        if (direction.y > Mathf.Abs(direction.x) && art.reverseMove.count > 0)
            clip = velocity.sqrMagnitude > 0.02f ? art.reverseMove : art.reverseIdle;
        if (attackTime > 0f)
        {
            clip = art.attack;
            if (direction.y < -Mathf.Abs(direction.x) && art.attackDown.count > 0) clip = art.attackDown;
            if (direction.y > Mathf.Abs(direction.x) && art.attackUp.count > 0) clip = art.attackUp;
            attackClock += Time.deltaTime;
        }
        if (clip.row != previous.row || clip.count != previous.count || clip.start != previous.start) clock = 0f;
        previous = clip;
        clock += Time.deltaTime;
        if (attackTime > 0f)
        {
            int impact = Mathf.Clamp(art.attackImpactFrame, 0, clip.count - 1);
            CurrentFrame = attackClock < attackWindup ? Mathf.FloorToInt(attackClock / attackWindup * impact) :
                impact + Mathf.FloorToInt((attackClock - attackWindup) / 0.25f * (clip.count - impact));
            CurrentFrame = Mathf.Clamp(CurrentFrame, 0, clip.count - 1);
        }
        else CurrentFrame = Mathf.FloorToInt(clock * art.framesPerSecond) % Mathf.Max(1, clip.count);
        Sprite frame = art.Frame(Variant, clip, CurrentFrame);
        if (frame != null) visual.sprite = frame;
        attackTime = Mathf.Max(0f, attackTime - Time.deltaTime);
        if (hurtTime > 0f)
        {
            hurtTime = Mathf.Max(0f, hurtTime - Time.deltaTime);
            visual.color = hurtTime > 0f ? new Color(1f, 0.35f, 0.35f) : Color.white;
        }
    }
}
