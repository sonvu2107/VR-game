using TMPro;
using UnityEngine;

public class LevelBanner : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0f)] private float holdTime = 1.5f;
    [SerializeField, Min(0.1f)] private float fadeTime = 0.6f;

    private float timer = -1f;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.LevelChanged += HandleLevelChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.LevelChanged -= HandleLevelChanged;
    }

    private void HandleLevelChanged(int level, int total)
    {
        label.text = level >= total ? "FINAL LEVEL" : $"LEVEL {level}";
        timer = 0f;
        group.alpha = 1f;
    }

    private void Update()
    {
        if (timer < 0f) return;

        timer += Time.unscaledDeltaTime;
        if (timer > holdTime)
            group.alpha = Mathf.Clamp01(1f - (timer - holdTime) / fadeTime);

        if (timer > holdTime + fadeTime)
        {
            group.alpha = 0f;
            timer = -1f;
        }
    }
}