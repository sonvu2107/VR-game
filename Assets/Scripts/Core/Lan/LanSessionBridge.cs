using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class LanSessionBridge : GameSessionBridge
{
    public static LanSessionBridge Active { get; internal set; }
    public override bool HasStateAuthority => LanLaunch.Mode != LanMode.Client;
    public bool HasGuest => guest != null;
    public bool IsReady => peerReady;
    public string Status { get; private set; }
    public LanPlayerState LocalState { get; private set; }
    public int ReceivedSnapshots { get; private set; }
    public int ReplicatedRuntimeTextures => world != null ? world.RuntimeTextureCount : 0;
    public int RunRevision => run;
    public int TeammateHealth => HasStateAuthority ? (guestHealth != null ? guestHealth.currentHealth : 0) : teammateHealth;

    private GameManager manager;
    private LanTransport transport;
    private LanAssetCatalog catalog;
    private LanWorldReplication world;
    private PlayerMovement guest;
    private HealthController guestHealth;
    private HealthController hostHealth;
    private LanPlayerInput guestInput;
    private LanInputFrame pendingInput;
    private bool previousAttack;
    private bool peerReady;
    private bool authenticated;
    private bool waitingForGuest = true;
    private int levelSeed;
    private int run = 1;
    private int clientRun;
    private int clientLevel;
    private int inputSequence;
    private int lastInputSequence;
    private float nextSend;
    private float lastInputAt;
    private float lastPacketAt;
    private bool leaving;
    private int teammateHealth;
    private AudioSource remoteAudio;
    private Transform teammateLight;
    private Vector3 lightOffset;
    private ScoreManager scoreManager;
    private DarknessController clientDarkness;
    private UnityEngine.Rendering.Universal.Light2D teammateDungeonLight;
    private UnityEngine.Rendering.Universal.Light2D guestDungeonLight;

    public void Initialize(GameManager gameManager)
    {
        Active = this;
        manager = gameManager;
        hostHealth = manager.PlayerMovement.GetComponent<HealthController>();
        scoreManager = FindObjectOfType<ScoreManager>();
        catalog = Resources.Load<LanAssetCatalog>("LanAssetCatalog");
        if (catalog == null) { Fail("Thiếu LAN catalog. Chọn Tools > The Labyrinth > Refresh LAN Catalog rồi build lại."); return; }
        catalog.Initialize();
        world = new LanWorldReplication(catalog, transform);
        manager.LevelChanged += OnLevelReady;
        manager.StateChanged += OnStateChanged;
        transport = new LanTransport();
        try
        {
            if (HasStateAuthority)
            {
                transport.Host(LanLaunch.Port);
                Status = "Đang chờ người chơi 2 · cổng " + LanLaunch.Port;
            }
            else
            {
                PrepareClient();
                transport.Join(LanLaunch.Address, LanLaunch.Port);
                Status = "Đang kết nối " + LanLaunch.Address + ":" + LanLaunch.Port;
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Fail(ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse
                ? "Cổng " + LanLaunch.Port + " đang được sử dụng. Chọn cổng khác."
                : "Không thể mở kết nối LAN: " + ex.Message);
        }
        catch (Exception ex) { Fail(ex.Message); }
        LanMenuUI.CreateSessionBar(this);
    }

    private void PrepareClient()
    {
        manager.PlayerMovement.enabled = false;
        foreach (MonoBehaviour script in manager.PlayerMovement.GetComponents<MonoBehaviour>())
            script.enabled = false;
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) renderer.enabled = false;
        var body = manager.PlayerMovement.GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;
        foreach (Collider2D collider in manager.PlayerMovement.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        var circle = FindObjectOfType<PowerupCircleController>();
        if (circle != null)
        {
            circle.enabled = false;
            foreach (Renderer renderer in circle.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }
        // Scene-authored animators must not produce a second body sprite.
        foreach (Animator animator in manager.PlayerMovement.GetComponentsInChildren<Animator>()) animator.enabled = false;
        PlayerSkillController skills = manager.PlayerMovement.GetComponent<PlayerSkillController>();
        if (skills == null) skills = manager.PlayerMovement.gameObject.AddComponent<PlayerSkillController>();
        skills.enabled = false;
        SkillHudController hud = manager.PlayerMovement.GetComponent<SkillHudController>();
        if (hud == null) hud = manager.PlayerMovement.gameObject.AddComponent<SkillHudController>();
        hud.enabled = true;
        var light = manager.PlayerMovement.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        if (light != null)
        {
            lightOffset = light.transform.position - manager.PlayerMovement.transform.position;
            teammateLight = Instantiate(light.gameObject, transform).transform;
            teammateLight.name = "LAN Teammate Light";
        }
    }

    private void Update()
    {
        if (transport == null || leaving) return;
        while (transport.TryEvent(out string value))
        {
            if (value == "connected")
            {
                lastPacketAt = Time.realtimeSinceStartup;
                authenticated = peerReady = false;
                lastInputSequence = 0;
                world.ResetPeerCache();
                if (!HasStateAuthority) Send(new LanMessage { type = "hello", catalog = catalog.fingerprint });
            }
            else if (value == "disconnected")
            {
                if (!HasStateAuthority) { Fail("Mất kết nối với Host."); return; }
                RemoveGuest();
                waitingForGuest = true;
                if (manager.CurrentState == GameState.Playing) manager.Pause();
                Status = "Người chơi 2 đã rời phòng. Đang chờ kết nối lại.";
            }
            else if (value.StartsWith("error:")) { Fail(value.Substring(6)); return; }
        }
        int processed = 0;
        while (processed++ < 128 && transport.TryReceive(out string json))
        {
            try
            {
                if (json.Length > LanTransport.MaxMessageBytes) throw new System.IO.InvalidDataException();
                LanMessage message = JsonUtility.FromJson<LanMessage>(json);
                if (message == null) throw new System.IO.InvalidDataException();
                lastPacketAt = Time.realtimeSinceStartup;
                if (HasStateAuthority) HandleHostMessage(message);
                else HandleClientMessage(message);
            }
            catch (Exception ex) { Fail("Dữ liệu LAN không hợp lệ: " + ex.Message); return; }
        }
        if (transport.Connected && Time.realtimeSinceStartup - lastPacketAt > 12f)
        { transport.DisconnectPeer(); return; }

        if (HasStateAuthority)
        {
            if (waitingForGuest && manager.CurrentState == GameState.Playing) manager.Pause();
            if (guestInput != null && Time.realtimeSinceStartup - lastInputAt > 0.5f) guestInput.ResetInput();
            if (peerReady && Time.realtimeSinceStartup >= nextSend)
            {
                nextSend = Time.realtimeSinceStartup + 1f / 15f;
                SendSnapshot();
            }
            else if (authenticated && !peerReady && Time.realtimeSinceStartup >= nextSend)
            {
                nextSend = Time.realtimeSinceStartup + 1f;
                Send(new LanMessage { type = "heartbeat" });
            }
        }
        else if (authenticated)
        {
            SampleInput();
            if (Time.realtimeSinceStartup >= nextSend)
            {
                nextSend = Time.realtimeSinceStartup + 1f / 30f;
                Send(new LanMessage { type = "input", run = clientRun, level = clientLevel, sequence = ++inputSequence, input = pendingInput });
                pendingInput.ClearEdges();
            }
        }
    }

    private void LateUpdate()
    {
        if (!HasStateAuthority && world != null) world.Interpolate();
    }

    private void HandleHostMessage(LanMessage message)
    {
        if (message.type == "hello")
        {
            if (authenticated) return;
            if (message.protocol != 1 || message.catalog != catalog.fingerprint)
            {
                Send(new LanMessage { type = "error", error = "Hai máy cần dùng cùng một bản build." });
                Status = "Bản build của người tham gia không tương thích.";
                return;
            }
            authenticated = true;
            Send(new LanMessage { type = "welcome", catalog = catalog.fingerprint });
            SpawnGuest();
            if (manager.CurrentState != GameState.Generating) SendWorld();
            return;
        }
        if (!authenticated) return;
        if (message.type == "ready" && message.run == run && message.level == manager.CurrentLevel)
        {
            peerReady = true;
            if (waitingForGuest && manager.CurrentState == GameState.Paused) manager.Resume();
            waitingForGuest = false;
            Status = "LAN · Host · 2/2 người chơi";
            SendSnapshot();
        }
        else if (message.type == "input" && peerReady && message.run == run && message.level == manager.CurrentLevel &&
                 message.sequence > lastInputSequence && message.input.IsValid)
        {
            lastInputSequence = message.sequence;
            lastInputAt = Time.realtimeSinceStartup;
            if (manager.CurrentState == GameState.Playing && guestHealth != null && guestHealth.currentHealth > 0)
                guestInput.Apply(message.input);
            else guestInput?.ResetInput();
        }
        else if (message.type == "pause" && peerReady) manager.Pause();
        else if (message.type == "resume" && peerReady) manager.Resume();
        // Client outcomes (damage, health, position, victory, restart, advance) are never accepted.
    }

    private void HandleClientMessage(LanMessage message)
    {
        if (message.type == "error") { Fail(message.error); return; }
        if (message.type == "welcome")
        {
            if (message.protocol != 1 || message.catalog != catalog.fingerprint) { Fail("Hai máy cần dùng cùng một bản build."); return; }
            authenticated = true;
            Status = "Đã kết nối. Đang nhận bản đồ từ Host…";
        }
        else if (authenticated && message.type == "world")
        {
            if (clientRun != message.run) scoreManager?.ResetLanRun();
            clientRun = message.run; clientLevel = message.level;
            pendingInput = default; previousAttack = false;
            world.ApplyMap(message.maps);
            ApplySnapshot(message);
            ApplyClientLighting();
            peerReady = true;
            Send(new LanMessage { type = "ready", run = clientRun, level = clientLevel });
            Status = "LAN · Người chơi 2 · 2/2 người chơi";
        }
        else if (authenticated && message.type == "snapshot" && message.run == clientRun && message.level == clientLevel)
            ApplySnapshot(message);
        else if (authenticated && message.type == "sound" && message.audio >= 0 &&
                 catalog.audioClips != null && message.audio < catalog.audioClips.Length)
        {
            if (remoteAudio == null) { remoteAudio = gameObject.AddComponent<AudioSource>(); remoteAudio.playOnAwake = false; }
            remoteAudio.pitch = Mathf.Clamp(message.pitch, 0.5f, 2f);
            remoteAudio.PlayOneShot(catalog.audioClips[message.audio], Mathf.Clamp01(message.volume));
        }
    }

    private void ApplySnapshot(LanMessage message)
    {
        if (!Enum.IsDefined(typeof(GameState), message.state) || message.level < 1 || message.level > manager.TotalLevels ||
            message.players == null || message.players.Length != 2 || message.visuals == null || message.visuals.Length > 4096)
            throw new System.IO.InvalidDataException("Invalid LAN state.");
        world.ApplyVisuals(message);
        LocalState = message.players[1];
        teammateHealth = message.players[0].health;
        if (teammateLight != null) teammateLight.position = message.players[0].position + lightOffset;
        manager.PlayerMovement.transform.position = LocalState.position;
        hostHealth.ApplyLanHealth(LocalState.health, LocalState.hearts);
        scoreManager?.ApplyLanScore(message.score);
        manager.ApplyLanState(message.level, (GameState)message.state, message.enemies,
            message.keys, message.requiredKeys, message.timeRemaining);
        if (teammateDungeonLight != null) teammateDungeonLight.transform.position = message.players[0].position;
        PowerupController power = manager.PlayerMovement.GetComponent<PowerupController>();
        if (LocalState.power > 0f) power.UpdateCooldown(LocalState.power); else power.ShowPoweredUp();
        ReceivedSnapshots++;
    }

    private void SampleInput()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (LanSmokeProbe.Active != null)
        {
            pendingInput.MergeEdges(LanSmokeProbe.Active.ClientInput());
            return;
        }
#endif
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad gamepad = Gamepad.current;
        Vector2 movement = Vector2.zero;
        bool held = false;
        LanInputFrame frame = default;
        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            held = keyboard.leftAltKey.isPressed;
            frame.powerHeld = keyboard.spaceKey.isPressed;
            frame.dash = keyboard.leftShiftKey.wasPressedThisFrame;
            frame.q = keyboard.qKey.wasPressedThisFrame; frame.e = keyboard.eKey.wasPressedThisFrame; frame.r = keyboard.rKey.wasPressedThisFrame;
        }
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > movement.sqrMagnitude) movement = stick;
            held |= gamepad.rightTrigger.isPressed;
            frame.dash |= gamepad.rightShoulder.wasPressedThisFrame;
        }
        held |= mouse != null && mouse.leftButton.isPressed;
        frame.movement = Vector2.ClampMagnitude(movement, 1f);
        frame.attackHeld = held; frame.attackPressed = held && !previousAttack;
        previousAttack = held;
        if (manager.CurrentState != GameState.Playing || LocalState == null || LocalState.health <= 0)
            frame = default;
        pendingInput.MergeEdges(frame);
    }

    private void SpawnGuest()
    {
        if (guest != null) return;
        GameObject copy = Instantiate(manager.PlayerMovement.gameObject);
        copy.name = "LAN Player 2";
        Vector3 scale = copy.transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        copy.transform.localScale = scale;
        guest = copy.GetComponent<PlayerMovement>();
        guest.enabled = false;
        guestInput = copy.AddComponent<LanPlayerInput>();
        guestHealth = copy.GetComponent<HealthController>();
        guestHealth.gameManager = manager;
        guestHealth.heartImages = Array.Empty<UnityEngine.UI.Image>();
        var hud = copy.GetComponent<SkillHudController>();
        if (hud != null) { hud.enabled = false; Destroy(hud); }
        // Clone only the scene-authored circle, not the first player's runtime VFX children.
        foreach (Transform child in copy.transform)
            if (child.name.Contains("VFX") || child.name.Contains("Slash")) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        if (copy.GetComponentInChildren<PowerupCircleController>() == null)
        {
            PowerupCircleController original = manager.PlayerMovement.GetComponentInChildren<PowerupCircleController>();
            if (original == null) original = FindObjectOfType<PowerupCircleController>();
            if (original != null)
            {
                var circle = Instantiate(original.gameObject, copy.transform);
                circle.transform.localPosition = manager.PlayerMovement.transform.InverseTransformPoint(original.transform.position);
            }
        }
        foreach (Collider2D a in copy.GetComponentsInChildren<Collider2D>())
            foreach (Collider2D b in manager.PlayerMovement.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(a, b);
        PositionGuest();
        guest.enabled = manager.CurrentState == GameState.Playing;
        Debug.Log("LAN_GUEST_SPAWNED");
    }

    private void PositionGuest()
    {
        if (guest == null) return;
        // Both bodies start in the valid spawn cell; player/player collision is ignored.
        guest.transform.position = manager.PlayerMovement.transform.position;
        var body = guest.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = guest.transform.position; body.velocity = Vector2.zero; }
        guestInput.ResetInput();
    }

    private void RemoveGuest()
    {
        authenticated = peerReady = false;
        if (guest != null) Destroy(guest.gameObject);
        guest = null; guestHealth = null; guestInput = null;
        if (hostHealth.currentHealth <= 0 && manager.CurrentState != GameState.Victory) manager.GameOver();
    }

    private void OnLevelReady(int level, int total)
    {
        if (!HasStateAuthority) return;
        PositionGuest();
        // A knocked-out player returns with full health when the team reaches the next level.
        if (HasGuest)
        {
            hostHealth.ResetForLanRun();
            guestHealth.ResetForLanRun();
            float radius = manager.CurrentLevelDefinition.DarknessRadius;
            ConfigurePointLight(ref guestDungeonLight, guest.transform, radius);
            manager.PlayerMovement.enabled = true;
            guest.enabled = true;
        }
        if (authenticated) SendWorld();
    }

    private void OnStateChanged(GameState state)
    {
        if (guest == null) return;
        guest.enabled = state == GameState.Playing && guestHealth.currentHealth > 0;
        if (state != GameState.Playing) guestInput.ResetInput();
    }

    private LanPlayerState CapturePlayer(PlayerMovement player)
    {
        if (player == null) return new LanPlayerState();
        HealthController health = player.GetComponent<HealthController>();
        PlayerSkillController skills = player.GetComponent<PlayerSkillController>();
        return new LanPlayerState
        {
            position = player.transform.position, health = health.currentHealth, hearts = health.startHeart,
            dash = player.DashCooldownNormalized, power = player.PowerUpCooldownRemaining,
            q = skills != null ? skills.SwordWaveCooldownNormalized : 0f,
            e = skills != null ? skills.SpinSlashCooldownNormalized : 0f,
            ultimate = skills != null ? skills.UltimateNormalized : 0f
        };
    }

    private LanMessage Capture(string type)
    {
        var message = new LanMessage
        {
            type = type, run = run, level = manager.CurrentLevel, total = manager.TotalLevels, seed = levelSeed,
            state = (int)manager.CurrentState, enemies = manager.enemyCount,
            keys = manager.KeysCollected, requiredKeys = manager.KeysRequired, timeRemaining = manager.LevelTimeRemaining,
            score = scoreManager != null ? scoreManager.CurrentScore : 0,
            players = new[] { CapturePlayer(manager.PlayerMovement), CapturePlayer(guest) }
        };
        world.CaptureVisuals(message);
        return message;
    }

    private void SendWorld()
    {
        peerReady = false;
        guestInput?.ResetInput();
        LanMessage message = Capture("world");
        message.maps = world.CaptureMap();
        Send(message);
    }
    private void SendSnapshot() => Send(Capture("snapshot"));
    private void Send(LanMessage message) => transport?.Send(JsonUtility.ToJson(message));
    public override void PublishLevel(int currentLevel, int totalLevels, int seed) => levelSeed = seed;
    public override void PublishState(GameState state) { }
    public override void RequestLevelAdvance(int currentLevel) { /* Only Host physics can authorize an exit. */ }
    public bool SendClientControl(string command)
    {
        if (HasStateAuthority) return false;
        if (peerReady) Send(new LanMessage { type = command });
        return true;
    }

    public HealthController NearestAlivePlayer(Vector3 position)
    {
        HealthController nearest = hostHealth != null && hostHealth.currentHealth > 0 ? hostHealth : null;
        if (guestHealth != null && guestHealth.currentHealth > 0 &&
            (nearest == null || (guestHealth.transform.position - position).sqrMagnitude < (nearest.transform.position - position).sqrMagnitude))
            nearest = guestHealth;
        return nearest;
    }
    public void PlayerDied(HealthController player)
    {
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<PlayerMovement>().CancelLanActions();
        if (NearestAlivePlayer(player.transform.position) == null) manager.GameOver();
    }
    public void RestartRun()
    {
        if (!HasStateAuthority) return;
        run++;
        hostHealth.ResetForLanRun(); guestHealth?.ResetForLanRun();
        manager.PlayerMovement.ResetForLanRun(); guest?.ResetForLanRun();
        scoreManager?.ResetLanRun();
        peerReady = false;
        guestInput?.ResetInput();
        manager.RestartLanCampaign();
    }
    public void PublishSound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (!HasStateAuthority || !peerReady || clip == null) return;
        int id = catalog.Id(clip);
        if (id >= 0) Send(new LanMessage { type = "sound", audio = id, volume = volume, pitch = pitch });
    }
    public void Leave()
    {
        leaving = true;
        transport?.Dispose();
        Time.timeScale = 1f;
        LanLaunch.Configure(LanMode.Solo);
        SceneManager.LoadScene("StartMenu");
    }
    private void Fail(string error)
    {
        Debug.LogWarning("LAN: " + error);
        LanLaunch.LastError = error;
        leaving = true;
        transport?.Dispose();
        Time.timeScale = 1f;
        LanLaunch.Configure(LanMode.Solo);
        LanLaunch.LastError = error;
        SceneManager.LoadScene("StartMenu");
    }
    private void OnDestroy()
    {
        if (manager != null) { manager.LevelChanged -= OnLevelReady; manager.StateChanged -= OnStateChanged; }
        transport?.Dispose(); world?.Dispose();
        if (guest != null) Destroy(guest.gameObject);
        if (Active == this) Active = null;
        Time.timeScale = 1f;
    }

    private void ApplyClientLighting()
    {
        float radius = manager.CurrentLevelDefinition.DarknessRadius;
        if (clientDarkness == null) clientDarkness = gameObject.AddComponent<DarknessController>();
        if (radius > 0f) clientDarkness.EnableDarkness(manager.PlayerMovement.transform, radius);
        else clientDarkness.DisableDarkness();
        ConfigurePointLight(ref teammateDungeonLight, transform, radius);
    }

    private static void ConfigurePointLight(ref UnityEngine.Rendering.Universal.Light2D light, Transform parent, float radius)
    {
        if (light == null && radius > 0f)
        {
            var lightObject = new GameObject("LAN Dungeon Light");
            lightObject.transform.SetParent(parent, false);
            light = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        }
        if (light == null) return;
        light.enabled = radius > 0f;
        light.pointLightOuterRadius = Mathf.Max(0.01f, radius);
        light.pointLightInnerRadius = Mathf.Max(0.01f, radius * 0.35f);
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.87f, 0.66f, 1f);
    }
}
