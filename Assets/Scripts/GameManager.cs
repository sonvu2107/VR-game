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
    [SerializeField, Min(1)] private int totalFloors = 3;
    [SerializeField] private int baseSeed = 12345;
    [SerializeField, Min(1)] private int floorSeedStep = 1009;

    public static bool isGamePaused;
    public static bool isGameOver;
    public static bool isWin;

    public int enemyCount;
    public GameState CurrentState { get; private set; } = GameState.Generating;
    public int CurrentFloor { get; private set; } = 1;
    public int TotalFloors => totalFloors;

    public event Action<GameState> StateChanged;
    public event Action<int, int> FloorChanged;

    private PlayerControls playerControls;
    private InputAction pauseAction;
    private FloorExit currentExit;
    private Coroutine generationRoutine;

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
        SetState(GameState.Generating);
        BeginFloorGeneration();
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
        if (CurrentState != GameState.Generating)
            return;

        currentExit = floorExit;
        enemyCount = Mathf.Max(0, spawnedEnemyCount);
        SetState(GameState.Playing);
        FloorChanged?.Invoke(CurrentFloor, totalFloors);
        RefreshExitState();
    }

    public void GenerationFailed()
    {
        Debug.LogError($"Floor {CurrentFloor} could not be generated.");
        SetState(GameState.GameOver);
    }

    public void TryAdvanceFloor()
    {
        if (CurrentState != GameState.Playing || enemyCount > 0)
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

        EnemyCounter.text = CurrentState == GameState.Generating
            ? $"Floor {CurrentFloor}/{totalFloors} - Generating..."
            : $"Floor {CurrentFloor}/{totalFloors} - Enemies Remaining: {enemyCount}";
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
        currentExit?.SetUnlocked(CurrentState == GameState.Playing && enemyCount == 0);
    }

    private void EnsurePlayerControls()
    {
        playerControls ??= new PlayerControls();
    }
}
