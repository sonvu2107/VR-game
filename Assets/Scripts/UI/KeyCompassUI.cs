using UnityEngine;
using TMPro;

/// <summary>
/// Shows a directional arrow on the edge of the screen pointing toward
/// the nearest uncollected DungeonKey. Only active on floors requiring keys.
/// Attach to a UI element under a Canvas.
/// </summary>
public class KeyCompassUI : MonoBehaviour
{
    [Header("Arrow Display")]
    [Tooltip("The RectTransform of the arrow image (should be a simple arrow sprite or '▶' text).")]
    public RectTransform arrowRect;
    [Tooltip("Optional text showing distance to nearest key.")]
    public TextMeshProUGUI distanceText;

    [Header("Settings")]
    [Tooltip("Distance from screen center for the arrow (in pixels).")]
    public float arrowOrbitRadius = 80f;
    [Tooltip("Arrow fades out when player is closer than this distance.")]
    public float hideDistance = 2f;
    [Tooltip("Arrow appears when player is farther than this distance.")]
    public float showDistance = 4f;

    private GameManager gameManager;
    private Transform playerTransform;
    private CanvasGroup canvasGroup;
    private Camera mainCamera;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        mainCamera = Camera.main;

        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
            playerTransform = pm.transform;
    }

    private void Update()
    {
        // Only show when keys are required and not all collected
        if (gameManager == null || gameManager.keysRequired <= 0 
            || gameManager.keysCollected >= gameManager.keysRequired
            || playerTransform == null)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        // Find nearest uncollected key
        DungeonKey[] keys = FindObjectsOfType<DungeonKey>();
        if (keys.Length == 0)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        DungeonKey nearest = null;
        float nearestDist = float.MaxValue;

        foreach (DungeonKey key in keys)
        {
            float dist = Vector2.Distance(playerTransform.position, key.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = key;
            }
        }

        if (nearest == null)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        // Fade based on distance
        float alpha = Mathf.InverseLerp(hideDistance, showDistance, nearestDist);
        canvasGroup.alpha = alpha;

        if (alpha <= 0.01f) return;

        // Calculate direction to key
        Vector2 dir = (nearest.transform.position - playerTransform.position).normalized;

        // Rotate arrow to point toward key
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (arrowRect != null)
        {
            arrowRect.localRotation = Quaternion.Euler(0, 0, angle);
            // Position arrow in a circle around center
            arrowRect.anchoredPosition = dir * arrowOrbitRadius;
        }

        // Show distance text
        if (distanceText != null)
        {
            distanceText.text = $"{nearestDist:F0}m";
        }
    }
}
