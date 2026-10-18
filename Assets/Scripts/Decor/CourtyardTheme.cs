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
    public bool UsesArcaneArt => arcaneFloor != null && arcaneWalls != null && arcaneProps != null;
}
