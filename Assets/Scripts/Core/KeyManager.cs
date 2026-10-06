using UnityEngine;
using TMPro;

/// <summary>
/// Tracks keys collected by the player on the current floor.
/// Attach to the Player or a persistent manager object.
/// </summary>
public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Optional text to display key count.")]
    public TextMeshProUGUI keyCountText;

    private int keysCollected;
    private int keysRequired;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialize(int required)
    {
        keysRequired = required;
        keysCollected = 0;
        UpdateUI();
    }

    public void CollectKey()
    {
        keysCollected++;
        UpdateUI();
        Debug.Log($"Key collected! ({keysCollected}/{keysRequired})");
    }

    public bool HasEnoughKeys()
    {
        return keysCollected >= keysRequired;
    }

    public int KeysCollected => keysCollected;
    public int KeysRequired => keysRequired;

    private void UpdateUI()
    {
        if (keyCountText != null)
            keyCountText.text = $"🔑 {keysCollected}/{keysRequired}";
    }
}
