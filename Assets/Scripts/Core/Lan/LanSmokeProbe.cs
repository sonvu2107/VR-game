#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

/// <summary>Opt-in two-process integration probe, absent from release builds.</summary>
public sealed class LanSmokeProbe : MonoBehaviour
{
    public static LanSmokeProbe Active { get; private set; }
    private float clientStart;
    private bool sawQ, sawE, sawR, sawLights, sawBossArt, sawPower, sawPaused, sawLevel2, sawRestart, sawGameOver, sawRuntimePixels, sawRestoredHud;
    private Texture2D pixelFixture;
    private Sprite pixelFixtureSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lanSmoke") < 0) return;
        var go = new GameObject("LAN Smoke Probe");
        DontDestroyOnLoad(go);
        Active = go.AddComponent<LanSmokeProbe>();
    }
    private void Start() => StartCoroutine(Run());
    public LanInputFrame ClientInput()
    {
        if (clientStart == 0f) clientStart = Time.realtimeSinceStartup;
        float elapsed = Time.realtimeSinceStartup - clientStart;
        var frame = new LanInputFrame();
        if (elapsed < 2f) frame.movement = Vector2.right;
        if (elapsed > 0.6f && elapsed < 1.2f) { frame.attackHeld = true; frame.attackPressed = elapsed < 0.65f; }
        // Retry the intent until the Host confirms activation; Heavy Strike/skill
        // lock duration is gameplay configuration, not a fixed network-test delay.
        if (elapsed > 1.6f && !sawQ) frame.q = true;
        if (elapsed > 3.1f && sawQ && !sawE) frame.e = true;
        frame.powerHeld = elapsed > 5f && elapsed < 5.7f;
        if (elapsed > 5.9f && sawE && !sawR) frame.r = true;
        return frame;
    }
    private IEnumerator Run()
    {
        float timeout = Time.realtimeSinceStartup + 90f;
        while (LanSessionBridge.Active == null || !LanSessionBridge.Active.IsReady)
        {
            if (Time.realtimeSinceStartup > timeout) { Fail("connection timeout"); yield break; }
            yield return null;
        }
        LanSessionBridge session = LanSessionBridge.Active;
        GameManager manager = session.GetComponent<GameManager>();
        if (!session.HasStateAuthority)
        {
            while (Time.realtimeSinceStartup < timeout)
            {
                LanPlayerState state = session.LocalState;
                if (state != null)
                {
                    sawQ |= state.q > 0f; sawE |= state.e > 0f; sawPower |= state.power > 0f;
                    sawR |= sawE && state.ultimate < 0.5f;
                }
                sawPaused |= manager.CurrentState == GameState.Paused;
                sawGameOver |= manager.CurrentState == GameState.GameOver;
                if (!sawRestoredHud) sawRestoredHud = HasRestoredHud(manager, true);
                if (!sawRuntimePixels) sawRuntimePixels = HasRuntimePixelFixture();
                if (!sawBossArt)
                    foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                        if (renderer.name == "LAN/3D Ground Rune Circle" && renderer.sprite != null &&
                            renderer.sprite.texture.width > 1 && renderer.sprite.texture.isReadable) sawBossArt = true;
                if (!sawLights)
                    foreach (var light in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
                        if (light.name.StartsWith("LAN Light/") && light.isActiveAndEnabled && light.intensity > 0f) sawLights = true;
                sawLevel2 |= manager.CurrentLevel == 2;
                sawRestart |= sawLevel2 && manager.CurrentLevel == 1;
                if (manager.CurrentState == GameState.Victory)
                {
                    if (!sawQ || !sawE || !sawR || !sawLights || !sawBossArt || !sawPower || !sawPaused || !sawLevel2 || !sawRestart || !sawGameOver || !sawRuntimePixels || !sawRestoredHud || session.ReceivedSnapshots < 15)
                    { Fail($"replication q={sawQ} e={sawE} r={sawR} lights={sawLights} bossArt={sawBossArt} power={sawPower} pause={sawPaused} level2={sawLevel2} restart={sawRestart} gameover={sawGameOver} pixels={sawRuntimePixels}"); yield break; }
                    Debug.Log("LAN_CLIENT_SMOKE_PASS snapshots=" + session.ReceivedSnapshots + " runtimeTextures=" + session.ReplicatedRuntimeTextures + " runtimePixels=exact minimap=ok hearts=5 indicator=ok lights=ok bossArt=ok skills=Q/E/R/Space pause=ok level2=ok restart=ok gameover=ok victory=ok");
                    Application.Quit(0); yield break;
                }
                yield return null;
            }
            Fail("client replication timeout"); yield break;
        }
        HealthController[] players = FindObjectsByType<HealthController>(FindObjectsSortMode.None);
        if (players.Length != 2) { Fail("expected two Host actors"); yield break; }
        PlayerMovement guest = Array.Find(players, p => p.GetComponent<LanPlayerInput>() != null).GetComponent<PlayerMovement>();
        // Sample before yielding: the Client can already send input once its
        // world acknowledgement reaches the Host, before shader warm-up ends.
        Vector3 initial = guest.transform.position;
        yield return new WaitForSecondsRealtime(0.2f);
        if (!HasRestoredHud(manager, false)) { Fail("Host missing minimap, five hearts or direction indicator"); yield break; }
        // Stabilize fixture while exercising real input, colliders and ability controllers.
        FreezeEnemies();
        CreateRuntimePixelFixture(manager.PlayerMovement.transform.position);
        guest.GetComponent<HealthController>().ResetForLanRun();
        bool moved = false, hostQ = false, hostE = false, hostR = false, hostPower = false;
        PlayerSkillController guestSkills = guest.GetComponent<PlayerSkillController>();
        float inputDeadline = Time.realtimeSinceStartup + 25f;
        // Observe actual input effects instead of assuming a 0.5-second shader
        // warm-up. Slow graphics startup must not consume the measurement window.
        while (Time.realtimeSinceStartup < inputDeadline && !(moved && hostQ && hostE && hostR && hostPower))
        {
            moved |= Vector3.Distance(initial, guest.transform.position) > 0.03f;
            hostQ |= guestSkills.SwordWaveCooldownNormalized > 0f;
            hostE |= guestSkills.SpinSlashCooldownNormalized > 0f;
            hostR |= guestSkills.UltimateNormalized < 0.5f;
            hostPower |= guest.PowerUpCooldownRemaining > 0f;
            yield return null;
        }
        if (!moved || !hostQ || !hostE || !hostR || !hostPower)
        { Fail($"input effects movement={moved} Q={hostQ} E={hostE} R={hostR} Space={hostPower} initial={initial} current={guest.transform.position}"); yield break; }
        manager.Pause();
        yield return new WaitForSecondsRealtime(0.4f);
        manager.Resume();
        KillEnemies();
        manager.SetEnemyCount(0);
        yield return CollectKeysWithGuest(manager, guest);
        EnterPortal(guest);
        yield return new WaitForSecondsRealtime(0.8f);
        FreezeEnemies();
        if (manager.CurrentLevel != 2) { Fail("Host level advance"); yield break; }
        yield return new WaitForSecondsRealtime(0.5f);
        session.RestartRun();
        yield return new WaitForSecondsRealtime(0.9f);
        FreezeEnemies();
        if (manager.CurrentLevel != 1 || !session.HasGuest || !session.IsReady) { Fail("shared restart"); yield break; }
        // Verify one knockout is survivable, both knockouts cause shared GameOver.
        // Fixture leaves one half-heart; real damage still passes through the
        // new per-hit cap and invulnerability rules rather than bypassing them.
        guest.GetComponent<HealthController>().currentHealth = 1;
        guest.GetComponent<HealthController>().TakeDamage(1);
        if (manager.CurrentState == GameState.GameOver) { Fail("first knockout ended team run"); yield break; }
        manager.PlayerMovement.GetComponent<HealthController>().currentHealth = 1;
        manager.PlayerMovement.GetComponent<HealthController>().TakeDamage(1);
        if (manager.CurrentState != GameState.GameOver) { Fail("two knockouts did not end team run"); yield break; }
        yield return new WaitForSecondsRealtime(0.3f);
        session.RestartRun();
        yield return new WaitForSecondsRealtime(0.8f);
        for (int level = 1; level <= 10; level++)
        {
            FreezeEnemies();
            // Let a snapshot include the living boss rig before the fixture kills it.
            if (FindObjectOfType<Boss3DVisual>() != null) yield return new WaitForSecondsRealtime(0.25f);
            KillEnemies();
            manager.SetEnemyCount(0);
            yield return CollectKeysWithGuest(manager, guest);
            EnterPortal(guest);
            yield return new WaitForSecondsRealtime(0.65f);
        }
        if (manager.CurrentState != GameState.Victory) { Fail("10-level Victory"); yield break; }
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log("LAN_HOST_SMOKE_PASS actors=2 minimap=ok hearts=5 indicator=ok movement=ok pause=ok level2=ok restart=ok knockout=ok gameover=ok keys=ok levels=10 victory=ok");
        Application.Quit(0);
    }
    private static void FreezeEnemies()
    {
        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.enabled = false;
        foreach (BossController boss in FindObjectsByType<BossController>(FindObjectsSortMode.None)) boss.enabled = false;
        foreach (EnemyBrain enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        foreach (EnemyMovement movement in FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None)) movement.StopImmediately();
        foreach (EnemyCombat combat in FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None)) { combat.CancelAttack(); combat.enabled = false; }
        foreach (MiniBossController boss in FindObjectsByType<MiniBossController>(FindObjectsSortMode.None)) { boss.StopAllCoroutines(); boss.enabled = false; }
        foreach (SkeletonKingController boss in FindObjectsByType<SkeletonKingController>(FindObjectsSortMode.None)) { boss.StopAllCoroutines(); boss.enabled = false; }
    }
    private static void KillEnemies()
    {
        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.TakeDamage(100000);
        foreach (BossController boss in FindObjectsByType<BossController>(FindObjectsSortMode.None)) boss.TakeDamage(100000);
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.Die();
    }
    private static IEnumerator CollectKeysWithGuest(GameManager manager, PlayerMovement guest)
    {
        foreach (DungeonKey key in FindObjectsByType<DungeonKey>(FindObjectsSortMode.None))
        {
            if (manager.KeysCollected >= manager.KeysRequired) break;
            guest.transform.position = key.transform.position;
            guest.GetComponent<Rigidbody2D>().position = key.transform.position;
            Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.15f);
        }
        if (manager.KeysCollected < manager.KeysRequired) throw new InvalidOperationException("Guest failed to collect required keys.");
    }
    private static bool HasRestoredHud(GameManager manager, bool client)
    {
        HealthController health = manager.PlayerMovement.GetComponent<HealthController>();
        if (health.startHeart != 5 || health.MaxHealth != 10 || health.heartImages == null) return false;
        int visibleHearts = 0;
        foreach (var image in health.heartImages) if (image != null && image.enabled) visibleHearts++;
        if (visibleHearts != 5) return false;
        bool minimap = false;
        foreach (var image in FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None))
            if (image.texture is RenderTexture && image.enabled && image.gameObject.activeInHierarchy) minimap = true;
        if (!minimap || FindObjectOfType<MinimapFollow>() == null) return false;
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (renderer.enabled && renderer.sprite != null && renderer.gameObject.name.Contains("Nearest Enemy Direction") &&
                (!client || renderer.gameObject.name.StartsWith("LAN/", StringComparison.Ordinal))) return true;
        return false;
    }
    private static void EnterPortal(PlayerMovement guest)
    {
        FloorExit exit = FindObjectOfType<FloorExit>();
        if (exit == null || !exit.IsUnlocked) throw new InvalidOperationException("Expected unlocked portal.");
        guest.transform.position = exit.transform.position;
        guest.GetComponent<Rigidbody2D>().position = exit.transform.position;
        Physics2D.SyncTransforms();
    }
    private void CreateRuntimePixelFixture(Vector3 position)
    {
        pixelFixture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 0, 0, 255);
        pixels[31 + 31 * 32] = new Color32(0, 0, 255, 255);
        pixels[15 + 15 * 32] = new Color32(0, 255, 0, 255);
        pixelFixture.SetPixels32(pixels); pixelFixture.Apply();
        pixelFixtureSprite = Sprite.Create(pixelFixture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);
        var view = new GameObject("LAN Runtime Pixel Fixture");
        view.transform.SetParent(transform, false);
        view.transform.position = position + Vector3.up * 4f;
        view.AddComponent<SpriteRenderer>().sprite = pixelFixtureSprite;
    }
    private static bool HasRuntimePixelFixture()
    {
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            Texture2D texture = renderer.sprite != null ? renderer.sprite.texture : null;
            if (texture == null || !texture.isReadable || texture.width != 32 || texture.height != 32) continue;
            if (((Color32)texture.GetPixel(0, 0)).Equals(new Color32(255, 0, 0, 255)) &&
                ((Color32)texture.GetPixel(31, 31)).Equals(new Color32(0, 0, 255, 255)) &&
                ((Color32)texture.GetPixel(15, 15)).Equals(new Color32(0, 255, 0, 255))) return true;
        }
        return false;
    }
    private static void Fail(string message) { Debug.LogError("LAN_SMOKE_FAIL " + message); Application.Quit(2); }
    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (pixelFixtureSprite != null) Destroy(pixelFixtureSprite);
        if (pixelFixture != null) Destroy(pixelFixture);
    }
}
#endif
