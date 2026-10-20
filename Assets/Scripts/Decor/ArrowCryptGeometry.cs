using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Level-three environment built from individual low-resolution images.
/// Floor and wall tiles are batched; props remain separate sprites for clean pivots.
/// </summary>
public sealed class ArrowCryptGeometry : MonoBehaviour
{
    private readonly List<Mesh> ownedMeshes = new();
    private static Sprite archeryTarget;
    private static Sprite bowRack;
    private static Sprite weaponDebris;
    private static Sprite cryptGate;

    public static GameObject Build(DungeonLayout layout, CourtyardTheme theme,
        HashSet<Vector2Int> reservedCells = null)
    {
        GameObject root = new("Arrow Crypt - Individual Image Geometry");
        ArrowCryptGeometry owner = root.AddComponent<ArrowCryptGeometry>();
        owner.EnsureSprites();

        Batch floor = new();
        Batch walls = new();
        HashSet<Vector2Int> border = new();

        foreach (Vector2Int cell in layout.FloorTiles.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
        {
            floor.Quad(new Rect(cell.x, cell.y, 1f, 1f));
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighbour = cell + new Vector2Int(x, y);
                if (!layout.FloorTiles.Contains(neighbour)) border.Add(neighbour);
            }
        }

        foreach (Vector2Int cell in border.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
            walls.Quad(new Rect(cell.x, cell.y, 1f, 1f));

        owner.Emit("Arrow Crypt Floor", floor, theme.arrowCryptFloor, -1);
        owner.Emit("Arrow Crypt Walls", walls, theme.arrowCryptWalls, 0);

        int roomIndex = 0;
        foreach (DungeonRoom room in layout.Rooms)
            owner.DecorateRoom(room, roomIndex++, root.transform, reservedCells);

        return root;
    }

    private void DecorateRoom(DungeonRoom room, int roomIndex, Transform parent,
        HashSet<Vector2Int> reservedCells)
    {
        if (room?.FloorTiles == null || room.FloorTiles.Count == 0) return;

        int left = room.FloorTiles.Min(cell => cell.x);
        int right = room.FloorTiles.Max(cell => cell.x);
        int bottom = room.FloorTiles.Min(cell => cell.y);
        int top = room.FloorTiles.Max(cell => cell.y);
        int centerX = (left + right) / 2;

        Sprite mainProp = room.Type == RoomType.Combat
            ? (roomIndex % 2 == 0 ? archeryTarget : bowRack)
            : weaponDebris;
        Vector2Int propCell = new(left + 1, bottom + 1);
        if (room.FloorTiles.Contains(propCell) && (reservedCells == null || !reservedCells.Contains(propCell)))
            SpawnSprite(parent, mainProp, new Vector3(propCell.x + 0.5f, propCell.y + 0.5f, 0f),
                mainProp == weaponDebris ? 0.78f : 0.68f, 2, "Arrow Crypt Prop");

        if (room.Type is RoomType.Start or RoomType.Exit)
            SpawnSprite(parent, cryptGate, new Vector3(centerX + 0.5f, top + 1.15f, 0f),
                0.78f, 1, room.Type + " Arrow Gate");

        Color greenFlame = new(0.48f, 0.92f, 0.36f, 1f);
        foreach (int torchX in new[] { left + 2, right - 2 }.Distinct())
        {
            if (torchX <= left || torchX >= right) continue;
            DungeonDecorator.CreateWallTorch(new Vector3(torchX + 0.5f, top + 1.15f, 0f),
                parent, null, greenFlame);
        }
    }

    private static void SpawnSprite(Transform parent, Sprite sprite, Vector3 position,
        float scale, int order, string objectName)
    {
        if (sprite == null) return;
        GameObject obj = new(objectName);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
    }

    private void EnsureSprites()
    {
        archeryTarget ??= Resources.Load<Sprite>("MapThemes/ArrowCrypt/archery_target");
        bowRack ??= Resources.Load<Sprite>("MapThemes/ArrowCrypt/bow_rack");
        weaponDebris ??= Resources.Load<Sprite>("MapThemes/ArrowCrypt/weapon_debris");
        cryptGate ??= Resources.Load<Sprite>("MapThemes/ArrowCrypt/crypt_gate");
    }

    private void Emit(string label, Batch batch, Material material, int order)
    {
        if (material == null) return;
        GameObject child = new(label);
        child.transform.SetParent(transform, false);
        Mesh mesh = new() { name = label + " Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(batch.vertices);
        mesh.SetUVs(0, batch.uvs);
        mesh.SetColors(batch.colors);
        mesh.SetTriangles(batch.indices, 0);
        mesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (Application.isPlaying) ownedMeshes.Add(mesh);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes)
            if (mesh != null) Destroy(mesh);
    }

    private sealed class Batch
    {
        public readonly List<Vector3> vertices = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<Color> colors = new();
        public readonly List<int> indices = new();

        public void Quad(Rect world)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(world.xMin, world.yMin, 0f));
            vertices.Add(new Vector3(world.xMin, world.yMax, 0f));
            vertices.Add(new Vector3(world.xMax, world.yMax, 0f));
            vertices.Add(new Vector3(world.xMax, world.yMin, 0f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            for (int index = 0; index < 4; index++) colors.Add(Color.white);
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }
}
