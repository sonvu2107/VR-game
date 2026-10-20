using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Owns the dungeon run state and multi-level campaign progression.</summary>
public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public GameObject winUI;
    public TextMeshProUGUI EnemyCounter;
    public PlayerMovement PlayerMovement;
    public InfiniteWorldGenerator dungeonGenerator;
    [SerializeField] private GameSessionBridge sessionBridge;

    [Header("Level Progression")]
    [SerializeField] private List<LevelDefinition> levels = new();
    [SerializeField] private int baseSeed = 12345;
    [SerializeField, Min(1)] private int levelSeedStep = 1009;
    [SerializeField, Min(0f)] private float levelTransitionDelay = 0.35f;

    public static bool isGamePaused;
    public static bool isGameOver;
    public static bool isWin;
    public static bool isStoryPlaying;

    public int enemyCount;
    public GameState CurrentState { get; private set; } = GameState.Generating;
    public int CurrentLevel { get; private set; } = 1;
    public int TotalLevels => levels?.Count ?? 0;
    public LevelDefinition CurrentLevelDefinition => GetLevelDefinition(CurrentLevel);
    public int KeysCollected { get; private set; }
    public int KeysRequired { get; private set; }
    public float LevelTimeRemaining { get; private set; }
    public bool HasLevelTimeLimit => CurrentLevelDefinition?.HasTimeLimit == true;
    public LevelOneStory Story { get; private set; }

    [Obsolete("Use CurrentLevel instead.")]
    public int CurrentFloor => CurrentLevel;

    [Obsolete("Use TotalLevels instead.")]
    public int TotalFloors => TotalLevels;

    public event Action<GameState> StateChanged;
    public event Action<int, int> LevelChanged;

    [Obsolete("Use LevelChanged instead.")]
    public event Action<int, int> FloorChanged;

    private PlayerControls playerControls;
    private InputAction pauseAction;
    private FloorExit currentExit;
    private Coroutine generationRoutine;
    private Coroutine transitionRoutine;
    private bool advanceRequestPending;
    private int lastDisplayedSecond = -1;

    private void Awake()
    {
        EnsurePlayerControls();
        enemyCount = 0;

        if (PlayerMovement == null)
            PlayerMovement = FindObjectOfType<PlayerMovement>();

        if (dungeonGenerator == null)
            dungeonGenerator = FindObjectOfType<InfiniteWorldGenerator>();

        if (sessionBridge == null)
            sessionBridge = GetComponent<GameSessionBridge>();

        EnsureLevelDefinitions();
        Story = gameObject.AddComponent<LevelOneStory>();
        Story.Initialize(this);
        if (LanLaunch.Mode != LanMode.Solo)
        {
            LanSessionBridge lan = GetComponent<LanSessionBridge>();
            if (lan == null) lan = gameObject.AddComponent<LanSessionBridge>();
            sessionBridge = lan;
            lan.Initialize(this);
        }
    }

    private void OnEnable()
    {
        EnsurePlayerControls();
        pauseAction = playerControls.Player.Pause;
        pauseAction.Enable();
        EnemySignals.EnemyDied += OnConfiguredEnemyDied;
        EnemySignals.MiniBossDefeated += OnConfiguredEnemyDied;
        EnemySignals.BossDefeated += OnConfiguredEnemyDied;
    }

    private void OnDisable()
    {
        pauseAction?.Disable();
        EnemySignals.EnemyDied -= OnConfiguredEnemyDied;
        EnemySignals.MiniBossDefeated -= OnConfiguredEnemyDied;
        EnemySignals.BossDefeated -= OnConfiguredEnemyDied;
    }

    private void OnConfiguredEnemyDied(EnemyHealth enemy)
    {
        Story?.RecordGuardianDeath(enemy);
        if (enemy != null && enemy.CountsForLegacyCounter && CurrentState == GameState.Playing)
            EnemyDefeated();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        CurrentLevel = Mathf.Clamp(LanLaunch.StartingLevel, 1, TotalLevels);
        SetState(GameState.Generating);
        if (sessionBridge == null || sessionBridge.HasStateAuthority)
            BeginLevelGeneration();
    }

    private void Update()
    {
        if (pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            if (CurrentState == GameState.Playing)
                Pause();
            else if (CurrentState == GameState.Paused)
                Resume();
        }

        if (CurrentState is GameState.GameOver or GameState.Paused)
        {
            bool rPressed = (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
            try { if (!rPressed) rPressed = Input.GetKeyDown(KeyCode.R); } catch {}
            if (rPressed && CurrentState == GameState.GameOver)
            {
                RestartGame();
            }
        }

        TickLevelTimer();
    }

    public void Resume()
    {
        if (LanSessionBridge.Active != null && LanSessionBridge.Active.SendClientControl("resume")) return;
        if (CurrentState != GameState.Paused && CurrentState != GameState.WaitingForPartner)
            return;

        SetState(GameState.Playing);
    }

    public void WaitForPartner()
    {
        if (sessionBridge != null && sessionBridge.HasStateAuthority && CurrentState == GameState.Playing)
            SetState(GameState.WaitingForPartner);
    }

    public void Pause()
    {
        if (LanSessionBridge.Active != null && LanSessionBridge.Active.SendClientControl("pause")) return;
        if (CurrentState != GameState.Playing)
            return;

        SetState(GameState.Paused);
    }

    public void GameOver()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return;
        if (CurrentState is GameState.GameOver or GameState.Victory)
            return;

        SetState(GameState.GameOver);
    }

    public void Win()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return;
        if (CurrentState == GameState.Victory)
            return;

        SetState(GameState.Victory);
    }

    public void SetEnemyCount(int count)
    {
        enemyCount = Mathf.Max(0, count);
        UpdateCounter();
        RefreshExitState();
    }

    public void EnemyDefeated()
    {
        if (CurrentState is GameState.GameOver or GameState.Victory)
            return;

        Story?.Record(LevelOneStory.Beat.Kill);
        enemyCount = Mathf.Max(0, enemyCount - 1);
        UpdateCounter();
        RefreshExitState();
    }

    public void CompleteGeneration(FloorExit floorExit, int spawnedEnemyCount)
    {
        CompleteLevelGeneration(floorExit, spawnedEnemyCount);
    }

    public void CompleteLevelGeneration(FloorExit levelExit, int spawnedEnemyCount)
    {
        if (CurrentState != GameState.Generating)
            return;

        currentExit = levelExit;
        enemyCount = Mathf.Max(0, spawnedEnemyCount);
        LevelDefinition definition = CurrentLevelDefinition;
        KeysCollected = 0;
        KeysRequired = definition?.RequiredKeys ?? 0;
        LevelTimeRemaining = definition?.TimeLimitSeconds ?? 0f;
        lastDisplayedSecond = -1;
        advanceRequestPending = false;
        HealthController health = PlayerMovement != null ? PlayerMovement.GetComponent<HealthController>() : null;
        health?.RestoreFullHealth();
        Story.PrepareLevel(CurrentLevel);
        SetState(GameState.Playing);
        if (CurrentLevel == 1) Story.BeginOpening();
        LevelChanged?.Invoke(CurrentLevel, TotalLevels);
        FloorChanged?.Invoke(CurrentLevel, TotalLevels);
        RefreshExitState();
    }

    public void GenerationFailed()
    {
        Debug.LogError($"Level {CurrentLevel} could not be generated.");
        SetState(GameState.GameOver);
    }

    public void TryAdvanceLevel()
    {
        if (CurrentState != GameState.Playing ||
            !LevelCompletionRules.AreRequirementsMet(enemyCount, KeysCollected, KeysRequired) ||
            advanceRequestPending)
            return;

        advanceRequestPending = true;
        if (sessionBridge != null && !sessionBridge.HasStateAuthority)
        {
            sessionBridge.RequestLevelAdvance(CurrentLevel);
            return;
        }

        AuthorizeLevelAdvance(CurrentLevel);
    }

    public void AuthorizeLevelAdvance(int completedLevel)
    {
        if (CurrentState != GameState.Playing ||
            !LevelCompletionRules.AreRequirementsMet(enemyCount, KeysCollected, KeysRequired) ||
            completedLevel != CurrentLevel)
        {
            advanceRequestPending = false;
            return;
        }

        if (CurrentLevel >= TotalLevels)
        {
            Story.BeginEnding();
            return;
        }

        SetState(GameState.LevelCompleted);
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(AdvanceLevelRoutine());
    }

    [Obsolete("Use TryAdvanceLevel instead.")]
    public void TryAdvanceFloor()
    {
        TryAdvanceLevel();
    }

    public void RestartGame()
    {
        if (LanSessionBridge.Active != null)
        {
            if (sessionBridge.HasStateAuthority) LanSessionBridge.Active.RestartRun();
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    public void UpdateCounter()
    {
        if (EnemyCounter == null)
            return;

        if (CurrentState == GameState.Generating)
        {
            EnemyCounter.text = $"Level {CurrentLevel}/{TotalLevels} - Generating...";
            return;
        }

        string counter = $"Level {CurrentLevel}/{TotalLevels} - Enemies Remaining: {enemyCount}";
        if (KeysRequired > 0)
            counter += $" | Keys: {KeysCollected}/{KeysRequired}";

        if (HasLevelTimeLimit)
        {
            int secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(LevelTimeRemaining));
            int minutes = secondsRemaining / 60;
            int seconds = secondsRemaining % 60;
            counter += $" | Time: {minutes:00}:{seconds:00}";
        }

        EnemyCounter.text = counter;
    }

    private void BeginLevelGeneration()
    {
        if (generationRoutine != null)
            StopCoroutine(generationRoutine);

        generationRoutine = StartCoroutine(GenerateLevelRoutine());
    }

    private IEnumerator GenerateLevelRoutine()
    {
        currentExit = null;
        enemyCount = 0;
        KeysCollected = 0;
        KeysRequired = 0;
        LevelTimeRemaining = 0f;
        lastDisplayedSecond = -1;
        UpdateCounter();

        // Let the previous physics frame finish before clearing its dungeon.
        yield return null;

        if (dungeonGenerator == null)
        {
            Debug.LogError("InfiniteWorldGenerator is not assigned.");
            GenerationFailed();
            yield break;
        }

        LevelDefinition definition = CurrentLevelDefinition;
        if (definition == null)
        {
            Debug.LogError($"Level {CurrentLevel} has no configuration.");
            GenerationFailed();
            yield break;
        }

        int levelSeed = LevelSeedUtility.Calculate(baseSeed, levelSeedStep, CurrentLevel);
        sessionBridge?.PublishLevel(CurrentLevel, TotalLevels, levelSeed);
        if (!dungeonGenerator.GenerateLevel(definition, TotalLevels, levelSeed))
            GenerationFailed();

        generationRoutine = null;
    }

    private IEnumerator AdvanceLevelRoutine()
    {
        if (levelTransitionDelay > 0f)
            yield return new WaitForSecondsRealtime(levelTransitionDelay);

        CurrentLevel++;
        advanceRequestPending = false;
        transitionRoutine = null;
        SetState(GameState.Generating);
        BeginLevelGeneration();
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        isStoryPlaying = newState == GameState.Story;
        isGamePaused = newState is GameState.Paused or GameState.Story or GameState.WaitingForPartner;
        isGameOver = newState == GameState.GameOver;
        isWin = newState == GameState.Victory;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(newState == GameState.Paused);
        if (gameOverUI != null)
            gameOverUI.SetActive(newState == GameState.GameOver);
        if (winUI != null)
            winUI.SetActive(newState == GameState.Victory);

        Time.timeScale = newState is GameState.Paused or GameState.Story or GameState.WaitingForPartner or GameState.GameOver or GameState.Victory ? 0f : 1f;

        if (PlayerMovement != null)
            PlayerMovement.enabled = newState == GameState.Playing &&
                (sessionBridge == null || sessionBridge.HasStateAuthority) &&
                (PlayerMovement.GetComponent<HealthController>() == null ||
                 PlayerMovement.GetComponent<HealthController>().currentHealth > 0);

        UpdateCounter();
        StateChanged?.Invoke(newState);
        sessionBridge?.PublishState(newState);
    }

    private void RefreshExitState()
    {
        currentExit?.SetUnlocked(CurrentState == GameState.Playing &&
                                 LevelCompletionRules.AreRequirementsMet(
                                     enemyCount, KeysCollected, KeysRequired));
    }

    public void BeginStoneStory()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return;
        if (CurrentState == GameState.Playing) SetState(GameState.Story);
    }

    public void FinishOpeningStory()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return;
        if (CurrentState != GameState.Story || !Story.IsComplete) return;
        if (LanSessionBridge.Active != null && !LanSessionBridge.Active.IsReady) return;
        if (Story.Ending) { Win(); return; }
        Story.FinishOpeningPresentation();
        SetState(GameState.Playing);
        RefreshExitState();
    }

    public bool CollectKey()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return false;
        if (CurrentState != GameState.Playing || KeysRequired <= 0 || KeysCollected >= KeysRequired)
            return false;

        KeysCollected++;
        Story.Record(KeysCollected == 1 ? LevelOneStory.Beat.KeyOne : LevelOneStory.Beat.KeyTwo);
        UpdateCounter();
        RefreshExitState();
        return true;
    }

    [Obsolete("Use CollectKey instead.")]
    public void KeyCollected()
    {
        CollectKey();
    }

    private void TickLevelTimer()
    {
        if (sessionBridge != null && !sessionBridge.HasStateAuthority) return;
        if (CurrentState != GameState.Playing || !HasLevelTimeLimit)
            return;

        LevelTimeRemaining = Mathf.Max(0f, LevelTimeRemaining - Time.deltaTime);
        int displayedSecond = Mathf.CeilToInt(LevelTimeRemaining);
        if (displayedSecond != lastDisplayedSecond)
        {
            lastDisplayedSecond = displayedSecond;
            UpdateCounter();
        }

        if (LevelTimeRemaining <= 0f)
            GameOver();
    }

    // Client presentation only: never runs PCG, AI, damage or progression locally.
    public void ApplyLanState(int level, GameState state, int enemies, int keys, int requiredKeys, float timeRemaining)
    {
        bool levelChanged = CurrentLevel != level;
        CurrentLevel = Mathf.Clamp(level, 1, TotalLevels);
        enemyCount = Mathf.Max(0, enemies);
        KeysRequired = Mathf.Max(0, requiredKeys);
        KeysCollected = Mathf.Clamp(keys, 0, KeysRequired);
        LevelTimeRemaining = Mathf.Max(0f, timeRemaining);
        if (CurrentState != state) SetState(state);
        else UpdateCounter();
        if (levelChanged) LevelChanged?.Invoke(CurrentLevel, TotalLevels);
    }

    public void RestartLanCampaign()
    {
        if (sessionBridge == null || !sessionBridge.HasStateAuthority) return;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        CurrentLevel = Mathf.Clamp(LanLaunch.StartingLevel, 1, TotalLevels);
        advanceRequestPending = false;
        SetState(GameState.Generating);
        BeginLevelGeneration();
    }

    private void EnsurePlayerControls()
    {
        playerControls ??= new PlayerControls();
    }

    private void EnsureLevelDefinitions()
    {
        if (levels == null || levels.Count == 0)
            levels = LevelDefinition.CreateDefaultCampaign();

        foreach (LevelDefinition definition in levels)
            definition?.EnsureMechanicsDefaults();
    }

    private LevelDefinition GetLevelDefinition(int levelNumber)
    {
        EnsureLevelDefinitions();
        int index = levelNumber - 1;
        return index >= 0 && index < levels.Count ? levels[index] : null;
    }
}
