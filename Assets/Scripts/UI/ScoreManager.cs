using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    [Header("HUD (tùy chọn)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private TMP_Text highestLevelText;

    [Header("Game Over / Victory: mỗi mảng 2 phần tử [Game Over, Victory]")]
    [SerializeField] private TMP_Text[] finalScoreTexts;
    [SerializeField] private TMP_Text[] finalHighScoreTexts;
    [SerializeField] private TMP_Text[] finalHighestLevelTexts;

    [Header("Điểm")]
    [SerializeField, Min(0)] private int pointsPerEnemy = 100;
    [SerializeField, Min(0)] private int levelClearBonus = 500;

    private int score;
    private int highScore;
    private int highestLevel;
    private int lastEnemyCount;
    private int lastLevel;
    private bool finished;

    private void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        highScore = SaveSystem.Data.highScore;
        highestLevel = SaveSystem.Data.highestLevel;

        if (gameManager != null)
        {
            gameManager.LevelChanged += HandleLevelChanged;
            gameManager.StateChanged += HandleStateChanged;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.LevelChanged -= HandleLevelChanged;
            gameManager.StateChanged -= HandleStateChanged;
        }
        SaveIfHigher();
    }

    private void Update()
    {
        if (gameManager == null || finished) return;

        if (gameManager.CurrentState != GameState.Playing)
        {
            lastEnemyCount = gameManager.enemyCount;
            return;
        }

        int current = gameManager.enemyCount;
        if (current < lastEnemyCount)
        {
            score += (lastEnemyCount - current) * pointsPerEnemy;
            Refresh();
        }
        lastEnemyCount = current;
    }

    private void HandleLevelChanged(int level, int total)
    {
        if (lastLevel > 0 && level > lastLevel)
            score += levelClearBonus;
        lastLevel = level;

        // Level mới được sinh xong nghĩa là người chơi đã đến level này.
        if (level > highestLevel)
        {
            highestLevel = level;
            SaveIfHigher();
        }
        Refresh();
    }

    private void HandleStateChanged(GameState state)
    {
        if (finished) return;
        if (state != GameState.GameOver && state != GameState.Victory) return;

        finished = true;
        SaveIfHigher();
        Refresh();
    }

    private void SaveIfHigher()
    {
        bool changed = false;

        if (score > highScore)
        {
            highScore = score;
            SaveSystem.Data.highScore = highScore;
            changed = true;
        }

        if (highestLevel > SaveSystem.Data.highestLevel)
        {
            SaveSystem.Data.highestLevel = highestLevel;
            changed = true;
        }

        if (changed) SaveSystem.Save();
    }

    private void Refresh()
    {
        int best = Mathf.Max(score, highScore);
        int total = gameManager != null ? gameManager.TotalLevels : 0;
        string levelLabel = total > 0 ? $"{highestLevel}/{total}" : highestLevel.ToString();

        if (scoreText != null) scoreText.text = $"Score: {score}";
        if (highScoreText != null) highScoreText.text = $"Best: {best}";
        if (highestLevelText != null) highestLevelText.text = $"Highest Level: {levelLabel}";

        SetAll(finalScoreTexts, $"Score : {score}");
        SetAll(finalHighScoreTexts, $"High Score : {best}");
        SetAll(finalHighestLevelTexts, $"Highest Level : {levelLabel}");
    }

    private static void SetAll(TMP_Text[] texts, string value)
    {
        if (texts == null) return;
        foreach (TMP_Text t in texts)
            if (t != null) t.text = value;
    }
}