using UnityEngine;

/// <summary>Four-state mesh atlas presentation; damage remains owned by SpikeTrap.</summary>
public sealed class ArcaneSpikeTrapVisual : MonoBehaviour
{
    private Mesh mesh;
    public int CurrentFrame { get; private set; } = -1;
    // Measured plate bounds in the original 1254x1254 PNG, top-left coordinates.
    // Normalize each plate itself, not the 627px cell's unequal transparent margins.
    private static readonly RectInt[] PlateRects =
    {
        new(54, 61, 531, 520), new(669, 61, 532, 520),
        new(54, 668, 531, 520), new(669, 668, 533, 520)
    };
    private static readonly Vector2[][] Frames = CreateUvs();

    public void Initialize(Material material)
    {
        mesh = new Mesh { name = "Arcane Spike Atlas Quad" };
        const float halfWidth = 0.85f * 0.5f;
        const float halfHeight = 0.85f * (520f / 531f) * 0.5f;
        mesh.vertices = new[] { new Vector3(-halfWidth,-halfHeight), new Vector3(-halfWidth,halfHeight),
            new Vector3(halfWidth,halfHeight), new Vector3(halfWidth,-halfHeight) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 1;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        SetFrame(0);
    }

    public void SetFrame(int index)
    {
        if (mesh == null || CurrentFrame == index) return;
        CurrentFrame = Mathf.Clamp(index, 0, 3);
        mesh.uv = Frames[CurrentFrame];
    }

    private static Vector2[][] CreateUvs()
    {
        var result = new Vector2[4][];
        for (int i = 0; i < 4; i++)
        {
            RectInt plate = PlateRects[i];
            const float imageSize = 1254f;
            float left = (plate.xMin + 0.5f) / imageSize;
            float right = (plate.xMax - 0.5f) / imageSize;
            float bottom = 1f - (plate.yMax - 0.5f) / imageSize;
            float top = 1f - (plate.yMin + 0.5f) / imageSize;
            result[i] = new[] { new Vector2(left,bottom), new Vector2(left,top),
                new Vector2(right,top), new Vector2(right,bottom) };
        }
        return result;
    }

    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
