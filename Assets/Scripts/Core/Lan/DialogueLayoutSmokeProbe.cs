#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;

public sealed partial class StorySmokeProbe
{
    private static bool GlyphsInside(TMP_Text label)
    {
        label.ForceMeshUpdate(true, true);
        Rect rect = label.rectTransform.rect;
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            var ch = label.textInfo.characterInfo[i];
            if (!ch.isVisible) continue;
            if (ch.bottomLeft.x < rect.xMin - 1 || ch.topRight.x > rect.xMax + 1 ||
                ch.bottomLeft.y < rect.yMin - 1 || ch.topRight.y > rect.yMax + 1) return false;
        }
        return true;
    }

    private IEnumerator VerifyDialogueWrapping(LevelOneStory story)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(LevelOneStory);
        var text = (TMP_Text)type.GetField("body", flags).GetValue(story);
        var layout = type.GetMethod("SetCinematicLayout", flags);
        var configure = type.GetMethod("ConfigureDialogueText", flags);
        string original = text.text;
        int width = Screen.width, height = Screen.height;
        // Tái hiện UI giữ cấu hình không xuống dòng; kiểm tra hình học phải bắt được.
        text.enableWordWrapping = false;
        Check(!GlyphsInside(text), "geometry detects original unwrapped opening overflow");
        yield return null;
        Check(text.enableWordWrapping && GlyphsInside(text), "live dialogue repairs retained no-wrap setting");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768),
            new Vector2Int(1280, 360), new Vector2Int(800, 600) })
        {
            Screen.SetResolution(size.x, size.y, false);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Check(Screen.width == size.x && Screen.height == size.y, "dialogue viewport " + size);
            foreach (bool cinematic in new[] { true, false })
            {
                layout.Invoke(story, new object[] { cinematic });
                configure.Invoke(story, null);
                if (cinematic)
                {
                    foreach (string field in new[] { "OpeningLines", "EndLines" })
                        foreach (string line in (string[])type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))
                        {
                            text.text = line;
                            Check(GlyphsInside(text), "cinematic glyphs inside body " + size);
                        }
                }
                else
                    for (int level = 2; level <= 10; level++)
                        foreach (bool coop in new[] { false, true })
                        {
                            var chapter = CampaignStoryChapter.ForLevel(level);
                            for (int line = 0; line < chapter.LineCount; line++)
                            {
                                text.text = chapter.TextAt(line, coop);
                                Check(GlyphsInside(text), "chapter glyphs inside body " + size + " level=" + level);
                            }
                        }
            }
            layout.Invoke(story, new object[] { true }); configure.Invoke(story, null);
            text.text = original; text.ForceMeshUpdate(true, true);
        }
        Screen.SetResolution(width, height, false);
        yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
        Check(text.textInfo.lineCount > 1 && GlyphsInside(text), "opening wraps visibly into multiple contained lines");
    }
}
#endif
