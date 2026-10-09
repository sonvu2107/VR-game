using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    private void OnEnable()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }
}