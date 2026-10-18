using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Arcane map geometry. Reads atlas UVs; static meshes are replicated by the LAN world packet.</summary>
public sealed class ArcaneCitadelGeometry : MonoBehaviour
{
    private readonly List<Mesh> ownedMeshes = new();

    public static GameObject Build(DungeonLayout layout, CourtyardTheme theme, HashSet<Vector2Int> reservedCells = null)
    {
        var root = new GameObject("Arcane Citadel - New Atlas Geometry");
        var owner = root.AddComponent<ArcaneCitadelGeometry>();
        var floor = new Batch();
        var walls = new Batch();
        var props = new Batch();
        var border = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in layout.FloorTiles.OrderBy(c => c.y).ThenBy(c => c.x))
        {
            int variant = (int)((uint)(cell.x * 73856093 ^ cell.y * 19349663) % 4);
            floor.Quad(new Rect(cell.x, cell.y, 1, 1), CellUv(variant));
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighbour = cell + new Vector2Int(x, y);
                if (!layout.FloorTiles.Contains(neighbour)) border.Add(neighbour);
            }
        }
        foreach (Vector2Int cell in border.OrderBy(c => c.y).ThenBy(c => c.x))
        {
            // One masonry scale for BOTH directions and all junction cells.
            // Separate coping follows the outer contour, including L corners;
            // no mismatched horizontal facade / vertical pillar modules.
            walls.Quad(new Rect(cell.x, cell.y, 1, 1), FromTop(38, 133, 255, 110));
            bool topEdge = Outside(cell + Vector2Int.up, layout.FloorTiles, border);
            bool bottomEdge = Outside(cell + Vector2Int.down, layout.FloorTiles, border);
            bool leftEdge = Outside(cell + Vector2Int.left, layout.FloorTiles, border);
            bool rightEdge = Outside(cell + Vector2Int.right, layout.FloorTiles, border);
            Rect coping = FromTop(39, 87, 252, 27);
            const float rim = 0.18f;
            if (topEdge) walls.Quad(new Rect(cell.x, cell.y + 1 - rim, 1, rim), coping);
            if (bottomEdge) walls.Quad(new Rect(cell.x, cell.y, 1, rim), coping);
            if (leftEdge) walls.Quad(new Rect(cell.x, cell.y, rim, 1), coping, rotateUv: true);
            if (rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y, rim, 1), coping, rotateUv: true);
            Rect corner = FromTop(92, 88, 26, 25);
            if (topEdge && leftEdge) walls.Quad(new Rect(cell.x, cell.y + 1 - rim, rim, rim), corner);
            if (topEdge && rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y + 1 - rim, rim, rim), corner);
            if (bottomEdge && leftEdge) walls.Quad(new Rect(cell.x, cell.y, rim, rim), corner);
            if (bottomEdge && rightEdge) walls.Quad(new Rect(cell.x + 1 - rim, cell.y, rim, rim), corner);
        }
        foreach (DungeonRoom room in layout.Rooms)
        {
            int left = room.FloorTiles.Min(c => c.x), right = room.FloorTiles.Max(c => c.x);
            int bottom = room.FloorTiles.Min(c => c.y), top = room.FloorTiles.Max(c => c.y);
            int cx = (left + right) / 2, cy = (bottom + top) / 2;
            // Sparse accents leave the fighting area and all corridors clear.
            floor.Quad(new Rect(cx - 1, cy - 1, 3, 3), CellUv(room.Type == RoomType.Boss ? 10 : 8));
            foreach (int x in new[] { left + 3, right - 3 })
            {
                Vector2Int at = new(x, top - 1);
                if (!room.FloorTiles.Contains(at)) continue;
                owner.SolidProp(layout, props, "Runic Pillar", 0,
                    new Rect(x - 0.3f, top - 1, 1.6f, 2.1f),
                    new Rect(x - 0.02f, top - 0.98f, 1.04f, 0.48f), reservedCells);
                // Legacy torch frames are transient Sprites: animate in Play Mode,
                // not serialized into the saved editor-only mesh preview.
                float torchX = x + 0.5f + (x < cx ? 1.6f : -1.6f);
                if (!layout.FloorTiles.Contains(new Vector2Int(Mathf.FloorToInt(torchX), top + 1)))
                {
                    if (Application.isPlaying)
                        DungeonDecorator.CreateWallTorch(new Vector3(torchX, top + 1.45f, 0), root.transform,
                            theme.arcaneTorch, new Color(0.12f, 0.9f, 1f));
                    // A wall bracket separate from the pillar avoids a floating brazier silhouette.
                    walls.Quad(new Rect(torchX - 0.08f, top + 1.13f, 0.16f, 0.33f), FromTop(664, 139, 20, 90));
                }
            }
            if (room.Type == RoomType.Treasure)
                owner.SolidProp(layout, props, "Treasure Chest", 5,
                    new Rect(cx + 3, top - 2, 1.6f, 1.6f),
                    new Rect(cx + 3.12f, top - 1.9f, 1.36f, 0.55f), reservedCells);
            if (room.Type == RoomType.Boss)
                owner.SolidProp(layout, props, "Guardian Statue", 10,
                    new Rect(cx - 0.5f, top - 2, 1.8f, 2.4f),
                    new Rect(cx - 0.32f, top - 1.9f, 1.44f, 0.55f), reservedCells);
        }
        owner.Emit("Arcane Floor", floor, theme.arcaneFloor, -1);
        owner.Emit("Arcane Walls", walls, theme.arcaneWalls, 0);
        owner.Emit("Arcane Props", props, theme.arcaneProps, 2);
        return root;
    }

    private static bool Outside(Vector2Int cell, HashSet<Vector2Int> floor, HashSet<Vector2Int> border) =>
        !floor.Contains(cell) && !border.Contains(cell);

    private void SolidProp(DungeonLayout layout, Batch batch, string label, int atlasIndex,
        Rect image, Rect feet, HashSet<Vector2Int> reservedCells)
    {
        var cells = new List<Vector2Int>();
        for (int x = Mathf.FloorToInt(feet.xMin); x <= Mathf.FloorToInt(feet.xMax - 0.001f); x++)
        for (int y = Mathf.FloorToInt(feet.yMin); y <= Mathf.FloorToInt(feet.yMax - 0.001f); y++)
        {
            var cell = new Vector2Int(x, y);
            if (!layout.FloorTiles.Contains(cell) || (reservedCells != null && reservedCells.Contains(cell))) return;
            cells.Add(cell);
        }
        batch.Quad(image, CellUv(atlasIndex));
        var blocker = new GameObject(label + " Footprint");
        blocker.transform.SetParent(transform, false);
        blocker.transform.position = new Vector3(feet.center.x, feet.center.y, 0);
        var collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = feet.size;
        // NavMeshPlus uses render geometry in this project. A modifier VOLUME is
        // collected independently of SpriteRenderer/Collider collection mode.
        var volume = blocker.AddComponent<NavMeshPlus.Components.NavMeshModifierVolume>();
        volume.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        volume.size = new Vector3(feet.width + 0.6f, feet.height + 0.6f, 4);
        if (reservedCells != null) foreach (Vector2Int cell in cells) reservedCells.Add(cell);
    }

    private static Rect CellUv(int index) => new((index % 4) * 0.25f + 0.001f,
        1f - (index / 4 + 1) * 0.25f + 0.001f, 0.248f, 0.248f);

    // The new wall sheet is 1254 square; these rectangles exclude transparent gutters.
    private static Rect FromTop(float x, float y, float width, float height) =>
        new(x / 1254f, 1f - (y + height) / 1254f, width / 1254f, height / 1254f);

    private void Emit(string label, Batch batch, Material material, int order)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label + " Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(batch.vertices);
        mesh.SetUVs(0, batch.uvs);
        mesh.SetColors(batch.colors);
        mesh.SetTriangles(batch.indices, 0);
        mesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (Application.isPlaying) ownedMeshes.Add(mesh);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
    }

    private sealed class Batch
    {
        public readonly List<Vector3> vertices = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<Color> colors = new();
        public readonly List<int> indices = new();
        public void Quad(Rect world, Rect uv, bool mirrorX = false, bool rotateUv = false)
        {
            int n = vertices.Count;
            vertices.Add(new Vector3(world.xMin, world.yMin, 0));
            vertices.Add(new Vector3(world.xMin, world.yMax, 0));
            vertices.Add(new Vector3(world.xMax, world.yMax, 0));
            vertices.Add(new Vector3(world.xMax, world.yMin, 0));
            float uLeft = mirrorX ? uv.xMax : uv.xMin, uRight = mirrorX ? uv.xMin : uv.xMax;
            if (rotateUv)
            {
                uvs.Add(new Vector2(uLeft, uv.yMax)); uvs.Add(new Vector2(uRight, uv.yMax));
                uvs.Add(new Vector2(uRight, uv.yMin)); uvs.Add(new Vector2(uLeft, uv.yMin));
            }
            else
            {
                uvs.Add(new Vector2(uLeft, uv.yMin)); uvs.Add(new Vector2(uLeft, uv.yMax));
                uvs.Add(new Vector2(uRight, uv.yMax)); uvs.Add(new Vector2(uRight, uv.yMin));
            }
            for (int i = 0; i < 4; i++) colors.Add(Color.white);
            indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
    }
}
