using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime ability layer for the player. Q/E/R intentionally use direct keyboard
/// reads until the team adds the bindings to PlayerControls.inputactions.
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    internal enum FrameAnchorMode
    {
        CellCenter,
        VisualCenter,
        VisualCenterX
    }

    [Header("Sword Wave (Q)")]
    [Min(0f)] public float swordWaveCooldown = 2.2f;
    [Min(0.1f)] public float swordWaveDuration = 0.75f;
    [Min(0f)] public float swordWaveSpeed = 13.5f;
    [Min(0.1f)] public float swordWaveRadius = 0.75f;
    [Min(1)] public int swordWaveDamage = 55;
    [Min(1f)] public float swordWaveAutoAimRange = 14f;
    [Header("Spin Slash (E)")]
    [Min(0f)] public float spinSlashCooldown = 4.5f;
    [Min(0.1f)] public float spinSlashDuration = 0.62f;
    [Tooltip("Visual-only tail for Spin Slash. This can outlive the damage window so the last frame fades naturally.")]
    [Min(0.1f)] public float spinSlashVfxDuration = 0.86f;
    [Min(0.1f)] public float spinSlashRadius = 1.75f;
    [Min(1)] public int spinSlashDamagePerPulse = 28;
    [Header("Ashen Judgment (R)")]
    [Range(0f, 100f)] public float ultimateCharge;
    [Min(1f)] public float ultimateChargeRequired = 100f;
    [Min(0.1f)] public float ultimateDuration = 1.25f;
    [Tooltip("Visual-only tail for Ashen Judgment's final impact. This does not delay damage or input recovery.")]
    [Min(0.1f)] public float ultimateFinalVfxDuration = 0.86f;
    [Min(0.1f)] public float ultimateRadius = 3.1f;
    [Min(1)] public int ultimateSlashDamage = 55;
    [Min(1)] public int ultimateImpactDamage = 110;

    private PlayerMovement player;
    private HealthController playerHealth;
    private SpriteRenderer playerRenderer;
    private Sprite[] swordWaveCoreFrames;
    private Sprite[] swordWaveImpactFrames;
    private Sprite[] spinSlashFrames;
    private Sprite[] runeFrames;
    private Sprite[] ultimateSlashFrames;
    private Sprite[] ultimateImpactFrames;
    private Sprite[] casterAuraFrames;
    private Sprite[] azureAegisLanceFrames, azureWardbreakerFrames, azureCitadelVerdictFrames;
    private Sprite[] chronoRiftCutterFrames, chronoTimeFractureFrames, chronoZeroHourFrames;
    private Sprite[] graveWraithFanFrames, graveSnareFrames, graveNightfallFrames;
    private Sprite[] gravewindArrowFrames;
    private AudioClip ultimateChargeSfx;
    private AudioClip ultimateImpactSfx;
    private AudioClip ultimateImpactCrunchSfx;
    private AudioClip swordWaveSfx;
    private AudioClip spinSlashSfx;
    private AudioClip azureAegisLanceSfx, azureWardbreakerSfx, azureCitadelVerdictSfx;
    private AudioClip chronoRiftCutterSfx, chronoTimeFractureSfx, chronoZeroHourSfx;
    private AudioClip gravewindBasicArrowSfx, graveWraithFanSfx, graveSnareSfx;
    private AudioClip graveNightfallSfx, graveNightfallImpactSfx;
    private float nextSwordWaveAt;
    private float nextSpinSlashAt;
    private bool isSkillCasting;
    private bool hitStopActive;
    private static Material pixelVfxMaterial;
    private LanPlayerInput lanInput;
    // Effects are deliberately tracked separately from Player. They are never
    // character clones and are destroyed both at their normal end and when the
    // player/controller is destroyed mid-cast.
    private readonly HashSet<GameObject> activeEffects = new HashSet<GameObject>();

    public bool IsSkillCasting => isSkillCasting;
    public float SwordWaveCooldownNormalized => LanLaunch.Mode == LanMode.Client && LanSessionBridge.Active?.LocalState != null
        ? LanSessionBridge.Active.LocalState.q : swordWaveCooldown <= 0f ? 0f : Mathf.Clamp01((nextSwordWaveAt - Time.time) / swordWaveCooldown);
    public float SpinSlashCooldownNormalized => LanLaunch.Mode == LanMode.Client && LanSessionBridge.Active?.LocalState != null
        ? LanSessionBridge.Active.LocalState.e : spinSlashCooldown <= 0f ? 0f : Mathf.Clamp01((nextSpinSlashAt - Time.time) / spinSlashCooldown);
    public float UltimateNormalized => LanLaunch.Mode == LanMode.Client && LanSessionBridge.Active?.LocalState != null
        ? LanSessionBridge.Active.LocalState.ultimate : ultimateChargeRequired <= 0f ? 0f : Mathf.Clamp01(ultimateCharge / ultimateChargeRequired);

    private void Start()
    {
        player = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<HealthController>();
        lanInput = GetComponent<LanPlayerInput>();
        playerRenderer = GetComponent<SpriteRenderer>();
        swordWaveCoreFrames = LoadPixelFrames("Combat/Frames/SwordWavePixel_6f", 6);
        swordWaveImpactFrames = LoadPixelFrames("Combat/Frames/SwordWaveImpactPixel_6f", 6);
        // These low-resolution sequences are authored as individual images on
        // one shared canvas. Keep the canvas centre fixed so asymmetrical windup
        // and fade frames do not wobble around the player/strike point.
        spinSlashFrames = LoadPixelFrames("Combat/Frames/E_SpinSlash_6f", 6,
            FrameAnchorMode.CellCenter, 64f);
        runeFrames = LoadPixelFrames("Combat/Frames/AshenJudgmentRunePixel_6f", 6,
            FrameAnchorMode.CellCenter, 64f);
        ultimateSlashFrames = LoadPixelFrames("Combat/Frames/AshenJudgmentBladesPixel_6f", 6,
            FrameAnchorMode.CellCenter, 64f);
        ultimateImpactFrames = LoadPixelFrames("Combat/Frames/AshenJudgmentFinalPixel_6f", 6,
            FrameAnchorMode.CellCenter, 64f);
        casterAuraFrames = LoadPixelFrames("Combat/Frames/CasterBodyAuraPixel_6f", 6, FrameAnchorMode.VisualCenter);
        azureAegisLanceFrames = LoadPixelFrames("Combat/Frames/AzureAegisLance_v2_6f", 6, FrameAnchorMode.CellCenter, 64f);
        azureWardbreakerFrames = LoadPixelFrames("Combat/Frames/AzureWardbreaker_v2_6f", 6, FrameAnchorMode.CellCenter, 64f);
        azureCitadelVerdictFrames = LoadPixelFrames("Combat/Frames/AzureCitadelVerdict_v2_6f", 6, FrameAnchorMode.CellCenter, 64f);
        // The Chronoblade sheets are 512 px canvases, but their visible effects
        // are authored to cover several world units. At 512 PPU they rendered
        // at roughly half their gameplay hit radius and looked disconnected
        // from the damage. 256 PPU keeps the VFX and hit areas in sync.
        chronoRiftCutterFrames = LoadPixelFrames("Combat/Frames/ChronoRiftCutter_6f", 6, FrameAnchorMode.CellCenter, 256f);
        chronoTimeFractureFrames = LoadPixelFrames("Combat/Frames/ChronoTimeFracture_6f", 6, FrameAnchorMode.CellCenter, 256f);
        chronoZeroHourFrames = LoadPixelFrames("Combat/Frames/ChronoZeroHour_6f", 6, FrameAnchorMode.CellCenter, 256f);
        graveWraithFanFrames = LoadPixelFrames("Combat/Frames/GraveWraithFan_6f", 6, FrameAnchorMode.CellCenter, 512f);
        graveSnareFrames = LoadPixelFrames("Combat/Frames/GraveSnareRanged_6f", 6,
            FrameAnchorMode.CellCenter, 512f);
        graveNightfallFrames = LoadPixelFrames("Combat/Frames/GraveNightfallRain_6f", 6,
            FrameAnchorMode.CellCenter, 512f);
        gravewindArrowFrames = EnemyArrowAnimation.LoadFrames();
        ultimateChargeSfx = Resources.Load<AudioClip>("Audio/Combat/UltimateCharge");
        ultimateImpactSfx = Resources.Load<AudioClip>("Audio/Combat/UltimateImpact");
        ultimateImpactCrunchSfx = Resources.Load<AudioClip>("Audio/Combat/UltimateImpactCrunch");
        swordWaveSfx = Resources.Load<AudioClip>("Audio/Combat/SwordWave");
        spinSlashSfx = Resources.Load<AudioClip>("Audio/Combat/SpinSlash");
        azureAegisLanceSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/Q_AegisLance");
        azureWardbreakerSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/E_Wardbreaker");
        azureCitadelVerdictSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/AzureWarden/R_CitadelVerdict");
        chronoRiftCutterSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/Q_RiftCutter");
        chronoTimeFractureSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/E_TimeFracture");
        chronoZeroHourSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/Chronoblade/R_ZeroHour");
        gravewindBasicArrowSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/BasicArrow");
        graveWraithFanSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/Q_WraithFan");
        graveSnareSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/E_GraveSnare");
        graveNightfallSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/R_Nightfall");
        graveNightfallImpactSfx = Resources.Load<AudioClip>("Audio/Combat/Characters/GravewindRanger/NightfallImpact");
        if (lanInput == null && GetComponent<SkillHudController>() == null)
            gameObject.AddComponent<SkillHudController>();

        // Sẵn sàng dùng chiêu R (Ashen Judgment) ngay khi vào màn
        ultimateCharge = ultimateChargeRequired;
    }

    [Header("Passive Charge")]
    [Min(0f)] public float ultimatePassiveChargePerSecond = 5f;

    private void Update()
    {
        if (LanLaunch.Mode == LanMode.Client || player == null || !player.enabled || isSkillCasting ||
            GameManager.isGamePaused || GameManager.isGameOver || GameManager.isWin)
            return;

        // Tự động nạp nộ theo thời gian để chiêu R luôn sẵn sàng
        if (ultimatePassiveChargePerSecond > 0f && ultimateCharge < ultimateChargeRequired)
        {
            AddUltimateCharge(ultimatePassiveChargePerSecond * Time.deltaTime);
        }

        bool qPressed = lanInput != null ? lanInput.Frame.q : WasKeyPressed(Key.Q, KeyCode.Q);
        bool ePressed = lanInput != null ? lanInput.Frame.e : WasKeyPressed(Key.E, KeyCode.E);
        bool rPressed = lanInput != null ? lanInput.Frame.r : WasKeyPressed(Key.R, KeyCode.R);

        bool castQ = qPressed && Time.time >= nextSwordWaveAt;
        bool castE = ePressed && Time.time >= nextSpinSlashAt;
        bool castR = rPressed && ultimateCharge >= ultimateChargeRequired;

        if (!castQ && !castE && !castR)
            return;

        // A skill press interrupts a mouse combo or held heavy charge. Cooldown
        // is checked first, so an unavailable skill never cancels an attack.
        player.InterruptAttackForSkill();
        if (castQ)
        {
            nextSwordWaveAt = Time.time + swordWaveCooldown;
            StartCoroutine(CastQForCharacter());
        }
        else if (castE)
        {
            nextSpinSlashAt = Time.time + spinSlashCooldown;
            StartCoroutine(CastEForCharacter());
        }
        else if (castR)
        {
            ultimateCharge = 0f;
            StartCoroutine(CastRForCharacter());
        }
    }

    private static bool WasKeyPressed(Key newKey, KeyCode legacyKey)
    {
        if (Keyboard.current != null)
        {
            try
            {
                if (Keyboard.current[newKey].wasPressedThisFrame) return true;
            }
            catch {}
        }
        try
        {
            if (Input.GetKeyDown(legacyKey)) return true;
        }
        catch {}
        return false;
    }

    public void AddUltimateCharge(float amount)
    {
        if (isSkillCasting || amount <= 0f)
            return;

        ultimateCharge = Mathf.Clamp(ultimateCharge + amount, 0f, ultimateChargeRequired);
    }

    private Vector2 GetSwordWaveDirection(Vector3 origin)
    {
        Vector2 fallback = player != null ? player.FacingDirection : Vector2.right;
        float bestDistSqr = swordWaveAutoAimRange * swordWaveAutoAimRange;
        Vector3? targetPos = null;

        // 1. Quét các quái EnemyHealth còn sống
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth e = enemies[i];
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy) continue;
            float d2 = (e.transform.position - origin).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                targetPos = e.transform.position;
            }
        }

        // 2. Quét các BossController còn sống
        BossController[] bosses = FindObjectsOfType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController b = bosses[i];
            if (b == null || b.IsDead || !b.gameObject.activeInHierarchy) continue;
            float d2 = (b.transform.position - origin).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                targetPos = b.transform.position;
            }
        }

        // 3. Quét các quái Enemy cũ (nếu có)
        Enemy[] legacyEnemies = FindObjectsOfType<Enemy>();
        for (int i = 0; i < legacyEnemies.Length; i++)
        {
            Enemy le = legacyEnemies[i];
            if (le == null || !le.gameObject.activeInHierarchy) continue;
            float d2 = (le.transform.position - origin).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                targetPos = le.transform.position;
            }
        }

        if (targetPos.HasValue)
        {
            Vector2 delta = (targetPos.Value - origin);
            if (delta.sqrMagnitude > 0.001f)
            {
                Vector2 targetDir = delta.normalized;
                if (player != null)
                {
                    player.SetFacing(targetDir.x < 0);
                }
                return targetDir;
            }
        }

        return fallback;
    }

    public void ResetForLanRun()
    {
        StopAllCoroutines();
        foreach (GameObject effect in activeEffects) if (effect != null) Destroy(effect);
        activeEffects.Clear();
        nextSwordWaveAt = nextSpinSlashAt = 0f;
        ultimateCharge = ultimateChargeRequired;
        isSkillCasting = hitStopActive = false;
    }

    private PlayableCharacterId CurrentCharacter
    {
        get
        {
            PlayerCharacterFrameAnimator animator = GetComponent<PlayerCharacterFrameAnimator>();
            return animator != null ? animator.Character : PlayableCharacterId.AshenKnight;
        }
    }

    private IEnumerator CastQForCharacter()
    {
        switch (CurrentCharacter)
        {
            case PlayableCharacterId.AzureWarden:
                yield return StartCoroutine(CastAegisLance());
                yield break;
            case PlayableCharacterId.Chronoblade:
                yield return StartCoroutine(CastRiftCutter());
                yield break;
            case PlayableCharacterId.GravewindRanger:
                yield return StartCoroutine(CastWraithFan());
                yield break;
            default:
                yield return StartCoroutine(CastSwordWave());
                yield break;
        }
    }

    private IEnumerator CastEForCharacter()
    {
        switch (CurrentCharacter)
        {
            case PlayableCharacterId.AzureWarden:
                yield return StartCoroutine(CastWardbreaker());
                yield break;
            case PlayableCharacterId.Chronoblade:
                yield return StartCoroutine(CastTimeFracture());
                yield break;
            case PlayableCharacterId.GravewindRanger:
                yield return StartCoroutine(CastGraveSnare());
                yield break;
            default:
                yield return StartCoroutine(CastSpinSlash());
                yield break;
        }
    }

    private IEnumerator CastRForCharacter()
    {
        switch (CurrentCharacter)
        {
            case PlayableCharacterId.AzureWarden:
                yield return StartCoroutine(CastCitadelVerdict());
                yield break;
            case PlayableCharacterId.Chronoblade:
                yield return StartCoroutine(CastZeroHour());
                yield break;
            case PlayableCharacterId.GravewindRanger:
                yield return StartCoroutine(CastNightfall());
                yield break;
            default:
                yield return StartCoroutine(CastAshenJudgment());
                yield break;
        }
    }

    private IEnumerator CastAegisLance()
    {
        BeginCast("aegis-lance");
        player.PlaySkillBodyMotion(1);
        player.PlayAbilitySfx(azureAegisLanceSfx, 0.92f, 1f);
        // The lance is a guarded advance rather than a fragile ranged spell:
        // Azure can absorb the counter-hit while committing to the front line.
        GrantAzureGuard(0.34f);
        Vector2 direction = GetSwordWaveDirection(player.VisualCenter);
        yield return StartCoroutine(FirePiercingBolt("Aegis Lance", azureAegisLanceFrames, direction, Color.white,
            12f, 0.60f, 0.78f, 42, CombatImpactKind.Azure, true, true));
        EndCast("aegis-lance");
    }

    private IEnumerator CastRiftCutter()
    {
        BeginCast("rift-cutter");
        player.PlaySkillBodyMotion(1);
        player.PlayAbilitySfx(chronoRiftCutterSfx, 0.90f, 1f);
        Vector3 start = player.VisualCenter;
        Vector2 direction = GetSwordWaveDirection(start);
        Vector3 targetPoint = GetRangedTargetPoint(start, 8f, 3.9f);
        float targetDistance = Vector2.Distance((Vector2)start, (Vector2)targetPoint);
        // A real assassin opener: cross the line, arrive on the far side, and
        // cut only the compact area around the landing point.
        player.BlinkForSkill(direction, Mathf.Clamp(targetDistance + 0.35f, 1.6f, 3.9f));
        Vector3 landing = player.VisualCenter;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        CreateTintedEffect("Rift Cutter", chronoRiftCutterFrames, landing, 0.82f, 0.48f,
            rotation, Color.white, smoothFrames: true, sortingOffset: 2);
        yield return new WaitForSeconds(0.14f);
        int hits = player.DamageEnemiesInRadius(landing, 0.88f, 76,
            playSwordImpact: true, impactKind: CombatImpactKind.Chrono);
        if (hits > 0)
        {
            AddSkillHitCharge(13f * hits);
            yield return StartCoroutine(PlayImpactFeedback(0.09f, 0.055f, 0.03f));
        }
        yield return new WaitForSeconds(0.24f);
        EndCast("rift-cutter");
    }

    private IEnumerator CastWraithFan()
    {
        BeginCast("wraith-fan");
        player.PlaySkillBodyMotion(1);
        player.PlayAbilitySfx(graveWraithFanSfx, 0.92f, 1f);
        Vector2 direction = GetSwordWaveDirection(player.VisualCenter);
        for (int i = -1; i <= 1; i++)
        {
            float angle = i * 16f;
            Vector2 fanDirection = Quaternion.Euler(0f, 0f, angle) * direction;
            StartCoroutine(FirePiercingBolt("Wraith Fan", graveWraithFanFrames, fanDirection, Color.white,
                14f, 0.56f, 0.43f, 30, CombatImpactKind.Arrow, false));
        }
        yield return new WaitForSeconds(0.56f);
        EndCast("wraith-fan");
    }

    private IEnumerator FirePiercingBolt(string effectName, Sprite[] frames, Vector2 direction, Color tint, float speed,
        float duration, float radius, int damage, CombatImpactKind impactKind = CombatImpactKind.SwordWave,
        bool playSwordImpact = true, bool stopAtWalls = false)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : player.FacingDirection;
        Vector3 position = player.VisualCenter + (Vector3)direction * 0.62f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        GameObject effect = CreateTintedEffect(effectName, frames, position, 0.60f, duration, rotation, tint);
        HashSet<Enemy> enemies = new HashSet<Enemy>();
        HashSet<BossController> bosses = new HashSet<BossController>();
        HashSet<EnemyHealth> configured = new HashSet<EnemyHealth>();
        LayerMask wallLayers = stopAtWalls ? EnemyCombat.ResolveWallLayers() : 0;
        bool impactFeedbackPlayed = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float step = speed * Time.deltaTime;
            if (stopAtWalls)
            {
                RaycastHit2D wall = Physics2D.Raycast(position, direction, step, wallLayers);
                if (wall.collider != null && !wall.collider.isTrigger)
                {
                    position = wall.point;
                    if (effect != null) effect.transform.position = position;
                    break;
                }
            }

            elapsed += Time.deltaTime;
            position += (Vector3)direction * step;
            if (effect != null) effect.transform.position = position;
            int hits = player.DamageEnemiesInRadius(position, radius, damage, enemies, bosses,
                playSwordImpact: playSwordImpact, alreadyHitConfiguredEnemies: configured,
                impactKind: impactKind);
            if (hits > 0)
            {
                AddSkillHitCharge(9f * hits);
                if (!impactFeedbackPlayed && impactKind != CombatImpactKind.Arrow)
                {
                    impactFeedbackPlayed = true;
                    StartCoroutine(PlayImpactFeedback(0.08f, 0.045f, 0.025f));
                }
            }
            yield return null;
        }
    }

    private IEnumerator CastWardbreaker()
    {
        BeginCast("wardbreaker");
        player.PlaySkillBodyMotion(2);
        player.PlayAbilitySfx(azureWardbreakerSfx, 0.96f, 0.98f);
        GrantAzureGuard(0.78f);
        Vector3 origin = player.VisualCenter;
        Vector2 direction = GetSwordWaveDirection(origin);
        Vector3 impactPoint = origin + (Vector3)direction * 1.05f;
        CreateTintedEffect("Wardbreaker Impact", azureWardbreakerFrames, impactPoint, 1.40f, 0.76f,
            Quaternion.identity, Color.white, smoothFrames: false, sortingOffset: 1);

        float[] radii = { 1.45f, 2.30f };
        int[] damages = { 30, 54 };
        for (int pulse = 0; pulse < radii.Length; pulse++)
        {
            yield return new WaitForSeconds(pulse == 0 ? 0.20f : 0.24f);
            int hits = player.DamageEnemiesInRadius(impactPoint, radii[pulse], damages[pulse],
                playSwordImpact: true, impactKind: CombatImpactKind.Azure);
            if (hits > 0)
                AddSkillHitCharge(9f * hits);
            StartCoroutine(ShakeMainCamera(0.07f, 0.045f + pulse * 0.025f));
        }

        yield return new WaitForSeconds(0.12f);
        EndCast("wardbreaker");
    }

    private IEnumerator CastTimeFracture()
    {
        BeginCast("time-fracture");
        player.PlaySkillBodyMotion(2);
        player.PlayAbilitySfx(chronoTimeFractureSfx, 0.94f, 1.03f);
        Vector3 origin = player.VisualCenter;
        Vector3 fracturePoint = GetRangedTargetPoint(origin, 10f, 4.5f);
        CreateTintedEffect("Time Fracture", chronoTimeFractureFrames, fracturePoint, 1.72f, 0.84f,
            Quaternion.identity, Color.white, smoothFrames: false, sortingOffset: 2);

        // Keep the fracture surgical: it rewards marking a priority target,
        // instead of turning the assassin into a second area-damage caster.
        float[] radii = { 0.72f, 0.94f, 1.18f };
        int[] damages = { 34, 48, 86 };
        for (int pulse = 0; pulse < radii.Length; pulse++)
        {
            yield return new WaitForSeconds(0.18f);
            int hits = player.DamageEnemiesInRadius(fracturePoint, radii[pulse], damages[pulse],
                impactKind: CombatImpactKind.Chrono);
            if (hits > 0)
                AddSkillHitCharge(8f * hits);
            StartCoroutine(ShakeMainCamera(0.05f, 0.025f + pulse * 0.012f));
        }

        yield return new WaitForSeconds(0.10f);
        EndCast("time-fracture");
    }

    private IEnumerator CastGraveSnare()
    {
        BeginCast("grave-snare");
        player.PlaySkillBodyMotion(2);
        player.PlayAbilitySfx(graveSnareSfx, 0.94f, 1f);
        Vector3 origin = player.VisualCenter;
        Vector3 target = GetRangedTargetPoint(origin, 11f, 5.25f);
        const float pulseInterval = 0.20f;
        CreateTintedEffect("Grave Snare", graveSnareFrames, target, 1.62f, 0.86f,
            Quaternion.identity, Color.white, smoothFrames: false, sortingOffset: 1);

        float[] radii = { 0.9f, 1.25f, 1.55f };
        int[] damages = { 30, 38, 52 };
        for (int i = 0; i < radii.Length; i++)
        {
            yield return new WaitForSeconds(pulseInterval);
            int hits = player.DamageEnemiesInRadius(target, radii[i], damages[i],
                impactKind: CombatImpactKind.Arrow);
            if (hits > 0) AddSkillHitCharge(9f * hits);
            StartCoroutine(ShakeMainCamera(0.05f, 0.025f + i * 0.008f));
        }

        yield return new WaitForSeconds(0.12f);
        EndCast("grave-snare");
    }

    private IEnumerator CastCitadelVerdict()
    {
        BeginCast("citadel-verdict");
        player.PlaySkillBodyMotion(3, true);
        player.PlayAbilitySfx(azureCitadelVerdictSfx, 1f, 0.96f);
        // The tank ultimate owns space and lets Azure survive inside the mob.
        GrantAzureGuard(1.85f);
        Vector3 origin = player.VisualCenter;
        CreateTintedEffect("Citadel Verdict", azureCitadelVerdictFrames, origin, 1.75f, 1.02f,
            Quaternion.identity, Color.white, smoothFrames: true, sortingOffset: 1);
        float[] radii = { 2.10f, 3.0f, 3.75f };
        int[] damages = { 38, 52, 78 };
        for (int pulse = 0; pulse < radii.Length; pulse++)
        {
            yield return new WaitForSeconds(0.23f);
            player.DamageEnemiesInRadius(origin, radii[pulse], damages[pulse],
                playSwordImpact: pulse == radii.Length - 1, impactKind: CombatImpactKind.Azure);
            StartCoroutine(ShakeMainCamera(0.07f, 0.04f + pulse * 0.015f));
        }
        yield return new WaitForSeconds(0.22f);
        EndCast("citadel-verdict");
    }

    private IEnumerator CastZeroHour()
    {
        BeginCast("zero-hour");
        player.PlaySkillBodyMotion(3, true);
        player.PlayAbilitySfx(chronoZeroHourSfx, 1f, 0.98f);
        Vector3 origin = player.VisualCenter;
        Vector3 zeroPoint = GetRangedTargetPoint(origin, 12f, 5.5f);
        Vector2 direction = ((Vector2)zeroPoint - (Vector2)origin).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = player.FacingDirection;
        player.BlinkForSkill(direction, Mathf.Min(7.5f, Vector2.Distance((Vector2)origin, (Vector2)zeroPoint)));
        zeroPoint = player.VisualCenter;
        CreateTintedEffect("Zero Hour", chronoZeroHourFrames, zeroPoint, 1.70f, 0.82f,
            Quaternion.identity, Color.white, smoothFrames: true, sortingOffset: 2);

        float[] radii = { 0.95f, 1.15f, 1.40f, 1.65f };
        int[] damages = { 48, 64, 82, 138 };
        for (int pulse = 0; pulse < radii.Length; pulse++)
        {
            yield return new WaitForSeconds(0.17f);
            player.DamageEnemiesInRadius(zeroPoint, radii[pulse], damages[pulse],
                impactKind: CombatImpactKind.Chrono);
            StartCoroutine(ShakeMainCamera(0.06f, 0.035f + pulse * 0.014f));
        }

        yield return StartCoroutine(PlayImpactFeedback(0.20f, 0.10f, 0.055f));
        yield return new WaitForSeconds(0.16f);
        EndCast("zero-hour");
    }

    private void GrantAzureGuard(float duration)
    {
        if (playerHealth == null)
            playerHealth = GetComponent<HealthController>();
        playerHealth?.GrantTemporaryInvulnerability(duration);
    }

    private IEnumerator CastNightfall()
    {
        BeginCast("nightfall");
        player.PlaySkillBodyMotion(3, true);
        player.PlayAbilitySfx(graveNightfallSfx, 1f, 1f);
        Vector3 origin = player.VisualCenter;
        Vector3 target = GetRangedTargetPoint(origin, 13f, 6.5f);
        Vector2 direction = ((Vector2)(target - origin)).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = player.FacingDirection;

        CreateTintedEffect("Nightfall Target", graveNightfallFrames, target, 1.92f, 1.08f,
            Quaternion.identity, Color.white, smoothFrames: false, sortingOffset: 2);

        float[] radii = { 1.45f, 1.9f, 2.4f };
        int[] damages = { 38, 52, 78 };
        for (int wave = 0; wave < radii.Length; wave++)
        {
            yield return new WaitForSeconds(0.20f);
            player.PlayAbilitySfx(graveNightfallImpactSfx, 0.70f + wave * 0.08f, 0.94f + wave * 0.05f);
            for (int arrow = -2; arrow <= 2; arrow++)
            {
                float angle = arrow * 7f + (wave % 2 == 0 ? 0f : 3.5f);
                Vector2 volleyDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                StartCoroutine(FireGravewindArrow(volleyDirection, 20f + wave * 2f, 0.72f,
                    0.28f, 18 + wave * 4, true, true, 1.05f));
            }

            int hits = player.DamageEnemiesInRadius(target, radii[wave], damages[wave],
                impactKind: CombatImpactKind.Arrow);
            if (hits > 0) AddSkillHitCharge(10f * hits);
            StartCoroutine(ShakeMainCamera(0.08f, 0.04f + wave * 0.012f));
        }

        yield return new WaitForSeconds(0.24f);
        EndCast("nightfall");
    }

    public void FireGravewindBasicArrow(int damage, int comboHit)
    {
        if (player == null)
            player = GetComponent<PlayerMovement>();
        if (player == null)
            return;

        if (gravewindArrowFrames == null || gravewindArrowFrames.Length == 0)
            gravewindArrowFrames = EnemyArrowAnimation.LoadFrames();

        Vector2 direction = GetSwordWaveDirection(player.VisualCenter);
        int combo = Mathf.Clamp(comboHit, 0, 2);
        player.PlayAbilitySfx(gravewindBasicArrowSfx, 0.68f + combo * 0.08f, 0.96f + combo * 0.04f);
        StartCoroutine(FireGravewindArrow(direction, 17f + combo * 1.5f, 0.78f,
            0.26f + combo * 0.025f, damage, false, false, 0.9f + combo * 0.08f));
    }

    private IEnumerator FireGravewindArrow(Vector2 direction, float speed, float duration, float radius,
        int damage, bool piercing, bool awardSkillCharge, float visualScale)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : player.FacingDirection;
        Vector3 position = player.VisualCenter + (Vector3)direction * 0.52f;
        GameObject arrow = CreateGravewindArrowVisual(position, direction, visualScale);
        HashSet<Enemy> enemies = new HashSet<Enemy>();
        HashSet<BossController> bosses = new HashSet<BossController>();
        HashSet<EnemyHealth> configured = new HashSet<EnemyHealth>();
        LayerMask wallLayers = EnemyCombat.ResolveWallLayers();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float step = speed * Time.deltaTime;
            RaycastHit2D wall = Physics2D.Raycast(position, direction, step, wallLayers);
            if (wall.collider != null && !wall.collider.isTrigger)
            {
                position = wall.point;
                break;
            }

            elapsed += Time.deltaTime;
            position += (Vector3)direction * step;
            if (arrow != null)
                arrow.transform.position = position;

            int hits = player.DamageEnemiesInRadius(position, radius, damage, enemies, bosses,
                playSwordImpact: false, alreadyHitConfiguredEnemies: configured,
                impactKind: CombatImpactKind.Arrow);
            if (hits > 0)
            {
                if (awardSkillCharge)
                    AddSkillHitCharge(7f * hits);
                if (!piercing)
                    break;
            }

            yield return null;
        }

        activeEffects.Remove(arrow);
        if (arrow != null)
            Destroy(arrow);
    }

    private GameObject CreateGravewindArrowVisual(Vector3 position, Vector2 direction, float scale)
    {
        if (gravewindArrowFrames == null || gravewindArrowFrames.Length == 0)
            return null;

        GameObject arrow = new GameObject("Gravewind Arrow");
        activeEffects.Add(arrow);
        arrow.transform.position = position;
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        arrow.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = gravewindArrowFrames[0];
        renderer.color = new Color(0.72f, 1f, 0.82f);
        if (playerRenderer != null)
        {
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + 3;
        }
        arrow.AddComponent<EnemyArrowAnimation>().SetFrames(gravewindArrowFrames);
        return arrow;
    }

    private Vector3 GetRangedTargetPoint(Vector3 origin, float maxDistance, float fallbackDistance)
    {
        Vector2 direction = GetSwordWaveDirection(origin);
        RaycastHit2D targetHit = Physics2D.Raycast(origin, direction, maxDistance, player.enemyLayers);
        if (targetHit.collider != null)
            return targetHit.collider.bounds.center;
        return origin + (Vector3)direction * Mathf.Min(maxDistance, fallbackDistance);
    }

    private IEnumerator CastPulseSkill(string castName, Color tint, Sprite[] frames, float[] radii, int[] damages,
        float interval, float scale, int bodyMotion, CombatImpactKind impactKind,
        bool ultimate = false, AudioClip abilitySfx = null)
    {
        BeginCast(castName);
        player.PlaySkillBodyMotion(bodyMotion, ultimate);
        player.PlayAbilitySfx(abilitySfx, ultimate ? 1f : 0.90f, 1f);
        Vector3 origin = player.VisualCenter;
        CreateTintedEffect(castName + " visual", frames, origin, scale, interval * (radii.Length + 1),
            Quaternion.identity, tint, smoothFrames: false, sortingOffset: ultimate ? -1 : 1);
        for (int i = 0; i < radii.Length; i++)
        {
            yield return new WaitForSeconds(interval);
            int hits = player.DamageEnemiesInRadius(origin, radii[i], damages[i], impactKind: impactKind);
            if (!ultimate && hits > 0) AddSkillHitCharge(8f * hits);
            StartCoroutine(ShakeMainCamera(0.06f, 0.035f + i * 0.01f));
        }
        EndCast(castName);
    }

    private GameObject CreateTintedEffect(string name, Sprite[] frames, Vector3 position, float scale, float duration,
        Quaternion rotation, Color tint, bool smoothFrames = false, int sortingOffset = 2)
    {
        return CreateEffect(name, frames, position, scale, duration, rotation, null, sortingOffset,
            default, smoothFrames, tint);
    }

    private IEnumerator CastSwordWave()
    {
        BeginCast("sword-wave");
        player.PlaySkillBodyMotion(1);
        player.PlayAbilitySfx(swordWaveSfx, volume: 0.95f, pitch: 1.05f);
        Vector3 bodyCenter = player.VisualCenter;
        Vector2 direction = GetSwordWaveDirection(bodyCenter);

        Vector3 bodyOffset = bodyCenter - transform.position;
        Vector3 position = bodyCenter + (Vector3)direction * 0.65f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        CreateEffect("Sword Wave Caster Aura", casterAuraFrames, bodyCenter, 2.15f, 0.42f,
            Quaternion.identity, transform, 1, bodyOffset);
        GameObject core = CreateEffect("Sword Wave Core", swordWaveCoreFrames, position, 1.6f, swordWaveDuration,
            rotation, null);
        if (core != null && playerRenderer != null)
            core.GetComponent<SpriteRenderer>().sortingOrder = playerRenderer.sortingOrder + 3;
        AssertCastVisualInvariant("sword-wave-effects-created");

        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
        HashSet<BossController> hitBosses = new HashSet<BossController>();
        HashSet<EnemyHealth> hitConfiguredEnemies = new HashSet<EnemyHealth>();
        float elapsed = 0f;

        while (elapsed < swordWaveDuration)
        {
            elapsed += Time.deltaTime;
            position += (Vector3)direction * swordWaveSpeed * Time.deltaTime;
            if (core != null)
                core.transform.position = position;

            int hitCount = player.DamageEnemiesInRadius(position, swordWaveRadius, swordWaveDamage,
                hitEnemies, hitBosses, alreadyHitConfiguredEnemies: hitConfiguredEnemies,
                playSwordImpact: true, impactKind: CombatImpactKind.SwordWave);
            if (hitCount > 0)
            {
                AddSkillHitCharge(15f * hitCount);
                if (core != null)
                    Destroy(core);
                CreateEffect("Sword Wave Impact", swordWaveImpactFrames, position, 1.1f, 0.3f, rotation, null);
                StartCoroutine(PlayImpactFeedback(0.12f, 0.075f, 0.045f));
                EndCast("sword-wave-hit");
                yield break;
            }

            yield return null;
        }

        EndCast("sword-wave-complete");
    }

    private IEnumerator PlayImpactFeedback(float shakeDuration, float shakeMagnitude, float hitStopDuration)
    {
        // A local cosmetic hit-stop must not pause the other LAN player's simulation.
        if (LanLaunch.Mode != LanMode.Solo) yield break;
        StartCoroutine(ShakeMainCamera(shakeDuration, shakeMagnitude));
        if (hitStopActive)
            yield break;

        hitStopActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Min(previousTimeScale, 0.04f);
        yield return new WaitForSecondsRealtime(hitStopDuration);
        if (!GameManager.isGamePaused && !GameManager.isGameOver && !GameManager.isWin)
            Time.timeScale = previousTimeScale;
        hitStopActive = false;
    }

    private static IEnumerator ShakeMainCamera(float duration, float magnitude)
    {
        if (LanLaunch.Mode != LanMode.Solo) yield break;
        Camera camera = Camera.main;
        if (camera == null)
            yield break;

        Transform cameraTransform = camera.transform;
        Vector3 originalPosition = cameraTransform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = magnitude * (1f - elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * strength;
            cameraTransform.localPosition = originalPosition + (Vector3)offset;
            yield return null;
        }

        if (cameraTransform != null)
            cameraTransform.localPosition = originalPosition;
    }

    private IEnumerator CastSpinSlash()
    {
        BeginCast("spin-slash");
        float castStartedAt = Time.time;
        player.PlaySkillBodyMotion(2);
        player.PlayAbilitySfx(spinSlashSfx, volume: 0.96f, pitch: 1f);
        Vector3 bodyCenter = player.VisualCenter;
        // Keep the slash at its cast origin. The visual tail is intentionally
        // longer than the movement lock; following the player made that tail
        // slide sideways as soon as movement became available again.
        CreateEffect("Spin Slash", spinSlashFrames, bodyCenter, 1.4f, spinSlashVfxDuration,
            Quaternion.identity, null, 2, smoothFrames: true);
        AssertCastVisualInvariant("spin-slash-effects-created");
        float[] pulseTimes = { 0.14f, 0.34f, 0.54f };
        int nextPulse = 0;
        float elapsed = 0f;

        while (elapsed < spinSlashDuration)
        {
            elapsed += Time.deltaTime;
            if (nextPulse < pulseTimes.Length && elapsed >= pulseTimes[nextPulse])
            {
                int hits = player.DamageEnemiesInRadius(transform.position, spinSlashRadius, spinSlashDamagePerPulse,
                    playSwordImpact: true, impactKind: CombatImpactKind.SpinSlash);
                if (hits > 0)
                    AddSkillHitCharge(8f * hits);
                nextPulse++;
            }
            yield return null;
        }

        // Do not reopen movement while the last slash frames are still on
        // screen; that was the remaining sideways slide at the end of E.
        float remainingVfxTime = spinSlashVfxDuration - (Time.time - castStartedAt);
        if (remainingVfxTime > 0f)
            yield return new WaitForSeconds(remainingVfxTime);
        EndCast("spin-slash-complete");
    }

    // Q/E charge R during their cast lock; R cannot charge itself.
    private void AddSkillHitCharge(float amount)
    {
        if (amount > 0f)
            ultimateCharge = Mathf.Clamp(ultimateCharge + amount, 0f, ultimateChargeRequired);
    }

    private IEnumerator CastAshenJudgment()
    {
        BeginCast("ashen-judgment");
        float castStartedAt = Time.time;
        player.PlaySkillBodyMotion(3, true);
        player.PlayAbilitySfx(ultimateChargeSfx, volume: 0.9f);
        Vector3 strikePosition = transform.position;
        Vector3 bodyCenter = player.VisualCenter;
        Vector3 bodyOffset = bodyCenter - transform.position;
        // Reuse Q's body aura for R's opening charge. It follows the locked
        // player only during the windup, then clears before the final impact.
        CreateEffect("Ashen Judgment Caster Aura", casterAuraFrames, bodyCenter, 2.15f, 0.42f,
            Quaternion.identity, transform, 1, bodyOffset);
        // R covers the whole damage radius, so all of its large silhouettes
        // render behind the player. The character remains readable even when
        // the central sword and impact reach their brightest frames.
        CreateEffect("Ashen Judgment Rune", runeFrames, strikePosition, 2.2f, 0.94f,
            Quaternion.identity, null, -2, smoothFrames: true);
        AssertCastVisualInvariant("ashen-judgment-effects-created");
        StartCoroutine(ShakeMainCamera(0.18f, 0.035f));
        yield return new WaitForSeconds(0.18f);
        CreateEffect("Ashen Judgment Blade Storm", ultimateSlashFrames, strikePosition, 1.65f, 0.76f,
            Quaternion.identity, null, -1, smoothFrames: true);

        for (int pulse = 0; pulse < 3; pulse++)
        {
            yield return new WaitForSeconds(0.16f);
            player.DamageEnemiesInRadius(strikePosition, ultimateRadius, ultimateSlashDamage,
                impactKind: CombatImpactKind.Ashen);
            StartCoroutine(ShakeMainCamera(0.07f, 0.045f));
        }

        float finalStartedAt = Time.time;
        CreateEffect("Ashen Judgment Final", ultimateImpactFrames, strikePosition, 1.75f, ultimateFinalVfxDuration,
            Quaternion.identity, null, -1, smoothFrames: true);
        player.PlayAbilitySfx(ultimateImpactSfx, volume: 1f);
        player.PlayAbilitySfx(ultimateImpactCrunchSfx, volume: 0.85f);
        player.DamageEnemiesInRadius(strikePosition, ultimateRadius * 1.15f, ultimateImpactDamage,
            impactKind: CombatImpactKind.Ashen);
        yield return StartCoroutine(PlayImpactFeedback(0.28f, 0.14f, 0.075f));

        // Keep the body pose and input lock in sync with the configured cast
        // duration instead of returning to locomotion midway through the VFX.
        float remainingCastTime = Mathf.Max(
            ultimateDuration - (Time.time - castStartedAt),
            ultimateFinalVfxDuration - (Time.time - finalStartedAt));
        if (remainingCastTime > 0f)
            yield return new WaitForSeconds(remainingCastTime);
        EndCast("ashen-judgment-complete");
    }

    private void BeginCast(string castName)
    {
        AssertCastVisualInvariant($"{castName}-before");
        isSkillCasting = true;
        AssertCastVisualInvariant($"{castName}-during");
    }

    private void EndCast(string castName)
    {
        isSkillCasting = false;
        AssertCastVisualInvariant($"{castName}-after");
    }

    private void AssertCastVisualInvariant(string phase)
    {
        if (player != null)
            player.AssertSingleAuthoritativePlayerVisual(phase);
    }

    private GameObject CreateEffect(string effectName, Sprite[] frames, Vector3 position, float scale,
        float duration, Quaternion rotation, Transform followTarget, int sortingOffset = 2,
        Vector3 followOffset = default, bool smoothFrames = false, Color? tint = null)
    {
        if (frames == null || frames.Length == 0)
            return null;

        GameObject effect = new GameObject(effectName);
        activeEffects.Add(effect);
        effect.transform.position = position;
        effect.transform.rotation = rotation;
        effect.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = tint ?? Color.white;
        Material vfxMaterial = GetVfxMaterial();
        if (vfxMaterial != null)
            renderer.sharedMaterial = vfxMaterial;
        if (playerRenderer != null)
        {
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + sortingOffset;
        }

        SpriteRenderer frameTrail = null;
        if (smoothFrames)
        {
            GameObject trailObject = new GameObject("Frame Trail");
            trailObject.transform.SetParent(effect.transform, false);
            frameTrail = trailObject.AddComponent<SpriteRenderer>();
            frameTrail.sprite = frames[0];
            frameTrail.color = Color.clear;
            frameTrail.sharedMaterial = renderer.sharedMaterial;
            frameTrail.sortingLayerID = renderer.sortingLayerID;
            frameTrail.sortingOrder = renderer.sortingOrder - 1;
        }

        WorldDepthSort.Attach(effect, effect.transform.position.y);
        StartCoroutine(AnimateEffect(effect, renderer, frameTrail, frames, duration,
            followTarget, followOffset, smoothFrames, tint ?? Color.white));
        return effect;
    }

    private IEnumerator AnimateEffect(GameObject effect, SpriteRenderer renderer, SpriteRenderer frameTrail,
        Sprite[] frames, float duration, Transform followTarget, Vector3 followOffset, bool smoothFrames, Color tint)
    {
        float elapsed = 0f;
        int previousFrameIndex = 0;
        while (effect != null && elapsed < duration)
        {
            if (followTarget != null)
            {
                // Reuse the centre offset captured when casting. Reading the
                // current sprite bounds here made the VFX wobble whenever an
                // action frame had different transparent margins.
                effect.transform.position = followTarget.position + followOffset;
            }

            float progress = Mathf.Clamp01(elapsed / duration);
            float framePosition = progress * frames.Length;
            int frameIndex = Mathf.Min(Mathf.FloorToInt(framePosition), frames.Length - 1);
            if (frameIndex != previousFrameIndex)
            {
                if (frameTrail != null)
                    frameTrail.sprite = renderer.sprite;
                renderer.sprite = frames[frameIndex];
                previousFrameIndex = frameIndex;
            }

            if (smoothFrames)
            {
                // A short envelope removes hard pop-in/out. The previous frame
                // lingers faintly behind the new one, bridging sparse 6-frame
                // sheets without blurring the main pixel-art silhouette.
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.24f));
                float envelope = Mathf.Min(fadeIn, fadeOut);
                renderer.color = new Color(tint.r, tint.g, tint.b, envelope * tint.a);

                if (frameTrail != null)
                {
                    float framePhase = framePosition - Mathf.Floor(framePosition);
                    // Keep just a hint of motion. At 30% this read as a
                    // duplicate body during close-range casts.
                    float trailAlpha = 0.10f * (1f - Mathf.Clamp01(framePhase / 0.72f)) * envelope;
                    frameTrail.color = new Color(tint.r, tint.g, tint.b, trailAlpha * tint.a);
                }
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        activeEffects.Remove(effect);
        if (effect != null)
            Destroy(effect);
    }

    private void OnDestroy()
    {
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null)
                Destroy(effect);
        }
        activeEffects.Clear();
    }

    /// <summary>
    /// Loads individual frame textures and builds each on a padded canvas.
    /// The transparent border and edge feather soften source pixels that touch
    /// an original frame edge while keeping the frame anchored consistently.
    /// </summary>
    internal static Sprite[] LoadPixelFrames(string resourcePath, int frameCount,
        FrameAnchorMode anchorMode = FrameAnchorMode.CellCenter, float pixelsPerUnit = 512f)
    {
        if (frameCount <= 0)
        {
            Debug.LogWarning($"Invalid frame count for ability VFX: {resourcePath}");
            return System.Array.Empty<Sprite>();
        }
        Sprite[] frames = new Sprite[frameCount];
        const int transparentPaddingPixels = 24;
        const int sourceEdgeFeatherPixels = 14;
        const byte visibleAlphaThreshold = 8;
        for (int frame = 0; frame < frameCount; frame++)
        {
            Texture2D source = Resources.Load<Texture2D>($"{resourcePath}/Frame_{frame:00}");
            if (source == null)
            {
                return CreateFallbackFrames(resourcePath, frameCount);
            }

            source.filterMode = FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;
            int sourceWidth = source.width;
            int sourceHeight = source.height;
            Color32[] pixels = source.GetPixels32();
            int paddedWidth = sourceWidth + transparentPaddingPixels * 2;
            int paddedHeight = sourceHeight + transparentPaddingPixels * 2;
            Color32[] paddedPixels = new Color32[paddedWidth * paddedHeight];

            bool hasLeftEdgePixels = false;
            bool hasRightEdgePixels = false;
            int minX = sourceWidth;
            int maxX = -1;
            int minY = sourceHeight;
            int maxY = -1;
            for (int y = 0; y < sourceHeight; y++)
            {
                int sourceRow = y * sourceWidth;
                hasLeftEdgePixels |= pixels[sourceRow].a >= visibleAlphaThreshold;
                hasRightEdgePixels |= pixels[sourceRow + sourceWidth - 1].a >= visibleAlphaThreshold;
                for (int x = 0; x < sourceWidth; x++)
                {
                    if (pixels[sourceRow + x].a < visibleAlphaThreshold)
                        continue;

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            for (int y = 0; y < sourceHeight; y++)
            {
                int sourceRow = y * sourceWidth;
                int targetRow = (y + transparentPaddingPixels) * paddedWidth + transparentPaddingPixels;
                for (int x = 0; x < sourceWidth; x++)
                {
                    Color32 pixel = pixels[sourceRow + x];
                    if (pixel.a > 0)
                    {
                        float alphaMultiplier = 1f;
                        if (hasLeftEdgePixels && x < sourceEdgeFeatherPixels)
                            alphaMultiplier = Mathf.Min(alphaMultiplier, x / (float)sourceEdgeFeatherPixels);
                        if (hasRightEdgePixels && sourceWidth - 1 - x < sourceEdgeFeatherPixels)
                            alphaMultiplier = Mathf.Min(alphaMultiplier,
                                (sourceWidth - 1 - x) / (float)sourceEdgeFeatherPixels);
                        pixel.a = (byte)Mathf.RoundToInt(pixel.a * alphaMultiplier);
                    }
                    paddedPixels[targetRow + x] = pixel;
                }
            }

            Texture2D paddedTexture = new Texture2D(paddedWidth, paddedHeight, TextureFormat.RGBA32, false)
            {
                name = $"{resourcePath}_{frame:00}_Padded",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            paddedTexture.SetPixels32(paddedPixels);
            paddedTexture.Apply(false, LanLaunch.Mode == LanMode.Solo);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (maxX >= minX)
            {
                float visualCenterX = transparentPaddingPixels + (minX + maxX + 1) * 0.5f;
                float visualCenterY = transparentPaddingPixels + (minY + maxY + 1) * 0.5f;
                if (anchorMode == FrameAnchorMode.VisualCenter || anchorMode == FrameAnchorMode.VisualCenterX)
                    pivot.x = visualCenterX / paddedWidth;
                if (anchorMode == FrameAnchorMode.VisualCenter)
                    pivot.y = visualCenterY / paddedHeight;
            }

            frames[frame] = Sprite.Create(paddedTexture,
                new Rect(0f, 0f, paddedWidth, paddedHeight), pivot,
                Mathf.Max(1f, pixelsPerUnit), 0, SpriteMeshType.FullRect);
            frames[frame].name = $"{resourcePath}_{frame:00}";
        }

        return frames;
    }

    /// <summary>
    /// Keeps gameplay readable when the optional art pack is not merged. The
    /// generated sprites are intentionally simple and are replaced automatically
    /// as soon as frame textures exist at the expected Resources path.
    /// </summary>
    private static Sprite[] CreateFallbackFrames(string resourcePath, int frameCount)
    {
        const int textureSize = 16;
        Sprite[] frames = new Sprite[frameCount];
        Color effectColor = resourcePath.Contains("Ashen")
            ? new Color(1f, 0.35f, 0.08f, 0.9f)
            : resourcePath.Contains("Spin")
                ? new Color(0.2f, 0.9f, 1f, 0.85f)
                : new Color(0.35f, 0.75f, 1f, 0.85f);

        for (int frame = 0; frame < frameCount; frame++)
        {
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = $"{resourcePath}_{frame:00}_Fallback",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[textureSize * textureSize];
            float radius = 3.5f + frame * 0.55f;
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= radius && distance >= Mathf.Max(0f, radius - 2.2f))
                        pixels[y * textureSize + x] = effectColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, LanLaunch.Mode == LanMode.Solo);
            frames[frame] = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize);
            frames[frame].name = $"{resourcePath}_{frame:00}_Fallback";
        }

        return frames;
    }

    internal static Material GetVfxMaterial()
    {
        if (pixelVfxMaterial != null)
            return pixelVfxMaterial;

        Shader shader = Resources.Load<Shader>("Combat/PixelVFX");
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            return null;

        pixelVfxMaterial = new Material(shader) { name = "Pixel Combat VFX" };
        return pixelVfxMaterial;
    }

}
