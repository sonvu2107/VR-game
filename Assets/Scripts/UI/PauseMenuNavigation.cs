using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Thêm đường về menu bằng chính mẫu nút của scene, áp dụng cho mọi map.</summary>
public static class PauseMenuNavigation
{
    public static void Install(GameManager manager)
    {
        if (manager.pauseMenuUI == null) return;
        Transform panel = manager.pauseMenuUI.transform;
        CenterLabels(panel);
        if (panel.Find("Return To Main Menu") != null) return;
        var buttons = new List<Button>();
        Button quit = null;
        foreach (Transform child in panel)
        {
            var button = child.GetComponent<Button>();
            if (button == null) continue;
            buttons.Add(button);
            if (child.name == "Quit") quit = button;
        }
        if (quit == null || buttons.Count == 0) return;
        buttons.Sort((a, b) => ((RectTransform)b.transform).anchoredPosition.y.CompareTo(
            ((RectTransform)a.transform).anchoredPosition.y));
        float top = ((RectTransform)buttons[0].transform).anchoredPosition.y;
        float bottom = ((RectTransform)buttons[buttons.Count - 1].transform).anchoredPosition.y;
        var back = Object.Instantiate(quit, panel);
        back.name = "Return To Main Menu";
        back.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
        // Thay toàn bộ event, kể cả callback QuitGame được serialize trong nút gốc.
        back.onClick = new Button.ButtonClickedEvent();
        back.onClick.AddListener(manager.ReturnToMainMenu);
        var label = back.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Về menu chính";
        buttons.Insert(buttons.IndexOf(quit), back);
        for (int i = 0; i < buttons.Count; i++)
        {
            var rect = (RectTransform)buttons[i].transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                Mathf.Lerp(top, bottom, (float)i / (buttons.Count - 1)));
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y * .75f);
            var navigation = buttons[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count];
            navigation.selectOnDown = buttons[(i + 1) % buttons.Count];
            buttons[i].navigation = navigation;
        }
        CenterLabels(panel);
    }

    private static void CenterLabels(Transform panel)
    {
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.text = label.text.Trim();
            label.alignment = TextAlignmentOptions.Midline;
            label.margin = Vector4.zero;
            var rect = label.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(8, 2);
            rect.offsetMax = new Vector2(-8, -2);
        }
    }
}
