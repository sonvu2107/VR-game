using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Owns the dungeon run state and three-floor progression.</summary>
public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public GameObject winUI;
    public TextMeshProUGUI EnemyCounter;
    public PlayerMovement PlayerMovement;
    public InfiniteWorldGenerator dungeonGenerator;

    [Header("Run Progression")]
    [SerializeField, Min(1)] private int totalFloors = 10;
    [SerializeField] private int baseSeed = 12345;
    [SerializeField, Min(1)] private int floorSeedStep = 1009;

    [Header("Time Limit")]
    [Tooltip("Thời gian mặc định (giây) cho mỗi tầng. FloorConfigSO có thể override.")]
    [SerializeField] private float defaultTimeLimit = 120f;

    public static bool isGamePaused;
    public static bool isGameOver;
    public static bool isWin;

    public int enemyCount;
    public int keysCollected;
    public int keysRequired;
    public GameState CurrentState { get; private set; } = GameState.Generating;
    public int CurrentFloor { get; private set; } = 1;
    public int TotalFloors => totalFloors;

    public event Action<GameState> StateChanged;
    public event Action<int, int> FloorChanged;

    private PlayerControls playerControls;
    private InputAction pauseAction;
    private FloorExit currentExit;
    private Coroutine generationRoutine;

    // Floor timer
    private bool hasTimeLimit;
    private float floorTimeRemaining;
    private float floorTimeTotal;

    private void Awake()
    {
        EnsurePlayerControls();
        enemyCount = 0;

        if (PlayerMovement == null)
            PlayerMovement = FindObjectOfType<PlayerMovement>();

        if (dungeonGenerator == null)
            dungeonGenerator = FindObjectOfType<InfiniteWorldGenerator>();
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
        CurrentFloor = 1;

        // Reset item bonuses at the start of a new dungeon run
        if (PlayerMovement != null && PlayerMovement.playerStats != null)
            PlayerMovement.playerStats.ResetBonuses();

        SetState(GameState.Generating);
        BeginFloorGeneration();
    }

    private void Update()
    {
        // Pause input
        if (pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            if (CurrentState == GameState.Playing)
                Pause();
            else if (CurrentState == GameState.Paused)
                Resume();
        }

        // Floor timer countdown
        if (CurrentState == GameState.Playing && hasTimeLimit)
        {
            floorTimeRemaining -= Time.deltaTime;
            UpdateCounter();

            if (floorTimeRemaining <= 0f)
            {
                floorTimeRemaining = 0f;
                Debug.Log("Time's up! Game Over.");
                GameOver();
            }
        }
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
        if (CurrentState != GameState.Generating)
            return;

        currentExit = floorExit;
        enemyCount = Mathf.Max(0, spawnedEnemyCount);

        // Setup keys based on floor config
        keysCollected = 0;
        keysRequired = 0;

        // Timer always active — auto-calculate based on floor size
        hasTimeLimit = true;
        float autoTime = CalculateTimeLimit();
        floorTimeRemaining = autoTime;
        floorTimeTotal = autoTime;

        if (dungeonGenerator != null && dungeonGenerator.activeFloorConfig != null)
        {
            FloorConfigSO cfg = dungeonGenerator.activeFloorConfig;

            if (cfg.requireKeys)
                keysRequired = cfg.keysRequired;

            // Override timer if floor has custom time limit
            if (cfg.hasTimeLimit)
            {
                floorTimeRemaining = cfg.timeLimitSeconds;
                floorTimeTotal = cfg.timeLimitSeconds;
            }
        }

        SetState(GameState.Playing);
        FloorChanged?.Invoke(CurrentFloor, totalFloors);
        RefreshExitState();
    }

    /// <summary>
    /// Auto-calculates time limit based on floor size.
    /// Small floors (~5 rooms) = 3 minutes, large floors (~15+ rooms) = 8 minutes.
    /// </summary>
    private float CalculateTimeLimit()
    {
        int rooms = 10; // default
        if (dungeonGenerator != null && dungeonGenerator.activeFloorConfig != null)
            rooms = dungeonGenerator.activeFloorConfig.numberOfRooms;
        else if (dungeonGenerator != null)
            rooms = dungeonGenerator.numberOfRooms;

        // Lerp: 5 rooms -> 180s (3min), 15 rooms -> 480s (8min)
        float t = Mathf.InverseLerp(5f, 15f, rooms);
        return Mathf.Lerp(180f, 480f, t);
    }

    public void GenerationFailed()
    {
        Debug.LogError($"Floor {CurrentFloor} could not be generated.");
        SetState(GameState.GameOver);
    }

    public void TryAdvanceFloor()
    {
        if (CurrentState != GameState.Playing || enemyCount > 0 || keysCollected < keysRequired)
            return;

        if (CurrentFloor >= totalFloors)
        {
            Win();
            return;
        }

        CurrentFloor++;
        SetState(GameState.Generating);
        BeginFloorGeneration();
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

        if (CurrentState == GameState.Generating)
        {
            EnemyCounter.text = $"Level {CurrentFloor}/{totalFloors} - Generating...";
            return;
        }

        string text = $"Level {CurrentFloor}/{totalFloors} - Enemies: {enemyCount}";
        
        // Show timer (only on floors with time limit enabled)
        if (hasTimeLimit)
        {
            int minutes = Mathf.FloorToInt(floorTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(floorTimeRemaining % 60f);
            string timerColor = floorTimeRemaining <= 15f ? "#FF4444" 
                              : floorTimeRemaining <= 30f ? "#FFAA00" 
                              : "#FFFFFF";
            text += $"  |  <color={timerColor}>Time: {minutes:00}:{seconds:00}</color>";
        }

        // Show key counter (only on floors that require keys)
        if (keysRequired > 0)
        {
            string keyColor = keysCollected >= keysRequired ? "#44FF44" : "#FFD700";
            text += $"  |  <color={keyColor}>Key: {keysCollected}/{keysRequired}</color>";
        }

        // Show gold (only when player has gold)
        if (PlayerMovement != null && PlayerMovement.playerStats != null && PlayerMovement.playerStats.Gold > 0)
        {
            text += $"  |  Gold: {PlayerMovement.playerStats.Gold}";
        }

        EnemyCounter.text = text;
    }

    private void BeginFloorGeneration()
    {
        if (generationRoutine != null)
            StopCoroutine(generationRoutine);

        generationRoutine = StartCoroutine(GenerateFloorRoutine());
    }

    private IEnumerator GenerateFloorRoutine()
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

        int floorSeed = baseSeed + (CurrentFloor - 1) * floorSeedStep;
        if (!dungeonGenerator.GenerateFloor(CurrentFloor, totalFloors, floorSeed))
            GenerationFailed();

        generationRoutine = null;
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
    }

    private void RefreshExitState()
    {
        currentExit?.SetUnlocked(CurrentState == GameState.Playing && enemyCount == 0 && keysCollected >= keysRequired);
    }

    public void KeyCollected()
    {
        keysCollected++;
        UpdateCounter();
        RefreshExitState();
    }

    private void EnsurePlayerControls()
    {
        playerControls ??= new PlayerControls();
    }
}
