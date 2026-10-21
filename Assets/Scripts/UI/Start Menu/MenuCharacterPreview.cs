using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class MenuCharacterPreview : MonoBehaviour
{
    private readonly Sprite[][] frames = new Sprite[3][];
    private RawImage image;
    private int action;
    private float started;
    private bool knight;

    public void SetCharacter(PlayableCharacterId character)
    {
        image = GetComponent<RawImage>();
        knight = character == PlayableCharacterId.AshenKnight;
        if (knight)
        {
            var library = Resources.Load<MenuPreviewLibrary>("MenuPreviewLibrary");
            frames[0] = library != null ? library.idle : Array.Empty<Sprite>();
            frames[1] = library != null ? library.run : Array.Empty<Sprite>();
            frames[2] = library != null ? library.attack : Array.Empty<Sprite>();
        }
        else
        {
            string root = $"Characters/{character}/Frames/";
            string[] folders = { "Idle_6f", "Run_8f", "Attack_6f" };
            int[] counts = { 6, 8, 6 };
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new Sprite[counts[i]];
                for (int j = 0; j < counts[i]; j++)
                    frames[i][j] = Resources.Load<Sprite>($"{root}{folders[i]}/Frame_{j:00}");
            }
        }
        SetAction(action);
    }

    public void SetAction(int value)
    {
        action = Mathf.Clamp(value, 0, 2);
        started = Time.unscaledTime;
        RenderFrame(0);
    }

    private void Update()
    {
        float fps = knight ? 12f : action == 0 ? 6f : action == 1 ? 10f : 13f;
        RenderFrame(Mathf.FloorToInt((Time.unscaledTime - started) * fps));
    }

    private void RenderFrame(int index)
    {
        var sequence = frames[action];
        if (image == null || sequence == null || sequence.Length == 0) return;
        Sprite sprite = sequence[index % sequence.Length];
        image.enabled = sprite != null;
        if (sprite == null) return;
        image.texture = sprite.texture;
        Rect rect = sprite.rect;
        image.uvRect = new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
            rect.width / sprite.texture.width, rect.height / sprite.texture.height);
    }
}
