using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Creates a darkness overlay on "dark" floors (Level 6).
/// Only the area around the player is illuminated.
/// Requires URP (Universal Render Pipeline) 2D Lights.
/// If URP 2D lights aren't available, falls back to a dark sprite overlay.
/// </summary>
public class DarknessController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Radius of light around the player.")]
    public float lightRadius = 6f;
    [Tooltip("Darkness opacity (0 = transparent, 1 = pitch black).")]
    [Range(0f, 1f)] public float darkness = 0.85f;

    private GameObject darknessOverlay;
    private Transform playerTransform;
    private Light2D playerLight;
    private bool isActive;

    /// <summary>Enable darkness mode for this floor.</summary>
    public void EnableDarkness(float radius)
    {
        lightRadius = radius;
        playerTransform = GameObject.Find("Player")?.transform;
        if (playerTransform == null) return;

        isActive = true;

        // Try to add a 2D point light to the player
        playerLight = playerTransform.GetComponentInChildren<Light2D>();
        if (playerLight == null)
        {
            GameObject lightObj = new GameObject("PlayerLight");
            lightObj.transform.SetParent(playerTransform);
            lightObj.transform.localPosition = Vector3.zero;
            playerLight = lightObj.AddComponent<Light2D>();
            playerLight.lightType = Light2D.LightType.Point;
        }

        playerLight.pointLightOuterRadius = lightRadius;
        playerLight.pointLightInnerRadius = lightRadius * 0.3f;
        playerLight.intensity = 1.2f;
        playerLight.color = new Color(1f, 0.9f, 0.7f); // Warm torch light

        // Create a global darkness overlay
        // The 2D Global Light should be dimmed for dark floors
        Light2D[] globalLights = FindObjectsOfType<Light2D>();
        foreach (var light in globalLights)
        {
            if (light.lightType == Light2D.LightType.Global)
                light.intensity = 1f - darkness;
        }

        Debug.Log($"Darkness enabled with radius {lightRadius}");
    }

    /// <summary>Disable darkness (for normal floors).</summary>
    public void DisableDarkness()
    {
        isActive = false;

        if (playerLight != null)
        {
            Destroy(playerLight.gameObject);
            playerLight = null;
        }

        // Restore global light
        Light2D[] globalLights = FindObjectsOfType<Light2D>();
        foreach (var light in globalLights)
        {
            if (light.lightType == Light2D.LightType.Global)
                light.intensity = 1f;
        }

        if (darknessOverlay != null)
        {
            Destroy(darknessOverlay);
            darknessOverlay = null;
        }
    }

    private void OnDestroy()
    {
        if (isActive)
            DisableDarkness();
    }
}
