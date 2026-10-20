using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

public class StartMenuControls : MonoBehaviour
{
    public GameObject loadingScreen;
    public Slider slider;
    public TextMeshProUGUI progressText;
    private GameObject titleScreen;

    private void Start()
    {
        if (loadingScreen != null) loadingScreen.SetActive(false);
        ShowTitleScreen();
    }

    public void StartButtonControl()
    {
        if (titleScreen != null) Destroy(titleScreen);
        LanMenuUI.Show(this);
    }

    public void ShowTitleScreen()
    {
        if (titleScreen != null) return;
        titleScreen = new GameObject("Labyrinth Title Screen", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = titleScreen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = titleScreen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite art = Resources.Load<Sprite>("UI/Menu/LabyrinthLobbyBackground");
        var background = new GameObject("Dungeon artwork", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(titleScreen.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.GetComponent<Image>();
        bgImage.sprite = art;
        bgImage.color = art != null ? Color.white : new Color(0.035f, 0.025f, 0.08f);
        bgImage.raycastTarget = true;

        PlaceArt("Game title", "LabyrinthTitleLogo", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(1450, 386));
        PlaceArt("Course caption", "LabyrinthTagline", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -365), new Vector2(1200, 115));
        PlaceArt("Course name", "LabyrinthCoursePlaque", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 13), new Vector2(1080, 104));
        PlaceArt("Group credits", "LabyrinthCreditsPanel", new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-42, 24), new Vector2(620, 369));

        var button = new GameObject("Enter lobby", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(titleScreen.transform, false);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0f, 0f);
        buttonRect.anchoredPosition = new Vector2(70, 85);
        buttonRect.sizeDelta = new Vector2(360, 96);
        Image buttonImage = button.GetComponent<Image>();
        buttonImage.sprite = Resources.Load<Sprite>("UI/Menu/LabyrinthEnterButton");
        buttonImage.color = Color.white;
        Button enter = button.GetComponent<Button>();
        enter.targetGraphic = buttonImage;
        ColorBlock colors = enter.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.76f);
        colors.pressedColor = new Color(0.72f, 0.9f, 0.9f);
        enter.colors = colors;
        enter.onClick.AddListener(StartButtonControl);
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
    }

    private void PlaceArt(string objectName, string resourceName, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 dimensions)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(titleScreen.transform, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = obj.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/Menu/" + resourceName);
        image.raycastTarget = false;
    }

    public void LaunchDungeon() => StartCoroutine(LoadScene("Dungeon"));
    
    public void QuitButtonControl()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        
        loadingScreen.SetActive(true);
        
        while (!operation.isDone)
        {
            // float progress = Mathf.Clamp01(operation.progress / 0.9f);
            float progress = operation.progress;
            slider.value = progress;
            progressText.text = (int)(progress * 100f) + "%";
            
            yield return null;
        }
    }
}
