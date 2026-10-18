using UnityEngine;

/// <summary>Đọc sheet gốc theo ô runtime, không sửa SpriteRect/importer slicing.</summary>
[CreateAssetMenu(menuName = "The Labyrinth/Existing Enemy Sheet")]
public sealed class EnemySheetArtSO : ScriptableObject
{
    [System.Serializable]
    public struct Clip
    {
        public int row, start, count;
        public Clip(int row, int start, int count) { this.row = row; this.start = start; this.count = count; }
    }
    public Texture2D[] variants;
    public int cellSize = 192;
    public float pixelsPerUnit = 64f;
    public float framesPerSecond = 10f;
    public int attackImpactFrame = 3;
    public Clip idle, move, attack, attackDown, attackUp, reverseIdle, reverseMove;
    [System.NonSerialized] private Sprite[][] cache;

    public Sprite Frame(int variant, Clip clip, int index)
    {
        if (variants == null || variants.Length == 0 || clip.count <= 0) return null;
        variant = Mathf.Clamp(variant, 0, variants.Length - 1);
        Texture2D sheet = variants[variant];
        if (sheet == null) return null;
        if (cache == null) cache = new Sprite[variants.Length][];
        int columns = sheet.width / cellSize;
        int rows = sheet.height / cellSize;
        if (cache[variant] == null) cache[variant] = new Sprite[columns * rows];
        int column = clip.start + Mathf.Clamp(index, 0, clip.count - 1);
        if (column >= columns || clip.row >= rows || clip.row < 0) return null;
        int key = clip.row * columns + column;
        if (cache[variant][key] == null)
        {
            var sprite = Sprite.Create(sheet, new Rect(column * cellSize, sheet.height - (clip.row + 1) * cellSize,
                cellSize, cellSize), Vector2.one * 0.5f, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name + "_" + variant + "_" + key;
            sprite.hideFlags = HideFlags.DontSave;
            cache[variant][key] = sprite;
        }
        return cache[variant][key];
    }

    private void OnDisable()
    {
        if (cache == null) return;
        foreach (var row in cache)
            if (row != null) foreach (var sprite in row)
                if (sprite != null)
                {
                    if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite);
                }
        cache = null;
    }
}
