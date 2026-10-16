using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Stable build-time asset identities shared by both copies of the game.</summary>
public sealed class LanAssetCatalog : ScriptableObject
{
    public string fingerprint;
    public Sprite[] sprites;
    public Texture2D[] textures;
    public TileBase[] tiles;
    public Material[] materials;
    public AudioClip[] audioClips;
    private readonly Dictionary<Object, int> indices = new Dictionary<Object, int>();
    public void Initialize()
    {
        indices.Clear();
        Index(sprites); Index(textures); Index(tiles); Index(materials); Index(audioClips);
    }
    private void Index<T>(T[] assets) where T : Object
    {
        if (assets == null) return;
        for (int i = 0; i < assets.Length; i++) if (assets[i] != null) indices[assets[i]] = i;
    }
    public int Id(Object asset) => asset != null && indices.TryGetValue(asset, out int index) ? index : -1;
}
