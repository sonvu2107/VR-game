using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Temporarily dims global 2D lights and follows the player with a local light.</summary>
public sealed class DarknessController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float darkness = 0.78f;
    [SerializeField] private Color playerLightColor = new(1f, 0.87f, 0.66f, 1f);

    private readonly Dictionary<Light2D, float> originalGlobalIntensities = new();
    private Light2D playerLight;
    private bool darknessEnabled;

    public void EnableDarkness(Transform playerTransform, float radius)
    {
        if (playerTransform == null || radius <= 0f)
        {
            DisableDarkness();
            return;
        }

        EnsurePlayerLight(playerTransform);
        CaptureAndDimGlobalLights();

        playerLight.pointLightOuterRadius = radius;
        playerLight.pointLightInnerRadius = radius * 0.35f;
        playerLight.intensity = 1.15f;
        playerLight.color = playerLightColor;
        playerLight.enabled = true;
        darknessEnabled = true;
    }

    public void DisableDarkness()
    {
        if (playerLight != null)
            playerLight.enabled = false;

        RestoreGlobalLights();
        darknessEnabled = false;
    }

    private void EnsurePlayerLight(Transform playerTransform)
    {
        if (playerLight == null)
        {
            Transform existing = playerTransform.Find("Runtime Dungeon Light");
            if (existing != null)
                playerLight = existing.GetComponent<Light2D>();
        }

        if (playerLight == null)
        {
            GameObject lightObject = new("Runtime Dungeon Light");
            lightObject.transform.SetParent(playerTransform, false);
            playerLight = lightObject.AddComponent<Light2D>();
            playerLight.lightType = Light2D.LightType.Point;
        }

        if (playerLight.transform.parent != playerTransform)
            playerLight.transform.SetParent(playerTransform, false);
    }

    private void CaptureAndDimGlobalLights()
    {
        RestoreGlobalLights();
        foreach (Light2D light in FindObjectsOfType<Light2D>())
        {
            if (light == null || light == playerLight || light.lightType != Light2D.LightType.Global)
                continue;

            originalGlobalIntensities[light] = light.intensity;
            light.intensity *= 1f - darkness;
        }
    }

    private void RestoreGlobalLights()
    {
        foreach (KeyValuePair<Light2D, float> entry in originalGlobalIntensities)
        {
            if (entry.Key != null)
                entry.Key.intensity = entry.Value;
        }

        originalGlobalIntensities.Clear();
    }

    private void OnDisable()
    {
        if (darknessEnabled)
            DisableDarkness();
    }

    private void OnDestroy()
    {
        RestoreGlobalLights();
    }
}
