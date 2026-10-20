// Loads real frame-by-frame enemy sheets for legacy one-sprite prefabs.
// NavMeshAgent continues to own the root transform and collision shape.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyRuntimeSpriteAnimation : MonoBehaviour
{
    private enum Layout { Horizontal, Grid, Directional }
    private enum Pose { Idle, Moving, Attacking, Hurt, Dead }

    private sealed class Profile
    {
        public string idle, move, attack, hurt, death;
        public int cell, columns, rows, idleStart, idleCount, moveStart, moveCount;
        public int attackStart, attackCount, hurtStart, hurtCount, deathStart, deathCount;
        public int directionalFrames;
        public float fps = 10f;
        public Layout layout = Layout.Horizontal;
        public Color tint = Color.white;
    }

    private SpriteRenderer visual;
    private EnemyMovement movement;
    private EnemyHealth health;
    private EnemyCombat combat;
    private Profile profile;
    private Sprite[][] horizontalFrames;
    private Sprite[][] directionalFrames;
    private Sprite[][] stateFrames;
    private float clock;
    private float attackUntil;
    private float hurtUntil;
    private bool ready;

    public static bool TryAttach(EnemyMovement owner, SpriteRenderer renderer)
    {
        if (owner == null || renderer == null) return false;
        Profile profile = GetProfile(owner.gameObject.name, owner.GetComponent<EnemyBrain>()?.Config);
        if (profile == null) return false;
        EnemyRuntimeSpriteAnimation animation = owner.gameObject.AddComponent<EnemyRuntimeSpriteAnimation>();
        animation.Initialize(owner, renderer, profile);
        return animation.ready;
    }

    public void TriggerAttack(float duration)
    {
        attackUntil = Mathf.Max(attackUntil, Time.time + Mathf.Max(0.12f, duration));
        clock = 0f;
    }

    private void Initialize(EnemyMovement owner, SpriteRenderer renderer, Profile data)
    {
        movement = owner;
        visual = renderer;
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        profile = data;
        visual.color = profile.tint;
        if (profile.layout == Layout.Directional)
        {
            directionalFrames = new Sprite[4][];
            for (int row = 0; row < 4; row++)
                directionalFrames[row] = Slice(profile.move, profile.cell, profile.cell,
                    profile.columns, profile.rows, 0, profile.moveCount, row, profile.fps);
            stateFrames = new Sprite[6][];
            stateFrames[(int)Pose.Idle] = Slice(profile.idle, profile.cell, profile.cell,
                profile.columns, profile.rows, 0, profile.idleCount, -1, profile.fps);
            stateFrames[(int)Pose.Moving] = directionalFrames[0];
            stateFrames[(int)Pose.Attacking] = Slice(profile.attack, profile.cell, profile.cell,
                profile.columns, profile.rows, 0, profile.attackCount, -1, profile.fps);
            stateFrames[(int)Pose.Hurt] = Slice(profile.hurt, profile.cell, profile.cell,
                profile.columns, profile.rows, 0, profile.hurtCount, -1, profile.fps);
            stateFrames[(int)Pose.Dead] = Slice(profile.death, profile.cell, profile.cell,
                profile.columns, profile.rows, 0, profile.deathCount, -1, profile.fps);
        }
        else if (profile.layout == Layout.Grid)
        {
            stateFrames = new Sprite[6][];
            stateFrames[(int)Pose.Idle] = Slice(profile.idle, profile.cell, profile.cell,
                profile.columns, profile.rows, profile.idleStart, profile.idleCount, -1, profile.fps);
            stateFrames[(int)Pose.Moving] = Slice(profile.move, profile.cell, profile.cell,
                profile.columns, profile.rows, profile.moveStart, profile.moveCount, -1, profile.fps);
            stateFrames[(int)Pose.Attacking] = Slice(profile.attack, profile.cell, profile.cell,
                profile.columns, profile.rows, profile.attackStart, profile.attackCount, -1, profile.fps);
            stateFrames[(int)Pose.Hurt] = Slice(profile.hurt, profile.cell, profile.cell,
                profile.columns, profile.rows, profile.hurtStart, profile.hurtCount, -1, profile.fps);
            stateFrames[(int)Pose.Dead] = Slice(profile.death, profile.cell, profile.cell,
                profile.columns, profile.rows, profile.deathStart, profile.deathCount, -1, profile.fps);
        }
        else
        {
            horizontalFrames = new Sprite[6][];
            horizontalFrames[(int)Pose.Idle] = Slice(profile.idle, profile.cell, profile.cell,
                profile.columns, 1, profile.idleStart, profile.idleCount, 0, profile.fps);
            horizontalFrames[(int)Pose.Moving] = Slice(profile.move, profile.cell, profile.cell,
                profile.columns, 1, profile.moveStart, profile.moveCount, 0, profile.fps);
            horizontalFrames[(int)Pose.Attacking] = Slice(profile.attack, profile.cell, profile.cell,
                profile.columns, 1, profile.attackStart, profile.attackCount, 0, profile.fps);
            horizontalFrames[(int)Pose.Hurt] = Slice(profile.hurt, profile.cell, profile.cell,
                profile.columns, 1, profile.hurtStart, profile.hurtCount, 0, profile.fps);
            horizontalFrames[(int)Pose.Dead] = Slice(profile.death, profile.cell, profile.cell,
                profile.columns, 1, profile.deathStart, profile.deathCount, 0, profile.fps);
        }

        Sprite[] idleFrames = GetFrames(Pose.Idle);
        Sprite[] movingFrames = GetFrames(Pose.Moving);
        ready = idleFrames != null && idleFrames.Length > 0 && movingFrames != null && movingFrames.Length > 0;
        if (!ready) return;
        visual.sprite = idleFrames[0];
        movement?.SetVisualRenderer(visual);
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        if (combat != null) combat.AttackFinished += OnAttackFinished;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (combat != null) combat.AttackFinished -= OnAttackFinished;
    }

    private void OnDamaged(int amount)
    {
        hurtUntil = Time.time + 0.2f;
        clock = 0f;
    }

    private void OnDied(EnemyHealth dead)
    {
        attackUntil = 0f;
        clock = 0f;
    }

    private void OnAttackFinished()
    {
        attackUntil = 0f;
        clock = 0f;
    }

    private void Update()
    {
        if (!ready || visual == null) return;
        Vector2 velocity = movement != null ? movement.Velocity : Vector2.zero;
        if (Mathf.Abs(velocity.x) > 0.05f) visual.flipX = velocity.x < 0f;
        bool isDead = health != null && health.IsDead;
        Pose pose = isDead ? Pose.Dead : Time.time < hurtUntil ? Pose.Hurt :
            Time.time < attackUntil || combat != null && combat.IsAttacking ? Pose.Attacking :
            velocity.sqrMagnitude > 0.02f ? Pose.Moving : Pose.Idle;
        Sprite[] frames = GetFrames(pose);
        if (frames == null || frames.Length == 0) frames = GetFrames(Pose.Idle);
        if (frames == null || frames.Length == 0) return;

        int directionRow = DirectionRow(velocity);
        if (profile.layout == Layout.Directional && pose == Pose.Moving &&
            directionRow >= 0 && directionRow < directionalFrames.Length &&
            directionalFrames[directionRow] != null && directionalFrames[directionRow].Length > 0)
            frames = directionalFrames[directionRow];
        float fps = pose == Pose.Attacking ? profile.fps * 1.35f : profile.fps;
        clock += Time.deltaTime * fps;
        int frame = pose == Pose.Dead ? Mathf.Min(Mathf.FloorToInt(clock), frames.Length - 1) :
            Mathf.FloorToInt(clock) % frames.Length;
        visual.sprite = frames[frame];
    }

    private static int DirectionRow(Vector2 velocity)
    {
        if (velocity.sqrMagnitude < 0.02f) return 0;
        if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y)) return velocity.x < 0f ? 1 : 2;
        return velocity.y > 0f ? 3 : 0;
    }

    private Sprite[] GetFrames(Pose pose)
    {
        if (profile.layout == Layout.Horizontal)
            return horizontalFrames != null ? horizontalFrames[(int)pose] : null;
        return stateFrames != null ? stateFrames[(int)pose] : null;
    }

    private static Sprite[] Slice(string resourcePath, int cellWidth, int cellHeight,
        int columns, int rows, int start, int count, int fixedRow, float fps)
    {
        if (string.IsNullOrEmpty(resourcePath) || count <= 0) return System.Array.Empty<Sprite>();
        Texture2D texture = Resources.Load<Texture2D>("Enemy/AnimatedArt/" + resourcePath);
        if (texture == null) return System.Array.Empty<Sprite>();
        int actualColumns = texture.width / cellWidth;
        int actualRows = texture.height / cellHeight;
        if (actualColumns <= 0 || actualRows <= 0) return System.Array.Empty<Sprite>();
        int colCount = Mathf.Min(columns, actualColumns);
        int rowCount = Mathf.Min(rows, actualRows);
        Sprite[] sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            int linear = start + i;
            int col = linear % colCount;
            int row = fixedRow >= 0 ? fixedRow : rowCount - 1 - (linear / colCount) % rowCount;
            if (fixedRow >= rowCount || col >= actualColumns || row < 0 || row >= actualRows) continue;
            sprites[i] = Sprite.Create(texture,
                new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            sprites[i].name = texture.name + "_" + linear;
        }
        return sprites;
    }

    private static Profile GetProfile(string objectName, EnemyConfigSO config)
    {
        string id = config != null ? config.displayName : objectName;
        if (id.Contains("SkeletonKing") || id.Contains("BoneSentinel") || objectName.Contains("SkeletonKing") || objectName.Contains("BoneSentinel"))
            return new Profile { idle = "UndeadKing", move = "UndeadKing", attack = "UndeadKing", cell = 32, columns = 3, rows = 8,
                idleCount = 1, moveCount = 3, attackCount = 3, fps = 8, layout = Layout.Directional };
        if (id.Contains("Archer"))
            return new Profile { idle = "SkeletonArcher", move = "SkeletonArcher", attack = "SkeletonArcher", hurt = "SkeletonArcher", death = "SkeletonArcher",
                cell = 32, columns = 4, rows = 4, idleCount = 2, moveCount = 4, attackCount = 4, hurtCount = 2, deathCount = 2, fps = 9, layout = Layout.Grid };
        if (id.Contains("Bat"))
            return new Profile { idle = "Bat", move = "Bat", attack = "Bat", cell = 24, columns = 9, rows = 1,
                idleCount = 9, moveCount = 9, attackCount = 9, fps = 16 };
        if (id.Contains("Dark Mage") || id.Contains("Mage"))
            return MonsterProfile("MonsterWitch");
        if (id.Contains("Elite"))
            return MonsterProfile("MonsterZombie");
        if (id.Contains("Skull"))
            return MonsterProfile("MonsterSkeleton");
        if (id.Contains("Vampire"))
            return new Profile { idle = "VampireIdle", move = "VampireWalk", attack = "VampireAttack", hurt = "VampireHurt", death = "VampireDeath",
                cell = 64, columns = 12, rows = 4, idleCount = 4, moveCount = 6, attackCount = 12, hurtCount = 4, deathCount = 11,
                fps = 10, layout = Layout.Directional };
        return null;
    }

    private static Profile MonsterProfile(string art)
    {
        return new Profile { idle = art, move = art, attack = art, hurt = art, death = art,
            cell = 32, columns = art == "MonsterSkeleton" ? 39 : 40, rows = 1,
            idleStart = 0, idleCount = 2, moveStart = 6, moveCount = 16,
            attackStart = 2, attackCount = 4, hurtStart = 22, hurtCount = 4,
            deathStart = 28, deathCount = 8, fps = 11 };
    }
}
