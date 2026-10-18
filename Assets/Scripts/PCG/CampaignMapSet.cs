using UnityEngine;

[CreateAssetMenu(menuName = "PCG/Campaign Map Set")]
public sealed class CampaignMapSet : ScriptableObject
{
    // Ô 0 cố ý để trống: level 1 giữ map gốc, không đổi thành Arcane.
    public CourtyardTheme[] maps = new CourtyardTheme[10];
    public bool TryGetTheme(int level, out CourtyardTheme theme)
    {
        theme = null;
        if (maps == null || level < 1 || level > maps.Length) return false;
        theme = maps[level - 1];
        return level == 1 || theme != null;
    }
}
