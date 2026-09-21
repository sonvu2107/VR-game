using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public PlayerControls playerControl;
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public GameObject winUI;
    public TextMeshProUGUI EnemyCounter;
    public PlayerMovement PlayerMovement;
    public static bool isGamePaused = false;
    public static bool isGameOver = false;
    public static bool isWin = false;
    public int enemyCount;

    private InputAction quit;

    private void Awake()
    {
        playerControl = new PlayerControls();
        enemyCount = 0;
    }

    private void OnEnable()
    {
        quit = playerControl.Player.Pause;
        quit.Enable();
    }

    private void OnDisable()
    {
        quit?.Disable();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        isGamePaused = false;
        isWin = false;

        SetPanelActive(pauseMenuUI, false);
        SetPanelActive(gameOverUI, false);
        SetPanelActive(winUI, false);

        if (PlayerMovement != null)
            PlayerMovement.enabled = true;

        UpdateCounter();
    }

    private void Update()
    {
        if (isGameOver || isWin)
            return;

        if (enemyCount <= 0)
        {
            Win();
            return;
        }

        if (quit != null && quit.WasPressedThisFrame())
        {
            if (isGamePaused)
                Resume();
            else
                Pause();
        }
    }

    public void Resume()
    {
        if (isGameOver || isWin)
            return;

        SetPanelActive(pauseMenuUI, false);
        if (PlayerMovement != null)
            PlayerMovement.enabled = true;

        Time.timeScale = 1f;
        isGamePaused = false;
    }

    public void Pause()
    {
        if (isGameOver || isWin)
            return;

        SetPanelActive(pauseMenuUI, true);
        if (PlayerMovement != null)
            PlayerMovement.enabled = false;

        Time.timeScale = 0f;
        isGamePaused = true;
    }
    
    public void GameOver()
    {
        if (isGameOver || isWin)
            return;

        isGameOver = true;
        isGamePaused = false;
        SetPanelActive(pauseMenuUI, false);
        SetPanelActive(gameOverUI, true);
        if (PlayerMovement != null)
            PlayerMovement.enabled = false;

        Time.timeScale = 0f;
    }

    public void Win()
    {
        if (isGameOver || isWin)
            return;

        isWin = true;
        isGamePaused = false;
        SetPanelActive(pauseMenuUI, false);
        SetPanelActive(winUI, true);
        if (PlayerMovement != null)
            PlayerMovement.enabled = false;

        Time.timeScale = 0f;
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Dungeon");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void UpdateCounter()
    {
        if (EnemyCounter != null)
            EnemyCounter.text = "Enemies Remaining: " + Mathf.Max(0, enemyCount);
    }

    private static void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel != null)
            panel.SetActive(isActive);
    }
}
