using System;
using UnityEngine;

/// <summary>Trang trí thuần hình ảnh; không thêm collider, sát thương hay trạng thái gameplay.</summary>
[CreateAssetMenu(menuName = "PCG/Dungeon Ambience Set")]
public sealed class DungeonAmbienceSet : ScriptableObject
{
    public Palette[] palettes = new Palette[10];

    [Serializable]
    public sealed class Palette
    {
        public Material fire;
        public Material souls;
        public Material details;
        [Range(12, 15)] public int accentCell = 14;
        [Range(0, 3)] public int soulsPerRoom = 1;
        [Range(0, 3)] public int websPerRoom = 2;
    }

    public Palette ForTheme(CourtyardTheme theme)
    {
        int index = theme == null ? 0 : theme.usesCampaignAtlas ? (int)theme.layoutStyle + 2 : 1;
        return palettes != null && index >= 0 && index < palettes.Length ? palettes[index] : null;
    }
}
