using TMPro;
using UnityEngine;

/// <summary>
/// Displays a key counter (e.g. "🔑 1/3") on the HUD.
/// Only visible on floors that require keys.
/// Attach to a Canvas child with a TextMeshProUGUI component.
/// </summary>
public class KeyCounterUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The TMP text component to display the key count.")]
    public TextMeshProUGUI keyText;

    [Header("Display Settings")]
    [Tooltip("Icon or prefix before the count.")]
    public string keyIcon = "🔑";
    [Tooltip("Color when all keys are collected.")]
    public Color completedColor = new Color(0.2f, 1f, 0.4f, 1f); // green
    [Tooltip("Color while keys are still missing.")]
    public Color normalColor = new Color(1f, 0.85f, 0.2f, 1f);   // gold

    private GameManager gameManager;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Start hidden
        SetVisible(false);
    }

    private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();

        if (gameManager != null)
        {
            gameManager.StateChanged += OnStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.StateChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            // Show or hide based on whether this floor requires keys
            bool needsKeys = gameManager.keysRequired > 0;
            SetVisible(needsKeys);

            if (needsKeys)
                UpdateDisplay();
        }
        else if (state == GameState.Generating)
        {
            SetVisible(false);
        }
    }

    private void Update()
    {
        if (gameManager == null || gameManager.keysRequired <= 0) return;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (keyText == null || gameManager == null) return;

        int collected = gameManager.keysCollected;
        int required = gameManager.keysRequired;

        keyText.text = $"{keyIcon} {collected} / {required}";
        keyText.color = collected >= required ? completedColor : normalColor;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
