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

    public int enemyCount;
    public GameState CurrentState { get; private set; } = GameState.Generating;
    public int CurrentLevel { get; private set; } = 1;
    public int TotalLevels => levels?.Count ?? 0;
    public LevelDefinition CurrentLevelDefinition => GetLevelDefinition(CurrentLevel);

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
    }

    private void OnEnable()
    {
        EnsurePlayerControls();
        pauseAction = playerControls.Player.Pause;
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        pauseAction?.Disable();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        CurrentLevel = 1;
        SetState(GameState.Generating);
        BeginLevelGeneration();
    }

    private void Update()
    {
        if (pauseAction == null || !pauseAction.WasPressedThisFrame())
            return;

        if (CurrentState == GameState.Playing)
            Pause();
        else if (CurrentState == GameState.Paused)
            Resume();
    }

    public void Resume()
    {
        if (CurrentState != GameState.Paused)
            return;

        SetState(GameState.Playing);
    }

    public void Pause()
    {
        if (CurrentState != GameState.Playing)
            return;

        SetState(GameState.Paused);
    }

    public void GameOver()
    {
        if (CurrentState is GameState.GameOver or GameState.Victory)
            return;

        SetState(GameState.GameOver);
    }

    public void Win()
    {
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
        advanceRequestPending = false;
        SetState(GameState.Playing);
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
        if (CurrentState != GameState.Playing || enemyCount > 0 || advanceRequestPending)
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
        if (CurrentState != GameState.Playing || enemyCount > 0 || completedLevel != CurrentLevel)
        {
            advanceRequestPending = false;
            return;
        }

        if (CurrentLevel >= TotalLevels)
        {
            Win();
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

        EnemyCounter.text = CurrentState == GameState.Generating
            ? $"Level {CurrentLevel}/{TotalLevels} - Generating..."
            : $"Level {CurrentLevel}/{TotalLevels} - Enemies Remaining: {enemyCount}";
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
        isGamePaused = newState == GameState.Paused;
        isGameOver = newState == GameState.GameOver;
        isWin = newState == GameState.Victory;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(newState == GameState.Paused);
        if (gameOverUI != null)
            gameOverUI.SetActive(newState == GameState.GameOver);
        if (winUI != null)
            winUI.SetActive(newState == GameState.Victory);

        Time.timeScale = newState is GameState.Paused or GameState.GameOver or GameState.Victory ? 0f : 1f;

        if (PlayerMovement != null)
            PlayerMovement.enabled = newState == GameState.Playing;

        UpdateCounter();
        StateChanged?.Invoke(newState);
        sessionBridge?.PublishState(newState);
    }

    private void RefreshExitState()
    {
        currentExit?.SetUnlocked(CurrentState == GameState.Playing && enemyCount == 0);
    }

    private void EnsurePlayerControls()
    {
        playerControls ??= new PlayerControls();
    }

    private void EnsureLevelDefinitions()
    {
        if (levels == null || levels.Count == 0)
            levels = LevelDefinition.CreateDefaultCampaign();
    }

    private LevelDefinition GetLevelDefinition(int levelNumber)
    {
        EnsureLevelDefinitions();
        int index = levelNumber - 1;
        return index >= 0 && index < levels.Count ? levels[index] : null;
    }
}
