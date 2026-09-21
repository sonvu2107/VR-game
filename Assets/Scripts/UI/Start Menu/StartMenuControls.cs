using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuControls : MonoBehaviour
{
    public GameObject loadingScreen;
    public Slider slider;
    public TextMeshProUGUI progressText;

    private bool isLoading;

    private void Start()
    {
        Time.timeScale = 1f;
        isLoading = false;

        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        SetProgress(0f);
    }

    public void StartButtonControl()
    {
        if (isLoading)
            return;

        StartCoroutine(LoadScene("Dungeon"));
    }
    
    public void QuitButtonControl()
    {
        Application.Quit();
    }
    
    IEnumerator LoadScene(string sceneName)
    {
        isLoading = true;
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        SetProgress(0f);
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        
        while (!operation.isDone)
        {
            // Unity reports loading progress from 0 to 0.9 before activation.
            SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        SetProgress(1f);
    }

    private void SetProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        if (slider != null)
            slider.value = clampedProgress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
    }
}
