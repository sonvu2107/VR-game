using UnityEngine;

[CreateAssetMenu(menuName = "PCG/Courtyard Theme")]
public sealed class CourtyardTheme : ScriptableObject
{
    public Sprite[] storageProps;
    public Sprite[] potteryProps;
    public Sprite[] remainsProps;
    public Sprite entranceSteps;
    public Sprite[] wallDetails;
    public Sprite wallBanner;
    public Sprite[] pavingSprites;

    [Header("Optional Arcane Citadel mesh-atlas art")]
    public Material arcaneFloor;
    public Material arcaneWalls;
    public Material arcaneProps;
    public Material arcaneTorch;
    [Min(0)] public int arcaneTrapsPerCombatRoom = 2;
    public Material arcaneSpike;
    [Header("Campaign atlas 4 x 4 (optional; preserves legacy Arcane UVs)")]
    public bool usesCampaignAtlas;
    public CampaignLayoutStyle layoutStyle;
    public string mapDisplayName;
    public Color torchLightColor = new Color(0.12f, 0.9f, 1f);
    public Rect[] atlasUvs;
    public Rect AtlasUv(int index)
    {
        if (atlasUvs != null && index >= 0 && index < atlasUvs.Length) return atlasUvs[index];
        return new Rect((index % 4) * 0.25f + 0.001f, 1f - (index / 4 + 1) * 0.25f + 0.001f, 0.248f, 0.248f);
    }
    public bool UsesArcaneArt => arcaneFloor != null && arcaneWalls != null && arcaneProps != null;
}
