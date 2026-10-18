using UnityEngine;

/// <summary>Saved editor preview is replaced by authoritative runtime generation.</summary>
public sealed class CourtyardPreviewOnly : MonoBehaviour
{
    private void Awake()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
