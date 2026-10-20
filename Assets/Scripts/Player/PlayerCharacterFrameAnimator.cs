using UnityEngine;

/// <summary>
/// Drives the three new characters from standalone 120x80 PNG files. Ashen
/// Knight stays on the original AnimatorController and is never modified here.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class PlayerCharacterFrameAnimator : MonoBehaviour
{
    private enum BodyState { Idle, Run, Attack, Dash, Hurt, Death }

    public PlayableCharacterId Character { get; private set; } = PlayableCharacterId.AshenKnight;

    private PlayerMovement movement;
    private PlayerSkillController skills;
    private HealthController health;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Animator legacyAnimator;
    private Sprite[] idle, run, attack, dash, hurt, death;
    private BodyState state;
    private float stateStartedAt;

    public static PlayerCharacterFrameAnimator Configure(GameObject player, PlayableCharacterId character)
    {
        if (player == null) return null;
        PlayerCharacterFrameAnimator driver = player.GetComponent<PlayerCharacterFrameAnimator>();
        if (driver == null) driver = player.AddComponent<PlayerCharacterFrameAnimator>();
        driver.SetCharacter(character);
        return driver;
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        skills = GetComponent<PlayerSkillController>();
        health = GetComponent<HealthController>();
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        legacyAnimator = GetComponent<Animator>();
    }

    public void SetCharacter(PlayableCharacterId character)
    {
        if (!System.Enum.IsDefined(typeof(PlayableCharacterId), character))
            character = PlayableCharacterId.AshenKnight;

        Character = character;
        if (legacyAnimator == null) legacyAnimator = GetComponent<Animator>();
        if (character == PlayableCharacterId.AshenKnight)
        {
            if (legacyAnimator != null) legacyAnimator.enabled = true;
            enabled = false;
            return;
        }

        string root = $"Characters/{character}/Frames";
        idle = Load(root + "/Idle_6f", 6);
        run = Load(root + "/Run_8f", 8);
        attack = Load(root + "/Attack_6f", 6);
        dash = Load(root + "/Dash_6f", 6);
        hurt = Load(root + "/Hurt_3f", 3);
        death = Load(root + "/Death_6f", 6);
        enabled = true;
        state = BodyState.Idle;
        stateStartedAt = Time.time;
        if (legacyAnimator != null) legacyAnimator.enabled = false;
    }

    private void LateUpdate()
    {
        if (Character == PlayableCharacterId.AshenKnight || spriteRenderer == null)
            return;

        // PlayerMovement may briefly re-enable it after a heavy strike.
        if (legacyAnimator != null && legacyAnimator.enabled)
            legacyAnimator.enabled = false;

        BodyState next = ResolveState();
        if (next != state)
        {
            state = next;
            stateStartedAt = Time.time;
        }

        Sprite[] frames = FramesFor(state);
        if (frames == null || frames.Length == 0) return;
        float fps = state switch
        {
            BodyState.Run => 10f,
            BodyState.Attack => 13f,
            BodyState.Dash => 15f,
            BodyState.Hurt => 9f,
            BodyState.Death => 8f,
            _ => 6f
        };
        int raw = Mathf.FloorToInt((Time.time - stateStartedAt) * fps);
        bool loop = state is BodyState.Idle or BodyState.Run or BodyState.Attack or BodyState.Dash;
        int index = loop ? raw % frames.Length : Mathf.Min(raw, frames.Length - 1);
        spriteRenderer.sprite = frames[Mathf.Max(0, index)];
    }

    private BodyState ResolveState()
    {
        if (health != null && health.currentHealth <= 0) return BodyState.Death;
        if (health != null && Time.time - health.LastDamageAt < 0.28f) return BodyState.Hurt;
        if (movement != null && movement.IsDashing) return BodyState.Dash;
        if ((movement != null && (movement.IsAttacking || movement.IsHeavyLocked)) ||
            (skills != null && skills.IsSkillCasting)) return BodyState.Attack;
        if (body != null && body.velocity.sqrMagnitude > 0.02f) return BodyState.Run;
        return BodyState.Idle;
    }

    private Sprite[] FramesFor(BodyState value) => value switch
    {
        BodyState.Run => run,
        BodyState.Attack => attack,
        BodyState.Dash => dash,
        BodyState.Hurt => hurt,
        BodyState.Death => death,
        _ => idle
    };

    private static Sprite[] Load(string path, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Resources.Load<Sprite>($"{path}/Frame_{i:00}");
            if (frames[i] == null)
                Debug.LogError($"Missing standalone character frame: Resources/{path}/Frame_{i:00}.png");
        }
        return frames;
    }
}
