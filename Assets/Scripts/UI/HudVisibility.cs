using UnityEngine;

public class HudVisibility : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    [Tooltip("Các HUD có sẵn trong scene: kéo vào đây")]
    [SerializeField] private GameObject[] hudObjects;

    [Tooltip("Các HUD được tạo lúc chạy: gõ đúng tên đối tượng")]
    [SerializeField] private string[] runtimeHudNames = { "Combat Skill HUD" };

    [SerializeField] private bool hideOnPause = false;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        bool hide = state == GameState.GameOver
                 || state == GameState.Victory
                 || (hideOnPause && state == GameState.Paused);

        foreach (GameObject obj in hudObjects)
            SetVisible(obj, !hide);

        foreach (string objectName in runtimeHudNames)
        {
            if (string.IsNullOrEmpty(objectName)) continue;
            SetVisible(GameObject.Find(objectName), !hide);
        }
    }

    private static void SetVisible(GameObject obj, bool visible)
    {
        if (obj == null) return;

        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        if (group == null) group = obj.AddComponent<CanvasGroup>();

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }
}